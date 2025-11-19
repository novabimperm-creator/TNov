using System.Windows;

namespace TNov
{
    /// <summary>
    /// Логика взаимодействия для _infowindow280.xaml
    /// </summary>
    public partial class infowindow280 : Window
    {
        public infowindow280(string txt)
        {
            InitializeComponent();
            text1.Text += txt;
            this.SizeToContent = SizeToContent.Height;
        }

        private void escButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            this.Close(); // закрытие окна
        }

    }
}
