using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;

namespace TNov.Mcp
{
    /// <summary>
    /// MCP-сервер (транспорт Streamable HTTP, ответы обычным JSON без SSE) внутри Revit.
    /// Слушает только петлевые адреса 127.0.0.1 и [::1] (LoopbackHttp): http://localhost:{порт}/mcp — Claude,
    /// http://localhost:{порт}/codex — Codex (см. McpClient). Из сети порт не открыт.
    /// </summary>
    internal static class McpServer
    {
        public const int DefaultPort = 48884;
        private const int PortAttempts = 10;

        private static readonly string[] SupportedProtocolVersions =
            { "2025-11-25", "2025-06-18", "2025-03-26", "2024-11-05" };

        private const string Instructions =
            "Сервер подключён к запущенному Autodesk Revit (плагин TNov). Все инструменты работают с активным документом.\n" +
            "- Id элементов — целые числа.\n" +
            "- Значения параметров выдаются и принимаются в единицах проекта, как в интерфейсе Revit (например, \"3000\" для длины в мм).\n" +
            "- Категории и параметры называйте как в интерфейсе (обычно по-русски) либо именами BuiltInCategory/BuiltInParameter (OST_Walls, ALL_MODEL_MARK).\n" +
            "- Начинайте с get_document_info: в нём уровень доступа в текущей модели (access) и права скриптов.\n" +
            "- Работайте с Revit только через этот сервер. Если в access.otherMcpConnectors есть другие коннекторы, не используйте их для изменений модели " +
            "(они обходят уровни доступа TNov) и предупредите пользователя.\n" +
            "- Уровни: 1 — чтение, оформление (аннотации, виды, листы, спецификации, изображения), экспорт; 2 — плюс 3D-элементы до 50 за запрос; 3 — всё, включая удаление. " +
            "В локальной копии файла-хранилища скрипты не меняют 3D-элементы ниже уровня 3; в личной конфигурации там действует только уровень 1.\n" +
            "- Задачи без отдельного инструмента (листы и спецификации, марки, проверки, подсчёты по всей модели, пересечения) решайте через execute_csharp. " +
            "Внутри Revit API длины во внутренних единицах (футы). Для объёмов и количеств удобен summarize_elements.\n" +
            "- Если вопрос касается стандартов и методик Новации, правил оформления или функций плагина TNov — сначала найдите ответ в базе знаний (wiki_search, затем wiki_read) и сошлитесь на статью.\n" +
            "- Каждое изменение — отдельная транзакция, пользователь может отменить её Ctrl+Z в Revit.\n" +
            "- Если Revit не отвечает, у пользователя открыт диалог или идёт команда: попросите его освободить Revit.";

        private static LoopbackHttp _server;

        public static bool IsRunning => _server != null;
        public static int Port { get; private set; }
        public static string Url => UrlFor(McpClient.Claude);
        public static string UrlFor(McpClient client) => $"http://localhost:{Port}{client.Path}";

        /// <summary>Запускает сервер на первом свободном порту начиная с preferredPort.</summary>
        public static void Start(int preferredPort)
        {
            if (IsRunning) return;

            Exception last = null;
            for (int port = preferredPort; port < preferredPort + PortAttempts; port++)
            {
                try
                {
                    // Только петлевые адреса: из сети порт недоступен (см. LoopbackHttp).
                    _server = LoopbackHttp.Start(port, Handle);
                }
                catch (SocketException ex)
                {
                    // Порт занят (например, второй экземпляр Revit) — пробуем следующий.
                    last = ex;
                    continue;
                }

                Port = port;
                McpAccess.RegisterFirstUseInBackground();
                return;
            }
            throw new InvalidOperationException(
                $"Не удалось открыть порты {preferredPort}–{preferredPort + PortAttempts - 1}: {last?.Message}", last);
        }

        public static void Stop()
        {
            LoopbackHttp server = _server;
            _server = null;
            server?.Stop();
        }

        private static LoopbackHttp.Response Handle(LoopbackHttp.Request request)
        {
            try
            {
                // Защита от DNS rebinding: страница из сети, разрешившая своё имя в 127.0.0.1,
                // пришлёт чужой Host. Принимаем только петлевые имена.
                if (!IsLoopbackHost(request.Header("Host"))) return Send(403, null);
                // Запросы со страниц браузера (требование спецификации MCP).
                string origin = request.Header("Origin");
                if (!string.IsNullOrEmpty(origin) && !IsLoopbackOrigin(origin)) return Send(403, null);

                McpClient client = McpClient.FromPath(request.Path.TrimEnd('/'));
                if (client == null) return Send(404, null);
                if (request.Method != "POST")
                {
                    // Поток SSE (GET) и завершение сессии (DELETE) не поддерживаем — спецификация разрешает 405.
                    LoopbackHttp.Response notAllowed = Send(405, null);
                    notAllowed.Headers["Allow"] = "POST";
                    return notAllowed;
                }
                // application/json обязателен: браузер не может прислать его без CORS-preflight, а preflight мы не одобряем.
                string contentType = request.Header("Content-Type");
                if (contentType == null || !contentType.StartsWith("application/json", StringComparison.OrdinalIgnoreCase))
                    return Send(415, null);

                bool selfTest = request.Header("X-TNov-SelfTest") != null;
                if (!selfTest) client.Activity.LastRequestUtc = DateTime.UtcNow;

                string body = Encoding.UTF8.GetString(request.Body);

                JToken message;
                try { message = JToken.Parse(body); }
                catch (JsonException)
                {
                    return Send(400, Error(null, -32700, "Parse error"));
                }

                JToken reply;
                if (message is JArray batch)
                {
                    var replies = new JArray(batch.OfType<JObject>().Select(m => HandleMessage(m, client)).Where(r => r != null));
                    reply = replies.Count > 0 ? replies : null;
                }
                else if (message is JObject single)
                {
                    reply = HandleMessage(single, client);
                }
                else
                {
                    reply = Error(null, -32600, "Invalid Request");
                }

                // Уведомления и ответы клиента: 202 без тела.
                return Send(reply == null ? 202 : 200, reply);
            }
            catch (Exception ex)
            {
                return Send(500, Error(null, -32603, ex.Message));
            }
        }

        private static JObject HandleMessage(JObject message, McpClient client)
        {
            string method = (string)message["method"];
            JToken id = message["id"];
            bool isNotification = id == null || id.Type == JTokenType.Null;
            if (method == null) return null; // ответ клиента на наш запрос — мы их не шлём

            try
            {
                JToken result;
                switch (method)
                {
                    case "initialize":
                        result = Initialize(message["params"] as JObject);
                        client.Activity.OnInitialize(message["params"]?["clientInfo"] as JObject);
                        break;
                    case "ping":
                        result = new JObject();
                        break;
                    case "tools/list":
                        result = new JObject { ["tools"] = McpTools.List() };
                        break;
                    case "tools/call":
                        result = McpTools.Call(message["params"] as JObject, client);
                        break;
                    default:
                        return isNotification ? null : Error(id, -32601, "Method not found: " + method);
                }
                if (isNotification) return null;
                return new JObject { ["jsonrpc"] = "2.0", ["id"] = id, ["result"] = result };
            }
            catch (Exception ex)
            {
                return isNotification ? null : Error(id, -32603, ex.Message);
            }
        }

        private static JObject Initialize(JObject parameters)
        {
            string requested = (string)parameters?["protocolVersion"];
            string version = SupportedProtocolVersions.Contains(requested) ? requested : SupportedProtocolVersions[0];
            return new JObject
            {
                ["protocolVersion"] = version,
                ["capabilities"] = new JObject { ["tools"] = new JObject { ["listChanged"] = false } },
                ["serverInfo"] = new JObject
                {
                    ["name"] = "tnov-revit",
                    ["title"] = "Revit (TNov)",
                    ["version"] = typeof(McpServer).Assembly.GetName().Version.ToString()
                },
                ["instructions"] = Instructions
            };
        }

        private static JObject Error(JToken id, int code, string message) => new JObject
        {
            ["jsonrpc"] = "2.0",
            ["id"] = id ?? JValue.CreateNull(),
            ["error"] = new JObject { ["code"] = code, ["message"] = message }
        };

        private static bool IsLoopbackOrigin(string origin)
        {
            if (!Uri.TryCreate(origin, UriKind.Absolute, out Uri uri)) return false;
            return uri.IsLoopback;
        }

        /// <summary>Host — только петлевое имя: localhost, 127.0.0.1, [::1] (с портом или без).</summary>
        private static bool IsLoopbackHost(string host)
        {
            if (string.IsNullOrEmpty(host)) return false;
            if (!Uri.TryCreate("http://" + host, UriKind.Absolute, out Uri uri)) return false;
            return uri.IsLoopback;
        }

        private static LoopbackHttp.Response Send(int status, JToken body)
        {
            var response = new LoopbackHttp.Response { Status = status };
            if (body != null)
            {
                // JsonConvert, а не JToken.ToString(Formatting): в Revit 2022 загружен Newtonsoft 11.
                response.Body = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(body, Formatting.None));
                response.ContentType = "application/json; charset=utf-8";
            }
            return response;
        }
    }
}
