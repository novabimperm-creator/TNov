using System.Windows;

namespace TNov
{
    /// <summary>
    /// Логика взаимодействия для _qwindow280.xaml
    /// </summary>
    public partial class qwindow280 : Window
    {
        public qwindow280(qwindow280ViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
            this.SizeToContent = SizeToContent.Height;
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
