using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.Attributes;
using System.Collections.Generic;
using System.Linq;
using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Collections.ObjectModel;
using Newtonsoft.Json;
using System.IO;
using System.Windows.Threading;
using System.Threading;
using TNov.main;

namespace TNov
{

    
    [Transaction(TransactionMode.Manual)]
    public class ParkMark : IExternalCommand
    {
        private TNovProgressBar pmProgressBar;
        private void ThreadStartingPoint()
        {
            this.pmProgressBar = new TNovProgressBar();
            this.pmProgressBar.Show();
            Dispatcher.Run();
        }
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            string TNovClassName = "Парковки Марка Позиция"; DateTime dateTime = DateTime.Now;
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
                if (qok != null && qok == true) { Logger.TurnOffExtendedLogs(); } else Logger.Log( "Расширенные логи вкл", 2);
            }

            //параметры
            Guid NLevelNumberParamGuid = new Guid("4d2aa1b8-727c-43a1-8b1e-8c22dd484e11"); //N_Эт.Номер
            Guid adskPositionParamGuid = new Guid("ae8ff999-1f22-4ed7-ad33-61503d85f0f4"); //A_Позиция
            BuiltInParameter markParam = BuiltInParameter.DOOR_NUMBER; //Марка

            Logger.Log("Сбор элементов",1);

            List<FamilyInstance> parks = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_Parking)   //фильтр по категории Парковка
                                                                         .WhereElementIsNotElementType()
                                                                         .Cast<FamilyInstance>()
                                                                         .ToList();


            int pc = parks.Count;
            if(pc ==  0) 
            { var info1 = new infowindow280("В проекте отсутствуют элементы паркинга."); info1.ShowDialog(); return Result.Failed; }

            ElementId workviewid = uidoc.ActiveView.Id;
            Logger.Log("Элементы собраны. Выбор сценария",1);
            
            //Диалог
            var viewModel = new ParkMarkViewModel();
            // Десериализация
            bool forProject = true;
            json js = new json(in TNovClassName, in forProject, out bool canserialize, out string jsonpath);
            if (canserialize)
            {
                viewModel = JsonConvert.DeserializeObject<ParkMarkViewModel>(File.ReadAllText(jsonpath));
                Logger.Log("Десериализация прошла успешно",1);
            }
            var wpfview = new ParkMarkWPF(viewModel);
            viewModel.CloseRequest += (s, e) => wpfview.Close();
            bool? ok = wpfview.ShowDialog();
            if (ok != null && ok == true) { }
            else { Logger.Log("Запуск отменен пользователем. Завершение работы.", 3); return Result.Cancelled; }
            //Сериализация
            try
            {
                File.WriteAllText(jsonpath, JsonConvert.SerializeObject(viewModel));
                Logger.Log("Сериализация прошла успешно",1);
            }
            catch (Exception ex) { Logger.Log("Ошибка при сериализации: " + ex.Message,4); }

            string scenario = viewModel.scenario; 
            
            bool all = viewModel.all; string allstr = " (поэтажно)"; if(all) { allstr = " сквозное заполнение"; }
            string prefix = viewModel.prefix; 

            Logger.Log("Сценарий: " + scenario  +", "+ allstr + ";",1);

            string badelems = "";

            Logger.Log("Создаем список элементов класса Park",1);

            List<Park> parkss = new List<Park>(); //список элементов класса Park
            //в класс Park передаем ид элемента (ElementId), Эт.Номер, int-значение исходного параметра
            //число сортировки определяется исходя из способа сортировки (переменная rul)
            foreach (var p in parks)
            {
                Logger.Log(p.Id.ToString(),2);
                ElementId pid = p.Id; Element elem = doc.GetElement(pid);
                int mark = 0;

                Parameter param1 = elem.get_Parameter(adskPositionParamGuid); Parameter param2 = elem.get_Parameter(markParam);
                if (scenario.Contains("Марку в Позицию")) 
                { param2 = elem.get_Parameter(adskPositionParamGuid); param1 = elem.get_Parameter(markParam); }

                string param1value = param1.AsString(); Logger.Log("   "+ param1value, 2);
                if(param1value==null||param1value.Length==0) { badelems += pid.ToString()+", "; Logger.Log("   исходный параметр не заполнен", 2); continue; }
                if (param1value.Contains(prefix)) { param1value=param1value.Replace(prefix, ""); }
                int.TryParse(param1value, out mark); Logger.Log("   " + mark.ToString(), 2);
                double elev = p.get_Parameter(NLevelNumberParamGuid).AsDouble();
                Park pk = new Park();
                pk.elemid = pid; pk.elevation = elev; pk.mark= mark;
                parkss.Add(pk);
            }
            Logger.Log("Список элементов класса Park создан",1);

            if (scenario.Contains("Марку в Позицию")==false) { prefix = ""; } //целевой параметр - марка

            using (Transaction transaction = new Transaction(doc))
            {
                transaction.Start("TNov - Парковки Марка Позиция");
                TransactionHandler.SetWarningResolver(transaction);
                Logger.Log("Открываем транзакцию",1);

                Thread thread = new Thread(new ThreadStart(this.ThreadStartingPoint));
                thread.SetApartmentState(ApartmentState.STA);
                thread.IsBackground = true;
                thread.Start();
                Thread.Sleep(100);

                int PBCount = 0;
                this.pmProgressBar.TNov_ProgressBar.Dispatcher.Invoke<double>((Func<double>)(() => this.pmProgressBar.TNov_ProgressBar.Minimum = (double)PBCount));
                this.pmProgressBar.TNov_ProgressBar.Dispatcher.Invoke<string>((Func<string>)(() => this.pmProgressBar.value.Text = PBCount.ToString()));
                this.pmProgressBar.TNov_ProgressBar.Dispatcher.Invoke<double>((Func<double>)(() => this.pmProgressBar.TNov_ProgressBar.Maximum = (double)parkss.Count()));
                this.pmProgressBar.TNov_ProgressBar.Dispatcher.Invoke<string>((Func<string>)(() => this.pmProgressBar.maxvalue.Text = parkss.Count().ToString()));



                var pbl = from pk in parkss //сортированный список Park по elev
                            orderby pk.elevation
                            select pk;
                var levels = from pk in pbl //список elev
                             group pk by pk.elevation;
                
                int j = 1;
                foreach (var level in levels)
                {
                    List<Park> parksatlevel = new List<Park>(); //список Park на уровне
                    foreach (var p in level)
                    {
                        parksatlevel.Add(p);
                    }
                    var psorted = from pk in parksatlevel //сортированный список Park по свойству mark на уровне
                                    orderby pk.mark
                                    select pk;
                    int i = 1; 
                    foreach (var p in psorted) 
                    {
                        PBCount++;
                        this.pmProgressBar.TNov_ProgressBar.Dispatcher.Invoke<double>((Func<double>)(() => this.pmProgressBar.TNov_ProgressBar.Value = (double)PBCount));
                        this.pmProgressBar.TNov_ProgressBar.Dispatcher.Invoke<string>((Func<string>)(() => this.pmProgressBar.value.Text = PBCount.ToString()));

                        Element elem = doc.GetElement(p.elemid);
                        Parameter param1 = elem.get_Parameter(adskPositionParamGuid); Parameter param2 = elem.get_Parameter(markParam);
                        if (scenario.Contains("Марку в Позицию"))
                        { param2 = elem.get_Parameter(adskPositionParamGuid); param1 = elem.get_Parameter(markParam); }


                        Logger.Log("Элемент " + elem.Id.ToString() +" ,"+ param1.Definition.Name+": "+p.mark.ToString(), 2);
                        int k = j; if (!all) { k = i; }
                        param2?.Set(prefix+k.ToString());
                        Logger.Log("   " + param2 + ": " + prefix + k.ToString(), 2);
                        i++; j++;
                    } 
                }
                
                transaction.Commit();
                this.pmProgressBar.Dispatcher.Invoke((System.Action)(() => this.pmProgressBar.Close()));
                Logger.Log("Закрываем транзакцию",1);

            }

            if (badelems.Length > 0)
            {
                int ind = badelems.Length-1;
                badelems = badelems.Remove(ind);
                ind--; badelems = badelems.Remove(ind);
                // Диалоговое окно
                var viewModel1 = new infowindowtextfieldViewModel();
                viewModel1.headtxt = "У некоторых паркомест на заполнен исходный параметр:";
                viewModel1.ids = badelems;
                viewModel1.lowtxt = "Эти элементы не обработаны, проверьте их.";
                var wpfview1 = new infowindowtextfield(viewModel1);
                viewModel1.CloseRequest += (s, e) => wpfview1.Close();
                bool? ok1 = wpfview1.ShowDialog();
            }

            Logger.Log("Завершение работы.",5);
            return Result.Succeeded;
        }
    }
    
}
