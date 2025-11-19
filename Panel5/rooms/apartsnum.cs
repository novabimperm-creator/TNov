using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.Attributes;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB.Architecture;
using System;
using System.IO;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Threading;
using System.Threading;
using TNov.main;
using Newtonsoft.Json;

namespace TNov
{
    public class apartsnumViewModel : INotifyPropertyChanged
    {
        private string _parameterName = "N_Кв.Номер";
        public string parameterName { get => _parameterName; set { _parameterName = value; OnPropertyChanged(); } }
        private string _first = "1";
        public string first { get => _first; set { _first = value; } }

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
    public class apartsnum : IExternalCommand
    {
        private TNovProgressBar apartsnumProgressBar;
        private void ThreadStartingPoint()
        {
            this.apartsnumProgressBar = new TNovProgressBar();
            this.apartsnumProgressBar.Show();
            Dispatcher.Run();
        }
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            string TNovClassName = "Сквозные номера квартир"; DateTime dateTime = DateTime.Now;
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

            //Проверка актуальности шаблона
            templatecheck tc = new templatecheck(in commandData, out bool oldProject);
            
            //Список используемых параметров

            string apartment = "N_Квартира";
            if (oldProject == true) { apartment = "квартира"; }
            string levelnumber = "N_Эт.Номер";
            if (oldProject == true) { levelnumber = "Эт.Номер"; }
            string numAtLevel = "N_Кв.НомерНаЭтаже";
            if (oldProject == true) { numAtLevel = "Квартира.Номер.ПоЭтажам"; }
            string apartNumber = "N_Кв.Номер";
            if (oldProject == true) { apartNumber = "квартира.номер"; }
            

            Logger.Log( "Сбор элементов",1);
            List<Room> rooms = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_Rooms)   //фильтр по категории Помещения
                                                                         .WhereElementIsNotElementType()    //фильтр только экземпляры
                                                                         .Cast<Room>()                     //элементы категории Помещения
                                                                         .ToList();                         //формируем список

            List<Room> roomsA = new List<Room>(); //список помещений квартир

            foreach (Room room in rooms) //заполнение списка помещений квартир
            {
                int aBool = (int)(room.LookupParameter(apartment)?.AsInteger());
                if (aBool == 1) { roomsA.Add(room); }
            }
            
            var roomsAF = from room in roomsA //сортировка квартир по Эт.Номеру и номеру на этаже
                                      orderby (double)(room.LookupParameter(levelnumber)?.AsDouble())*1000+ (int)(room.LookupParameter(numAtLevel)?.AsInteger())
                                      select room;

            var floors = from room in roomsAF //группирование по Эт.Номеру
                        group room by ((double)(room.LookupParameter(levelnumber)?.AsDouble()) * 0.3048 * 0.3048 * 1000 + (int)(room.LookupParameter(numAtLevel)?.AsInteger())).ToString();
            
            List<Room>roomsToSet = new List<Room>(); //итоговый список помещений
            List<int>values = new List<int>(); //итоговый список значений параметра

            int i1 = 1;

            // Диалоговое окно
            Logger.Log( "Диалоговое окно - ввод первого номера",1);
            var viewModel = new apartsnumViewModel();
            viewModel.parameterName = apartNumber;
            var wpfview = new apartsnumwpf(viewModel);
            viewModel.CloseRequest += (s, e) => wpfview.Close();
            bool? ok = wpfview.ShowDialog();
            if (ok != null && ok == true) { } else { Logger.Log("Запуск отменен пользователем. Завершение работы.", 3); return Result.Cancelled; }

            string first = viewModel.first;

            Logger.Log( "Проверка корректности номера",1);

            int.TryParse(first, out i1); //первый номер квартиры
            
            Logger.Log( "Заполняем итоговые списки",1);

            foreach (var f in floors)
            {
                Logger.Log( "Этаж " +f.First().LookupParameter(levelnumber)?.AsDouble().ToString(),2);
                foreach (Room room in f)
                {
                    roomsToSet.Add(room); //заполняем итоговый список помещений
                    values.Add(i1); //заполняем итоговый список значений параметра
                }
                i1++;
            }

            using (Transaction transaction = new Transaction(doc))
            {
                Thread thread = new Thread(new ThreadStart(this.ThreadStartingPoint));
                thread.SetApartmentState(ApartmentState.STA);
                thread.IsBackground = true;
                thread.Start();
                Thread.Sleep(100);

                int PBCount = 0;
                this.apartsnumProgressBar.TNov_ProgressBar.Dispatcher.Invoke<double>((Func<double>)(() => this.apartsnumProgressBar.TNov_ProgressBar.Minimum = (double)PBCount));
                this.apartsnumProgressBar.TNov_ProgressBar.Dispatcher.Invoke<string>((Func<string>)(() => this.apartsnumProgressBar.value.Text = "Квартира " + PBCount.ToString()));
                this.apartsnumProgressBar.TNov_ProgressBar.Dispatcher.Invoke<double>((Func<double>)(() => this.apartsnumProgressBar.TNov_ProgressBar.Maximum = (double)roomsToSet.Count()));
                this.apartsnumProgressBar.TNov_ProgressBar.Dispatcher.Invoke<string>((Func<string>)(() => this.apartsnumProgressBar.maxvalue.Text = roomsToSet.Count().ToString()));

                int i = 0;
                transaction.Start("TNov - Сквозные номера квартир");
                Logger.Log( "Открываем транзакцию",1);

                foreach (Room room in roomsToSet)
                {
                    try
                    {
                        room.LookupParameter(apartNumber)?.Set(values[i].ToString());
                        i++;
                        PBCount++;
                        Logger.Log("   Помещение " + room.Id + " успешно", 2);
                    }
                    catch (Exception ex) { Logger.Log("   Помещение " + room.Id + " ошибка: " + ex.Message, 4); }
                    this.apartsnumProgressBar.TNov_ProgressBar.Dispatcher.Invoke<double>((Func<double>)(() => this.apartsnumProgressBar.TNov_ProgressBar.Value = (double)PBCount));
                    this.apartsnumProgressBar.TNov_ProgressBar.Dispatcher.Invoke<string>((Func<string>)(() => this.apartsnumProgressBar.value.Text = "Квартира "+PBCount.ToString()));
                    
                }

                //var info1 = new infowindow280("Успешно!\nСквозные номера квартир заполнены."); info1.ShowDialog();
                
                transaction.Commit();
                this.apartsnumProgressBar.Dispatcher.Invoke((System.Action)(() => this.apartsnumProgressBar.Close()));
                Logger.Log( "Закрываем транзакцию",1);
            }
            Logger.Log( "Завершение работы.",5);
            return Result.Succeeded;
        }
    }
}
