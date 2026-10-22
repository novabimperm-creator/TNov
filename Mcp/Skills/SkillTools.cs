using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Interop;

namespace TNov.Mcp.Skills
{
    /// <summary>Инструменты MCP для библиотеки скиллов: поиск, чтение, запуск, сохранение.</summary>
    internal static class SkillTools
    {
        public static object Search(JObject args)
        {
            string query = args["query"]?.ToString();
            int limit = args["limit"]?.Type == JTokenType.Integer ? Math.Min(Math.Max((int)args["limit"], 1), 50) : 10;
            List<Skill> found = SkillLibrary.Search(query, limit);
            return new JObject
            {
                ["store"] = SkillLibrary.StoreName,
                ["found"] = found.Count,
                ["skills"] = new JArray(found.Select(Brief)),
                ["hint"] = found.Count == 0
                    ? "Подходящих скиллов нет — решите задачу через execute_csharp; удачное решение можно сохранить через skill_save."
                    : "Подробности и код — skills_get, запуск — skill_run с параметрами."
            };
        }

        public static object Get(JObject args)
        {
            Skill skill = Find(args);
            int? number = args["version"]?.Type == JTokenType.Integer ? (int?)args["version"] : null;
            SkillVersion version = skill.Version(number) ?? throw new ArgumentException($"У скилла «{skill.Name}» нет версии {number}.");
            JObject result = Brief(skill);
            result["examples"] = new JArray(skill.Examples ?? new List<string>());
            result["instruction"] = skill.Instruction;
            result["files"] = new JArray((skill.Files ?? new List<SkillFile>()).Select(f => new JObject
            {
                ["name"] = f.Name,
                ["size"] = SkillFiles.SizeText(f.Size),
                ["addedBy"] = f.AddedBy
            }));
            result["versions"] = new JArray((skill.Versions ?? new List<SkillVersion>()).OrderByDescending(v => v.Number).Select(v => new JObject
            {
                ["number"] = v.Number,
                ["author"] = v.Author,
                ["saved"] = v.SavedUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm"),
                ["revit"] = v.Revit,
                ["note"] = v.Note
            }));
            result["code"] = version.Code;
            result["codeVersion"] = version.Number;
            return result;
        }

        public static object Run(UIApplication app, JObject args)
        {
            Skill skill = Find(args);
            if (skill.Status == Skill.Hidden) throw new InvalidOperationException($"Скилл «{skill.Name}» скрыт и не запускается.");
            int? number = args["version"]?.Type == JTokenType.Integer ? (int?)args["version"] : null;
            SkillVersion version = skill.Version(number) ?? throw new ArgumentException($"У скилла «{skill.Name}» нет версии {number}.");

            // Значения по умолчанию, поверх — переданные.
            var scriptArgs = new JObject();
            foreach (SkillParameter p in skill.Parameters ?? new List<SkillParameter>())
                if (!string.IsNullOrEmpty(p.Name) && p.Default != null) scriptArgs[p.Name] = p.Default;
            if (args["args"] is JObject given)
                foreach (JProperty property in given.Properties()) scriptArgs[property.Name] = property.Value;

            var call = new JObject { ["code"] = version.Code, ["args"] = scriptArgs };
            if (args["transaction"] != null) call["transaction"] = args["transaction"];
            call["transaction_name"] = McpClient.Current.Name + ": скилл " + skill.Name;

            string draft = skill.Status == Skill.Draft ? ", черновик" : "";
            string title = $"«{skill.Name}» v{version.Number} (автор {version.Author}{draft})";
            object result = ScriptRunner.Run(app, call, title);
            if (result is JObject json)
            {
                json["skill"] = skill.Id;
                json["skillVersion"] = version.Number;
                if (!string.IsNullOrEmpty(version.Revit) && version.Revit != app.Application.VersionNumber)
                    json["note"] = $"Скилл проверен в Revit {version.Revit}, сейчас Revit {app.Application.VersionNumber}.";
            }
            return result;
        }

        public static object Save(UIApplication app, JObject args)
        {
            string name = args["name"]?.ToString()?.Trim();
            string description = args["description"]?.ToString()?.Trim();
            string code = args["code"]?.ToString();
            if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Нет названия скилла.");
            if (string.IsNullOrWhiteSpace(code)) throw new ArgumentException("Нет кода скилла.");

            if (!SkillLibrary.IsVerified(code))
                throw new InvalidOperationException(
                    "Этот код ещё не отрабатывал в Revit у пользователя без ошибок и отката (ни в этом, ни в прошлых сеансах). " +
                    "Передайте код точно таким, каким он выполнялся через execute_csharp. Если код меняли (например, вынесли значения в параметры), " +
                    "сначала выполните его один раз через execute_csharp с нужными args, затем сохраните.");

            var parameters = (args["parameters"] as JArray ?? new JArray()).OfType<JObject>()
                .Select(p => new SkillParameter { Name = p["name"]?.ToString(), Description = p["description"]?.ToString(), Default = p["default"]?.ToString() })
                .Where(p => !string.IsNullOrWhiteSpace(p.Name))
                .ToList();
            var examples = (args["examples"] as JArray ?? new JArray()).Select(e => e.ToString()).Where(e => !string.IsNullOrWhiteSpace(e)).ToList();
            string note = args["note"]?.ToString();

            Skill existing = SkillLibrary.Get(SkillLibrary.IdFor(name));
            bool publish = McpAccess.Level >= 2;
            var model = new SkillSaveModel
            {
                Client = McpClient.Current.Name,
                Name = name,
                Description = description,
                Parameters = parameters,
                Examples = examples,
                Code = code,
                Instruction = args["instruction"]?.ToString()?.Trim() ?? existing?.Instruction ?? "",
                AuthorText = existing == null
                    ? "Автор: " + Environment.UserName
                    : $"Автор скилла: {existing.Author}. Новую версию сохраняет: {Environment.UserName}",
                Outcome = (existing != null ? $"Новая версия {existing.Latest?.Number + 1} скилла «{existing.Name}» (автор {existing.Author}). " : "Новый скилл. ") +
                          (publish ? "Будет сразу опубликован для всех." : "Уровень доступа 1: сохранится черновиком — его увидят коллеги с уровнем 2–3 и смогут опубликовать."),
                ConfirmText = publish ? "Опубликовать" : "Сохранить черновик"
            };

            var window = new SkillSaveWPF(model);
            new WindowInteropHelper(window) { Owner = app.MainWindowHandle };
            if (window.ShowDialog() != true)
                return new JObject { ["declined"] = true, ["message"] = "Пользователь отменил сохранение скилла." };

            Skill skill = SkillLibrary.Save(name, description, code, parameters, examples, note, app.Application.VersionNumber, model.Instruction);
            var failed = new List<string>();
            foreach (string path in model.Attachments)
            {
                // Файлы прикладывает тот, кто сохраняет скилл, — без проверки прав автора/BIM.
                try { skill = SkillFiles.Add(skill.Id, path, requireEdit: false); }
                catch (Exception ex) { failed.Add(System.IO.Path.GetFileName(path) + ": " + ex.Message); }
            }
            var result = new JObject
            {
                ["id"] = skill.Id,
                ["name"] = skill.Name,
                ["author"] = skill.Author,
                ["status"] = skill.StatusText,
                ["version"] = skill.Latest?.Number,
                ["files"] = new JArray((skill.Files ?? new List<SkillFile>()).Select(f => f.Name)),
                ["store"] = SkillLibrary.StoreName
            };
            if (failed.Count > 0) result["filesFailed"] = new JArray(failed);
            return result;
        }

        private static Skill Find(JObject args)
        {
            string id = args["id"]?.ToString();
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("Не указан id скилла.");
            return SkillLibrary.Get(id) ?? SkillLibrary.Get(SkillLibrary.IdFor(id))
                   ?? throw new ArgumentException($"Скилл «{id}» не найден. Поищите через skills_search.");
        }

        private static JObject Brief(Skill s) => new JObject
        {
            ["id"] = s.Id,
            ["name"] = s.Name,
            ["description"] = s.Description,
            ["status"] = s.StatusText,
            ["author"] = s.Author,
            ["version"] = s.Latest?.Number,
            ["revit"] = s.Latest?.Revit,
            ["parameters"] = new JArray((s.Parameters ?? new List<SkillParameter>()).Select(p => new JObject
            {
                ["name"] = p.Name,
                ["description"] = p.Description,
                ["default"] = p.Default
            }))
        };
    }

    /// <summary>Данные окна подтверждения сохранения скилла. Инструкцию и файлы пользователь задаёт в окне.</summary>
    public sealed class SkillSaveModel
    {
        /// <summary>Короткая инструкция для коллег (нейросеть предлагает черновик, пользователь правит).</summary>
        public string Instruction { get; set; }
        /// <summary>Локальные пути файлов, которые приложатся к скиллу.</summary>
        public System.Collections.ObjectModel.ObservableCollection<string> Attachments { get; } =
            new System.Collections.ObjectModel.ObservableCollection<string>();
        public string AuthorText { get; set; }
        public string Client { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public List<SkillParameter> Parameters { get; set; }
        public List<string> Examples { get; set; }
        public string Code { get; set; }
        public string Outcome { get; set; }
        public string ConfirmText { get; set; }
        public string Title => Client + " предлагает сохранить скилл";
        public string ExamplesText => Examples == null || Examples.Count == 0 ? "—" : string.Join("\n", Examples.Select(e => "• " + e));
        public string ParametersText => Parameters == null || Parameters.Count == 0
            ? "нет"
            : string.Join("\n", Parameters.Select(p => $"• {p.Name}" + (string.IsNullOrEmpty(p.Description) ? "" : " — " + p.Description) +
                                                      (string.IsNullOrEmpty(p.Default) ? "" : $" (по умолчанию: {p.Default})")));
    }
}
