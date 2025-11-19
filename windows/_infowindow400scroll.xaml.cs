using System.Windows;

namespace TNov
{
    /// <summary>
    /// Логика взаимодействия для _infowindow400scroll.xaml
    /// </summary>
    public partial class infowindow400scroll : Window
    {
        public infowindow400scroll(string txt)
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
