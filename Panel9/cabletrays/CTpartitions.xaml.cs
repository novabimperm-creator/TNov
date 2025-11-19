using System.Windows;

namespace TNov
{
    /// <summary>
    /// Логика взаимодействия для CTpartitionswpf.xaml
    /// </summary>
    public partial class CTpartitionswpf : Window
    {
        public CTpartitionswpf(CTpartitionsViewModel viewModel)
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
