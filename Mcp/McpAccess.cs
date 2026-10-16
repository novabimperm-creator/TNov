using Autodesk.Revit.DB;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using TNovCommon;

namespace TNov.Mcp
{
    /// <summary>
    /// Уровни доступа Claude к модели.
    ///   1 — чтение, оформление (аннотации, виды, листы, спецификации, изображения), экспорт;
    ///       C# только в режиме «без изменения 3D». Любые модели.
    ///   2 — 3D-элементы: параметры экземпляров, размещение, перемещение, не больше Level2MaxChanges за запрос;
    ///       C# с правом менять 3D — только в «свободных» моделях (даже в корпоративной конфигурации).
    ///   3 — всё, включая удаление и C# без ограничений.
    /// «Защищённая» модель — локальная копия файла-хранилища (IsWorkshared и не отсоединена).
    /// В личной конфигурации уровни 2–3 действуют только в свободных моделях, в защищённых — уровень 1.
    ///
    /// Откуда уровень:
    ///   личная конфигурация — пользователь выбирает сам (TNovClient\TNovMcp.json, PersonalLevel);
    ///   корпоративная — назначение в {ServerPath}TNovMcp.json "Users" или опыт работы для уровня 2:
    ///   больше Level2StreakDays дней подряд, или больше Level2Days дней всего, или больше Level2Calls операций.
    /// "KnownUsers" плагин дописывает сам при первом запуске коннектора (только для сведения).
    /// </summary>
    internal static class McpAccess
    {
        public const int Level2MaxChanges = 50;

        /// <summary>Что разрешено скрипту execute_csharp менять в модели.</summary>
        public enum ScriptScope
        {
            /// <summary>Аннотации, виды, листы, спецификации, изображения, загрузка типов; без 3D и удалений.</summary>
            Annotations,
            /// <summary>Плюс 3D-элементы, не больше Level2MaxChanges; без удалений.</summary>
            Model50,
            /// <summary>Без ограничений.</summary>
            Full
        }

        private class ServerSettings
        {
            public bool Enabled { get; set; } = true;
            public int Level2StreakDays { get; set; } = 3;
            public int Level2Days { get; set; } = 5;
            public int Level2Calls { get; set; } = 30;
            public Dictionary<string, int> Users { get; set; }
        }

        /// <summary>Локальные настройки и счётчики опыта: TNovClient\TNovMcp.json.</summary>
        public class LocalSettings
        {
            public int Port { get; set; } = McpServer.DefaultPort;
            public int SuccessfulCalls { get; set; }
            public int ActiveDays { get; set; }
            /// <summary>Текущая серия дней работы подряд и лучшая серия.</summary>
            public int CurrentStreak { get; set; }
            public int MaxStreak { get; set; }
            public string LastActiveDay { get; set; }
            /// <summary>Уровень, выбранный пользователем в личной конфигурации (1–3).</summary>
            public int PersonalLevel { get; set; } = 1;
            /// <summary>Уровень, выбранный в корпоративной конфигурации в пределах доступного (null — максимальный).</summary>
            public int? CorpSelectedLevel { get; set; }
            /// <summary>Последний известный максимум в корпоративной конфигурации — чтобы заметить его рост.</summary>
            public int CorpMaxSeen { get; set; }
            /// <summary>Пользователь уже записан в KnownUsers серверного файла.</summary>
            public bool RegisteredOnServer { get; set; }
            /// <summary>Какие разделы нейросетей раскрыты в окне «Нейросетошная» (имя коннектора → раскрыт).</summary>
            public Dictionary<string, bool> ExpandedSections { get; set; } = new Dictionary<string, bool>();
        }

        private static readonly object _lock = new object();
        private static readonly string LocalPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "TNovClient", "TNovMcp.json");

        private static LocalSettings _local;
        private static ServerSettings _server;
        private static DateTime _serverReadUtc;

        public static LocalSettings Local
        {
            get
            {
                lock (_lock)
                {
                    if (_local == null)
                    {
                        try { _local = JsonConvert.DeserializeObject<LocalSettings>(File.ReadAllText(LocalPath)); }
                        catch (Exception) { }
                        if (_local == null) _local = new LocalSettings();
                    }
                    return _local;
                }
            }
        }

        /// <summary>Раскрыт ли раздел нейросети в окне. Пока пользователь не выбирал — раскрыт только Claude.</summary>
        public static bool IsSectionExpanded(string name)
        {
            lock (_lock)
            {
                Dictionary<string, bool> sections = Local.ExpandedSections;
                return sections != null && sections.TryGetValue(name, out bool expanded) ? expanded : name == "Claude";
            }
        }

        public static void SetSectionExpanded(string name, bool expanded)
        {
            lock (_lock)
            {
                if (Local.ExpandedSections == null) Local.ExpandedSections = new Dictionary<string, bool>();
                if (Local.ExpandedSections.TryGetValue(name, out bool old) && old == expanded) return;
                Local.ExpandedSections[name] = expanded;
                SaveLocal();
            }
        }

        public static void SaveLocal()
        {
            lock (_lock)
            {
                try
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(LocalPath));
                    File.WriteAllText(LocalPath, JsonConvert.SerializeObject(Local, Formatting.Indented));
                }
                catch (Exception) { }
            }
        }

        /// <summary>Коннектор не запрещён администратором.</summary>
        public static bool Enabled => Server.Enabled;

        /// <summary>Назначен ли уровень администратором явно.</summary>
        public static int? AssignedLevel
        {
            get
            {
                Dictionary<string, int> users = Server.Users;
                if (users == null) return null;
                foreach (KeyValuePair<string, int> pair in users)
                {
                    if (string.Equals(pair.Key, Environment.UserName, StringComparison.OrdinalIgnoreCase))
                        return Math.Min(Math.Max(pair.Value, 1), 3);
                }
                return null;
            }
        }

        public static bool IsCorp => CorpGate.IsCorp();

        /// <summary>Наибольший уровень, доступный пользователю: личная — 3, корпоративная — назначение или опыт.</summary>
        public static int MaxLevel
        {
            get
            {
                if (!IsCorp) return 3;
                int? assigned = AssignedLevel;
                if (assigned != null) return assigned.Value;
                return Level2Earned ? 2 : 1;
            }
        }

        /// <summary>Выбранный уровень пользователя без учёта модели (не выше MaxLevel).</summary>
        public static int Level
        {
            get
            {
                if (!IsCorp) return Clamp(Local.PersonalLevel);
                lock (_lock)
                {
                    int max = MaxLevel;
                    LocalSettings local = Local;
                    if (max != local.CorpMaxSeen)
                    {
                        // Рост максимума один раз поднимает выбор; понижение просто запоминаем,
                        // чтобы следующий рост снова поднял выбор.
                        if (max > local.CorpMaxSeen) local.CorpSelectedLevel = null;
                        local.CorpMaxSeen = max;
                        SaveLocal();
                    }
                    return Math.Min(local.CorpSelectedLevel ?? max, max);
                }
            }
        }

        /// <summary>Выбор уровня пользователем: в личной — 1–3, в корпоративной — не выше доступного.</summary>
        public static void SetLevel(int level)
        {
            lock (_lock)
            {
                if (IsCorp)
                {
                    int max = MaxLevel;
                    int chosen = Math.Min(Clamp(level), max);
                    Local.CorpSelectedLevel = chosen == max ? (int?)null : chosen;
                }
                else
                {
                    Local.PersonalLevel = Clamp(level);
                }
                SaveLocal();
            }
        }

        /// <summary>Локальная копия файла-хранилища (в том числе облачного): в ней действуют ограничения.</summary>
        public static bool IsProtected(Document doc) => doc != null && doc.IsWorkshared && !doc.IsDetached;

        public static string ModelKind(Document doc)
        {
            if (doc == null) return "нет документа";
            if (IsProtected(doc)) return "локальная копия хранилища";
            if (doc.IsDetached) return "отсоединённая модель";
            return doc.IsFamilyDocument ? "семейство" : "файл без совместной работы";
        }

        /// <summary>Уровень, действующий в этой модели для типовых инструментов.</summary>
        public static int EffectiveLevel(Document doc)
        {
            int level = Level;
            if (level > 1 && !IsCorp && IsProtected(doc)) return 1;
            return level;
        }

        /// <summary>Права скрипта: изменение 3D скриптом на уровне 2 — только в свободных моделях.</summary>
        public static ScriptScope ScriptScopeFor(Document doc)
        {
            int level = EffectiveLevel(doc);
            if (level >= 3) return ScriptScope.Full;
            if (level == 2 && !IsProtected(doc)) return ScriptScope.Model50;
            return ScriptScope.Annotations;
        }

        /// <summary>Почему в этой модели уровень ниже уровня пользователя (null — не ниже).</summary>
        public static string RestrictionReason(Document doc)
        {
            if (EffectiveLevel(doc) < Level)
                return "в личной конфигурации уровни 2–3 работают только в отсоединённых моделях, а это локальная копия хранилища";
            return null;
        }

        private static bool Level2Earned
        {
            get
            {
                ServerSettings server = Server;
                LocalSettings local = Local;
                return local.MaxStreak > server.Level2StreakDays
                    || local.ActiveDays > server.Level2Days
                    || local.SuccessfulCalls > server.Level2Calls;
            }
        }

        /// <summary>Сколько осталось до уровня 2 (для окон). Достаточно любого из условий.</summary>
        public static string Level2Progress
        {
            get
            {
                ServerSettings server = Server;
                LocalSettings local = Local;
                return $"дней подряд {Math.Min(local.MaxStreak, server.Level2StreakDays + 1)}/{server.Level2StreakDays + 1}, " +
                       $"или дней всего {Math.Min(local.ActiveDays, server.Level2Days + 1)}/{server.Level2Days + 1}, " +
                       $"или операций {Math.Min(local.SuccessfulCalls, server.Level2Calls + 1)}/{server.Level2Calls + 1}";
            }
        }

        /// <summary>Учитывает успешный запрос. Возвращает новый уровень, если он повысился.</summary>
        public static int? RegisterSuccessfulCall()
        {
            lock (_lock)
            {
                int before = Level;
                LocalSettings local = Local;
                local.SuccessfulCalls++;
                DateTime today = DateTime.Today;
                string todayText = today.ToString("yyyy-MM-dd");
                if (local.LastActiveDay != todayText)
                {
                    bool consecutive = local.LastActiveDay == today.AddDays(-1).ToString("yyyy-MM-dd");
                    local.CurrentStreak = consecutive ? local.CurrentStreak + 1 : 1;
                    local.LastActiveDay = todayText;
                    local.ActiveDays++;
                }
                else if (local.CurrentStreak == 0)
                {
                    local.CurrentStreak = 1; // счётчики из версии без серий
                }
                local.MaxStreak = Math.Max(local.MaxStreak, local.CurrentStreak);
                SaveLocal();
                int after = Level;
                return after > before ? after : (int?)null;
            }
        }

        public static string LevelName(int level)
        {
            switch (level)
            {
                case 1: return "1 — чтение и оформление";
                case 2: return "2 — 3D-элементы (до 50 за запрос)";
                default: return "3 — полный доступ";
            }
        }

        private static int Clamp(int level) => Math.Min(Math.Max(level, 1), 3);

        private static ServerSettings Server
        {
            get
            {
                lock (_lock)
                {
                    // Шару перечитываем не чаще раза в 5 минут: назначения меняются редко, а сеть бывает медленной.
                    if (_server == null || DateTime.UtcNow - _serverReadUtc > TimeSpan.FromMinutes(5))
                    {
                        _server = ReadServer() ?? _server ?? new ServerSettings();
                        _serverReadUtc = DateTime.UtcNow;
                    }
                    return _server;
                }
            }
        }

        /// <summary>{ServerPath}TNovMcp.json; null — не корпоративная лицензия.</summary>
        private static string ServerFilePath()
        {
            TNovConfig config = TNovConfigLoad.GetCachedConfig();
            if (!CorpGate.IsCorp(config) || string.IsNullOrEmpty(config.ServerPath)) return null;
            return Path.Combine(TNovCommon.Server.ServerData.NormalizeRoot(config.ServerPath), "TNovMcp.json");
        }

        private static ServerSettings ReadServer()
        {
            try
            {
                string path = ServerFilePath();
                if (path == null || !File.Exists(path)) return new ServerSettings();
                return JsonConvert.DeserializeObject<ServerSettings>(File.ReadAllText(path)) ?? new ServerSettings();
            }
            catch (Exception)
            {
                return null; // шара недоступна — оставляем прошлое значение
            }
        }

        /// <summary>
        /// При первом запуске коннектора дописывает пользователя в "KnownUsers" серверного файла
        /// ({ "логин": "дата первого запуска" }), чтобы BIM-менеджер видел, кому назначать уровни.
        /// На уровень доступа KnownUsers не влияет. Фоном и молча: нет прав на шару — попробуем в следующий раз.
        /// </summary>
        public static void RegisterFirstUseInBackground()
        {
            if (Local.RegisteredOnServer) return;
            ThreadPool.QueueUserWorkItem(_ =>
            {
                try
                {
                    string path = ServerFilePath();
                    if (path == null) return;

                    for (int attempt = 0; attempt < 5; attempt++)
                    {
                        try
                        {
                            // Монопольно: несколько пользователей могут дописывать файл одновременно.
                            using (var stream = new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None))
                            {
                                string text;
                                using (var reader = new StreamReader(stream, Encoding.UTF8, true, 4096, leaveOpen: true))
                                    text = reader.ReadToEnd();

                                JObject root = string.IsNullOrWhiteSpace(text) ? new JObject() : JObject.Parse(text);
                                if (!(root["KnownUsers"] is JObject known))
                                {
                                    known = new JObject();
                                    root["KnownUsers"] = known;
                                }
                                if (known[Environment.UserName] == null)
                                {
                                    known[Environment.UserName] = DateTime.Today.ToString("yyyy-MM-dd");
                                    byte[] bytes = new UTF8Encoding(false).GetBytes(JsonConvert.SerializeObject(root, Formatting.Indented));
                                    stream.SetLength(0);
                                    stream.Write(bytes, 0, bytes.Length);
                                }
                            }
                            lock (_lock)
                            {
                                Local.RegisteredOnServer = true;
                                SaveLocal();
                            }
                            return;
                        }
                        catch (IOException)
                        {
                            Thread.Sleep(300); // файл занят другим пользователем
                        }
                    }
                }
                catch (Exception) { }
            });
        }
    }
}
