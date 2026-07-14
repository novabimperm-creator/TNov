using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
using System.Windows.Navigation;
using TNovCommon;

namespace TNov
{
    /// <summary>
    /// Логика взаимодействия для AppVersionWPF.xaml
    /// </summary>
    public partial class AppVersionWPF : Window
    {
        private bool _releasesLoaded;

        public AppVersionWPF(AppVersionViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
        }

        private AppVersionViewModel ViewModel => (AppVersionViewModel)DataContext;

        private void acceptButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }

        private void escButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void JournalButton_Click(object sender, RoutedEventArgs e)
        {
            if (!_releasesLoaded)
            {
                ViewModel.LoadReleases();
                _releasesLoaded = true;
            }

            SettingsPanel.Visibility = Visibility.Collapsed;
            JournalPanel.Visibility = Visibility.Visible;
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            JournalPanel.Visibility = Visibility.Collapsed;
            SettingsPanel.Visibility = Visibility.Visible;
        }

        private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
            e.Handled = true;
        }

        private void HelpButton_Click(object sender, RoutedEventArgs e)
        {
            string commandText = HelpLinks.GetHelpLink("-");
            var proc = new System.Diagnostics.Process();
            proc.StartInfo.FileName = commandText;
            proc.StartInfo.UseShellExecute = true;
            proc.Start();
        }

        private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
                DragMove();
        }
    }
}
