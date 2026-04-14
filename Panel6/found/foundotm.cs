using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.Attributes;
using System.Collections.Generic;
using System.Linq;
using System;
using System.IO;
using Outline = Autodesk.Revit.DB.Outline;
using View = Autodesk.Revit.DB.View;
using System.Windows.Threading;
using System.Threading;
using Newtonsoft.Json;
using TNovCommon;

namespace TNov
{
    [Transaction(TransactionMode.Manual)]
    public class FoundOtm : IExternalCommand
    {
        
        private TNovProgressBar foundcutProgressBar;
        private void ThreadStartingPoint()
        {
            this.foundcutProgressBar = new TNovProgressBar();
            this.foundcutProgressBar.Show();
            Dispatcher.Run();
        }
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            string TNovClassName = "Сваи Заполнить отметки"; DateTime dateTime = DateTime.Now;
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
                if (qok != null && qok == true) { Logger.TurnOffExtendedLogs(); } else Logger.Log("Расширенные логи вкл",2);
            }

            //параметры
            BuiltInParameter gm = BuiltInParameter.ALL_MODEL_MODEL; //параметр Группа модели

            Logger.Log("Сбор элементов",1);
            List<Autodesk.Revit.DB.Floor> foundations = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_StructuralFoundation)   //Фундаменты
                                                                         .WhereElementIsNotElementType()
                                                                         .OfClass(typeof(Autodesk.Revit.DB.Floor))  //отсеиваем модели в контексте
                                                                         .Cast<Autodesk.Revit.DB.Floor>()
                                                                         .ToList();
            
            List<FamilyInstance> piles = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_StructuralFoundation)   //Фундаменты семействами
                                                                         .WhereElementIsNotElementType()
                                                                         .OfClass(typeof(Autodesk.Revit.DB.FamilyInstance))
                                                                         .Cast<FamilyInstance>()
                                                                         .ToList();

            List<FamilyInstance> piles1 = new List<FamilyInstance>();

            BasePoint basePoint = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_ProjectBasePoint).Cast<BasePoint>().First();

            foreach (var p in piles) //ищем сваи
            {
                string pvalue = p.Symbol.get_Parameter(gm).AsString();
                if (pvalue != null)
                {
                    if (pvalue.Contains("Свая")) { piles1.Add(p); }
                }
            }

            int pc = piles1.Count;
            if(pc ==  0) 
            {
                new InfoWindow280("В проекте отсутствуют сваи.").ShowDialog();
                Logger.Log("В проекте отсутствуют сваи. Завершение работы.", 3);
                return Result.Failed;
            }


            int allcount = piles1.Count;

            Thread thread = new Thread(new ThreadStart(this.ThreadStartingPoint));
            thread.SetApartmentState(ApartmentState.STA);
            thread.IsBackground = true;
            thread.Start();
            Thread.Sleep(100);

            int PBCount = 0;
            this.foundcutProgressBar.TNov_ProgressBar.Dispatcher.Invoke<double>((Func<double>)(() => this.foundcutProgressBar.TNov_ProgressBar.Minimum = (double)PBCount));
            this.foundcutProgressBar.TNov_ProgressBar.Dispatcher.Invoke<string>((Func<string>)(() => this.foundcutProgressBar.value.Text = PBCount.ToString()));
            this.foundcutProgressBar.TNov_ProgressBar.Dispatcher.Invoke<double>((Func<double>)(() => this.foundcutProgressBar.TNov_ProgressBar.Maximum = (double)allcount));
            this.foundcutProgressBar.TNov_ProgressBar.Dispatcher.Invoke<string>((Func<string>)(() => this.foundcutProgressBar.maxvalue.Text = allcount.ToString()));


            using (Transaction transaction = new Transaction(doc))
            {
                transaction.Start("TNov - отметки низа ростверка");
                Logger.Log("Открываем транзакцию",1);

                foreach (FamilyInstance p in piles1)
                {
                    
                    Element elem1 = doc.GetElement(p.Id);
                    
                    LocationPoint linkElem_lp = (LocationPoint)elem1.Location;
                    XYZ point = linkElem_lp.Point;
                    double zz = point.Z- basePoint.Position.Z ; zz = zz*304.8;

                    Parameter param = elem1.LookupParameter("Свая.ОтмНизаРостверка");
                    if (param != null) {
                        try
                        {
                            param.Set(zz); Logger.Log("Элемент " + p.Id.ToString() + " назначено " + zz.ToString(),2);
                        }
                        catch (Exception e) { Logger.Log("Элемент " + p.Id.ToString() + " ошибка: " + e.Message,4); }
                    }

                    PBCount++;
                    this.foundcutProgressBar.TNov_ProgressBar.Dispatcher.Invoke<double>((Func<double>)(() => this.foundcutProgressBar.TNov_ProgressBar.Value = (double)PBCount));
                    this.foundcutProgressBar.TNov_ProgressBar.Dispatcher.Invoke<string>((Func<string>)(() => this.foundcutProgressBar.value.Text = PBCount.ToString()));

                }

                transaction.Commit();
                this.foundcutProgressBar.Dispatcher.Invoke((System.Action)(() => this.foundcutProgressBar.Close()));
                Logger.Log("Закрываем транзакцию",1);

            }

            Logger.Log("Завершение работы.",5);
            return Result.Succeeded;
        }
    }
    
}
