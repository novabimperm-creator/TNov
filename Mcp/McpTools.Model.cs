using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TNovCommon;

namespace TNov.Mcp
{
    /// <summary>Инструменты сводок, экспорта и операций с 3D-элементами.</summary>
    internal static partial class McpTools
    {
        private sealed class SummaryGroup
        {
            public string[] Key;
            public int Count;
            public double[] Sums;
            public bool[] HasValue;
        }

        private static object SummarizeElements(UIApplication app, JObject args)
        {
            UIDocument uidoc = ActiveUiDoc(app);
            List<string> groupBy = StrList(args, "group_by");
            List<string> sums = StrList(args, "sum");

            var groups = new Dictionary<string, SummaryGroup>();
            int total = 0;
            foreach (Element e in QueryElements(uidoc, args))
            {
                total++;
                string[] key = groupBy.Select(name =>
                {
                    Parameter p = FindParameterWithType(e, name);
                    return (p == null ? null : DisplayValue(p)) ?? "—";
                }).ToArray();
                string joined = string.Join("\u001f", key);
                if (!groups.TryGetValue(joined, out SummaryGroup group))
                {
                    group = new SummaryGroup { Key = key, Sums = new double[sums.Count], HasValue = new bool[sums.Count] };
                    groups[joined] = group;
                }
                group.Count++;
                for (int i = 0; i < sums.Count; i++)
                {
                    Parameter p = FindParameterWithType(e, sums[i]);
                    double? value = p == null ? null : NumericValue(p);
                    if (value == null) continue;
                    group.Sums[i] += value.Value;
                    group.HasValue[i] = true;
                }
            }

            var rows = new JArray();
            foreach (SummaryGroup group in groups.Values.OrderBy(g => string.Join("\u001f", g.Key), StringComparer.CurrentCulture).Take(MaxElementsPerCall))
            {
                var row = new JObject();
                if (groupBy.Count > 0)
                {
                    var keyObject = new JObject();
                    for (int i = 0; i < groupBy.Count; i++) keyObject[groupBy[i]] = group.Key[i];
                    row["group"] = keyObject;
                }
                row["count"] = group.Count;
                if (sums.Count > 0)
                {
                    var sumObject = new JObject();
                    for (int i = 0; i < sums.Count; i++)
                        sumObject[sums[i]] = group.HasValue[i] ? (JToken)Math.Round(group.Sums[i], 6) : JValue.CreateNull();
                    row["sums"] = sumObject;
                }
                rows.Add(row);
            }

            return new JObject
            {
                ["totalElements"] = total,
                ["groups"] = groups.Count,
                ["rows"] = rows,
                ["note"] = "Суммы в единицах проекта; null — у элементов группы нет такого числового параметра."
            };
        }

        private static object ExportDocument(UIApplication app, JObject args)
        {
            UIDocument uidoc = ActiveUiDoc(app);
            Document doc = uidoc.Document;
            string format = (Str(args, "format") ?? "").ToLowerInvariant();
            string folder = Str(args, "folder") ?? throw new ArgumentException("Не указана папка folder.");
            Directory.CreateDirectory(folder);
            string name = Str(args, "file_name") ?? SafeFileName(doc.Title);
            List<ElementId> viewIds = args["view_ids"] is JArray ? Ids(args, "view_ids") : new List<ElementId>();

            var before = new HashSet<string>(Directory.GetFiles(folder), StringComparer.OrdinalIgnoreCase);
            switch (format)
            {
                case "dwg":
                    if (viewIds.Count == 0) viewIds.Add(uidoc.ActiveView.Id);
                    doc.Export(folder, name, viewIds, new DWGExportOptions());
                    break;

                case "pdf":
                    if (viewIds.Count == 0) viewIds.Add(uidoc.ActiveView.Id);
                    doc.Export(folder, viewIds, new PDFExportOptions { FileName = name, Combine = true });
                    break;

                case "ifc":
                    // Экспорт IFC требует открытой транзакции; изменений в модели не оставляем.
                    using (var t = new Transaction(doc, McpClient.Current.Name + ": экспорт IFC"))
                    {
                        t.Start();
                        var options = new IFCExportOptions();
                        if (viewIds.Count > 0) options.FilterViewId = viewIds[0];
                        doc.Export(folder, name, options);
                        t.RollBack();
                    }
                    break;

                case "nwc":
                    if (!OptionalFunctionalityUtils.IsNavisworksExporterAvailable())
                        throw new InvalidOperationException("Navisworks Exporter для этой версии Revit не установлен.");
                    var nwc = new NavisworksExportOptions();
                    if (viewIds.Count > 0)
                    {
                        nwc.ExportScope = NavisworksExportScope.View;
                        nwc.ViewId = viewIds[0];
                    }
                    doc.Export(folder, name, nwc);
                    break;

                case "fbx":
                    View view = viewIds.Count > 0 ? doc.GetElement(viewIds[0]) as View : uidoc.ActiveView;
                    if (!(view is View3D)) throw new ArgumentException("Для FBX нужен 3D-вид: передайте его в view_ids или откройте.");
                    var set = new ViewSet();
                    set.Insert(view);
                    doc.Export(folder, name, set, new FBXExportOptions());
                    break;

                default:
                    throw new ArgumentException("format должен быть ifc, nwc, dwg, pdf или fbx.");
            }

            var created = Directory.GetFiles(folder).Where(f => !before.Contains(f)).ToList();
            return new JObject
            {
                ["format"] = format,
                ["folder"] = folder,
                ["files"] = new JArray(created.Select(Path.GetFileName))
            };
        }

        private static object PlaceFamilyInstances(UIApplication app, JObject args)
        {
            UIDocument uidoc = ActiveUiDoc(app);
            Document doc = uidoc.Document;
            var symbol = doc.GetElement(ElementIdCompat.ToElementId((long)args["type_id"])) as FamilySymbol
                ?? throw new ArgumentException("type_id не является типоразмером семейства (FamilySymbol).");
            JArray points = args["points"] as JArray ?? throw new ArgumentException("Нужен массив points.");
            CheckBatch(doc, points.Count);

            Level level = args["level_id"] != null
                ? doc.GetElement(ElementIdCompat.ToElementId((long)args["level_id"])) as Level
                : uidoc.ActiveView.GenLevel
                  ?? new FilteredElementCollector(doc).OfClass(typeof(Level)).Cast<Level>().OrderBy(l => l.Elevation).FirstOrDefault();
            Element host = args["host_id"] != null ? GetElement(doc, ElementIdCompat.ToElementId((long)args["host_id"])) : null;

            var created = new JArray();
            var warnings = new List<string>();
            using (var t = new Transaction(doc, McpClient.Current.Name + ": размещение элементов"))
            {
                t.Start();
                SuppressWarnings(t, warnings);
                if (!symbol.IsActive) symbol.Activate();
                foreach (JArray p in points.OfType<JArray>())
                {
                    if (p.Count < 2) throw new ArgumentException("Точка — массив [x, y] или [x, y, z] в мм.");
                    var xyz = new XYZ(Mm((double)p[0]), Mm((double)p[1]), p.Count > 2 ? Mm((double)p[2]) : (level?.Elevation ?? 0));
                    FamilyInstance instance = host != null
                        ? doc.Create.NewFamilyInstance(xyz, symbol, host, level, StructuralType.NonStructural)
                        : level != null
                            ? doc.Create.NewFamilyInstance(xyz, symbol, level, StructuralType.NonStructural)
                            : doc.Create.NewFamilyInstance(xyz, symbol, StructuralType.NonStructural);
                    created.Add(Summary(instance));
                }
                t.Commit();
            }
            return new JObject { ["created"] = created, ["warnings"] = new JArray(warnings.Distinct()) };
        }

        private static object MoveElements(UIApplication app, JObject args)
        {
            Document doc = ActiveUiDoc(app).Document;
            List<ElementId> ids = Ids(args, "ids");
            CheckBatch(doc, ids.Count);
            JArray v = args["vector"] as JArray;
            if (v == null || v.Count < 2) throw new ArgumentException("vector — массив [dx, dy] или [dx, dy, dz] в мм.");
            var vector = new XYZ(Mm((double)v[0]), Mm((double)v[1]), v.Count > 2 ? Mm((double)v[2]) : 0);

            foreach (ElementId id in ids)
            {
                Element e = GetElement(doc, id);
                if (e.Pinned) throw new ArgumentException($"Элемент {id.LongValue()} закреплён — открепите его в Revit или исключите из списка.");
            }

            var warnings = new List<string>();
            using (var t = new Transaction(doc, McpClient.Current.Name + ": перемещение элементов"))
            {
                t.Start();
                SuppressWarnings(t, warnings);
                ElementTransformUtils.MoveElements(doc, ids, vector);
                t.Commit();
            }
            return new JObject { ["moved"] = ids.Count, ["warnings"] = new JArray(warnings.Distinct()) };
        }

        private static object DeleteElements(UIApplication app, JObject args)
        {
            Document doc = ActiveUiDoc(app).Document;
            List<ElementId> ids = Ids(args, "ids").Where(id => doc.GetElement(id) != null).ToList();
            if (ids.Count == 0) throw new ArgumentException("Ни один из элементов не найден.");

            ICollection<ElementId> deleted;
            var warnings = new List<string>();
            using (var t = new Transaction(doc, McpClient.Current.Name + ": удаление элементов"))
            {
                t.Start();
                SuppressWarnings(t, warnings);
                deleted = doc.Delete(ids);
                t.Commit();
            }
            return new JObject
            {
                ["requested"] = ids.Count,
                ["deletedWithDependents"] = deleted.Count,
                ["warnings"] = new JArray(warnings.Distinct())
            };
        }

        /// <summary>Лимит 3D-операций за запрос ниже уровня 3.</summary>
        private static void CheckBatch(Document doc, int count)
        {
            if (McpAccess.EffectiveLevel(doc) < 3 && count > McpAccess.Level2MaxChanges)
                throw new ArgumentException($"На уровне 2 — не больше {McpAccess.Level2MaxChanges} элементов за запрос (передано {count}). Разбейте на части.");
        }

        private static double Mm(double value) => UnitUtils.ConvertToInternalUnits(value, UnitTypeId.Millimeters);

        private static string SafeFileName(string name)
        {
            foreach (char c in Path.GetInvalidFileNameChars()) name = name.Replace(c, '_');
            return name;
        }
    }
}
