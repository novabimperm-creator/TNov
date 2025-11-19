using System.Windows;

namespace TNov
{
    /// <summary>
    /// Логика взаимодействия для levelnumberwpf.xaml
    /// </summary>
    public partial class levelnumberwpf : Window
    {
        public levelnumberwpf(levelnumberwpfViewModel viewModel)
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


    }
}
