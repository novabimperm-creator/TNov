using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using TNovCommon;
#if NETFRAMEWORK
using System.CodeDom.Compiler;
#else
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System.Runtime.Loader;
#endif

namespace TNov.Mcp
{
    /// <summary>
    /// Инструмент execute_csharp: компиляция и запуск C#-кода, присланного Claude.
    /// Revit 2027 — Roslyn (последний C#). Revit 2022 — встроенный в .NET Framework компилятор (C# 5):
    /// Roslyn на net48 конфликтует со старыми System.Collections.Immutable/System.Memory из папки Revit.
    /// </summary>
    internal static class ScriptRunner
    {
        private const string ScriptNamespace = "TNovMcpScript";
        private const int MaxCodeInDialog = 3000;

        /// <summary>Права, для которых пользователь разрешил выполнять код без подтверждения до закрытия Revit.</summary>
        private static McpAccess.ScriptScope? _trustedScope;

        private static readonly string[] Usings =
        {
            "System", "System.Collections.Generic", "System.IO", "System.Linq", "System.Text",
            "Autodesk.Revit.DB", "Autodesk.Revit.DB.Architecture", "Autodesk.Revit.DB.Structure",
            "Autodesk.Revit.DB.Mechanical", "Autodesk.Revit.DB.Plumbing", "Autodesk.Revit.DB.Electrical",
            "Autodesk.Revit.UI"
        };

        public static object Run(UIApplication app, JObject args)
        {
            string code = args["code"]?.ToString();
            if (string.IsNullOrWhiteSpace(code)) throw new ArgumentException("Пустой код.");
            bool useTransaction = args["transaction"]?.Type == JTokenType.Boolean && (bool)args["transaction"];
            string transactionName = args["transaction_name"]?.ToString() ?? McpClient.Current.Name + ": скрипт";

            UIDocument uidoc = app.ActiveUIDocument;
            Document doc = uidoc?.Document;
            if (useTransaction && doc == null) throw new InvalidOperationException("Нет открытого документа для транзакции.");
            McpAccess.ScriptScope scope = McpAccess.ScriptScopeFor(doc);

            if (!Confirm(code, useTransaction, scope))
                return new JObject { ["declined"] = true, ["message"] = "Пользователь отклонил выполнение кода в Revit." };

            MethodInfo entry = Compile(code, out List<string> errors);
            if (entry == null)
                throw new InvalidOperationException("Ошибки компиляции (строки считаются от начала кода):\n" + string.Join("\n", errors));

            var log = new StringWriter(CultureInfo.InvariantCulture);
            var warnings = new List<string>();
            // Всё, что сделает скрипт (и его собственные транзакции), — в одной группе:
            // «сторож» проверяет изменения и при нарушении прав откатывает их целиком.
            TransactionGroup group = doc != null ? new TransactionGroup(doc, transactionName) : null;
            Transaction transaction = useTransaction ? new Transaction(doc, transactionName) : null;
            ChangeTracker tracker = doc != null ? new ChangeTracker(app.Application, doc) : null;
            object result;
            try
            {
                group?.Start();
                if (transaction != null)
                {
                    transaction.Start();
                    McpTools.SuppressWarnings(transaction, warnings);
                }

                result = entry.Invoke(null, new object[] { app, uidoc, doc, log });

                if (transaction != null && transaction.GetStatus() == TransactionStatus.Started)
                {
                    TransactionStatus status = transaction.Commit();
                    if (status != TransactionStatus.Committed)
                        throw new InvalidOperationException("Транзакция не зафиксирована: " + status + ". " + string.Join(" ", warnings));
                }

                string violation = tracker?.Violation(scope);
                if (violation != null)
                {
                    group.RollBack();
                    throw new InvalidOperationException(
                        "Изменения отменены: " + violation + ".\n" +
                        "Права в этой модели: " + ScopeText(scope) + "." + Output(log));
                }
                if (group != null && group.GetStatus() == TransactionStatus.Started) group.Assimilate();
            }
            catch (TargetInvocationException ex) when (ex.InnerException != null)
            {
                RollBack(transaction, group);
                Exception inner = ex.InnerException;
                throw new InvalidOperationException(
                    $"Исключение при выполнении: {inner.GetType().FullName}: {inner.Message}\n{ScriptStack(inner)}" +
                    Output(log) + (doc != null ? "\nВсе изменения скрипта отменены." : ""));
            }
            catch (Exception)
            {
                RollBack(transaction, group);
                throw;
            }
            finally
            {
                tracker?.Dispose();
                transaction?.Dispose();
                group?.Dispose();
            }

            var response = new JObject { ["result"] = ToJson(result, 0) };
            string output = log.ToString();
            if (output.Length > 0) response["log"] = output;
            if (warnings.Count > 0) response["warnings"] = new JArray(warnings.Distinct());
            if (tracker != null && tracker.Total > 0)
                response["changes"] = new JObject { ["added"] = tracker.Added.Count, ["modified"] = tracker.Modified.Count, ["deleted"] = tracker.Deleted.Count };
            return response;
        }

        public static string ScopeText(McpAccess.ScriptScope scope)
        {
            switch (scope)
            {
                case McpAccess.ScriptScope.Full:
                    return "без ограничений";
                case McpAccess.ScriptScope.Model50:
                    return $"аннотации, виды, листы, экспорт и до {McpAccess.Level2MaxChanges} 3D-элементов за запрос; без изменения типоразмеров и без удаления";
                default:
                    return "чтение, аннотации, виды, листы, спецификации, изображения, экспорт; без изменения 3D-элементов и их типоразмеров, без удаления";
            }
        }

        /// <summary>Копит изменения документа, пока выполняется скрипт (DocumentChanged приходит при каждой фиксации).</summary>
        private sealed class ChangeTracker : IDisposable
        {
            private readonly Autodesk.Revit.ApplicationServices.Application _app;
            private readonly Document _doc;
            public readonly HashSet<long> Added = new HashSet<long>();
            public readonly HashSet<long> Modified = new HashSet<long>();
            public readonly HashSet<long> Deleted = new HashSet<long>();
            private bool _otherDocumentChanged;

            public ChangeTracker(Autodesk.Revit.ApplicationServices.Application app, Document doc)
            {
                _app = app;
                _doc = doc;
                _app.DocumentChanged += OnChanged;
            }

            public int Total => Added.Count + Modified.Count + Deleted.Count;

            private void OnChanged(object sender, Autodesk.Revit.DB.Events.DocumentChangedEventArgs e)
            {
                if (!e.GetDocument().Equals(_doc))
                {
                    _otherDocumentChanged = true;
                    return;
                }
                foreach (ElementId id in e.GetAddedElementIds()) Added.Add(id.LongValue());
                foreach (ElementId id in e.GetModifiedElementIds())
                {
                    long v = id.LongValue();
                    if (!Added.Contains(v)) Modified.Add(v);
                }
                foreach (ElementId id in e.GetDeletedElementIds())
                {
                    long v = id.LongValue();
                    // Созданный и удалённый в том же скрипте элемент — не удаление существующего.
                    if (Added.Remove(v)) continue;
                    Modified.Remove(v);
                    Deleted.Add(v);
                }
            }

            /// <summary>Описание нарушения прав или null. Вызывать до отката группы: элементы ещё в модели.</summary>
            public string Violation(McpAccess.ScriptScope scope)
            {
                if (scope == McpAccess.ScriptScope.Full) return null;
                if (_otherDocumentChanged) return "скрипт изменил другой документ — это доступно только на уровне 3";
                if (Deleted.Count > 0) return $"скрипт удалил существующие элементы ({Deleted.Count} шт.) — удаление доступно только на уровне 3";

                // Типоразмеры 3D-элементов (переименование, параметры типа) меняют все экземпляры — только уровень 3.
                // Новые типы (загрузка семейства для марок и т.п.) разрешены.
                List<Element> types = Modified
                    .Select(v => _doc.GetElement(ElementIdCompat.ToElementId(v)))
                    .Where(IsModelType)
                    .ToList();
                if (types.Count > 0)
                    return $"скрипт изменил типоразмеры 3D-элементов ({types.Count} шт.: {Examples(types)}). " +
                           "Изменение типов, включая переименование, доступно только на уровне 3";

                List<Element> model = Added.Concat(Modified)
                    .Select(v => _doc.GetElement(ElementIdCompat.ToElementId(v)))
                    .Where(IsModelInstance)
                    .ToList();
                if (model.Count == 0) return null;

                string examples = Examples(model);
                if (scope == McpAccess.ScriptScope.Annotations)
                    return $"скрипт создал или изменил 3D-элементы ({model.Count} шт.: {examples}). " +
                           "На этом уровне и в этой модели скрипты не меняют 3D-элементы; для параметров и перемещения используйте инструменты уровня 2, если они доступны";
                if (model.Count > McpAccess.Level2MaxChanges)
                    return $"скрипт затронул {model.Count} 3D-элементов при лимите {McpAccess.Level2MaxChanges} за запрос (примеры: {examples}). Разбейте работу на части";
                return null;
            }

            /// <summary>3D-экземпляр модели: не тип, не вид, категория модели, не элемент вида и не компонент легенды.</summary>
            private static bool IsModelInstance(Element e) =>
                e != null && !(e is ElementType) && !(e is View) && IsModelCategory(e) && !e.ViewSpecific;

            /// <summary>Типоразмер категории модели (стены, семейства и т.п.).</summary>
            private static bool IsModelType(Element e) => e is ElementType && IsModelCategory(e);

            private static bool IsModelCategory(Element e) =>
                e.Category != null && e.Category.CategoryType == CategoryType.Model
                // Компонент легенды — оформление листа, хотя категория у него модельная.
                && e.Category.Id.LongValue() != (long)BuiltInCategory.OST_LegendComponents;

            private static string Examples(List<Element> elements) =>
                string.Join(", ", elements.Take(5).Select(e => $"{e.Category.Name} «{McpTools.SafeName(e)}» {e.Id.LongValue()}"));

            public void Dispose() => _app.DocumentChanged -= OnChanged;
        }

        private static void RollBack(Transaction transaction, TransactionGroup group)
        {
            if (transaction != null && transaction.GetStatus() == TransactionStatus.Started)
                transaction.RollBack();
            if (group != null && group.GetStatus() == TransactionStatus.Started)
                group.RollBack();
        }

        private static string Output(StringWriter log)
        {
            string s = log.ToString();
            return s.Length == 0 ? "" : "\nВывод log до исключения:\n" + s;
        }

        /// <summary>Только кадры кода скрипта — внутренности Revit Claude не помогут.</summary>
        private static string ScriptStack(Exception ex)
        {
            IEnumerable<string> lines = (ex.StackTrace ?? "").Split('\n').Where(l => l.Contains(ScriptNamespace));
            return string.Join("\n", lines).Trim();
        }

        private static bool Confirm(string code, bool useTransaction, McpAccess.ScriptScope scope)
        {
            // Доверие «до закрытия Revit» действует только для прав не шире тех, при которых его дали.
            if (_trustedScope != null && scope <= _trustedScope.Value) return true;

            string shown = code.Length > MaxCodeInDialog ? code.Substring(0, MaxCodeInDialog) + "\n…" : code;
            var dialog = new TaskDialog("TNov — " + McpClient.Current.Name)
            {
                MainIcon = TaskDialogIcon.TaskDialogIconWarning,
                MainInstruction = McpClient.Current.Name + " хочет выполнить код в Revit",
                MainContent = "Права скрипта: " + ScopeText(scope) + ". Изменения сверх прав будут отменены целиком.\n" +
                    "Результат можно отменить Ctrl+Z.\n\n" + shown,
                CommonButtons = TaskDialogCommonButtons.Cancel,
                DefaultButton = TaskDialogResult.Cancel
            };
            dialog.AddCommandLink(TaskDialogCommandLinkId.CommandLink1, "Выполнить");
            dialog.AddCommandLink(TaskDialogCommandLinkId.CommandLink2, "Выполнять без подтверждения", "До закрытия Revit");

            switch (dialog.Show())
            {
                case TaskDialogResult.CommandLink1:
                    return true;
                case TaskDialogResult.CommandLink2:
                    _trustedScope = scope;
                    return true;
                default:
                    return false;
            }
        }

        private static string WrapSource(string body)
        {
            return string.Join("\n", Usings.Select(u => "using " + u + ";")) + "\n" +
                   "namespace " + ScriptNamespace + "\n{\n" +
                   "public static class Script\n{\n" +
                   "public static object Run(UIApplication uiapp, UIDocument uidoc, Document doc, TextWriter log)\n{\n" +
                   "#line 1\n" + body + "\n" +
                   "}\n}\n}\n";
        }

        private static MethodInfo Compile(string body, out List<string> errors)
        {
            Assembly assembly = CompileSource(WrapSource(body), out errors);
            // Код без return в конце — частый случай; добавляем return null сами.
            if (assembly == null && errors.Any(e => e.Contains("CS0161")))
                assembly = CompileSource(WrapSource(body + "\nreturn null;"), out errors);
            return assembly?.GetType(ScriptNamespace + ".Script")?.GetMethod("Run");
        }

        private static IEnumerable<string> ReferencePaths()
        {
            var byName = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (Assembly a in AppDomain.CurrentDomain.GetAssemblies())
            {
                string location;
                try { location = a.IsDynamic ? null : a.Location; }
                catch (Exception) { continue; }
                if (string.IsNullOrEmpty(location)) continue;
                string name = a.GetName().Name;
                if (name.StartsWith(ScriptNamespace, StringComparison.Ordinal) || byName.ContainsKey(name)) continue;
                byName[name] = location;
            }
#if !NETFRAMEWORK
            // Сборки рантайма .NET, которые ещё не загружены (System.Runtime, System.Linq и т.п.).
            if (AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") is string tpa)
            {
                foreach (string path in tpa.Split(Path.PathSeparator))
                {
                    string name = Path.GetFileNameWithoutExtension(path);
                    if (!byName.ContainsKey(name)) byName[name] = path;
                }
            }
#endif
            return byName.Values;
        }

#if NETFRAMEWORK
        private static Assembly CompileSource(string source, out List<string> errors)
        {
            using (var provider = new Microsoft.CSharp.CSharpCodeProvider())
            {
                var parameters = new CompilerParameters
                {
                    GenerateInMemory = true,
                    GenerateExecutable = false,
                    CompilerOptions = "/optimize"
                };
                foreach (string path in ReferencePaths())
                {
                    if (!Path.GetFileName(path).Equals("mscorlib.dll", StringComparison.OrdinalIgnoreCase))
                        parameters.ReferencedAssemblies.Add(path);
                }

                CompilerResults results = provider.CompileAssemblyFromSource(parameters, source);
                errors = results.Errors.Cast<CompilerError>()
                    .Where(e => !e.IsWarning)
                    .Select(e => $"({e.Line},{e.Column}) {e.ErrorNumber}: {e.ErrorText}")
                    .ToList();
                return errors.Count == 0 ? results.CompiledAssembly : null;
            }
        }
#else
        private static readonly Dictionary<string, MetadataReference> _referenceCache =
            new Dictionary<string, MetadataReference>(StringComparer.OrdinalIgnoreCase);

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static Assembly CompileSource(string source, out List<string> errors)
        {
            var references = new List<MetadataReference>();
            foreach (string path in ReferencePaths())
            {
                if (!_referenceCache.TryGetValue(path, out MetadataReference reference))
                {
                    try { reference = MetadataReference.CreateFromFile(path); }
                    catch (Exception) { reference = null; } // не .NET-сборка
                    _referenceCache[path] = reference;
                }
                if (reference != null) references.Add(reference);
            }

            SyntaxTree tree = CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(LanguageVersion.Latest));
            CSharpCompilation compilation = CSharpCompilation.Create(
                ScriptNamespace + "_" + Guid.NewGuid().ToString("N"),
                new[] { tree },
                references,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, optimizationLevel: OptimizationLevel.Release));

            using (var stream = new MemoryStream())
            {
                var emit = compilation.Emit(stream);
                errors = emit.Diagnostics
                    .Where(d => d.Severity == DiagnosticSeverity.Error)
                    .Select(d =>
                    {
                        var position = d.Location.GetMappedLineSpan().StartLinePosition;
                        return $"({position.Line + 1},{position.Character + 1}) {d.Id}: {d.GetMessage(CultureInfo.GetCultureInfo("ru-RU"))}";
                    })
                    .ToList();
                if (!emit.Success) return null;

                stream.Position = 0;
                // В контекст загрузки плагина: скрипту доступны те же зависимости, что и TNov.
                AssemblyLoadContext context = AssemblyLoadContext.GetLoadContext(typeof(ScriptRunner).Assembly) ?? AssemblyLoadContext.Default;
                return context.LoadFromStream(stream);
            }
        }
#endif

        #region Результат в JSON

        private const int MaxDepth = 4;
        private const int MaxItems = 2000;

        private static JToken ToJson(object value, int depth)
        {
            switch (value)
            {
                case null:
                    return JValue.CreateNull();
                case string s:
                    return s;
                case bool _:
                case char _:
                case byte _: case sbyte _: case short _: case ushort _:
                case int _: case uint _: case long _: case ulong _:
                case float _: case double _: case decimal _:
                case DateTime _:
                    return new JValue(value);
                case Enum e:
                    return e.ToString();
                case Guid g:
                    return g.ToString();
                case JToken token:
                    return token;
                case ElementId id:
                    return id.LongValue();
                case Element element:
                    return McpTools.Summary(element);
                case XYZ xyz:
                    return new JArray(Math.Round(xyz.X, 6), Math.Round(xyz.Y, 6), Math.Round(xyz.Z, 6));
                case Parameter p:
                    return new JObject { ["name"] = p.Definition?.Name, ["value"] = McpTools.DisplayValue(p) };
            }

            if (depth >= MaxDepth) return value.ToString();

            if (value is IDictionary dictionary)
            {
                var o = new JObject();
                foreach (DictionaryEntry entry in dictionary)
                {
                    if (o.Count >= MaxItems) break;
                    o[Convert.ToString(entry.Key, CultureInfo.InvariantCulture) ?? ""] = ToJson(entry.Value, depth + 1);
                }
                return o;
            }

            if (value is IEnumerable enumerable)
            {
                var array = new JArray();
                foreach (object item in enumerable)
                {
                    if (array.Count >= MaxItems)
                    {
                        array.Add($"… обрезано до {MaxItems} элементов");
                        break;
                    }
                    array.Add(ToJson(item, depth + 1));
                }
                return array;
            }

            Type type = value.GetType();
            // Анонимные типы и простые классы скрипта — по свойствам; объекты Revit API — краткой строкой.
            bool isRevitApi = type.Namespace != null && type.Namespace.StartsWith("Autodesk.", StringComparison.Ordinal);
            if (isRevitApi && depth > 0) return value.ToString();

            var obj = new JObject();
            foreach (PropertyInfo property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (property.GetIndexParameters().Length > 0) continue;
                try { obj[property.Name] = ToJson(property.GetValue(value), depth + 1); }
                catch (Exception) { }
            }
            return obj.Count > 0 ? (JToken)obj : value.ToString();
        }

        #endregion
    }
}
