 using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TNov.main;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Plumbing;
using System.Windows.Controls;
using Microsoft.Office.Interop.Excel;
using Parameter = Autodesk.Revit.DB.Parameter;
using System.Data.Common;
using System.Runtime.InteropServices;

namespace TNov
{
    public class modulePEX
    {
        public void PEXpipesReadExcel(out List<string> PEXPipeDiams,out List<string> PEXPipeMass,out List<string> PEXPipeArts,out List<string> PEXPipeMans, out List<string> PEXPipeTypes)
        {
            Microsoft.Office.Interop.Excel.Application xlApp = null;
            Workbooks workbooks = null;
            Workbook wb = null;
            try
            {
                xlApp = new Microsoft.Office.Interop.Excel.Application();
                workbooks = xlApp.Workbooks;
                wb = workbooks.Open("//fs-nova/NOVA/04_БИБЛИОТЕКА/BIM/ВК_ОВ_Семейства/_TNov/VM_PEX_Спецификация труб.xlsx", 0, true, 5, "", "", false, Microsoft.Office.Interop.Excel.XlPlatform.xlWindows, "", true, false, 0, true, false, false);
                PEXPipeDiams = new List<string>();
                PEXPipeMass = new List<string>();
                PEXPipeArts = new List<string>();
                PEXPipeMans = new List<string>();
                PEXPipeTypes = new List<string>();
                if (xlApp != null)
                {
                    Worksheet ws1 = (Worksheet)wb.Sheets[1];
                    int imax = 200;

                    for (int i = 2; i < imax; i++)
                    {
                        Range rangeDiam = ws1.get_Range("B" + i.ToString(), "B" + i.ToString());
                        string rangeDiamtxt = rangeDiam.Text.ToString(); if (rangeDiamtxt == "" || rangeDiamtxt == null) { rangeDiamtxt = "0"; imax = i; }
                        PEXPipeDiams.Add(rangeDiamtxt);
                    }

                    for (int i = 2; i < imax; i++)
                    {
                        Range rangeMass = ws1.get_Range("C" + i.ToString(), "C" + i.ToString());
                        string rangeMasstxt = rangeMass.Text.ToString(); if (rangeMasstxt == "" || rangeMasstxt == null) rangeMasstxt = "0";
                        PEXPipeMass.Add(rangeMasstxt);
                    }

                    for (int i = 2; i < imax; i++)
                    {
                        Range rangeArt = ws1.get_Range("D" + i.ToString(), "D" + i.ToString());
                        string rangeArttxt = rangeArt.Text.ToString(); if (rangeArttxt == "" || rangeArttxt == null) rangeArttxt = "-";
                        PEXPipeArts.Add(rangeArttxt);
                    }

                    for (int i = 2; i < imax; i++)
                    {
                        Range rangeMan = ws1.get_Range("E" + i.ToString(), "E" + i.ToString());
                        string rangeMantxt = rangeMan.Text.ToString(); if (rangeMantxt == "" || rangeMantxt == null) rangeMantxt = "-";
                        PEXPipeMans.Add(rangeMantxt);
                    }

                    for (int i = 2; i < imax; i++)
                    {
                        Range rangeT = ws1.get_Range("A" + i.ToString(), "A" + i.ToString());
                        string rangeTtxt = rangeT.Text.ToString(); if (rangeTtxt == "" || rangeTtxt == null) rangeTtxt = "-";
                        PEXPipeTypes.Add(rangeTtxt);
                    }
                    
                    
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
        public void PEXfitsReadExcel(out List<string> PEXFitCodes, out List<string> PEXFitArt1, out List<string> PEXFitArt2, out List<string> PEXFitArt3)
        {
            Microsoft.Office.Interop.Excel.Application xlApp = null;
            Workbooks workbooks = null;
            Workbook wb = null;
            try
            {
                xlApp = new Microsoft.Office.Interop.Excel.Application();
                workbooks = xlApp.Workbooks;
                wb = workbooks.Open("//fs-nova/NOVA/04_БИБЛИОТЕКА/BIM/ВК_ОВ_Семейства/_TNov/VM_PEX_Спецификация труб.xlsx", 0, true, 5, "", "", false, Microsoft.Office.Interop.Excel.XlPlatform.xlWindows, "", true, false, 0, true, false, false);
                PEXFitCodes = new List<string>();
                PEXFitArt1 = new List<string>();
                PEXFitArt2 = new List<string>();
                PEXFitArt3 = new List<string>();
                if (xlApp != null)
                {
                    Worksheet ws1 = (Worksheet)wb.Sheets[2];
                    int imax = 200;

                    for (int i = 2; i < imax; i++)
                    {
                        Range rangeDiam = ws1.get_Range("B" + i.ToString(), "B" + i.ToString());
                        string rangeDiamtxt = rangeDiam.Text.ToString(); if (rangeDiamtxt == "" || rangeDiamtxt == null) { rangeDiamtxt = "0"; imax = i; }
                        PEXFitCodes.Add(rangeDiamtxt);
                    }
                    imax++;
                    for (int i = 2; i < imax; i++)
                    {
                        Range rangeMass = ws1.get_Range("C" + i.ToString(), "C" + i.ToString());
                        string rangeMasstxt = rangeMass.Text.ToString(); if (rangeMasstxt == "" || rangeMasstxt == null) rangeMasstxt = "0";
                        PEXFitArt1.Add(rangeMasstxt);
                    }

                    for (int i = 2; i < imax; i++)
                    {
                        Range rangeArt = ws1.get_Range("D" + i.ToString(), "D" + i.ToString());
                        string rangeArttxt = rangeArt.Text.ToString(); if (rangeArttxt == "" || rangeArttxt == null) rangeArttxt = "-";
                        PEXFitArt2.Add(rangeArttxt);
                    }

                    for (int i = 2; i < imax; i++)
                    {
                        Range rangeMan = ws1.get_Range("E" + i.ToString(), "E" + i.ToString());
                        string rangeMantxt = rangeMan.Text.ToString(); if (rangeMantxt == "" || rangeMantxt == null) rangeMantxt = "-";
                        PEXFitArt3.Add(rangeMantxt);
                    }
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
        public static bool PEXpipesTypeMarkCheck(in List<ElementId> elementIds)
        {
            bool res=false;
            Document doc = RevitAPI.Document;
            foreach (ElementId elementId in elementIds) 
            {
                Element element = doc.GetElement(elementId);
                if (element != null)
                {
                    Element eType = doc.GetElement(element.GetTypeId());
                    string pipeCode = "";
                    try { pipeCode = eType.LookupParameter("VM_Код трубы")?.AsString(); } catch { }
                    if (pipeCode != null)
                    {
                        if (pipeCode == "PEX")
                        {
                            Parameter markTypeParam = eType.get_Parameter(BuiltInParameter.WINDOW_TYPE_ID);
                            if(markTypeParam != null)
                            {
                                markTypeParam.Set(""); res = true; 
                            }
                            
                        }
                    }
                }
                
            }
            return res;
        }
        public static void PEXpipesParamsSet(in ElementId elemId, in double koeffTrub)
        {
            Document doc = RevitAPI.Document;
            Element elem = doc.GetElement(elemId);
            if (elem != null)
            {
                Element eType = doc.GetElement(elem.GetTypeId());
                bool adskNparamexist = Param.ParamExist("ADSK_Толщина стенки", elem);
                string komm = eType.LookupParameter("Комментарии к типоразмеру").AsString(); if (komm == null) komm = "";
                double d = elem.LookupParameter("Диаметр").AsDouble();
                double t = 0;
                if (adskNparamexist) t = elem.LookupParameter("ADSK_Толщина стенки").AsDouble(); 
                double l = elem.LookupParameter("Длина").AsDouble();
                d = Math.Round(d * 304.8, 0); t = Math.Round(t * 304.8, 1); l = Math.Round(l * 0.3048, 1);
                string tstr = t.ToString(); tstr = tstr.Replace(",", ".");
                try
                {
                    elem.LookupParameter("ADSK_Наименование").Set(komm + " ø" + d.ToString() + "х" + tstr);
                    double count = l * koeffTrub;
                    elem.LookupParameter("ADSK_Количество").Set(count);
                }
                catch (Exception e) { Logger.Log( "   элемент " + elem.Id.ToString() + " ошибка: " + e.Message,4); }
                
            }
            
        }
        public static void PEXpipesParamsSetFromExcel(in ElementId elemId, 
            in List<string> PEXPipeDiams, in List<string> PEXPipeMass, in List<string> PEXPipeArts, in List<string> PEXPipeMans, in List<string> PEXPipeTypes)
        {
            Document doc = RevitAPI.Document;
            Element elem = doc.GetElement(elemId);
            if (elem != null)
            {
                Element eType = doc.GetElement(elem.GetTypeId());
                double d = elem.LookupParameter("Диаметр").AsDouble();
                d = Math.Round(d * 304.8, 0);
                
                //назначение параметров согласно таблице
                
                List<int> rowNums = new List<int>();

                for (int i = 0; i < PEXPipeDiams.Count; i++)
                {
                    string rangetxt = PEXPipeDiams[i]; double diamFromTable = 0; Double.TryParse(rangetxt, out diamFromTable);
                    if (rangetxt != "")
                    {
                        if (diamFromTable == d) rowNums.Add(i); //получаем строки с данным диаметром для разных производителей
                    }
                }
                string manuf = eType.LookupParameter("Изготовитель").AsString();
                if (manuf != null && manuf != "" && rowNums.Count > 0)
                {

                    for (int i = 0; i < rowNums.Count; i++)
                    {
                        string typeFromTable = PEXPipeTypes[rowNums[i]];
                        string manufFromTable = PEXPipeMans[rowNums[i]];
                        if (manufFromTable == manuf && typeFromTable == eType.Name)
                        {
                            string massText = PEXPipeMass[rowNums[i]];
                            double mass = 0; Double.TryParse(massText, out mass);
                            string artText = PEXPipeArts[rowNums[i]];
                            try
                            {
                                elem.LookupParameter("ADSK_Завод-изготовитель").Set(manuf);
                                elem.LookupParameter("ADSK_Масса").Set(mass);
                                elem.LookupParameter("ADSK_Код изделия").Set(artText);
                            }
                            catch (Exception e) { Logger.Log( "   элемент " + elem.Id.ToString() + " ошибка: " + e.Message, 4); }
                            break;
                        }
                        else
                        {
                            try
                            {
                                elem.LookupParameter("ADSK_Завод-изготовитель").Set(manuf);
                                elem.LookupParameter("ADSK_Код изделия").Set("");
                            }
                            catch (Exception e) { Logger.Log( "   элемент " + elem.Id.ToString() + " ошибка: " + e.Message, 4); }
                        }
                    }

                }

                
            }

        }
        public void PEXPipeSetParams(in DateTime dateTime, in string TNovClassName, in Element elem, in double koeffTrub,
            in List<string> PEXPipeDiams, in List<string> PEXPipeMass, in List<string> PEXPipeArts, in List<string> PEXPipeMans, in List<string> PEXPipeTypes, out bool success)
        {
            success = true; 
            Document doc = RevitAPI.Document;
            string type = "По умолчанию";
            Element eType = doc.GetElement(elem.GetTypeId());
            string ePipeCode = eType.LookupParameter("VM_Код трубы").AsString(); if (ePipeCode != null) type = ePipeCode;
            Logger.Log(type,2);
            string komm = eType.LookupParameter("Комментарии к типоразмеру").AsString(); if (komm == null) komm = "";
            double dnar = elem.LookupParameter("Внешний диаметр").AsDouble();
            double d = elem.LookupParameter("Диаметр").AsDouble();
            bool adskNparamexist = Param.ParamExist("ADSK_Толщина стенки", elem);
            double t = 0;
            if (adskNparamexist) { t = elem.LookupParameter("ADSK_Толщина стенки").AsDouble(); }
            double l = elem.LookupParameter("Длина").AsDouble();
            dnar = Math.Round(dnar * 304.8, 0); d = Math.Round(d * 304.8, 0); t = Math.Round(t * 304.8, 1); l = Math.Round(l * 0.3048, 1);
            if (type == "PEX")
            {
                success = false;
                PEXpipesParamsSet(elem.Id, koeffTrub);
                PEXpipesParamsSetFromExcel(elem.Id, PEXPipeDiams, PEXPipeMass, PEXPipeArts, PEXPipeMans, PEXPipeTypes);
                success = true;
            }
        }
        public void PEXFitsSetParams(in DateTime dateTime, in string TNovClassName, in ElementId eId, in List<string> PEXFitCodes, in List<string> PEXFitArt1, in List<string> PEXFitArt2, in List<string> PEXFitArt3, out bool success)
        {
            Logger.Log("   элемент " + eId.ToString(),2);
            success = true;
            Document doc = RevitAPI.Document;
            Element elem = doc.GetElement(eId);
            
            if (elem != null)
            {
                string fitCode = "По умолчанию";
                bool fitCodeparamexist = Param.ParamExist("Код фитинга", elem);
                if (fitCodeparamexist) 
                {
                    string fitCodeP = elem.LookupParameter("Код фитинга").AsString(); if (fitCodeP != null) fitCode = fitCodeP;
                }
                Logger.Log("   код фитинга " + fitCode,2);
                Element elemType = doc.GetElement(elem.GetTypeId());
                Logger.Log("   тип " + elemType.Id.ToString(),2);
                if (fitCode.Contains("СШПЭ"))
                {
                    FamilyInstance fi = (FamilyInstance)elem;
                    if (!fi.Symbol.Family.Name.Contains("Влж"))
                    {
                        success = false;
                        ConnectorSet cSet = fi.MEPModel.ConnectorManager.Connectors;
                        foreach (Connector connector in cSet)
                        {
                            if (connector.IsConnected)
                            {
                                ConnectorSet REFS = connector.AllRefs;
                                foreach (Connector r in REFS)
                                {
                                    if (r.Domain == Domain.DomainPiping)
                                    {
                                        Element pipe = r.Owner;
                                        ElementId pipeCatId = new ElementId(BuiltInCategory.OST_PipeCurves);
                                        if(pipe.Category.Id== pipeCatId)
                                        {
                                            Element pipeType = doc.GetElement(pipe.GetTypeId());
                                            string pipeNaim = pipe.LookupParameter("ADSK_Наименование").AsString(); pipeNaim = pipeNaim.Replace(",", ".");
                                            string pipeCode = pipe.LookupParameter("ADSK_Код изделия").AsString(); if (pipeCode == null) pipeCode = "";
                                            double pipeMass = pipe.LookupParameter("ADSK_Масса").AsDouble();
                                            string pipeManuf = pipe.LookupParameter("ADSK_Завод-изготовитель").AsString(); if (pipeManuf == null) pipeManuf = "";
                                            if (fitCode.Contains("Труба"))
                                            {
                                                try
                                                {
                                                    elem.LookupParameter("ADSK_Наименование").Set(pipeNaim);
                                                    elem.LookupParameter("ADSK_Код изделия").Set(pipeCode);
                                                    elem.LookupParameter("ADSK_Масса").Set(pipeMass);

                                                    elemType.LookupParameter("ADSK_Завод-изготовитель").Set(pipeManuf);

                                                    success = true;
                                                }
                                                catch (Exception e) { Logger.Log("   элемент " + elem.Id.ToString() + " ошибка: " + e.Message, 4); }
                                            }
                                            else
                                            {
                                                List<string> arts= new List<string>();
                                                switch (pipeManuf)
                                                {
                                                    case "Valtec":
                                                        foreach (var a in PEXFitArt1) arts.Add(a);
                                                        break;
                                                    case "STOUT":
                                                        foreach (var a in PEXFitArt2) arts.Add(a);
                                                        break;
                                                    case "REHAU":
                                                        foreach (var a in PEXFitArt3) arts.Add(a);
                                                        break;
                                                }
                                                if (arts.Count > 0)
                                                {
                                                    for (int i = 0; i < PEXFitCodes.Count; i++)
                                                    {
                                                        string rangetxt = PEXFitCodes[i]; if (rangetxt == null) rangetxt = "";
                                                        if (rangetxt != "")
                                                        {
                                                            if (rangetxt == fitCode)
                                                            {
                                                                string range1txt = arts[i]; if (range1txt == null) range1txt = "";
                                                                try
                                                                {
                                                                    elem.LookupParameter("ADSK_Код изделия").Set(range1txt);
                                                                    elemType.LookupParameter("ADSK_Завод-изготовитель").Set(pipeManuf);
                                                                    success = true;
                                                                }
                                                                catch (Exception e) { Logger.Log("   элемент " + elem.Id.ToString() + " ошибка: " + e.Message, 4); }
                                                            }
                                                        }
                                                    }
                                                }
                                                else
                                                {
                                                    try
                                                    {
                                                        elem.LookupParameter("ADSK_Код изделия").Set("");
                                                        elemType.LookupParameter("ADSK_Завод-изготовитель").Set(pipeManuf);
                                                        success = true;
                                                    }
                                                    catch (Exception e) { Logger.Log("   элемент " + elem.Id.ToString() + " ошибка: " + e.Message,4); }
                                                }
                                            
                                            }
                                            break;
                                        }                                        
                                    }
                                }
                            }
                        }
                    }
                }
            }            
        }
    }
}
