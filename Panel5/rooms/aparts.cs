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
using Microsoft.Office.Interop.Excel;
using Autodesk.Revit.UI.Selection;
using TNov.main;
using System.Xml.Linq;

namespace TNov
{
    


    [Transaction(TransactionMode.Manual)]
    public class aparts : IExternalCommand
    {
        private TNovProgressBar apartsProgressBar;
        private void ThreadStartingPoint()
        {
            this.apartsProgressBar = new TNovProgressBar();
            this.apartsProgressBar.Show();
            Dispatcher.Run();
        }
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            string TNovClassName = "Квартирография"; DateTime dateTime = DateTime.Now;
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
                if (qok != null && qok == true) { Logger.TurnOffExtendedLogs(); } else Logger.Log( "Расширенные логи вкл", 2);
            }

            //Список используемых параметров

            string N_Par_sq = "N_Площадь.Округленная";
            if (oldProject == true) { N_Par_sq = "Площадь.Округленная"; }
            string N_Par_sqk = "N_Площадь.ОкруглСКоэффициентом";
            if (oldProject == true) { N_Par_sqk = "Площадь.ОкруглСКоэффициентом"; }
            string N_Par_apartment = "N_Квартира";
            if (oldProject == true) { N_Par_apartment = "квартира"; }
            string N_Par_apartnum = "N_Кв.Номер";
            if (oldProject == true) { N_Par_apartnum = "квартира.номер"; }
            string N_Par_livingroom = "N_Кв.Комната.Жилая";
            if (oldProject == true) { N_Par_livingroom = "комната.жилая"; }
            string N_Par_apsqo = "N_Кв.Площадь.Общая";
            if (oldProject == true) { N_Par_apsqo = "квартира.площадь.общая"; }
            string N_Par_apsqok = "N_Кв.Площадь.ОбщаяСКоэффициентом";
            if (oldProject == true) { N_Par_apsqok = "Квартира.Площадь.ОбщаяСКоэффициентом"; }
            string N_Par_apsq = "N_Кв.Площадь";
            if (oldProject == true) { N_Par_apsq = "Квартира.Площадь"; }
            string N_Par_apsqb = "N_Кв.Площадь.Балконы";
            if (oldProject == true) { N_Par_apsqb = "Квартира.Площадь.Балконы"; }
            string N_Par_apsqbk = "N_Кв.Площадь.БалконыСКоэффициентом";
            if (oldProject == true) { N_Par_apsqbk = "Квартира.Площадь.БалконыСКоэффициентом"; }
            string N_Par_apsqliv = "N_Кв.Площадь.Жилая";
            if (oldProject == true) { N_Par_apsqliv = "квартира.площадь.жилая"; }
            string N_Par_aprn = "N_Кв.Комнаты.Количество";
            if (oldProject == true) { N_Par_aprn = "Квартира.Комнаты.Количество"; }
            string N_Par_roomToSpec = "Поквартир.Сетка";

            Logger.Log( "Сбор элементов",1);

            List<Room> rooms = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_Rooms)   //фильтр по категории Помещения
                                                                         .WhereElementIsNotElementType()    //фильтр только экземпляры
                                                                         .Cast<Room>()                     //элементы категории Помещения
                                                                         .ToList();                         //формируем список
            List<Room> arooms = new List<Room>();

            Logger.Log( "Ищем неразмещенные помещения",1);
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

            Logger.Log( "Ищем квартиры",1);
            int ap = 0; //счетчик количества помещений с включенным параметром N_Квартира

            foreach (Room room in rooms) //проверка наличия квартир
            {
                int apart = room.LookupParameter(N_Par_apartment).AsInteger();
                if (apart == 1) { ap++; arooms.Add(room); }
            }

            if (ap == 0) //если нет квартир - прерываем процесс
            {
                new infowindow280("В проекте отсутствуют помещения с включенным параметром " + 
                    N_Par_apartment + ". Заполните его в спецификации.").ShowDialog();
                Logger.Log( "Квартиры отсутствуют. Завершение работы.",3);
                string commandText = @"https://portal.talan.group/knowledge/proektirovanie/kvartirografiya/";
                var proc = new System.Diagnostics.Process();
                proc.StartInfo.FileName = commandText;
                proc.StartInfo.UseShellExecute = true;
                proc.Start();
                return Result.Failed;
            }

            // Диалоговое окно
            Logger.Log( "Диалоговое окно",1);
            var viewModel = new officesViewModel();
            // Десериализация
            bool forProject = true;
            json js = new json("Офисография", in forProject, out bool canserialize, out string jsonpath);
            if (canserialize)
            {
                viewModel = JsonConvert.DeserializeObject<officesViewModel>(File.ReadAllText(jsonpath));
                Logger.Log( "Десериализация прошла успешно",1);
            }
            var wpfview = new officeswpf(viewModel);
            viewModel.CloseRequest += (s, e) => wpfview.Close();
            bool? ok = wpfview.ShowDialog();
            if (ok != null && ok == true) { } else { Logger.Log("Запуск отменен пользователем. Завершение работы.", 3); return Result.Cancelled; }
            //Сериализация
            try
            {
                File.WriteAllText(jsonpath, JsonConvert.SerializeObject(viewModel));
                Logger.Log( "Сериализация прошла успешно",1);
            }
            catch (Exception ex) { Logger.Log( "Ошибка при сериализации: " + ex.Message,4); }

            string names1 = viewModel.k05; string names2 = viewModel.k03; bool recalc = viewModel.recalc;

            //получаем имена помещений, удаляем возможные пробелы в начале и конце имен
            string[] n1 = names1.Split(','); for (int i = 0; i < n1.Length; i++) { n1[i] = n1[i].Trim(); }
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

            //обработка сценария "одно помещение + его квартира"
            if (viewModel.selection == 2)
            {
                List<Room> newARooms = new List<Room>();
                foreach (Room room in rooms1) //проверка что помещение принадлежит квартире
                {
                    int apart = room.LookupParameter(N_Par_apartment).AsInteger();
                    if (apart == 1)
                    {
                        string apartNum = room.LookupParameter(N_Par_apartnum).AsValueString();
                        foreach (var aroom in arooms)
                        {
                            string apartNum1 = aroom.LookupParameter(N_Par_apartnum).AsValueString();
                            if (apartNum1 == apartNum) newARooms.Add(aroom);
                        }
                        arooms = newARooms;
                    }
                    else return Result.Succeeded; //выбранное помещение оказалось не квартирным
                }
            }

            //Округлятор (только квартиры)
            if (recalc) //если активна галочка Перерасчета - запускаем транзакцию
            {
                using (Transaction transaction = new Transaction(doc))
                {
                    transaction.Start("TNov - Округлятор");
                    Logger.Log( "Открываем транзакцию 1 (округлятор)",1);
                    foreach (Room room in arooms) 
                    {
                        double area = room.get_Parameter(BuiltInParameter.ROOM_AREA).AsDouble() * 0.3048 * 0.3048;
                        double areaR = Math.Round(area, 1);
                        string name = room.Name;
                        double k = 1;
                        foreach (string n in n1) { if (name.Contains(n)) { k = 0.5; } }
                        foreach (string n in n2) { if (name.Contains(n)) { k = 0.3; } }
                        double areaRK = Math.Round((areaR * k + 0.000001), 1);
                        room.LookupParameter(N_Par_sq)?.Set(areaR);
                        room.LookupParameter(N_Par_sqk)?.Set(areaRK);
                        Logger.Log( "   Помещение " + room.Id + " : успешно",2);
                    }

                    transaction.Commit(); Logger.Log( "Закрываем транзакцию 1",1);
                }
            }

            //Проверка заполненности сквозных номеров квартир
            Logger.Log( "Квартирография. Проверяем заполненность Кв.Номер",1);

            int failedrooms = 0;

            foreach (Room aroom in arooms)
            {
                string apart = aroom.LookupParameter(N_Par_apartnum).AsValueString();
                if (apart == "") { failedrooms++; }
            }

            if (failedrooms > 0) //если у некоторых помещений квартир не заполнен параметр N_Кв.Номер - прерываем процесс
            {
                new infowindow280("В проекте присутствуют помещения квартир с незаполненным параметром " + 
                    N_Par_apartnum + ". Запустите Нумератор квартир.").ShowDialog();
                Logger.Log( "Не у всех помещений с галочкой Квартира заполнен параметр Кв.Номер. Завершение работы.",3);
                string commandText = @"https://portal.talan.group/knowledge/proektirovanie/kvartirografiya/";
                var proc = new System.Diagnostics.Process();
                proc.StartInfo.FileName = commandText;
                proc.StartInfo.UseShellExecute = true;
                proc.Start();
                return Result.Failed;
            }

            //Квартирография
            var aroomssortbynum = from aroom in arooms //сортированный список помещений по номеру квартиры
                              orderby aroom.LookupParameter(N_Par_apartnum).AsValueString()
                                select aroom;

            var aparts = from aroom in aroomssortbynum //список квартир
                         group aroom by aroom.LookupParameter(N_Par_apartnum).AsValueString();

            int apartsCount = aparts.Count();

            using (Transaction transaction2 = new Transaction(doc))
            {
                transaction2.Start("TNov - Квартирография");
                Logger.Log( "Открываем транзакцию 2 (квартирография)",1);

                Thread thread = new Thread(new ThreadStart(this.ThreadStartingPoint));
                thread.SetApartmentState(ApartmentState.STA);
                thread.IsBackground = true;
                thread.Start();
                Thread.Sleep(100);

                int PBCount = 0;
                this.apartsProgressBar.TNov_ProgressBar.Dispatcher.Invoke<double>((Func<double>)(() => this.apartsProgressBar.TNov_ProgressBar.Minimum = (double)PBCount));
                this.apartsProgressBar.TNov_ProgressBar.Dispatcher.Invoke<string>((Func<string>)(() => this.apartsProgressBar.value.Text = PBCount.ToString()));
                this.apartsProgressBar.TNov_ProgressBar.Dispatcher.Invoke<double>((Func<double>)(() => this.apartsProgressBar.TNov_ProgressBar.Maximum = (double)apartsCount));
                this.apartsProgressBar.TNov_ProgressBar.Dispatcher.Invoke<string>((Func<string>)(() => this.apartsProgressBar.maxvalue.Text = apartsCount.ToString()));

                foreach (var apart in aparts) //проходим по каждой квартире в списке квартир
                {
                    Logger.Log("Квартира " + apart.First().LookupParameter(N_Par_apartnum).AsValueString(), 2);

                    double apsqo = 0; //объявляем переменную для заполнения значения параметра N_Кв.Площадь.Общая
                    double apsqok = 0; //N_Кв.Площадь.ОбщаяСКоэффициентом
                    double apsq = 0; //N_Кв.Площадь
                    double apsqb = 0; //N_Кв.Площадь.Балконы
                    double apsqbk = 0; //N_Кв.Площадь.БалконыСКоэффициентом
                    double apsqliv = 0; //N_Кв.Площадь.Жилая
                    int aprn = 0; //N_Кв.Комнаты.Количество
                    int specCount = 0; //Поквартир.Сетка

                    foreach (var aroom in apart) //проходим по каждой комнате в квартире
                    {
                        double sqNonConvert = aroom.LookupParameter(N_Par_sq).AsDouble();
                        double sq = aroom.LookupParameter(N_Par_sq).AsDouble() / 0.3048 / 0.3048; //объявляем переменную, получаем площадь каждого помещения в квартире
                        Logger.Log("   Помещение " + aroom.Id.ToString() + " имя: " + aroom.Name + " площадь:" + sqNonConvert.ToString(), 2);

                        string name = aroom.Name; 
                        double sqsq = sq; double sqb = 0; double sqbk = 0; double sqliv = 0;

                        apsqo = apsqo + sq; //добавляем значение площади помещения к общей площади квартиры

                        double sqk = aroom.LookupParameter(N_Par_sqk).AsDouble() / 0.3048 / 0.3048; //получаем площадь с коэфф каждого помещения в квартире
                        apsqok = apsqok + sqk; //общая с коэфф

                        foreach (string n in n1) { if (name.Contains(n)) { sqsq = 0; break; } }
                        foreach (string n in n2) { if (name.Contains(n)) { sqsq = 0; break; } }
                        apsq = apsq + sqsq; //кв.площадь

                        foreach (string n in n1) { if (name.Contains(n)) { sqb = sq; break; } }
                        foreach (string n in n2) { if (name.Contains(n)) { sqb = sq; break; } }
                        apsqb = apsqb + sqb; //балконы

                        foreach (string n in n1) { if (name.Contains(n)) { sqbk = sqk; break; } }
                        foreach (string n in n2) { if (name.Contains(n)) { sqbk = sqk; break; } }
                        apsqbk = apsqbk + sqbk; //балк с коэфф

                        int livingroom = aroom.LookupParameter(N_Par_livingroom).AsInteger();
                        if (livingroom == 1) 
                        { 
                            sqliv = sq;
                            aprn++; //кол-во комнат
                        }
                        apsqliv = apsqliv + sqliv; //жилая
                    }

                    //N_Кв.Площадь.Общая
                    foreach (var aroom in apart)
                    {
                        try
                        {
                            aroom.LookupParameter(N_Par_apsqo).Set(apsqo);
                        }
                        catch (Exception ex)
                        {
                            Logger.Log("   Комната " + aroom.Id.ToString() + " Параметр "
                                + N_Par_apsqo + " ошибка: " + ex.Message, 4);
                        }
                    }

                    //N_Кв.Площадь.ОбщаяСКоэффициентом
                    foreach (var aroom in apart)
                    {
                        try
                        {
                            aroom.LookupParameter(N_Par_apsqok).Set(apsqok);
                        }
                        catch (Exception ex)
                        {
                            Logger.Log("   Комната " + aroom.Id.ToString() + " Параметр "
                                + N_Par_apsqok + " ошибка: " + ex.Message, 4);
                        }
                    }

                    //N_Кв.Площадь
                    foreach (var aroom in apart)
                    {
                        try
                        {
                            aroom.LookupParameter(N_Par_apsq).Set(apsq);
                        }
                        catch (Exception ex)
                        {
                            Logger.Log("   Комната " + aroom.Id.ToString() + " Параметр "
                                + N_Par_apsq + " ошибка: " + ex.Message, 4);
                        }
                    }

                    //N_Кв.Площадь.Балконы
                    foreach (var aroom in apart)
                    {
                        try
                        {
                            aroom.LookupParameter(N_Par_apsqb).Set(apsqb);
                        }
                        catch (Exception ex)
                        {
                            Logger.Log("   Комната " + aroom.Id.ToString() + " Параметр "
                                + N_Par_apsqb + " ошибка: " + ex.Message, 4);
                        }
                    }

                    //N_Кв.Площадь.БалконыСКоэффициентом
                    foreach (var aroom in apart)
                    {
                        try
                        {
                            aroom.LookupParameter(N_Par_apsqbk).Set(apsqbk);
                        }
                        catch (Exception ex)
                        {
                            Logger.Log("   Комната " + aroom.Id.ToString() + " Параметр "
                                + N_Par_apsqbk + " ошибка: " + ex.Message, 4);
                        }
                    }

                    //N_Кв.Площадь.Жилая
                    foreach (var aroom in apart)
                    {
                        try
                        {
                            aroom.LookupParameter(N_Par_apsqliv).Set(apsqliv);
                        }
                        catch (Exception ex)
                        {
                            Logger.Log("   Комната " + aroom.Id.ToString() + " Параметр "
                                + N_Par_apsqliv + " ошибка: " + ex.Message, 4);
                        }
                    }

                    //N_Кв.Комнаты.Количество
                    foreach (var aroom in apart)
                    {
                        try
                        {
                            aroom.LookupParameter(N_Par_aprn).Set(aprn.ToString());
                        }
                        catch (Exception ex)
                        {
                            Logger.Log("   Комната " + aroom.Id.ToString() + " Параметр "
                                + N_Par_aprn + " ошибка: " + ex.Message, 4);
                        }
                    }

                    //Поквартир.Сетка
                    foreach (var aroom in apart)
                    {
                        if (specCount == 0) aroom.LookupParameter(N_Par_roomToSpec).Set(1); //назначаем Поквартир.Сетка только первому помещению в кв
                        else aroom.LookupParameter(N_Par_roomToSpec).Set(0);
                        specCount++;
                    }

                    //Прогресс-бар: +1
                    PBCount++;
                    this.apartsProgressBar.TNov_ProgressBar.Dispatcher.Invoke<double>((Func<double>)(() => this.apartsProgressBar.TNov_ProgressBar.Value = (double)PBCount));
                    this.apartsProgressBar.TNov_ProgressBar.Dispatcher.Invoke<string>((Func<string>)(() => this.apartsProgressBar.value.Text = "Квартиры " + PBCount.ToString()));

                }
                

                transaction2.Commit();
                this.apartsProgressBar.Dispatcher.Invoke((System.Action)(() => this.apartsProgressBar.Close()));
                Logger.Log( "Закрываем транзакцию 2",1);
            }
            Logger.Log( "Завершение работы.",5);
            return Result.Succeeded;
        }
    }
}
