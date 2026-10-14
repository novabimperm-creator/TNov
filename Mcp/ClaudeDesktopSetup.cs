using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace TNov.Mcp
{
    /// <summary>
    /// Подключение коннектора к обычному чату Claude Desktop.
    /// Чат запускает локальные MCP-серверы как программу (stdio), поэтому ставим мост
    /// Mcp\Bridge\TNovMcpBridge.ps1 (вшит в TNov.dll) в %LOCALAPPDATA%\TNov\mcp и прописываем его
    /// в claude_desktop_config.json — обычную установку и установку из Microsoft Store.
    /// </summary>
    internal static class ClaudeDesktopSetup
    {
        private const string ServerName = "revit";
        private const string BridgeFileName = "TNovMcpBridge.ps1";
        private const string ResourceName = "TNov.Mcp." + BridgeFileName;

        public static string BridgePath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TNov", "mcp", BridgeFileName);

        /// <summary>Ставит мост и прописывает его в настройки Claude Desktop. Возвращает текст для пользователя.</summary>
        public static string Install(int port)
        {
            WriteBridge();

            List<string> configs = ConfigPaths(out bool desktopFound);
            foreach (string path in configs) Merge(path, port);

            string result = "Мост прописан в Claude Desktop. Полностью закройте Claude Desktop (значок в трее → Выход) и откройте снова — " +
                            "в чате появятся инструменты Revit.";
            if (!desktopFound)
                result = "Claude Desktop на этом компьютере не найден, настройки записаны заранее. Установите приложение Claude и войдите — инструменты Revit появятся в чате.";
            return result;
        }

        /// <summary>true — мост прописан; false — Claude Desktop есть, но моста нет; null — Claude Desktop не найден.</summary>
        public static bool? IsConfigured()
        {
            try
            {
                List<string> configs = ConfigPaths(out bool desktopFound);
                if (!desktopFound) return null;
                return configs.Any(p => File.Exists(p) && File.ReadAllText(p).Contains(BridgeFileName));
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static void WriteBridge()
        {
            string text;
            using (Stream stream = typeof(ClaudeDesktopSetup).Assembly.GetManifestResourceStream(ResourceName)
                   ?? throw new InvalidOperationException("Мост не найден в сборке TNov."))
            using (var reader = new StreamReader(stream, Encoding.UTF8))
                text = reader.ReadToEnd();

            Directory.CreateDirectory(Path.GetDirectoryName(BridgePath));
            // С BOM: Windows PowerShell 5.1 без него читает кириллицу в скрипте как ANSI.
            File.WriteAllText(BridgePath, text, new UTF8Encoding(true));
        }

        /// <summary>Файлы настроек всех найденных установок Claude Desktop (или обычный путь, если не найдено ни одной).</summary>
        private static List<string> ConfigPaths(out bool desktopFound)
        {
            var dirs = new List<string>();
            string roaming = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Claude");
            if (Directory.Exists(roaming)) dirs.Add(roaming);

            // Версия из Microsoft Store хранит настройки в виртуализированной папке пакета.
            string packages = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Packages");
            if (Directory.Exists(packages))
            {
                foreach (string package in Directory.GetDirectories(packages, "Claude_*"))
                {
                    string dir = Path.Combine(package, "LocalCache", "Roaming", "Claude");
                    if (Directory.Exists(dir)) dirs.Add(dir);
                }
            }

            desktopFound = dirs.Count > 0;
            if (!desktopFound) dirs.Add(roaming);
            return dirs.Select(d => Path.Combine(d, "claude_desktop_config.json")).ToList();
        }

        private static void Merge(string path, int port)
        {
            JObject root;
            if (File.Exists(path))
            {
                string text = File.ReadAllText(path);
                try { root = string.IsNullOrWhiteSpace(text) ? new JObject() : JObject.Parse(text); }
                catch (JsonException)
                {
                    // Чужие настройки не затираем.
                    throw new InvalidOperationException($"Файл настроек Claude Desktop повреждён: {path}. Исправьте или удалите его и повторите.");
                }
                File.Copy(path, path + ".bak-tnov", true);
            }
            else
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                root = new JObject();
            }

            if (!(root["mcpServers"] is JObject servers))
            {
                servers = new JObject();
                root["mcpServers"] = servers;
            }
            servers[ServerName] = new JObject
            {
                ["command"] = "powershell.exe",
                ["args"] = new JArray("-NoProfile", "-ExecutionPolicy", "Bypass", "-File", BridgePath, "-Port", port.ToString())
            };

            File.WriteAllText(path, JsonConvert.SerializeObject(root, Formatting.Indented), new UTF8Encoding(false));
        }
    }
}
