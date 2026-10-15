using System.Windows;
using System.Windows.Input;

namespace TNov.Mcp
{
    /// <summary>Окно кнопки «Нейросетошная»: состояние коннекторов Claude и Codex, общий уровень доступа, подключение.</summary>
    public partial class McpConnectorWPF : Window
    {
        private readonly McpStatusViewModel _status = new McpStatusViewModel();

        public McpConnectorWPF()
        {
            InitializeComponent();
            // На ноутбуках окно целиком не помещается — ограничиваем рабочей областью, ниже заголовка прокрутка.
            MaxHeight = SystemParameters.WorkArea.Height - 20;
            DataContext = _status;
            Loaded += (s, e) => _status.StartMonitoring();
            Closed += (s, e) => _status.StopMonitoring();
        }

        private void CopyClaudeButton_Click(object sender, RoutedEventArgs e) => _status.CopyConnectCommand(_status.Claude);

        private void CopyCodexButton_Click(object sender, RoutedEventArgs e) => _status.CopyConnectCommand(_status.Codex);

        private void DesktopButton_Click(object sender, RoutedEventArgs e) => _status.ConnectClaudeDesktop();

        private void CodexButton_Click(object sender, RoutedEventArgs e) => _status.ConnectCodex();

        private void CheckButton_Click(object sender, RoutedEventArgs e) => _status.Check();

        private void ToggleButton_Click(object sender, RoutedEventArgs e) => _status.Toggle();

        private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();

        private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
                DragMove();
        }
    }
}
