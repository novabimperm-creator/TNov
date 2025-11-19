using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.UI.Selection;
using System;
using TNov.main;

namespace TNov
{
    [Transaction(TransactionMode.Manual)]
    [RegenerationAttribute(RegenerationOption.Manual)]

    public class paint2 : IExternalCommand
    {
       
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            string TNovClassName = "Материал?"; DateTime dateTime = DateTime.Now;
            //подключение приложения и документа
            if (RevitAPI.UiApplication == null) { RevitAPI.Initialize(commandData); }
            UIDocument uidoc = RevitAPI.UiDocument; Document doc = RevitAPI.Document;
            UIApplication uiApp = RevitAPI.UiApplication; Autodesk.Revit.ApplicationServices.Application rvtApp = uiApp.Application;
            
            //проверка подключения, запись в журнал
            bool check = false; servercheck sc = new servercheck(in TNovClassName, out check); if (check == false) { return Result.Failed; }

            // создание log - файла
            Logger.Initialize(TNovClassName);
            

            Logger.Log("Выбор грани", 1);
            Selection selection = uidoc.Selection;

            Reference faceRef = null;

            try
            {
                faceRef = selection.PickObject(ObjectType.Face, "Выберите исходную грань (Esc - отмена)");
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException e)
            {
                Logger.Log("Отменено: " + e.Message + " .Завершение работы.", 3); return Result.Cancelled;
            }

            GeometryObject geoObject = doc.GetElement(faceRef).GetGeometryObjectFromReference(faceRef);

            PlanarFace planarFace = geoObject as PlanarFace;

            ElementId matId = planarFace.MaterialElementId;

            Element mat = doc.GetElement(matId);

            string txt = mat.Name;

            Logger.Log("Материал " + matId.ToString()+ " - "+ txt,1);
            Logger.Log("Диалоговое окно",1);

            // Диалоговое окно
            var viewModel = new infowindowtextfieldViewModel();
            viewModel.headtxt = "Элементу/грани назначен следующий материал:";
            viewModel.ids = txt;
            viewModel.lowtxt = "Вы можете найти его в Диспетчере материалов.";
            var wpfview = new infowindowtextfield(viewModel);
            viewModel.CloseRequest += (s, e) => wpfview.Close();
            bool? ok = wpfview.ShowDialog();

            Logger.Log("Завершение работы.",5);
            return Result.Succeeded;
        }
    }
}
