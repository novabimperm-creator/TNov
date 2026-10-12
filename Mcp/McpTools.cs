using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using TNovCommon;

namespace TNov.Mcp
{
    /// <summary>
    /// Инструменты MCP: описание (tools/list) и выполнение (tools/call).
    /// Тела инструментов выполняются на потоке Revit через RevitDispatcher.
    /// </summary>
    internal static partial class McpTools
    {
        private class Tool
        {
            public string Name;
            public string Title;
            public string Description;
            public JObject Schema;
            public bool ReadOnly;
            /// <summary>Минимальный уровень доступа (McpAccess).</summary>
            public int Level = 1;
            /// <summary>false — инструмент не трогает Revit (база знаний) и выполняется вне его потока.</summary>
            public bool NeedsRevit = true;
            public TimeSpan Timeout = TimeSpan.FromMinutes(2);
            public Func<UIApplication, JObject, object> Run;
        }

        private const int MaxElementsPerCall = 2000;

        private static readonly List<Tool> _tools = new List<Tool>
        {
            new Tool
            {
                Name = "get_document_info",
                Title = "Сведения о документе",
                Description = "Активный документ Revit: имя, путь, активный вид, уровни, стадии, сведения о проекте, связи, все открытые документы.",
                Schema = Schema(),
                ReadOnly = true,
                Run = GetDocumentInfo
            },
            new Tool
            {
                Name = "list_categories",
                Title = "Категории модели",
                Description = "Категории, в которых есть экземпляры элементов, с количеством. Возвращает имя категории в интерфейсе и BuiltInCategory (OST_...).",
                Schema = Schema(null,
                    Prop("category_type", "string", "Model | Annotation | AnalyticalModel | Internal. Если не задано — все.")),
                ReadOnly = true,
                Run = ListCategories
            },
            new Tool
            {
                Name = "find_elements",
                Title = "Поиск элементов",
                Description =
                    "Поиск элементов по категории, классу API, виду и значениям параметров. " +
                    "Возвращает total (всего совпало) и страницу элементов: id, имя, категория, тип, уровень и запрошенные поля. " +
                    "Фильтр по параметру смотрит в экземпляр, а если там параметра нет — в тип. " +
                    "Операции: equals, not_equals, contains, not_contains, starts_with, greater, less, greater_or_equal, less_or_equal, exists, is_empty, is_not_empty. " +
                    "Числа сравниваются в единицах проекта.",
                Schema = Schema(null,
                    Prop("category", "string", "Категория: имя в интерфейсе (\"Стены\") или BuiltInCategory (\"OST_Walls\")."),
                    Prop("class_name", "string", "Класс Revit API, например Wall, FamilyInstance, Room, ViewSheet, Level."),
                    Prop("element_kind", "string", "instances (по умолчанию) или types — искать типоразмеры."),
                    Prop("view_id", "integer", "Только элементы, видимые на этом виде."),
                    Prop("active_view_only", "boolean", "Только элементы, видимые на активном виде."),
                    Prop("name_contains", "string", "Подстрока в имени элемента (без учёта регистра)."),
                    FiltersProperty(),
                    StringArray("fields", "Параметры, значения которых добавить в результат."),
                    Prop("limit", "integer", "Сколько элементов вернуть (по умолчанию 100, максимум 2000)."),
                    Prop("offset", "integer", "Пропустить столько элементов (постраничный вывод).")),
                ReadOnly = true,
                Run = FindElements
            },
            new Tool
            {
                Name = "get_element_parameters",
                Title = "Параметры элементов",
                Description = "Все параметры указанных элементов: имя, значение в единицах проекта, тип хранения, только чтение. По желанию — и параметры типа.",
                Schema = Schema(new[] { "ids" },
                    IntArray("ids", "Id элементов (до 50)."),
                    Prop("include_type", "boolean", "Добавить параметры типа."),
                    StringArray("names", "Вернуть только эти параметры.")),
                ReadOnly = true,
                Run = GetElementParameters
            },
            new Tool
            {
                Name = "set_parameters",
                Title = "Изменение параметров",
                Description =
                    "Записывает значения параметров в одной транзакции (отменяется Ctrl+Z). " +
                    "Значение — строка в единицах проекта, как ввёл бы пользователь в свойствах: \"3000\", \"Да\", \"Текст\". " +
                    "Для параметра типа передайте id типа. Предупреждения Revit подавляются и возвращаются в ответе.",
                Schema = Schema(new[] { "changes" },
                    new JProperty("changes", new JObject
                    {
                        ["type"] = "array",
                        ["items"] = Schema(new[] { "id", "parameter", "value" },
                            Prop("id", "integer", "Id элемента."),
                            Prop("parameter", "string", "Имя параметра или BuiltInParameter."),
                            Prop("value", "string", "Новое значение."))
                    }),
                    Prop("transaction_name", "string", "Имя транзакции в журнале отмены.")),
                Level = 2,
                Run = SetParameters
            },
            new Tool
            {
                Name = "get_selection",
                Title = "Текущее выделение",
                Description = "Элементы, выделенные пользователем в Revit.",
                Schema = Schema(),
                ReadOnly = true,
                Run = GetSelection
            },
            new Tool
            {
                Name = "set_selection",
                Title = "Выделить элементы",
                Description = "Выделяет элементы в Revit; show=true дополнительно показывает их на виде (зум).",
                Schema = Schema(new[] { "ids" },
                    IntArray("ids", "Id элементов. Пустой список снимает выделение."),
                    Prop("show", "boolean", "Показать элементы на виде.")),
                Run = SetSelection
            },
            new Tool
            {
                Name = "isolate_elements",
                Title = "Временно изолировать/скрыть",
                Description = "Временная изоляция или скрытие элементов на активном виде (как «очки» Revit). mode=reset снимает режим.",
                Schema = Schema(new[] { "mode" },
                    Prop("mode", "string", "isolate | hide | reset"),
                    IntArray("ids", "Id элементов (для isolate/hide).")),
                Run = IsolateElements
            },
            new Tool
            {
                Name = "list_views",
                Title = "Виды и листы",
                Description = "Список видов, листов, спецификаций или шаблонов видов.",
                Schema = Schema(null,
                    Prop("kind", "string", "views (по умолчанию) | sheets | schedules | templates | all"),
                    Prop("name_contains", "string", "Подстрока в имени (для листов — также в номере)."),
                    Prop("limit", "integer", "По умолчанию 200.")),
                ReadOnly = true,
                Run = ListViews
            },
            new Tool
            {
                Name = "open_view",
                Title = "Открыть вид",
                Description = "Делает вид или лист активным в Revit.",
                Schema = Schema(new[] { "view_id" }, Prop("view_id", "integer", "Id вида или листа.")),
                Run = OpenView
            },
            new Tool
            {
                Name = "export_view_image",
                Title = "Изображение вида",
                Description = "PNG-изображение вида или листа, чтобы увидеть модель. По умолчанию — активный вид целиком.",
                Schema = Schema(null,
                    Prop("view_id", "integer", "Id вида или листа. Если не задан — активный вид."),
                    Prop("pixel_size", "integer", "Ширина изображения в пикселях, 256–4096 (по умолчанию 1600)."),
                    Prop("visible_region", "boolean", "Только видимая на экране область активного вида.")),
                ReadOnly = true,
                Run = ExportViewImage
            },
            new Tool
            {
                Name = "execute_csharp",
                Title = "Выполнить C#",
                Description =
                    "Универсальный инструмент: компилирует и выполняет C#-код против Revit API. Им решаются задачи, для которых нет отдельного инструмента: " +
                    "разбивка спецификаций по листам, размещение видов на листах, марки и аннотации, проверки высот и расстояний, " +
                    "подсчёты арматуры и объёмов по всей модели без лимита элементов, импорт изображений на виды, экспорт, поиск пересечений. " +
                    "Код — тело метода `object Run(UIApplication uiapp, UIDocument uidoc, Document doc, TextWriter log)`; " +
                    "верните результат через return (сериализуется в JSON; Element → краткое описание, XYZ → [x,y,z] во футах), " +
                    "промежуточный вывод — log.WriteLine(...). " +
                    "Подключены using: System, System.Linq, System.Collections.Generic, System.Text, System.IO, Autodesk.Revit.DB (+ Architecture, Structure, Mechanical, Plumbing, Electrical), Autodesk.Revit.UI. " +
                    "Изменения модели требуют транзакции: transaction=true или своя Transaction в коде. " +
                    "ПРАВА: всё, что сделал скрипт, проверяется после выполнения и откатывается целиком, если вышло за права модели. " +
                    "Уровень 1 — можно создавать и менять аннотации, виды, листы, видовые экраны, спецификации, изображения, загружать семейства, экспортировать; нельзя менять 3D-элементы и их типоразмеры (включая переименование), нельзя удалять." +
                    "Уровень 2 в отсоединённой модели — плюс до 50 3D-элементов за запрос, без изменения типоразмеров и удаления (в локальной копии хранилища скрипт остаётся на правах уровня 1). " +
                    "Уровень 3 — без ограничений. Права в текущей модели показывает get_document_info (accessLevel). " +
                    "Пользователь подтверждает запуск в Revit. " +
                    "Длины в Revit API во внутренних единицах (футы): UnitUtils.ConvertToInternalUnits(мм, UnitTypeId.Millimeters). " +
                    "Не открывайте окна и не вызывайте PickObject — пользователь не у экрана. " +
                    "В Revit 2022 доступен только C# 5 (без $\"...\", ?., =>-членов, кортежей); в Revit 2027 — последняя версия C#.",
                Schema = Schema(new[] { "code" },
                    Prop("code", "string", "Тело метода на C#."),
                    Prop("transaction", "boolean", "Выполнить в одной транзакции (откат при исключении)."),
                    Prop("transaction_name", "string", "Имя операции в журнале отмены.")),
                Timeout = TimeSpan.FromMinutes(10),
                Run = (app, args) => ScriptRunner.Run(app, args)
            },
            new Tool
            {
                Name = "summarize_elements",
                Title = "Сводка по элементам",
                Description =
                    "Сводка по всем подходящим элементам без лимита количества: число элементов и суммы числовых параметров с группировкой. " +
                    "Для сбора объёмов, длин, масс, количества арматуры по конструкциям. Отбор — как в find_elements (category, class_name, filters). " +
                    "Суммы — в единицах проекта.",
                Schema = Schema(null,
                    Prop("category", "string", "Категория: имя в интерфейсе или BuiltInCategory."),
                    Prop("class_name", "string", "Класс Revit API."),
                    Prop("element_kind", "string", "instances (по умолчанию) или types."),
                    Prop("view_id", "integer", "Только элементы, видимые на этом виде."),
                    FiltersProperty(),
                    StringArray("group_by", "Параметры для группировки (например «Тип», «Уровень», «Марка конструкции»)."),
                    StringArray("sum", "Числовые параметры для суммирования (например «Объем», «Длина», «Масса»).")),
                ReadOnly = true,
                Timeout = TimeSpan.FromMinutes(10),
                Run = SummarizeElements
            },
            new Tool
            {
                Name = "export_document",
                Title = "Экспорт",
                Description =
                    "Экспорт активной модели или видов/листов в файлы: ifc, nwc (нужен Navisworks Exporter), dwg, pdf (только Revit 2022+), fbx (3D-вид). " +
                    "Возвращает список созданных файлов.",
                Schema = Schema(new[] { "format", "folder" },
                    Prop("format", "string", "ifc | nwc | dwg | pdf | fbx"),
                    Prop("folder", "string", "Папка для файлов (будет создана)."),
                    Prop("file_name", "string", "Имя файла без расширения (по умолчанию — имя модели)."),
                    IntArray("view_ids", "Виды или листы (для dwg, pdf, fbx; для nwc/ifc — один 3D-вид, по умолчанию вся модель).")),
                ReadOnly = true,
                Timeout = TimeSpan.FromMinutes(10),
                Run = ExportDocument
            },
            new Tool
            {
                Name = "wiki_search",
                Title = "Поиск в базе знаний",
                Description =
                    "Поиск статей в базе знаний TNov (tnov.pm-nova.ru/wiki): стандарты и методики моделирования Новации, оформление, работа функций плагина TNov. " +
                    "Используйте, когда вопрос касается принятых в компании правил или функций TNov. Затем откройте статью через wiki_read.",
                Schema = Schema(new[] { "query" },
                    Prop("query", "string", "Слова для поиска в заголовках статей."),
                    Prop("limit", "integer", "Сколько статей вернуть (по умолчанию 10).")),
                ReadOnly = true,
                NeedsRevit = false,
                Timeout = TimeSpan.FromSeconds(40),
                Run = (app, args) => WikiClient.Search(Str(args, "query"), Int(args, "limit", 10))
            },
            new Tool
            {
                Name = "wiki_read",
                Title = "Статья базы знаний",
                Description = "Текст статьи базы знаний TNov по slug из wiki_search.",
                Schema = Schema(new[] { "slug" }, Prop("slug", "string", "Идентификатор статьи.")),
                ReadOnly = true,
                NeedsRevit = false,
                Timeout = TimeSpan.FromSeconds(40),
                Run = (app, args) => WikiClient.Read(Str(args, "slug"))
            },
            new Tool
            {
                Name = "place_family_instance",
                Title = "Разместить элемент",
                Description =
                    "Размещает экземпляры типоразмера семейства по точкам (мм, координаты проекта Revit), до 50 за запрос. " +
                    "Для элементов на основе (двери, окна) укажите host_id. Работает в одной транзакции (Ctrl+Z).",
                Schema = Schema(new[] { "type_id", "points" },
                    Prop("type_id", "integer", "Id типоразмера (FamilySymbol), найдите через find_elements с element_kind=types."),
                    new JProperty("points", new JObject
                    {
                        ["type"] = "array",
                        ["description"] = "Точки вставки [x, y, z] в мм.",
                        ["items"] = new JObject { ["type"] = "array", ["items"] = new JObject { ["type"] = "number" } }
                    }),
                    Prop("level_id", "integer", "Уровень (по умолчанию — уровень активного вида или нижний)."),
                    Prop("host_id", "integer", "Основа для элементов на основе (стена и т.п.).")),
                Level = 2,
                Run = PlaceFamilyInstances
            },
            new Tool
            {
                Name = "move_elements",
                Title = "Переместить элементы",
                Description = "Перемещает элементы на вектор (мм), до 50 за запрос, в одной транзакции (Ctrl+Z).",
                Schema = Schema(new[] { "ids", "vector" },
                    IntArray("ids", "Id элементов."),
                    new JProperty("vector", new JObject
                    {
                        ["type"] = "array",
                        ["description"] = "Смещение [dx, dy, dz] в мм.",
                        ["items"] = new JObject { ["type"] = "number" }
                    })),
                Level = 2,
                Run = MoveElements
            },
            new Tool
            {
                Name = "delete_elements",
                Title = "Удалить элементы",
                Description = "Удаляет элементы в одной транзакции (Ctrl+Z). Возвращает, сколько элементов удалено вместе с зависимыми.",
                Schema = Schema(new[] { "ids" }, IntArray("ids", "Id элементов.")),
                Level = 3,
                Run = DeleteElements
            },
        };

        #region Протокол

        public static JArray List()
        {
            int level = McpAccess.Level;
            return new JArray(_tools.Where(t => t.Level <= level).Select(t => new JObject
            {
                ["name"] = t.Name,
                ["title"] = t.Title,
                ["description"] = t.Level == 2
                    ? t.Description + $" Уровень 2: не больше {McpAccess.Level2MaxChanges} элементов за запрос, параметры типов — только на уровне 3." +
                      (McpAccess.IsCorp ? "" : " В личной конфигурации — только в отсоединённых моделях.")
                    : t.Level == 3 && !McpAccess.IsCorp
                    ? t.Description + " В личной конфигурации — только в отсоединённых моделях."
                    : t.Description,
                ["inputSchema"] = t.Schema,
                ["annotations"] = new JObject
                {
                    ["title"] = t.Title,
                    ["readOnlyHint"] = t.ReadOnly,
                    ["destructiveHint"] = t.Name == "delete_elements",
                    ["openWorldHint"] = !t.NeedsRevit
                }
            }));
        }

        public static JObject Call(JObject parameters)
        {
            string name = (string)parameters?["name"];
            JObject args = parameters?["arguments"] as JObject ?? new JObject();
            Tool tool = _tools.FirstOrDefault(t => t.Name == name);
            if (tool == null) return TextResult("Неизвестный инструмент: " + name, true);
            int level = McpAccess.Level;
            var context = new McpLog.Context { EffectiveLevel = level };
            if (tool.Level > level)
            {
                McpLog.Write(name, args, context, "отказ: уровень пользователя " + level, null);
                return TextResult(
                    $"Инструмент {name} требует уровня доступа {tool.Level}, выбран {McpAccess.LevelName(level)}." +
                    (McpAccess.MaxLevel >= tool.Level ? " Пользователь может переключить уровень в окне кнопки Claude, затем начать новый сеанс." : ""), true);
            }
            string scriptFile = name == "execute_csharp" ? McpLog.SaveScript(args["code"]?.ToString()) : null;

            try
            {
                object result = tool.NeedsRevit
                    ? RevitDispatcher.Invoke(app =>
                    {
                        Document doc = app.ActiveUIDocument?.Document;
                        CaptureContext(app, doc, context);
                        if (tool.Level > context.EffectiveLevel)
                        {
                            throw new InvalidOperationException(
                                $"Инструмент {name} требует уровня {tool.Level}, а в этой модели действует уровень {context.EffectiveLevel}: " +
                                McpAccess.RestrictionReason(doc) + ".");
                        }
                        return tool.Run(app, args);
                    }, tool.Timeout)
                    : tool.Run(null, args);

                JArray content = result as JArray
                    ?? new JArray(new JObject { ["type"] = "text", ["text"] = result as string ?? JsonConvert.SerializeObject(result, Formatting.None) });

                string outcome = "ok";
                if (result is JObject json && json["changes"] is JObject changes)
                    outcome += $" (+{changes["added"]} ~{changes["modified"]} -{changes["deleted"]})";
                if (result is JObject declined && declined["declined"] != null) outcome = "отклонено пользователем";
                McpLog.Write(name, args, context, outcome, scriptFile);

                McpActivity.OnToolCall(name, null);
                int? raised = McpAccess.RegisterSuccessfulCall();
                if (raised != null)
                {
                    content.Add(new JObject
                    {
                        ["type"] = "text",
                        ["text"] = $"Уровень доступа пользователя повышен до {McpAccess.LevelName(raised.Value)}. " +
                                   "Сообщите пользователю: новые инструменты появятся после переподключения коннектора (команда /mcp или новый сеанс)."
                    });
                }
                return new JObject { ["content"] = content, ["isError"] = false };
            }
            catch (Exception ex)
            {
                McpLog.Write(name, args, context, "ошибка: " + ex.Message, scriptFile);
                McpActivity.OnToolCall(name, ex.Message);
                return TextResult(ex.Message, true);
            }
        }

        private static void CaptureContext(UIApplication app, Document doc, McpLog.Context context)
        {
            context.Kind = McpAccess.ModelKind(doc);
            context.EffectiveLevel = McpAccess.EffectiveLevel(doc);
            // Как в журнале usage: имя локальной копии без суффикса «_пользователь».
            if (doc != null) context.Model = doc.Title.Replace("_" + app.Application.Username, "");
            McpActivity.OnModel(context.Model, context.Kind, context.EffectiveLevel);
        }

        private static JObject TextResult(string text, bool isError) => new JObject
        {
            ["content"] = new JArray(new JObject { ["type"] = "text", ["text"] = text }),
            ["isError"] = isError
        };

        #endregion

        #region Инструменты

        private static object GetDocumentInfo(UIApplication app, JObject args)
        {
            UIDocument uidoc = ActiveUiDoc(app);
            Document doc = uidoc.Document;

            JObject projectInfo = null;
            ProjectInfo pi = doc.ProjectInformation;
            if (pi != null)
            {
                projectInfo = new JObject
                {
                    ["name"] = pi.Name,
                    ["number"] = pi.Number,
                    ["client"] = pi.ClientName,
                    ["building"] = pi.BuildingName,
                    ["address"] = pi.Address,
                    ["status"] = pi.Status
                };
            }

            return new JObject
            {
                ["revit"] = app.Application.VersionName,
                ["user"] = app.Application.Username,
                ["title"] = doc.Title,
                ["path"] = doc.PathName,
                ["isFamilyDocument"] = doc.IsFamilyDocument,
                ["isWorkshared"] = doc.IsWorkshared,
                ["lengthUnits"] = LabelUtils.GetLabelForUnit(doc.GetUnits().GetFormatOptions(SpecTypeId.Length).GetUnitTypeId()),
                ["activeView"] = ViewInfo(uidoc.ActiveView),
                ["access"] = new JObject
                {
                    ["configuration"] = McpAccess.IsCorp ? "корпоративная" : "личная",
                    ["userLevel"] = McpAccess.Level,
                    ["modelKind"] = McpAccess.ModelKind(doc),
                    ["accessLevel"] = McpAccess.EffectiveLevel(doc),
                    ["scriptRights"] = ScriptRunner.ScopeText(McpAccess.ScriptScopeFor(doc)),
                    ["restriction"] = McpAccess.RestrictionReason(doc),
                    // Claude должен знать, что изменения через другой коннектор обходят уровни TNov.
                    ["otherMcpConnectors"] = new JArray(ClaudeConnector.OtherMcpAddins())
                },
                ["projectInfo"] = projectInfo,
                ["levels"] = new JArray(new FilteredElementCollector(doc).OfClass(typeof(Level)).Cast<Level>()
                    .OrderBy(l => l.Elevation)
                    .Select(l => new JObject
                    {
                        ["id"] = l.Id.LongValue(),
                        ["name"] = l.Name,
                        ["elevation"] = UnitFormatUtils.Format(doc.GetUnits(), SpecTypeId.Length, l.Elevation, false)
                    })),
                ["phases"] = new JArray(doc.Phases.Cast<Phase>().Select(p => p.Name)),
                ["links"] = new JArray(new FilteredElementCollector(doc).OfClass(typeof(RevitLinkType)).Cast<RevitLinkType>()
                    .Select(t => new JObject
                    {
                        ["id"] = t.Id.LongValue(),
                        ["name"] = t.Name,
                        ["loaded"] = RevitLinkType.IsLoaded(doc, t.Id)
                    })),
                ["openDocuments"] = new JArray(app.Application.Documents.Cast<Document>()
                    .Where(d => !d.IsLinked)
                    .Select(d => new JObject { ["title"] = d.Title, ["active"] = d.Equals(doc) }))
            };
        }

        private static object ListCategories(UIApplication app, JObject args)
        {
            Document doc = ActiveUiDoc(app).Document;
            string typeFilter = Str(args, "category_type");

            var rows = new FilteredElementCollector(doc).WhereElementIsNotElementType()
                .Select(e => e.Category)
                .Where(c => c != null)
                .GroupBy(c => c.Id.LongValue())
                .Select(g => new { Category = g.First(), Count = g.Count() })
                .Where(r => typeFilter == null || string.Equals(r.Category.CategoryType.ToString(), typeFilter, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(r => r.Count);

            return new JArray(rows.Select(r => new JObject
            {
                ["name"] = r.Category.Name,
                ["builtIn"] = BuiltInName(r.Category.Id),
                ["type"] = r.Category.CategoryType.ToString(),
                ["count"] = r.Count
            }));
        }

        private class ParamFilter
        {
            public string Name;
            public string Op;
            public string Value;
        }

        private static readonly HashSet<string> FilterOps = new HashSet<string>
        {
            "equals", "not_equals", "contains", "not_contains", "starts_with",
            "greater", "less", "greater_or_equal", "less_or_equal", "exists", "is_empty", "is_not_empty"
        };

        private static object FindElements(UIApplication app, JObject args)
        {
            IEnumerable<Element> query = QueryElements(ActiveUiDoc(app), args);
            List<string> fields = StrList(args, "fields");
            int limit = Math.Min(Math.Max(Int(args, "limit", 100), 1), MaxElementsPerCall);
            int offset = Math.Max(Int(args, "offset", 0), 0);

            int total = 0;
            var items = new JArray();
            foreach (Element e in query)
            {
                if (total >= offset && items.Count < limit)
                {
                    JObject item = Summary(e);
                    if (fields.Count > 0)
                    {
                        var values = new JObject();
                        foreach (string field in fields)
                        {
                            Parameter p = FindParameterWithType(e, field);
                            values[field] = p == null ? null : DisplayValue(p);
                        }
                        item["fields"] = values;
                    }
                    items.Add(item);
                }
                total++;
            }

            return new JObject
            {
                ["total"] = total,
                ["offset"] = offset,
                ["returned"] = items.Count,
                ["elements"] = items
            };
        }

        /// <summary>Отбор элементов по аргументам find_elements: категория, класс, вид, имя, фильтры по параметрам.</summary>
        private static IEnumerable<Element> QueryElements(UIDocument uidoc, JObject args)
        {
            Document doc = uidoc.Document;

            ElementId viewId = null;
            if (args["view_id"] != null) viewId = ElementIdCompat.ToElementId((long)args["view_id"]);
            else if (Bool(args, "active_view_only", false)) viewId = uidoc.ActiveView.Id;

            var collector = viewId != null ? new FilteredElementCollector(doc, viewId) : new FilteredElementCollector(doc);

            string category = Str(args, "category");
            if (category != null) collector = collector.OfCategoryId(ResolveCategory(doc, category));

            Type cls = null;
            string className = Str(args, "class_name");
            if (className != null)
            {
                cls = ResolveClass(className);
                // OfClass не принимает часть классов (Room, Area и др.) — тогда отфильтруем ниже.
                try { collector = collector.OfClass(cls); cls = null; }
                catch (ArgumentException) { }
            }

            bool types = string.Equals(Str(args, "element_kind"), "types", StringComparison.OrdinalIgnoreCase);
            collector = types ? collector.WhereElementIsElementType() : collector.WhereElementIsNotElementType();

            List<ParamFilter> filters = (args["filters"] as JArray ?? new JArray()).OfType<JObject>()
                .Select(f => new ParamFilter { Name = Str(f, "parameter"), Op = (Str(f, "op") ?? "equals").ToLowerInvariant(), Value = Str(f, "value") ?? "" })
                .ToList();
            foreach (ParamFilter f in filters)
            {
                if (string.IsNullOrEmpty(f.Name)) throw new ArgumentException("В фильтре не указан parameter.");
                if (!FilterOps.Contains(f.Op)) throw new ArgumentException($"Неизвестная операция «{f.Op}». Допустимы: {string.Join(", ", FilterOps)}.");
            }

            string nameContains = Str(args, "name_contains");

            IEnumerable<Element> query = collector;
            if (cls != null) query = query.Where(cls.IsInstanceOfType);
            if (nameContains != null) query = query.Where(e => (SafeName(e) ?? "").IndexOf(nameContains, StringComparison.OrdinalIgnoreCase) >= 0);
            if (filters.Count > 0) query = query.Where(e => filters.All(f => Matches(e, doc, f)));
            return query;
        }

        private static object GetElementParameters(UIApplication app, JObject args)
        {
            Document doc = ActiveUiDoc(app).Document;
            List<ElementId> ids = Ids(args, "ids");
            if (ids.Count > 50) throw new ArgumentException("Не больше 50 элементов за вызов.");
            bool includeType = Bool(args, "include_type", false);
            HashSet<string> names = new HashSet<string>(StrList(args, "names"), StringComparer.OrdinalIgnoreCase);

            var result = new JArray();
            foreach (ElementId id in ids)
            {
                Element e = GetElement(doc, id);
                JObject item = Summary(e);
                item["parameters"] = ParametersJson(e, names);
                if (includeType && doc.GetElement(e.GetTypeId()) is ElementType type)
                    item["typeParameters"] = ParametersJson(type, names);
                result.Add(item);
            }
            return result;
        }

        private static object SetParameters(UIApplication app, JObject args)
        {
            Document doc = ActiveUiDoc(app).Document;
            JArray changes = args["changes"] as JArray;
            if (changes == null || changes.Count == 0) throw new ArgumentException("Список changes пуст.");
            bool limited = McpAccess.EffectiveLevel(doc) < 3;
            if (limited && changes.Count > McpAccess.Level2MaxChanges)
                throw new ArgumentException($"На уровне доступа 2 — не больше {McpAccess.Level2MaxChanges} изменений за вызов (передано {changes.Count}). Разбейте на части.");

            var results = new JArray();
            var warnings = new List<string>();
            int succeeded = 0;

            using (var t = new Transaction(doc, Str(args, "transaction_name") ?? "Claude: изменение параметров"))
            {
                t.Start();
                SuppressWarnings(t, warnings);

                foreach (JObject change in changes.OfType<JObject>())
                {
                    long idValue = (long)change["id"];
                    string name = Str(change, "parameter");
                    string value = Str(change, "value") ?? "";
                    var row = new JObject { ["id"] = idValue, ["parameter"] = name };
                    try
                    {
                        Element e = GetElement(doc, ElementIdCompat.ToElementId(idValue));
                        if (limited && e is ElementType)
                            throw new ArgumentException("Параметры типов меняются только на уровне доступа 3 — изменение затронет все экземпляры.");
                        Parameter p = FindParameter(e, name);
                        if (p == null)
                        {
                            bool onType = doc.GetElement(e.GetTypeId()) is ElementType type && FindParameter(type, name) != null;
                            throw new ArgumentException(onType && limited
                                ? $"«{name}» — параметр типа; их меняют только на уровне доступа 3."
                                : onType
                                ? $"«{name}» — параметр типа. Передайте id типа: {e.GetTypeId().LongValue()} (изменится у всех экземпляров)."
                                : $"У элемента нет параметра «{name}».");
                        }
                        if (p.IsReadOnly) throw new ArgumentException($"Параметр «{name}» только для чтения.");
                        SetValue(p, value);
                        row["ok"] = true;
                        row["value"] = DisplayValue(p);
                        succeeded++;
                    }
                    catch (Exception ex)
                    {
                        row["ok"] = false;
                        row["error"] = ex.Message;
                    }
                    results.Add(row);
                }

                if (succeeded > 0) t.Commit();
                else t.RollBack();
            }

            return new JObject
            {
                ["succeeded"] = succeeded,
                ["failed"] = results.Count - succeeded,
                ["results"] = results,
                ["warnings"] = new JArray(warnings.Distinct())
            };
        }

        private static object GetSelection(UIApplication app, JObject args)
        {
            UIDocument uidoc = ActiveUiDoc(app);
            ICollection<ElementId> ids = uidoc.Selection.GetElementIds();
            return new JObject
            {
                ["count"] = ids.Count,
                ["elements"] = new JArray(ids.Take(MaxElementsPerCall).Select(id => uidoc.Document.GetElement(id)).Where(e => e != null).Select(Summary))
            };
        }

        private static object SetSelection(UIApplication app, JObject args)
        {
            UIDocument uidoc = ActiveUiDoc(app);
            List<ElementId> ids = Ids(args, "ids").Where(id => uidoc.Document.GetElement(id) != null).ToList();
            uidoc.Selection.SetElementIds(ids);
            if (ids.Count > 0 && Bool(args, "show", false)) uidoc.ShowElements(ids);
            return new JObject { ["selected"] = ids.Count };
        }

        private static object IsolateElements(UIApplication app, JObject args)
        {
            UIDocument uidoc = ActiveUiDoc(app);
            Document doc = uidoc.Document;
            View view = uidoc.ActiveView;
            string mode = (Str(args, "mode") ?? "").ToLowerInvariant();

            using (var t = new Transaction(doc, "Claude: временная изоляция"))
            {
                t.Start();
                switch (mode)
                {
                    case "isolate":
                        view.IsolateElementsTemporary(Ids(args, "ids"));
                        break;
                    case "hide":
                        view.HideElementsTemporary(Ids(args, "ids"));
                        break;
                    case "reset":
                        view.DisableTemporaryViewMode(TemporaryViewMode.TemporaryHideIsolate);
                        break;
                    default:
                        throw new ArgumentException("mode должен быть isolate, hide или reset.");
                }
                t.Commit();
            }
            return new JObject { ["view"] = view.Name, ["mode"] = mode };
        }

        private static object ListViews(UIApplication app, JObject args)
        {
            Document doc = ActiveUiDoc(app).Document;
            string kind = (Str(args, "kind") ?? "views").ToLowerInvariant();
            string nameContains = Str(args, "name_contains");
            int limit = Math.Min(Math.Max(Int(args, "limit", 200), 1), MaxElementsPerCall);

            IEnumerable<View> views = new FilteredElementCollector(doc).OfClass(typeof(View)).Cast<View>();
            switch (kind)
            {
                case "sheets": views = views.Where(v => v is ViewSheet); break;
                case "schedules": views = views.Where(v => v is ViewSchedule && !v.IsTemplate); break;
                case "templates": views = views.Where(v => v.IsTemplate); break;
                case "all": break;
                default: views = views.Where(v => !v.IsTemplate && !(v is ViewSheet) && !(v is ViewSchedule) && v.ViewType != ViewType.Internal && v.ViewType != ViewType.ProjectBrowser && v.ViewType != ViewType.SystemBrowser); break;
            }
            if (nameContains != null)
            {
                views = views.Where(v => v.Name.IndexOf(nameContains, StringComparison.OrdinalIgnoreCase) >= 0
                    || (v is ViewSheet s && s.SheetNumber.IndexOf(nameContains, StringComparison.OrdinalIgnoreCase) >= 0));
            }

            List<View> list = views.OrderBy(v => v is ViewSheet s ? s.SheetNumber : v.Name).ToList();
            return new JObject
            {
                ["total"] = list.Count,
                ["views"] = new JArray(list.Take(limit).Select(ViewInfo))
            };
        }

        private static object OpenView(UIApplication app, JObject args)
        {
            UIDocument uidoc = ActiveUiDoc(app);
            ElementId id = ElementIdCompat.ToElementId((long)args["view_id"]);
            if (!(uidoc.Document.GetElement(id) is View view)) throw new ArgumentException($"Вид {id.LongValue()} не найден.");
            if (view.IsTemplate) throw new ArgumentException("Шаблон вида нельзя открыть.");
            uidoc.ActiveView = view;
            return ViewInfo(view);
        }

        private static object ExportViewImage(UIApplication app, JObject args)
        {
            UIDocument uidoc = ActiveUiDoc(app);
            Document doc = uidoc.Document;

            View view = uidoc.ActiveView;
            if (args["view_id"] != null)
            {
                ElementId id = ElementIdCompat.ToElementId((long)args["view_id"]);
                view = doc.GetElement(id) as View ?? throw new ArgumentException($"Вид {id.LongValue()} не найден.");
            }
            if (view.IsTemplate || !view.CanBePrinted)
                throw new ArgumentException($"Вид «{view.Name}» ({view.ViewType}) нельзя экспортировать в изображение.");

            bool visibleRegion = Bool(args, "visible_region", false);
            if (visibleRegion && view.Id != uidoc.ActiveView.Id)
                throw new ArgumentException("visible_region работает только для активного вида.");

            string dir = Path.Combine(Path.GetTempPath(), "TNovMcp", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            try
            {
                var options = new ImageExportOptions
                {
                    FilePath = Path.Combine(dir, "view"),
                    HLRandWFViewsFileType = ImageFileType.PNG,
                    ShadowViewsFileType = ImageFileType.PNG,
                    ImageResolution = ImageResolution.DPI_150,
                    ZoomType = ZoomFitType.FitToPage,
                    FitDirection = FitDirectionType.Horizontal,
                    PixelSize = Math.Min(Math.Max(Int(args, "pixel_size", 1600), 256), 4096)
                };
                if (visibleRegion)
                {
                    options.ExportRange = ExportRange.VisibleRegionOfCurrentView;
                }
                else
                {
                    options.ExportRange = ExportRange.SetOfViews;
                    options.SetViewsAndSheets(new List<ElementId> { view.Id });
                }
                doc.ExportImage(options);

                string file = Directory.GetFiles(dir, "*.png").FirstOrDefault()
                    ?? throw new InvalidOperationException("Revit не создал файл изображения.");
                return new JArray
                {
                    new JObject { ["type"] = "image", ["data"] = Convert.ToBase64String(File.ReadAllBytes(file)), ["mimeType"] = "image/png" },
                    new JObject { ["type"] = "text", ["text"] = $"Вид «{view.Name}» (id {view.Id.LongValue()}, {view.ViewType})" }
                };
            }
            finally
            {
                try { Directory.Delete(dir, true); } catch (Exception) { }
            }
        }

        #endregion

        #region Revit-помощники (используются и в ScriptRunner)

        public static UIDocument ActiveUiDoc(UIApplication app)
        {
            return app.ActiveUIDocument ?? throw new InvalidOperationException("В Revit нет открытого документа.");
        }

        public static Element GetElement(Document doc, ElementId id)
        {
            return doc.GetElement(id) ?? throw new ArgumentException($"Элемент {id.LongValue()} не найден.");
        }

        public static JObject Summary(Element e)
        {
            var o = new JObject
            {
                ["id"] = e.Id.LongValue(),
                ["name"] = SafeName(e),
                ["category"] = e.Category?.Name
            };
            Document doc = e.Document;
            if (e is ElementType ownType)
            {
                o["family"] = ownType.FamilyName;
            }
            else
            {
                ElementId typeId = e.GetTypeId();
                if (typeId != ElementId.InvalidElementId && doc.GetElement(typeId) is ElementType type)
                {
                    o["type"] = string.IsNullOrEmpty(type.FamilyName) ? type.Name : type.FamilyName + ": " + type.Name;
                    o["typeId"] = typeId.LongValue();
                }
            }
            if (e.LevelId != ElementId.InvalidElementId)
                o["level"] = doc.GetElement(e.LevelId)?.Name;
            return o;
        }

        private static JObject ViewInfo(View v)
        {
            var o = new JObject
            {
                ["id"] = v.Id.LongValue(),
                ["name"] = v.Name,
                ["viewType"] = v.ViewType.ToString()
            };
            if (v is ViewSheet sheet) o["sheetNumber"] = sheet.SheetNumber;
            if (v.IsTemplate) o["isTemplate"] = true;
            return o;
        }

        private static JArray ParametersJson(Element e, HashSet<string> names)
        {
            var list = new JArray();
            foreach (Parameter p in e.Parameters.Cast<Parameter>().OrderBy(x => x.Definition?.Name))
            {
                string name = p.Definition?.Name;
                if (name == null || (names.Count > 0 && !names.Contains(name))) continue;
                var o = new JObject
                {
                    ["name"] = name,
                    ["value"] = DisplayValue(p),
                    ["storage"] = p.StorageType.ToString()
                };
                if (p.StorageType == StorageType.ElementId && p.AsElementId() != ElementId.InvalidElementId)
                    o["elementId"] = p.AsElementId().LongValue();
                if (p.IsReadOnly) o["readOnly"] = true;
                if (p.IsShared) o["shared"] = true;
                if (p.Definition is InternalDefinition def && def.BuiltInParameter != BuiltInParameter.INVALID)
                    o["builtIn"] = def.BuiltInParameter.ToString();
                list.Add(o);
            }
            return list;
        }

        public static string DisplayValue(Parameter p)
        {
            if (!p.HasValue && p.StorageType != StorageType.String) return null;
            switch (p.StorageType)
            {
                case StorageType.String:
                    return p.AsString();
                case StorageType.ElementId:
                    {
                        string s = p.AsValueString();
                        if (!string.IsNullOrEmpty(s)) return s;
                        ElementId id = p.AsElementId();
                        return id == ElementId.InvalidElementId ? null : id.LongValue().ToString(CultureInfo.InvariantCulture);
                    }
                case StorageType.Integer:
                    return p.AsValueString() ?? p.AsInteger().ToString(CultureInfo.InvariantCulture);
                case StorageType.Double:
                    return p.AsValueString() ?? p.AsDouble().ToString(CultureInfo.InvariantCulture);
                default:
                    return null;
            }
        }

        private static double? NumericValue(Parameter p)
        {
            switch (p.StorageType)
            {
                case StorageType.Integer:
                    return p.AsInteger();
                case StorageType.Double:
                    try { return UnitUtils.ConvertFromInternalUnits(p.AsDouble(), p.GetUnitTypeId()); }
                    catch (Exception) { return p.AsDouble(); } // безразмерные величины
                case StorageType.ElementId:
                    return p.AsElementId().LongValue();
                default:
                    return ParseNumber(p.AsString());
            }
        }

        private static readonly Regex LeadingNumber = new Regex(@"^\s*[-+]?\d[\d\s]*([.,]\d+)?");

        private static double? ParseNumber(string s)
        {
            if (string.IsNullOrEmpty(s)) return null;
            Match m = LeadingNumber.Match(s);
            if (!m.Success) return null;
            string normalized = m.Value.Replace(" ", "").Replace(' '.ToString(), "").Replace(',', '.');
            return double.TryParse(normalized, NumberStyles.Float, CultureInfo.InvariantCulture, out double v) ? v : (double?)null;
        }

        private static bool Matches(Element e, Document doc, ParamFilter f)
        {
            Parameter p = FindParameterWithType(e, f.Name);
            string text = p == null ? null : DisplayValue(p);
            bool empty = string.IsNullOrEmpty(text);

            switch (f.Op)
            {
                case "exists": return p != null;
                case "is_empty": return empty;
                case "is_not_empty": return !empty;
                case "equals": return p != null && ValueEquals(p, text, f.Value);
                case "not_equals": return p == null || !ValueEquals(p, text, f.Value);
                case "contains": return !empty && text.IndexOf(f.Value, StringComparison.OrdinalIgnoreCase) >= 0;
                case "not_contains": return empty || text.IndexOf(f.Value, StringComparison.OrdinalIgnoreCase) < 0;
                case "starts_with": return !empty && text.StartsWith(f.Value, StringComparison.OrdinalIgnoreCase);
            }

            // Числовые сравнения
            double? actual = p == null ? null : NumericValue(p);
            double? expected = ParseNumber(f.Value);
            if (actual == null || expected == null) return false;
            const double eps = 1e-6;
            switch (f.Op)
            {
                case "greater": return actual > expected + eps;
                case "less": return actual < expected - eps;
                case "greater_or_equal": return actual >= expected - eps;
                case "less_or_equal": return actual <= expected + eps;
                default: return false;
            }
        }

        private static bool ValueEquals(Parameter p, string text, string expected)
        {
            if (string.Equals(text ?? "", expected, StringComparison.OrdinalIgnoreCase)) return true;
            if (p.StorageType == StorageType.String) return false;
            double? a = NumericValue(p), b = ParseNumber(expected);
            return a != null && b != null && Math.Abs(a.Value - b.Value) < 1e-4;
        }

        public static Parameter FindParameter(Element e, string name)
        {
            Parameter p = e.LookupParameter(name);
            if (p == null && !name.All(char.IsDigit) && Enum.TryParse(name, true, out BuiltInParameter bip))
                p = e.get_Parameter(bip);
            return p;
        }

        private static Parameter FindParameterWithType(Element e, string name)
        {
            Parameter p = FindParameter(e, name);
            if (p == null && e.Document.GetElement(e.GetTypeId()) is ElementType type)
                p = FindParameter(type, name);
            return p;
        }

        private static void SetValue(Parameter p, string value)
        {
            switch (p.StorageType)
            {
                case StorageType.String:
                    p.Set(value);
                    return;

                case StorageType.Integer:
                    if (p.Definition.GetDataType() == SpecTypeId.Boolean.YesNo)
                    {
                        string v = value.Trim().ToLowerInvariant();
                        if (v == "да" || v == "yes" || v == "true" || v == "1") { p.Set(1); return; }
                        if (v == "нет" || v == "no" || v == "false" || v == "0") { p.Set(0); return; }
                        throw new ArgumentException($"Для параметра Да/Нет ожидается «Да» или «Нет», получено «{value}».");
                    }
                    if (int.TryParse(value.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int i)) { p.Set(i); return; }
                    if (p.SetValueString(value)) return;
                    break;

                case StorageType.Double:
                    if (p.SetValueString(value)) return;
                    double? number = ParseNumber(value);
                    if (number != null)
                    {
                        double internalValue;
                        try { internalValue = UnitUtils.ConvertToInternalUnits(number.Value, p.GetUnitTypeId()); }
                        catch (Exception) { internalValue = number.Value; }
                        p.Set(internalValue);
                        return;
                    }
                    break;

                case StorageType.ElementId:
                    if (long.TryParse(value.Trim(), out long id)) { p.Set(ElementIdCompat.ToElementId(id)); return; }
                    if (p.SetValueString(value)) return;
                    break;
            }
            throw new ArgumentException($"Не удалось записать «{value}» в параметр типа {p.StorageType}.");
        }

        public static string SafeName(Element e)
        {
            try { return e.Name; }
            catch (Exception) { return null; }
        }

        private static string BuiltInName(ElementId categoryId)
        {
            long v = categoryId.LongValue();
            if (v >= 0 || v < int.MinValue) return null;
            return Enum.IsDefined(typeof(BuiltInCategory), (int)v) ? ((BuiltInCategory)(int)v).ToString() : null;
        }

        private static ElementId ResolveCategory(Document doc, string name)
        {
            if (!name.All(c => char.IsDigit(c) || c == '-'))
            {
                if (Enum.TryParse(name, true, out BuiltInCategory bic) || Enum.TryParse("OST_" + name, true, out bic))
                    return new ElementId(bic);
            }
            foreach (Category c in doc.Settings.Categories)
            {
                if (string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase)) return c.Id;
            }
            throw new ArgumentException($"Категория «{name}» не найдена. Посмотрите имена через list_categories.");
        }

        private static Dictionary<string, Type> _elementClasses;

        private static Type ResolveClass(string name)
        {
            if (_elementClasses == null)
            {
                Type[] all;
                try { all = typeof(Element).Assembly.GetTypes(); }
                catch (System.Reflection.ReflectionTypeLoadException ex) { all = ex.Types.Where(t => t != null).ToArray(); }

                var map = new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase);
                foreach (Type t in all.Where(t => t.IsPublic && typeof(Element).IsAssignableFrom(t)))
                {
                    // Короткое имя — первым, полное — всегда.
                    if (!map.ContainsKey(t.Name)) map[t.Name] = t;
                    map[t.FullName] = t;
                }
                _elementClasses = map;
            }
            return _elementClasses.TryGetValue(name, out Type type)
                ? type
                : throw new ArgumentException($"Класс «{name}» не найден среди наследников Element в Revit API.");
        }

        /// <summary>Подавляет предупреждения транзакции (иначе Revit покажет диалог) и собирает их текст.</summary>
        public static void SuppressWarnings(Transaction t, List<string> sink)
        {
            FailureHandlingOptions options = t.GetFailureHandlingOptions();
            options.SetFailuresPreprocessor(new WarningCollector(sink));
            options.SetClearAfterRollback(true);
            t.SetFailureHandlingOptions(options);
        }

        private class WarningCollector : IFailuresPreprocessor
        {
            private readonly List<string> _sink;
            public WarningCollector(List<string> sink) { _sink = sink; }

            public FailureProcessingResult PreprocessFailures(FailuresAccessor accessor)
            {
                foreach (FailureMessageAccessor message in accessor.GetFailureMessages())
                {
                    _sink.Add(message.GetDescriptionText());
                    if (message.GetSeverity() == FailureSeverity.Warning) accessor.DeleteWarning(message);
                }
                return FailureProcessingResult.Continue;
            }
        }

        #endregion

        #region Аргументы и схемы

        private static string Str(JObject args, string name)
        {
            JToken token = args[name];
            return token == null || token.Type == JTokenType.Null ? null : token.ToString();
        }

        private static int Int(JObject args, string name, int defaultValue)
        {
            JToken token = args[name];
            return token == null || token.Type == JTokenType.Null ? defaultValue : (int)token;
        }

        private static bool Bool(JObject args, string name, bool defaultValue)
        {
            JToken token = args[name];
            return token == null || token.Type == JTokenType.Null ? defaultValue : (bool)token;
        }

        private static List<string> StrList(JObject args, string name)
        {
            return (args[name] as JArray)?.Select(t => t.ToString()).ToList() ?? new List<string>();
        }

        private static List<ElementId> Ids(JObject args, string name)
        {
            if (!(args[name] is JArray array)) throw new ArgumentException($"Нужен массив {name}.");
            return array.Select(t => ElementIdCompat.ToElementId((long)t)).ToList();
        }

        private static JObject Schema(string[] required = null, params JProperty[] properties)
        {
            var schema = new JObject
            {
                ["type"] = "object",
                ["properties"] = new JObject(properties)
            };
            if (required != null && required.Length > 0) schema["required"] = new JArray(required);
            return schema;
        }

        private static JProperty Prop(string name, string type, string description) =>
            new JProperty(name, new JObject { ["type"] = type, ["description"] = description });

        private static JProperty IntArray(string name, string description) =>
            new JProperty(name, new JObject { ["type"] = "array", ["items"] = new JObject { ["type"] = "integer" }, ["description"] = description });

        private static JProperty StringArray(string name, string description) =>
            new JProperty(name, new JObject { ["type"] = "array", ["items"] = new JObject { ["type"] = "string" }, ["description"] = description });

        private static JProperty FiltersProperty() =>
            new JProperty("filters", new JObject
            {
                ["type"] = "array",
                ["description"] = "Фильтры по параметрам (все должны выполняться).",
                ["items"] = Schema(new[] { "parameter", "op" },
                    Prop("parameter", "string", "Имя параметра как в интерфейсе или BuiltInParameter."),
                    Prop("op", "string", "Операция сравнения."),
                    Prop("value", "string", "Значение для сравнения."))
            });

        #endregion
    }
}
