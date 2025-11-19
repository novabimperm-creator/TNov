using System.Windows;

namespace TNov
{
    /// <summary>
    /// Логика взаимодействия для unpinnerwpf.xaml
    /// </summary>
    public partial class unpinnerwpf : Window
    {
        public unpinnerwpf(unpinnerwpfViewModel viewModel)
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


    }
}
