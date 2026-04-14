using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Threading;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using Microsoft.Office.Interop.Excel;
using TNovCommon;
using TNovCommon;

namespace TNov
{
    public class Isolation
    {
        public int host; // 1 - возд, 2 - труб
        public ElementId elemid;
        public string type;
        public string position;
        public double quantity;
        public double length;
        public double thickness;
        public double area;
        public string grouping;
        public double ps;
    }

    
    public class IsolNonModel
    {
        private TNovProgressBar isolProgressBar;
        private void ThreadStartingPoint()
        {
            this.isolProgressBar = new TNovProgressBar();
            this.isolProgressBar.Show();
            Dispatcher.Run();
        }
        public int Generate(List<Element>IsolVozd, List<Element> IsolTrub, out int countElems)
        {
            
            countElems = 0;
            //Исходные параметры
            //string paramType = "Описание";
            string paramTypeMark = "ADSK_Марка"; //используется в ET Vent
            string paramTypeManufacturer = "Изготовитель"; //используется в ROLS

            //Параметры
            string paramGroup = "ADSK_Группирование";
            string paramNaim = "ADSK_Наименование";
            string paramCode = "ADSK_Код изделия";
            string paramManufacturer = "ADSK_Завод-изготовитель";
            string paramMeasure = "ADSK_Единица измерения";
            string paramSum = "ADSK_Количество";
            string paramPS = "N_Принадлежность системы";

            int imax = 100; //колво строк в таблице 1
            int jmax = 100; //колво строк в таблице 2

            
            Logger.Log("Убираем фитинги",1);
            //убираем фитинги

            IsolVozd = IsolVozd.Where(a => a.LookupParameter("Площадь").AsDouble() > 0).ToList();
            IsolTrub = IsolTrub.Where(a => a.LookupParameter("Площадь").AsDouble() > 0).ToList();
            
            int allcount1 = IsolVozd.Count+IsolTrub.Count;

            if(allcount1 > 0)
            {
                //считываем типы из Excel

                Microsoft.Office.Interop.Excel.Application xlApp = null;
                Workbooks workbooks = null;
                Workbook wb = null;
                try
                {
                    xlApp = new Microsoft.Office.Interop.Excel.Application();
                    // проверка установлен ли Excel
                    if (xlApp == null)
                    {
                        string info2txt = "Ошибка! MS Excel не установлен на данном компьютере.";
                        var info2 = new InfoWindow280(info2txt); info2.ShowDialog();
                        Logger.Log("MS Excel не установлен на данном компьютере.", 1);
                    }
                    Logger.Log("Открываем книгу Excel", 1);
                    //книга
                    workbooks = xlApp.Workbooks;
                    wb = workbooks.Open("//fs-nova/NOVA/04_БИБЛИОТЕКА/BIM/ВК_ОВ_Семейства/_TNov/Немоделируемые.xlsx", 0, true, 5, "", "", false, Microsoft.Office.Interop.Excel.XlPlatform.xlWindows, "", true, false, 0, true, false, false);
                    Worksheet ws1; ws1 = (Worksheet)wb.Sheets[1]; //изоляция возд
                    Worksheet ws2; ws2 = (Worksheet)wb.Sheets[2]; //изоляция труб

                    List<string> types1 = new List<string>();
                    //собираем типы с листа изоляция возд
                    for (int i = 2; i < imax; i++)
                    {
                        Range range = ws1.get_Range("A" + i.ToString(), "A" + i.ToString());
                        string rangetxt = range.Text.ToString();
                        if (rangetxt != "") types1.Add(rangetxt); else { imax = i + 1; break; } //уменьшаем заодно imax по количеству строк, заполненных в таблице
                    }
                    //собираем типы с листа изоляция труб
                    for (int j = 2; j < jmax; j++)
                    {
                        Range range = ws2.get_Range("A" + j.ToString(), "A" + j.ToString());
                        string rangetxt = range.Text.ToString();
                        if (rangetxt != "") types1.Add(rangetxt); else { jmax = j + 1; break; }
                        ;
                    }
                    types1 = types1.Distinct().ToList(); //принципиальные типы


                    int kmax = imax + jmax;

                    List<string> positions1 = new List<string>(); //типы кубиков
                    for (int i = 2; i < kmax; i++)
                    {
                        Range range = ws1.get_Range("B" + i.ToString(), "B" + i.ToString());
                        string rangetxt = range.Text.ToString();
                        if (rangetxt != "") positions1.Add(rangetxt); else break;
                    }

                    List<Isolation> isolation = new List<Isolation>(); //создаем элементы класса Isolation

                    if (IsolVozd.Count > 0)
                    {
                        Logger.Log("Создаем элементы класса Isolation для воздуховодов", 1);
                        foreach (Element elem in IsolVozd) //для воздуховодов
                        {
                            Logger.Log("   Элемент " + elem.Id.ToString(),2);
                            Element eType = RevitAPI.Document.GetElement(elem.GetTypeId());
                            string t = "?"; string p = "?"; //тип и позиция для сопоставления с таблицей
                            string eTypeMark = eType.LookupParameter(paramTypeMark).AsString(); if (eTypeMark == null) eTypeMark = "?";
                            string eTypeManufacturer = eType.LookupParameter(paramTypeManufacturer).AsString(); if (eTypeManufacturer == null) eTypeManufacturer = "?";
                            bool run = false;
                            /// ET Vent
                            if (eTypeMark.Contains("ET Vent"))
                            {
                                t = "ET Vent"; p = eTypeMark; run = true;
                            }
                            /// ROLS
                            else if (eTypeManufacturer.Contains("ROLS"))
                            {
                                t = "ROLS"; p = t + " " + eType.LookupParameter("Тип изоляции").AsValueString(); run = true;
                            }

                            if (run)
                            {
                                Logger.Log("      тип " + t + ", позиция " + p,2);

                                Isolation isol = new Isolation()
                                {
                                    host = 1,
                                    elemid = elem.Id,
                                    type = t,
                                    position = p,
                                    quantity = elem.LookupParameter(paramSum).AsDouble(),
                                    length = elem.LookupParameter("Длина").AsDouble(),
                                    thickness = elem.LookupParameter("Толщина изоляции").AsDouble(),
                                    area = elem.LookupParameter("Площадь").AsDouble(),
                                    grouping = elem.LookupParameter(paramGroup).AsString(),
                                    ps = elem.LookupParameter(paramPS).AsDouble(),
                                };
                                isolation.Add(isol);
                            }

                        }
                    }

                    if (IsolTrub.Count > 0)
                    {
                        Logger.Log("Создаем элементы класса Isolation для труб", 1);
                        foreach (Element elem in IsolTrub) //для труб
                        {
                            Logger.Log("   Элемент " + elem.Id.ToString(), 2);
                            Element eType = RevitAPI.Document.GetElement(elem.GetTypeId());
                            string t = "?"; string p = "?"; //тип и позиция для сопоставления с таблицей
                            string eTypeMark = eType.LookupParameter(paramTypeMark).AsString(); if (eTypeMark == null) eTypeMark = "?";
                            string eTypeManufacturer = eType.LookupParameter(paramTypeManufacturer).AsString(); if (eTypeManufacturer == null) eTypeManufacturer = "?";
                            bool run = false;
                            /// ROLS
                            if (eTypeManufacturer.Contains("ROLS"))
                            {
                                t = "ROLS"; p = t + " " + eType.LookupParameter("Тип изоляции").AsValueString(); run = true;
                            }
                            if (run)
                            {
                                Logger.Log("      тип " + t + ", позиция " + p, 2);

                                Isolation isol = new Isolation()
                                {
                                    host = 2,
                                    elemid = elem.Id,
                                    type = t,
                                    position = p,
                                    quantity = elem.LookupParameter(paramSum).AsDouble(),
                                    length = elem.LookupParameter("Длина").AsDouble(),
                                    thickness = elem.LookupParameter("Толщина изоляции").AsDouble(),
                                    area = elem.LookupParameter("Площадь").AsDouble(),
                                    grouping = elem.LookupParameter(paramGroup).AsString(),
                                    ps = elem.LookupParameter(paramPS).AsDouble(),
                                };
                                isolation.Add(isol);
                            }

                        }
                    }


                    if (isolation.Count > 0)
                    {
                        //проверяем наличие кубиков
                        List<string> isoltypes = new List<string>(); //список для типов изоляции, существующих в проекте

                        foreach (var i in isolation) isoltypes.Add(i.type);
                        isoltypes = isoltypes.Distinct().ToList();
                        List<string> cubetypes = new List<string>(); //список для типов кубиков, размещенных в модели


                        Logger.Log("Ищем кубики", 1);

                        foreach (string type in types1)
                        {
                            Logger.Log("Тип " + type + ":", 1);
                            foreach (var i in isoltypes)
                            {
                                if (i == type)
                                {
                                    int j = 0;
                                    ICollection<ElementId> GMsToRemove = new List<ElementId>();
                                    List<FamilyInstance> GMs = new FilteredElementCollector(RevitAPI.Document).OfCategory(BuiltInCategory.OST_GenericModel)   //фильтр по категории Об модели
                                                                                    .WhereElementIsNotElementType()
                                                                                    .OfClass(typeof(FamilyInstance))
                                                                                    .Cast<FamilyInstance>()
                                                                                    .ToList();
                                    foreach (FamilyInstance GM in GMs)
                                    {
                                        Element e = RevitAPI.Document.GetElement(GM.Id);
                                        string familyName = GM.Symbol.FamilyName;
                                        Element eType = RevitAPI.Document.GetElement(e.GetTypeId());
                                        if (familyName.Contains("pmN.Условное семейство Экземпляр") && eType.Name.Contains(type))
                                        {
                                            j++;
                                            if (j == 1) //первый кубик данного типа - очищаем параметры
                                            {
                                                e.LookupParameter(paramGroup)?.Set("");
                                                e.LookupParameter(paramNaim)?.Set("");
                                                e.LookupParameter(paramCode)?.Set("");
                                                e.LookupParameter(paramManufacturer)?.Set("");
                                                e.LookupParameter(paramMeasure)?.Set("");
                                                e.LookupParameter(paramSum)?.Set(0);
                                                e.LookupParameter(paramPS)?.Set(0);
                                                cubetypes.Add(type);
                                                Logger.Log("   Кубик найден и обработан", 1);
                                            }
                                            if (j > 1) GMsToRemove.Add(GM.Id); //последующие кубики данного типа - в список на удаление
                                        }

                                    }
                                    if (j > 1)
                                    {
                                        RevitAPI.Document.Delete(GMsToRemove.ToArray());
                                        Logger.Log("   Удалены остальные кубики в количестве: " + GMsToRemove.Count.ToString(), 1);
                                    }
                                    else if (j == 0)
                                    {
                                        Logger.Log("   Кубик для данного типа отсутствует в модели.", 1);
                                        new InfoWindow280("Отсутствует хотя бы 1 размещенный экземпляр семейства pmN.Условное семейство Экземпляр с типом " + type + ". Разместите его в любом удобном месте в модели.").ShowDialog();
                                    }
                                    break;
                                }
                            }
                        }

                        List<FamilyInstance> GMs1 = new FilteredElementCollector(RevitAPI.Document).OfCategory(BuiltInCategory.OST_GenericModel)   //фильтр по категории Об модели
                                                                                    .WhereElementIsNotElementType()
                                                                                    .OfClass(typeof(FamilyInstance))
                                                                                    .Cast<FamilyInstance>()
                                                                                    .ToList();



                        Thread thread = new Thread(new ThreadStart(this.ThreadStartingPoint));
                        thread.SetApartmentState(ApartmentState.STA);
                        thread.IsBackground = true;
                        thread.Start();
                        Thread.Sleep(100);

                        Logger.Log("Группируем элементы", 1);
                        double allcount = 0; double PBCount = 0;
                        //группы элементов класса Isolation по ADSK_Группированию
                        var isolSortByGroup = from i in isolation
                                              orderby i.grouping
                                              select i;
                        var iGroups = from i in isolSortByGroup
                                      group i by i.grouping;
                        foreach (var iGroup in iGroups) //группа по ADSK_Группированию
                        {
                            //группы элементов класса Isolation по Принадлежности системы
                            var isolSortByPS = from i in iGroup
                                               orderby i.ps
                                               select i;
                            var iPSs = from i in isolSortByPS
                                       group i by i.ps;
                            foreach (var iPS in iPSs) //группа по Принадлежности системы
                            {
                                //группы элементов класса Isolation по позиции
                                var isolSortByPosition = from i in iPS
                                                         orderby i.position
                                                         select i;
                                var iPositions = from i in isolSortByPosition
                                                 group i by i.position;
                                foreach (var iPosition in iPositions) //группа по Позиции
                                {
                                    allcount++;
                                }
                            }
                        }
                        this.isolProgressBar.TNov_ProgressBar.Dispatcher.Invoke<double>((Func<double>)(() => this.isolProgressBar.TNov_ProgressBar.Minimum = (double)PBCount));
                        this.isolProgressBar.TNov_ProgressBar.Dispatcher.Invoke<string>((Func<string>)(() => this.isolProgressBar.value.Text = PBCount.ToString()));
                        this.isolProgressBar.TNov_ProgressBar.Dispatcher.Invoke<double>((Func<double>)(() => this.isolProgressBar.TNov_ProgressBar.Maximum = (double)allcount));
                        this.isolProgressBar.TNov_ProgressBar.Dispatcher.Invoke<string>((Func<string>)(() => this.isolProgressBar.maxvalue.Text = allcount.ToString()));



                        //создание элементов
                        double incrementX = 0;
                        foreach (var iGroup in iGroups) //группа по ADSK_Группированию
                        {
                            incrementX = incrementX + 1 / 304.8;
                            string groupingValue = iGroup.First().grouping; //ADSK_Группирование
                            Logger.Log("Группа " + groupingValue, 1);
                            //группы элементов класса Isolation по Принадлежности системы
                            var isolSortByPS = from i in iGroup
                                               orderby i.ps
                                               select i;
                            var iPSs = from i in isolSortByPS
                                       group i by i.ps;
                            double incrementY = 0;
                            foreach (var iPS in iPSs) //группа по Принадлежности системы
                            {
                                incrementY = incrementY + 1 / 304.8;
                                double PSValue = iPS.First().ps; //Принадлежность системы
                                Logger.Log("   Группа " + PSValue.ToString(), 1);
                                //группы элементов класса Isolation по позиции
                                var isolSortByPosition = from i in iPS
                                                         orderby i.position
                                                         select i;
                                var iPositions = from i in isolSortByPosition
                                                 group i by i.position;
                                double incrementZ = 0;
                                foreach (var iPosition in iPositions) //группа по Позиции
                                {
                                    PBCount++;
                                    this.isolProgressBar.TNov_ProgressBar.Dispatcher.Invoke<double>((Func<double>)(() => this.isolProgressBar.TNov_ProgressBar.Value = (double)PBCount));
                                    this.isolProgressBar.TNov_ProgressBar.Dispatcher.Invoke<string>((Func<string>)(() => this.isolProgressBar.value.Text = PBCount.ToString()));
                                    incrementZ = incrementZ + 1 / 304.8;
                                    string position = iPosition.First().position; string type = iPosition.First().type;
                                    Logger.Log("      Позиция " + position, 1);
                                    int host = iPosition.First().host;
                                    if (cubetypes.Contains(type)) //если кубик размещен в модели
                                    {
                                        //ищем строки в таблице, соответствующие данной позиции
                                        Worksheet ws = ws1; if (host == 2) { ws = ws2; }
                                        for (int b = 2; b < kmax; b++)
                                        {
                                            Range range = ws.get_Range("B" + b.ToString(), "B" + b.ToString()); string rangetxt = range.Text.ToString();
                                            if (rangetxt == position)
                                            {
                                                Logger.Log("         Строка таблицы " + b.ToString(), 1);
                                                //создаем кубик
                                                int count = 0;
                                                foreach (FamilyInstance GM in GMs1)
                                                {
                                                    Element e = RevitAPI.Document.GetElement(GM.Id);
                                                    string familyName = GM.Symbol.FamilyName;
                                                    Element eType = RevitAPI.Document.GetElement(e.GetTypeId());
                                                    if (familyName.Contains("pmN.Условное семейство Экземпляр") && eType.Name.Contains(type) && count == 0)
                                                    {
                                                        LocationPoint point = GM.Location as LocationPoint;
                                                        XYZ newLocation = new XYZ(point.Point.X + incrementX, point.Point.Y + incrementY, point.Point.Z + incrementZ);
                                                        FamilyInstance instance = RevitAPI.Document.Create.NewFamilyInstance(newLocation, GM.Symbol, StructuralType.NonStructural);
                                                        Element newElem = RevitAPI.Document.GetElement(instance.Id);
                                                        Logger.Log("            Новый элемент " + instance.Id.ToString() + ", значения параметров:",2);
                                                        //заполняем параметры кубика
                                                        newElem.LookupParameter("N_Категория")?.Set("6. Материалы и прочие элементы");
                                                        newElem.LookupParameter(paramGroup)?.Set(groupingValue);
                                                        Logger.Log("            " + groupingValue, 2);
                                                        newElem.LookupParameter(paramPS)?.Set(PSValue);
                                                        Logger.Log("            " + PSValue, 2);
                                                        Range rangeC = ws.get_Range("C" + b.ToString(), "C" + b.ToString()); string naimValue = rangeC.Text.ToString();
                                                        newElem.LookupParameter(paramNaim)?.Set(naimValue);
                                                        Logger.Log("            " + naimValue, 2);
                                                        Range rangeD = ws.get_Range("D" + b.ToString(), "D" + b.ToString()); string codeValue = rangeD.Text.ToString();
                                                        newElem.LookupParameter(paramCode)?.Set(codeValue);
                                                        Logger.Log("            " + codeValue,2);
                                                        Range rangeE = ws.get_Range("E" + b.ToString(), "E" + b.ToString()); string manValue = rangeE.Text.ToString();
                                                        newElem.LookupParameter(paramManufacturer)?.Set(manValue);
                                                        Logger.Log("            " + manValue, 2);
                                                        Range rangeF = ws.get_Range("F" + b.ToString(), "F" + b.ToString()); string measureValue = rangeF.Text.ToString();
                                                        newElem.LookupParameter(paramMeasure)?.Set(measureValue);
                                                        Logger.Log("            " + measureValue, 2);
                                                        //параметр количество
                                                        Range rangeG = ws.get_Range("G" + b.ToString(), "G" + b.ToString()); string kstr = rangeG.Text.ToString();
                                                        kstr = kstr.Replace(".", ",");
                                                        double k = 0;
                                                        double.TryParse(kstr, out k);
                                                        double sumValue = 0;
                                                        if (k != 999)
                                                        {
                                                            foreach (var elem3 in iPosition) //элементы каждой группы по Позиции
                                                            {
                                                                Logger.Log("               " + elem3.elemid.ToString() + " колво: " + elem3.quantity.ToString(), 2);
                                                                sumValue += elem3.quantity;
                                                            }
                                                            sumValue = sumValue * k;
                                                        }
                                                        else
                                                        {
                                                            ///кейсы, где количество считается по сложным формулам
                                                            ///ROLS
                                                            if (type.Contains("ROLS"))
                                                            {
                                                                foreach (var elem3 in iPosition) //элементы каждой группы по Позиции
                                                                {
                                                                    double elemValue = 0;
                                                                    Logger.Log("               " + elem3.elemid.ToString() + " площадь: " + elem3.area.ToString(), 2);
                                                                    Logger.Log("               " + elem3.elemid.ToString() + " длина: " + elem3.length.ToString(), 2);
                                                                    Logger.Log("               " + elem3.elemid.ToString() + " толщина: " + elem3.thickness.ToString(), 2);
                                                                    if (measureValue == "м")
                                                                    {
                                                                        double dnar = elem3.area * 0.3048 * 0.3048 / (3.14159 * elem3.length * 0.3048) - 2 * elem3.thickness * 0.3048;
                                                                        elemValue = 1.1 * (1 + 3.14159 * (dnar + 2 * elem3.thickness * 0.3048)) * elem3.length * 0.3048;
                                                                    }
                                                                    else if (measureValue == "л")
                                                                    {
                                                                        double dnar = elem3.area * 0.3048 * 0.3048 / (3.14159 * elem3.length * 0.3048) - 2 * elem3.thickness * 0.3048;
                                                                        elemValue = 0.54 * (elem3.thickness * 0.3048 + 0.25 * 3.14159 * ((dnar + 2 * elem3.thickness * 0.3048) * (dnar + 2 * elem3.thickness * 0.3048) - dnar * dnar)) * elem3.length * 0.3048;
                                                                    }
                                                                    else if (measureValue == "м²")
                                                                    {
                                                                        bool pokrovParamExist = Param.ParamExist("Покровный материал", RevitAPI.Document.GetElement(elem3.elemid));
                                                                        if (pokrovParamExist)
                                                                        {
                                                                            int pokrovYes = RevitAPI.Document.GetElement(elem3.elemid).LookupParameter("Покровный материал").AsInteger();
                                                                            if (pokrovYes == 1)
                                                                            {
                                                                                if (elem3.position == "ROLS 20" || elem3.position == "ROLS 23" || elem3.position == "ROLS 25") elemValue = elem3.quantity;
                                                                                else elemValue = elem3.area * 0.3048 * 0.3048;
                                                                            }
                                                                        }
                                                                        else elemValue = 0;

                                                                    }
                                                                    Logger.Log("               " + elem3.elemid.ToString() + " колво: " + elemValue.ToString(), 2);
                                                                    sumValue += elemValue;
                                                                }

                                                            }
                                                        }
                                                        if (sumValue > 0.099) { sumValue = Math.Round(sumValue, 1); }
                                                        newElem.LookupParameter(paramSum)?.Set(sumValue);
                                                        Logger.Log("            " + sumValue.ToString(), 2);
                                                        count++; countElems++;
                                                    }
                                                }
                                                incrementZ = incrementZ + 1 / 304.8;
                                            }
                                        }
                                    }

                                }
                            }
                        }
                        this.isolProgressBar.Dispatcher.Invoke((System.Action)(() => this.isolProgressBar.Close()));

                        
                        
                    }
                }
                finally
                {
                    wb.Close();
                    xlApp.Quit();
                    Marshal.ReleaseComObject(wb);
                    Marshal.ReleaseComObject(workbooks);
                    Marshal.ReleaseComObject(xlApp);
                }
            }

            
            


            return countElems;
        }

    }
}
