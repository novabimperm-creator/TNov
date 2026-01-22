using System.Windows;

namespace TNov
{
    /// <summary>
    /// Логика взаимодействия для SchemespecWPF.xaml
    /// </summary>
    public partial class SchemespecWPF : Window
    {
        public SchemespecWPF(SchemespecViewModel viewModel)
        {
            InitializeComponent();
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
