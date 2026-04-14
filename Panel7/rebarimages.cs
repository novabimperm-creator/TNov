using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.Attributes;
using System.Collections.Generic;

using System.Linq;
using System;
using Rebar = Autodesk.Revit.DB.Structure.Rebar;
using System.Windows.Threading;
using System.Threading;
using TNovCommon;
using Newtonsoft.Json;
using System.IO;

namespace TNov
{
    [Transaction(TransactionMode.Manual)]
    public class RebarImages : IExternalCommand
    {
        private TNovProgressBar rbrProgressBar;
        private void ThreadStartingPoint()
        {
            this.rbrProgressBar = new TNovProgressBar();
            this.rbrProgressBar.Show();
            Dispatcher.Run();
        }
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            string TNovClassName = "Эскизы деталей"; DateTime dateTime = DateTime.Now;
            //подключение приложения и документа
            if (RevitAPI.UiApplication == null) { RevitAPI.Initialize(commandData); }
            UIDocument uidoc = RevitAPI.UiDocument; Document doc = RevitAPI.Document;
            UIApplication uiApp = RevitAPI.UiApplication; Autodesk.Revit.ApplicationServices.Application rvtApp = uiApp.Application;
            
            //проверка подключения, запись в журнал
            if(ServerUtils.CheckConnection(TNovClassName)==false) return Result.Failed;

            // создание log - файла
            Logger.Initialize(TNovClassName);

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
                if (qok != null && qok == true) { Logger.TurnOffExtendedLogs(); } else Logger.Log( "Расширенные логи вкл", 2);
            }

            //Список используемых параметров

            Guid adskRebarImageOnParamGuid = new Guid("2f9abac5-9608-4bb6-b509-d75b738777cf"); //A_Арм Эскиз вкл
            Guid adskRebarImageParamGuid = new Guid("ffd5b2a7-3613-4013-ab33-ae18647b7e98"); //A_Арм Эскиз формы

            Logger.Log("Сбор элементов",1);

            List<Rebar> rebar = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_Rebar)   //фильтр по категории Несущая арматура
                                                                         .WhereElementIsNotElementType()    //фильтр только экземпляры
                                                                         .OfClass(typeof(Rebar))            //отсеиваем IFC-арматуру
                                                                         .Cast<Rebar>()                     //элементы категории Несущая арматура
                                                                         .ToList();                         //формируем список

            List<Rebar> rebarImageOn = new List<Rebar>();


            foreach (Rebar rbr in rebar) //заполняем список арматуры с включенным параметром A_Арм Эскиз вкл
            {
                int imageOn = rbr.get_Parameter(adskRebarImageOnParamGuid).AsInteger();
                if (imageOn == 1)
                {
                    rebarImageOn.Add(rbr);
                }
            }

            Thread thread = new Thread(new ThreadStart(this.ThreadStartingPoint));
            thread.SetApartmentState(ApartmentState.STA);
            thread.IsBackground = true;
            thread.Start();
            Thread.Sleep(100);

            int PBCount = 0; int allcount = rebarImageOn.Count;
            this.rbrProgressBar.TNov_ProgressBar.Dispatcher.Invoke<double>((Func<double>)(() => this.rbrProgressBar.TNov_ProgressBar.Minimum = (double)PBCount));
            this.rbrProgressBar.TNov_ProgressBar.Dispatcher.Invoke<string>((Func<string>)(() => this.rbrProgressBar.value.Text = PBCount.ToString()));
            this.rbrProgressBar.TNov_ProgressBar.Dispatcher.Invoke<double>((Func<double>)(() => this.rbrProgressBar.TNov_ProgressBar.Maximum = (double)allcount));
            this.rbrProgressBar.TNov_ProgressBar.Dispatcher.Invoke<string>((Func<string>)(() => this.rbrProgressBar.maxvalue.Text = allcount.ToString()));


            using (Transaction transaction = new Transaction(doc))
            {
                transaction.Start("TNov - Эскизы деталей");
                Logger.Log("Открываем транзакцию",1);

                foreach (Rebar rbr in rebarImageOn) //заполняем параметр A_Арм Эскиз формы
                {
                    ElementId baseimage = rbr.LookupParameter("Изображение формы").AsElementId();
                    try
                    {
                        rbr.get_Parameter(adskRebarImageParamGuid)?.Set(baseimage);
                        Logger.Log("Элемент " + rbr.Id.ToString() + " назначено",2);
                    }
                    catch (Exception ex) { Logger.Log("Элемент " + rbr.Id.ToString() + " ошибка: "+ex.Message, 4); }

                    PBCount++;
                    this.rbrProgressBar.TNov_ProgressBar.Dispatcher.Invoke<double>((Func<double>)(() => this.rbrProgressBar.TNov_ProgressBar.Value = (double)PBCount));
                    this.rbrProgressBar.TNov_ProgressBar.Dispatcher.Invoke<string>((Func<string>)(() => this.rbrProgressBar.value.Text = PBCount.ToString()));

                }

                //var info1 = new InfoWindow280("Успешно!\nЭскизы у системной арматуры заполнены."); info1.ShowDialog();
                transaction.Commit();
                this.rbrProgressBar.Dispatcher.Invoke((System.Action)(() => this.rbrProgressBar.Close()));
                Logger.Log("Закрываем транзакцию",1);
            }

            Logger.Log("Завершение работы.",5);
            return Result.Succeeded;
        }
    }
}
