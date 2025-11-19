using System.Windows;

namespace TNov
{
    /// <summary>
    /// Логика взаимодействия для parkmarkwpf.xaml
    /// </summary>
    public partial class parkmarkwpf : Window
    {
        public parkmarkwpf(parkmarkViewModel viewModel)
        {
            InitializeComponent();
            textBox1.Focus();
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
