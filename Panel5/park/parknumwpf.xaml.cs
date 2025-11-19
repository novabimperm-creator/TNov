using System.Windows;

namespace TNov
{
    /// <summary>
    /// Логика взаимодействия для parknumwpf.xaml
    /// </summary>
    public partial class parknumwpf : Window
    {
        public parknumwpf(parknumViewModel viewModel)
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

    }
}
