using System.Windows;

namespace TNov
{
    /// <summary>
    /// Логика взаимодействия для SSNumbererWPF.xaml
    /// </summary>
    public partial class SSNumbererWPF : Window
    {
        public SSNumbererWPF(SSNumbererViewModel viewModel)
        {
            InitializeComponent();
            textBox1.Focus();
            DataContext = viewModel;
            
            this.SizeToContent = SizeToContent.Height;
            
        }
        /*
        private void Button_Click(object sender, RoutedEventArgs e)
        {
            this.Hide();
        }
        */


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
