using System.Windows;

namespace TNov
{
    /// <summary>
    /// Логика взаимодействия для roomsBackupStart.xaml
    /// </summary>
    public partial class roomsBackupStart : Window
    {
        public int scenario = 0;
        public roomsBackupStart()
        {
            InitializeComponent();
            this.SizeToContent = SizeToContent.Height;
        }
        private void saveButton_Click(object sender, RoutedEventArgs e)
        {
            scenario = 1;
            DialogResult = true;
            this.Close(); // закрытие окна
        }
        private void loadButton_Click(object sender, RoutedEventArgs e)
        {
            scenario = 2;
            DialogResult = true;
            this.Close(); // закрытие окна
        }
        private void escButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            this.Close(); // закрытие окна
        }

        private void Border_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {

        }
    }
}
