using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
using TNov.main;

namespace TNov
{
    [Transaction(TransactionMode.Manual)]
    public class finishing : IExternalCommand
    {
        private TNovProgressBar levnumProgressBar;
        private void ThreadStartingPoint()
        {
            this.levnumProgressBar = new TNovProgressBar();
            this.levnumProgressBar.Show();
            Dispatcher.Run();
        }
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            string TNovClassName = "Отделка"; DateTime dateTime = DateTime.Now;
            //подключение приложения и документа
            if (RevitAPI.UiApplication == null) { RevitAPI.Initialize(commandData); }
            UIDocument uidoc = RevitAPI.UiDocument; Document doc = RevitAPI.Document;
            UIApplication uiApp = RevitAPI.UiApplication; Autodesk.Revit.ApplicationServices.Application rvtApp = uiApp.Application;

            //проверка подключения, запись в журнал
            bool check = false; servercheck sc = new servercheck(in TNovClassName, out check); if (check == false) { return Result.Failed; }

            // создание log - файла
            Logger.Initialize(TNovClassName);

            var walls = new FilteredElementCollector(doc)
                .OfCategory(BuiltInCategory.OST_Walls)
                .WhereElementIsNotElementType()
                .OfClass(typeof(Wall))
                .Cast<Wall>()
                .Where(w => w.WallType != null && w.WallType.Kind == WallKind.Basic)
                .ToList();

            var floors = new FilteredElementCollector(doc)
                .OfCategory(BuiltInCategory.OST_Floors)
                .WhereElementIsNotElementType()
                .OfClass(typeof(Floor))
                .Cast<Floor>()
                .ToList();

            var ceilings = new FilteredElementCollector(doc)
                .OfCategory(BuiltInCategory.OST_Ceilings)
                .WhereElementIsNotElementType()
                .OfClass(typeof(Ceiling))
                .Cast<Ceiling>()
                .ToList();

            List<Element> elems = new List<Element>();
            foreach(var wall in walls)
            {
                Element type = doc.GetElement(wall.GetTypeId());
                if(type.get_Parameter(BuiltInParameter.ALL_MODEL_MODEL).AsString().Contains("Отделка")) elems.Add(doc.GetElement(wall.Id));
            }
            foreach (var floor in floors)
            {
                Element type = doc.GetElement(floor.GetTypeId());
                if (type.get_Parameter(BuiltInParameter.ALL_MODEL_MODEL).AsString().Contains("Пол")) elems.Add(doc.GetElement(floor.Id));
            }
            foreach (var ceiling in ceilings)
            {
                Element type = doc.GetElement(ceiling.GetTypeId());
                if (type.get_Parameter(BuiltInParameter.ALL_MODEL_MODEL).AsString().Contains("Потолок")) elems.Add(doc.GetElement(ceiling.Id));
            }
            if (elems.Count == 0) { Logger.Log("Отсутствуют элементы отделки. Завершение работы", 3); return Result.Cancelled; }

            int allcount = elems.Count;
            

            using (Transaction transaction = new Transaction(doc))
            {
                transaction.Start("TNov - Ведомость отделки");
                Logger.Log("Открываем транзакцию", 1);

                foreach(var elem in elems)
                {
                    Logger.Log("Элемент " + elem.Id.IntegerValue.ToString(), 2);
                    Parameter roomParam = elem.LookupParameter("N_Отделка.Помещение");
                    if (roomParam != null) roomParam.Set(""); //очищаем параметр, чтобы отработали апдейтеры
                }


                transaction.Commit();
                
                Logger.Log("Закрываем транзакцию.", 1);
            }

            new infowindow280("Готово! Параметры отделки заполнены.").ShowDialog();

            Logger.Log("Завершение работы.", 5);
            return Result.Succeeded;
        }
        

        
        
    }
}
