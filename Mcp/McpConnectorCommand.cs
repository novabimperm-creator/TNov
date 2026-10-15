using Autodesk.Revit.Attributes;
using Autodesk.Revit.UI;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using TNovCommon;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace TNov.Mcp
{
    /// <summary>Запуск/остановка коннекторов Claude и Codex и связь с жизненным циклом плагина.</summary>
    internal static class McpConnector
    {
        public static void OnStartup()
        {
            RevitDispatcher.Initialize();
        }

        public static void OnShutdown() => McpServer.Stop();

        /// <summary>Ручное подключение (один раз на компьютере): для Claude — команда терминала, для Codex — блок config.toml.</summary>
        public static string ConnectCommand(McpClient client) => client == McpClient.Codex
            ? CodexSetup.Block(McpServer.IsRunning ? McpServer.Port : McpAccess.Local.Port).TrimEnd()
            : $"claude mcp add --transport http --scope user revit {McpServer.UrlFor(client)}";

        /// <summary>
        /// Другие MCP-коннекторы, загруженные в Revit (по именам и папкам сборок со словом «mcp»
        /// или с официальной библиотекой ModelContextProtocol). Их действия уровнями TNov не ограничиваются.
        /// </summary>
        public static List<string> OtherMcpAddins()
        {
            var found = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
            Assembly own = typeof(McpConnector).Assembly;
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    if (assembly == own || assembly.IsDynamic) continue;
                    string name = assembly.GetName().Name ?? "";
                    if (name.StartsWith("TNovMcpScript", StringComparison.Ordinal)) continue;
                    string folder = Path.GetFileName(Path.GetDirectoryName(assembly.Location) ?? "") ?? "";
                    bool mcpName = name.IndexOf("mcp", StringComparison.OrdinalIgnoreCase) >= 0
                                   || name.StartsWith("ModelContextProtocol", StringComparison.OrdinalIgnoreCase);
                    bool mcpFolder = folder.IndexOf("mcp", StringComparison.OrdinalIgnoreCase) >= 0;
                    if (mcpName) found.Add(name.StartsWith("ModelContextProtocol", StringComparison.OrdinalIgnoreCase) && folder.Length > 0 ? folder : name);
                    else if (mcpFolder) found.Add(folder);
                }
                catch (Exception) { } // сборка без Location и т.п.
            }
            return found.ToList();
        }

        /// <summary>Значок кнопки «Нейросетошная» — узлы нейросети; рисуется в коде, чтобы не заводить ресурс под один значок.</summary>
        public static BitmapSource Icon(int size)
        {
            var visual = new DrawingVisual();
            using (DrawingContext dc = visual.RenderOpen())
            {
                var background = new SolidColorBrush(Color.FromRgb(0x6E, 0x56, 0xCF));
                dc.DrawRoundedRectangle(background, null, new Rect(0, 0, size, size), size * 0.2, size * 0.2);
                var pen = new Pen(Brushes.White, Math.Max(1, size * 0.07));
                Point[] inputs = { new Point(size * 0.25, size * 0.28), new Point(size * 0.25, size * 0.72) };
                Point[] hidden = { new Point(size * 0.5, size * 0.2), new Point(size * 0.5, size * 0.5), new Point(size * 0.5, size * 0.8) };
                var output = new Point(size * 0.77, size * 0.5);
                foreach (Point a in inputs)
                    foreach (Point b in hidden)
                        dc.DrawLine(pen, a, b);
                foreach (Point b in hidden) dc.DrawLine(pen, b, output);
                double r = size * 0.09;
                foreach (Point p in inputs.Concat(hidden).Concat(new[] { output }))
                    dc.DrawEllipse(Brushes.White, null, p, r, r);
            }
            var bitmap = new RenderTargetBitmap(size, size, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(visual);
            bitmap.Freeze();
            return bitmap;
        }
    }

    /// <summary>Кнопка «Нейросетошная»: общее окно коннекторов Claude и Codex.</summary>
    [Transaction(TransactionMode.Manual)]
    public class McpConnectorCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, Autodesk.Revit.DB.ElementSet elements)
        {
            if (!McpAccess.Enabled)
            {
                McpServer.Stop();
                new InfoWindow280("Коннекторы нейросетей отключены администратором.").ShowDialog();
                return Result.Succeeded;
            }

            // Мост у пользователя мог остаться от прошлой версии TNov.
            ClaudeDesktopSetup.RefreshBridgeIfInstalled();

            if (!McpServer.IsRunning)
            {
                try { McpServer.Start(McpAccess.Local.Port); }
                catch (Exception ex)
                {
                    TaskDialog.Show("TNov — нейросети", "Не удалось запустить коннектор:\n" + ex.Message);
                    return Result.Failed;
                }
            }

            // Немодально: пока окно открыто, Revit должен выполнять запросы Claude и Codex.
            if (_window != null)
            {
                _window.Activate();
                return Result.Succeeded;
            }
            _window = new McpConnectorWPF();
            new WindowInteropHelper(_window) { Owner = commandData.Application.MainWindowHandle };
            _window.Closed += (s, e) => _window = null;
            _window.Show();
            return Result.Succeeded;
        }

        private static McpConnectorWPF _window;
    }
}
