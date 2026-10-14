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
    /// <summary>Активность Claude за сеанс Revit — для мониторинга в окне «Настройки».</summary>
    internal static class McpActivity
    {
        private static readonly object _lock = new object();

        public static DateTime? LastRequestUtc;
        public static string ClientName { get; private set; }
        public static string LastTool { get; private set; }
        public static string LastError { get; private set; }
        public static DateTime? LastErrorUtc { get; private set; }
        public static int Calls { get; private set; }
        public static int Errors { get; private set; }
        /// <summary>Модель последнего запроса: читать Revit API из окна нельзя, поэтому запоминаем при вызове.</summary>
        public static string LastModel { get; private set; }
        public static string LastModelKind { get; private set; }
        public static int LastEffectiveLevel { get; private set; }

        public static void OnModel(string model, string kind, int effectiveLevel)
        {
            LastModel = model;
            LastModelKind = kind;
            LastEffectiveLevel = effectiveLevel;
        }

        public static void OnInitialize(JObject clientInfo)
        {
            string name = (string)clientInfo?["name"];
            string version = (string)clientInfo?["version"];
            if (name != null) ClientName = version != null ? name + " " + version : name;
        }

        public static void OnToolCall(string tool, string error)
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

    /// <summary>Модель блока «Коннектор Claude» в окне «Настройки» и окна кнопки Claude. Обновляется таймером окна.</summary>
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

        public string StatusText { get; private set; }
        public Brush StatusBrush { get; private set; }
        public string Details { get; private set; }
        /// <summary>Уровень доступа 1–3 (шкала в окне кнопки Claude).</summary>
        public int Level { get; private set; }
        public string LevelCaption { get; private set; }

        /// <summary>Предупреждение о других MCP-коннекторах в Revit (null — их нет). Сборки грузятся при старте Revit, поэтому считаем один раз.</summary>
        public string OtherMcpWarning { get; } = BuildOtherMcpWarning();

        private static string BuildOtherMcpWarning()
        {
            List<string> others = ClaudeConnector.OtherMcpAddins();
            if (others.Count == 0) return null;
            return $"⚠ В Revit загружен другой MCP-коннектор: {string.Join(", ", others)}. " +
                   "Его действия уровнями доступа TNov не ограничиваются и в журнал TNov не попадают. " +
                   "Для работы и тестов отключите его в Claude Code или удалите надстройку.";
        }

        /// <summary>Переключатель уровня: в личной — 1–3, в корпоративной — в пределах доступного (скрыт, если доступен только 1).</summary>
        public bool CanChooseLevel => LevelOptions.Length > 1;
        public int[] LevelOptions { get; private set; } = { 1 };
        public string LevelChoiceLabel => McpAccess.IsCorp
            ? "Уровень для работы (не выше доступного вам)"
            : "Уровень Claude (уровни 2–3 — только в отсоединённых моделях)";
        public int SelectedLevel
        {
            get => Level;
            set
            {
                if (value == McpAccess.Level) return;
                McpAccess.SetLevel(value);
                CheckResult = "Уровень сохранён. Новые инструменты Claude увидит в новом сеансе.";
                Refresh();
            }
        }
        /// <summary>Клиент и счётчики запросов — без адреса и уровня, они в окне показаны отдельно.</summary>
        public string Activity { get; private set; }
        public string Url => McpServer.IsRunning ? McpServer.Url : "коннектор остановлен";
        public string ConnectCommand => ClaudeConnector.ConnectCommand;

        public void CopyConnectCommand()
        {
            try { System.Windows.Clipboard.SetText(ConnectCommand); CheckResult = "Команда подключения скопирована."; }
            catch (Exception) { CheckResult = "Не удалось скопировать — выделите команду и нажмите Ctrl+C."; }
        }

        /// <summary>Подключение к обычному чату Claude Desktop через мост stdio -> HTTP.</summary>
        public void ConnectClaudeDesktop()
        {
            try { CheckResult = ClaudeDesktopSetup.Install(McpServer.IsRunning ? McpServer.Port : McpAccess.Local.Port); }
            catch (Exception ex) { CheckResult = "Не удалось подключить Claude Desktop: " + ex.Message; }
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
            DateTime? last = McpActivity.LastRequestUtc;
            if (!McpAccess.Enabled && !McpServer.IsRunning)
            {
                Set("Отключён администратором", Gray);
            }
            else if (!McpServer.IsRunning)
            {
                Set("Остановлен", Gray);
            }
            else if (last == null)
            {
                Set("Запущен — Claude ещё не подключался", Amber);
            }
            else
            {
                TimeSpan idle = DateTime.UtcNow - last.Value;
                if (idle.TotalMinutes < ActiveMinutes)
                    Set("Claude подключён · запрос " + Ago(idle), Green);
                else
                    Set("Запущен — нет запросов " + Ago(idle), Amber);
            }

            var details = new StringBuilder();
            if (McpServer.IsRunning) details.AppendLine("Адрес: " + McpServer.Url);
            int level = McpAccess.Level;
            details.Append("Уровень доступа: " + McpAccess.LevelName(level));
            if (McpAccess.AssignedLevel == null && level < 2) details.Append(" (до уровня 2: " + McpAccess.Level2Progress + ")");
            details.AppendLine();
            if (McpActivity.ClientName != null) details.AppendLine("Клиент: " + McpActivity.ClientName);
            if (McpActivity.Calls > 0)
            {
                details.AppendLine($"Вызовов за сеанс: {McpActivity.Calls}, ошибок: {McpActivity.Errors}. Последний: {McpActivity.LastTool}");
                if (McpActivity.LastError != null && McpActivity.LastErrorUtc != null)
                    details.AppendLine($"Последняя ошибка ({Ago(DateTime.UtcNow - McpActivity.LastErrorUtc.Value)}): {Trim(McpActivity.LastError, 160)}");
            }
            Details = details.ToString().TrimEnd();

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
            if (McpActivity.LastModel != null && McpActivity.LastEffectiveLevel > 0 && McpActivity.LastEffectiveLevel < level)
                LevelCaption += $"\nВ модели «{McpActivity.LastModel}» ({McpActivity.LastModelKind}) действует уровень {McpActivity.LastEffectiveLevel}.";
            var activity = new StringBuilder();
            if (McpActivity.ClientName != null) activity.AppendLine("Клиент: " + McpActivity.ClientName);
            if (McpActivity.Calls > 0)
                activity.AppendLine($"Запросов за сеанс: {McpActivity.Calls}, ошибок: {McpActivity.Errors}. Последний: {McpActivity.LastTool}");
            Activity = activity.ToString().TrimEnd();

            OnPropertyChanged(nameof(StatusText));
            OnPropertyChanged(nameof(StatusBrush));
            OnPropertyChanged(nameof(Details));
            OnPropertyChanged(nameof(ToggleText));
            OnPropertyChanged(nameof(CanToggle));
            OnPropertyChanged(nameof(Level));
            OnPropertyChanged(nameof(LevelCaption));
            OnPropertyChanged(nameof(LevelOptions));
            OnPropertyChanged(nameof(CanChooseLevel));
            OnPropertyChanged(nameof(LevelChoiceLabel));
            OnPropertyChanged(nameof(SelectedLevel));
            OnPropertyChanged(nameof(Activity));
            OnPropertyChanged(nameof(Url));
            OnPropertyChanged(nameof(ConnectCommand));
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
        /// Самопроверка: ping на собственный адрес по HTTP и поиск коннектора в настройках Claude Code.
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
                try
                {
                    var watch = Stopwatch.StartNew();
                    var request = (HttpWebRequest)WebRequest.Create(McpServer.Url);
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
                    lines.AppendLine($"✓ Сервер отвечает ({watch.ElapsedMilliseconds} мс).");
                }
                catch (Exception ex)
                {
                    lines.AppendLine("✕ Сервер не отвечает: " + ex.Message);
                }
            }

            switch (ClaudeCodeConfigured())
            {
                case true:
                    lines.AppendLine("✓ Коннектор прописан в Claude Code.");
                    break;
                case false:
                    lines.AppendLine("✕ В Claude Code коннектор не найден — выполните команду подключения (кнопка «Claude» на ленте).");
                    break;
                default:
                    lines.AppendLine("? Claude Code на этом компьютере не найден.");
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
            return lines.ToString().TrimEnd();
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

        private void Set(string text, Brush brush)
        {
            StatusText = text;
            StatusBrush = brush;
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
