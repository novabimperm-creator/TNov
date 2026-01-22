using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Mechanical;
using Autodesk.Revit.UI;
using Microsoft.Office.Interop.Excel;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Input;
using Parameter = Autodesk.Revit.DB.Parameter;
using System.Runtime.Remoting.Messaging;
using System.Windows.Threading;
using System.Threading;
using TNov.main;

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
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            string TNovClassName = "ADSK Стенки"; DateTime dateTime = DateTime.Now;
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
                if (qok != null && qok == true) { Logger.TurnOffExtendedLogs(); } else Logger.Log("Расширенные логи вкл",2);
            }

            Logger.Log("Сбор элементов",1);
            //Получаем элементы модели
            List<Element> Ducts = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_DuctCurves)
                .WhereElementIsNotElementType()
                .Cast<Element>()
                .ToList();

            //проверка наличия параметра у категорий

            bool paramexist = false;
            if (Ducts.Count > 0)
            {
                foreach (Parameter param in Ducts.First().ParametersMap)
                {
                    string paramName = param.Definition.Name;
                    if (paramName == "ADSK_Толщина стенки") { paramexist = true; break; }
                }
            }
            else 
            {
                string str0 = "Нечего обрабатывать";
                Logger.Log(str0 + ". Завершение работы.",3); new infowindow280(str0).ShowDialog(); 
                return Result.Failed;
            }
            if (!paramexist)
            {
                string str0 = "В проекте отсутствует параметр ADSK_Толщина стенки!";
                new infowindow280(str0).ShowDialog(); Logger.Log(str0 + " Завершение работы.", 3);
                return Result.Failed;
            }

            //проверка наличия параметра О_Материал обозначение
            Element dtype = doc.GetElement(Ducts.First().GetTypeId());
            bool matParamExist = Param.ParamExist("О_Материал обозначение", dtype);
            if(!matParamExist)
            {
                string str0 = "В проекте отсутствует параметр О_Материал обозначение!"; Logger.Log(str0 + ". Завершение работы.", 3);
                new infowindow280(str0).ShowDialog();
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

                int allcount = Ducts.Count;

                int PBCount = 0;
                this.adsksProgressBar.TNov_ProgressBar.Dispatcher.Invoke<double>((Func<double>)(() => this.adsksProgressBar.TNov_ProgressBar.Minimum = (double)PBCount));
                this.adsksProgressBar.TNov_ProgressBar.Dispatcher.Invoke<string>((Func<string>)(() => this.adsksProgressBar.value.Text = PBCount.ToString()));
                this.adsksProgressBar.TNov_ProgressBar.Dispatcher.Invoke<double>((Func<double>)(() => this.adsksProgressBar.TNov_ProgressBar.Maximum = (double)allcount));
                this.adsksProgressBar.TNov_ProgressBar.Dispatcher.Invoke<string>((Func<string>)(() => this.adsksProgressBar.maxvalue.Text = allcount.ToString()));


                foreach (Element elem in Ducts)
                {
                    PBCount++;
                    this.adsksProgressBar.TNov_ProgressBar.Dispatcher.Invoke<double>((Func<double>)(() => this.adsksProgressBar.TNov_ProgressBar.Value = (double)PBCount));
                    this.adsksProgressBar.TNov_ProgressBar.Dispatcher.Invoke<string>((Func<string>)(() => this.adsksProgressBar.value.Text = PBCount.ToString()));


                    Logger.Log(elem.Id.ToString(), 2);
                    string classGerm = "?";

                    Element eType = doc.GetElement(elem.GetTypeId());
                    string key = eType.LookupParameter("О_Материал обозначение").AsString();
                    string comments = eType.get_Parameter(BuiltInParameter.ALL_MODEL_TYPE_COMMENTS).AsString();
                    Logger.Log("   "+key,2);
                    if (key == null||key=="")
                    {
                        if (comments.Contains("оцинкованной стали")) key = "Оцинковка";
                        if (comments.Contains("листовой горячекатаной")) key = "Сталь черная";
                    }
                    bool matCheck = false;
                    foreach (var mat in mats)
                    {
                        if (mat == key) { matCheck = true; Logger.Log("   найден в экселе: " + mat, 2); break; }
                    }
                    if (!matCheck) { Logger.Log("   не найден в экселе",1); continue; }

                    string type = "Круглый"; string sizeParamName = "Диаметр";
                    DuctType ductType = (DuctType)eType; if (ductType.FamilyName.Contains("рямоуг"))
                    {
                        type = "Прямоугольный";
                        double s1 = elem.LookupParameter("Ширина").AsDouble(); double s2 = elem.LookupParameter("Высота").AsDouble();
                        if (s1 > s2) sizeParamName = "Ширина"; else sizeParamName = "Высота";
                    }

                    key = key + "/" + type;


                    double vnIsolt = elem.LookupParameter("Толщина внутренней изоляции").AsDouble();
                    if(vnIsolt > 0)
                    {
                        string vnIsolType = elem.LookupParameter("Тип внутренней изоляции").AsString();
                        Logger.Log("   "+vnIsolType, 2);
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
                        Logger.Log("   " + key,2);
                    }
                    if (vnIsolt > 0)
                    {
                        string vnIsolType = elem.LookupParameter("Тип внутренней изоляции").AsString();
                        key = key + "/" + vnIsolType;
                        Logger.Log("   " + key, 2);
                    }

                    double size = elem.LookupParameter(sizeParamName).AsDouble() * 304.8;

                    double thickness = 0;

                    Dictionary<Double,Double> valuesForKey = new Dictionary<Double,Double>();
                    
                    for (int i = 0; i < imax; i++)
                    {
                        if (key == keys[i])
                        {
                            valuesForKey.Add(sizes[i], thicknesses[i]);
                        }
                    }
                    var sortedValuesForKey = new SortedDictionary<Double,Double>(valuesForKey);

                    Logger.Log("   Размер " + size.ToString(),2);

                    foreach (var k in sortedValuesForKey.Keys)
                    {
                        if (size > k) continue;
                        thickness = sortedValuesForKey[k] / 304.8;
                        Logger.Log("   Толщина стенки " + sortedValuesForKey[k].ToString(), 2);
                        break;
                    }

                    try
                    {
                        if (thickness > 0) elem.LookupParameter("ADSK_Толщина стенки").Set(thickness);
                        if(classGerm!="?") elem.LookupParameter("Класс герметичности").Set(classGerm);
                        bool success = true;
                        MEPSpec adskg = new MEPSpec();
                        adskg.Setadskpparam(elem.Id, "Воздуховоды", "-ОВ", out success);
                        if(!success) { failscount++; failed.Add(elem.Id.ToString()); }
                    }
                    catch(Exception e) 
                    { 
                        Logger.Log("Элемент "+elem.Id.ToString()+" ошибка: "+e.Message,4); 
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
                var viewModel1 = new infowindowtextfieldViewModel();
                viewModel1.headtxt = "У некоторых элементов не заполнились параметры:";
                viewModel1.ids = failedstr;
                viewModel1.lowtxt = "Проверьте их вручную или посмотрите ошибки в лог-файле.";
                var wpfview1 = new infowindowtextfield(viewModel1);
                viewModel1.CloseRequest += (s, e) => wpfview1.Close();
                bool? ok1 = wpfview1.ShowDialog();
                Logger.Log(viewModel1.ids, 1);
            }

            Logger.Log("Завершение работы.",5);
            return Result.Succeeded;
        }
    }
}
