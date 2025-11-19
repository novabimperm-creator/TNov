using System.Windows;
using System.Windows.Controls;

namespace TNov
{
    /// <summary>
    /// Логика взаимодействия для foundautonumwpf.xaml
    /// </summary>
    public partial class foundautonumwpf : Window
    {
        public foundautonumwpf(foundautonumViewModel viewModel)
        {
            InitializeComponent();
            textBox1.Focus();
            DataContext = viewModel;
            
            this.SizeToContent = SizeToContent.Height;
            
        }
        private void Slider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            ((Slider)sender).SelectionEnd = e.NewValue;
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
        /*
         <StackPanel Orientation="Horizontal" HorizontalAlignment="Left">
            <TextBlock Name="text2" Margin="5" Text="Настройка:" TextWrapping="Wrap" />
            <Slider Minimum="500" Maximum="2000" Value="{Binding tolerance}" TickPlacement="None" TickFrequency="5" IsSnapToTickEnabled="True" Name="slValue" Width="124" Margin="5"
                    ValueChanged="Slider_ValueChanged"/>
            <TextBox Text="{Binding ElementName=slValue, Path=Value, UpdateSourceTrigger=PropertyChanged}" TextAlignment="Center" Width="50" Margin="5" />
        </StackPanel>
         */

    }
}
