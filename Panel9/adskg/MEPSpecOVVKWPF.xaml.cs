using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Navigation;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Newtonsoft.Json;
using System.IO;

namespace TNov
{
    /// <summary>
    /// Логика взаимодействия для MEPSpecOVVKWPF.xaml
    /// </summary>
    public partial class MEPSpecOVVKWPF : Window
    {
        public MEPSpecOVVKWPF(MEPSpecOVVKViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
            //скрываем лишние stackpanel
            if(viewModel.visibility1==false) sp01.Visibility = System.Windows.Visibility.Collapsed;
            if (viewModel.visibility2 == false) sp02.Visibility = System.Windows.Visibility.Collapsed;
            if (viewModel.visibility3 == false) sp03.Visibility = System.Windows.Visibility.Collapsed;
            if (viewModel.visibility4 == false) sp04.Visibility = System.Windows.Visibility.Collapsed;
            if (viewModel.visibility5 == false) sp05.Visibility = System.Windows.Visibility.Collapsed;
            if (viewModel.visibility6 == false) sp06.Visibility = System.Windows.Visibility.Collapsed;
            if (viewModel.visibility7 == false) sp07.Visibility = System.Windows.Visibility.Collapsed;
            if (viewModel.visibility8 == false) sp08.Visibility = System.Windows.Visibility.Collapsed;
            if (viewModel.visibility9 == false) sp09.Visibility = System.Windows.Visibility.Collapsed;
            if (viewModel.visibility10 == false) sp010.Visibility = System.Windows.Visibility.Collapsed;
            if (viewModel.visibility11 == false) sp011.Visibility = System.Windows.Visibility.Collapsed;
            if (viewModel.visibility12 == false) sp012.Visibility = System.Windows.Visibility.Collapsed;
            if (viewModel.visibility13 == false) sp013.Visibility = System.Windows.Visibility.Collapsed;
            if (viewModel.visibility14 == false) sp014.Visibility = System.Windows.Visibility.Collapsed;
            //создаем кнопки для Настроек правил заполнения наименования и количества
            string groups1 = viewModel.types;
            int index = groups1.LastIndexOf('|');
            groups1 = groups1.Remove(index);
            string[] groups = groups1.Split('|');

            foreach (string group in groups)
            {
                string[] parts = group.Split('_');
                string category = parts[0];
                string categorytoreplace = parts[0] + "_";
                string groupnametxt = group.Replace(categorytoreplace, "");

                StackPanel sp = new StackPanel(); sp.Orientation = Orientation.Horizontal; sp.HorizontalAlignment = HorizontalAlignment.Left;
                sp.Width = 170;
                
                var btn = new Button
                { Content = new TextBlock() { Text = groupnametxt, TextWrapping = TextWrapping.Wrap }, 
                    Width = 150, MinHeight = 25, Margin = new Thickness(5, 5, 5, 5), VerticalAlignment = VerticalAlignment.Center, 
                    Tag = viewModel.fileName+"="+group};
                sp.Children.Add(btn);
                btn.Click += new RoutedEventHandler(settings_Click);

                switch (category)
                {
                    case "Трубы":
                        sp11.Children.Add(sp);
                        break;
                    case "Материалы изоляции труб":
                        sp12.Children.Add(sp);
                        break;
                    case "Гибкие трубы":
                        sp10.Children.Add(sp);
                        break;
                    case "Воздуховоды":
                        sp5.Children.Add(sp);
                        break;
                    case "Материалы изоляции воздуховодов":
                        sp6.Children.Add(sp);
                        break;
                    case "Соединительные детали воздуховодов":
                        sp7.Children.Add(sp);
                        break;
                }

            }

            this.SizeToContent = SizeToContent.Height;
        }
        private void acceptButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            this.Close(); // закрытие окна
        }

        private void escButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close(); // закрытие окна
        }
        private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {

            Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri));
            e.Handled = true;
        }

        private void settings_Click(object sender, RoutedEventArgs e)
        {
            Button button = (Button)sender;
            string[] tagParts = button.Tag.ToString().Split('=');
            string type = tagParts[1];

            MEPSpecOVVKParamsViewModel viewModel = new MEPSpecOVVKParamsViewModel();
            viewModel.elemType = type;
            
            // Десериализация
            bool forProject = true;
            string VMName = "ADSK Параметры_" + type;
            json js = new json(in VMName, in forProject, out bool canserialize, out string jsonpath);
            if (canserialize)
            {
                viewModel = JsonConvert.DeserializeObject<MEPSpecOVVKParamsViewModel>(File.ReadAllText(jsonpath));
            }
            else
            {
                //базовые значения
                MEPSpecTools.MEPSpecOVVKBaseParams(type, tagParts[0], viewModel);
            }
            var wpfview = new MEPSpecOVVKParamsWPF(viewModel);
            viewModel.CloseRequest += (ss, ea) => wpfview.Close();
            bool? ok = wpfview.ShowDialog();
            //Сериализация
            try
            {
                File.WriteAllText(jsonpath, JsonConvert.SerializeObject(viewModel));
            }
            catch (Exception) { }

        }

        private void Border_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {

        }
    }
}
