using System.Windows;
using System.Windows.Input;

namespace TNov.Mcp
{
    /// <summary>Окно кнопки «Нейросетошная»: состояние коннекторов Claude, Codex и Qwen, общий уровень доступа, подключение.</summary>
    public partial class McpConnectorWPF : Window
    {
        private readonly McpStatusViewModel _status = new McpStatusViewModel();
        private readonly Skills.SkillsViewModel _skills = new Skills.SkillsViewModel();

        public McpConnectorWPF()
        {
            InitializeComponent();
            // На ноутбуках окно целиком не помещается — ограничиваем рабочей областью, ниже заголовка прокрутка.
            MaxHeight = SystemParameters.WorkArea.Height - 20;
            DataContext = _status;
            SkillsPanel.DataContext = _skills;
            Loaded += (s, e) => _status.StartMonitoring();
            Closed += (s, e) => _status.StopMonitoring();
        }

        private void Tab_Checked(object sender, RoutedEventArgs e)
        {
            // Checked приходит и во время InitializeComponent — до создания панелей.
            if (ConnectionPanel == null || SkillsPanel == null) return;
            bool skills = SkillsTab.IsChecked == true;
            ConnectionPanel.Visibility = skills ? Visibility.Collapsed : Visibility.Visible;
            SkillsPanel.Visibility = skills ? Visibility.Visible : Visibility.Collapsed;
            if (skills) _skills.EnsureLoaded();
        }

        private void SkillsRefresh_Click(object sender, RoutedEventArgs e) => _skills.Load(true);

        private void SkillCopy_Click(object sender, RoutedEventArgs e) => _skills.CopyCode();

        private void SkillPublish_Click(object sender, RoutedEventArgs e) => _skills.Publish();

        private void SkillHide_Click(object sender, RoutedEventArgs e) => _skills.Hide();

        private void SkillRestore_Click(object sender, RoutedEventArgs e) => _skills.Restore();

        private void SkillSaveInstruction_Click(object sender, RoutedEventArgs e) => _skills.SaveInstruction();

        private void SkillFileOpen_Click(object sender, RoutedEventArgs e) => _skills.OpenFile(TagOf(sender));

        private void SkillFileRemove_Click(object sender, RoutedEventArgs e)
        {
            string name = TagOf(sender);
            if (MessageBox.Show(this, $"Удалить файл «{name}» из скилла?", "TNov", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                _skills.RemoveFile(name);
        }

        private void SkillFileReplace_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog { Title = "Заменить файл «" + TagOf(sender) + "»" };
            if (dialog.ShowDialog(this) == true) _skills.ReplaceFile(TagOf(sender), dialog.FileName);
        }

        private void SkillFileAdd_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog { Title = "Прикрепить файлы к скиллу", Multiselect = true };
            if (dialog.ShowDialog(this) == true) _skills.AddFiles(dialog.FileNames);
        }

        private static string TagOf(object sender) => (sender as FrameworkElement)?.Tag as string;

        private void CopyClaudeButton_Click(object sender, RoutedEventArgs e) => _status.CopyConnectCommand(_status.Claude);

        private void CopyCodexButton_Click(object sender, RoutedEventArgs e) => _status.CopyConnectCommand(_status.Codex);

        private void DesktopButton_Click(object sender, RoutedEventArgs e) => _status.ConnectClaudeDesktop();

        private void CodexButton_Click(object sender, RoutedEventArgs e) => _status.ConnectCodex();

        private void CopyQwenButton_Click(object sender, RoutedEventArgs e) => _status.CopyConnectCommand(_status.Qwen);

        private void QwenButton_Click(object sender, RoutedEventArgs e) => _status.ConnectQwen();

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
