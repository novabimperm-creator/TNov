using System.Windows;

namespace TNov
{
    /// <summary>
    /// Логика взаимодействия для FoundWPF.xaml
    /// </summary>
    public partial class FoundWPF : Window
    {
        public FoundWPF(FoundViewModel viewModel)
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

        private void Border_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {

        }
    }
}
