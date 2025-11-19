using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Navigation;
using Autodesk.Revit.UI;
using Newtonsoft.Json;
using System.IO;
using TNov.Panel8;

namespace TNov
{
    /// <summary>
    /// Логика взаимодействия для about.xaml
    /// </summary>
    public partial class about : Window
    {
        public about(aboutViewModel viewModel)
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
        private void plwButton_Click(object sender, RoutedEventArgs e)
        {
            //Диалог
            var viewModel = new plwViewModel();
            // Десериализация
            bool forProject = false;
            json js = new json("Закреплятор Уровни Наборы", in forProject, out bool canserialize, out string jsonpath);
            if (canserialize)
            {
                viewModel = JsonConvert.DeserializeObject<plwViewModel>(File.ReadAllText(jsonpath));
            }
            var wpfview = new plwwpf(viewModel);
            viewModel.CloseRequest += (s, ev) => wpfview.Close(); bool? ok = wpfview.ShowDialog();
            //Сериализация
            try
            {
                File.WriteAllText(jsonpath, JsonConvert.SerializeObject(viewModel));
            }
            catch (Exception) { }
        }
        private void changesButton_Click(object sender, RoutedEventArgs e)
        {
            var viewModel = new changesViewModel();
            // Десериализация
            bool forProject = true;
            json js = new json("Изменения", in forProject, out bool canserialize, out string jsonpath);
            if (canserialize)
            {
                viewModel = JsonConvert.DeserializeObject<changesViewModel>(File.ReadAllText(jsonpath));
            }
            var wpfview = new changeswpf(viewModel);
            viewModel.CloseRequest += (s, ev) => wpfview.Close(); bool? ok = wpfview.ShowDialog();
            //Сериализация
            try
            {
                File.WriteAllText(jsonpath, JsonConvert.SerializeObject(viewModel));
            }
            catch (Exception) { }
        }
        private void excelButton_Click(object sender, RoutedEventArgs e)
        {
            var viewModel = new excelViewModel();
            // Десериализация
            string jsonpath = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "TNovClient/excel.json");
            try
            {
                viewModel = JsonConvert.DeserializeObject<excelViewModel>(File.ReadAllText(jsonpath));
            }
            catch (Exception) { }

            var wpfview = new excelwpf(viewModel);
            viewModel.CloseRequest += (s, ev) => wpfview.Close(); bool? ok = wpfview.ShowDialog();
            //Сериализация
            try
            {
                File.WriteAllText(jsonpath, JsonConvert.SerializeObject(viewModel));
            }
            catch (Exception) { }
        }
        private void levelnumberButton_Click(object sender, RoutedEventArgs e)
        {
            var viewModel = new levelnumberwpfViewModel();
            // Десериализация
            bool forProject = true;
            json js = new json("Эт.Номер", in forProject, out bool canserialize, out string jsonpath);
            if (canserialize)
            {
                viewModel = JsonConvert.DeserializeObject<levelnumberwpfViewModel>(File.ReadAllText(jsonpath));
            }
            var wpfview = new levelnumberwpf(viewModel);
            viewModel.CloseRequest += (s, ev) => wpfview.Close(); bool? ok = wpfview.ShowDialog();
            //Сериализация
            try
            {
                File.WriteAllText(jsonpath, JsonConvert.SerializeObject(viewModel));
            }
            catch (Exception) { }
        }
        private void officesButton_Click(object sender, RoutedEventArgs e)
        {
            var viewModel = new officesViewModel();
            // Десериализация
            bool forProject = true;
            json js = new json("Офисография", in forProject, out bool canserialize, out string jsonpath);
            if (canserialize)
            {
                viewModel = JsonConvert.DeserializeObject<officesViewModel>(File.ReadAllText(jsonpath));
            }
            var wpfview = new officeswpf(viewModel);
            viewModel.CloseRequest += (s, ev) => wpfview.Close(); bool? ok = wpfview.ShowDialog();
            //Сериализация
            try
            {
                File.WriteAllText(jsonpath, JsonConvert.SerializeObject(viewModel));
            }
            catch (Exception) { }
        }
        private void steelButton_Click(object sender, RoutedEventArgs e)
        {
            //Диалог
            var viewModel = new steelscheduleViewModel();
            // Десериализация
            bool forProject = false;
            json js = new json("ВРС подчистить", in forProject, out bool canserialize, out string jsonpath);
            if (canserialize)
            {
                viewModel = JsonConvert.DeserializeObject<steelscheduleViewModel>(File.ReadAllText(jsonpath));
            }
            var wpfview = new steelschedulewpf(viewModel);
            viewModel.CloseRequest += (s, ev) => wpfview.Close(); bool? ok = wpfview.ShowDialog();
            //Сериализация
            try
            {
                File.WriteAllText(jsonpath, JsonConvert.SerializeObject(viewModel));
            }
            catch (Exception) { }
        }
        private void schemespecButton_Click(object sender, RoutedEventArgs e)
        {
            var viewModel = new schemespecViewModel();
            // Десериализация
            bool forProject = true;
            json js = new json("Группировка", in forProject, out bool canserialize, out string jsonpath);
            if (canserialize)
            {
                viewModel = JsonConvert.DeserializeObject<schemespecViewModel>(File.ReadAllText(jsonpath));
            }
            var wpfview = new schemespecwpf(viewModel);
            viewModel.CloseRequest += (s, ev) => wpfview.Close(); bool? ok = wpfview.ShowDialog();
            //Сериализация
            try
            {
                File.WriteAllText(jsonpath, JsonConvert.SerializeObject(viewModel));
            }
            catch (Exception) { }
        }
        private void holesButton_Click(object sender, RoutedEventArgs e)
        {
            var viewModel = new holesViewModel();
            // Десериализация
            bool forProject = true;
            json js = new json("Отверстия", in forProject, out bool canserialize, out string jsonpath);
            if (canserialize)
            {
                viewModel = JsonConvert.DeserializeObject<holesViewModel>(File.ReadAllText(jsonpath));
            }
            var wpfview = new holeswpf(viewModel);
            viewModel.CloseRequest += (s, ev) => wpfview.Close(); bool? ok = wpfview.ShowDialog();
            //Сериализация
            try
            {
                File.WriteAllText(jsonpath, JsonConvert.SerializeObject(viewModel));
            }
            catch (Exception) { }
        }
        private void adskgButton_Click(object sender, RoutedEventArgs e)
        {
            var viewModel = new adskgViewModel();
            // Десериализация
            bool forProject = true;
            json js = new json("ADSK Группы", in forProject, out bool canserialize, out string jsonpath);
            if (canserialize)
            {
                viewModel = JsonConvert.DeserializeObject<adskgViewModel>(File.ReadAllText(jsonpath));
            }
            var wpfview = new adskgwpf(viewModel);
            viewModel.CloseRequest += (s, ev) => wpfview.Close(); bool? ok = wpfview.ShowDialog();
            //Сериализация
            try
            {
                File.WriteAllText(jsonpath, JsonConvert.SerializeObject(viewModel));
            }
            catch (Exception) { }
        }
        private void duct3dButton_Click(object sender, RoutedEventArgs e)
        {
            var viewModel = new duct3dViewModel();
            // Десериализация
            bool forProject = true;
            json js = new json("Схемы вентиляции", in forProject, out bool canserialize, out string jsonpath);
            if (canserialize)
            {
                viewModel = JsonConvert.DeserializeObject<duct3dViewModel>(File.ReadAllText(jsonpath));
            }
            var wpfview = new duct3dwpf(viewModel);
            viewModel.CloseRequest += (s, ev) => wpfview.Close(); bool? ok = wpfview.ShowDialog();
            //Сериализация
            try
            {
                File.WriteAllText(jsonpath, JsonConvert.SerializeObject(viewModel));
            }
            catch (Exception) { }
        }
        private void eflButton_Click(object sender, RoutedEventArgs e)
        {
            var viewModel = new eflwpfinputViewModel();
            // Десериализация
            bool forProject = true;
            json js = new json("ЭЛ Отметки размещения", in forProject, out bool canserialize, out string jsonpath);
            if (canserialize)
            {
                viewModel = JsonConvert.DeserializeObject<eflwpfinputViewModel>(File.ReadAllText(jsonpath));
            }
            var wpfview = new eflwpfinput(viewModel);
            viewModel.CloseRequest += (s, ev) => wpfview.Close(); bool? ok = wpfview.ShowDialog();
            //Сериализация
            try
            {
                File.WriteAllText(jsonpath, JsonConvert.SerializeObject(viewModel));
            }
            catch (Exception) { }
        }
        private void ctButton_Click(object sender, RoutedEventArgs e)
        {
            var viewModel = new cabletraysViewModel();
            // Десериализация
            bool forProject = true;
            json js = new json("Лотки", in forProject, out bool canserialize, out string jsonpath);
            if (canserialize)
            {
                viewModel = JsonConvert.DeserializeObject<cabletraysViewModel>(File.ReadAllText(jsonpath));
            }
            var wpfview = new cabletrayswpf(viewModel);
            viewModel.CloseRequest += (s, ev) => wpfview.Close(); bool? ok = wpfview.ShowDialog();
            //Сериализация
            try
            {
                File.WriteAllText(jsonpath, JsonConvert.SerializeObject(viewModel));
            }
            catch (Exception) { }
        }

        private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            
            Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri));
            e.Handled = true;
        }
    }
}
