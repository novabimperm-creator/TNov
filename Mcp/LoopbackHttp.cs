using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace TNov.Mcp
{
    /// <summary>
    /// Минимальный HTTP/1.1-сервер только на петлевых адресах (127.0.0.1 и [::1]).
    ///
    /// Раньше использовался HttpListener (http.sys): он держит порт на всех интерфейсах (0.0.0.0, PID 4),
    /// а «только localhost» проверяет по заголовку Host. Запрос из сети с подменённым «Host: localhost»
    /// доходил до коннектора — это и нашла проверка ИБ. Сокет, привязанный к петлевому адресу,
    /// соединения из сети не принимает вовсе; адрес отправителя дополнительно проверяется.
    /// Поддерживается ровно то, что нужно клиентам MCP: POST/GET, Content-Length и chunked-тело,
    /// Expect: 100-continue, keep-alive.
    /// </summary>
    internal sealed class LoopbackHttp
    {
        public sealed class Request
        {
            public string Method;
            public string Path;
            public readonly Dictionary<string, string> Headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            public byte[] Body = new byte[0];

            public string Header(string name) => Headers.TryGetValue(name, out string value) ? value : null;
        }

        public sealed class Response
        {
            public int Status;
            public string ContentType;
            public byte[] Body;
            public readonly Dictionary<string, string> Headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        private const int MaxHeaderBytes = 64 * 1024;
        private const int MaxBodyBytes = 64 * 1024 * 1024;
        private static readonly TimeSpan IdleTimeout = TimeSpan.FromMinutes(5);

        private readonly List<TcpListener> _listeners = new List<TcpListener>();
        private readonly Func<Request, Response> _handler;
        private volatile bool _running;

        public int Port { get; }

        private LoopbackHttp(int port, Func<Request, Response> handler)
        {
            Port = port;
            _handler = handler;
        }

        /// <summary>Открывает порт на 127.0.0.1 (обязательно) и [::1] (если IPv6 включён). SocketException — порт занят.</summary>
        public static LoopbackHttp Start(int port, Func<Request, Response> handler)
        {
            var server = new LoopbackHttp(port, handler);
            try
            {
                server.Listen(IPAddress.Loopback);
                // Node (Claude Code) может разрешить localhost в ::1 — слушаем и его.
                if (Socket.OSSupportsIPv6)
                {
                    try { server.Listen(IPAddress.IPv6Loopback); }
                    catch (SocketException) { } // IPv6 отключён на машине
                }
            }
            catch
            {
                server.Stop();
                throw;
            }
            server._running = true;
            foreach (TcpListener listener in server._listeners)
            {
                TcpListener l = listener;
                new Thread(() => server.AcceptLoop(l)) { IsBackground = true, Name = "TNov MCP " + l.LocalEndpoint }.Start();
            }
            return server;
        }

        private void Listen(IPAddress address)
        {
            var listener = new TcpListener(address, Port);
            // Никто другой не сможет занять тот же адрес и порт и перехватить запросы.
            listener.ExclusiveAddressUse = true;
            listener.Start();
            _listeners.Add(listener);
        }

        public void Stop()
        {
            _running = false;
            foreach (TcpListener listener in _listeners)
            {
                try { listener.Stop(); } catch (Exception) { }
            }
            _listeners.Clear();
        }

        private void AcceptLoop(TcpListener listener)
        {
            while (_running)
            {
                TcpClient client;
                try { client = listener.AcceptTcpClient(); }
                catch (Exception) { break; } // Stop()
                ThreadPool.QueueUserWorkItem(_ => Serve(client));
            }
        }

        private void Serve(TcpClient client)
        {
            using (client)
            {
                try
                {
                    // Привязка к петлевому адресу уже не пускает сеть; проверка — на случай будущих изменений.
                    if (!(client.Client.RemoteEndPoint is IPEndPoint remote) || !IPAddress.IsLoopback(remote.Address)) return;

                    client.NoDelay = true;
                    NetworkStream stream = client.GetStream();
                    stream.ReadTimeout = (int)IdleTimeout.TotalMilliseconds;
                    var reader = new Reader(stream);

                    while (_running)
                    {
                        Request request = ReadRequest(reader, stream, out bool keepAlive, out string version);
                        if (request == null) return; // соединение закрыто клиентом

                        Response response;
                        try { response = _handler(request); }
                        catch (Exception) { response = new Response { Status = 500 }; }

                        Write(stream, response, keepAlive, version);
                        if (!keepAlive) return;
                    }
                }
                catch (IOException) { }      // таймаут простоя или разрыв
                catch (SocketException) { }
                catch (ObjectDisposedException) { }
                catch (FormatException)
                {
                    // Битый запрос — отвечать некому, просто закрываем соединение.
                }
            }
        }

        private static Request ReadRequest(Reader reader, Stream stream, out bool keepAlive, out string version)
        {
            keepAlive = false;
            version = "HTTP/1.1";

            string requestLine = reader.ReadLine();
            while (requestLine != null && requestLine.Length == 0) requestLine = reader.ReadLine(); // лишние CRLF между запросами
            if (requestLine == null) return null;

            string[] parts = requestLine.Split(' ');
            if (parts.Length != 3) throw new FormatException("request line");
            var request = new Request { Method = parts[0].ToUpperInvariant() };
            string target = parts[1];
            int query = target.IndexOf('?');
            request.Path = Uri.UnescapeDataString(query >= 0 ? target.Substring(0, query) : target);
            version = parts[2].StartsWith("HTTP/1.0", StringComparison.OrdinalIgnoreCase) ? "HTTP/1.0" : "HTTP/1.1";

            int headerBytes = requestLine.Length;
            string line;
            while ((line = reader.ReadLine()) != null && line.Length > 0)
            {
                headerBytes += line.Length;
                if (headerBytes > MaxHeaderBytes) throw new FormatException("headers too large");
                int colon = line.IndexOf(':');
                if (colon <= 0) continue;
                request.Headers[line.Substring(0, colon).Trim()] = line.Substring(colon + 1).Trim();
            }
            if (line == null) return null;

            string connection = request.Header("Connection") ?? "";
            keepAlive = version == "HTTP/1.1"
                ? connection.IndexOf("close", StringComparison.OrdinalIgnoreCase) < 0
                : connection.IndexOf("keep-alive", StringComparison.OrdinalIgnoreCase) >= 0;

            if (string.Equals(request.Header("Expect"), "100-continue", StringComparison.OrdinalIgnoreCase))
            {
                byte[] cont = Encoding.ASCII.GetBytes(version + " 100 Continue\r\n\r\n");
                stream.Write(cont, 0, cont.Length);
            }

            if ((request.Header("Transfer-Encoding") ?? "").IndexOf("chunked", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                request.Body = reader.ReadChunked(MaxBodyBytes);
            }
            else if (long.TryParse(request.Header("Content-Length"), out long length) && length > 0)
            {
                if (length > MaxBodyBytes) throw new FormatException("body too large");
                request.Body = reader.ReadExact((int)length);
            }
            return request;
        }

        private static void Write(Stream stream, Response response, bool keepAlive, string version)
        {
            byte[] body = response.Body ?? new byte[0];
            var head = new StringBuilder();
            head.Append(version).Append(' ').Append(response.Status).Append(' ').Append(Reason(response.Status)).Append("\r\n");
            if (response.ContentType != null) head.Append("Content-Type: ").Append(response.ContentType).Append("\r\n");
            head.Append("Content-Length: ").Append(body.Length).Append("\r\n");
            foreach (KeyValuePair<string, string> header in response.Headers)
                head.Append(header.Key).Append(": ").Append(header.Value).Append("\r\n");
            head.Append("Connection: ").Append(keepAlive ? "keep-alive" : "close").Append("\r\n\r\n");

            byte[] headBytes = Encoding.ASCII.GetBytes(head.ToString());
            stream.Write(headBytes, 0, headBytes.Length);
            if (body.Length > 0) stream.Write(body, 0, body.Length);
            stream.Flush();
        }

        private static string Reason(int status)
        {
            switch (status)
            {
                case 200: return "OK";
                case 202: return "Accepted";
                case 400: return "Bad Request";
                case 403: return "Forbidden";
                case 404: return "Not Found";
                case 405: return "Method Not Allowed";
                case 415: return "Unsupported Media Type";
                default: return status >= 500 ? "Internal Server Error" : "Status";
            }
        }

        /// <summary>Буферизованное чтение строк и байтов из сокета.</summary>
        private sealed class Reader
        {
            private readonly Stream _stream;
            private readonly byte[] _buffer = new byte[16 * 1024];
            private int _start;
            private int _end;

            public Reader(Stream stream) { _stream = stream; }

            private bool Fill()
            {
                if (_start < _end) return true;
                _start = 0;
                _end = _stream.Read(_buffer, 0, _buffer.Length);
                return _end > 0;
            }

            /// <summary>Строка до CRLF (без него); null — соединение закрыто.</summary>
            public string ReadLine()
            {
                var bytes = new List<byte>();
                while (true)
                {
                    if (!Fill()) return bytes.Count == 0 ? null : Encoding.ASCII.GetString(bytes.ToArray());
                    byte b = _buffer[_start++];
                    if (b == (byte)'\n')
                    {
                        if (bytes.Count > 0 && bytes[bytes.Count - 1] == (byte)'\r') bytes.RemoveAt(bytes.Count - 1);
                        return Encoding.UTF8.GetString(bytes.ToArray());
                    }
                    bytes.Add(b);
                    if (bytes.Count > MaxHeaderBytes) throw new FormatException("line too long");
                }
            }

            public byte[] ReadExact(int count)
            {
                var result = new byte[count];
                int read = 0;
                while (read < count)
                {
                    if (!Fill()) throw new IOException("connection closed");
                    int n = Math.Min(count - read, _end - _start);
                    Buffer.BlockCopy(_buffer, _start, result, read, n);
                    _start += n;
                    read += n;
                }
                return result;
            }

            public byte[] ReadChunked(int limit)
            {
                var body = new MemoryStream();
                while (true)
                {
                    string sizeLine = ReadLine() ?? throw new IOException("connection closed");
                    int semicolon = sizeLine.IndexOf(';');
                    if (semicolon >= 0) sizeLine = sizeLine.Substring(0, semicolon);
                    int size = int.Parse(sizeLine.Trim(), System.Globalization.NumberStyles.HexNumber);
                    if (size == 0)
                    {
                        while (!string.IsNullOrEmpty(ReadLine())) { } // trailer-заголовки
                        return body.ToArray();
                    }
                    if (body.Length + size > limit) throw new FormatException("body too large");
                    byte[] chunk = ReadExact(size);
                    body.Write(chunk, 0, chunk.Length);
                    ReadLine(); // CRLF после блока
                }
            }
        }
    }
}
