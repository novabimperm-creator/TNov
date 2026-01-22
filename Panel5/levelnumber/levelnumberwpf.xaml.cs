using System.Windows;

namespace TNov
{
    /// <summary>
    /// Логика взаимодействия для LevelNumberWPF.xaml
    /// </summary>
    public partial class LevelNumberWPF : Window
    {
        public LevelNumberWPF(LevelNumberViewModel viewModel)
        {
            InitializeComponent();
            this.SizeToContent = SizeToContent.Height;
            DataContext = viewModel;
        }
        private void acceptButton_Click(object sender, RoutedEventArgs e)
        {
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
