using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.Attributes;
using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.UI.Selection;
using Autodesk.Revit.DB.Electrical;
using Newtonsoft.Json;
using TNov.main;
using System.IO;

namespace TNov
{



    [Transaction(TransactionMode.Manual)]
    public class cabletraycapdelete : IExternalCommand
    {
        public class CableTraySelectionFilter : ISelectionFilter
        {
            public bool AllowElement(Element elem)
            {
                if (elem.Category.Name == "Кабельные лотки") { return true; }
                else return false;
            }

            public bool AllowReference(Reference reference, XYZ position)
            {
                return true;
            }
        }
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            string TNovClassName = "Удалить крышки"; DateTime dateTime = DateTime.Now;
            //подключение приложения и документа
            if (RevitAPI.UiApplication == null) { RevitAPI.Initialize(commandData); }
            UIDocument uidoc = RevitAPI.UiDocument; Autodesk.Revit.DB.Document doc = RevitAPI.Document;
            UIApplication uiApp = RevitAPI.UiApplication; Autodesk.Revit.ApplicationServices.Application rvtApp = uiApp.Application;
            
            //проверка подключения, запись в журнал
            bool check = false; servercheck sc = new servercheck(in TNovClassName, out check); if (check == false) { return Result.Failed; }

            // создание log - файла
            Logger.Initialize(TNovClassName);

            BuiltInParameter mark = BuiltInParameter.DOOR_NUMBER; //параметр Марка

            //сбор элементов

            Logger.Log("Сбор элементов", 1);

            //проверка наличия крышек в проекте
            List<FamilyInstance> GMs = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_GenericModel)   //фильтр по категории Об модели
                                                                         .WhereElementIsNotElementType()
                                                                         .OfClass(typeof(FamilyInstance))
                                                                         .Cast<FamilyInstance>()
                                                                         .ToList();

            List<FamilySymbol> familytypes = new FilteredElementCollector(doc).OfClass(typeof(FamilySymbol))
                .Cast<FamilySymbol>()
                .ToList();
            
            //заполняем списки

            List<FamilyInstance> caps = new List<FamilyInstance>(); //список крышек в проекте            
            foreach(FamilyInstance g in GMs)
            {
                if (g.Symbol.FamilyName.Contains("Крышка")) 
                { 
                    caps.Add(g); 
                }
            }
            List<FamilySymbol> captypes = new List<FamilySymbol>(); //список типов крышек в проекте
            Logger.Log("Типы крышек:",1);
            foreach (FamilySymbol t in familytypes)
            {
                
                if (t.FamilyName.Contains("Крышка на лоток"))
                {
                    captypes.Add(t); Logger.Log("   " + t.Name,1);
                }
            }


            Logger.Log("Проверяем наличие крышек в проекте", 1);
            if (captypes.Count == 0) 
            {
                new infowindow280("Ошибка! В проекте отсутствуют загруженные семейства крышек.").ShowDialog();
                Logger.Log("Отсутствуют семейства крышек. Завершение работы.", 3);
                return Result.Failed;
            }

            
            //анализ текущей выборки
            Logger.Log("Анализ текущей выборки", 1);
            Autodesk.Revit.UI.Selection.Selection selection = commandData.Application.ActiveUIDocument.Selection;
            List<CableTray> CTList = new List<CableTray>();
            CTList = cabletraycapdelete.GetCableTraysFromCurrentSelection(doc, selection); //получаем лотки из текущей выборки
            if (CTList.Count == 0) //запускаем выбор элементов если ничего не выбрано
            {
                CableTraySelectionFilter CTSelectionFilter = new CableTraySelectionFilter();
                IList<Reference> referenceList;
                try
                {
                    referenceList = selection.PickObjects((ObjectType)1, (ISelectionFilter)CTSelectionFilter, "Выберите кабельные лотки");
                }
                catch (Autodesk.Revit.Exceptions.OperationCanceledException ex)
                {
                    Logger.Log("Отменено: " + ex.Message + ". Завершение работы", 3); return Result.Cancelled;
                }
                foreach (Reference reference in (IEnumerable<Reference>)referenceList)
                    CTList.Add(doc.GetElement(reference) as CableTray);
            }

            if (CTList.Count < 1) { Logger.Log("Отсутствуют лотки в выборке. Завершение работы", 3); return Result.Cancelled; }

            //отсеиваем лотки "не специфицировать"

            List<CableTray> CTList1 = new List<CableTray>();

            foreach(CableTray ct in CTList)
            {
                Element elem = doc.GetElement(ct.Id);
                int parval = elem.LookupParameter("N_ЭЛ.Не специфицировать").AsInteger();
                if (parval != 1) { CTList1.Add(ct); }
            }

            Logger.Log("Элементы собраны. Диалоговое окно", 1);
            

            

            int count = 0;

            //транзакция
            using (Transaction transaction = new Transaction(doc))
            {
                
                transaction.Start("TNov - Удалить крышки");
                Logger.Log("Открываем транзакцию",1);

                

                //обновленные списки

                List<FamilyInstance> GMs1 = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_GenericModel)   //фильтр по категории Об модели
                                                             .WhereElementIsNotElementType()
                                                             .OfClass(typeof(FamilyInstance))
                                                             .Cast<FamilyInstance>()
                                                             .ToList();
                List<FamilyInstance> caps1 = new List<FamilyInstance>();
                foreach (FamilyInstance g in GMs1)
                {
                    if (g.Symbol.FamilyName.Contains("Крышка"))
                    {
                        caps1.Add(g);
                    }
                }

                //удаляем крышки, которые будут пересозданы
                Logger.Log("Удаляем крышки",1);
                ICollection<ElementId> capstoremove = new List<ElementId>();
                
                
                foreach (FamilyInstance cap in caps1)
                {
                    Element elem = doc.GetElement(cap.Id);
                    int mrkint = 0;
                    string mrkstr = elem.get_Parameter(mark).AsString();
                    if (mrkstr != null)
                    {
                        int.TryParse(mrkstr, out mrkint);
                        if (mrkint != 0)
                        {
                            foreach (CableTray ct in CTList1)
                            {
                                Element elem1 = doc.GetElement(ct.Id);
                                int ctid = elem1.Id.IntegerValue;
                                if (ctid == mrkint) { capstoremove.Add(cap.Id); count++; break; }
                            }
                        }
                    }
                }
                if (capstoremove.Count > 0) { doc.Delete(capstoremove.ToArray()); Logger.Log("Удалено " + count.ToString() + " элементов",1); }
                else { Logger.Log("крышки отсутствуют",1); }
                

               
                transaction.Commit();
                Logger.Log("Закрываем транзакцию", 1);

                if (count > 0)
                {
                    if (count == 1) { var info1 = new infowindow280("Успешно!\nКрышка лотка удалена."); info1.ShowDialog(); }
                    else { var info1 = new infowindow280("Успешно!\nУдалены крышки в количестве " + count.ToString() + " шт."); info1.ShowDialog(); }
                }

                

            }

            

            return Result.Succeeded;
        }
        private static List<CableTray> GetCableTraysFromCurrentSelection(Autodesk.Revit.DB.Document doc, Autodesk.Revit.UI.Selection.Selection sel)
        {
            ICollection<ElementId> elementIds = sel.GetElementIds();
            List<CableTray> currentSelection = new List<CableTray>();
            foreach (ElementId elementId in (IEnumerable<ElementId>)elementIds)
            {
                if (doc.GetElement(elementId) is CableTray && doc.GetElement(elementId).Category != null && doc.GetElement(elementId).Category.Id.IntegerValue.Equals(-2008130))
                    currentSelection.Add(doc.GetElement(elementId) as CableTray);
            }
            return currentSelection;
        }
    }
}
