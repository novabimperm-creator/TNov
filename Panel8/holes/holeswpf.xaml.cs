using System.Windows;

namespace TNov
{
    /// <summary>
    /// Логика взаимодействия для HolesWPF.xaml
    /// </summary>
    public partial class HolesWPF : Window
    {
        public HolesWPF(HolesViewModel viewModel)
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
