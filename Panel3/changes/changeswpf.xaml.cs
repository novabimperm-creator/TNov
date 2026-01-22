using System.Windows;

namespace TNov
{
    /// <summary>
    /// Логика взаимодействия для ChangesWPF.xaml
    /// </summary>
    public partial class ChangesWPF : Window
    {
        public ChangesWPF(ChangesViewModel viewModel)
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
