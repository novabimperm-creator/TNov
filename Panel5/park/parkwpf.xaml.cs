using System.Windows;

namespace TNov
{
    /// <summary>
    /// Логика взаимодействия для parkwpf.xaml
    /// </summary>
    public partial class parkwpf : Window
    {
        public parkwpf(parkViewModel viewModel)
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

        
    }
}
