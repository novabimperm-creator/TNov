using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Electrical;
using Autodesk.Revit.DB.Structure;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using Microsoft.Office.Interop.Excel;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Threading;
using TNov.main;
using Parameter = Autodesk.Revit.DB.Parameter;

namespace TNov
{
    public class ConduitCube
    {
        public string Name;
        public string ADSKGroup;
        public List<string> StringValues;
        public List<double> DoubleValues;
    }

    public class ElNonModelCube
    {
        public string adskGroup;
        public string adskNaim;
        public string adskMark;
        public string adskObozn;
        public string adskCode;
        public string adskManuf;
        public string adskEd;
        public string NEGroup;
        public string NSort;
        public double adskC;
    }

    [Transaction(TransactionMode.Manual)]
    public class MEPSpec : IExternalCommand
    {
        private TNovProgressBar adskgProgressBar;
        //Параметры
        Guid NGNparamGuid = new Guid("cb7d14c9-43a3-451a-926c-f569df8b8c03");//N_Группирование_Не перезаполнять
        Guid NCatparamGuid = new Guid("e71ab526-6b0b-4c3f-9b52-ba7f61a83d46");//N_Категория
        Guid adskGparamGuid = new Guid("3de5f1a4-d560-4fa8-a74f-25d250fb3401");//ADSK_Группирование
        Guid adskNparamGuid = new Guid("e6e0f5cd-3e26-485b-9342-23882b20eb43");//ADSK_Наименование
        Guid adskMarkparamGuid = new Guid("2204049c-d557-4dfc-8d70-13f19715e46d");//ADSK_Марка
        Guid adskOboznparamGuid = new Guid("9c98831b-9450-412d-b072-7d69b39f4029");//ADSK_Обозначение
        Guid adskCodeparamGuid = new Guid("2fd9e8cb-84f3-4297-b8b8-75f444e124ed");//ADSK_Код изделия
        Guid adskManufparamGuid = new Guid("a8cdbf7b-d60a-485e-a520-447d2055f351");//ADSK_Завод-изготовитель
        Guid adskEdparamGuid = new Guid("4289cb19-9517-45de-9c02-5a74ebf5c86d");//ADSK_Единица измерения
        Guid adskCparamGuid = new Guid("8d057bb3-6ccd-4655-9165-55526691fe3a");//ADSK_Количество
        Guid NEGparamGuid = new Guid("837842da-379d-496f-9ef3-be8886a0161f");//N_ЭЛ.Группирование ЭЛ
        Guid NSortparamGuid = new Guid("dbd21888-5efd-4e29-8722-2fe8c6d4f799");//N_Сортировка
        string[] conduitStringParams = new string[]
        {
            "RBZ_Пучок1_Ед.измерения","RBZ_Пучок1_Марка","RBZ_Пучок1_Описание","RBZ_Пучок1_Производитель",
            "RBZ_Пучок2_Ед.измерения","RBZ_Пучок2_Марка","RBZ_Пучок2_Описание","RBZ_Пучок2_Производитель",
            "RBZ_Пучок3_Ед.измерения","RBZ_Пучок3_Марка","RBZ_Пучок3_Описание","RBZ_Пучок3_Производитель",
            "RBZ_Пучок4_Ед.измерения","RBZ_Пучок4_Марка","RBZ_Пучок4_Описание","RBZ_Пучок4_Производитель",
            "RBZ_Пучок5_Ед.измерения","RBZ_Пучок5_Марка","RBZ_Пучок5_Описание","RBZ_Пучок5_Производитель",
            "RBZ_Крепеж_Ед.измерения","RBZ_Крепеж_Марка","RBZ_Крепеж_Описание","RBZ_Крепеж_Производитель","RBZ_Крепеж_Артикул",
            "RBZ_Труба_Ед.измерения","RBZ_Труба_Марка","RBZ_Труба_Описание","RBZ_Труба_Производитель","RBZ_Труба_Артикул"
        };
        string[] esystemStringParams = new string[]
        {
            "RBZ_Кабель_Ед.измерения","RBZ_Кабель_Марка","RBZ_Кабель_Описание","RBZ_Кабель_Производитель",
            "RBZ_Крепеж_Ед.измерения","RBZ_Крепеж_Марка","RBZ_Крепеж_Описание","RBZ_Крепеж_Производитель","RBZ_Крепеж_Артикул",
            "RBZ_Труба_Ед.измерения","RBZ_Труба_Марка","RBZ_Труба_Описание","RBZ_Труба_Производитель","RBZ_Труба_Артикул"
        };
        string[] cubeConduitDoubleParams = new string[]
        {
            "Короб_Кабель_1_Количество","Короб_Кабель_2_Количество","Короб_Кабель_3_Количество","Короб_Кабель_4_Количество",
            "Короб_Кабель_5_Количество","Короб_Крепеж_Количество","Короб_Труба_Количество"
        };
        string[] cubeElSystemDoubleParams = new string[]
        {
            "Цепь_Кабель_Количество","Цепь_Крепеж_Количество","Цепь_Труба_Количество"
        };
        string[] cubeCableTrayDoubleParams = new string[]
        {
            "Лоток_Кабель_1_Количество","Лоток_Кабель_2_Количество","Лоток_Кабель_3_Количество","Лоток_Кабель_4_Количество",
            "Лоток_Кабель_5_Количество"
        };
        private const string TargetScheduleName = "СО_Общая для ТЗ";

        private void ThreadStartingPoint()
        {
            this.adskgProgressBar = new TNovProgressBar();
            this.adskgProgressBar.Show();
            Dispatcher.Run();
        }
        
        private void Setadskgparam(ElementId elemid, in string paramname, in bool systemcut, out bool success)
        {
            string eid = elemid.ToString();
            Element elem = RevitAPI.Document.GetElement(elemid);
            Logger.Log("   Элемент " + eid + ":", 2);
            string system = elem.LookupParameter(paramname).AsValueString(); //получаем значение исходного параметра
            success = false;
            bool runG = true;
            bool param2exist = Param.ParamExistByGuid(NGNparamGuid, elem);
            if (param2exist)
            {
                int noRun = elem.get_Parameter(NGNparamGuid).AsInteger(); //получаем значение параметра "Не перезаполнять"
                if (noRun == 1) { runG = false; success = true; Logger.Log("      не перезаполнять", 2); }
            }

            bool adskNparamexist = Param.ParamExistByGuid(adskNparamGuid, elem);

            string adskN;
            if (adskNparamexist) { adskN = elem.get_Parameter(adskNparamGuid).AsString(); }
            else { Element aType = RevitAPI.Document.GetElement(elem.GetTypeId()); adskN = aType.get_Parameter(adskNparamGuid).AsString(); }
            bool hasValue = elem.LookupParameter("Имя системы").HasValue;
            if (adskN == null || adskN == "") adskN = "нет";
            else if (!hasValue || adskN.Contains("!Не учитывать"))
            {
                runG = false;
                success = true;
                Logger.Log("      пропуск", 2);
            }
            if (elem.get_Parameter(adskNparamGuid).IsReadOnly) runG = false;
            if (runG) //ADSK_Группирование
            {
                if (systemcut && system.Contains(","))
                {
                    string[] systemParts = system.Split(',');
                    system = systemParts[0];
                }
                bool adskgParamExist = Param.ParamExistByGuid(adskGparamGuid, elem);
                if (adskgParamExist)
                {
                    bool isReadOnly = elem.get_Parameter(adskGparamGuid).IsReadOnly;
                    if (!isReadOnly)
                    {
                        try
                        {
                            elem.get_Parameter(adskGparamGuid)?.Set(system);
                            success = true;
                            Logger.Log("      назначено " + system, 2);
                        }
                        catch (Exception ex)
                        {
                            Logger.Log("      Ошибка: " + ex.Message, 4);
                        }
                    }
                    else success = true;
                }

            }
        }
        private void SetNCategory(ElementId elemid, out bool success)
        {
            string eid = elemid.ToString();
            Element elem = RevitAPI.Document.GetElement(elemid);
            Logger.Log("   Элемент " + eid + ":", 2);
            success = false;

            bool runCat = Param.ParamExistByGuid(NCatparamGuid, elem);  //ищем параметр N_Категория

            if (runCat) { if (elem.get_Parameter(NCatparamGuid).IsReadOnly) runCat = false; }

            if (runCat) //N_Категория
            {
                string category = elem.Category.Name;
                string Ncategory = "6. Материалы и прочие элементы";
                switch (category)
                {
                    case "Материалы изоляции труб":
                        Ncategory = "5. Изоляционные материалы"; break;
                    case "Материалы изоляции воздуховодов":
                        Ncategory = "5. Изоляционные материалы"; break;
                    case "Материалы внутренней изоляции воздуховодов":
                        Ncategory = "5. Изоляционные материалы"; break;
                    case "Спринклеры":
                        Ncategory = "2. Спринклеры"; break;
                    case "Арматура трубопроводов":
                        Ncategory = "3. Арматура"; break;
                    case "Арматура воздуховодов":
                        Ncategory = "3. Арматура"; break;
                    case "Воздухораспределители":
                        Ncategory = "2. Воздухораспределители"; break;
                    case "Гибкие воздуховоды":
                        Ncategory = "4. Воздуховоды"; break;
                    case "Воздуховоды":
                        Ncategory = "4. Воздуховоды"; break;
                    case "Соединительные детали воздуховодов":
                        Ncategory = "4. Воздуховоды"; break;
                    case "Соединительные детали трубопроводов":
                        Ncategory = "4. Трубопроводы"; break;
                    case "Оборудование":
                        Ncategory = "1. Оборудование"; break;
                    case "Трубы":
                        Ncategory = "4. Трубопроводы"; break;
                    case "Гибкие трубы":
                        Ncategory = "4. Трубопроводы"; break;
                    case "Сантехнические приборы":
                        Ncategory = "2. Сантехнические приборы"; break;
                }

                try
                {
                    elem.get_Parameter(NCatparamGuid)?.Set(Ncategory); success = true;
                    Logger.Log("      N_Категория " + Ncategory, 2);
                }
                catch (Exception ex)
                {
                    success = false;
                    Logger.Log("     Ошибка: " + ex.Message, 4);
                }

            }


        }
        public void Setadskpparam(ElementId elemid, in string category, in string fileName, out bool success)
        {
            success = false;

            string eid = elemid.ToString();
            Element elem = RevitAPI.Document.GetElement(elemid);
            Logger.Log("   Элемент " + eid + ":", 2);

            //вьюмодель
            Element type = RevitAPI.Document.GetElement(elem.GetTypeId());
            string mark = type.get_Parameter(BuiltInParameter.WINDOW_TYPE_ID).AsString();
            if (mark == null || mark.Length == 0)
            {
                mark = "пустая маркировка";
            }
            if (mark.Contains("PEX")) { success = true; }
            else
            {
                mark = category + "_" + mark;
                MEPSpecOVVKParamsViewModel viewModel2 = new MEPSpecOVVKParamsViewModel();
                viewModel2.elemType = mark;
                // Десериализация
                bool forProject2 = true;
                string VMName = "ADSK Параметры_" + mark;
                json js = new json(in VMName, in forProject2, out bool canserialize2, out string jsonpath2);
                if (canserialize2)
                {
                    viewModel2 = JsonConvert.DeserializeObject<MEPSpecOVVKParamsViewModel>(File.ReadAllText(jsonpath2));
                    Logger.Log("Десериализация прошла успешно: " + VMName, 2);
                }
                else
                {
                    //базовые значения
                    MEPSpecTools.MEPSpecOVVKBaseParams(mark, fileName, viewModel2);
                    //Сериализация
                    try
                    {
                        File.WriteAllText(jsonpath2, JsonConvert.SerializeObject(viewModel2));
                        Logger.Log("Cериализация прошла успешно: " + VMName, 2);
                    }
                    catch (Exception e) { Logger.Log("Ошибка сериализации: " + e.Message, 4); }
                }
                //вычисление Наименования
                string naimValue = "";
                string param1 = "";
                if (Param.ParamExist(viewModel2.naimPar1, type))
                {
                    param1 = type.LookupParameter(viewModel2.naimPar1).AsString();
                    if (param1 == null || param1.Length == 0) param1 = "";
                }
                else if (Param.ParamExist(viewModel2.naimPar1, elem)) //добавлена возможность считывания первого параметра по экземпляру
                {
                    param1 = elem.LookupParameter(viewModel2.naimPar1).AsString();
                    if (param1 == null || param1.Length == 0) param1 = "";
                }

                naimValue = param1;

                if (viewModel2.naimPar2 != "выкл")
                {
                    bool param2exist = Param.ParamExist(viewModel2.naimPar2, elem);
                    if (param2exist)
                    {
                        string param2 = "";
                        if (elem.LookupParameter(viewModel2.naimPar2).StorageType == StorageType.Double)
                        {
                            double paramDoubleValue = elem.LookupParameter(viewModel2.naimPar2).AsDouble() * 0.3048 * 1000;
                            paramDoubleValue = Math.Round(paramDoubleValue, 1);
                            param2 = paramDoubleValue.ToString().Replace(',', '.');
                        }
                        else param2 = elem.LookupParameter(viewModel2.naimPar2).AsValueString();
                        if (param2 != null && param2.Length > 0)
                        {
                            if (viewModel2.naimPrefix2 != null && viewModel2.naimPrefix2.Length > 0) naimValue = naimValue + viewModel2.naimPrefix2;
                            naimValue = naimValue + param2;
                        }
                    }
                }

                if (viewModel2.naimPar3 != "выкл")
                {
                    bool param3exist = Param.ParamExist(viewModel2.naimPar3, elem);
                    if (param3exist)
                    {
                        string param3 = "";
                        if (elem.LookupParameter(viewModel2.naimPar3).StorageType == StorageType.Double)
                        {
                            double paramDoubleValue = elem.LookupParameter(viewModel2.naimPar3).AsDouble() * 0.3048 * 1000;
                            paramDoubleValue = Math.Round(paramDoubleValue, 1);
                            param3 = paramDoubleValue.ToString().Replace(',', '.');
                        }
                        else param3 = elem.LookupParameter(viewModel2.naimPar3).AsValueString();
                        if (param3 != null && param3.Length > 0)
                        {
                            if (viewModel2.naimPrefix3 != null && viewModel2.naimPrefix3.Length > 0) naimValue = naimValue + viewModel2.naimPrefix3;
                            naimValue = naimValue + param3;
                        }
                    }
                }

                if (viewModel2.naimPar4 != "выкл")
                {
                    bool param4exist = Param.ParamExist(viewModel2.naimPar4, elem);
                    if (param4exist)
                    {
                        string param4 = "";
                        if (elem.LookupParameter(viewModel2.naimPar4).StorageType == StorageType.Double)
                        {
                            double paramDoubleValue = elem.LookupParameter(viewModel2.naimPar4).AsDouble() * 0.3048 * 1000;
                            paramDoubleValue = Math.Round(paramDoubleValue, 1);
                            param4 = paramDoubleValue.ToString().Replace(',', '.');
                        }
                        else param4 = elem.LookupParameter(viewModel2.naimPar4).AsValueString();
                        if (param4 != null && param4.Length > 0)
                        {
                            if (viewModel2.naimPrefix4 != null && viewModel2.naimPrefix4.Length > 0) naimValue = naimValue + viewModel2.naimPrefix4;
                            naimValue = naimValue + param4;
                        }
                    }
                }

                //вычисление Количества
                double countValue = 0;
                switch (viewModel2.countPar)
                {
                    case "Число":
                        countValue = 1;
                        break;
                    case "Длина":
                        Parameter paramL = elem.get_Parameter(BuiltInParameter.CURVE_ELEM_LENGTH);
                        if (paramL != null) countValue = paramL.AsDouble();
                        countValue = countValue * 0.3048;
                        break;
                    case "Площадь":
                        Parameter paramA = elem.get_Parameter(BuiltInParameter.RBS_CURVE_SURFACE_AREA);
                        if (paramA != null) countValue = paramA.AsDouble();
                        countValue = countValue * 0.3048 * 0.3048;
                        break;
                    case "Объем":
                        Parameter paramV = elem.get_Parameter(BuiltInParameter.RBS_INSULATION_LINING_VOLUME);
                        if (paramV != null) countValue = paramV.AsDouble();
                        countValue = countValue * 0.3048 * 0.3048 * 0.3048;
                        break;
                }
                double coeff = 1;
                string vmk = viewModel2.countK.Replace('.', ',');
                Double.TryParse(vmk, out coeff);
                countValue = countValue * coeff;
                countValue = Math.Round(countValue, 1);

                //заполнение параметров
                bool success1 = false;
                //bool adskNparamexist = param.ParamExist("ADSK_Наименование", elem);
                bool adskNparamexist = Param.ParamExistByGuid(adskNparamGuid, elem);
                if (adskNparamexist)
                {
                    bool isReadOnly = elem.get_Parameter(adskNparamGuid).IsReadOnly;
                    if (!isReadOnly)
                    {
                        try
                        {
                            elem.get_Parameter(adskNparamGuid)?.Set(naimValue);
                            success1 = true;
                            Logger.Log("      назначено " + naimValue, 2);
                        }
                        catch (Exception ex)
                        {
                            Logger.Log("      Ошибка: " + ex.Message, 4);
                        }
                    }
                    else success1 = true;
                }
                else
                {
                    bool adskCparamexistType = Param.ParamExistByGuid(adskNparamGuid, type);
                    if (adskCparamexistType) //наименование назначено по типу
                    {
                        success1 = true;
                    }
                }
                bool success2 = false;
                //bool adskCparamexist = param.ParamExist("ADSK_Количество", elem);
                bool adskCparamexist = Param.ParamExistByGuid(adskCparamGuid, elem);
                if (adskCparamexist)
                {
                    double currentC = elem.get_Parameter(adskCparamGuid).AsDouble();
                    if (currentC == 1 && countValue == 1) //количество назначено 1 по экз
                    {
                        success2 = true;
                    }
                    else
                    {
                        try
                        {
                            elem.get_Parameter(adskCparamGuid)?.Set(countValue);
                            success2 = true;
                            Logger.Log("      назначено " + countValue.ToString(), 2);
                        }
                        catch (Exception ex)
                        {
                            Logger.Log("      Ошибка: " + ex.Message, 4);
                        }
                    }

                }
                else
                {
                    bool adskCparamexistType = Param.ParamExistByGuid(adskCparamGuid, type); //количество назначено 1 по типу
                    if (adskCparamexistType)
                    {
                        double currentC = type.get_Parameter(adskCparamGuid).AsDouble();
                        if (currentC == 1 && countValue == 1) //количество назначено 1 по экз
                        {
                            success2 = true;
                        }
                    }
                }
                success = success1 && success2;
            }


        }
        
        
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            string TNovClassName = "ADSK Группы"; DateTime dateTime = DateTime.Now;
            //подключение приложения и документа
            if (RevitAPI.UiApplication == null) { RevitAPI.Initialize(commandData); }
            UIDocument uidoc = RevitAPI.UiDocument; Document doc = RevitAPI.Document; string fileName = doc.Title.ToString();
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

            Logger.Log("Сбор элементов", 1);




            //СЦЕНАРИЙ ВК ОВ / СС ПС / ЭЛ
            bool ss = false; bool el = false; bool vkov = false;
            string docName = doc.Title.ToString();
            if (docName.Contains("-СС") || docName.Contains("_СС")) ss = true;
            if (docName.Contains("-ЭЛ") || docName.Contains("_ЭЛ") || docName.Contains("-ЭО") || docName.Contains("_ЭО")
                || docName.Contains("-ЭС") || docName.Contains("_ЭС")) el = true;
            if (docName.Contains("-ВК") || docName.Contains("_ВК") || docName.Contains("-ПТ") || docName.Contains("_ПТ")
                || docName.Contains("-ОВ") || docName.Contains("_ОВ") || docName.Contains("-ТС") || docName.Contains("_ТС")) vkov = true;

            if (ss)
            {
                //СС ПС

                //сбор категорий для СС ПС

                List<FamilyInstance> elEq = new FilteredElementCollector(doc)
                    .OfCategory(BuiltInCategory.OST_ElectricalEquipment)
                    .WhereElementIsNotElementType()
                    .Cast<FamilyInstance>()
                    .ToList();

                List<CableTray> CableTrays = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_CableTray)
                                                                 .WhereElementIsNotElementType()
                                                                 .Cast<CableTray>()
                                                                 .ToList();

                List<FamilyInstance> CableTrayFittings = new FilteredElementCollector(doc)
                    .OfCategory(BuiltInCategory.OST_CableTrayFitting)
                    .WhereElementIsNotElementType()
                    .Cast<FamilyInstance>()
                    .ToList();

                List<Conduit> Conduit = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_Conduit)
                                                                 .WhereElementIsNotElementType()
                                                                 .Cast<Conduit>()
                                                                 .ToList();

                List<FamilyInstance> FireAlarmDevices = new FilteredElementCollector(doc)
                    .OfCategory(BuiltInCategory.OST_FireAlarmDevices)
                    .WhereElementIsNotElementType()
                    .Cast<FamilyInstance>()
                    .ToList();

                List<ElectricalSystem> ElectricalSystems = new FilteredElementCollector(doc)
                    .OfCategory(BuiltInCategory.OST_ElectricalCircuit)
                    .WhereElementIsNotElementType()
                    .Cast<ElectricalSystem>()
                    .ToList();

                List<Element> GMs = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_GenericModel)   //фильтр по категории Об модели
                                                                             .WhereElementIsNotElementType()
                                                                             .OfClass(typeof(FamilyInstance))
                                                                             .Cast<Element>()
                                                                             .ToList();

                //коэффициенты 
                Logger.Log("Диалог", 1);
                MEPSpecSSViewModel viewModel = new MEPSpecSSViewModel();

                // Десериализация
                bool forProject = true;
                string VMName = TNovClassName + "_СС";
                json js = new json(in VMName, in forProject, out bool canserialize, out string jsonpath);
                if (canserialize)
                {
                    viewModel = JsonConvert.DeserializeObject<MEPSpecSSViewModel>(File.ReadAllText(jsonpath));
                }
                var wpfview = new MEPSpecSSWPF(viewModel);
                viewModel.CloseRequest += (s, e) => wpfview.Close();
                bool? ok = wpfview.ShowDialog();
                if (ok != null && ok == true) { }
                else { Logger.Log("Запуск отменен пользователем. Завершение работы.", 3); return Result.Cancelled; }
                //Сериализация
                try
                {
                    File.WriteAllText(jsonpath, JsonConvert.SerializeObject(viewModel));
                    Logger.Log("Сериализация прошла успешно", 1);
                }
                catch (Exception ex) { Logger.Log("Ошибка при сериализации: " + ex.Message, 4); }

                double conduitCoeff = 1.3;
                string vmk1 = viewModel.ConduitCoeff.Replace('.', ',');
                Double.TryParse(vmk1, out conduitCoeff);

                double conduitCoeffPipe = 1.3;
                string vmk2 = viewModel.ConduitCoeffPipe.Replace('.', ',');
                Double.TryParse(vmk2, out conduitCoeffPipe);

                double conduitStep = 500;
                string vmk3 = viewModel.ElSystemStep.Replace('.', ',');
                Double.TryParse(vmk3, out conduitStep);

                double elSystemCoeffCable = 1.5;
                string vmk4 = viewModel.ElSystemCoeffCable.Replace('.', ',');
                Double.TryParse(vmk4, out elSystemCoeffCable);

                double elSystemCoeffPipe = 1.3;
                string vmk5 = viewModel.ElSystemCoeffPipe.Replace('.', ',');
                Double.TryParse(vmk5, out elSystemCoeffPipe);

                double elSystemStep = 500;
                string vmk6 = viewModel.ElSystemStep.Replace('.', ',');
                Double.TryParse(vmk6, out elSystemStep);

                double cableTrayCoeffCable = 1.3;
                string vmk7 = viewModel.CableTrayCoeffCable.Replace('.', ',');
                Double.TryParse(vmk7, out cableTrayCoeffCable);

                int failscount = 0;
                List<string> failed = new List<string>();

                Logger.Log("Ищем принципиальные типы лотков", 1);
                List<string> CableTrayTypes = new List<string>();
                foreach (var c in CableTrays)
                {
                    string cType =
                        c.get_Parameter(adskGparamGuid).AsString() +
                        c.LookupParameter("Кабель тип 1").AsElementId().IntegerValue.ToString() +
                        c.LookupParameter("Кабель тип 2").AsElementId().IntegerValue.ToString() +
                        c.LookupParameter("Кабель тип 3").AsElementId().IntegerValue.ToString() +
                        c.LookupParameter("Кабель тип 4").AsElementId().IntegerValue.ToString() +
                        c.LookupParameter("Кабель тип 5").AsElementId().IntegerValue.ToString();
                    CableTrayTypes.Add(cType);
                }
                CableTrayTypes = CableTrayTypes.Distinct().ToList();
                foreach (var cType in CableTrayTypes) Logger.Log("   " + cType, 2);

                Logger.Log("Ищем принципиальные типы коробов", 1);
                List<string> ConduitTypes = new List<string>();
                foreach (var c in Conduit)
                {
                    string cType =
                        c.get_Parameter(adskGparamGuid).AsString() +
                        c.LookupParameter("Кабель тип 1").AsElementId().IntegerValue.ToString() +
                        c.LookupParameter("Кабель тип 2").AsElementId().IntegerValue.ToString() +
                        c.LookupParameter("Кабель тип 3").AsElementId().IntegerValue.ToString() +
                        c.LookupParameter("Кабель тип 4").AsElementId().IntegerValue.ToString() +
                        c.LookupParameter("Кабель тип 5").AsElementId().IntegerValue.ToString() +
                        c.LookupParameter("Труба").AsElementId().IntegerValue.ToString() +
                        c.LookupParameter("Крепеж").AsElementId().IntegerValue.ToString();
                    ConduitTypes.Add(cType);
                }
                ConduitTypes = ConduitTypes.Distinct().ToList();
                foreach (var cType in ConduitTypes) Logger.Log("   " + cType, 2);

                using (TransactionGroup group = new TransactionGroup(RevitAPI.Document, "TNov - Сводная спека"))
                {
                    group.Start();

                    //короба


                    int allcount = elEq.Count + CableTrays.Count + CableTrayFittings.Count + ConduitTypes.Count + CableTrayTypes.Count + FireAlarmDevices.Count + ElectricalSystems.Count;

                    Thread thread = new Thread(new ThreadStart(this.ThreadStartingPoint));
                    thread.SetApartmentState(ApartmentState.STA);
                    thread.IsBackground = true;
                    thread.Start();
                    Thread.Sleep(100);


                    int PBCount = 0;
                    this.adskgProgressBar.TNov_ProgressBar.Dispatcher.Invoke<double>((Func<double>)(() => this.adskgProgressBar.TNov_ProgressBar.Minimum = (double)PBCount));
                    this.adskgProgressBar.TNov_ProgressBar.Dispatcher.Invoke<string>((Func<string>)(() => this.adskgProgressBar.value.Text = PBCount.ToString()));
                    this.adskgProgressBar.TNov_ProgressBar.Dispatcher.Invoke<double>((Func<double>)(() => this.adskgProgressBar.TNov_ProgressBar.Maximum = (double)allcount));
                    this.adskgProgressBar.TNov_ProgressBar.Dispatcher.Invoke<string>((Func<string>)(() => this.adskgProgressBar.maxvalue.Text = allcount.ToString()));



                    Logger.Log("Короба. Ищем кубики", 1);

                    int j = 0;
                    ICollection<ElementId> GMsToRemove = new List<ElementId>();
                    int cubeId = -1;
                    foreach (FamilyInstance GM0 in GMs)
                    {
                        Element e = RevitAPI.Document.GetElement(GM0.Id);
                        string familyName0 = GM0.Symbol.FamilyName;
                        Element eType0 = RevitAPI.Document.GetElement(e.GetTypeId());
                        if (familyName0.Contains("pmN.Условное семейство СС ПС") && eType0.Name.Contains("Короб"))
                        {
                            j++;
                            if (j == 1) //первый кубик данного типа - очищаем параметры
                            {
                                e.LookupParameter("Короб_Кабель_1_Количество")?.Set(0);
                                e.LookupParameter("Короб_Кабель_2_Количество")?.Set(0);
                                e.LookupParameter("Короб_Кабель_3_Количество")?.Set(0);
                                e.LookupParameter("Короб_Кабель_4_Количество")?.Set(0);
                                e.LookupParameter("Короб_Кабель_5_Количество")?.Set(0);
                                e.LookupParameter("Короб_Крепеж_Количество")?.Set(0);
                                e.LookupParameter("Короб_Труба_Количество")?.Set(0);
                                cubeId = GM0.Id.IntegerValue;
                                Logger.Log("   Первый кубик найден и обработан", 2);
                            }
                            if (j > 1) GMsToRemove.Add(GM0.Id); //последующие кубики данного типа - в список на удаление
                        }

                    }
                    if (j > 1)
                    {
                        using (Transaction transactionCubes = new Transaction(doc))
                        {
                            transactionCubes.Start("TNov - Сводная спека (короба кубики)");
                            Logger.Log("Открываем транзакцию 1", 1);

                            RevitAPI.Document.Delete(GMsToRemove.ToArray());
                            Logger.Log("   Удалены остальные кубики в количестве: " + GMsToRemove.Count.ToString(), 1);

                            Logger.Log("Закрываем транзакцию 1", 1);
                            transactionCubes.Commit();
                        }


                    }
                    else if (j == 0)
                    {
                        Logger.Log("   Кубик с типом Короб отсутствует в модели. Завершение работы.", 3);
                        new infowindow280("Отсутствует хотя бы 1 размещенный экземпляр семейства pmN.Условное семейство СС ПС с типом Короб. " +
                            "Разместите его в любом удобном месте в модели.").ShowDialog();
                        this.adskgProgressBar.Dispatcher.Invoke((System.Action)(() => this.adskgProgressBar.Close()));
                        return Result.Cancelled;
                    }

                    List<FamilyInstance> GMs1 = new FilteredElementCollector(RevitAPI.Document).OfCategory(BuiltInCategory.OST_GenericModel)   //фильтр по категории Об модели
                                                                                        .WhereElementIsNotElementType()
                                                                                        .OfClass(typeof(FamilyInstance))
                                                                                        .Cast<FamilyInstance>()
                                                                                        .ToList();

                    //собираем данные с коробов исходя из принципиальных типов
                    Logger.Log("Формируем данные для кубиков", 1);

                    List<ConduitCube> cubes = new List<ConduitCube>();

                    foreach (var cType in ConduitTypes)
                    {
                        Logger.Log("   " + cType, 2);

                        List<Element> cTypeElems = new List<Element>(); //пустой список коробов
                        foreach (var c in Conduit)
                        {
                            string cType1 =
                                c.get_Parameter(adskGparamGuid).AsString() +
                                c.LookupParameter("Кабель тип 1").AsElementId().IntegerValue.ToString() +
                                c.LookupParameter("Кабель тип 2").AsElementId().IntegerValue.ToString() +
                                c.LookupParameter("Кабель тип 3").AsElementId().IntegerValue.ToString() +
                                c.LookupParameter("Кабель тип 4").AsElementId().IntegerValue.ToString() +
                                c.LookupParameter("Кабель тип 5").AsElementId().IntegerValue.ToString() +
                                c.LookupParameter("Труба").AsElementId().IntegerValue.ToString() +
                                c.LookupParameter("Крепеж").AsElementId().IntegerValue.ToString();
                            if (cType1 == cType)
                            {
                                cTypeElems.Add(doc.GetElement(c.Id));
                                Logger.Log("      " + c.Id.ToString(), 2);
                            }
                        }
                        List<string> stringValues = new List<string>();
                        List<double> doubleValues = new List<double>();
                        string gvalue = "";

                        int cableCounter = 0; //счетчик для считывания кол-ва кабеля

                        for (int i = 0; i < conduitStringParams.Length; i++) //проходим по списку текстовых параметров
                        {
                            string conduitParam = conduitStringParams[i];
                            Logger.Log("   " + conduitParam, 2);
                            string value = "";
                            //получаем значение текстового параметра с первого короба в списке коробов данного типа
                            bool cParamExist = Param.ParamExist(conduitParam, cTypeElems.First());
                            if (cParamExist)
                            {
                                bool hasValue = cTypeElems.First().LookupParameter(conduitParam).HasValue;
                                if (hasValue)
                                {
                                    string cParamValue = cTypeElems.First().LookupParameter(conduitParam).AsString();
                                    if (cParamValue.Length > 0)
                                    {
                                        value = cParamValue; Logger.Log("      " + cParamValue, 2);
                                    }
                                    else Logger.Log("      пустое значение", 2);
                                }
                                else Logger.Log("      пустое значение", 2);
                            }
                            stringValues.Add(value);
                            bool gParamExist = Param.ParamExistByGuid(adskGparamGuid, cTypeElems.First());
                            if (cParamExist)
                            {
                                bool hasValue = cTypeElems.First().get_Parameter(adskGparamGuid).HasValue;
                                if (hasValue)
                                {
                                    string gParamValue = cTypeElems.First().get_Parameter(adskGparamGuid).AsString();
                                    if (gParamValue.Length > 0)
                                    {
                                        gvalue = gParamValue; Logger.Log("      " + gParamValue, 2);
                                    }
                                    else Logger.Log("      пустое значение", 2);
                                }
                                else Logger.Log("      пустое значение", 2);
                            }


                            if (i == 0 || i == 4 || i == 8 || i == 12 || i == 16) //кабели
                            {
                                cableCounter++;
                                string cableCountParam = "Кабель тип " + cableCounter.ToString() + " колво";
                                bool cableCountParamExist = Param.ParamExist(cableCountParam, cTypeElems.First());

                                double dValue = 0;
                                if (value.Length > 0)
                                {
                                    foreach (var c in cTypeElems) //прибавляем длину с каждого элемента
                                    {
                                        int cableCount = 1;
                                        if (cableCountParamExist)
                                        {
                                            if (c.LookupParameter(cableCountParam).HasValue) cableCount = c.LookupParameter(cableCountParam).AsInteger();
                                        }

                                        dValue += c.get_Parameter(BuiltInParameter.CURVE_ELEM_LENGTH).AsDouble() * 0.3048 * cableCount * conduitCoeff;
                                    }
                                }
                                doubleValues.Add(dValue);
                                Logger.Log("      " + dValue.ToString(), 2);
                            }
                            if (i == 20) //крепежи
                            {
                                double dValue = 0;
                                if (value.Length > 0)
                                {
                                    foreach (var c in cTypeElems)
                                    {
                                        dValue += (int)Math.Round(c.get_Parameter(BuiltInParameter.CURVE_ELEM_LENGTH).AsDouble() * 0.3048 * 1000 / conduitStep);
                                    }
                                }
                                doubleValues.Add(dValue);
                                Logger.Log("      " + dValue.ToString(), 2);
                            }
                            if (i == 25) //трубы
                            {
                                double dValue = 0;
                                if (value.Length > 0)
                                {
                                    foreach (var c in cTypeElems) //прибавляем длину с каждого элемента
                                    {
                                        dValue += c.get_Parameter(BuiltInParameter.CURVE_ELEM_LENGTH).AsDouble() * 0.3048 * conduitCoeffPipe;
                                    }
                                }
                                doubleValues.Add(dValue);
                                Logger.Log("      " + dValue.ToString(), 2);
                            }
                        }

                        cubes.Add(new ConduitCube { Name = cType, StringValues = stringValues, DoubleValues = doubleValues, ADSKGroup = gvalue });
                    }
                    using (Transaction transactionConduit = new Transaction(doc))
                    {
                        transactionConduit.Start("TNov - Сводная спека (короба)");

                        //создание элементов кубиков
                        Logger.Log("Транзакция 2 (короба). Создаем кубики", 1);

                        ElementId cubeElementId = new ElementId(cubeId); //нашли существующий кубик
                        FamilyInstance GM = (FamilyInstance)doc.GetElement(cubeElementId);
                        string familyName = GM.Symbol.FamilyName;
                        Element eType = doc.GetElement(GM.GetTypeId());
                        LocationPoint point = GM.Location as LocationPoint;

                        int count = 0;

                        foreach (var cc in cubes)
                        {
                            Logger.Log("   Тип " + cc.Name, 2);
                            count++;
                            XYZ newLocation = new XYZ(point.Point.X, point.Point.Y, point.Point.Z + count * 0.3048);
                            FamilyInstance instance = RevitAPI.Document.Create.NewFamilyInstance(newLocation, GM.Symbol, StructuralType.NonStructural);
                            Element newElem = RevitAPI.Document.GetElement(instance.Id);

                            Logger.Log("      Новый элемент " + instance.Id.ToString() + ", значения параметров:", 2);
                            //заполняем параметры кубика
                            for (int i = 0; i < conduitStringParams.Length; i++)
                            {
                                newElem.LookupParameter(conduitStringParams[i])?.Set(cc.StringValues[i]);
                                Logger.Log("      " + conduitStringParams[i] + ": " + cc.StringValues[i], 2);
                            }
                            for (int i = 0; i < cubeConduitDoubleParams.Length; i++)
                            {
                                newElem.LookupParameter(cubeConduitDoubleParams[i])?.Set(Math.Round(cc.DoubleValues[i], 1));
                                Logger.Log("      " + cubeConduitDoubleParams[i] + ": " + Math.Round(cc.DoubleValues[i], 1).ToString(), 2);
                            }
                            newElem.get_Parameter(adskGparamGuid)?.Set(cc.ADSKGroup);
                            PBCount++;
                            this.adskgProgressBar.TNov_ProgressBar.Dispatcher.Invoke<double>((Func<double>)(() => this.adskgProgressBar.TNov_ProgressBar.Value = (double)PBCount));
                            this.adskgProgressBar.TNov_ProgressBar.Dispatcher.Invoke<string>((Func<string>)(() => this.adskgProgressBar.value.Text = PBCount.ToString()));

                        }

                        Logger.Log("Закрываем транзакцию 2", 1);
                        transactionConduit.Commit();




                    }

                    //цепи

                    Logger.Log("Цепи. Ищем кубики", 1);

                    int j2 = 0;
                    ICollection<ElementId> GMsToRemove2 = new List<ElementId>();
                    int cubeId2 = -1;
                    foreach (FamilyInstance GM0 in GMs1)
                    {
                        Element e = RevitAPI.Document.GetElement(GM0.Id);
                        string familyName0 = GM0.Symbol.FamilyName;
                        Element eType0 = RevitAPI.Document.GetElement(e.GetTypeId());
                        if (familyName0.Contains("pmN.Условное семейство СС ПС") && eType0.Name.Contains("Цепь"))
                        {
                            j2++;
                            if (j2 == 1) //первый кубик данного типа - очищаем параметры
                            {
                                e.LookupParameter("Цепь_Кабель_Количество")?.Set(0);
                                e.LookupParameter("Цепь_Крепеж_Количество")?.Set(0);
                                e.LookupParameter("Цепь_Труба_Количество")?.Set(0);
                                cubeId2 = GM0.Id.IntegerValue;
                                Logger.Log("   Первый кубик найден и обработан", 2);
                            }
                            if (j2 > 1) GMsToRemove2.Add(GM0.Id); //последующие кубики данного типа - в список на удаление
                        }

                    }
                    if (j2 > 1)
                    {
                        using (Transaction transactionCubes2 = new Transaction(doc))
                        {
                            transactionCubes2.Start("TNov - Сводная спека (цепи кубики)");
                            Logger.Log("Открываем транзакцию 3", 1);

                            RevitAPI.Document.Delete(GMsToRemove2.ToArray());
                            Logger.Log("   Удалены остальные кубики в количестве: " + GMsToRemove2.Count.ToString(), 1);

                            Logger.Log("Закрываем транзакцию 3", 1);
                            transactionCubes2.Commit();
                        }


                    }
                    else if (j2 == 0)
                    {
                        Logger.Log("   Кубик с типом Цепь отсутствует в модели. Завершение работы.", 3);
                        new infowindow280("Отсутствует хотя бы 1 размещенный экземпляр семейства pmN.Условное семейство СС ПС с типом Цепь. " +
                            "Разместите его в любом удобном месте в модели.").ShowDialog();
                        this.adskgProgressBar.Dispatcher.Invoke((System.Action)(() => this.adskgProgressBar.Close()));
                        return Result.Cancelled;
                    }

                    List<FamilyInstance> GMs2 = new FilteredElementCollector(RevitAPI.Document).OfCategory(BuiltInCategory.OST_GenericModel)   //фильтр по категории Об модели
                                                                                        .WhereElementIsNotElementType()
                                                                                        .OfClass(typeof(FamilyInstance))
                                                                                        .Cast<FamilyInstance>()
                                                                                        .ToList();

                    //собираем данные с цепей
                    Logger.Log("Формируем данные для кубиков", 1);

                    List<ConduitCube> cubes2 = new List<ConduitCube>();

                    foreach (var ElectricalSystem in ElectricalSystems)
                    {
                        Logger.Log("   " + ElectricalSystem.Id.IntegerValue.ToString(), 2);

                        List<string> stringValues = new List<string>();
                        List<double> doubleValues = new List<double>();
                        string gvalue = "";
                        for (int i = 0; i < esystemStringParams.Length; i++) //проходим по списку текстовых параметров
                        {
                            string esystemParam = esystemStringParams[i];
                            Logger.Log("   " + esystemParam, 2);
                            string value = "";
                            //получаем значение текстового параметра с первого короба в списке коробов данного типа
                            bool cParamExist = Param.ParamExist(esystemParam, ElectricalSystem);
                            if (cParamExist)
                            {
                                bool hasValue = ElectricalSystem.LookupParameter(esystemParam).HasValue;
                                if (hasValue)
                                {
                                    string cParamValue = ElectricalSystem.LookupParameter(esystemParam).AsString();
                                    if (cParamValue.Length > 0)
                                    {
                                        value = cParamValue; Logger.Log("      " + cParamValue, 2);
                                    }
                                    else Logger.Log("      пустое значение", 2);
                                }
                                else Logger.Log("      пустое значение", 2);
                            }
                            stringValues.Add(value);
                            bool gParamExist = Param.ParamExistByGuid(adskGparamGuid, ElectricalSystem);
                            if (cParamExist)
                            {
                                bool hasValue = ElectricalSystem.get_Parameter(adskGparamGuid).HasValue;
                                if (hasValue)
                                {
                                    string gParamValue = ElectricalSystem.get_Parameter(adskGparamGuid).AsString();
                                    if (gParamValue.Length > 0)
                                    {
                                        gvalue = gParamValue; Logger.Log("      " + gParamValue, 2);
                                    }
                                    else Logger.Log("      пустое значение", 2);
                                }
                                else Logger.Log("      пустое значение", 2);
                            }

                            if (i == 0) //кабели
                            {
                                double dValue = 0;
                                if (value.Length > 0)
                                {
                                    dValue += ElectricalSystem.get_Parameter(BuiltInParameter.RBS_ELEC_CIRCUIT_LENGTH_PARAM).AsDouble() * 0.3048 * elSystemCoeffCable;

                                }
                                doubleValues.Add(dValue);
                                Logger.Log("      " + dValue.ToString(), 2);
                            }
                            if (i == 4) //крепежи
                            {
                                double dValue = 0;
                                if (value.Length > 0) //4 крепежа на 1 метр с каждого элемента
                                {
                                    dValue += (int)Math.Round(ElectricalSystem.get_Parameter(BuiltInParameter.RBS_ELEC_CIRCUIT_LENGTH_PARAM).AsDouble() * 0.3048 * 1000 / elSystemStep);

                                }
                                doubleValues.Add(dValue);
                                Logger.Log("      " + dValue.ToString(), 2);
                            }
                            if (i == 9) //трубы
                            {
                                double dValue = 0;
                                if (value.Length > 0)
                                {
                                    dValue += ElectricalSystem.get_Parameter(BuiltInParameter.RBS_ELEC_CIRCUIT_LENGTH_PARAM).AsDouble() * 0.3048 * elSystemCoeffPipe;

                                }
                                doubleValues.Add(dValue);
                                Logger.Log("      " + dValue.ToString(), 2);
                            }
                        }

                        cubes2.Add(new ConduitCube
                        {
                            Name = ElectricalSystem.Id.IntegerValue.ToString(),
                            StringValues = stringValues,
                            DoubleValues = doubleValues,
                            ADSKGroup = gvalue
                        });


                    }
                    using (Transaction transactionElectricalSystem = new Transaction(doc))
                    {
                        transactionElectricalSystem.Start("TNov - Сводная спека (цепи)");

                        //создание элементов кубиков
                        Logger.Log("Транзакция 4 (цепи). Создаем кубики", 1);

                        ElementId cubeElementId = new ElementId(cubeId2); //нашли существующий кубик
                        FamilyInstance GM = (FamilyInstance)doc.GetElement(cubeElementId);
                        string familyName = GM.Symbol.FamilyName;
                        Element eType = doc.GetElement(GM.GetTypeId());
                        LocationPoint point = GM.Location as LocationPoint;

                        int count = 0;

                        foreach (var cc in cubes2)
                        {
                            Logger.Log("   Тип " + cc.Name, 2);
                            count++;
                            XYZ newLocation = new XYZ(point.Point.X, point.Point.Y, point.Point.Z + count * 0.3048);
                            FamilyInstance instance = RevitAPI.Document.Create.NewFamilyInstance(newLocation, GM.Symbol, StructuralType.NonStructural);
                            Element newElem = RevitAPI.Document.GetElement(instance.Id);

                            Logger.Log("      Новый элемент " + instance.Id.ToString() + ", значения параметров:", 2);
                            //заполняем параметры кубика
                            for (int i = 0; i < esystemStringParams.Length; i++)
                            {
                                newElem.LookupParameter(esystemStringParams[i])?.Set(cc.StringValues[i]);
                                Logger.Log("      " + esystemStringParams[i] + ": " + cc.StringValues[i], 2);
                            }
                            for (int i = 0; i < cubeElSystemDoubleParams.Length; i++)
                            {
                                newElem.LookupParameter(cubeElSystemDoubleParams[i])?.Set(Math.Round(cc.DoubleValues[i], 1));
                                Logger.Log("      " + cubeElSystemDoubleParams[i] + ": " + Math.Round(cc.DoubleValues[i], 1).ToString(), 2);
                            }
                            newElem.get_Parameter(adskGparamGuid)?.Set(cc.ADSKGroup);

                            PBCount++;
                            this.adskgProgressBar.TNov_ProgressBar.Dispatcher.Invoke<double>((Func<double>)(() => this.adskgProgressBar.TNov_ProgressBar.Value = (double)PBCount));
                            this.adskgProgressBar.TNov_ProgressBar.Dispatcher.Invoke<string>((Func<string>)(() => this.adskgProgressBar.value.Text = PBCount.ToString()));

                        }

                        Logger.Log("Закрываем транзакцию 4", 1);
                        transactionElectricalSystem.Commit();




                    }
                    //лотки (кабель)

                    Logger.Log("Лотки (кабель). Ищем кубики", 1);

                    List<FamilyInstance> GMs3 = new FilteredElementCollector(RevitAPI.Document).OfCategory(BuiltInCategory.OST_GenericModel)   //фильтр по категории Об модели
                                                                                        .WhereElementIsNotElementType()
                                                                                        .OfClass(typeof(FamilyInstance))
                                                                                        .Cast<FamilyInstance>()
                                                                                        .ToList();

                    int k = 0;
                    ICollection<ElementId> GMsToRemove3 = new List<ElementId>();
                    int cubeId3 = -1;
                    foreach (FamilyInstance GM0 in GMs3)
                    {
                        Element e = RevitAPI.Document.GetElement(GM0.Id);
                        string familyName0 = GM0.Symbol.FamilyName;
                        Element eType0 = RevitAPI.Document.GetElement(e.GetTypeId());
                        if (familyName0.Contains("pmN.Условное семейство СС ПС") && eType0.Name.Contains("Лоток"))
                        {
                            k++;
                            if (k == 1) //первый кубик данного типа - очищаем параметры
                            {
                                e.LookupParameter("Лоток_Кабель_1_Количество")?.Set(0);
                                e.LookupParameter("Лоток_Кабель_2_Количество")?.Set(0);
                                e.LookupParameter("Лоток_Кабель_3_Количество")?.Set(0);
                                e.LookupParameter("Лоток_Кабель_4_Количество")?.Set(0);
                                e.LookupParameter("Лоток_Кабель_5_Количество")?.Set(0);
                                cubeId3 = GM0.Id.IntegerValue;
                                Logger.Log("   Первый кубик найден и обработан", 2);
                            }
                            if (k > 1) GMsToRemove3.Add(GM0.Id); //последующие кубики данного типа - в список на удаление
                        }

                    }
                    if (k > 1)
                    {
                        using (Transaction transactionCubesCT = new Transaction(doc))
                        {
                            transactionCubesCT.Start("TNov - Сводная спека (лотки кубики)");
                            Logger.Log("Открываем транзакцию 5", 1);

                            RevitAPI.Document.Delete(GMsToRemove3.ToArray());
                            Logger.Log("   Удалены остальные кубики в количестве: " + GMsToRemove3.Count.ToString(), 1);

                            Logger.Log("Закрываем транзакцию 5", 1);
                            transactionCubesCT.Commit();
                        }


                    }
                    else if (k == 0)
                    {
                        Logger.Log("   Кубик с типом Лоток отсутствует в модели. Завершение работы.", 3);
                        new infowindow280("Отсутствует хотя бы 1 размещенный экземпляр семейства pmN.Условное семейство СС ПС с типом Лоток. " +
                            "Разместите его в любом удобном месте в модели.").ShowDialog();
                        this.adskgProgressBar.Dispatcher.Invoke((System.Action)(() => this.adskgProgressBar.Close()));
                        return Result.Cancelled;
                    }

                    List<FamilyInstance> GMs4 = new FilteredElementCollector(RevitAPI.Document).OfCategory(BuiltInCategory.OST_GenericModel)   //фильтр по категории Об модели
                                                                                        .WhereElementIsNotElementType()
                                                                                        .OfClass(typeof(FamilyInstance))
                                                                                        .Cast<FamilyInstance>()
                                                                                        .ToList();

                    //собираем данные с лотков исходя из принципиальных типов
                    Logger.Log("Формируем данные для кубиков", 1);

                    List<ConduitCube> cubes3 = new List<ConduitCube>();

                    foreach (var cType in CableTrayTypes)
                    {
                        Logger.Log("   " + cType, 2);

                        List<Element> cTypeElems = new List<Element>(); //пустой список лотков
                        foreach (var c in CableTrays)
                        {
                            string cType1 =
                                c.get_Parameter(adskGparamGuid).AsString() +
                                c.LookupParameter("Кабель тип 1").AsElementId().IntegerValue.ToString() +
                                c.LookupParameter("Кабель тип 2").AsElementId().IntegerValue.ToString() +
                                c.LookupParameter("Кабель тип 3").AsElementId().IntegerValue.ToString() +
                                c.LookupParameter("Кабель тип 4").AsElementId().IntegerValue.ToString() +
                                c.LookupParameter("Кабель тип 5").AsElementId().IntegerValue.ToString();
                            if (cType1 == cType)
                            {
                                cTypeElems.Add(doc.GetElement(c.Id));
                                Logger.Log("      " + c.Id.ToString(), 2);
                            }
                        }
                        List<string> stringValues = new List<string>();
                        List<double> doubleValues = new List<double>();
                        string gvalue = "";

                        int cableCounter = 0; //счетчик для считывания кол-ва кабеля

                        for (int i = 0; i < 20; i++) //проходим по списку текстовых параметров (20 - только параметры кабеля)
                        {
                            string conduitParam = conduitStringParams[i];
                            Logger.Log("   " + conduitParam, 2);
                            string value = "";
                            //получаем значение текстового параметра с первого лотка в списке лотков данного типа
                            bool cParamExist = Param.ParamExist(conduitParam, cTypeElems.First());
                            if (cParamExist)
                            {
                                bool hasValue = cTypeElems.First().LookupParameter(conduitParam).HasValue;
                                if (hasValue)
                                {
                                    string cParamValue = cTypeElems.First().LookupParameter(conduitParam).AsString();
                                    if (cParamValue.Length > 0)
                                    {
                                        value = cParamValue; Logger.Log("      " + cParamValue, 2);
                                    }
                                    else Logger.Log("      пустое значение", 2);
                                }
                                else Logger.Log("      пустое значение", 2);
                            }
                            stringValues.Add(value);
                            bool gParamExist = Param.ParamExistByGuid(adskGparamGuid, cTypeElems.First());
                            if (cParamExist)
                            {
                                bool hasValue = cTypeElems.First().get_Parameter(adskGparamGuid).HasValue;
                                if (hasValue)
                                {
                                    string gParamValue = cTypeElems.First().get_Parameter(adskGparamGuid).AsString();
                                    if (gParamValue.Length > 0)
                                    {
                                        gvalue = gParamValue; Logger.Log("      " + gParamValue, 2);
                                    }
                                    else Logger.Log("      пустое значение", 2);
                                }
                                else Logger.Log("      пустое значение", 2);
                            }


                            if (i == 0 || i == 4 || i == 8 || i == 12 || i == 16) //кабели
                            {
                                cableCounter++;
                                string cableCountParam = "Кабель тип " + cableCounter.ToString() + " колво";
                                bool cableCountParamExist = Param.ParamExist(cableCountParam, cTypeElems.First());

                                double dValue = 0;
                                if (value.Length > 0)
                                {
                                    foreach (var c in cTypeElems) //прибавляем длину с каждого элемента
                                    {
                                        int cableCount = 1;
                                        if (cableCountParamExist)
                                        {
                                            if (c.LookupParameter(cableCountParam).HasValue) cableCount = c.LookupParameter(cableCountParam).AsInteger();
                                        }

                                        dValue += c.get_Parameter(BuiltInParameter.CURVE_ELEM_LENGTH).AsDouble() * 0.3048 * cableCount * cableTrayCoeffCable;
                                    }
                                }
                                doubleValues.Add(dValue);
                                Logger.Log("      " + dValue.ToString(), 2);
                            }

                        }

                        cubes3.Add(new ConduitCube { Name = cType, StringValues = stringValues, DoubleValues = doubleValues, ADSKGroup = gvalue });
                    }
                    using (Transaction transactionCTCable = new Transaction(doc))
                    {
                        transactionCTCable.Start("TNov - Сводная спека (кабели в лотках)");

                        //создание элементов кубиков
                        Logger.Log("Транзакция 6 (кабели в лотках). Создаем кубики", 1);

                        ElementId cubeElementId = new ElementId(cubeId3); //нашли существующий кубик
                        FamilyInstance GM = (FamilyInstance)doc.GetElement(cubeElementId);
                        string familyName = GM.Symbol.FamilyName;
                        Element eType = doc.GetElement(GM.GetTypeId());
                        LocationPoint point = GM.Location as LocationPoint;

                        int count = 0;

                        foreach (var cc in cubes3)
                        {
                            Logger.Log("   Тип " + cc.Name, 2);
                            count++;
                            XYZ newLocation = new XYZ(point.Point.X, point.Point.Y, point.Point.Z + count * 0.3048);
                            FamilyInstance instance = RevitAPI.Document.Create.NewFamilyInstance(newLocation, GM.Symbol, StructuralType.NonStructural);
                            Element newElem = RevitAPI.Document.GetElement(instance.Id);

                            Logger.Log("      Новый элемент " + instance.Id.ToString() + ", значения параметров:", 2);
                            //заполняем параметры кубика
                            for (int i = 0; i < 20; i++)
                            {
                                newElem.LookupParameter(conduitStringParams[i])?.Set(cc.StringValues[i]);
                                Logger.Log("      " + conduitStringParams[i] + ": " + cc.StringValues[i], 2);
                            }
                            for (int i = 0; i < cubeCableTrayDoubleParams.Length; i++)
                            {
                                newElem.LookupParameter(cubeCableTrayDoubleParams[i])?.Set(Math.Round(cc.DoubleValues[i], 1));
                                Logger.Log("      " + cubeCableTrayDoubleParams[i] + ": " + Math.Round(cc.DoubleValues[i], 1).ToString(), 2);
                            }
                            newElem.get_Parameter(adskGparamGuid)?.Set(cc.ADSKGroup);
                            PBCount++;
                            this.adskgProgressBar.TNov_ProgressBar.Dispatcher.Invoke<double>((Func<double>)(() => this.adskgProgressBar.TNov_ProgressBar.Value = (double)PBCount));
                            this.adskgProgressBar.TNov_ProgressBar.Dispatcher.Invoke<string>((Func<string>)(() => this.adskgProgressBar.value.Text = PBCount.ToString()));

                        }

                        Logger.Log("Закрываем транзакцию 6", 1);
                        transactionCTCable.Commit();




                    }

                    //лотки и фитинги лотков
                    Logger.Log("Лотки, фитинги лотков", 1);

                    List<Element> CTelems = new List<Element>();
                    foreach (var CT in CableTrays) CTelems.Add(doc.GetElement(CT.Id));
                    foreach (var CTF in CableTrayFittings) CTelems.Add(doc.GetElement(CTF.Id));

                    using (Transaction transactionCT = new Transaction(doc))
                    {
                        Logger.Log("Открываем транзакцию (лотки)", 1);
                        transactionCT.Start("TNov - Сводная спека Лотки");
                        foreach (var elem in CTelems)
                        {
                            Logger.Log("   " + elem.Id.IntegerValue.ToString(), 2);
                            Element type = RevitAPI.Document.GetElement(elem.GetTypeId());

                            //вычисление Наименования и Марки

                            string naimValue = ""; string markValue = "";

                            string manuf = "-";
                            if (elem.Category.Name.Contains("детали") && Param.ParamExist("ADSK_Завод-изготовитель", type))
                            {
                                if (type.LookupParameter("ADSK_Завод-изготовитель").HasValue) manuf = type.LookupParameter("ADSK_Завод-изготовитель").AsString();
                            }
                            bool IEK = manuf.Contains("IEK") || manuf.Contains("«Интерэлектрокомплект");

                            if (elem.Category.Name.Contains("детали") && IEK && Param.ParamExist("Наименование (IEK)", elem) && Param.ParamExist("Марка (IEK)", elem)) //фитинги IEK
                            {
                                naimValue = elem.LookupParameter("Наименование (IEK)").AsString();
                                markValue = elem.LookupParameter("Марка (IEK)").AsString();
                            }
                            else
                            {
                                string param1 = type.LookupParameter("Комментарии к типоразмеру").AsString();
                                if (param1 == null || param1.Length == 0) param1 = "проверьте Комментарии к типоразмеру";
                                string param2 = elem.get_Parameter(BuiltInParameter.RBS_CALCULATED_SIZE).AsString().Replace("мм", "").Replace(" ", "");
                                naimValue = param1 + " " + param2;
                            }


                            //вычисление Количества
                            double countValue = 0;
                            if (elem.Category.Name.Contains("лотки"))
                            {
                                Parameter paramL = elem.get_Parameter(BuiltInParameter.CURVE_ELEM_LENGTH);
                                if (paramL != null) countValue = paramL.AsDouble();
                                countValue = countValue * 0.3048;
                                countValue = Math.Round(countValue, 1);
                            }
                            else countValue = 1;

                            //заполнение параметров
                            //наименование
                            bool success = false;
                            bool success1 = false;
                            bool adskNparamexist = Param.ParamExist("ADSK_Наименование", elem);
                            if (adskNparamexist)
                            {
                                bool isReadOnly = elem.LookupParameter("ADSK_Наименование").IsReadOnly;
                                if (!isReadOnly)
                                {
                                    try
                                    {
                                        elem.LookupParameter("ADSK_Наименование")?.Set(naimValue);
                                        success1 = true;
                                        Logger.Log("      назначено " + naimValue, 2);
                                    }
                                    catch (Exception ex)
                                    {
                                        Logger.Log("      Ошибка: " + ex.Message, 4);
                                    }
                                }
                                else success1 = true;
                            }
                            else
                            {
                                bool adskCparamexistType = Param.ParamExist("ADSK_Наименование", type);
                                if (adskCparamexistType) //наименование назначено по типу
                                {
                                    success1 = true;
                                }
                            }
                            bool success2 = false;
                            //марка
                            bool adskMparamexist = Param.ParamExistByGuid(adskMarkparamGuid, elem);
                            if (adskMparamexist && elem.Category.Name.Contains("детали") && IEK)
                            {
                                bool isReadOnly = elem.get_Parameter(adskMarkparamGuid).IsReadOnly;
                                if (!isReadOnly)
                                {
                                    try
                                    {
                                        elem.get_Parameter(adskMarkparamGuid)?.Set(markValue);
                                        Logger.Log("      назначено " + naimValue, 2);
                                    }
                                    catch (Exception ex)
                                    {
                                        Logger.Log("      Ошибка: " + ex.Message, 4);
                                    }
                                }
                                else success1 = true;
                            }
                            //количество
                            bool adskCparamexist = Param.ParamExistByGuid(adskCparamGuid, elem);
                            if (adskCparamexist)
                            {
                                double currentC = elem.get_Parameter(adskCparamGuid).AsDouble();
                                if (countValue == 1 && currentC > 0) //количество уже назначено в семействе по экз
                                {
                                    success2 = true;
                                }
                                else
                                {
                                    try
                                    {
                                        elem.get_Parameter(adskCparamGuid)?.Set(countValue);
                                        success2 = true;
                                        Logger.Log("      назначено " + countValue.ToString(), 2);
                                    }
                                    catch (Exception ex)
                                    {
                                        Logger.Log("      Ошибка: " + ex.Message, 4);
                                    }
                                }

                            }
                            else
                            {
                                bool adskCparamexistType = Param.ParamExistByGuid(adskCparamGuid, type); //количество уже назначено в семействе по типу
                                if (adskCparamexistType)
                                {
                                    double currentC = type.get_Parameter(adskCparamGuid).AsDouble();
                                    if (countValue == 1 && currentC > 0)
                                    {
                                        success2 = true;
                                    }
                                }
                            }
                            success = success1 && success2;
                            if (!success) { failed.Add(elem.Id.IntegerValue.ToString()); failscount++; }

                            PBCount++;
                            this.adskgProgressBar.TNov_ProgressBar.Dispatcher.Invoke<double>((Func<double>)(() => this.adskgProgressBar.TNov_ProgressBar.Value = (double)PBCount));
                            this.adskgProgressBar.TNov_ProgressBar.Dispatcher.Invoke<string>((Func<string>)(() => this.adskgProgressBar.value.Text = PBCount.ToString()));

                        }
                        transactionCT.Commit();
                        Logger.Log("Закрываем транзакцию (лотки)", 1);
                    }

                    //электрооборудование + пожарная сигнализация
                    List<Element> SSelems = new List<Element>();
                    foreach (var FA in FireAlarmDevices) SSelems.Add(doc.GetElement(FA.Id));
                    foreach (var EE in elEq) SSelems.Add(doc.GetElement(EE.Id));

                    using (Transaction transactionSS = new Transaction(doc))
                    {
                        Logger.Log("Открываем транзакцию (оборудование)", 1);
                        transactionSS.Start("TNov - Сводная спека Оборудование");
                        foreach (var elem in SSelems)
                        {
                            Logger.Log("   " + elem.Id.IntegerValue.ToString(), 2);
                            Element type = RevitAPI.Document.GetElement(elem.GetTypeId());

                            //заполнение параметров
                            bool success = false;

                            bool adskCparamexist = Param.ParamExistByGuid(adskCparamGuid, elem);
                            if (adskCparamexist)
                            {
                                double currentC = elem.get_Parameter(adskCparamGuid).AsDouble();
                                if (currentC == 1) //количество назначено 1 по экз
                                {
                                    success = true;
                                }
                                else
                                {
                                    try
                                    {
                                        elem.get_Parameter(adskCparamGuid)?.Set(1);
                                        success = true;
                                        Logger.Log("      назначено 1", 2);
                                    }
                                    catch (Exception ex)
                                    {
                                        Logger.Log("      Ошибка: " + ex.Message, 4);
                                    }
                                }

                            }
                            else
                            {
                                bool adskCparamexistType = Param.ParamExistByGuid(adskCparamGuid, type); //количество назначено 1 по типу
                                if (adskCparamexistType)
                                {
                                    double currentC = type.get_Parameter(adskCparamGuid).AsDouble();
                                    if (currentC == 1) //количество назначено 1 по экз
                                    {
                                        success = true;
                                    }
                                }
                            }
                            if (!success) { failed.Add(elem.Id.IntegerValue.ToString()); failscount++; }

                            PBCount++;
                            this.adskgProgressBar.TNov_ProgressBar.Dispatcher.Invoke<double>((Func<double>)(() => this.adskgProgressBar.TNov_ProgressBar.Value = (double)PBCount));
                            this.adskgProgressBar.TNov_ProgressBar.Dispatcher.Invoke<string>((Func<string>)(() => this.adskgProgressBar.value.Text = PBCount.ToString()));

                        }
                        transactionSS.Commit();
                        Logger.Log("Закрываем транзакцию (оборудование)", 1);
                    }

                    this.adskgProgressBar.Dispatcher.Invoke((System.Action)(() => this.adskgProgressBar.Close()));

                    if (allcount == 0) new infowindow280("Нечего обрабатывать.").ShowDialog();

                    group.Assimilate();
                }





                if (failscount > 0)
                {
                    string messageF = "";

                    string failed3str = "Не заполнились параметры ADSK_Наименование или ASDK_Количество: ";
                    if (failed.Count > 0) { failed3str = failed3str + String.Join(",", failed); messageF = messageF + failed3str + ". "; }

                    Logger.Log("Открываем окно с ID проблемных элементов: " + messageF, 1);
                    // Диалоговое окно
                    var viewModel1 = new infowindowtextfieldViewModel();
                    viewModel1.headtxt = "Журнал работы:";
                    viewModel1.ids = messageF;
                    viewModel1.lowtxt = "Проверьте их вручную или посмотрите ошибки в лог-файле.";
                    var wpfview1 = new infowindowtextfield(viewModel1);
                    viewModel1.CloseRequest += (s, e) => wpfview1.Close();
                    bool? ok1 = wpfview1.ShowDialog();
                    Logger.Log(viewModel1.ids, 1);
                }


            }
            else if (el)
            {
                // ЭЛ
                
                // Найти все спецификации категории "Типовые аннотации"
                List<ViewSchedule> schedules = new FilteredElementCollector(doc)
                    .OfClass(typeof(ViewSchedule))
                    .Cast<ViewSchedule>()
                    .Where(s => s.Definition.CategoryId.IntegerValue == (int)BuiltInCategory.OST_GenericAnnotation)
                    .Where(s => s.Name.Equals(TargetScheduleName))
                    .ToList();

                if (schedules.Count == 0)
                {
                    new infowindow280($"Спецификация с именем '{TargetScheduleName}' не найдена.").ShowDialog();
                    Logger.Log($"Спецификация с именем '{TargetScheduleName}' не найдена. Завершение работы.", 4);
                    return Result.Failed;
                }

                ViewSchedule targetSchedule = schedules.First();
                //IList<ElementId> filteredElementIds = GetFilteredElementIdsFromSchedule(doc, targetSchedule);

                FilteredElementCollector collector = new FilteredElementCollector(doc, targetSchedule.Id)
    .WhereElementIsNotElementType()
    .OfCategory(BuiltInCategory.OST_GenericAnnotation); // для типовых аннотаций

                if (collector.Count() < 1 || collector == null) {Logger.Log("Типовых аннотаций в проекте не обнаружено!",4); return Result.Cancelled; }

                List<ElementId> elementIds = collector.ToElementIds().ToList();

                Logger.Log($"Найдено элементов: {elementIds.Count} : " + string.Join(", ", elementIds.Select(id => id.ToString())), 1);

                // Создаем фильтр для категории "Обобщенные модели"
                ElementCategoryFilter categoryFilter = new ElementCategoryFilter(BuiltInCategory.OST_GenericModel);

                // Получаем все элементы категории
                FilteredElementCollector collector1 = new FilteredElementCollector(doc);
                IList<Element> genericModels = collector1.WherePasses(categoryFilter).WhereElementIsNotElementType().ToElements();

                // Фильтруем по семейству и типу
                List<Element> targetElements = new List<Element>();
                foreach (Element element in genericModels)
                {
                    FamilyInstance familyInstance = element as FamilyInstance;
                    if (familyInstance != null)
                    {
                        string familyName = familyInstance.Symbol.Family.Name;
                        string typeName = familyInstance.Name;

                        if (familyName == "pmN.Неспецифицируемый материал" &&
                            typeName == "Экспортированный материал")
                        {
                            targetElements.Add(element);
                        }
                    }
                }

                if(genericModels.Count()<1|| genericModels==null) { Logger.Log("Размещенных кубиков в проекте не обнаружено!", 4); return Result.Cancelled; }

                //заполнение списка ElNonModelCube + проверка максимального колва
                Logger.Log("Заполняем список ElNonModelCube", 1);
                List<ElNonModelCube> elCubes = new List<ElNonModelCube>();

                //Сортируем элементы типовых аннотаций из collector

                int gmCounter = 0; //счетчик заполняемых семейств из collector2

                // Сортировка 1 - ADSK_Группирование (по типу)
                var elemsSortByGroup = from i in collector
                                       let type = doc.GetElement(i.GetTypeId()) //as ElementType
                                       let paramValue = type?.get_Parameter(adskGparamGuid)?.AsString() ?? ""
                                       orderby paramValue
                                       select i;
                var adskGgroups = from i in elemsSortByGroup
                                  let type = doc.GetElement(i.GetTypeId()) //as ElementType
                                  let paramValue = type?.get_Parameter(adskGparamGuid)?.AsString() ?? ""
                                  group i by paramValue;
                foreach (var adskGgroup in adskGgroups)
                {
                    //тип первого элемента
                    Element firstElemType = doc.GetElement(adskGgroup.First().GetTypeId());
                    Logger.Log("1 : "+firstElemType.get_Parameter(adskGparamGuid)?.AsString(), 2);
                    // Сортировка 2 - N_ЭЛ. Группирование ЭЛ
                    var elemsSortByEGroup = from i in adskGgroup
                                            orderby i.get_Parameter(NEGparamGuid)?.AsString() ?? ""
                                            select i;
                    var NEGgroups = from i in elemsSortByEGroup
                                    group i by i.get_Parameter(NEGparamGuid)?.AsString() ?? "";
                    foreach (var NEGgroup in NEGgroups)
                    {
                        //тип первого элемента
                        Element firstElem2 = doc.GetElement(NEGgroup.First().Id);
                        Logger.Log("2 : "+firstElem2.get_Parameter(NEGparamGuid)?.AsString(), 2);
                        // Сортировка 3 - N_Сортировка (по типу)
                        var elemsSortByNSort = from i in NEGgroup
                                               let type = doc.GetElement(i.GetTypeId()) //as ElementType
                                               let paramValue = type?.get_Parameter(NSortparamGuid)?.AsString() ?? ""
                                               orderby paramValue descending //обратная
                                               select i;
                        var Nsortgroups = from i in elemsSortByNSort
                                          let type = doc.GetElement(i.GetTypeId()) //as ElementType
                                          let paramValue = type?.get_Parameter(NSortparamGuid)?.AsString() ?? ""
                                          group i by paramValue;
                        foreach (var Nsortgroup in Nsortgroups)
                        {
                            //тип первого элемента
                            Element firstElemType3 = doc.GetElement(Nsortgroup.First().GetTypeId());
                            Logger.Log("3 : " + firstElemType3.get_Parameter(NSortparamGuid)?.AsString(), 2);
                            // Сортировка 4 - ADSK_Наименование 
                            var elemsSortByNaim = from i in Nsortgroup
                                                  orderby i.get_Parameter(adskNparamGuid)?.AsString() ?? ""
                                                  select i;
                            var adskNgroups = from i in elemsSortByNaim
                                              group i by i.get_Parameter(adskNparamGuid)?.AsString() ?? "";

                            foreach (var adskNgroup in adskNgroups)
                            {
                                Element firstElem = adskNgroup.First();
                                Element firstElementType = doc.GetElement(firstElem.GetTypeId());
                                Logger.Log("4 : " + firstElem.get_Parameter(adskNparamGuid)?.AsString(), 2);

                                double adskNgroupCount = 0;
                                foreach(var elem in adskNgroup)
                                {
                                    adskNgroupCount += elem.get_Parameter(adskCparamGuid)?.AsDouble() ?? 0;
                                }

                                string naim = firstElem.get_Parameter(adskNparamGuid)?.AsString() ?? "";
                                string mrk = firstElem.get_Parameter(adskMarkparamGuid)?.AsString() ?? "";
                                if (mrk.Contains("ВВГ")) naim = naim.Replace(".", ",");

                                ElNonModelCube elCube = new ElNonModelCube
                                {
                                    adskGroup = firstElementType.get_Parameter(adskGparamGuid)?.AsString() ?? "",
                                    adskNaim = naim,
                                    adskMark = firstElem.get_Parameter(adskMarkparamGuid)?.AsString() ?? "",
                                    adskObozn = firstElem.get_Parameter(adskOboznparamGuid)?.AsString() ?? "",
                                    adskCode = firstElem.get_Parameter(adskCodeparamGuid)?.AsString() ?? "",
                                    adskManuf = firstElem.get_Parameter(adskManufparamGuid)?.AsString() ?? "",
                                    adskEd = firstElem.get_Parameter(adskEdparamGuid)?.AsString() ?? "",
                                    NEGroup = firstElem.get_Parameter(NEGparamGuid)?.AsString() ?? "",
                                    NSort = firstElementType.get_Parameter(NSortparamGuid)?.AsString() ?? "",
                                    adskC = adskNgroupCount,
                                };
                                Logger.Log("№ "+gmCounter.ToString()+" : "+elCube.adskGroup + " "+elCube.NEGroup + " "+elCube.NSort+ " " + elCube.adskNaim + " " + elCube.adskMark + " " + elCube.adskObozn +
                                    " " + elCube.adskCode + " " + elCube.adskEd + " " + elCube.adskC.ToString(), 2);
                                elCubes.Add(elCube);

                                gmCounter++;
                            }
                        }
                    }
                }

                if (gmCounter > collector1.Count())
                {
                    Logger.Log("Размещенных кубиков недостаточно. Завершение работы.", 4);
                    new infowindow280("В модели размещено " + collector1.Count().ToString() + " кубиков, а требуется не менее " + gmCounter.ToString() + ". Разместите больше кубиков.").ShowDialog();
                    return Result.Cancelled;
                }

                //транзакция
                using (Transaction transaction = new Transaction(doc))
                {
                    transaction.Start("TNov Сводная спека");
                    Logger.Log("Открываем транзакцию", 1);

                    for (int i = 0; i < elCubes.Count(); i++)
                    {
                        Logger.Log("индекс "+i.ToString()+" ", 2);
                        //ищем в targetElements элемент с индексом i
                        Element targetElem = doc.GetElement(targetElements[i].Id);
                        targetElem.get_Parameter(adskGparamGuid)?.Set(elCubes[i].adskGroup);//ADSK_Группирование
                        targetElem.get_Parameter(adskNparamGuid)?.Set(elCubes[i].adskNaim);//ADSK_Наименование
                        targetElem.get_Parameter(adskMarkparamGuid)?.Set(elCubes[i].adskMark);//ADSK_Марка
                        //targetElem.get_Parameter(adskOboznparamGuid)?.Set(elCubes[i].adskObozn);//ADSK_Обозначение
                        targetElem.get_Parameter(adskCodeparamGuid)?.Set(elCubes[i].adskCode);//ADSK_Код изделия
                        targetElem.get_Parameter(adskManufparamGuid)?.Set(elCubes[i].adskManuf);//ADSK_Завод-изготовитель
                        targetElem.get_Parameter(adskEdparamGuid)?.Set(elCubes[i].adskEd);//ADSK_Единица измерения
                        targetElem.get_Parameter(adskCparamGuid)?.Set(elCubes[i].adskC);//ADSK_Количество
                        targetElem.get_Parameter(NEGparamGuid)?.Set(elCubes[i].NEGroup);//N_ЭЛ.Группирование ЭЛ
                        targetElem.get_Parameter(NSortparamGuid)?.Set(elCubes[i].NSort);//N_Сортировка
                    }
                    Logger.Log("Обнуление оставшихся элементов", 1);
                    for (int i = elCubes.Count(); i < targetElements.Count(); i++) //обнуляем оставшиеся элементы
                    {
                        Element targetElem = doc.GetElement(targetElements[i].Id);
                        targetElem.get_Parameter(adskGparamGuid)?.Set("");//ADSK_Группирование
                        targetElem.get_Parameter(adskNparamGuid)?.Set("");//ADSK_Наименование
                        targetElem.get_Parameter(adskMarkparamGuid)?.Set("");//ADSK_Марка
                        //targetElem.get_Parameter(adskOboznparamGuid)?.Set("");//ADSK_Обозначение
                        targetElem.get_Parameter(adskCodeparamGuid)?.Set("");//ADSK_Код изделия
                        targetElem.get_Parameter(adskManufparamGuid)?.Set("");//ADSK_Завод-изготовитель
                        targetElem.get_Parameter(adskEdparamGuid)?.Set("");//ADSK_Единица измерения
                        targetElem.get_Parameter(adskCparamGuid)?.Set(0);//ADSK_Количество
                        targetElem.get_Parameter(NEGparamGuid)?.Set("");//N_ЭЛ.Группирование ЭЛ
                        targetElem.get_Parameter(NSortparamGuid)?.Set("");//N_Сортировка
                    }

                    Logger.Log("Закрываем транзакцию", 1);
                    transaction.Commit();
                }
            }
            else if (vkov)
            {
                //ВК ОВ

                //Получаем элементы модели

                List<Element> ArmVozd = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_DuctAccessory)
                    .WhereElementIsNotElementType()
                    .Cast<Element>()
                    .ToList();

                List<Element> Vozdrasp = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_DuctTerminal)
                    .WhereElementIsNotElementType()
                    .Cast<Element>()
                    .ToList();

                List<Element> GibkVozd = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_FlexDuctCurves)
                    .WhereElementIsNotElementType()
                    .Cast<Element>()
                    .ToList();

                List<Element> VnIsolVozd = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_DuctLinings)
                    .WhereElementIsNotElementType()
                    .Cast<Element>()
                    .ToList();

                List<Element> Vozd = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_DuctCurves)
                    .WhereElementIsNotElementType()
                    .Cast<Element>()
                    .ToList();

                List<Element> IsolVozd = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_DuctInsulations)
                    .WhereElementIsNotElementType()
                    .Cast<Element>()
                    .ToList();

                List<Element> FitVozd = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_DuctFitting)
                    .WhereElementIsNotElementType()
                    .Cast<Element>()
                    .ToList();

                List<Element> Obor = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_MechanicalEquipment)
                    .WhereElementIsNotElementType()
                    .Cast<Element>()
                    .ToList();

                List<Element> ArmTrub = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_PipeAccessory)
                    .WhereElementIsNotElementType()
                    .Cast<Element>()
                    .ToList();

                List<Element> GibkTrub = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_FlexPipeCurves)
                    .WhereElementIsNotElementType()
                    .Cast<Element>()
                    .ToList();

                List<Element> Trub = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_PipeCurves)
                    .WhereElementIsNotElementType()
                    .Cast<Element>()
                    .ToList();

                List<Element> IsolTrub = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_PipeInsulations)
                    .WhereElementIsNotElementType()
                    .Cast<Element>()
                    .ToList();

                List<Element> FitTrub = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_PipeFitting)
                    .WhereElementIsNotElementType()
                    .Cast<Element>()
                    .ToList();

                List<Element> Santeh = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_PlumbingFixtures)
                    .WhereElementIsNotElementType()
                    .Cast<Element>()
                    .ToList();

                List<Element> GMs = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_GenericModel)   //фильтр по категории Об модели
                                                                             .WhereElementIsNotElementType()
                                                                             .OfClass(typeof(FamilyInstance))
                                                                             .Cast<Element>()
                                                                             .ToList();

                List<Element> rebar = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_Rebar)   //Несущая арматура семействами
                                                                             .WhereElementIsNotElementType()
                                                                             .OfClass(typeof(FamilyInstance))
                                                                             .Cast<Element>()
                                                                             .ToList();

                //собираем типы для заполнения наим и колва
                List<string> typeMarks = new List<string>();

                List<string> pipeTypeMarks = new List<string>();
                List<Element> pipeTypes = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_PipeCurves) //типы труб
                    .WhereElementIsElementType()
                    .Cast<Element>()
                    .ToList();
                if (pipeTypes.Count > 0)
                {
                    foreach (var type in pipeTypes)
                    {
                        string str0 = "Трубы";
                        string mark = type.get_Parameter(BuiltInParameter.WINDOW_TYPE_ID).AsString();
                        if (mark != null)
                        {
                            if (mark.Length > 0) pipeTypeMarks.Add(str0 + "_" + mark);
                            else pipeTypeMarks.Add(str0 + "_пустая маркировка");
                        }
                    }
                    if (pipeTypeMarks.Count == 0) pipeTypeMarks.Add("Трубы" + "_пустая маркировка");
                }
                pipeTypeMarks = pipeTypeMarks.Distinct().ToList(); foreach (var t in pipeTypeMarks) typeMarks.Add(t);

                List<string> pipeInsulationTypeMarks = new List<string>();
                List<Element> pipeInsulationTypes = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_PipeInsulations) //типы изоляции труб
                    .WhereElementIsElementType()
                    .Cast<Element>()
                    .ToList();
                if (pipeInsulationTypes.Count > 0)
                {
                    foreach (var type in pipeInsulationTypes)
                    {
                        string str0 = "Материалы изоляции труб";
                        string mark = type.get_Parameter(BuiltInParameter.WINDOW_TYPE_ID).AsString();
                        if (mark != null)
                        {
                            if (mark.Length > 0) pipeInsulationTypeMarks.Add(str0 + "_" + mark);
                            else pipeInsulationTypeMarks.Add(str0 + "_пустая маркировка");
                        }
                    }
                    if (pipeInsulationTypeMarks.Count == 0) pipeInsulationTypeMarks.Add("Материалы изоляции труб" + "_пустая маркировка");
                }
                pipeInsulationTypeMarks = pipeInsulationTypeMarks.Distinct().ToList(); foreach (var t in pipeInsulationTypeMarks) typeMarks.Add(t);

                List<string> pipeFlexTypeMarks = new List<string>();
                List<Element> pipeFlexTypes = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_FlexPipeCurves) //типы гибких труб
                    .WhereElementIsElementType()
                    .Cast<Element>()
                    .ToList();
                if (pipeFlexTypes.Count > 0)
                {
                    foreach (var type in pipeFlexTypes)
                    {
                        string str0 = "Гибкие трубы";
                        string mark = type.get_Parameter(BuiltInParameter.WINDOW_TYPE_ID).AsString();
                        if (mark != null)
                        {
                            if (mark.Length > 0) pipeFlexTypeMarks.Add(str0 + "_" + mark);
                            else pipeFlexTypeMarks.Add(str0 + "_пустая маркировка");
                        }
                    }
                    if (pipeFlexTypeMarks.Count == 0) pipeFlexTypeMarks.Add("Гибкие трубы" + "_пустая маркировка");
                }
                pipeFlexTypeMarks = pipeFlexTypeMarks.Distinct().ToList(); foreach (var t in pipeFlexTypeMarks) typeMarks.Add(t);

                List<string> ductTypeMarks = new List<string>();
                List<Element> ductTypes = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_DuctCurves) //типы возд
                    .WhereElementIsElementType()
                    .Cast<Element>()
                    .ToList();
                if (ductTypes.Count > 0)
                {
                    foreach (var type in ductTypes)
                    {
                        string str0 = "Воздуховоды";
                        string mark = type.get_Parameter(BuiltInParameter.WINDOW_TYPE_ID).AsString();
                        if (mark != null)
                        {
                            if (mark.Length > 0) ductTypeMarks.Add(str0 + "_" + mark);
                            else ductTypeMarks.Add(str0 + "_пустая маркировка");
                        }
                    }
                    if (ductTypeMarks.Count == 0) ductTypeMarks.Add("Воздуховоды" + "_пустая маркировка");
                }
                ductTypeMarks = ductTypeMarks.Distinct().ToList(); foreach (var t in ductTypeMarks) typeMarks.Add(t);

                List<string> ductInsulationTypeMarks = new List<string>();
                List<Element> ductInsulationTypes = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_DuctInsulations) //типы изоляции возд
                    .WhereElementIsElementType()
                    .Cast<Element>()
                    .ToList();
                if (ductTypes.Count > 0)
                {
                    foreach (var type in ductInsulationTypes)
                    {
                        string str0 = "Материалы изоляции воздуховодов";
                        string mark = type.get_Parameter(BuiltInParameter.WINDOW_TYPE_ID).AsString();
                        if (mark != null)
                        {
                            if (mark.Length > 0) ductInsulationTypeMarks.Add(str0 + "_" + mark);
                            else ductInsulationTypeMarks.Add(str0 + "_пустая маркировка");
                        }
                    }
                    if (ductInsulationTypeMarks.Count == 0) ductInsulationTypeMarks.Add("Материалы изоляции воздуховодов" + "_пустая маркировка");
                }
                ductInsulationTypeMarks = ductInsulationTypeMarks.Distinct().ToList(); foreach (var t in ductInsulationTypeMarks) typeMarks.Add(t);

                List<string> ductFittingTypeMarks = new List<string>();
                List<Element> ductFittingTypes = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_DuctFitting) //типы соед возд
                    .WhereElementIsElementType()
                    .Cast<Element>()
                    .ToList();
                if (ductFittingTypes.Count > 0)
                {
                    foreach (var type in ductFittingTypes)
                    {
                        string str0 = "Соединительные детали воздуховодов";
                        string mark = type.get_Parameter(BuiltInParameter.WINDOW_TYPE_ID).AsString();
                        if (mark != null)
                        {
                            if (mark.Length > 0) ductFittingTypeMarks.Add(str0 + "_" + mark);
                            else ductFittingTypeMarks.Add(str0 + "_пустая маркировка");
                        }
                    }
                    if (ductFittingTypeMarks.Count == 0) ductFittingTypeMarks.Add("Соединительные детали воздуховодов" + "_пустая маркировка");
                }
                ductFittingTypeMarks = ductFittingTypeMarks.Distinct().ToList(); foreach (var t in ductFittingTypeMarks) typeMarks.Add(t);

                //итоговый список типов
                string typeMarksString = "";
                foreach (var type in typeMarks) typeMarksString = typeMarksString + type + "|";

                int failscount = 0;
                List<string> failed1 = new List<string>(); List<string> failed2 = new List<string>(); List<string> failed3 = new List<string>(); List<string> failed4 = new List<string>();

                // Диалоговое окно
                Logger.Log("Элементы собраны. Диалог", 1);

                var viewModel = new MEPSpecOVVKViewModel();
                // Десериализация
                bool forProject = true;
                json js = new json(in TNovClassName, in forProject, out bool canserialize, out string jsonpath);
                if (canserialize)
                {
                    viewModel = JsonConvert.DeserializeObject<MEPSpecOVVKViewModel>(File.ReadAllText(jsonpath));
                    Logger.Log("Десериализация прошла успешно", 1);
                }
                if (typeMarksString.Length == 0)
                {
                    new infowindow280("В данной модели отсутствуют элементы для обработки!").ShowDialog();
                    Logger.Log("Элементы в модели отсутствуют. Завершение работы.", 3); return Result.Cancelled;
                }
                //данные вне сериализации
                viewModel.types = typeMarksString;
                viewModel.visibility1 = ArmVozd.Count > 0;
                viewModel.visibility2 = Vozdrasp.Count > 0;
                viewModel.visibility3 = GibkVozd.Count > 0;
                viewModel.visibility4 = VnIsolVozd.Count > 0;
                viewModel.visibility5 = Vozd.Count > 0;
                viewModel.visibility6 = IsolVozd.Count > 0;
                viewModel.visibility7 = FitVozd.Count > 0;
                viewModel.visibility8 = Obor.Count > 0;
                viewModel.visibility9 = ArmTrub.Count > 0;
                viewModel.visibility10 = GibkTrub.Count > 0;
                viewModel.visibility11 = Trub.Count > 0;
                viewModel.visibility12 = IsolTrub.Count > 0;
                viewModel.visibility13 = FitTrub.Count > 0;
                viewModel.visibility14 = Santeh.Count > 0;
                //окно
                var wpfview = new MEPSpecOVVKWPF(viewModel);
                viewModel.CloseRequest += (s, e) => wpfview.Close();
                bool? ok = wpfview.ShowDialog();
                if (ok != null && ok == true) { }
                else { Logger.Log("Запуск отменен пользователем. Завершение работы.", 3); return Result.Cancelled; }
                //Сериализация
                try
                {
                    File.WriteAllText(jsonpath, JsonConvert.SerializeObject(viewModel));
                    Logger.Log("Сериализация прошла успешно", 1);
                }
                catch (Exception ex) { Logger.Log("Ошибка при сериализации: " + ex.Message, 4); }


                //сценарий
                if (viewModel.selection == 2)
                {
                    List<int> selectedIds = new List<int>();
                    //анализ текущей выборки
                    Logger.Log("Анализ текущей выборки", 1);
                    Autodesk.Revit.UI.Selection.Selection selection = commandData.Application.ActiveUIDocument.Selection;
                    ICollection<ElementId> preselectedIds = selection.GetElementIds();
                    if (preselectedIds.Count > 0)
                    {
                        foreach (ElementId id in preselectedIds) { selectedIds.Add(id.IntegerValue); }
                    }
                    else //запускаем выбор элементов если ничего не выбрано
                    {
                        Selection elemselection = uidoc.Selection;

                        List<Element> selectedElements = null;


                        try
                        {
                            selectedElements = elemselection.PickElementsByRectangle("Выберите элементы при помощи рамки (Esc - отмена)").ToList();
                        }
                        catch (Autodesk.Revit.Exceptions.OperationCanceledException e)
                        {
                            Logger.Log("Запуск отменен пользователем. Завершение работы: " + e.Message, 3);
                            return Result.Cancelled;
                        }
                        foreach (Element element in selectedElements) selectedIds.Add(element.Id.IntegerValue);
                    }

                    ArmVozd = ArmVozd.Where(e => selectedIds.Contains(e.Id.IntegerValue)).ToList();
                    Vozdrasp = Vozdrasp.Where(e => selectedIds.Contains(e.Id.IntegerValue)).ToList();
                    GibkVozd = GibkVozd.Where(e => selectedIds.Contains(e.Id.IntegerValue)).ToList();
                    VnIsolVozd = VnIsolVozd.Where(e => selectedIds.Contains(e.Id.IntegerValue)).ToList();
                    Vozd = Vozd.Where(e => selectedIds.Contains(e.Id.IntegerValue)).ToList();
                    IsolVozd = IsolVozd.Where(e => selectedIds.Contains(e.Id.IntegerValue)).ToList();
                    FitVozd = FitVozd.Where(e => selectedIds.Contains(e.Id.IntegerValue)).ToList();
                    Obor = Obor.Where(e => selectedIds.Contains(e.Id.IntegerValue)).ToList();
                    ArmTrub = ArmTrub.Where(e => selectedIds.Contains(e.Id.IntegerValue)).ToList();
                    GibkTrub = GibkTrub.Where(e => selectedIds.Contains(e.Id.IntegerValue)).ToList();
                    Trub = Trub.Where(e => selectedIds.Contains(e.Id.IntegerValue)).ToList();
                    IsolTrub = IsolTrub.Where(e => selectedIds.Contains(e.Id.IntegerValue)).ToList();
                    FitTrub = FitTrub.Where(e => selectedIds.Contains(e.Id.IntegerValue)).ToList();
                    Santeh = Santeh.Where(e => selectedIds.Contains(e.Id.IntegerValue)).ToList();
                    GMs = GMs.Where(e => selectedIds.Contains(e.Id.IntegerValue)).ToList();
                    rebar = rebar.Where(e => selectedIds.Contains(e.Id.IntegerValue)).ToList();
                }

                //проверка Excel
                Microsoft.Office.Interop.Excel.Application xlApp = null;
                Workbooks workbooks = null;
                Workbook wb0 = null;
                try
                {
                    xlApp = new Microsoft.Office.Interop.Excel.Application();
                    // проверка установлен ли Excel
                    if (xlApp == null)
                    {
                        string info2txt = "Ошибка! MS Excel не установлен на данном компьютере.";
                        var info2 = new infowindow280(info2txt); info2.ShowDialog();
                        Logger.Log("MS Excel не установлен на данном компьютере.", 4);
                    }
                    workbooks = xlApp.Workbooks;
                    wb0 = workbooks.Open("//fs-nova/NOVA/04_БИБЛИОТЕКА/BIM/ВК_ОВ_Семейства/_TNov/VM_PEX_Спецификация труб.xlsx", 0, true, 5, "", "", false, Microsoft.Office.Interop.Excel.XlPlatform.xlWindows, "", true, false, 0, true, false, false);
                }
                catch (Exception e)
                {
                    string info2txt = "Ошибка! Имеются неполадки в работе MS Excel на данном компьютере. " +
                        "Перезапустите плагин без галочек Обрабатывать каталоги и Построить немоделируемые." +
                        "Этот функционал доступен только при нормальной работе Excel.";
                    var info2 = new infowindow280(info2txt); info2.ShowDialog();
                    Logger.Log(e.Message, 4);
                }
                finally
                {
                    wb0.Close();
                    xlApp.Quit();
                    Marshal.ReleaseComObject(wb0);
                    Marshal.ReleaseComObject(workbooks);
                    Marshal.ReleaseComObject(xlApp);
                }

                //переменные запусков
                bool runadsk = viewModel.runadskg;
                bool runadskp = viewModel.runadskp;
                bool runncat = viewModel.runNCat;
                bool runcatalogs = viewModel.runCatalogs;
                //исходные параметры для группирования
                string parArmVozd = viewModel.output1; string parVozdrasp = viewModel.output2; string parGibkVozd = viewModel.output3;
                string parVnIsolVozd = viewModel.output4; string parVozd = viewModel.output5; string parIsolVozd = viewModel.output6;
                string parFitVozd = viewModel.output7; string parObor = "Имя системы"; string parArmTrub = viewModel.output9;
                string parGibkTrub = viewModel.output10; string parTrub = viewModel.output11; string parIsolTrub = viewModel.output12;
                string parFitTrub = viewModel.output13; string parSanteh = viewModel.output14;
                bool systemcut = viewModel.systemcut;

                int count1 = ArmVozd.Count; if (viewModel.run1 == false && viewModel.runNCat == false) count1 = 0;
                int count2 = Vozdrasp.Count; if (viewModel.run2 == false && viewModel.runNCat == false) count2 = 0;
                int count3 = GibkVozd.Count; if (viewModel.run3 == false && viewModel.runNCat == false) count3 = 0;
                int count4 = VnIsolVozd.Count; if (viewModel.run4 == false && viewModel.runNCat == false) count4 = 0;
                int count5 = Vozd.Count; if (viewModel.run5 == false && viewModel.runNCat == false) count5 = 0;
                int count6 = IsolVozd.Count; if (viewModel.run6 == false && viewModel.runNCat == false) count6 = 0;
                int count7 = FitVozd.Count; if (viewModel.run7 == false && viewModel.runNCat == false) count7 = 0;
                int count8 = Obor.Count; if (viewModel.run8 == false && viewModel.runNCat == false) count8 = 0;
                int count9 = ArmTrub.Count; if (viewModel.run9 == false && viewModel.runNCat == false) count9 = 0;
                int count10 = GibkTrub.Count; if (viewModel.run10 == false && viewModel.runNCat == false) count10 = 0;
                int count11 = Trub.Count; if (viewModel.run11 == false && viewModel.runNCat == false) count11 = 0;
                int count12 = IsolTrub.Count; if (viewModel.run12 == false && viewModel.runNCat == false) count12 = 0;
                int count13 = FitTrub.Count; if (viewModel.run13 == false && viewModel.runNCat == false) count13 = 0;
                int count14 = Santeh.Count; if (viewModel.run14 == false && viewModel.runNCat == false) count14 = 0;
                int countGM = GMs.Count; if (viewModel.runNCat == false) countGM = 0;
                int countRebar = rebar.Count; if (viewModel.runNCat == false) countRebar = 0;

                int allcount = count1 + count2 + count3 + count4 + count5 + count6 + count7 + count8 + count9 + count10 + count11 + count12 + count13 + count14 + countGM + countRebar;

                if (viewModel.runadskg || viewModel.runNCat || viewModel.runadskp)
                {
                    using (Transaction transaction = new Transaction(doc))
                    {
                        TransactionHandler.SetWarningResolver(transaction);
                        transaction.Start("TNov - Сводная спека");
                        Logger.Log("Открываем транзакцию", 1);


                        Thread thread = new Thread(new ThreadStart(this.ThreadStartingPoint));
                        thread.SetApartmentState(ApartmentState.STA);
                        thread.IsBackground = true;
                        thread.Start();
                        Thread.Sleep(100);


                        int PBCount = 0;
                        this.adskgProgressBar.TNov_ProgressBar.Dispatcher.Invoke<double>((Func<double>)(() => this.adskgProgressBar.TNov_ProgressBar.Minimum = (double)PBCount));
                        this.adskgProgressBar.TNov_ProgressBar.Dispatcher.Invoke<string>((Func<string>)(() => this.adskgProgressBar.value.Text = PBCount.ToString()));
                        this.adskgProgressBar.TNov_ProgressBar.Dispatcher.Invoke<double>((Func<double>)(() => this.adskgProgressBar.TNov_ProgressBar.Maximum = (double)allcount));
                        this.adskgProgressBar.TNov_ProgressBar.Dispatcher.Invoke<string>((Func<string>)(() => this.adskgProgressBar.maxvalue.Text = allcount.ToString()));


                        Logger.Log("Арматура воздуховодов:", 1);
                        if (ArmVozd.Count > 0 && viewModel.run1)
                        {
                            foreach (Element a in ArmVozd)
                            {
                                PBCount++;
                                this.adskgProgressBar.TNov_ProgressBar.Dispatcher.Invoke<double>((Func<double>)(() => this.adskgProgressBar.TNov_ProgressBar.Value = (double)PBCount));
                                this.adskgProgressBar.TNov_ProgressBar.Dispatcher.Invoke<string>((Func<string>)(() => this.adskgProgressBar.value.Text = PBCount.ToString()));
                                string param1 = parArmVozd;
                                bool success1 = true;
                                if (runadsk) Setadskgparam(a.Id, param1, systemcut, out success1);
                                if (!success1) { failed1.Add(a.Id.ToString()); failscount++; }
                                bool success2 = true;
                                if (runncat) SetNCategory(a.Id, out success2);
                                if (!success2) { failed2.Add(a.Id.ToString()); failscount++; }
                            }
                        }
                        else { Logger.Log("ВЫКЛЮЧЕНО", 1); }

                        Logger.Log("Воздухораспределители:", 1);
                        if (Vozdrasp.Count > 0 && viewModel.run2)
                        {
                            foreach (Element a in Vozdrasp)
                            {
                                PBCount++;
                                this.adskgProgressBar.TNov_ProgressBar.Dispatcher.Invoke<double>((Func<double>)(() => this.adskgProgressBar.TNov_ProgressBar.Value = (double)PBCount));
                                this.adskgProgressBar.TNov_ProgressBar.Dispatcher.Invoke<string>((Func<string>)(() => this.adskgProgressBar.value.Text = PBCount.ToString()));
                                string param1 = parVozdrasp;
                                bool success1 = true;
                                if (runadsk) Setadskgparam(a.Id, param1, systemcut, out success1);
                                if (!success1) { failed1.Add(a.Id.ToString()); failscount++; }
                                bool success2 = true;
                                if (runncat) SetNCategory(a.Id, out success2);
                                if (!success2) { failed2.Add(a.Id.ToString()); failscount++; }
                            }
                        }
                        else { Logger.Log("ВЫКЛЮЧЕНО", 1); }

                        Logger.Log("Гибкие воздуховоды:", 1);
                        if (GibkVozd.Count > 0 && viewModel.run3)
                        {
                            string cat = "Гибкие воздуховоды";
                            foreach (Element a in GibkVozd)
                            {
                                PBCount++;
                                this.adskgProgressBar.TNov_ProgressBar.Dispatcher.Invoke<double>((Func<double>)(() => this.adskgProgressBar.TNov_ProgressBar.Value = (double)PBCount));
                                this.adskgProgressBar.TNov_ProgressBar.Dispatcher.Invoke<string>((Func<string>)(() => this.adskgProgressBar.value.Text = PBCount.ToString()));
                                string param1 = parGibkVozd;
                                bool success1 = true;
                                if (runadsk) Setadskgparam(a.Id, param1, systemcut, out success1);
                                if (!success1) { failed1.Add(a.Id.ToString()); failscount++; }
                                bool success2 = true;
                                if (runncat) SetNCategory(a.Id, out success2);
                                if (!success2) { failed2.Add(a.Id.ToString()); failscount++; }
                                bool success3 = true;
                                if (runadskp) Setadskpparam(a.Id, cat, fileName, out success3);
                                if (!success3) { failed3.Add(a.Id.ToString()); failscount++; }
                            }
                        }
                        else { Logger.Log("ВЫКЛЮЧЕНО", 1); }

                        Logger.Log("Внутр изоляция возд:", 1);
                        if (VnIsolVozd.Count > 0 && viewModel.run4)
                        {
                            foreach (Element a in VnIsolVozd)
                            {
                                PBCount++;
                                this.adskgProgressBar.TNov_ProgressBar.Dispatcher.Invoke<double>((Func<double>)(() => this.adskgProgressBar.TNov_ProgressBar.Value = (double)PBCount));
                                this.adskgProgressBar.TNov_ProgressBar.Dispatcher.Invoke<string>((Func<string>)(() => this.adskgProgressBar.value.Text = PBCount.ToString()));
                                string param1 = parVnIsolVozd;
                                bool success1 = true;
                                if (runadsk) Setadskgparam(a.Id, param1, systemcut, out success1);
                                if (!success1) { failed1.Add(a.Id.ToString()); failscount++; }
                                bool success2 = true;
                                if (runncat) SetNCategory(a.Id, out success2);
                                if (!success2) { failed2.Add(a.Id.ToString()); failscount++; }
                            }
                        }
                        else { Logger.Log("ВЫКЛЮЧЕНО", 1); }

                        Logger.Log("Воздуховоды:", 1);
                        if (Vozd.Count > 0 && viewModel.run5)
                        {
                            string cat = "Воздуховоды";
                            foreach (Element a in Vozd)
                            {
                                PBCount++;
                                this.adskgProgressBar.TNov_ProgressBar.Dispatcher.Invoke<double>((Func<double>)(() => this.adskgProgressBar.TNov_ProgressBar.Value = (double)PBCount));
                                this.adskgProgressBar.TNov_ProgressBar.Dispatcher.Invoke<string>((Func<string>)(() => this.adskgProgressBar.value.Text = PBCount.ToString()));
                                string param1 = parVozd;
                                bool success1 = true;
                                if (runadsk) Setadskgparam(a.Id, param1, systemcut, out success1);
                                if (!success1) { failed1.Add(a.Id.ToString()); failscount++; }
                                bool success2 = true;
                                if (runncat) SetNCategory(a.Id, out success2);
                                if (!success2) { failed2.Add(a.Id.ToString()); failscount++; }
                                bool success3 = true;
                                if (runadskp) Setadskpparam(a.Id, cat, fileName, out success3);
                                if (!success3) { failed3.Add(a.Id.ToString()); failscount++; }
                            }
                        }
                        else { Logger.Log("ВЫКЛЮЧЕНО", 1); }

                        Logger.Log("Изоляция возд:", 1);
                        if (IsolVozd.Count > 0 && viewModel.run6)
                        {
                            string cat = "Материалы изоляции воздуховодов";
                            foreach (Element a in IsolVozd)
                            {
                                PBCount++;
                                this.adskgProgressBar.TNov_ProgressBar.Dispatcher.Invoke<double>((Func<double>)(() => this.adskgProgressBar.TNov_ProgressBar.Value = (double)PBCount));
                                this.adskgProgressBar.TNov_ProgressBar.Dispatcher.Invoke<string>((Func<string>)(() => this.adskgProgressBar.value.Text = PBCount.ToString()));
                                string param1 = parIsolVozd;
                                bool success1 = true;
                                if (runadsk) Setadskgparam(a.Id, param1, systemcut, out success1);
                                if (!success1) { failed1.Add(a.Id.ToString()); failscount++; }
                                bool success2 = true;
                                if (runncat) SetNCategory(a.Id, out success2);
                                if (!success2) { failed2.Add(a.Id.ToString()); failscount++; }
                                bool success3 = true;
                                if (runadskp) Setadskpparam(a.Id, cat, fileName, out success3);
                                if (!success3) { failed3.Add(a.Id.ToString()); failscount++; }
                            }
                        }
                        else { Logger.Log("ВЫКЛЮЧЕНО", 1); }

                        Logger.Log("Фитинги возд:", 1);
                        if (FitVozd.Count > 0 && viewModel.run7)
                        {
                            string cat = "Соединительные детали воздуховодов";
                            foreach (Element a in FitVozd)
                            {
                                PBCount++;
                                this.adskgProgressBar.TNov_ProgressBar.Dispatcher.Invoke<double>((Func<double>)(() => this.adskgProgressBar.TNov_ProgressBar.Value = (double)PBCount));
                                this.adskgProgressBar.TNov_ProgressBar.Dispatcher.Invoke<string>((Func<string>)(() => this.adskgProgressBar.value.Text = PBCount.ToString()));
                                string param1 = parFitVozd;
                                bool success1 = true;
                                if (runadsk) Setadskgparam(a.Id, param1, systemcut, out success1);
                                if (!success1) { failed1.Add(a.Id.ToString()); failscount++; }
                                bool success2 = true;
                                if (runncat) SetNCategory(a.Id, out success2);
                                if (!success2) { failed2.Add(a.Id.ToString()); failscount++; }
                                bool success3 = true;
                                if (runadskp) Setadskpparam(a.Id, cat, fileName, out success3);
                                if (!success3) { failed3.Add(a.Id.ToString()); failscount++; }
                            }
                        }
                        else { Logger.Log("ВЫКЛЮЧЕНО", 1); }

                        Logger.Log("Оборудование:", 1);
                        if (Obor.Count > 0 && viewModel.run8)
                        {
                            foreach (Element a in Obor)
                            {
                                PBCount++;
                                this.adskgProgressBar.TNov_ProgressBar.Dispatcher.Invoke<double>((Func<double>)(() => this.adskgProgressBar.TNov_ProgressBar.Value = (double)PBCount));
                                this.adskgProgressBar.TNov_ProgressBar.Dispatcher.Invoke<string>((Func<string>)(() => this.adskgProgressBar.value.Text = PBCount.ToString()));
                                string param1 = parObor;
                                bool success1 = true;
                                if (runadsk) Setadskgparam(a.Id, param1, systemcut, out success1);
                                if (!success1) { failed1.Add(a.Id.ToString()); failscount++; }
                                bool success2 = true;
                                if (runncat) SetNCategory(a.Id, out success2);
                                if (!success2) { failed2.Add(a.Id.ToString()); failscount++; }
                            }
                        }
                        else { Logger.Log("ВЫКЛЮЧЕНО", 1); }

                        Logger.Log("Арматура труб:", 1);
                        if (ArmTrub.Count > 0 && viewModel.run9)
                        {
                            foreach (Element a in ArmTrub)
                            {
                                PBCount++;
                                this.adskgProgressBar.TNov_ProgressBar.Dispatcher.Invoke<double>((Func<double>)(() => this.adskgProgressBar.TNov_ProgressBar.Value = (double)PBCount));
                                this.adskgProgressBar.TNov_ProgressBar.Dispatcher.Invoke<string>((Func<string>)(() => this.adskgProgressBar.value.Text = PBCount.ToString()));
                                string param1 = parArmTrub;
                                bool success1 = true;
                                if (runadsk) Setadskgparam(a.Id, param1, systemcut, out success1);
                                if (!success1) { failed1.Add(a.Id.ToString()); failscount++; }
                                bool success2 = true;
                                if (runncat) SetNCategory(a.Id, out success2);
                                if (!success2) { failed2.Add(a.Id.ToString()); failscount++; }
                            }
                        }
                        else { Logger.Log("ВЫКЛЮЧЕНО", 1); }

                        Logger.Log("Гибкие трубы:", 1);
                        if (GibkTrub.Count > 0 && viewModel.run10)
                        {
                            string cat = "Гибкие трубы";
                            foreach (Element a in GibkTrub)
                            {
                                PBCount++;
                                this.adskgProgressBar.TNov_ProgressBar.Dispatcher.Invoke<double>((Func<double>)(() => this.adskgProgressBar.TNov_ProgressBar.Value = (double)PBCount));
                                this.adskgProgressBar.TNov_ProgressBar.Dispatcher.Invoke<string>((Func<string>)(() => this.adskgProgressBar.value.Text = PBCount.ToString()));
                                string param1 = parGibkTrub;
                                bool success1 = true;
                                if (runadsk) Setadskgparam(a.Id, param1, systemcut, out success1);
                                if (!success1) { failed1.Add(a.Id.ToString()); failscount++; }
                                bool success2 = true;
                                if (runncat) SetNCategory(a.Id, out success2);
                                if (!success2) { failed2.Add(a.Id.ToString()); failscount++; }
                                bool success3 = true;
                                if (runadskp) Setadskpparam(a.Id, cat, fileName, out success3);
                                if (!success3) { failed3.Add(a.Id.ToString()); failscount++; }
                            }
                        }
                        else { Logger.Log("ВЫКЛЮЧЕНО", 1); }

                        Logger.Log("Трубы:", 1);
                        if (Trub.Count > 0 && viewModel.run11)
                        {
                            string cat = "Трубы";
                            foreach (Element a in Trub)
                            {
                                PBCount++;
                                this.adskgProgressBar.TNov_ProgressBar.Dispatcher.Invoke<double>((Func<double>)(() => this.adskgProgressBar.TNov_ProgressBar.Value = (double)PBCount));
                                this.adskgProgressBar.TNov_ProgressBar.Dispatcher.Invoke<string>((Func<string>)(() => this.adskgProgressBar.value.Text = PBCount.ToString()));
                                string param1 = parTrub;
                                bool success1 = true;
                                if (runadsk) Setadskgparam(a.Id, param1, systemcut, out success1);
                                if (!success1) { failed1.Add(a.Id.ToString()); failscount++; }
                                bool success2 = true;
                                if (runncat) SetNCategory(a.Id, out success2);
                                if (!success2) { failed2.Add(a.Id.ToString()); failscount++; }
                                bool success3 = true;
                                if (runadskp) Setadskpparam(a.Id, cat, fileName, out success3);
                                if (!success3) { failed3.Add(a.Id.ToString()); failscount++; }
                            }
                            if (runcatalogs)
                            {

                                //PEX
                                List<ElementId> TrubIds = new List<ElementId>(); foreach (Element t in Trub) TrubIds.Add(t.Id);
                                bool PEXmarkCheck = modulePEX.PEXpipesTypeMarkCheck(TrubIds);
                                modulePEX mPEX = new modulePEX();
                                mPEX.PEXpipesReadExcel(out List<string> PEXPipeDiams, out List<string> PEXPipeMass, out List<string> PEXPipeArts, out List<string> PEXPipeMans, out List<string> PEXPipeTypes);
                                mPEX.PEXfitsReadExcel(out List<string> PEXFitCodes, out List<string> PEXFitArt1, out List<string> PEXFitArt2, out List<string> PEXFitArt3);
                                foreach (Element a in Trub)
                                {
                                    bool success4 = true;
                                    if (PEXmarkCheck) mPEX.PEXPipeSetParams(dateTime, TNovClassName, a, 1, PEXPipeDiams, PEXPipeMass, PEXPipeArts, PEXPipeMans, PEXPipeTypes, out success4);
                                    //пока что коэффициент = 1
                                    if (success4 == false) { failed4.Add(a.Id.ToString()); failscount++; continue; }
                                }
                            }


                        }
                        else { Logger.Log("ВЫКЛЮЧЕНО", 1); }

                        Logger.Log("Материалы изоляции труб:", 1);
                        if (IsolTrub.Count > 0 && viewModel.run12)
                        {
                            string cat = "Материалы изоляции труб";
                            foreach (Element a in IsolTrub)
                            {
                                PBCount++;
                                this.adskgProgressBar.TNov_ProgressBar.Dispatcher.Invoke<double>((Func<double>)(() => this.adskgProgressBar.TNov_ProgressBar.Value = (double)PBCount));
                                this.adskgProgressBar.TNov_ProgressBar.Dispatcher.Invoke<string>((Func<string>)(() => this.adskgProgressBar.value.Text = PBCount.ToString()));
                                string param1 = parIsolTrub;
                                bool success1 = true;
                                if (runadsk) Setadskgparam(a.Id, param1, systemcut, out success1);
                                if (!success1) { failed1.Add(a.Id.ToString()); failscount++; }
                                bool success2 = true;
                                if (runncat) SetNCategory(a.Id, out success2);
                                if (!success2) { failed2.Add(a.Id.ToString()); failscount++; }
                                bool success3 = true;
                                if (runadskp) Setadskpparam(a.Id, cat, fileName, out success3);
                                if (!success3) { failed3.Add(a.Id.ToString()); failscount++; }
                            }
                        }
                        else { Logger.Log("ВЫКЛЮЧЕНО", 1); }

                        Logger.Log("Фитинги труб:", 1);
                        if (FitTrub.Count > 0 && viewModel.run13)
                        {
                            foreach (Element a in FitTrub)
                            {
                                PBCount++;
                                this.adskgProgressBar.TNov_ProgressBar.Dispatcher.Invoke<double>((Func<double>)(() => this.adskgProgressBar.TNov_ProgressBar.Value = (double)PBCount));
                                this.adskgProgressBar.TNov_ProgressBar.Dispatcher.Invoke<string>((Func<string>)(() => this.adskgProgressBar.value.Text = PBCount.ToString()));
                                string param1 = parFitTrub;
                                bool success1 = true;
                                if (runadsk) Setadskgparam(a.Id, param1, systemcut, out success1);
                                if (!success1) { failed1.Add(a.Id.ToString()); failscount++; }
                                bool success2 = true;
                                if (runncat) SetNCategory(a.Id, out success2);
                                if (!success2) { failed2.Add(a.Id.ToString()); failscount++; }
                            }
                            if (runcatalogs)
                            {
                                //PEX
                                List<ElementId> TrubIds = new List<ElementId>(); foreach (Element t in Trub) TrubIds.Add(t.Id);
                                bool PEXmarkCheck = modulePEX.PEXpipesTypeMarkCheck(TrubIds);
                                modulePEX mPEX = new modulePEX();
                                mPEX.PEXpipesReadExcel(out List<string> PEXPipeDiams, out List<string> PEXPipeMass, out List<string> PEXPipeArts, out List<string> PEXPipeMans, out List<string> PEXPipeTypes);
                                mPEX.PEXfitsReadExcel(out List<string> PEXFitCodes, out List<string> PEXFitArt1, out List<string> PEXFitArt2, out List<string> PEXFitArt3);
                                foreach (Element a in FitTrub)
                                {
                                    bool success4 = true;
                                    if (PEXmarkCheck) mPEX.PEXFitsSetParams(dateTime, TNovClassName, a.Id, PEXFitCodes, PEXFitArt1, PEXFitArt2, PEXFitArt3, out success4);
                                    if (success4 == false) { failed4.Add(a.Id.ToString()); failscount++; continue; }
                                }
                            }
                        }
                        else { Logger.Log("ВЫКЛЮЧЕНО", 1); }

                        Logger.Log("Сантехника:", 1);
                        if (Santeh.Count > 0 && viewModel.run14)
                        {
                            foreach (Element a in Santeh)
                            {
                                PBCount++;
                                this.adskgProgressBar.TNov_ProgressBar.Dispatcher.Invoke<double>((Func<double>)(() => this.adskgProgressBar.TNov_ProgressBar.Value = (double)PBCount));
                                this.adskgProgressBar.TNov_ProgressBar.Dispatcher.Invoke<string>((Func<string>)(() => this.adskgProgressBar.value.Text = PBCount.ToString()));
                                string param1 = parSanteh;
                                bool success1 = true;
                                if (runadsk) Setadskgparam(a.Id, param1, systemcut, out success1);
                                if (!success1) { failed1.Add(a.Id.ToString()); failscount++; }
                                bool success2 = true;
                                if (runncat) SetNCategory(a.Id, out success2);
                                if (!success2) { failed2.Add(a.Id.ToString()); failscount++; }
                            }
                        }
                        else { Logger.Log("ВЫКЛЮЧЕНО", 1); }



                        if (runncat)
                        {
                            Logger.Log("Обобщенные модели:", 1);
                            //обобщенные модели
                            if (GMs.Count > 0)
                            {
                                bool runCat1 = false;
                                foreach (Parameter p in GMs.First().ParametersMap)
                                {
                                    if (p.IsShared)
                                    {
                                        if (p.GUID == NCatparamGuid) { runCat1 = true; break; }
                                    }
                                }
                                if (GMs.Count > 0 && runCat1)
                                {
                                    foreach (Element elem in GMs)
                                    {
                                        PBCount++;
                                        this.adskgProgressBar.TNov_ProgressBar.Dispatcher.Invoke<double>((Func<double>)(() => this.adskgProgressBar.TNov_ProgressBar.Value = (double)PBCount));
                                        this.adskgProgressBar.TNov_ProgressBar.Dispatcher.Invoke<string>((Func<string>)(() => this.adskgProgressBar.value.Text = PBCount.ToString()));
                                        try
                                        {
                                            elem.get_Parameter(NCatparamGuid)?.Set("6. Материалы и прочие элементы");
                                            Logger.Log("   Элемент " + elem.Id.ToString() + ": N_Категория " + "6. Материалы и прочие элементы", 2);
                                        }
                                        catch (Exception ex)
                                        {
                                            Logger.Log("   Элемент " + elem.Id.ToString() + " Ошибка: " + ex.Message, 4);
                                            failed2.Add(elem.Id.ToString()); failscount++;
                                        }
                                    }

                                }
                            }

                            Logger.Log("Несущая арматура:", 1);
                            //несущая арматура
                            if (rebar.Count > 0)
                            {
                                bool runCat2 = false;
                                foreach (Parameter p in rebar.First().ParametersMap)
                                {
                                    if (p.IsShared)
                                    {
                                        if (p.GUID == NCatparamGuid) { runCat2 = true; break; }
                                    }
                                }
                                if (runCat2)
                                {
                                    foreach (Element elem in rebar)
                                    {
                                        PBCount++;
                                        this.adskgProgressBar.TNov_ProgressBar.Dispatcher.Invoke<double>((Func<double>)(() => this.adskgProgressBar.TNov_ProgressBar.Value = (double)PBCount));
                                        this.adskgProgressBar.TNov_ProgressBar.Dispatcher.Invoke<string>((Func<string>)(() => this.adskgProgressBar.value.Text = PBCount.ToString()));
                                        try
                                        {
                                            elem.get_Parameter(NCatparamGuid)?.Set("6. Материалы и прочие элементы");
                                            Logger.Log("   Элемент " + elem.Id.ToString() + ": N_Категория " + "6. Материалы и прочие элементы", 2);
                                        }
                                        catch (Exception ex)
                                        {
                                            Logger.Log("   Элемент " + elem.Id.ToString() + " Ошибка: " + ex.Message, 4);
                                            failed2.Add(elem.Id.ToString()); failscount++;
                                        }
                                    }

                                }
                            }

                        }

                        transaction.Commit();
                        this.adskgProgressBar.Dispatcher.Invoke((System.Action)(() => this.adskgProgressBar.Close()));
                        Logger.Log("Закрываем транзакцию", 1);
                    }
                }



                int countElems = 0;

                //обработка немоделируемых
                if (viewModel.runNonModel)
                {
                    using (Transaction transaction2 = new Transaction(doc))
                    {
                        transaction2.Start("TNov - генерация немоделируемых");

                        //обработка немоделируемых
                        Logger.Log("Запускаем обработку немоделируемых", 1);

                        new IsolNonModel().Generate(IsolVozd, IsolTrub, out countElems);

                        transaction2.Commit();
                        Logger.Log("Закрываем транзакцию", 1);
                    }
                }

                if (allcount == 0) new infowindow280("Нечего обрабатывать.").ShowDialog();

                if (failscount > 0)
                {
                    string messageF = "";
                    string failed1str = "Не заполнился параметр ADSK_Группирование: ";
                    if (failed1.Count > 0) { failed1str = failed1str + String.Join(",", failed1); messageF = messageF + failed1str + ". "; }
                    string failed2str = "Не заполнился параметр N_Категория: ";
                    if (failed2.Count > 0) { failed2str = failed2str + String.Join(",", failed2); messageF = messageF + failed2str + ". "; }
                    string failed3str = "Не заполнились параметры ADSK_Наименование или ASDK_Количество: ";
                    if (failed3.Count > 0) { failed3str = failed3str + String.Join(",", failed3); messageF = messageF + failed3str + ". "; }
                    string failed4str = "Не обработались элементы из каталогов: ";
                    if (failed4.Count > 0) { failed4str = failed4str + String.Join(",", failed4); messageF = messageF + failed4str + ". "; }

                    Logger.Log("Открываем окно с ID проблемных элементов: " + messageF, 1);
                    // Диалоговое окно
                    var viewModel1 = new infowindowtextfieldViewModel();
                    viewModel1.headtxt = "Журнал работы:";
                    viewModel1.ids = messageF;
                    viewModel1.lowtxt = "Проверьте их вручную или посмотрите ошибки в лог-файле.";
                    var wpfview1 = new infowindowtextfield(viewModel1);
                    viewModel1.CloseRequest += (s, e) => wpfview1.Close();
                    bool? ok1 = wpfview1.ShowDialog();
                    Logger.Log(viewModel1.ids, 1);
                }
            }
            else
            {
                new infowindow280("Плагин работает по разным сценариям в зависимости от раздела. Похоже, ваш файл не относится к" +
                " разделу ВК/ПТ/ОВ/ЭЛ/СС. Раздел должен содержаться в имени модели.").ShowDialog();
                Logger.Log("Раздел не является ВК/ПТ/ОВ/ЭЛ/СС. Завершение работы.", 3);


                
            }
            Logger.Log("Завершение работы.", 5);
            return Result.Succeeded;
        }
    }
}
