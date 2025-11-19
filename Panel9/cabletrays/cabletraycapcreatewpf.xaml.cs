using System.Windows;

namespace TNov
{
    /// <summary>
    /// Логика взаимодействия для cabletraycapcreatewpf.xaml
    /// </summary>
    public partial class cabletraycapcreatewpf : Window
    {
        public cabletraycapcreatewpf(cabletraycapcreateViewModel viewModel)
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


    }
}
