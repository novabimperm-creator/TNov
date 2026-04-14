using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Navigation;
using Autodesk.Revit.UI;
using Newtonsoft.Json;
using System.IO;

namespace TNov
{
    /// <summary>
    /// Логика взаимодействия для AppVersionWPF.xaml
    /// </summary>
    public partial class AppVersionWPF : Window
    {
        public AppVersionWPF(AppVersionViewModel viewModel)
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
        
        

        private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            
            Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri));
            e.Handled = true;
        }

        private void Border_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {

        }
    }
}
