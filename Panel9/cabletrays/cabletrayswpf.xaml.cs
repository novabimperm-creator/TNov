using System.Windows;

namespace TNov
{
    /// <summary>
    /// Логика взаимодействия для CableTraysWPF.xaml
    /// </summary>
    public partial class CableTraysWPF : Window
    {
        public CableTraysWPF(CableTraysViewModel viewModel)
        {
            InitializeComponent();
            textBox1.Focus();
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

        private void Border_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {

        }
    }
}
