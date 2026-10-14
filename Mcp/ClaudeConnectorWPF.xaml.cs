using System.Windows;
using System.Windows.Input;

namespace TNov.Mcp
{
    /// <summary>Окно кнопки «Claude»: состояние коннектора, уровень доступа, подключение.</summary>
    public partial class ClaudeConnectorWPF : Window
    {
        private readonly McpStatusViewModel _status = new McpStatusViewModel();

        public ClaudeConnectorWPF()
        {
            InitializeComponent();
            DataContext = _status;
            Loaded += (s, e) => _status.StartMonitoring();
            Closed += (s, e) => _status.StopMonitoring();
        }

        private void CopyButton_Click(object sender, RoutedEventArgs e) => _status.CopyConnectCommand();

        private void DesktopButton_Click(object sender, RoutedEventArgs e) => _status.ConnectClaudeDesktop();

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
