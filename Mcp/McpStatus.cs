using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Threading;

namespace TNov.Mcp
{
    /// <summary>Активность коннектора (Claude или Codex, см. McpClient) за сеанс Revit — для мониторинга в окнах.</summary>
    internal sealed class McpActivity
    {
        private readonly object _lock = new object();

        public DateTime? LastRequestUtc;
        public string ClientName { get; private set; }
        public string LastTool { get; private set; }
        public string LastError { get; private set; }
        public DateTime? LastErrorUtc { get; private set; }
        public int Calls { get; private set; }
        public int Errors { get; private set; }
        /// <summary>Модель последнего запроса (общая для коннекторов): читать Revit API из окна нельзя, поэтому запоминаем при вызове.</summary>
        public static string LastModel { get; private set; }
        public static string LastModelKind { get; private set; }
        public static int LastEffectiveLevel { get; private set; }

        public static void OnModel(string model, string kind, int effectiveLevel)
        {
            LastModel = model;
            LastModelKind = kind;
            LastEffectiveLevel = effectiveLevel;
        }

        public void OnInitialize(JObject clientInfo)
        {
            string name = (string)clientInfo?["name"];
            string version = (string)clientInfo?["version"];
            if (name != null) ClientName = version != null ? name + " " + version : name;
        }

        public void OnToolCall(string tool, string error)
        {
            lock (_lock)
            {
                LastTool = tool;
                Calls++;
                if (error != null)
                {
                    Errors++;
                    LastError = error;
                    LastErrorUtc = DateTime.UtcNow;
                }
            }
        }
    }

    /// <summary>Состояние одного коннектора (Claude, Codex или Qwen) — строка статуса в окнах.</summary>
    public class McpClientStatus : INotifyPropertyChanged
    {
        internal McpClientStatus(McpClient client) => Client = client;

        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>Все свойства пересчитываются разом в McpStatusViewModel.Refresh.</summary>
        internal void RaiseAll() => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(string.Empty));

        internal McpClient Client { get; }
        public string Name => Client.Name;
        public string StatusText { get; internal set; }
        public Brush StatusBrush { get; internal set; }
        /// <summary>Клиент, счётчики и последняя ошибка (пусто, если не подключался).</summary>
        public string Activity { get; internal set; }
        public string Url => McpServer.IsRunning ? McpServer.UrlFor(Client) : "коннектор остановлен";
        public string ConnectCommand => McpConnector.ConnectCommand(Client);

        /// <summary>Раскрыт ли раздел в окне «Нейросетошная» — запоминается у пользователя (TNovClient\TNovMcp.json).</summary>
        public bool IsExpanded
        {
            get => McpAccess.IsSectionExpanded(Client.Name);
            set => McpAccess.SetSectionExpanded(Client.Name, value);
        }
    }

    /// <summary>
    /// Модель окна кнопки «Нейросетошная»: один сервер, общий уровень доступа, коннекторы Claude, Codex и Qwen. Обновляется таймером окна.
    /// </summary>
    public class McpStatusViewModel : INotifyPropertyChanged
    {
        /// <summary>Сколько минут без запросов считать подключение активным.</summary>
        private const int ActiveMinutes = 10;

        private static readonly Brush Green = Frozen(Color.FromRgb(0x3F, 0xB9, 0x50));
        private static readonly Brush Amber = Frozen(Color.FromRgb(0xE3, 0xA0, 0x08));
        private static readonly Brush Gray = Frozen(Color.FromRgb(0x8B, 0x94, 0x9E));

        private readonly DispatcherTimer _timer;

        public McpStatusViewModel()
        {
            _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
            _timer.Tick += (s, e) => Refresh();
            Refresh();
        }

        public McpClientStatus Claude { get; } = new McpClientStatus(McpClient.Claude);
        public McpClientStatus Codex { get; } = new McpClientStatus(McpClient.Codex);
        public McpClientStatus Qwen { get; } = new McpClientStatus(McpClient.Qwen);

        /// <summary>Уровень доступа 1–3 (шкала в окне).</summary>
        public int Level { get; private set; }
        public string LevelCaption { get; private set; }

        /// <summary>Предупреждение о других MCP-коннекторах в Revit (null — их нет). Сборки грузятся при старте Revit, поэтому считаем один раз.</summary>
        public string OtherMcpWarning { get; } = BuildOtherMcpWarning();

        private static string BuildOtherMcpWarning()
        {
            List<string> others = McpConnector.OtherMcpAddins();
            if (others.Count == 0) return null;
            return $"⚠ В Revit загружен другой MCP-коннектор: {string.Join(", ", others)}. " +
                   "Его действия уровнями доступа TNov не ограничиваются и в журнал TNov не попадают. " +
                   "Для работы и тестов отключите его в Claude Code / Codex / Qwen Code или удалите надстройку.";
        }

        /// <summary>Переключатель уровня: в личной — 1–3, в корпоративной — в пределах доступного (скрыт, если доступен только 1).</summary>
        public bool CanChooseLevel => LevelOptions.Length > 1;
        public int[] LevelOptions { get; private set; } = { 1 };
        public string LevelChoiceLabel => McpAccess.IsCorp
            ? "Уровень для работы (не выше доступного вам)"
            : "Уровень нейросетей (уровни 2–3 — только в отсоединённых моделях)";
        public int SelectedLevel
        {
            get => Level;
            set
            {
                if (value == McpAccess.Level) return;
                McpAccess.SetLevel(value);
                CheckResult = "Уровень сохранён. Новые инструменты нейросети увидят в новом сеансе.";
                Refresh();
            }
        }

        public void CopyConnectCommand(McpClientStatus client)
        {
            try
            {
                System.Windows.Clipboard.SetText(client.ConnectCommand);
                CheckResult = client.Client == McpClient.Codex
                    ? "Блок скопирован. Вставьте его в конец config.toml в Блокноте и перезапустите Codex."
                    : client.Client == McpClient.Qwen
                    ? "Фрагмент скопирован. Добавьте сервер «revit» в раздел mcpServers файла %USERPROFILE%\\.qwen\\settings.json и перезапустите Qwen Code."
                    : "Команда скопирована. Выполните её в PowerShell, не в чате Claude.";
            }
            catch (Exception) { CheckResult = "Не удалось скопировать — выделите команду и нажмите Ctrl+C."; }
        }

        /// <summary>Подключение к обычному чату Claude Desktop через мост stdio -> HTTP.</summary>
        public void ConnectClaudeDesktop()
        {
            try { CheckResult = ClaudeDesktopSetup.Install(McpServer.IsRunning ? McpServer.Port : McpAccess.Local.Port); }
            catch (Exception ex) { CheckResult = "Не удалось подключить Claude Desktop: " + ex.Message; }
        }

        /// <summary>Подключение к Codex: мост stdio -> HTTP в ~/.codex/config.toml.</summary>
        public void ConnectCodex()
        {
            try { CheckResult = CodexSetup.Install(McpServer.IsRunning ? McpServer.Port : McpAccess.Local.Port); }
            catch (Exception ex) { CheckResult = "Не удалось подключить Codex: " + ex.Message; }
        }

        /// <summary>Подключение к Qwen Code: мост stdio -> HTTP в ~/.qwen/settings.json.</summary>
        public void ConnectQwen()
        {
            try { CheckResult = QwenSetup.Install(McpServer.IsRunning ? McpServer.Port : McpAccess.Local.Port); }
            catch (Exception ex) { CheckResult = "Не удалось подключить Qwen Code: " + ex.Message; }
        }

        public string ToggleText => McpServer.IsRunning ? "Остановить" : "Запустить";
        public bool CanToggle => McpAccess.Enabled || McpServer.IsRunning;

        private string _checkResult;
        public string CheckResult
        {
            get => _checkResult;
            private set { _checkResult = value; OnPropertyChanged(); }
        }

        public void StartMonitoring() => _timer.Start();
        public void StopMonitoring() => _timer.Stop();

        public void Refresh()
        {
            Update(Claude);
            Update(Codex);
            Update(Qwen);

            int level = McpAccess.Level;
            Level = level;
            int max = McpAccess.MaxLevel;
            if (LevelOptions.Length != max) LevelOptions = Enumerable.Range(1, max).ToArray();
            if (!McpAccess.IsCorp)
            {
                LevelCaption = "Личная конфигурация: уровень выбираете вы. Уровни 2–3 работают только в отсоединённых моделях.";
            }
            else if (max < 2)
            {
                LevelCaption = "До уровня 2 (любое из условий): " + McpAccess.Level2Progress;
            }
            else
            {
                LevelCaption = $"Доступен уровень {max} ({(McpAccess.AssignedLevel != null ? "назначен BIM-менеджером" : "получен за опыт работы")}).";
                if (level < max) LevelCaption += $" Сейчас выбран уровень {level}.";
            }
            LevelCaption += " Уровень общий для всех нейросетей.";
            if (McpActivity.LastModel != null && McpActivity.LastEffectiveLevel > 0 && McpActivity.LastEffectiveLevel < level)
                LevelCaption += $"\nВ модели «{McpActivity.LastModel}» ({McpActivity.LastModelKind}) действует уровень {McpActivity.LastEffectiveLevel}.";

            Claude.RaiseAll();
            Codex.RaiseAll();
            Qwen.RaiseAll();
            OnPropertyChanged(nameof(ToggleText));
            OnPropertyChanged(nameof(CanToggle));
            OnPropertyChanged(nameof(Level));
            OnPropertyChanged(nameof(LevelCaption));
            OnPropertyChanged(nameof(LevelOptions));
            OnPropertyChanged(nameof(CanChooseLevel));
            OnPropertyChanged(nameof(LevelChoiceLabel));
            OnPropertyChanged(nameof(SelectedLevel));
        }

        private static void Update(McpClientStatus status)
        {
            // Имя нейросети — в заголовке раздела окна, здесь только состояние.
            McpActivity activity = status.Client.Activity;
            DateTime? last = activity.LastRequestUtc;
            if (!McpAccess.Enabled && !McpServer.IsRunning)
            {
                Set(status, "Отключён администратором", Gray);
            }
            else if (!McpServer.IsRunning)
            {
                Set(status, "Коннектор остановлен", Gray);
            }
            else if (last == null)
            {
                Set(status, "Ещё не подключался", Amber);
            }
            else
            {
                TimeSpan idle = DateTime.UtcNow - last.Value;
                if (idle.TotalMinutes < ActiveMinutes)
                    Set(status, "Подключён · запрос " + Ago(idle), Green);
                else
                    Set(status, "Нет запросов " + Ago(idle), Amber);
            }

            var text = new StringBuilder();
            if (activity.ClientName != null) text.AppendLine("Клиент: " + activity.ClientName);
            if (activity.Calls > 0)
            {
                text.AppendLine($"Запросов за сеанс: {activity.Calls}, ошибок: {activity.Errors}. Последний: {activity.LastTool}");
                if (activity.LastError != null && activity.LastErrorUtc != null)
                    text.AppendLine($"Последняя ошибка ({Ago(DateTime.UtcNow - activity.LastErrorUtc.Value)}): {Trim(activity.LastError, 160)}");
            }
            status.Activity = text.ToString().TrimEnd();
        }

        public void Toggle()
        {
            try
            {
                if (McpServer.IsRunning) McpServer.Stop();
                else McpServer.Start(McpAccess.Local.Port);
                CheckResult = null;
            }
            catch (Exception ex)
            {
                CheckResult = "Не удалось запустить: " + ex.Message;
            }
            Refresh();
        }

        /// <summary>
        /// Самопроверка: ping на адреса обоих коннекторов по HTTP и поиск коннектора в настройках Claude Code, Claude Desktop и Codex.
        /// Поток Revit не проверяем — пока открыто модальное окно, Revit всё равно не выполнит запрос.
        /// </summary>
        public async void Check()
        {
            CheckResult = "Проверка…";
            string result = await Task.Run(() => RunCheck());
            CheckResult = result;
            Refresh();
        }

        private static string RunCheck()
        {
            var lines = new StringBuilder();
            if (!McpServer.IsRunning)
            {
                lines.AppendLine("✕ Коннектор остановлен — нажмите «Запустить».");
            }
            else
            {
                foreach (McpClient client in McpClient.All)
                {
                    try
                    {
                        var watch = Stopwatch.StartNew();
                        var request = (HttpWebRequest)WebRequest.Create(McpServer.UrlFor(client));
                        request.Method = "POST";
                        request.ContentType = "application/json";
                        request.Headers["X-TNov-SelfTest"] = "1";
                        request.Timeout = 5000;
                        request.Proxy = null;
                        byte[] body = Encoding.UTF8.GetBytes("{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"ping\"}");
                        using (Stream stream = request.GetRequestStream()) stream.Write(body, 0, body.Length);
                        using (var response = (HttpWebResponse)request.GetResponse())
                        using (var reader = new StreamReader(response.GetResponseStream()))
                        {
                            JObject reply = JObject.Parse(reader.ReadToEnd());
                            if (reply["result"] == null) throw new InvalidOperationException("неожиданный ответ сервера");
                        }
                        lines.AppendLine($"✓ Адрес {client.Name} отвечает ({watch.ElapsedMilliseconds} мс).");
                    }
                    catch (Exception ex)
                    {
                        lines.AppendLine($"✕ Адрес {client.Name} не отвечает: " + ex.Message);
                    }
                }
            }

            switch (ClaudeCodeConfigured())
            {
                case true:
                    lines.AppendLine("✓ Коннектор прописан в Claude Code.");
                    break;
                case false:
                    lines.AppendLine("– В Claude Code коннектор не прописан — выполните команду из раздела Claude.");
                    break;
            }
            switch (ClaudeDesktopSetup.IsConfigured())
            {
                case true:
                    lines.AppendLine("✓ Мост прописан в Claude Desktop (обычный чат).");
                    break;
                case false:
                    lines.AppendLine("– В Claude Desktop мост не прописан (нужен только для обычного чата).");
                    break;
            }
            switch (CodexSetup.IsConfigured())
            {
                case true:
                    lines.AppendLine("✓ Коннектор прописан в Codex.");
                    break;
                case false:
                    lines.AppendLine("– В Codex коннектор не прописан — нажмите «Подключить к Codex».");
                    break;
            }
            if (CodexSetup.IsConfigured() == true) lines.AppendLine(BridgeLogLine(McpClient.Codex));
            switch (QwenSetup.IsConfigured())
            {
                case true:
                    lines.AppendLine("✓ Коннектор прописан в Qwen Code.");
                    lines.AppendLine(BridgeLogLine(McpClient.Qwen));
                    break;
                case false:
                    lines.AppendLine("– В Qwen Code коннектор не прописан — нажмите «Подключить к Qwen Code».");
                    break;
            }
            return lines.ToString().TrimEnd();
        }

        /// <summary>Последняя запись моста этого клиента — запускает ли клиент мост вообще.</summary>
        private static string BridgeLogLine(McpClient client)
        {
            string tag = "[" + client.Name + " ";
            string last = null, lastLog = null;
            foreach (string log in ClaudeDesktopSetup.BridgeLogs())
            {
                try
                {
                    string line = File.ReadAllLines(log).LastOrDefault(l => l.Contains(tag));
                    // Строки начинаются с даты yyyy-MM-dd HH:mm:ss — сравниваем как текст.
                    if (line != null && (last == null || string.CompareOrdinal(line, last) > 0))
                    {
                        last = line;
                        lastLog = log;
                    }
                }
                catch (Exception) { }
            }
            if (last == null)
                return $"✕ {client.Name} ни разу не запускал мост. Проверьте, что {client.Name} перезапущен и это версия для компьютера, а не сайт.";
            string where = lastLog.IndexOf(@"\Packages\", StringComparison.OrdinalIgnoreCase) >= 0
                ? " (журнал в папке приложения из Microsoft Store)"
                : "";
            return $"• Мост {client.Name}, последняя запись{where}: {Trim(last, 200)}";
        }

        /// <summary>true/false — есть ли адрес коннектора в ~/.claude.json; null — файла нет.</summary>
        private static bool? ClaudeCodeConfigured()
        {
            try
            {
                string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".claude.json");
                if (!File.Exists(path)) return null;
                string text = File.ReadAllText(path);
                int port = McpServer.IsRunning ? McpServer.Port : McpAccess.Local.Port;
                return text.Contains("localhost:" + port + "/mcp") || text.Contains("127.0.0.1:" + port + "/mcp");
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static void Set(McpClientStatus status, string text, Brush brush)
        {
            status.StatusText = text;
            status.StatusBrush = brush;
        }

        private static string Ago(TimeSpan span)
        {
            if (span.TotalSeconds < 60) return "только что";
            if (span.TotalMinutes < 60) return $"{(int)span.TotalMinutes} мин назад";
            return $"{(int)span.TotalHours} ч назад";
        }

        private static string Trim(string s, int max) => s.Length <= max ? s : s.Substring(0, max) + "…";

        private static Brush Frozen(Color color)
        {
            var brush = new SolidColorBrush(color);
            brush.Freeze();
            return brush;
        }

        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
