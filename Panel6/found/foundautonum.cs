using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.Attributes;
using System.Collections.Generic;
using System.Linq;
using System;
using Autodesk.Revit.UI.Selection;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Collections.ObjectModel;
using System.Windows.Input;
using Newtonsoft.Json;
using System.IO;
using System.Threading;
using System.Windows.Threading;
using TNov.main;

namespace TNov
{

    
    [Transaction(TransactionMode.Manual)]
    public class FoundAutoNum : IExternalCommand
    {
        private TNovProgressBar foundautonumProgressBar;
        private void ThreadStartingPoint()
        {
            this.foundautonumProgressBar = new TNovProgressBar();
            this.foundautonumProgressBar.Show();
            Dispatcher.Run();
        }
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            string TNovClassName = "Сваи Автонумерация"; DateTime dateTime = DateTime.Now;
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
            BuiltInParameter gm = BuiltInParameter.ALL_MODEL_MODEL; //параметр Группа модели
            Guid pileNumberParamGuid = new Guid("3df328ab-5e4d-4da0-9138-42f1a8bb54a7"); //N_Свая.Номер

            Logger.Log("Сбор элементов",1);
            
            List<FamilyInstance> piles = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_StructuralFoundation)   //Фундаменты семействами
                                                                         .WhereElementIsNotElementType()
                                                                         .OfClass(typeof(Autodesk.Revit.DB.FamilyInstance))
                                                                         .Cast<FamilyInstance>()
                                                                         .ToList();

            List<FamilyInstance> piles1 = new List<FamilyInstance>();

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
            { var info1 = new infowindow280("В проекте отсутствуют сваи."); info1.ShowDialog(); return Result.Failed; }

            ElementId workviewid = uidoc.ActiveView.Id;
            Logger.Log("Элементы собраны. Выбор сценария",1);

            //Диалог
            var viewModel = new FoundAutoNumViewModel();
            // Десериализация
            bool forProject = true;
            json js = new json(in TNovClassName, in forProject, out bool canserialize, out string jsonpath);
            if (canserialize)
            {
                viewModel = JsonConvert.DeserializeObject<FoundAutoNumViewModel>(File.ReadAllText(jsonpath));
                Logger.Log("Десериализация прошла успешно",1);
            }
            viewModel.parameterName = "N_Свая.Номер";
            var wpfview = new FoundAutoNumWPF(viewModel);
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

            bool parse = int.TryParse(viewModel.startvalue, out int startvalue); if (parse==false) { return Result.Cancelled; }
            int sel = viewModel.selection; //выборка элементов
            bool divide = viewModel.divide; //делить ли по отметкам и длинам
            string rule = viewModel.rule; int rul = 0; //сценарий нумерации
            //double tolerance = viewModel.tolerance; //допуск
            
            switch (rule)
            {
                case "Слева направо, снизу вверх": rul = 1; break;
                case "Слева направо, сверху вниз": rul = 2; break;
                case "Справа налево, снизу вверх": rul = 3; break;
                case "Справа налево, сверху вниз": rul = 4; break;
                case "По ID элементов": rul = 5; break;
            }
            Logger.Log("Выбран сценарий " + sel.ToString() + ", правило "+rule,1);

            

            List<Element> pileslist = new List<Element>();
            
            //Выбор элементов

            Logger.Log("Собираем сваи в список pileslist",1);

            if (sel == 2)
            {
                Selection elemselection = uidoc.Selection;

                List<Element> selectedElements = null;

                try
                {
                    selectedElements = elemselection.PickElementsByRectangle(new PileSelectionFilter(),
                                        "Выберите сваи или группы, содержащие сваи, при помощи рамки (Esc - отмена)").ToList();
                }
                catch (Autodesk.Revit.Exceptions.OperationCanceledException e)
                {
                    Logger.Log("Ошибка: " + e.Message, 4);
                    return Result.Cancelled;
                }


                foreach (var element in selectedElements)
                {
                    if (element is FamilyInstance)
                    {
                        FamilyInstance fi = element as FamilyInstance;
                        if (fi.Symbol.FamilyName.Contains("Свая")) { pileslist.Add(element); } //Коллекция выбранных свай      
                    }
                }
            }
            else 
            {
                foreach (var p in piles1)
                {
                    pileslist.Add(p); //Коллекция всех свай
                }
            }
            
            Logger.Log("Список pileslist собран. Создаем список элементов класса Pile",1);

            List<Pile> piless = new List<Pile>(); //список элементов класса Pile
            //в класс Pile передаем ид элемента (ElementId), число сортировки (sort), отметку по высоте (z), ид типа
            //число сортировки определяется исходя из способа сортировки (переменная rul)
            foreach (var p in pileslist)
            {
                ElementId pid = p.Id; Element elem = doc.GetElement(p.Id);
                double z = 0;
                z = (double)(elem.LookupParameter("Свая.ОтмНизаРостверка")?.AsDouble()); //Свая.ОтмНизаРостверка
                Autodesk.Revit.DB.LocationPoint lp = (LocationPoint)p.Location;
                double x = lp.Point.X ; double y = lp.Point.Y ;
                //x = Math.Round(x/tolerance)*tolerance; y = Math.Round(y / tolerance) * tolerance; //учитываем параметр Настройка
                double sort = 0; //переменная для числа сортировки
                switch (rul) //определяем число сортировки
                {
                    case 1:
                        sort = y * 1000 + x;
                        break;
                    case 2:
                        sort = y * (-1000) + x;
                        break;
                    case 3:
                        sort = y * 1000 - x;
                        break;
                    case 4:
                        sort = y * (-1000) - x;
                        break;
                    case 5:
                        sort = pid.IntegerValue;
                        break;
                }
                Pile pl = new Pile();
                pl.elemid = pid; pl.sort = sort; pl.z = z; pl.type = pl.type = elem.GetTypeId().ToString(); 
                piless.Add(pl);
            }
            Logger.Log("Список элементов класса Pile создан. Сортируем их",1);

            List<Pile> pilestowork = new List<Pile>(); //список свай-Pile в работу
            if (divide)
            {
                var pbl = from pl in piless //сортированный список свай-Pile по z
                          orderby pl.z
                          select pl;
                var levels = from pl in pbl //список z
                             group pl by pl.z;
                foreach (var level in levels)
                {
                    List<Pile> pilesatlevel = new List<Pile>(); //список свай-Pile на уровне
                    foreach (var p in level)
                    {
                        pilesatlevel.Add(p);
                    }
                    var psorted = from pl in pilesatlevel //сортированный список свай-Pile по свойству sort на уровне
                                  orderby pl.sort
                                  select pl;
                    foreach (var p in psorted) { pilestowork.Add(p); } //заполняем список в работу
                }
            }
            else
            {
                var psorted = from pl in piless //сортированный список свай-Pile по свойству sort
                              orderby pl.sort
                              select pl;
                foreach (var p in psorted) { pilestowork.Add(p); } //заполняем список в работу
            }

            int allcount=pilestowork.Count;

            Thread thread = new Thread(new ThreadStart(this.ThreadStartingPoint));
            thread.SetApartmentState(ApartmentState.STA);
            thread.IsBackground = true;
            thread.Start();
            Thread.Sleep(100);

            int PBCount = 0;
            this.foundautonumProgressBar.TNov_ProgressBar.Dispatcher.Invoke<double>((Func<double>)(() => this.foundautonumProgressBar.TNov_ProgressBar.Minimum = (double)PBCount));
            this.foundautonumProgressBar.TNov_ProgressBar.Dispatcher.Invoke<string>((Func<string>)(() => this.foundautonumProgressBar.value.Text = PBCount.ToString()));
            this.foundautonumProgressBar.TNov_ProgressBar.Dispatcher.Invoke<double>((Func<double>)(() => this.foundautonumProgressBar.TNov_ProgressBar.Maximum = (double)allcount));
            this.foundautonumProgressBar.TNov_ProgressBar.Dispatcher.Invoke<string>((Func<string>)(() => this.foundautonumProgressBar.maxvalue.Text = allcount.ToString()));


            using (Transaction transaction = new Transaction(doc))
            {
                transaction.Start("TNov - автонумерация свай");
                Logger.Log("Открываем транзакцию",1);
                int i = startvalue;

                foreach (var p in pilestowork)
                {
                    
                    Element elem = doc.GetElement(p.elemid);
                    try
                    {
                        elem.get_Parameter(pileNumberParamGuid)?.Set(i.ToString());
                        Logger.Log("Элемент " + elem.Id.ToString(), 2);
                        PBCount++;
                        this.foundautonumProgressBar.TNov_ProgressBar.Dispatcher.Invoke<double>((Func<double>)(() => this.foundautonumProgressBar.TNov_ProgressBar.Value = (double)PBCount));
                        this.foundautonumProgressBar.TNov_ProgressBar.Dispatcher.Invoke<string>((Func<string>)(() => this.foundautonumProgressBar.value.Text = PBCount.ToString()));
                        i++;
                    }
                    catch (Exception ex) 
                    {
                        Logger.Log("Элемент " + elem.Id.ToString()+" ошибка: "+ex.Message, 4);
                    }
                }
                
                //var info1 = new infowindow280("Успешно!"); info1.ShowDialog();
                transaction.Commit();
                this.foundautonumProgressBar.Dispatcher.Invoke((System.Action)(() => this.foundautonumProgressBar.Close()));
                Logger.Log("Закрываем транзакцию",1);

            }
                
            Logger.Log("Завершение работы.",5);
            return Result.Succeeded;
        }
    }
    
}
