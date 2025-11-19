using System.Windows;

namespace TNov
{
    /// <summary>
    /// Логика взаимодействия для cablewayswpf.xaml
    /// </summary>
    public partial class cablewayswpf : Window
    {
        public cablewayswpf(cablewaysViewModel viewModel)
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


    }
}
