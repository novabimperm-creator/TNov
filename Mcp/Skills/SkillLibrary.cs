using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using TNovCommon;
using TNovCommon.Storage;

namespace TNov.Mcp.Skills
{
    /// <summary>
    /// Общая библиотека скиллов (только корпоративная конфигурация).
    /// Хранилище: TNovApi (/documents/mcpskill/{id}), если на шаре есть tnovapi.json с адресом API,
    /// иначе файлы {ServerPath}mcp\skills\{id},mcpskill.json — тот же IDocumentStore, что у чек-листов.
    ///
    /// Права: публиковать и править опубликованные — уровень 2–3 (без модерации); уровень 1 сохраняет
    /// черновик, который видят автор и пользователи уровня 2–3. Скрыть — автор или уровень 3.
    /// Сохранить можно только код, который хотя бы раз отработал в Revit у этого пользователя без отката
    /// (в любом сеансе): отпечатки удачного кода копятся в %LOCALAPPDATA%\TNov\mcp-skills\verified.txt.
    /// </summary>
    internal static class SkillLibrary
    {
        public const string Kind = "mcpskill";
        private const string Folder = "mcp\\skills";

        private static readonly object _lock = new object();
        private static readonly Dictionary<string, KeyValuePair<long, Skill>> _cache =
            new Dictionary<string, KeyValuePair<long, Skill>>(StringComparer.OrdinalIgnoreCase);
        private static DateTime _refreshedUtc;
        private static string _storeSignature;

        public static bool Available
        {
            get
            {
                TNovConfig config = TNovConfigLoad.GetCachedConfig();
                return CorpGate.IsCorp(config) && !string.IsNullOrWhiteSpace(config.ServerPath);
            }
        }

        private static IDocumentStore Store(out string name)
        {
            TNovConfig config = TNovConfigLoad.GetCachedConfig();
            if (!CorpGate.IsCorp(config) || string.IsNullOrWhiteSpace(config?.ServerPath))
                throw new InvalidOperationException("Скиллы доступны только в корпоративной конфигурации.");
            if (!string.IsNullOrWhiteSpace(config.ApiUrl))
            {
                name = "TNovApi";
                return new ApiDocumentStore(DocumentStores.GetClient(config));
            }
            name = "шара";
            return new FileDocumentStore(config.ServerPath, Folder);
        }

        public static string StoreName
        {
            get { Store(out string name); return name; }
        }

        #region Чтение и поиск

        /// <summary>Все скиллы, видимые текущему пользователю. Кэш обновляется не чаще раза в 30 с (force — сразу).</summary>
        public static List<Skill> All(bool force = false)
        {
            Refresh(force);
            lock (_lock) return _cache.Values.Select(v => v.Value).Where(CanSee).OrderBy(s => s.Name).ToList();
        }

        public static Skill Get(string id)
        {
            Refresh(false);
            lock (_lock)
            {
                if (_cache.TryGetValue(id ?? "", out var entry) && CanSee(entry.Value)) return entry.Value;
                // По названию — нейросеть может передать имя вместо id.
                Skill byName = _cache.Values.Select(v => v.Value)
                    .FirstOrDefault(s => string.Equals(s.Name, id, StringComparison.OrdinalIgnoreCase) && CanSee(s));
                return byName;
            }
        }

        public static List<Skill> Search(string query, int limit)
        {
            List<Skill> all = All();
            if (string.IsNullOrWhiteSpace(query)) return all.Where(s => s.Status == Skill.Published).Take(limit).ToList();

            // Основы слов: русские окончания меняются, сравниваем по первым 5 буквам.
            string[] stems = Regex.Split(query.ToLowerInvariant(), @"[^\p{L}\p{N}]+")
                .Where(w => w.Length > 1).Select(w => w.Length > 5 ? w.Substring(0, 5) : w).Distinct().ToArray();
            return all
                .Select(s =>
                {
                    string name = (s.Name ?? "").ToLowerInvariant();
                    string examples = string.Join(" ", s.Examples ?? new List<string>()).ToLowerInvariant();
                    string text = ((s.Description ?? "") + " " + string.Join(" ", (s.Parameters ?? new List<SkillParameter>()).Select(p => p.Name + " " + p.Description))).ToLowerInvariant();
                    int score = stems.Sum(st => (name.Contains(st) ? 3 : 0) + (examples.Contains(st) ? 2 : 0) + (text.Contains(st) ? 1 : 0));
                    if (s.Status != Skill.Published) score -= 1; // черновики ниже опубликованных
                    return new { Skill = s, Score = score };
                })
                .Where(r => r.Score > 0)
                .OrderByDescending(r => r.Score).ThenBy(r => r.Skill.Name)
                .Take(limit)
                .Select(r => r.Skill)
                .ToList();
        }

        private static void Refresh(bool force)
        {
            IDocumentStore store = Store(out string storeName);
            TNovConfig config = TNovConfigLoad.GetCachedConfig();
            string signature = storeName + "|" + config.ApiUrl + "|" + config.ServerPath;
            lock (_lock)
            {
                if (_storeSignature != signature) { _cache.Clear(); _storeSignature = signature; force = true; }
                if (!force && DateTime.UtcNow - _refreshedUtc < TimeSpan.FromSeconds(30)) return;
            }

            IReadOnlyList<StoredDocumentInfo> infos = Run(() => store.ListAsync(Kind));
            var alive = new HashSet<string>(infos.Select(i => i.Key), StringComparer.OrdinalIgnoreCase);
            foreach (StoredDocumentInfo info in infos)
            {
                lock (_lock)
                {
                    if (_cache.TryGetValue(info.Key, out var cached) && cached.Key == info.Version) continue;
                }
                StoredDocument doc = Run(() => store.LoadAsync(Kind, info.Key));
                Skill skill = Parse(doc);
                if (skill == null) continue;
                lock (_lock) _cache[info.Key] = new KeyValuePair<long, Skill>(doc.Version, skill);
            }
            lock (_lock)
            {
                foreach (string gone in _cache.Keys.Where(k => !alive.Contains(k)).ToList()) _cache.Remove(gone);
                _refreshedUtc = DateTime.UtcNow;
            }
        }

        private static Skill Parse(StoredDocument doc)
        {
            if (doc == null || !doc.Exists || string.IsNullOrWhiteSpace(doc.Json)) return null;
            try
            {
                Skill skill = JsonConvert.DeserializeObject<Skill>(doc.Json);
                if (skill != null && string.IsNullOrEmpty(skill.Id)) skill.Id = doc.Key;
                return skill;
            }
            catch (JsonException) { return null; } // битый документ не роняет библиотеку
        }

        #endregion

        #region Права

        private static string Login => Environment.UserName;

        public static bool CanSee(Skill s)
        {
            if (s.Status == Skill.Published) return true;
            if (s.IsAuthor(Login)) return true;
            int level = McpAccess.Level;
            return s.Status == Skill.Draft ? level >= 2 : level >= 3;
        }

        /// <summary>Отдел BIM по roles.txt (считается один раз за сеанс Revit).</summary>
        private static readonly Lazy<bool> _isBim = new Lazy<bool>(() =>
        {
            try { return RolesService.GetCurrentUser(null).IsBim; }
            catch (Exception) { return false; }
        });

        public static bool IsBim => _isBim.Value;

        /// <summary>Инструкцию и файлы меняют автор скилла и любой сотрудник отдела BIM.</summary>
        public static bool CanEdit(Skill s) => s.IsAuthor(Login) || IsBim;

        public static bool CanPublish(Skill s) => s.Status == Skill.Draft && McpAccess.Level >= 2;
        public static bool CanHide(Skill s) => s.Status != Skill.Hidden && (s.IsAuthor(Login) || McpAccess.Level >= 3);
        public static bool CanRestore(Skill s) => s.Status == Skill.Hidden && (s.IsAuthor(Login) || McpAccess.Level >= 3);

        #endregion

        #region Сохранение

        /// <summary>
        /// Новый скилл или новая версия существующего (то же название). Уровень 2–3 публикует сразу,
        /// уровень 1 — черновик; опубликованный чужой скилл уровень 1 не меняет.
        /// </summary>
        public static Skill Save(string name, string description, string code, List<SkillParameter> parameters,
                                 List<string> examples, string note, string revit, string instruction)
        {
            if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Нет названия скилла.");
            if (string.IsNullOrWhiteSpace(code)) throw new ArgumentException("Нет кода скилла.");
            IDocumentStore store = Store(out _);
            string id = IdFor(name);
            int level = McpAccess.Level;
            Skill result = null;

            StoredDocument saved = Run(() => store.UpdateAsync(Kind, id, json =>
            {
                Skill skill = string.IsNullOrWhiteSpace(json) ? null : JsonConvert.DeserializeObject<Skill>(json);
                DateTime now = DateTime.UtcNow;
                if (skill == null)
                {
                    skill = new Skill { Id = id, Author = Login, CreatedUtc = now, Status = level >= 2 ? Skill.Published : Skill.Draft };
                }
                else
                {
                    if (level < 2 && !(skill.Status == Skill.Draft && skill.IsAuthor(Login)))
                        throw new InvalidOperationException(
                            $"Скилл «{skill.Name}» уже есть и опубликован (автор {skill.Author}). На уровне 1 его нельзя менять — сохраните под другим названием.");
                    if (skill.Status == Skill.Hidden && !(skill.IsAuthor(Login) || level >= 3))
                        throw new InvalidOperationException($"Скилл «{skill.Name}» скрыт. Сохраните под другим названием.");
                    if (level >= 2 && skill.Status == Skill.Draft) skill.Status = Skill.Published;
                }
                skill.Name = name.Trim();
                skill.Description = description?.Trim();
                skill.Parameters = parameters ?? new List<SkillParameter>();
                skill.Examples = examples ?? new List<string>();
                skill.Instruction = string.IsNullOrWhiteSpace(instruction) ? skill.Instruction : instruction.Trim();
                skill.UpdatedUtc = now;
                skill.Versions = skill.Versions ?? new List<SkillVersion>();
                skill.Versions.Add(new SkillVersion
                {
                    Number = (skill.Versions.Count == 0 ? 0 : skill.Versions.Max(v => v.Number)) + 1,
                    Code = code,
                    Hash = Hash(code),
                    Revit = revit,
                    Level = level,
                    Author = Login,
                    SavedUtc = now,
                    Note = note
                });
                result = skill;
                return JsonConvert.SerializeObject(skill, Formatting.Indented);
            }));

            lock (_lock) _cache[id] = new KeyValuePair<long, Skill>(saved.Version, result);
            return result;
        }

        /// <summary>Изменить инструкцию — автор или отдел BIM.</summary>
        public static Skill SetInstruction(string id, string instruction) =>
            Modify(id, skill => skill.Instruction = instruction?.Trim(), requireEdit: true);

        /// <summary>Изменить список файлов скилла (сами файлы кладёт SkillFiles).</summary>
        public static Skill ModifyFiles(string id, Action<List<SkillFile>> change, bool requireEdit) =>
            Modify(id, skill =>
            {
                skill.Files = skill.Files ?? new List<SkillFile>();
                change(skill.Files);
            }, requireEdit);

        /// <summary>Общая правка документа скилла с повтором при конфликте версий.</summary>
        private static Skill Modify(string id, Action<Skill> change, bool requireEdit)
        {
            IDocumentStore store = Store(out _);
            Skill result = null;
            StoredDocument saved = Run(() => store.UpdateAsync(Kind, id, json =>
            {
                Skill skill = JsonConvert.DeserializeObject<Skill>(json ?? "") ?? throw new InvalidOperationException("Скилл не найден.");
                if (requireEdit && !CanEdit(skill))
                    throw new InvalidOperationException("Менять инструкцию и файлы скилла могут его автор и сотрудники отдела BIM.");
                change(skill);
                skill.Files = skill.Files ?? new List<SkillFile>();
                skill.UpdatedUtc = DateTime.UtcNow;
                result = skill;
                return JsonConvert.SerializeObject(skill, Formatting.Indented);
            }));
            lock (_lock) _cache[id] = new KeyValuePair<long, Skill>(saved.Version, result);
            return result;
        }

        public static Skill SetStatus(string id, string status)
        {
            IDocumentStore store = Store(out _);
            Skill result = null;
            StoredDocument saved = Run(() => store.UpdateAsync(Kind, id, json =>
            {
                Skill skill = JsonConvert.DeserializeObject<Skill>(json ?? "") ?? throw new InvalidOperationException("Скилл не найден.");
                bool allowed = status == Skill.Published ? (CanPublish(skill) || CanRestore(skill)) : CanHide(skill);
                if (!allowed) throw new InvalidOperationException("Недостаточно прав для этого действия.");
                skill.Status = status;
                skill.UpdatedUtc = DateTime.UtcNow;
                result = skill;
                return JsonConvert.SerializeObject(skill, Formatting.Indented);
            }));
            lock (_lock) _cache[id] = new KeyValuePair<long, Skill>(saved.Version, result);
            return result;
        }

        /// <summary>Ключ документа из названия: строчные буквы и цифры, остальное — дефисы.</summary>
        public static string IdFor(string name)
        {
            string id = Regex.Replace(name.Trim().ToLowerInvariant(), @"[^\p{L}\p{N}]+", "-").Trim('-');
            if (id.Length > 80) id = id.Substring(0, 80).Trim('-');
            return id.Length == 0 ? "skill-" + Guid.NewGuid().ToString("N").Substring(0, 8) : id;
        }

        #endregion

        #region Проверенный код

        private static readonly string VerifiedPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TNov", "mcp-skills", "verified.txt");

        /// <summary>Отпечаток кода без пробелов и переводов строк: нейросеть может переформатировать код из чата.</summary>
        public static string Hash(string code)
        {
            string normalized = Regex.Replace(code ?? "", @"\s+", "");
            using (var sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(normalized))).Replace("-", "").ToLowerInvariant();
        }

        /// <summary>Запомнить код, который отработал без исключения и без отката проверкой прав.</summary>
        public static void MarkVerified(string code)
        {
            try
            {
                string hash = Hash(code);
                lock (_lock)
                {
                    if (IsVerifiedUnlocked(hash)) return;
                    Directory.CreateDirectory(Path.GetDirectoryName(VerifiedPath));
                    File.AppendAllText(VerifiedPath, hash + "\t" + DateTime.Now.ToString("yyyy-MM-dd HH:mm") + Environment.NewLine);
                }
            }
            catch (Exception) { } // не мешаем работе скрипта
        }

        public static bool IsVerified(string code)
        {
            lock (_lock) return IsVerifiedUnlocked(Hash(code));
        }

        private static bool IsVerifiedUnlocked(string hash)
        {
            try
            {
                return File.Exists(VerifiedPath) && File.ReadLines(VerifiedPath).Any(l => l.StartsWith(hash, StringComparison.Ordinal));
            }
            catch (Exception) { return false; }
        }

        #endregion

        /// <summary>Синхронно дождаться задачи хранилища вне контекста синхронизации (без взаимоблокировки на потоке UI).</summary>
        private static T Run<T>(Func<Task<T>> action) => Task.Run(action).GetAwaiter().GetResult();
    }
}
