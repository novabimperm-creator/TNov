using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;

namespace TNov.Mcp.Skills
{
    /// <summary>Параметр скилла: в скрипте доступен как args["имя"] (строка).</summary>
    public sealed class SkillParameter
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public string Default { get; set; }
    }

    /// <summary>Файл, прикреплённый к скиллу: на шаре (mcp\skills\files\{id}\{имя}) или в TNovApi (FileId).</summary>
    public sealed class SkillFile
    {
        public string Name { get; set; }
        /// <summary>Id файла в TNovApi; null — файл на шаре.</summary>
        public string FileId { get; set; }
        public long Size { get; set; }
        public string AddedBy { get; set; }
        public DateTime AddedUtc { get; set; }
    }

    /// <summary>Версия кода скилла. Код — тело метода, как в execute_csharp.</summary>
    public sealed class SkillVersion
    {
        public int Number { get; set; }
        public string Code { get; set; }
        /// <summary>Отпечаток кода без пробелов (SkillLibrary.Hash) — им код и проверяется.</summary>
        public string Hash { get; set; }
        /// <summary>Версия Revit, в которой код отработал (2022 — только C# 5).</summary>
        public string Revit { get; set; }
        /// <summary>Уровень доступа автора при сохранении.</summary>
        public int Level { get; set; }
        public string Author { get; set; }
        public DateTime SavedUtc { get; set; }
        public string Note { get; set; }
    }

    /// <summary>
    /// Скилл — проверенный рецепт для Revit: описание, параметры, примеры запросов и версии кода.
    /// Хранится одним документом (kind = mcpskill, key = Id) в TNovApi или в файле на шаре.
    /// </summary>
    public sealed class Skill
    {
        public const string Published = "published";
        public const string Draft = "draft";
        public const string Hidden = "hidden";

        public string Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public List<string> Examples { get; set; } = new List<string>();
        public List<SkillParameter> Parameters { get; set; } = new List<SkillParameter>();
        public string Status { get; set; } = Draft;
        /// <summary>Автор — логин Windows создателя скилла; обязателен, не меняется при новых версиях.</summary>
        public string Author { get; set; }
        /// <summary>Короткая инструкция для людей: когда применять, что подготовить, что проверить после.</summary>
        public string Instruction { get; set; }
        public List<SkillFile> Files { get; set; } = new List<SkillFile>();
        public DateTime CreatedUtc { get; set; }
        public DateTime UpdatedUtc { get; set; }
        public List<SkillVersion> Versions { get; set; } = new List<SkillVersion>();

        [JsonIgnore] public SkillVersion Latest => Versions?.OrderByDescending(v => v.Number).FirstOrDefault();

        public SkillVersion Version(int? number) =>
            number == null ? Latest : Versions?.FirstOrDefault(v => v.Number == number.Value);

        public bool IsAuthor(string login) => string.Equals(Author, login, StringComparison.OrdinalIgnoreCase);

        [JsonIgnore] public string StatusText =>
            Status == Published ? "опубликован" : Status == Hidden ? "скрыт" : "черновик";
    }
}
