using System.Windows;

namespace TNov
{
    /// <summary>
    /// Логика взаимодействия для floorwpf.xaml
    /// </summary>
    public partial class floorwpf : Window
    {
        public floorwpf(floorViewModel viewModel)
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
