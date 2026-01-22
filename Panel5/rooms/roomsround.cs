using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.Attributes;
using System.Collections.Generic;

using System.Linq;
using Autodesk.Revit.DB.Architecture;
using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Newtonsoft.Json;
using System.IO;
using System.Windows.Threading;
using System.Threading;
using Autodesk.Revit.UI.Selection;
using TNov.main;

namespace TNov
{
    

    [Transaction(TransactionMode.Manual)]
    public class RoomsRound : IExternalCommand
    {
        private TNovProgressBar levnumProgressBar;
        private void ThreadStartingPoint()
        {
            this.levnumProgressBar = new TNovProgressBar();
            this.levnumProgressBar.Show();
            Dispatcher.Run();
        }
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            string TNovClassName = "Округлятор"; DateTime dateTime = DateTime.Now;
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
                if (qok != null && qok == true) { Logger.TurnOffExtendedLogs(); } else Logger.Log( "Расширенные логи вкл",2);
            }

            //параметры
            Guid NRoomSqParamGuid = new Guid("4f890165-ec27-4a22-811a-07e010101ec5"); //N_Площадь.Округленная
            Guid NRoomSqKParamGuid = new Guid("e6b18cda-4550-4531-afae-96a9035f7fca"); //N_Площадь.ОкруглСКоэффициентом

            Logger.Log( "Сбор элементов",1);
            List<Room> rooms = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_Rooms)   //фильтр по категории Помещения
                                                                         .WhereElementIsNotElementType()    //фильтр только экземпляры
                                                                         .Cast<Room>()                     //элементы категории Помещения
                                                                         .ToList();                         //формируем список
            Logger.Log( "Ищем неразмещенные помещения", 1);
            int ec = 0; //счетчик неразмещенных помещений

            foreach (Room room in rooms) //проверка наличия неразмещенных помещений
            {
                double area = room.get_Parameter(BuiltInParameter.ROOM_AREA).AsDouble();
                if (area == 0) { ec++; }
            }

            if (ec > 0) //если есть неразмещенные помещения - прерываем процесс
            {
                new infowindow280("В проекте присутствуют неразмещенные или избыточные помещения в количестве " +
                   ec + " шт. Удалите их плагином или через спецификацию.").ShowDialog();
                Logger.Log("В проекте присутствуют неразмещенные или избыточные помещения в количестве " + ec + " шт. Завершение работы", 3);
                return Result.Failed;
            }

            // Диалоговое окно
            Logger.Log("Диалоговое окно", 1);
            var viewModel = new RoomsViewModel();
            // Десериализация
            bool forProject = true;
            json js = new json("Офисография", in forProject, out bool canserialize, out string jsonpath);
            if (canserialize)
            {
                viewModel = JsonConvert.DeserializeObject<RoomsViewModel>(File.ReadAllText(jsonpath));
                Logger.Log( "Десериализация прошла успешно",1);
            }
            var wpfview = new RoomsWPF(viewModel);
            viewModel.CloseRequest += (s, e) => wpfview.Close();
            bool? ok = wpfview.ShowDialog();
            if (ok != null && ok == true) { } else { Logger.Log("Запуск отменен пользователем. Завершение работы.", 3); return Result.Cancelled; }
            //Сериализация
            try
            {
                File.WriteAllText(jsonpath, JsonConvert.SerializeObject(viewModel));
                Logger.Log( "Сериализация прошла успешно",1);
            }
            catch (Exception ex) { Logger.Log( "Ошибка при сериализации: " + ex.Message, 4); }

            string names1 = viewModel.k05; string names2 = viewModel.k03;

            //получаем имена помещений, удаляем возможные пробелы в начале и конце имен
            string[] n1 = names1.Split(','); for (int i = 0; i < n1.Length; i++) {n1[i] = n1[i].Trim(); }
            string[] n2 = names2.Split(','); for (int i = 0; i < n2.Length; i++) { n2[i] = n2[i].Trim(); }

            List<Element> rooms1 = new List<Element>();

            //Выбор элементов

            Logger.Log( "Финальный список помещений",1);

            if (viewModel.selection == 2)
            {
                Selection elemselection = uidoc.Selection;

                
                ISelectionFilter _filter = new RoomSelectionFilter();
                try
                {
                    Reference reference = RevitAPI.UiDocument.Selection.PickObject(ObjectType.Element, _filter, $"Выберите помещение");
                    rooms1.Add(doc.GetElement(reference));
                }
                catch (Autodesk.Revit.Exceptions.OperationCanceledException e)
                {
                    Logger.Log( "Ошибка: " + e.Message,4);
                    return Result.Cancelled;
                }

            }
            else
            {
                foreach (var r in rooms)
                {
                    rooms1.Add(r); //Коллекция всех помещений
                }
            }

            

            int roomsCount=rooms1.Count();

            using (Transaction transaction = new Transaction(doc))
            {
                transaction.Start("TNov - Округлятор");
                Logger.Log( "Открываем транзакцию",1);

                Thread thread = new Thread(new ThreadStart(this.ThreadStartingPoint));
                thread.SetApartmentState(ApartmentState.STA);
                thread.IsBackground = true;
                thread.Start();
                Thread.Sleep(100);

                int PBCount = 0;
                this.levnumProgressBar.TNov_ProgressBar.Dispatcher.Invoke<double>((Func<double>)(() => this.levnumProgressBar.TNov_ProgressBar.Minimum = (double)PBCount));
                this.levnumProgressBar.TNov_ProgressBar.Dispatcher.Invoke<string>((Func<string>)(() => this.levnumProgressBar.value.Text = PBCount.ToString()));
                this.levnumProgressBar.TNov_ProgressBar.Dispatcher.Invoke<double>((Func<double>)(() => this.levnumProgressBar.TNov_ProgressBar.Maximum = (double)roomsCount));
                this.levnumProgressBar.TNov_ProgressBar.Dispatcher.Invoke<string>((Func<string>)(() => this.levnumProgressBar.maxvalue.Text = roomsCount.ToString()));


                foreach (Room room in rooms1) //проверка наличия неразмещенных помещений
                {
                    PBCount++;
                    this.levnumProgressBar.TNov_ProgressBar.Dispatcher.Invoke<double>((Func<double>)(() => this.levnumProgressBar.TNov_ProgressBar.Value = (double)PBCount));
                    this.levnumProgressBar.TNov_ProgressBar.Dispatcher.Invoke<string>((Func<string>)(() => this.levnumProgressBar.value.Text = PBCount.ToString()));

                    Logger.Log( "Помещение "+room.Id.ToString(),2);

                    double area = room.get_Parameter(BuiltInParameter.ROOM_AREA).AsDouble() * 0.3048 * 0.3048;
                    double areaR = Math.Round(area, 1); Logger.Log( "   площадь: " + areaR.ToString(),2);
                    string name = room.Name;
                    double k = 1;
                    foreach (string n in n1) { if (name.Contains(n)) { k = 0.5; } }
                    foreach (string n in n2) { if (name.Contains(n)) { k = 0.3; } }
                    double areaRK = Math.Round((areaR * k + 0.000001), 1); 
                    Logger.Log( "   площадь с коэфф: " + areaR.ToString(), 2);
                    room.get_Parameter(NRoomSqParamGuid)?.Set(areaR);
                    room.get_Parameter(NRoomSqKParamGuid)?.Set(areaRK);
                    Logger.Log( "   "+"параметры назначены успешно",2);
                }

                transaction.Commit();
                this.levnumProgressBar.Dispatcher.Invoke((System.Action)(() => this.levnumProgressBar.Close()));
                Logger.Log( "Закрываем транзакцию",1);
            }
            Logger.Log( "Завершение работы.",5);
            return Result.Succeeded;
        }
    }
}
