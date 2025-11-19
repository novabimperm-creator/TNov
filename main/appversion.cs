using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Newtonsoft.Json;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows.Media;
using TNov.main;
using adWin = Autodesk.Windows;
using File = System.IO.File;

namespace TNov
{
    public class aboutViewModel : INotifyPropertyChanged
    {
        public string headtxt { get; set; }
        public string url { get; set; }
        private bool _extendedLogs = false;
        public bool extendedLogs
        {
            get => _extendedLogs; set { _extendedLogs = value; OnPropertyChanged(); }
        }
        [JsonIgnore] public string userName {  get; set; }
        [JsonIgnore] public string userDep { get; set; }
        [JsonIgnore] public string userDepRole { get; set; }

        [JsonIgnore] public ObservableCollection<string> synclist { get; set; }
        private string _sync1;
        public string sync1 { get { return _sync1; } set { _sync1 = value; OnPropertyChanged(); } }
        private int _paramnum = 0;
        public int paramnum { get => _paramnum; set { _paramnum = value; OnPropertyChanged(); } }
        public aboutViewModel()
        {
            Param();
        }
        private void Param()
        {
            synclist = new ObservableCollection<string>
            {
                "Подсветка 20/30 минут",
                "Подсветка 30/60 минут",
                "Подсветка 40/60 минут",
                "Подсветка 60/90 минут",
                "Без подсветки панелей (не рекомендуется)",
                "Подсветка 1/2 минуты :-)"
            }; 
            sync1 = synclist[paramnum];
        }

        public event EventHandler CloseRequest;
        private void RaiseCloseRequest()
        {
            CloseRequest?.Invoke(this, EventArgs.Empty);
        }
        public event PropertyChangedEventHandler PropertyChanged;

        void OnPropertyChanged([CallerMemberName] string PropertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(PropertyName));
        }
    }
    
    [Transaction(TransactionMode.Manual)]
    public class appversion : IExternalCommand
    {
        
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            string TNovClassName = "О программе"; DateTime dateTime = DateTime.Now;
            //подключение приложения и документа
            if (RevitAPI.UiApplication == null) { RevitAPI.Initialize(commandData); }
            UIDocument uidoc = RevitAPI.UiDocument; Document doc = RevitAPI.Document;
            UIApplication uiApp = RevitAPI.UiApplication; Autodesk.Revit.ApplicationServices.Application rvtApp = uiApp.Application;
            //проверка подключения, запись в журнал
            bool check = false; servercheck sc = new servercheck(in TNovClassName, out check); if (check == false) { return Result.Failed; }

            //версии
            string TNovVersion = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString();
            string verfilePath = nova.novaserver + "_TNov/actual/version.txt";
            string actualVersion = FileVersionInfo.GetVersionInfo(Path.Combine(nova.novafolder, "TNov.dll")).FileVersion;

            //имя и роль пользователя
            string userName = rvtApp.Username;
            string userDepartment = "-";
            string userDepRole = "-";
#if config1 || config2
            string[] rolesFile = File.ReadAllLines("//fs-nova/Distr/0.For Admin/_TNov/roles.txt");
            foreach(string role in rolesFile) 
            { 
                if (role.Contains(userName))
                {
                    string[] line = role.Split(','); userDepartment = line[1]; userDepRole = line[2]; break;
                }
                
            }
            switch (userDepartment)
            {
                case "AR": userDepartment = "АР"; break;
                case "ST": userDepartment = "КР"; break;
                case "VK": userDepartment = "ВК"; break;
                case "OV": userDepartment = "ОВ"; break;
                case "EL": userDepartment = "ЭО"; break;
                case "SS": userDepartment = "СС"; break;
            }
            switch (userDepRole)
            {
                case "head": userDepRole = "руководитель"; break;
                case "user": userDepRole = "исполнитель"; break;
            }
#endif
            var viewModel = new aboutViewModel();
            // Десериализация

            string jsonpath = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "TNovClient/TNovSettings.json");
            try
            {
                viewModel = JsonConvert.DeserializeObject<aboutViewModel>(File.ReadAllText(jsonpath));
            }
            catch (Exception) { }
            
            
            viewModel.headtxt = "Плагин TNov разработан в проектной мастерской Новация.\n" +
                "Версия программы - " + TNovVersion + ". Актуальная версия - " + actualVersion;
            viewModel.url = "https://portal.talan.group/knowledge/proektirovanie/plaginyiskriptynovatsiya/";
            viewModel.userName = userName; viewModel.userDep = userDepartment; viewModel.userDepRole = userDepRole;
            var wpfview = new about(viewModel);
            viewModel.CloseRequest += (s, e) => wpfview.Close();
            bool? ok = wpfview.ShowDialog();
            if (ok != null && ok == true) { } else { return Result.Cancelled; }
            //Сериализация
            try
            {
                File.WriteAllText(jsonpath, JsonConvert.SerializeObject(viewModel));
            }
            catch (Exception) { }

            if(viewModel.sync1== "Без подсветки панелей (не рекомендуется)")
            {
                adWin.RibbonControl ribbon = adWin.ComponentManager.Ribbon;
                foreach (adWin.RibbonTab tab in ribbon.Tabs)
                {
                    foreach (adWin.RibbonPanel panel in tab.Panels)
                    {
                        panel.CustomPanelBackground = (SolidColorBrush)new BrushConverter().ConvertFromString("#F6F6F6");
                        panel.CustomPanelTitleBarBackground = (SolidColorBrush)new BrushConverter().ConvertFromString("#F6F6F6");

                    }
                }
            }

            return Result.Succeeded;
        }
    }
}
