using System.Windows;

namespace TNov
{
    /// <summary>
    /// Логика взаимодействия для beamswpf.xaml
    /// </summary>
    public partial class beamswpf : Window
    {
        public beamswpf(beamsViewModel viewModel)
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
