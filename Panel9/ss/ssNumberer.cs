using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Electrical;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using Newtonsoft.Json;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Documents;
using TNov.main;

namespace TNov
{
    public class SSSelectionFilter : ISelectionFilter
    {
        public bool AllowElement(Element element)
        {
            bool result = false;
            switch (element.Category.Name)
            {
                case "Пожарная сигнализация": result = true; break;
                case "Электрооборудование": result = true; break;
                case "Устройства вызова и оповещения": result = true; break;
                case "Устройства связи": result = true; break;
            }
            return result;
        }

        public bool AllowReference(Reference refer, XYZ point)
        {
            return false;
        }
    }
    public class ssNumbererViewModel : INotifyPropertyChanged
    {
        Guid adskPparamGuid = new Guid("ae8ff999-1f22-4ed7-ad33-61503d85f0f4");//ADSK_Позиция
        private string _startvalue = "1"; 
        public string startvalue { get => _startvalue; set { _startvalue = value; OnPropertyChanged(); } } //можно обновить из окна
        private string _circuitvalue = "1/1";
        public string circuitvalue { get => _circuitvalue; set { _circuitvalue = value; OnPropertyChanged(); } }
        private string _circuitsection = "";
        public string circuitsection { get => _circuitsection; set { _circuitsection = value; OnPropertyChanged(); } }

        public RelayCommand NumerateCommand { get; set; }
        public RelayCommand GetLastNumberCommand { get; set; }
        public ssNumbererViewModel()
        {
            NumerateCommand = new RelayCommand(param => { Numerate(); }, CanNumerate); //команда метода нумерации
            GetLastNumberCommand = new RelayCommand(param => { GetLastNumber(); }, CanGetLastNumber); //команда подгрузки последнего номера в цепи-выходе
        }
        public void Numerate() //метод нумерации
        {
            RaiseHideRequest();
            int i = 1;
            int.TryParse(startvalue, out i);
            string prefix = circuitvalue + ".";
            if (circuitsection != "") prefix = prefix + circuitsection + ".";
            if (i > 200)
            {
                new infowindow280($"Ошибка!\nПревышено максимальное количество элементов в цепи (200).").ShowDialog();
            }
            using (TransactionGroup group = new TransactionGroup(RevitAPI.Document, "TNov - Адресатор"))
            {
                ISelectionFilter _filter = new SSSelectionFilter();
                group.Start();

                while (i<201)
                {
                    try
                    {
                        using (Transaction t = new Transaction(RevitAPI.Document, "TNov - Адресатор"))
                        {
                            t.Start();
                            TransactionHandler.SetWarningResolver(t);
                            Reference reference = RevitAPI.UiDocument.Selection.PickObject(ObjectType.Element, _filter, $"Выберите элемент {i}");
                            Element elem = RevitAPI.Document.GetElement(reference); Element type = RevitAPI.Document.GetElement(elem.GetTypeId());
                            string prefix1 = prefix;
                            //параметр-префикс
                            if (param.ParamExistByGuid(adskPparamGuid, elem))
                                prefix1 = elem.get_Parameter(adskPparamGuid).AsString() + prefix1;
                            else if (param.ParamExistByGuid(adskPparamGuid, type))
                                prefix1 = type.get_Parameter(adskPparamGuid).AsString() + prefix1;
                            //целевой параметр
                            Autodesk.Revit.DB.Parameter parameter = elem.get_Parameter(BuiltInParameter.DOOR_NUMBER);
                            if (parameter != null)
                            {
                                parameter.Set(prefix1+i.ToString());
                                i++;
                                t.Commit();
                            }
                            else
                            {
                                t.Commit();
                                group.Assimilate();
                                break;
                            }
                        }
                    }
                    catch
                    {
                        group.Assimilate();
                        break;
                    }
                }
            }
            startvalue = i.ToString();
            RaiseShowRequest();
        }
        public void GetLastNumber() //метод получения последнего номера в цепи-выходе
        {
            //получаем элементы
            List<FamilyInstance> FIs = new List<FamilyInstance>();
            List<FamilyInstance> elEq = new FilteredElementCollector(RevitAPI.Document).OfCategory(BuiltInCategory.OST_ElectricalEquipment)
                .WhereElementIsNotElementType().Cast<FamilyInstance>().ToList(); foreach (FamilyInstance el in elEq) FIs.Add(el);
            List<FamilyInstance> FireAlarmDevices = new FilteredElementCollector(RevitAPI.Document).OfCategory(BuiltInCategory.OST_FireAlarmDevices)
                .WhereElementIsNotElementType().Cast<FamilyInstance>().ToList(); foreach (FamilyInstance fad in FireAlarmDevices) FIs.Add(fad);
            List<FamilyInstance> AlertDevices = new FilteredElementCollector(RevitAPI.Document).OfCategory(BuiltInCategory.OST_NurseCallDevices)
                .WhereElementIsNotElementType().Cast<FamilyInstance>().ToList(); foreach (FamilyInstance ad in AlertDevices) FIs.Add(ad);
            List<FamilyInstance> CommDevices = new FilteredElementCollector(RevitAPI.Document).OfCategory(BuiltInCategory.OST_CommunicationDevices)
                .WhereElementIsNotElementType().Cast<FamilyInstance>().ToList(); foreach (FamilyInstance cd in CommDevices) FIs.Add(cd);
            //ищем элементы цепи
            string curcuit = circuitvalue; if (circuitsection != "") curcuit = circuitvalue + "." + circuitsection;
            List<int> numbersList = new List<int>();
            foreach (var FI in FIs)
            {
                string mark = FI.get_Parameter(BuiltInParameter.DOOR_NUMBER).AsString();
                Element elem = RevitAPI.Document.GetElement(FI.Id); Element type = RevitAPI.Document.GetElement(elem.GetTypeId());
                //параметр-префикс
                string adskP = "";
                if (param.ParamExistByGuid(adskPparamGuid, elem))
                    adskP = elem.get_Parameter(adskPparamGuid).AsString();
                else if (param.ParamExistByGuid(adskPparamGuid, type))
                    adskP = type.get_Parameter(adskPparamGuid).AsString();
                if (adskP != ""&&adskP!=null) mark = mark.Replace(adskP, ""); //убираем из Марки префикс (ADSK_Позиция)
                //получаем из Марки цепь
                string[] markparts = mark.Split('.');
                if (markparts.Length > 1) mark = mark.Replace("." + markparts[markparts.Length - 1], "");
                //проверяем цепь на введенную в окне
                if (mark == curcuit)
                {
                    int i = 0;
                    int.TryParse(markparts[markparts.Length - 1], out i);
                    numbersList.Add(i);
                }
            }
            if (numbersList.Count > 0)
            {
                //проверяем наличие пропусков в нумерации элементов цепи
                int maxNumber = numbersList.Max();
                if (maxNumber > numbersList.Count + 1)
                    new infowindow280("В цепи " + curcuit + " " + numbersList.Count.ToString() +
                        " элементов, а последний использованный номер - " + numbersList.Max().ToString() +
                        ". Брать последний номер - некорректно. Возможно, нужна перенумерация элементов.").ShowDialog();
                else
                {
                    //назначаем стартовый номер
                    if (maxNumber > 0) { maxNumber = maxNumber + 1; startvalue = maxNumber.ToString(); }
                }
            }
            else startvalue = "1";



        }
        private bool CanNumerate(object param)
        {
            return int.TryParse(startvalue, out _);
        }
        private bool CanGetLastNumber(object param)
        {
            /*
            //получаем элементы
            List<FamilyInstance> FIs = new List<FamilyInstance>();
            List<FamilyInstance> elEq = new FilteredElementCollector(RevitAPI.Document).OfCategory(BuiltInCategory.OST_ElectricalEquipment)
                .WhereElementIsNotElementType().Cast<FamilyInstance>().ToList(); foreach (FamilyInstance el in elEq) FIs.Add(el);
            List<FamilyInstance> FireAlarmDevices = new FilteredElementCollector(RevitAPI.Document).OfCategory(BuiltInCategory.OST_FireAlarmDevices)
                .WhereElementIsNotElementType().Cast<FamilyInstance>().ToList(); foreach (FamilyInstance fad in FireAlarmDevices) FIs.Add(fad);
            List<FamilyInstance> AlertDevices = new FilteredElementCollector(RevitAPI.Document).OfCategory(BuiltInCategory.OST_NurseCallDevices)
                .WhereElementIsNotElementType().Cast<FamilyInstance>().ToList(); foreach (FamilyInstance ad in AlertDevices) FIs.Add(ad);
            List<FamilyInstance> CommDevices = new FilteredElementCollector(RevitAPI.Document).OfCategory(BuiltInCategory.OST_CommunicationDevices)
                .WhereElementIsNotElementType().Cast<FamilyInstance>().ToList(); foreach (FamilyInstance cd in CommDevices) FIs.Add(cd);
            //ищем цепь по введенным данным
            string curcuit = circuitvalue; if (circuitsection != "") curcuit = circuitvalue + "." + circuitsection;
            bool curcuitExists = false;
            foreach(var FI in FIs)
            {
                string mark = FI.get_Parameter(BuiltInParameter.DOOR_NUMBER).AsString();
                Element elem = RevitAPI.Document.GetElement(FI.Id); Element type = RevitAPI.Document.GetElement(elem.GetTypeId());
                //параметр-префикс
                string adskP = "";
                if (param.ParamExistByGuid(adskPparamGuid, elem))
                    adskP = elem.get_Parameter(adskPparamGuid).AsString();
                else if (param.ParamExistByGuid(adskPparamGuid, type))
                    adskP = type.get_Parameter(adskPparamGuid).AsString();
                if (adskP != "") mark = mark.Replace(adskP, ""); //убираем из Марки префикс (ADSK_Позиция)
                //получаем из Марки цепь
                string[] markparts = mark.Split('.');
                if (markparts.Length > 2) mark = mark.Replace("." + markparts[markparts.Length - 1], "");
                //проверяем цепь на введенную в окне
                if (mark == curcuit) { curcuitExists = true; break; }
            }
            //цепь существует
            return curcuitExists;
            */
            return true;
        }

        public event EventHandler CloseRequest;
        private void RaiseCloseRequest()
        {
            CloseRequest?.Invoke(this, EventArgs.Empty);
        }
        public event EventHandler HideRequest;
        private void RaiseHideRequest()
        {
            HideRequest?.Invoke(this, EventArgs.Empty);
        }
        public event EventHandler ShowRequest;
        private void RaiseShowRequest()
        {
            ShowRequest?.Invoke(this, EventArgs.Empty);
        }

        public event PropertyChangedEventHandler PropertyChanged;

        void OnPropertyChanged([CallerMemberName] string PropertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(PropertyName));
        }
    }
    [Transaction(TransactionMode.Manual)]
    public class ssNumberer : IExternalCommand
    {
        Guid adskPparamGuid = new Guid("ae8ff999-1f22-4ed7-ad33-61503d85f0f4");//ADSK_Позиция
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            string TNovClassName = "Адресатор"; DateTime dateTime = DateTime.Now;
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


            //диалоговое окно
            var viewModel = new ssNumbererViewModel();
            // Десериализация
            bool forProject = true;
            json js = new json(in TNovClassName, in forProject, out bool canserialize, out string jsonpath);
            if (canserialize)
            {
                viewModel = JsonConvert.DeserializeObject<ssNumbererViewModel>(File.ReadAllText(jsonpath));
                Logger.Log("Десериализация прошла успешно", 1);
            }
            var view = new ssNumbererwpf(viewModel);
            viewModel.CloseRequest += (s, e) => view.Close();
            viewModel.HideRequest += (s, e) => view.Hide();
            viewModel.ShowRequest += (s, e) => view.ShowDialog();
            view.ShowDialog();
            //Сериализация
            try
            {
                File.WriteAllText(jsonpath, JsonConvert.SerializeObject(viewModel));
                Logger.Log("Сериализация прошла успешно", 1);
            }
            catch (Exception ex) { Logger.Log("Ошибка при сериализации: " + ex.Message, 4); }

            //получаем элементы
            List<FamilyInstance> FIs = new List<FamilyInstance>();
            List<FamilyInstance> elEq = new FilteredElementCollector(RevitAPI.Document).OfCategory(BuiltInCategory.OST_ElectricalEquipment)
                .WhereElementIsNotElementType().Cast<FamilyInstance>().ToList(); foreach (FamilyInstance el in elEq) FIs.Add(el);
            List<FamilyInstance> FireAlarmDevices = new FilteredElementCollector(RevitAPI.Document).OfCategory(BuiltInCategory.OST_FireAlarmDevices)
                .WhereElementIsNotElementType().Cast<FamilyInstance>().ToList(); foreach (FamilyInstance fad in FireAlarmDevices) FIs.Add(fad);
            List<FamilyInstance> AlertDevices = new FilteredElementCollector(RevitAPI.Document).OfCategory(BuiltInCategory.OST_NurseCallDevices)
                .WhereElementIsNotElementType().Cast<FamilyInstance>().ToList(); foreach (FamilyInstance ad in AlertDevices) FIs.Add(ad);
            List<FamilyInstance> CommDevices = new FilteredElementCollector(RevitAPI.Document).OfCategory(BuiltInCategory.OST_CommunicationDevices)
                .WhereElementIsNotElementType().Cast<FamilyInstance>().ToList(); foreach (FamilyInstance cd in CommDevices) FIs.Add(cd);
            //ищем элементы цепи
            string curcuit = viewModel.circuitvalue; if (viewModel.circuitsection != "") curcuit = viewModel.circuitvalue + "." + viewModel.circuitsection;
            List<int> numbersList = new List<int>();
            foreach (var FI in FIs)
            {
                string mark = FI.get_Parameter(BuiltInParameter.DOOR_NUMBER).AsString();
                Element elem = RevitAPI.Document.GetElement(FI.Id); Element type = RevitAPI.Document.GetElement(elem.GetTypeId());
                //параметр-префикс
                string adskP = "";
                if (param.ParamExistByGuid(adskPparamGuid, elem))
                    adskP = elem.get_Parameter(adskPparamGuid).AsString();
                else if (param.ParamExistByGuid(adskPparamGuid, type))
                    adskP = type.get_Parameter(adskPparamGuid).AsString();
                if (adskP != "" && adskP != null) mark = mark.Replace(adskP, ""); //убираем из Марки префикс (ADSK_Позиция)
                //получаем из Марки цепь
                string[] markparts = mark.Split('.');
                if (markparts.Length > 1) mark = mark.Replace("." + markparts[markparts.Length - 1], "");
                //проверяем цепь на введенную в окне
                if (mark == curcuit)
                {
                    int i = 0;
                    int.TryParse(markparts[markparts.Length - 1], out i);
                    numbersList.Add(i);
                }
            }
            if (numbersList.Count > 0)
            {
                string messageStart = "В цепи " + curcuit;
                string message1 = "";
                //проверяем наличие пропусков в нумерации элементов цепи
                int maxNumber = numbersList.Max();
                if (maxNumber > numbersList.Count + 1)
                    message1 = " "+numbersList.Count.ToString() +
                        " элементов, а последний использованный номер - " + numbersList.Max().ToString();
                string message2 = "";
                //проверяем наличие дублей
                var duplicates = numbersList.GroupBy(s => s.ToString()).SelectMany(grp => grp.Skip(1));
                if(duplicates.ToList().Count> 0)
                {
                    duplicates = duplicates.Distinct().ToList();
                    string combinedString = string.Join(",", duplicates.ToArray());
                    if (message1.Length > 0) message2 = ", а также";
                    message2 += " найдены дублирующиеся адреса: " + combinedString;
                }
                if(duplicates.ToList().Count > 0|| maxNumber > numbersList.Count + 1)
                {
                    new infowindow280(messageStart + message1+ message2+
                        ". Возможно, нужна перенумерация элементов.").ShowDialog();
                }
            }

            Logger.Log("Завершение работы.", 5);
            return Result.Succeeded;
        }
    }
}
