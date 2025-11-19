using System.Windows;
using TNov.Panel8;

namespace TNov
{
    /// <summary>
    /// Логика взаимодействия для duct3dwpf.xaml
    /// </summary>
    public partial class duct3dwpf : Window
    {
        public duct3dwpf(duct3dViewModel viewModel)
        {
            InitializeComponent();
            this.SizeToContent = SizeToContent.Height;
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
