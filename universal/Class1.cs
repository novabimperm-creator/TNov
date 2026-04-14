using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
using TNovCommon;

namespace TNov
{
    [Transaction(TransactionMode.Manual)]
    public class Class1 : IExternalCommand
    {
        private TNovProgressBar Class1ProgressBar;
        private void ThreadStartingPoint()
        {
            this.Class1ProgressBar = new TNovProgressBar();
            this.Class1ProgressBar.Show();
            Dispatcher.Run();
        }
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            string TNovClassName = "Class1"; DateTime dateTime = DateTime.Now; string TNovVersion = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString();
            //подключение приложения и документа
            if (RevitAPI.UiApplication == null) { RevitAPI.Initialize(commandData); }
            UIDocument uidoc = RevitAPI.UiDocument; Document doc = RevitAPI.Document;
            UIApplication uiApp = RevitAPI.UiApplication; Autodesk.Revit.ApplicationServices.Application rvtApp = uiApp.Application;

            //проверка подключения, запись в журнал
            if(ServerUtils.CheckConnection(TNovClassName, TNovVersion)==false) return Result.Failed;

            // создание log - файла
            Logger.Initialize(TNovClassName,dateTime,TNovVersion);

            var viewModel0 = new AppVersionViewModel();

            string jsonpath0 = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "TNovClient/TNovSettings.json");
            viewModel0 = JsonConvert.DeserializeObject<AppVersionViewModel>(File.ReadAllText(jsonpath0));

            if (viewModel0.extendedLogs)

            {
                var qViewModel = new QuestionWindowViewModel();
                qViewModel.headtxt = "Включены расширенные логи. " +
                    "Плагин будет работать медленнее, но соберет больше данных. " +
                    "Выключить расширенные логи для ускорения работы?";
                var qwpfview = new QuestionWindow280(qViewModel);
                qViewModel.CloseRequest += (s, e) => qwpfview.Close();
                bool? qok = qwpfview.ShowDialog();
                if (qok != null && qok == true) { Logger.TurnOffExtendedLogs(); } else Logger.Log("Расширенные логи вкл", 2);
            }

            //сбор элементов
            //...

            List<ElementId> ids = new List<ElementId>();
            //...
            int allcount = ids.Count;

            Thread thread = new Thread(new ThreadStart(this.ThreadStartingPoint));
            thread.SetApartmentState(ApartmentState.STA);
            thread.IsBackground = true;
            thread.Start();
            Thread.Sleep(100);

            int PBCount = 0;
            this.Class1ProgressBar.TNov_ProgressBar.Dispatcher.Invoke<double>((Func<double>)(() => this.Class1ProgressBar.TNov_ProgressBar.Minimum = (double)PBCount));
            this.Class1ProgressBar.TNov_ProgressBar.Dispatcher.Invoke<string>((Func<string>)(() => this.Class1ProgressBar.value.Text = PBCount.ToString()));
            this.Class1ProgressBar.TNov_ProgressBar.Dispatcher.Invoke<double>((Func<double>)(() => this.Class1ProgressBar.TNov_ProgressBar.Maximum = allcount));
            this.Class1ProgressBar.TNov_ProgressBar.Dispatcher.Invoke<string>((Func<string>)(() => this.Class1ProgressBar.maxvalue.Text = allcount.ToString()));

            //назначение параметров
            using (Transaction transaction = new Transaction(doc))
            {
                transaction.Start("TNov - Хосты изоляции");
                Logger.Log("Открываем транзакцию", 1);

                foreach (var id in ids)
                {
                    PBCount++;
                    this.Class1ProgressBar.TNov_ProgressBar.Dispatcher.Invoke<double>((Func<double>)(() => this.Class1ProgressBar.TNov_ProgressBar.Value = (double)PBCount));
                    this.Class1ProgressBar.TNov_ProgressBar.Dispatcher.Invoke<string>((Func<string>)(() => this.Class1ProgressBar.value.Text = PBCount.ToString()));

                    //тело цикла
                    //...
                }

                transaction.Commit(); Logger.Log("Закрываем транзакцию", 1);
            }

            this.Class1ProgressBar.Dispatcher.Invoke((System.Action)(() => this.Class1ProgressBar.Close()));

            Logger.Log("Завершение работы.", 5);
            return Result.Succeeded;
        }
    }
}
