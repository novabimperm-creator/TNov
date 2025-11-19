using System.Windows;

namespace TNov
{
    /// <summary>
    /// Логика взаимодействия для idselectionTaskswpf.xaml
    /// </summary>
    public partial class idselectionTaskswpf : Window
    {
        public idselectionTaskswpf(idselectionTasksViewModel viewModel)
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
