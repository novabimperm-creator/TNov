using System;

namespace TNov.Mcp
{
    /// <summary>
    /// Коннектор (клиент) сервера: Claude ходит на /mcp, Codex — на /codex.
    /// Инструменты и уровень доступа общие, отдельно ведутся статус, счётчики и столбец журнала.
    /// </summary>
    internal sealed class McpClient
    {
        public static readonly McpClient Claude = new McpClient("Claude", "/mcp");
        public static readonly McpClient Codex = new McpClient("Codex", "/codex");

        public static readonly McpClient[] All = { Claude, Codex };

        /// <summary>Коннектор вызова, который выполняется сейчас на потоке Revit (вызовы там идут по одному).</summary>
        public static McpClient Current { get; set; } = Claude;

        private McpClient(string name, string path)
        {
            Name = name;
            Path = path;
        }

        public string Name { get; }
        public string Path { get; }
        public McpActivity Activity { get; } = new McpActivity();

        /// <summary>Коннектор по пути запроса (без завершающего «/»); null — путь не наш.</summary>
        public static McpClient FromPath(string path)
        {
            foreach (McpClient client in All)
                if (string.Equals(path, client.Path, StringComparison.OrdinalIgnoreCase)) return client;
            return null;
        }
    }
}
