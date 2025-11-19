using System.Windows;

namespace TNov
{
    /// <summary>
    /// Логика взаимодействия для excelwpf.xaml
    /// </summary>
    public partial class excelwpf : Window
    {
        public excelwpf(excelViewModel viewModel)
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
