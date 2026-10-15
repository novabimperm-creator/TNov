using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace TNov.Mcp
{
    /// <summary>
    /// Подключение коннектора к Codex (CLI, расширение IDE, приложение — у них общий ~/.codex/config.toml).
    /// Прописываем тот же мост stdio -> HTTP, что и для Claude Desktop, с -Client Codex (адрес /codex):
    /// коннектор запускается только кнопкой, а мост даёт Codex стартовать и без Revit.
    /// Свой блок помечаем BEGIN/END и при повторной установке заменяем только его.
    /// </summary>
    internal static class CodexSetup
    {
        private const string ServerName = "revit";
        private const string BeginMarker = "# BEGIN tnov-revit managed configuration";
        private const string EndMarker = "# END tnov-revit managed configuration";

        private static readonly Regex ManagedBlock = new Regex(
            "^" + Regex.Escape(BeginMarker) + @"\r?\n.*?^" + Regex.Escape(EndMarker) + @"\r?\n?",
            RegexOptions.Multiline | RegexOptions.Singleline);

        private static readonly Regex ForeignEntry = new Regex(
            @"^\s*\[mcp_servers\.(?:revit|""revit""|'revit')(?:\.|\])", RegexOptions.Multiline);

        /// <summary>Таблица [mcp_servers.revit] вместе с подтаблицами (.env и т.п.) — до следующей чужой таблицы или конца файла.</summary>
        private static readonly Regex ForeignTable = new Regex(
            @"^[ \t]*\[mcp_servers\.(?:revit|""revit""|'revit')(?:\.[^\]\r\n]*)?\][^\r\n]*\r?\n.*?(?=^[ \t]*\[(?!mcp_servers\.(?:revit|""revit""|'revit')[.\]])|\z)",
            RegexOptions.Multiline | RegexOptions.Singleline);

        /// <summary>Папка настроек Codex: CODEX_HOME или %USERPROFILE%\.codex.</summary>
        private static string Home
        {
            get
            {
                string custom = Environment.GetEnvironmentVariable("CODEX_HOME");
                return !string.IsNullOrWhiteSpace(custom)
                    ? custom
                    : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".codex");
            }
        }

        public static string ConfigPath => Path.Combine(Home, "config.toml");

        /// <summary>
        /// Блок для config.toml — то же, что пишет кнопка (с маркерами, чтобы кнопка потом его узнала).
        /// В окне показывается для ручной установки: команда `codex mcp add` таймауты не задаёт.
        /// </summary>
        public static string Block(int port)
        {
            // В TOML-строке в одинарных кавычках обратные слэши не экранируются; путь с апострофом — в двойных с экранированием.
            string bridge = ClaudeDesktopSetup.BridgePath;
            string quoted = bridge.Contains("'")
                ? "\"" + bridge.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\""
                : "'" + bridge + "'";
            return string.Join("\r\n",
                BeginMarker,
                $"[mcp_servers.{ServerName}]",
                "command = 'powershell.exe'",
                $"args = ['-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', {quoted}, '-Port', '{port}', '-Client', 'Codex']",
                "startup_timeout_sec = 20",
                // Скрипты в Revit работают до 10 минут, мост ждёт ответа 660 с.
                "tool_timeout_sec = 660",
                EndMarker) + "\r\n";
        }

        /// <summary>Ставит мост и прописывает его в config.toml. Возвращает текст для пользователя.</summary>
        public static string Install(int port)
        {
            bool codexFound = Directory.Exists(Home);
            string path = ConfigPath;
            string old = File.Exists(path) ? File.ReadAllText(path) : "";

            string block = Block(port);

            string updated;
            if (ManagedBlock.IsMatch(old))
                updated = ManagedBlock.Replace(old, _ => block, 1);
            else if (ForeignEntry.IsMatch(old))
            {
                // Прописан вручную (codex mcp add) на наш же мост — заменяем своим блоком с таймаутами.
                string own = string.Concat(ForeignTable.Matches(old).Cast<Match>().Select(m => m.Value));
                if (!own.Contains("TNovMcpBridge.ps1"))
                    throw new InvalidOperationException(
                        $"В {path} уже есть сервер «{ServerName}», прописанный не TNov. Удалите его или переименуйте и повторите.");
                string rest = ForeignTable.Replace(old, "").TrimEnd();
                updated = rest.Length == 0 ? block : rest + "\r\n\r\n" + block;
            }
            else
                updated = old.Length == 0 ? block : old.TrimEnd() + "\r\n\r\n" + block;

            ClaudeDesktopSetup.WriteBridge();
            if (updated != old)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                if (File.Exists(path)) File.Copy(path, path + ".bak-tnov", true);
                File.WriteAllText(path, updated, new UTF8Encoding(false));
            }

            return codexFound
                ? "Коннектор прописан в Codex. Перезапустите Codex (или начните новый сеанс) — появятся инструменты Revit."
                : "Codex на этом компьютере не найден, настройки записаны заранее в " + path + ".";
        }

        /// <summary>true — блок TNov прописан; false — Codex есть, но блока нет; null — Codex не найден.</summary>
        public static bool? IsConfigured()
        {
            try
            {
                string path = ConfigPath;
                if (!File.Exists(path)) return Directory.Exists(Home) ? false : (bool?)null;
                return File.ReadAllText(path).Contains(BeginMarker);
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
