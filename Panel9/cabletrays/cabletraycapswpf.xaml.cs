using System.Windows;

namespace TNov
{
    /// <summary>
    /// Логика взаимодействия для cabletraycapswpf.xaml
    /// </summary>
    public partial class cabletraycapswpf : Window
    {
        public cabletraycapswpf(cabletraycapsViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
            
            this.SizeToContent = SizeToContent.Height;
            
        }

        private void acceptButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            this.Close(); // закрытие окна
        }

        
    }
}
