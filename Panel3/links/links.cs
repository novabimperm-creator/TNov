using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Threading;
using TNov.main;
using static System.Windows.Forms.LinkLabel;

namespace TNov
{
    [Transaction(TransactionMode.Manual)]
    public class links : IExternalCommand
    {
        private TNovProgressBar plwProgressBar;
        private void ThreadStartingPoint()
        {
            this.plwProgressBar = new TNovProgressBar();
            this.plwProgressBar.Show();
            Dispatcher.Run();
        }
        private static IEnumerable<Node> GetAllNodes(ObservableCollection<Node> nodes)
        {
            foreach (var node in nodes)
            {
                yield return node;

                foreach (var child in GetAllNodes(node.Children))
                {
                    yield return child;
                }
            }
        }
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            string TNovClassName = "Связной"; DateTime dateTime = DateTime.Now;
            //подключение приложения и документа
            if (RevitAPI.UiApplication == null) { RevitAPI.Initialize(commandData); }
            UIDocument uidoc = RevitAPI.UiDocument; Document doc = RevitAPI.Document;
            UIApplication uiApp = RevitAPI.UiApplication; Autodesk.Revit.ApplicationServices.Application rvtApp = uiApp.Application;

            //проверка подключения, запись в журнал
            bool check = false; servercheck sc = new servercheck(in TNovClassName, out check); if (check == false) { return Result.Failed; }

            // создание log - файла
            Logger.Initialize(TNovClassName);

            var viewModel0 = new aboutViewModel();

            string jsonpath0 = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "TNovClient/TNovSettings.json");
            viewModel0 = JsonConvert.DeserializeObject<aboutViewModel>(File.ReadAllText(jsonpath0));
            if (viewModel0.extendedLogs)

            {
                var qViewModel = new qwindow280ViewModel();
                qViewModel.headtxt = "Включены расширенные логи. " +
                    "Плагин будет работать медленнее, но соберет больше данных. " +
                    "Выключить расширенные логи для ускорения работы?";
                var qwpfview = new qwindow280(qViewModel);
                qViewModel.CloseRequest += (s, e) => qwpfview.Close();
                bool? qok = qwpfview.ShowDialog();
                if (qok != null && qok == true) { Logger.TurnOffExtendedLogs(); } else Logger.Log("Расширенные логи вкл", 2);
            }

            Logger.Log("Сбор элементов", 1);

            List<RevitLinkInstance> links0 = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_RvtLinks)      //фильтр по категории Связи
                                                                         .WhereElementIsNotElementType()    //фильтр только экземпляры
                                                                         .Cast<RevitLinkInstance>()         //элементы категории Связи
                                                                         .ToList();                         //формируем список

            List<string> linksString = new List<string>();
            if (links0 == null || links0.Count == 0) linksString.Add("-----");
            else
            {
                Logger.Log("Существующие связи: ", 2);
                foreach (var link in links0)
                {
                    string[] nameparts = link.Name.Split(new char[] { ':' });
                    linksString.Add(nameparts[0]);
                    Logger.Log("   " + nameparts[0], 2);
                }
            }
                

            Logger.Log("Элементы собраны. Создаем списки для работы, проверяем, является ли модель файлом хранилища", 1);
            bool dws = doc.IsWorkshared; if (!dws) Logger.Log("Документ не является ФХ", 2);

            //Диалоговое окно
            Logger.Log("Диалоговое окно", 1);
            revitserverViewModel viewModel = new revitserverViewModel(linksString);
            var wpfview = new revitserver(viewModel);
            viewModel.CloseRequest += (s, ea) => wpfview.Close();
            bool? ok = wpfview.ShowDialog();

            //собираем список связей на вставку
            Logger.Log("Собираем список связей на вставку", 1);

            List<string> modelPaths = new List<string>();

            List<Node> allNodes = GetAllNodes(viewModel.Nodes).ToList();
            foreach (var node in allNodes)
            {
                if (node.IsChecked && node.IsModel && node.IsLocked==false) 
                {
                    string path = @"RSN:\\" + nova.revitserver + @"\" + node.Path;
                    modelPaths.Add(path);
                    Logger.Log("   " + path, 2);
                }
            }

            //транзакция - вставка связей

            //назначение наборов



            Logger.Log("Завершение работы.", 5);
            return Result.Succeeded;
        }
    }

}
