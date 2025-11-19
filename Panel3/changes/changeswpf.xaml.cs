using System.Windows;

namespace TNov
{
    /// <summary>
    /// Логика взаимодействия для changeswpf.xaml
    /// </summary>
    public partial class changeswpf : Window
    {
        public changeswpf(changesViewModel viewModel)
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
