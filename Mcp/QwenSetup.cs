using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.IO;
using System.Text;

namespace TNov.Mcp
{
    /// <summary>
    /// Подключение коннектора к Qwen Code (консольный агент Alibaba): блок mcpServers в ~/.qwen/settings.json.
    /// Прописываем тот же мост stdio -> HTTP, что и для Claude Desktop и Codex, с -Client Qwen (адрес /qwen):
    /// коннектор запускается только кнопкой, а мост даёт Qwen Code стартовать и без Revit.
    /// Свою запись узнаём по пути к мосту; чужой сервер «revit» не трогаем.
    /// </summary>
    internal static class QwenSetup
    {
        private const string ServerName = "revit";
        private const string BridgeFileName = "TNovMcpBridge.ps1";

        /// <summary>Скрипты в Revit работают до 10 минут, мост ждёт ответа 660 с (таймаут Qwen Code — в мс).</summary>
        private const int TimeoutMs = 660000;

        private static string Home => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".qwen");

        public static string ConfigPath => Path.Combine(Home, "settings.json");

        private static JObject Entry(int port) => new JObject
        {
            ["command"] = "powershell.exe",
            ["args"] = new JArray("-NoProfile", "-ExecutionPolicy", "Bypass", "-File", ClaudeDesktopSetup.BridgePath,
                                  "-Port", port.ToString(), "-Client", "Qwen"),
            ["timeout"] = TimeoutMs
        };

        /// <summary>Фрагмент settings.json для ручной установки — то же, что пишет кнопка.</summary>
        public static string Snippet(int port)
        {
            var root = new JObject { ["mcpServers"] = new JObject { [ServerName] = Entry(port) } };
            return JsonConvert.SerializeObject(root, Formatting.Indented);
        }

        /// <summary>Ставит мост и прописывает его в settings.json. Возвращает текст для пользователя.</summary>
        public static string Install(int port)
        {
            bool qwenFound = Directory.Exists(Home);
            string path = ConfigPath;

            JObject root;
            if (File.Exists(path))
            {
                string text = File.ReadAllText(path);
                try { root = string.IsNullOrWhiteSpace(text) ? new JObject() : JObject.Parse(text); }
                catch (JsonException)
                {
                    // Чужие настройки не затираем.
                    throw new InvalidOperationException($"Файл настроек Qwen Code не читается как JSON: {path}. Исправьте его и повторите.");
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
            if (servers[ServerName] is JToken existing && !existing.ToString().Contains(BridgeFileName))
                throw new InvalidOperationException(
                    $"В {path} уже есть сервер «{ServerName}», прописанный не TNov. Удалите его или переименуйте и повторите.");
            servers[ServerName] = Entry(port);

            ClaudeDesktopSetup.WriteBridge();
            Directory.CreateDirectory(Home);
            if (File.Exists(path)) File.Copy(path, path + ".bak-tnov", true);
            File.WriteAllText(path, JsonConvert.SerializeObject(root, Formatting.Indented), new UTF8Encoding(false));

            return qwenFound
                ? "Коннектор прописан в Qwen Code. Перезапустите Qwen Code (или начните новый сеанс) — появятся инструменты Revit, проверить: команда /mcp."
                : "Qwen Code на этом компьютере не найден, настройки записаны заранее в " + path + ".";
        }

        /// <summary>true — запись TNov прописана; false — Qwen Code есть, но записи нет; null — Qwen Code не найден.</summary>
        public static bool? IsConfigured()
        {
            try
            {
                string path = ConfigPath;
                if (!File.Exists(path)) return Directory.Exists(Home) ? false : (bool?)null;
                JObject root = JObject.Parse(File.ReadAllText(path));
                JToken entry = root["mcpServers"]?[ServerName];
                return entry != null && entry.ToString().Contains(BridgeFileName);
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
