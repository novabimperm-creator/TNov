using System.Windows;

namespace TNov
{
    /// <summary>
    /// Логика взаимодействия для foundwpf.xaml
    /// </summary>
    public partial class foundwpf : Window
    {
        public foundwpf(foundViewModel viewModel)
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
