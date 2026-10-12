using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;

namespace TNov.Mcp
{
    /// <summary>
    /// MCP-сервер (транспорт Streamable HTTP, ответы обычным JSON без SSE) внутри Revit.
    /// Слушает только loopback: http://localhost:{порт}/mcp. Claude Code подключается по этому адресу.
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

        private static HttpListener _listener;

        public static bool IsRunning => _listener != null && _listener.IsListening;
        public static int Port { get; private set; }
        public static string Url => $"http://localhost:{Port}/mcp";

        /// <summary>Запускает сервер на первом свободном порту начиная с preferredPort.</summary>
        public static void Start(int preferredPort)
        {
            if (IsRunning) return;

            Exception last = null;
            for (int port = preferredPort; port < preferredPort + PortAttempts; port++)
            {
                var listener = new HttpListener();
                listener.Prefixes.Add($"http://localhost:{port}/");
                try
                {
                    listener.Start();
                }
                catch (HttpListenerException ex)
                {
                    // Порт занят (например, второй экземпляр Revit) — пробуем следующий.
                    last = ex;
                    listener.Close();
                    continue;
                }

                _listener = listener;
                Port = port;
                new Thread(() => Listen(listener)) { IsBackground = true, Name = "TNov MCP" }.Start();
                McpAccess.RegisterFirstUseInBackground();
                return;
            }
            throw new InvalidOperationException(
                $"Не удалось открыть порты {preferredPort}–{preferredPort + PortAttempts - 1}: {last?.Message}", last);
        }

        public static void Stop()
        {
            HttpListener listener = _listener;
            _listener = null;
            if (listener == null) return;
            try { listener.Stop(); listener.Close(); }
            catch (Exception) { }
        }

        private static void Listen(HttpListener listener)
        {
            while (listener.IsListening)
            {
                HttpListenerContext context;
                try { context = listener.GetContext(); }
                catch (Exception) { break; } // Stop()
                ThreadPool.QueueUserWorkItem(_ =>
                {
                    // Ошибка вне Handle (например, MissingMethodException при JIT из-за версии Newtonsoft в Revit)
                    // не должна оставлять запрос без ответа — клиент иначе висит до таймаута HTTP.sys.
                    try { Handle(context); }
                    catch (Exception) { Abort(context.Response); }
                });
            }
        }

        private static void Handle(HttpListenerContext context)
        {
            HttpListenerRequest request = context.Request;
            HttpListenerResponse response = context.Response;
            try
            {
                // Защита от DNS rebinding и запросов со страниц браузера (требование спецификации MCP).
                string origin = request.Headers["Origin"];
                if (!string.IsNullOrEmpty(origin) && !IsLoopbackOrigin(origin))
                {
                    Send(response, 403, null);
                    return;
                }
                if (!string.Equals(request.Url.AbsolutePath.TrimEnd('/'), "/mcp", StringComparison.OrdinalIgnoreCase))
                {
                    Send(response, 404, null);
                    return;
                }
                if (request.HttpMethod != "POST")
                {
                    // Поток SSE (GET) и завершение сессии (DELETE) не поддерживаем — спецификация разрешает 405.
                    response.AddHeader("Allow", "POST");
                    Send(response, 405, null);
                    return;
                }
                // application/json обязателен: браузер не может прислать его без CORS-preflight, а preflight мы не одобряем.
                if (request.ContentType == null || !request.ContentType.StartsWith("application/json", StringComparison.OrdinalIgnoreCase))
                {
                    Send(response, 415, null);
                    return;
                }

                bool selfTest = request.Headers["X-TNov-SelfTest"] != null;
                if (!selfTest) McpActivity.LastRequestUtc = DateTime.UtcNow;

                string body;
                using (var reader = new StreamReader(request.InputStream, Encoding.UTF8))
                    body = reader.ReadToEnd();

                JToken message;
                try { message = JToken.Parse(body); }
                catch (JsonException)
                {
                    Send(response, 400, Error(null, -32700, "Parse error"));
                    return;
                }

                JToken reply;
                if (message is JArray batch)
                {
                    var replies = new JArray(batch.OfType<JObject>().Select(HandleMessage).Where(r => r != null));
                    reply = replies.Count > 0 ? replies : null;
                }
                else if (message is JObject single)
                {
                    reply = HandleMessage(single);
                }
                else
                {
                    reply = Error(null, -32600, "Invalid Request");
                }

                // Уведомления и ответы клиента: 202 без тела.
                Send(response, reply == null ? 202 : 200, reply);
            }
            catch (Exception ex)
            {
                try { Send(response, 500, Error(null, -32603, ex.Message)); }
                catch (Exception) { Abort(response); }
            }
        }

        private static JObject HandleMessage(JObject message)
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
                        McpActivity.OnInitialize(message["params"]?["clientInfo"] as JObject);
                        break;
                    case "ping":
                        result = new JObject();
                        break;
                    case "tools/list":
                        result = new JObject { ["tools"] = McpTools.List() };
                        break;
                    case "tools/call":
                        result = McpTools.Call(message["params"] as JObject);
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

        private static void Abort(HttpListenerResponse response)
        {
            try { response.Abort(); }
            catch (Exception) { }
        }

        private static void Send(HttpListenerResponse response, int status, JToken body)
        {
            response.StatusCode = status;
            if (body != null)
            {
                byte[] bytes = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(body, Formatting.None));
                response.ContentType = "application/json; charset=utf-8";
                response.ContentLength64 = bytes.Length;
                response.OutputStream.Write(bytes, 0, bytes.Length);
            }
            response.Close();
        }
    }
}
