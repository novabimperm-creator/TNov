using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.IO;
using System.Text;
using System.Threading;
using TNovCommon;
using TNovCommon.Server;

namespace TNov.Mcp
{
    /// <summary>
    /// Журнал действий Claude — только в корпоративной конфигурации.
    ///   {ServerPath}mcp\{логин}.txt — строка на каждый вызов инструмента (через ServerOutbox: переживает недоступность шары);
    ///   {ServerPath}mcp\scripts\{логин}_{время}.cs — полный текст каждого C#-скрипта
    ///   (нет доступа к шаре — %LOCALAPPDATA%\TNov\mcp-scripts).
    /// Формат строки: дата;логин;модель;тип модели;уровень выбранный/доступный;уровень в модели;инструмент;итог;аргументы.
    /// </summary>
    internal static class McpLog
    {
        private const int MaxArgsLength = 300;

        /// <summary>Сведения о модели на момент вызова (собираются на потоке Revit).</summary>
        public sealed class Context
        {
            public string Model = "-";
            public string Kind = "-";
            public int EffectiveLevel;
        }

        public static void Write(string tool, JObject args, Context context, string outcome, string scriptFile)
        {
            if (!McpAccess.IsCorp) return;
            try
            {
                string argsText = tool == "execute_csharp"
                    ? "script=" + (scriptFile ?? "-")
                    : JsonConvert.SerializeObject(args, Formatting.None);
                if (argsText.Length > MaxArgsLength) argsText = argsText.Substring(0, MaxArgsLength) + "…";

                string line = string.Join(";",
                    DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                    Environment.UserName,
                    Clean(context?.Model),
                    Clean(context?.Kind),
                    McpAccess.Level + "/" + McpAccess.MaxLevel,
                    context?.EffectiveLevel ?? 0,
                    tool,
                    Clean(outcome),
                    Clean(argsText));
                ServerOutbox.AppendLine("mcp/" + Environment.UserName + ".txt", line);
            }
            catch (Exception) { } // журнал не должен ломать работу Claude
        }

        /// <summary>Сохраняет текст скрипта фоном и возвращает имя файла для строки журнала (null — не корпоративная).</summary>
        public static string SaveScript(string code)
        {
            if (!McpAccess.IsCorp || string.IsNullOrEmpty(code)) return null;

            string name = $"{Environment.UserName}_{DateTime.Now:yyyyMMdd_HHmmss_fff}.cs";
            string root = ServerData.NormalizeRoot(TNovConfigLoad.GetCachedConfig()?.ServerPath);
            ThreadPool.QueueUserWorkItem(_ =>
            {
                byte[] bytes = new UTF8Encoding(true).GetBytes(code);
                try
                {
                    string dir = Path.Combine(root, "mcp", "scripts");
                    Directory.CreateDirectory(dir);
                    File.WriteAllBytes(Path.Combine(dir, name), bytes);
                }
                catch (Exception)
                {
                    try
                    {
                        string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TNov", "mcp-scripts");
                        Directory.CreateDirectory(dir);
                        File.WriteAllBytes(Path.Combine(dir, name), bytes);
                    }
                    catch (Exception) { }
                }
            });
            return name;
        }

        private static string Clean(string s) =>
            string.IsNullOrEmpty(s) ? "-" : s.Replace(";", ",").Replace("\r", " ").Replace("\n", " ");
    }
}
