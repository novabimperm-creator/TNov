using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.Attributes;
using System.Collections.Generic;
using System.Linq;
using System;
using Parameter = Autodesk.Revit.DB.Parameter;
using TNov.main;

namespace TNov
{
    [Transaction(TransactionMode.Manual)]
    public class mirror : IExternalCommand
    {
        
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            string TNovClassName = "Антизеркало"; DateTime dateTime = DateTime.Now;
            //подключение приложения и документа
            if (RevitAPI.UiApplication == null) { RevitAPI.Initialize(commandData); }
            UIDocument uidoc = RevitAPI.UiDocument; Document doc = RevitAPI.Document;
            UIApplication uiApp = RevitAPI.UiApplication; Autodesk.Revit.ApplicationServices.Application rvtApp = uiApp.Application;
            
            //проверка подключения, запись в журнал
            bool check = false; servercheck sc = new servercheck(in TNovClassName, out check); if (check == false) { return Result.Failed; }

            // создание log - файла
            Logger.Initialize(TNovClassName);
            

            Logger.Log("Сбор элементов",1);

            BuiltInParameter gm = BuiltInParameter.ALL_MODEL_MODEL; //параметр Группа модели

            BuiltInParameter mrk = BuiltInParameter.ALL_MODEL_MARK; //параметр Марка

            List<FamilyInstance> windows = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_Windows)   //фильтр по категории Окна
                                                                         .WhereElementIsNotElementType()
                                                                         .Cast<FamilyInstance>()
                                                                         //.Where(it => it.Symbol.get_Parameter(gm).AsString() == "Окно") //только род семейства
                                                                         .ToList();

            List<FamilyInstance> doors = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_Doors)   //фильтр по категории Двери
                                                                         .WhereElementIsNotElementType()
                                                                         .Cast<FamilyInstance>()
                                                                         //.Where(it => it.Symbol.get_Parameter(gm).AsString() == "Дверь") //только род семейства
                                                                         .ToList();
            
            List<FamilyInstance> elems = new List<FamilyInstance>(); 

            foreach(FamilyInstance f in windows)
            {
                string fvalue = f.Symbol.get_Parameter(gm).AsString();
                Element element = (Element)f;
                if (fvalue != null)
                {
                    if (fvalue.Contains(".")) { }
                    else if (fvalue.Contains("Окно")) { elems.Add(f); }
                }
                if (element.Name.Contains("Витраж")) { elems.Add(f); }
            }
            foreach (FamilyInstance f in doors)
            {
                string fvalue = f.Symbol.get_Parameter(gm).AsString();
                Element element = (Element)f;
                if (fvalue != null)
                {
                    if (fvalue=="Дверь") { elems.Add(f); }
                }
                if (element.Name.Contains("Витраж")) { elems.Add(f); }
            }

            int failscount = 0;
            List<string> failed = new List<string>(); //пустой список id элементов отзеркаленных

            
            using (Transaction transaction = new Transaction(doc))
            {
                Logger.Log("Открываем транзакцию",1); 
                transaction.Start("TNov - Антизеркало");
                
                foreach (var elem in elems) 
                {
                    string eid = elem.Id.ToString();
                    bool m = elem.Mirrored; 
                    if (m) {Logger.Log("Элемент " + eid+" отзеркален",1); }
                    Parameter elmrk = elem.get_Parameter(mrk);
                    if (m) { failed.Add(eid); failscount++; elmrk.Set("зеркальный"); }
                    else
                    {
                        string mrkvalue = elem.get_Parameter(mrk).AsValueString();
                        if (mrkvalue != null) 
                        {
                            mrkvalue = mrkvalue.Replace("зеркальный", "");
                            elmrk.Set(mrkvalue);
                        }
                    }
                }
                
                if (failscount == 0) 
                {
                    string info1txt = "Отлично! Отзеркаленные элементы отсутствуют.";
                    var info1 = new infowindow400(info1txt); info1.ShowDialog();
                }
                else
                {
                    Logger.Log("Открываем окно с ID проблемных элементов", 1);
                    // Диалоговое окно
                    var viewModel = new infowindowtextfieldViewModel();
                    viewModel.headtxt = "Один или несколько элементов отзеркалены:";
                    viewModel.ids = String.Join(",", failed);
                    viewModel.lowtxt = "Требуется исправить это недоразумение.";
                    var wpfview = new infowindowtextfield(viewModel);
                    viewModel.CloseRequest += (s, e) => wpfview.Close();
                    bool? ok = wpfview.ShowDialog();
                    Logger.Log(viewModel.ids, 1);
                    Logger.Log("Выделяем проблемные элементы в модели", 1);
                    uidoc.Selection.SetElementIds(failed.Select(s => new ElementId(int.Parse(s))).ToArray());
                }
   
                transaction.Commit();
                Logger.Log("Закрываем транзакцию",1);

            }
            Logger.Log("Завершение работы.",5);

            return Result.Succeeded;
        }
    }
    
}
