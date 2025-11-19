using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.Attributes;
using System.Collections.Generic;
using System.Linq;
using System;
using TNov.main;

namespace TNov
{


    [Transaction(TransactionMode.Manual)]
    public class foundnumpurge : IExternalCommand
    {
                
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            string TNovClassName = "Сваи Убрать пробелы дубли"; DateTime dateTime = DateTime.Now;
            //подключение приложения и документа
            if (RevitAPI.UiApplication == null) { RevitAPI.Initialize(commandData); }
            UIDocument uidoc = RevitAPI.UiDocument; Document doc = RevitAPI.Document;
            UIApplication uiApp = RevitAPI.UiApplication; Autodesk.Revit.ApplicationServices.Application rvtApp = uiApp.Application;
            
            //проверка подключения, запись в журнал
            bool check = false; servercheck sc = new servercheck(in TNovClassName, out check); if (check == false) { return Result.Failed; }

            //Проверка актуальности шаблона
            templatecheck tc = new templatecheck(in commandData, out bool oldProject);

            // создание log - файла
            Logger.Initialize(TNovClassName);
            

            //Список используемых параметров

            BuiltInParameter gm = BuiltInParameter.ALL_MODEL_MODEL; //параметр Группа модели
            string parameterName = "N_Свая.Номер"; if (oldProject == true) { parameterName = "Свая.Номер"; }

            Logger.Log("Сбор элементов",1);
            
            List<FamilyInstance> piles = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_StructuralFoundation)   //Фундаменты семействами
                                                                         .WhereElementIsNotElementType()
                                                                         .OfClass(typeof(Autodesk.Revit.DB.FamilyInstance))
                                                                         .Cast<FamilyInstance>()
                                                                         .ToList();

            List<FamilyInstance> piles1 = new List<FamilyInstance>();

            foreach (var p in piles) //ищем сваи
            {
                string pvalue = p.Symbol.get_Parameter(gm).AsString();
                if (pvalue != null)
                {
                    if (pvalue.Contains("Свая")) { piles1.Add(p); }
                }
            }

            int pc = piles1.Count;
            if(pc ==  0) 
            { 
                new infowindow280("В проекте отсутствуют сваи.").ShowDialog();
                Logger.Log("В проекте отсутствуют сваи. Завершение работы.", 3);
                return Result.Failed; 
            }

            List<Pile> pilestowork = new List<Pile>(); //список свай-Pile
            foreach (var p in piles1)
            {
                Element elem = doc.GetElement(p.Id); 
                int.TryParse(p.LookupParameter(parameterName).AsString(), out int num);
                Pile pl = new Pile();
                pl.elemid = p.Id; pl.sort = num; pl.z = 0; pl.type = pl.type = elem.GetTypeId().ToString(); ;
                pilestowork.Add(pl);
            }

            var pilessorted = from pl in pilestowork //сортированный список свай-Pile по номеру
                            orderby pl.sort
                            select pl;


            using (Transaction transaction = new Transaction(doc))
            {
                transaction.Start("TNov - автонумерация свай");
                Logger.Log("Открываем транзакцию",1);
                int i = 1;

                foreach (var p in pilessorted)
                {
                    Element elem = doc.GetElement(p.elemid);
                    Logger.Log("Элемент " +elem.Id.ToString()+" старый номер "+elem.LookupParameter(parameterName).AsString(),1);
                    elem.LookupParameter(parameterName)?.Set(i.ToString());
                    Logger.Log("   новый номер " + i.ToString(),1);
                    i++;
                }
                
                var info1 = new infowindow280("Успешно!"); info1.ShowDialog();
                transaction.Commit();
                Logger.Log("Закрываем транзакцию",1);

            }
                
            Logger.Log("Завершение работы.",5);
            return Result.Succeeded;
        }
    }
    
}
