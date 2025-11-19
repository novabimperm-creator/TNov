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
using System.Windows.Input;
using Autodesk.Revit.UI.Selection;
using TNov.main;

namespace TNov
{
    public class officesViewModel : INotifyPropertyChanged
    {
        public int selection { get; set; }

        private ICommand _scenario1;
        public ICommand scenario1
        {
            get
            {
                if (_scenario1 == null)
                {
                    _scenario1 = new RelayCommand(param => { selection = 1; }, CanExecute);
                }
                return _scenario1;
            }
        }
        private ICommand _scenario2;
        public ICommand scenario2
        {
            get
            {
                if (_scenario2 == null)
                {
                    _scenario2 = new RelayCommand(param => { selection = 2; }, CanExecute);
                }
                return _scenario2;
            }
        }
        private bool _recalc = true;
        public bool recalc { get => _recalc; set { _recalc = value; OnPropertyChanged(); } }

        private string _k03 = "Балкон,Французский балкон,Терраса";
        public string k03
        {
            get => _k03;
            set
            {
                _k03 = value;
                OnPropertyChanged();
            }
        }
        private string _k05 = "Лоджия";
        public string k05
        {
            get => _k05;
            set
            {
                _k05 = value;
                OnPropertyChanged();
            }
        }

        private string _names1 = "Лестница,лестница,Лестничная клетка,лестничная клетка";
        public string names1
        {
            get => _names1;
            set
            {
                _names1 = value;
                OnPropertyChanged();
            }
        }
        private string _names2 = "Коридор,Тамбур,Холл,Электрощитовая,Венткамера,Терраса";
        public string names2
        {
            get => _names2;
            set
            {
                _names2 = value;
                OnPropertyChanged();
            }
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
        private bool CanExecute(object param)
        {
            return true;
        }
    }


    [Transaction(TransactionMode.Manual)]
    public class offices : IExternalCommand
    {
        private TNovProgressBar officesProgressBar;
        private void ThreadStartingPoint()
        {
            this.officesProgressBar = new TNovProgressBar();
            this.officesProgressBar.Show();
            Dispatcher.Run();
        }
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            string TNovClassName = "Офисография"; DateTime dateTime = DateTime.Now;
            //подключение приложения и документа
            if (RevitAPI.UiApplication == null) { RevitAPI.Initialize(commandData); }
            UIDocument uidoc = RevitAPI.UiDocument; Document doc = RevitAPI.Document;
            UIApplication uiApp = RevitAPI.UiApplication; Autodesk.Revit.ApplicationServices.Application rvtApp = uiApp.Application;
            
            //проверка подключения, запись в журнал
            bool check = false; servercheck sc = new servercheck(in TNovClassName, out check); if (check == false) { return Result.Failed; }

            // создание log - файла
            Logger.Initialize(TNovClassName);
            

            //Проверка актуальности шаблона
            templatecheck tc = new templatecheck(in commandData, out bool oldProject);

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
                if (qok != null && qok == true) { Logger.TurnOffExtendedLogs(); } else Logger.Log("Расширенные логи вкл",2);
            }

            //Список используемых параметров

            string N_Par_sq = "N_Площадь.Округленная";
            if (oldProject == true) { N_Par_sq = "Площадь.Округленная"; }
            string N_Par_sqk = "N_Площадь.ОкруглСКоэффициентом";
            if (oldProject == true) { N_Par_sqk = "Площадь.ОкруглСКоэффициентом"; }
            string N_Par_offnum = "N_Офис.Номер";
            if (oldProject == true) { N_Par_offnum = "Офис.Номер"; }
            string N_Par_offsqo = "N_Офис.Площадь.Общая";
            if (oldProject == true) { N_Par_offsqo = "Офис.Площадь.Общая"; }
            string N_Par_offsqp = "N_Офис.Площадь.Полезная";
            if (oldProject == true) { N_Par_offsqp = "Офис.Площадь.Полезная"; }
            string N_Par_offsqr = "N_Офис.Площадь.Расчетная";
            if (oldProject == true) { N_Par_offsqr = "Офис.Площадь.Расчетная"; }

            Logger.Log("Сбор элементов",1);
            List<Room> rooms = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_Rooms)   //фильтр по категории Помещения
                                                                         .WhereElementIsNotElementType()    //фильтр только экземпляры
                                                                         .Cast<Room>()                     //элементы категории Помещения
                                                                         .ToList();                         //формируем список
            List<Room> orooms = new List<Room>();

            Logger.Log("Ищем неразмещенные помещения",1);
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
                Logger.Log("В проекте присутствуют неразмещенные или избыточные помещения в количестве " +ec + " шт. Завершение работы",3);
                string commandText = @"https://portal.talan.group/knowledge/proektirovanie/ofisografiya/";
                var proc = new System.Diagnostics.Process();
                proc.StartInfo.FileName = commandText;
                proc.StartInfo.UseShellExecute = true;
                proc.Start();
                return Result.Failed;
            }

            Logger.Log("Ищем офисы",1);
            int officescount = 0; //счетчик количества помещений с заполненным параметром N_Офис.Номер
            
            foreach (Room room in rooms) //проверка наличия офисов
            {
                Parameter offnumParam = room.LookupParameter(N_Par_offnum);
                if (offnumParam!=null&&offnumParam.HasValue)
                {
                    string offNumValue = offnumParam.AsString();
                    bool isOffice = Double.TryParse(offNumValue, out double num);
                    if (isOffice || offnumParam.AsString().Length > 0) { officescount++; orooms.Add(room); }
                }  
            }
            
            if (officescount == 0) //если нет офисов - прерываем процесс
            {
                new infowindow280("В проекте отсутствуют помещения с включенным параметром " + 
                    N_Par_offnum + ". Заполните его в спецификации.").ShowDialog();
                Logger.Log("Офисы отсутствуют. Завершение работы.", 3);
                string commandText = @"https://portal.talan.group/knowledge/proektirovanie/ofisografiya/";
                var proc = new System.Diagnostics.Process();
                proc.StartInfo.FileName = commandText;
                proc.StartInfo.UseShellExecute = true;
                proc.Start();
                return Result.Failed;
            }

            // Диалоговое окно

            Logger.Log("Диалоговое окно",1);
            var viewModel = new officesViewModel();
            // Десериализация
            bool forProject = true;
            json js = new json(in TNovClassName, in forProject, out bool canserialize, out string jsonpath);
            if (canserialize)
            {
                viewModel = JsonConvert.DeserializeObject<officesViewModel>(File.ReadAllText(jsonpath));
                Logger.Log("Десериализация прошла успешно",1);
            }
            var wpfview = new officeswpf(viewModel);
            viewModel.CloseRequest += (s, e) => wpfview.Close();
            bool? ok = wpfview.ShowDialog();
            if (ok != null && ok == true) { } else { Logger.Log("Запуск отменен пользователем. Завершение работы.", 3); return Result.Cancelled; }
            //Сериализация
            try
            {
                File.WriteAllText(jsonpath, JsonConvert.SerializeObject(viewModel));
                Logger.Log("Сериализация прошла успешно",1);
            }
            catch (Exception ex) { Logger.Log("Ошибка при сериализации: " + ex.Message,4); }

            string names1 = viewModel.names1; string names2 = viewModel.names2;
            string names3 = viewModel.k05; string names4 = viewModel.k03; bool recalc = viewModel.recalc;

            //получаем имена помещений, удаляем возможные пробелы в начале и конце имен
            string[] n1 = names1.Split(','); for (int i = 0; i < n1.Length; i++) { n1[i] = n1[i].Trim(); }
            string[] n2 = names2.Split(','); for (int i = 0; i < n2.Length; i++) { n2[i] = n2[i].Trim(); }
            string[] n3 = names3.Split(','); for (int i = 0; i < n3.Length; i++) { n3[i] = n3[i].Trim(); }
            string[] n4 = names4.Split(','); for (int i = 0; i < n4.Length; i++) { n4[i] = n4[i].Trim(); }

            //Выбор элементов

            List<Element> rooms1 = new List<Element>();

            Logger.Log("Финальный список помещений", 1);

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
                    Logger.Log("Ошибка: " + e.Message, 4);
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

            //обработка сценария "одно помещение + его офис"
            if (viewModel.selection == 2)
            {
                Logger.Log("Обработка выделенного помещения", 1);
                List<Room> newORooms = new List<Room>();
                foreach (Room room in rooms1) //проверка что помещение принадлежит офису
                {
                    bool isOfficeRoom = false;

                    Parameter offnumParam = room.LookupParameter(N_Par_offnum);
                    if (offnumParam != null && offnumParam.HasValue)
                    {
                        string offNumValue = offnumParam.AsString();
                        bool isOffice = Double.TryParse(offNumValue, out double num);
                        if (isOffice || offnumParam.AsString().Length > 0) isOfficeRoom = true;
                    }

                    if (isOfficeRoom)
                    {
                        string officeNum = room.LookupParameter(N_Par_offnum).AsValueString();
                        foreach (var oroom in orooms)
                        {
                            string officeNum1 = oroom.LookupParameter(N_Par_offnum).AsValueString();
                            if (officeNum1 == officeNum)
                            {
                                newORooms.Add(oroom);
                                Logger.Log("   Помещение " + oroom.Id + " добавлено в список на обработку", 2);
                            }
                        }
                        orooms = newORooms;
                    }
                    else
                    {
                        Logger.Log("Помещение - не офисное. Завершение работы.", 3);
                        return Result.Succeeded; //выбранное помещение оказалось не офисным
                    }
                }
            }

            //Округлятор (только офисы)
            if (recalc) //если активна галочка Перерасчета - запускаем транзакцию
            {
                using (Transaction transaction = new Transaction(doc))
                {
                    transaction.Start("TNov - Округлятор");
                    Logger.Log("Открываем транзакцию 1 (округлятор)",1);
                    foreach (Room room in orooms) 
                    {
                        double area = room.get_Parameter(BuiltInParameter.ROOM_AREA).AsDouble() * 0.3048 * 0.3048;
                        double areaR = Math.Round(area, 1);
                        string name = room.Name;
                        double k = 1;
                        foreach (string n in n3) { if (name.Contains(n)) { k = 0.5; } }
                        foreach (string n in n4) { if (name.Contains(n)) { k = 0.3; } }
                        double areaRK = Math.Round((areaR * k + 0.000001), 1);
                        room.LookupParameter(N_Par_sq)?.Set(areaR);
                        room.LookupParameter(N_Par_sqk)?.Set(areaRK);
                        Logger.Log("   Помещение " + room.Id + " : успешно",2);
                    }

                    transaction.Commit(); Logger.Log("Закрываем транзакцию 1", 1);
                }
            }

            

            //Офисография

            var oroomssortbynum = from oroom in orooms //сортированный список помещений по номеру офиса
                              orderby oroom.LookupParameter(N_Par_offnum).AsValueString()
                                select oroom;

            var offices = from oroom in oroomssortbynum //список офисов
                         group oroom by oroom.LookupParameter(N_Par_offnum).AsValueString();

            int officesCount = offices.Count();

            using (Transaction transaction2 = new Transaction(doc))
            {
                transaction2.Start("TNov - Офисография");
                Logger.Log("Открываем транзакцию 2 (офисография)",1);

                Thread thread = new Thread(new ThreadStart(this.ThreadStartingPoint));
                thread.SetApartmentState(ApartmentState.STA);
                thread.IsBackground = true;
                thread.Start();
                Thread.Sleep(100);

                int PBCount = 0;
                this.officesProgressBar.TNov_ProgressBar.Dispatcher.Invoke<double>((Func<double>)(() => this.officesProgressBar.TNov_ProgressBar.Minimum = (double)PBCount));
                this.officesProgressBar.TNov_ProgressBar.Dispatcher.Invoke<string>((Func<string>)(() => this.officesProgressBar.value.Text = PBCount.ToString()));
                this.officesProgressBar.TNov_ProgressBar.Dispatcher.Invoke<double>((Func<double>)(() => this.officesProgressBar.TNov_ProgressBar.Maximum = (double)officesCount));
                this.officesProgressBar.TNov_ProgressBar.Dispatcher.Invoke<string>((Func<string>)(() => this.officesProgressBar.maxvalue.Text = officesCount.ToString()));

                foreach (var office in offices) //проходим по каждому офису в списке офисов
                {
                    Logger.Log("Офис "+office.First().LookupParameter(N_Par_offnum).AsValueString(),2);
                    
                    double offsqo = 0; //объявляем переменную для заполнения значения параметра N_Офис.Площадь.Общая
                    double offsqp = 0; //N_Офис.Площадь.Полезная
                    double offsqr = 0; //N_Офис.Площадь.Расчетная
                    foreach (var oroom in office) //проходим по каждой комнате в офисе
                    {
                        double sqNonConvert = oroom.LookupParameter(N_Par_sq).AsDouble();
                        double sq = oroom.LookupParameter(N_Par_sq).AsDouble() / 0.3048 / 0.3048; //объявляем переменную, получаем площадь каждого помещения в офисе
                        Logger.Log("   Помещение " + oroom.Id.ToString()+" имя: "+oroom.Name+" площадь:"+ sqNonConvert.ToString(), 2);
                        
                        offsqo += sq; //добавляем значение площади помещения к общей площади офиса

                        double sqp = sq; double sqr = sq; string name = oroom.Name;

                        foreach (string n in n1) { if (name.Contains(n)) { sqp = 0; sqr = 0; break; } }
                        offsqp += sqp; //полезная
 
                        foreach (string n in n2) { if (name.Contains(n)) { sqr = 0; break; } }
                        offsqr += sqr; //расчетная
                    }
                    //Общая площадь
                    foreach (var oroom in office) //проходим по каждой комнате в офисе
                    {
                        try
                        {
                            oroom.LookupParameter(N_Par_offsqo).Set(offsqo); //назначаем параметр каждому помещению в офисе
                        }
                        catch (Exception ex)
                        {
                            Logger.Log("   Комната " + oroom.Id.ToString() + " Параметр "
                                + N_Par_offsqo + " ошибка: " + ex.Message,4);
                        }
                    }
                    //Полезная площадь
                    foreach (var oroom in office) 
                    {
                        try
                        {
                            oroom.LookupParameter(N_Par_offsqp).Set(offsqp); 
                        }
                        catch (Exception ex)
                        {
                            Logger.Log("   Комната " + oroom.Id.ToString() + " Параметр "
                                + N_Par_offsqp + " ошибка: " + ex.Message, 4);
                        }
                    }
                    //Расчетная площадь
                    foreach (var oroom in office)
                    {
                        try
                        {
                            oroom.LookupParameter(N_Par_offsqr).Set(offsqr);
                        }
                        catch (Exception ex)
                        {
                            Logger.Log("   Комната " + oroom.Id.ToString() + " Параметр "
                                + N_Par_offsqr + " ошибка: " + ex.Message, 4);
                        }
                    }
                    //Прогресс-бар: +1
                    PBCount++;
                    this.officesProgressBar.TNov_ProgressBar.Dispatcher.Invoke<double>((Func<double>)(() => this.officesProgressBar.TNov_ProgressBar.Value = (double)PBCount));
                    this.officesProgressBar.TNov_ProgressBar.Dispatcher.Invoke<string>((Func<string>)(() => this.officesProgressBar.value.Text = "Офисы " + PBCount.ToString()));

                }

                transaction2.Commit();
                this.officesProgressBar.Dispatcher.Invoke((System.Action)(() => this.officesProgressBar.Close()));
                Logger.Log("Закрываем транзакцию 2",1);

            }
            Logger.Log("Завершение работы.",5);
            return Result.Succeeded;
        }
    }
}
