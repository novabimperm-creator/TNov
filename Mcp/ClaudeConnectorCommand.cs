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
    /// <summary>Запуск/остановка коннектора и связь с жизненным циклом плагина.</summary>
    internal static class ClaudeConnector
    {
        public static void OnStartup()
        {
            RevitDispatcher.Initialize();
        }

        public static void OnShutdown() => McpServer.Stop();

        public static string ConnectCommand => $"claude mcp add --transport http --scope user revit {McpServer.Url}";

        /// <summary>
        /// Другие MCP-коннекторы, загруженные в Revit (по именам и папкам сборок со словом «mcp»
        /// или с официальной библиотекой ModelContextProtocol). Их действия уровнями TNov не ограничиваются.
        /// </summary>
        public static List<string> OtherMcpAddins()
        {
            var found = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
            Assembly own = typeof(ClaudeConnector).Assembly;
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

        /// <summary>Значок кнопки: рисуется в коде, чтобы не заводить ресурс под один значок.</summary>
        public static BitmapSource Icon(int size)
        {
            var visual = new DrawingVisual();
            using (DrawingContext dc = visual.RenderOpen())
            {
                var background = new SolidColorBrush(Color.FromRgb(0xD9, 0x77, 0x57));
                dc.DrawRoundedRectangle(background, null, new Rect(0, 0, size, size), size * 0.2, size * 0.2);
                var pen = new Pen(Brushes.White, size * 0.11) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
                var center = new Point(size / 2.0, size / 2.0);
                double r = size * 0.32;
                for (int i = 0; i < 4; i++)
                {
                    double a = Math.PI / 4 * i;
                    var d = new Vector(Math.Cos(a) * r, Math.Sin(a) * r);
                    dc.DrawLine(pen, center - d, center + d);
                }
            }
            var bitmap = new RenderTargetBitmap(size, size, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(visual);
            bitmap.Freeze();
            return bitmap;
        }
    }

    [Transaction(TransactionMode.Manual)]
    public class ClaudeConnectorCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, Autodesk.Revit.DB.ElementSet elements)
        {
            if (!McpAccess.Enabled)
            {
                McpServer.Stop();
                new InfoWindow280("Коннектор Claude отключён администратором.").ShowDialog();
                return Result.Succeeded;
            }

            if (!McpServer.IsRunning)
            {
                try { McpServer.Start(McpAccess.Local.Port); }
                catch (Exception ex)
                {
                    TaskDialog.Show("TNov — Claude", "Не удалось запустить коннектор:\n" + ex.Message);
                    return Result.Failed;
                }
            }

            // Немодально: пока окно открыто, Revit должен выполнять запросы Claude.
            if (_window != null)
            {
                _window.Activate();
                return Result.Succeeded;
            }
            _window = new ClaudeConnectorWPF();
            new WindowInteropHelper(_window) { Owner = commandData.Application.MainWindowHandle };
            _window.Closed += (s, e) => _window = null;
            _window.Show();
            return Result.Succeeded;
        }

        private static ClaudeConnectorWPF _window;
    }
}
