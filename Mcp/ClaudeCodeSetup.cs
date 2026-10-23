using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

namespace TNov.Mcp
{
    /// <summary>
    /// Подключение Claude Code (вкладка Code в приложении Claude и программа claude для терминала) через мост.
    ///
    /// Раньше Claude Code подключался прямо по http://localhost:{порт}/mcp: после перезапуска Revit или
    /// остановки коннектора связь рвалась до нового сеанса, и инструменты Revit пропадали из чата.
    /// Мост (TNovMcpBridge.ps1, stdio) переживает перезапуск Revit: пока коннектора нет, отвечает
    /// «Revit не отвечает», затем снова пропускает запросы и сообщает об обновлении инструментов.
    ///
    /// Куда пишем сервер «revit»:
    ///   рабочая папка %USERPROFILE%\Claude\Revit\.mcp.json (и прежняя C:\Claude\Revit из инструкции, если есть) —
    ///     для вкладки Code; у настроек папки приоритет над пользовательскими;
    ///   пользовательские настройки Claude Code — через «claude mcp add», если программа claude есть,
    ///     иначе правкой %USERPROFILE%\.claude.json (с копией .bak-tnov).
    /// Чужой сервер «revit» (не TNov) не трогаем.
    /// </summary>
    internal static class ClaudeCodeSetup
    {
        private const string ServerName = "revit";
        private const string BridgeFileName = "TNovMcpBridge.ps1";

        public static string WorkFolder => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Claude", "Revit");
        private const string LegacyWorkFolder = @"C:\Claude\Revit";
        private static string UserConfig => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".claude.json");

        private static string[] BridgeArgs(int port) =>
            new[] { "-NoProfile", "-ExecutionPolicy", "Bypass", "-File", ClaudeDesktopSetup.BridgePath, "-Port", port.ToString(), "-Client", "Claude" };

        private static JObject Entry(int port) => new JObject
        {
            ["type"] = "stdio",
            ["command"] = "powershell.exe",
            ["args"] = new JArray(BridgeArgs(port)),
            ["env"] = new JObject()
        };

        /// <summary>Команда для ручного подключения в терминале (для окна «Нейросетошная»).</summary>
        public static string Command(int port) =>
            "claude mcp add revit --scope user -- powershell.exe " +
            string.Join(" ", BridgeArgs(port).Select(a => a.Contains(" ") ? "\"" + a + "\"" : a));

        /// <summary>Ставит мост и прописывает его для Claude Code. Возвращает текст для пользователя. Вызывать вне UI-потока.</summary>
        public static string Install(int port)
        {
            ClaudeDesktopSetup.WriteBridge();
            var done = new List<string>();

            // 1. Рабочие папки для вкладки Code.
            Directory.CreateDirectory(WorkFolder);
            MergeConfig(Path.Combine(WorkFolder, ".mcp.json"), port);
            done.Add("папка " + WorkFolder);
            if (Directory.Exists(LegacyWorkFolder) && !string.Equals(LegacyWorkFolder, WorkFolder, StringComparison.OrdinalIgnoreCase))
            {
                MergeConfig(Path.Combine(LegacyWorkFolder, ".mcp.json"), port);
                done.Add("папка " + LegacyWorkFolder);
            }

            // 2. Пользовательские настройки — для любой папки и для программы claude в терминале.
            string cli = FindCli();
            string userNote;
            if (cli != null && RunCli(cli, port, out string cliError))
            {
                userNote = "пользовательские настройки (claude mcp add)";
            }
            else if (File.Exists(UserConfig))
            {
                MergeConfig(UserConfig, port);
                userNote = "пользовательские настройки (.claude.json)";
            }
            else
            {
                userNote = null; // Claude Code ещё ни разу не запускался — хватит рабочей папки
            }
            if (userNote != null) done.Add(userNote);

            return "Мост прописан для Claude Code: " + string.Join(", ", done) + ". " +
                   "Начните новый сеанс Claude Code (вкладка Code → папка " + WorkFolder + ") и при первом запуске разрешите сервер revit. " +
                   "Теперь после перезапуска Revit новый сеанс не нужен.";
        }

        /// <summary>true — мост прописан; false — Claude Code есть, но моста нет; null — Claude Code не найден.</summary>
        public static bool? IsConfigured()
        {
            try
            {
                var files = new[] { Path.Combine(WorkFolder, ".mcp.json"), Path.Combine(LegacyWorkFolder, ".mcp.json"), UserConfig };
                bool any = false;
                foreach (string f in files.Where(File.Exists))
                {
                    any = true;
                    JToken entry = JObject.Parse(File.ReadAllText(f))["mcpServers"]?[ServerName];
                    if (entry != null && entry.ToString().Contains(BridgeFileName)) return true;
                }
                return any ? false : (bool?)null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>Прописан ли ещё прямой адрес (старая схема без моста) — для подсказки в «Проверить».</summary>
        public static bool HasDirectUrl(int port)
        {
            try
            {
                return new[] { Path.Combine(WorkFolder, ".mcp.json"), Path.Combine(LegacyWorkFolder, ".mcp.json"), UserConfig }
                    .Where(File.Exists)
                    .Any(f => IsOurDirect(JObject.Parse(File.ReadAllText(f))["mcpServers"]?[ServerName]));
            }
            catch (Exception) { return false; }
        }

        /// <summary>Записать сервер revit в файл настроек (.mcp.json или .claude.json), не трогая остальное.</summary>
        private static void MergeConfig(string path, int port)
        {
            JObject root;
            if (File.Exists(path))
            {
                string text = File.ReadAllText(path);
                try { root = string.IsNullOrWhiteSpace(text) ? new JObject() : JObject.Parse(text); }
                catch (JsonException)
                {
                    throw new InvalidOperationException($"Файл настроек не читается как JSON: {path}. Исправьте его и повторите.");
                }
            }
            else
            {
                root = new JObject();
            }

            if (!(root["mcpServers"] is JObject servers))
            {
                servers = new JObject();
                root["mcpServers"] = servers;
            }
            JToken existing = servers[ServerName];
            if (existing != null && !existing.ToString().Contains(BridgeFileName) && !IsOurDirect(existing))
                throw new InvalidOperationException($"В {path} уже есть сервер «{ServerName}», прописанный не TNov. Удалите его или переименуйте и повторите.");

            JObject entry = Entry(port);
            if (existing != null && JToken.DeepEquals(existing, entry)) return; // уже прописан так же

            servers[ServerName] = entry;
            if (File.Exists(path)) File.Copy(path, path + ".bak-tnov", true);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, JsonConvert.SerializeObject(root, Formatting.Indented), new UTF8Encoding(false));
        }

        /// <summary>Наш прежний прямой адрес: http://localhost:{порт}/mcp.</summary>
        private static bool IsOurDirect(JToken entry)
        {
            string url = (string)entry?["url"];
            if (string.IsNullOrEmpty(url) || !Uri.TryCreate(url, UriKind.Absolute, out Uri uri)) return false;
            return uri.IsLoopback && uri.AbsolutePath.TrimEnd('/').Equals("/mcp", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>Путь к программе claude (claude.exe или claude.cmd из npm), если она есть в PATH.</summary>
        private static string FindCli()
        {
            string path = Environment.GetEnvironmentVariable("PATH") ?? "";
            foreach (string dir in path.Split(Path.PathSeparator).Where(d => !string.IsNullOrWhiteSpace(d)))
            {
                foreach (string name in new[] { "claude.exe", "claude.cmd" })
                {
                    try
                    {
                        string candidate = Path.Combine(dir.Trim(), name);
                        if (File.Exists(candidate)) return candidate;
                    }
                    catch (Exception) { } // кривой элемент PATH
                }
            }
            return null;
        }

        /// <summary>claude mcp remove + add в пользовательские настройки. false — не получилось (тогда правим файл сами).</summary>
        private static bool RunCli(string cli, int port, out string error)
        {
            error = null;
            string addArgs = "mcp add revit --scope user -- powershell.exe " +
                             string.Join(" ", BridgeArgs(port).Select(a => "\"" + a + "\""));
            try
            {
                Run(cli, "mcp remove revit --scope user", out _); // прежней записи может не быть — это не ошибка
                if (Run(cli, addArgs, out string output)) return true;
                error = output;
                return false;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        private static bool Run(string cli, string arguments, out string output)
        {
            bool cmd = cli.EndsWith(".cmd", StringComparison.OrdinalIgnoreCase);
            var info = new ProcessStartInfo
            {
                FileName = cmd ? "cmd.exe" : cli,
                Arguments = cmd ? "/d /s /c \"\"" + cli + "\" " + arguments + "\"" : arguments,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            using (Process process = Process.Start(info))
            {
                string stdout = process.StandardOutput.ReadToEnd();
                string stderr = process.StandardError.ReadToEnd();
                if (!process.WaitForExit(30000)) { try { process.Kill(); } catch (Exception) { } output = "таймаут"; return false; }
                output = (stdout + " " + stderr).Trim();
                return process.ExitCode == 0;
            }
        }
    }
}
