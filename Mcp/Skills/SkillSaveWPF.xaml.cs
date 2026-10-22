using Microsoft.Win32;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Input;

namespace TNov.Mcp.Skills
{
    /// <summary>
    /// Подтверждение сохранения скилла: нейросеть не может опубликовать скилл без ведома пользователя.
    /// Здесь же человек правит инструкцию и прикладывает файлы.
    /// </summary>
    public partial class SkillSaveWPF : Window
    {
        private readonly SkillSaveModel _model;

        public SkillSaveWPF(SkillSaveModel model)
        {
            InitializeComponent();
            _model = model;
            DataContext = model;
        }

        private void AddAttachment_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog { Title = "Файлы к скиллу", Multiselect = true };
            if (dialog.ShowDialog(this) != true) return;
            foreach (string path in dialog.FileNames)
            {
                if (new FileInfo(path).Length > SkillFiles.MaxSize)
                {
                    MessageBox.Show(this, $"Файл «{Path.GetFileName(path)}» больше {SkillFiles.MaxSize / 1024 / 1024} МБ — не приложен.", "TNov");
                    continue;
                }
                if (!_model.Attachments.Any(a => string.Equals(Path.GetFileName(a), Path.GetFileName(path), System.StringComparison.OrdinalIgnoreCase)))
                    _model.Attachments.Add(path);
            }
        }

        private void RemoveAttachment_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.Tag is string path) _model.Attachments.Remove(path);
        }

        private void ConfirmButton_Click(object sender, RoutedEventArgs e) => DialogResult = true;

        private void CancelButton_Click(object sender, RoutedEventArgs e) => DialogResult = false;

        private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left) DragMove();
        }
    }
}
