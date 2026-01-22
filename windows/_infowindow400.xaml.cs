using System.Windows;

namespace TNov
{
    /// <summary>
    /// Логика взаимодействия для _infowindow400.xaml
    /// </summary>
    public partial class infowindow400 : Window
    {
        public infowindow400(string txt)
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

        private void Border_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {

        }
    }
}
