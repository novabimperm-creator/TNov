using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Mechanical;
using Autodesk.Revit.UI;
using Microsoft.Office.Interop.Excel;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.Remoting.Messaging;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Threading;
using TNovCommon;
using Parameter = Autodesk.Revit.DB.Parameter;
using TNovCommon;

namespace TNov
{
    [Transaction(TransactionMode.Manual)]
    public class DuctThicknessClasses : IExternalCommand
    {
        private TNovProgressBar adsksProgressBar;
        private void ThreadStartingPoint()
        {
            this.adsksProgressBar = new TNovProgressBar();
            this.adsksProgressBar.Show();
            Dispatcher.Run();
        }
        //параметры
        Guid adskTstParamGuid = new Guid("381b467b-3518-42bb-b183-35169c9bdfb3");//ADSK_Толщина стенки
        Guid adskMatOboznParamGuid = new Guid("dbe7f282-3606-44cf-ac51-0f274c34c07b");//ADSK_Материал обозначение

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            string TNovClassName = "ADSK Стенки"; DateTime dateTime = DateTime.Now;
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

            
            Logger.Log("Сбор элементов",1);
            //Получаем элементы модели
            List<Element> Ducts = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_DuctCurves)
                .WhereElementIsNotElementType()
                .Cast<Element>()
                .ToList();
            List<Element> DuctFittings = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_DuctFitting)
                    .WhereElementIsNotElementType()
                    .Cast<Element>()
                    .ToList();
            List<Element> allElems = new List<Element>();
            foreach (Element elem in DuctFittings) { allElems.Add(elem); } foreach(Element elem in Ducts) { allElems.Add(elem); }

            //проверка наличия параметра у категории воздуховоды

            if (Ducts.Count > 0) { }
            else 
            {
                string str0 = "Нечего обрабатывать";
                Logger.Log(str0 + ". Завершение работы.",3); new InfoWindow280(str0).ShowDialog(); 
                return Result.Failed;
            }
            if (Param.ParamExistByGuid(adskTstParamGuid,Ducts.First()) == false)
            {
                string str0 = "У категории Воздуховоды отсутствует параметр ADSK_Толщина стенки!";
                new InfoWindow280(str0).ShowDialog(); Logger.Log(str0 + " Завершение работы.", 3);
                return Result.Failed;
            }
            if (Param.ParamExistByGuid(adskTstParamGuid, DuctFittings.First()) == false)
            {
                string str0 = "У категории Соединительные детали воздуховодов отсутствует параметр ADSK_Толщина стенки!";
                new InfoWindow280(str0).ShowDialog(); Logger.Log(str0 + " Завершение работы.", 3);
                return Result.Failed;
            }

            //проверка наличия параметра О_Материал обозначение
            Element dtype = doc.GetElement(Ducts.First().GetTypeId());
            bool matParamExist = Param.ParamExistByGuid(adskMatOboznParamGuid, dtype);
            if(!matParamExist)
            {
                string str0 = "В проекте отсутствует параметр О_Материал обозначение!"; Logger.Log(str0 + ". Завершение работы.", 3);
                new InfoWindow280(str0).ShowDialog();
                return Result.Failed;
            }

            //получаем типы материалов, указанных в экселе
            Microsoft.Office.Interop.Excel.Application xlApp=null;
            
            Workbooks workbooks = null;
            Workbook wb = null;
            List<string> mats = new List<string>();
            List<string> keys = new List<string>();
            List<double> sizes = new List<double>();
            List<double> thicknesses = new List<double>();
            int imax = 200;
            try
            {
                xlApp = new Microsoft.Office.Interop.Excel.Application();
                workbooks = xlApp.Workbooks;
                wb = workbooks.Open("//fs-nova/NOVA/04_БИБЛИОТЕКА/BIM/ВК_ОВ_Семейства/_TNov/Воздуховоды_Толщина стенки.xlsx", 0, true, 5, "", "", false, Microsoft.Office.Interop.Excel.XlPlatform.xlWindows, "", true, false, 0, true, false, false);
                
                
                if (xlApp != null)
                {
                    Worksheet ws1 = (Worksheet)wb.Sheets[1];
                    for (int i = 2; i < imax; i++)
                    {
                        Range range = ws1.get_Range("A" + i.ToString(), "A" + i.ToString());
                        string rangetxt = range.Text.ToString(); if (rangetxt == "" || rangetxt == null) { imax = i + 1; break; }
                        mats.Add(rangetxt);
                    }
                    for (int i = 2; i < imax; i++)
                    {
                        Range rangeA = ws1.get_Range("A" + i.ToString(), "A" + i.ToString());
                        Range rangeB = ws1.get_Range("B" + i.ToString(), "B" + i.ToString());
                        Range rangeC = ws1.get_Range("C" + i.ToString(), "C" + i.ToString());
                        Range rangeD = ws1.get_Range("D" + i.ToString(), "D" + i.ToString());
                        string key = rangeA.Text.ToString() + "/" + rangeB.Text.ToString() + "/" + rangeC.Text.ToString() + "/" + rangeD.Text.ToString();
                        keys.Add(key);
                        Logger.Log(key,2);
                        Range rangeE = ws1.get_Range("E" + i.ToString(), "E" + i.ToString());
                        double rangeEd = 0; Double.TryParse(rangeE.Text.ToString(), out rangeEd);
                        sizes.Add(rangeEd); Logger.Log(rangeEd.ToString(),2);
                        Range rangeF = ws1.get_Range("F" + i.ToString(), "F" + i.ToString()); string rangeFstr = rangeF.Text.ToString(); rangeFstr = rangeFstr.Replace('.', ',');
                        double rangeFd = 0; Double.TryParse(rangeFstr, out rangeFd);
                        thicknesses.Add(rangeFd); Logger.Log(rangeFd.ToString(), 2);
                    }
                    
                    
                }
                else return Result.Failed;
            }
            finally
            {
                Marshal.ReleaseComObject(wb);
                Marshal.ReleaseComObject(workbooks);
                Marshal.ReleaseComObject(xlApp);
            }

            mats = mats.Distinct().ToList();

            imax = imax - 2;

            int failscount = 0;
            List<string> failed = new List<string>();

            //транзакция
            using (Transaction transaction = new Transaction(doc))
            {

                transaction.Start("TNov - ADSK Стенки");
                Logger.Log("Открываем транзакцию",1);

                Thread thread = new Thread(new ThreadStart(this.ThreadStartingPoint));
                thread.SetApartmentState(ApartmentState.STA);
                thread.IsBackground = true;
                thread.Start();
                Thread.Sleep(100);

                int allcount = Ducts.Count+DuctFittings.Count;

                int PBCount = 0;
                this.adsksProgressBar.TNov_ProgressBar.Dispatcher.Invoke<double>((Func<double>)(() => this.adsksProgressBar.TNov_ProgressBar.Minimum = (double)PBCount));
                this.adsksProgressBar.TNov_ProgressBar.Dispatcher.Invoke<string>((Func<string>)(() => this.adsksProgressBar.value.Text = PBCount.ToString()));
                this.adsksProgressBar.TNov_ProgressBar.Dispatcher.Invoke<double>((Func<double>)(() => this.adsksProgressBar.TNov_ProgressBar.Maximum = (double)allcount));
                this.adsksProgressBar.TNov_ProgressBar.Dispatcher.Invoke<string>((Func<string>)(() => this.adsksProgressBar.maxvalue.Text = allcount.ToString()));


                foreach (Element elem in allElems)
                {
                    PBCount++;
                    this.adsksProgressBar.TNov_ProgressBar.Dispatcher.Invoke<double>((Func<double>)(() => this.adsksProgressBar.TNov_ProgressBar.Value = (double)PBCount));
                    this.adsksProgressBar.TNov_ProgressBar.Dispatcher.Invoke<string>((Func<string>)(() => this.adsksProgressBar.value.Text = PBCount.ToString()));

                    string classGerm = "?"; double thickness = 0; string connectedElems = "";
                    string cat = "Воздуховоды";

                    Logger.Log("   " + elem.Id.IntegerValue.ToString(), 2);

                    if (IsDuctConnector(elem)) 
                    {
                        cat = "Соединительные детали воздуховодов";
                        AnalyzeDuctConnector(doc, elem, out connectedElems);
                        if (connectedElems != "") 
                        {
                            double thickness0 = 0;
                            string[] connectedVozd = connectedElems.Split(new char[] { ',' });
                            foreach (string connectedElem in connectedVozd)
                            {
                                int.TryParse(connectedElem, out int id);
                                if (id > 0)
                                {
                                    Element cElem = doc.GetElement(new ElementId(id));
                                    GetValues(doc, cElem, mats, keys, sizes, thicknesses, imax, out thickness0, out classGerm);
                                    if (thickness0 > thickness) thickness = thickness0;
                                }
                            }
                        }
                    }
                    else
                    {
                        GetValues(doc, elem, mats, keys, sizes, thicknesses, imax, out thickness, out classGerm);
                    }

                    if (Param.ParamExistByGuid(adskTstParamGuid, elem) == false)
                    {
                        Logger.Log("Элемент " + elem.Id.ToString() + " ошибка: отсутствует параметр ADSK_Толщина стенки", 4);
                        failscount++; failed.Add(elem.Id.ToString()); continue;
                    }
                    try
                    {
                        Logger.Log("   "+ thickness.ToString(), 2);
                        if (thickness > 0) elem.get_Parameter(adskTstParamGuid).Set(thickness);
                        Logger.Log("   " + classGerm, 2);
                        if (classGerm != "?") elem.LookupParameter("Класс герметичности").Set(classGerm);
                        bool success = true;
                        MEPSpec adskg = new MEPSpec();
                        adskg.Setadskpparam(elem.Id, cat, "-ОВ", out success);
                        if (!success) { failscount++; failed.Add(elem.Id.ToString()); }
                    }
                    catch (Exception e)
                    {
                        Logger.Log("Элемент " + elem.Id.ToString() + " ошибка: " + e.Message, 4);
                        failscount++; failed.Add(elem.Id.ToString());
                    }

                }
                
                transaction.Commit();
                this.adsksProgressBar.Dispatcher.Invoke((System.Action)(() => this.adsksProgressBar.Close()));
                Logger.Log("Закрываем транзакцию",1);
            }

            if (failscount > 0)
            {
                failed = failed.Distinct().ToList();
                string failedstr = String.Join(",", failed);
                Logger.Log("Открываем окно с ID проблемных элементов: " + failedstr,1);
                // Диалоговое окно
                var viewModel1 = new InfoWindowTextFieldViewModel();
                viewModel1.headtxt = "У некоторых элементов не заполнились параметры:";
                viewModel1.ids = failedstr;
                viewModel1.lowtxt = "Проверьте их вручную или посмотрите ошибки в лог-файле.";
                var wpfview1 = new InfoWindowTextField(viewModel1);
                viewModel1.CloseRequest += (s, e) => wpfview1.Close();
                bool? ok1 = wpfview1.ShowDialog();
                Logger.Log(viewModel1.ids, 1);
            }

            Logger.Log("Завершение работы.",5);
            return Result.Succeeded;
        }
        private void GetValues(Document doc, Element elem, List<string>mats, List<string> keys, List<double> sizes, List<double> thicknesses, int imax,
            out double thickness, out string classGerm)
        {
            Logger.Log(elem.Id.ToString(), 2);
            classGerm = "?"; thickness = 0;

            Element eType = doc.GetElement(elem.GetTypeId());
            string key = eType.get_Parameter(adskMatOboznParamGuid).AsString();
            string comments = eType.get_Parameter(BuiltInParameter.ALL_MODEL_TYPE_COMMENTS).AsString();
            Logger.Log("   " + key, 2);
            if (key == null || key == "")
            {
                if (comments.Contains("оцинкованной стали")) key = "Оцинковка";
                if (comments.Contains("листовой горячекатаной")) key = "Сталь черная";
            }
            bool matCheck = false;
            foreach (var mat in mats)
            {
                if (mat == key) { matCheck = true; Logger.Log("   найден в экселе: " + mat, 2); break; }
            }
            if (!matCheck) { Logger.Log("   не найден в экселе", 1); }
            else
            {
                string type = "Круглый"; string sizeParamName = "Диаметр";
                DuctType ductType = (DuctType)eType; if (ductType.FamilyName.Contains("рямоуг"))
                {
                    type = "Прямоугольный";
                    double s1 = elem.LookupParameter("Ширина").AsDouble(); double s2 = elem.LookupParameter("Высота").AsDouble();
                    if (s1 > s2) sizeParamName = "Ширина"; else sizeParamName = "Высота";
                }

                key = key + "/" + type;


                double vnIsolt = elem.LookupParameter("Толщина внутренней изоляции").AsDouble();
                if (vnIsolt > 0)
                {
                    string vnIsolType = elem.LookupParameter("Тип внутренней изоляции").AsString();
                    Logger.Log("   " + vnIsolType, 2);
                    classGerm = vnIsolType.Replace("Класс герметичности ", "");
                }
                else
                {
                    double isolt = elem.LookupParameter("Толщина изоляции").AsDouble();
                    if (isolt == 0)
                    {
                        classGerm = "A";
                        key = key + "/" + "-";
                    }
                    else
                    {
                        string isolType = elem.LookupParameter("Тип изоляции").AsString();
                        if (isolType.Contains("Огнезащита"))
                        {
                            classGerm = "B";
                            key = key + "/" + "Огнезащита";
                        }
                        else
                        {
                            classGerm = "A";
                            key = key + "/" + "-";
                        }
                    }
                    key = key + "/" + "-";
                    Logger.Log("   " + key, 2);
                }
                if (vnIsolt > 0)
                {
                    string vnIsolType = elem.LookupParameter("Тип внутренней изоляции").AsString();
                    key = key + "/" + vnIsolType;
                    Logger.Log("   " + key, 2);
                }

                double size = elem.LookupParameter(sizeParamName).AsDouble() * 304.8;

                Dictionary<Double, Double> valuesForKey = new Dictionary<Double, Double>();

                for (int i = 0; i < imax; i++)
                {
                    if (key == keys[i])
                    {
                        valuesForKey.Add(sizes[i], thicknesses[i]);
                    }
                }
                var sortedValuesForKey = new SortedDictionary<Double, Double>(valuesForKey);

                Logger.Log("   Размер " + size.ToString(), 2);

                foreach (var k in sortedValuesForKey.Keys)
                {
                    if (size > k) continue;
                    thickness = sortedValuesForKey[k] / 304.8;
                    Logger.Log("   Толщина стенки " + sortedValuesForKey[k].ToString(), 2);
                    break;
                }
            }


            
        }
        private bool IsDuctConnector(Element element)
        {
            // Проверяем, является ли элемент соединительной деталью воздуховода
            if (element is FamilyInstance familyInstance)
            {
                // Проверяем категорию и наличие коннекторов
                if (familyInstance.Category != null &&
                    (familyInstance.Category.Id.IntegerValue == (int)BuiltInCategory.OST_DuctFitting ||
                     familyInstance.Category.Id.IntegerValue == (int)BuiltInCategory.OST_DuctAccessory ||
                     familyInstance.Category.Id.IntegerValue == (int)BuiltInCategory.OST_DuctTerminal))
                {
                    return true;
                }
            }
            return false;
        }

        private void AnalyzeDuctConnector(Document doc, Element ductConnector, out string connectedElems)
        {
            connectedElems = "";

            var connectedDuctIds = new HashSet<ElementId>();

            // Получаем коннекторы элемента
            var connectorSet = GetConnectors(ductConnector);
            if (connectorSet == null) return;

            // Шаг 1: Проверяем прямое подключение к воздуховодам
            bool hasDirectDuctConnection = false;

            foreach (Connector connector in connectorSet)
            {
                foreach (Connector refConnector in connector.AllRefs)
                {
                    if (refConnector.Owner is Duct)
                    {
                        connectedDuctIds.Add(refConnector.Owner.Id);
                        hasDirectDuctConnection = true;
                    }
                }
            }

            // Если есть прямое подключение к воздуховодам
            if (hasDirectDuctConnection)
            {
                connectedElems = string.Join(",", connectedDuctIds.Select(id => id.IntegerValue));
                return;
            }

            // Шаг 2: Если нет прямых подключений к воздуховодам,
            // ищем воздуховоды через подключенные соединительные детали/арматуру
            var analyzedElements = new HashSet<ElementId> { ductConnector.Id };
            var elementsToProcess = new Queue<Element>();
            var affectedElements = new List<Element> { ductConnector };

            // Добавляем все подключенные соединительные детали/арматуру
            foreach (Connector connector in connectorSet)
            {
                foreach (Connector refConnector in connector.AllRefs)
                {
                    Element connectedElement = refConnector.Owner;

                    if (connectedElement.Id != ductConnector.Id &&
                        IsDuctConnector(connectedElement) &&
                        !analyzedElements.Contains(connectedElement.Id))
                    {
                        elementsToProcess.Enqueue(connectedElement);
                        analyzedElements.Add(connectedElement.Id);
                        affectedElements.Add(connectedElement);
                    }
                }
            }

            // Проверяем каждый подключенный элемент на наличие воздуховодов
            while (elementsToProcess.Count > 0)
            {
                Element currentElement = elementsToProcess.Dequeue();
                var currentConnectors = GetConnectors(currentElement);

                if (currentConnectors != null)
                {
                    foreach (Connector connector in currentConnectors)
                    {
                        foreach (Connector refConnector in connector.AllRefs)
                        {
                            if (refConnector.Owner is Duct)
                            {
                                connectedDuctIds.Add(refConnector.Owner.Id);
                            }
                        }
                    }
                }
            }

            // Если нашли воздуховоды через подключенные элементы
            if (connectedDuctIds.Count > 0)
            {
                connectedElems = string.Join(",", connectedDuctIds.Select(id => id.IntegerValue));
            }
        }

        private ConnectorSet GetConnectors(Element element)
        {
            if (element is FamilyInstance familyInstance)
            {
                if (familyInstance.MEPModel != null)
                {
                    if (familyInstance.MEPModel.ConnectorManager != null&& familyInstance.MEPModel.ConnectorManager.Connectors!=null)
                        return familyInstance.MEPModel.ConnectorManager.Connectors;
                }
            }
            else if (element is MEPCurve mepCurve)
            {
                return mepCurve.ConnectorManager.Connectors;
            }
            return null;
        }
    }
}
