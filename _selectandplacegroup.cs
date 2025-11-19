using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using System;

namespace TNov
{
    [Transaction(TransactionMode.Manual)]
    
    public class _selectandplacegroup: IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            //Получение объектов приложения и документа
            UIApplication uiApp = commandData.Application;
            Document doc = uiApp.ActiveUIDocument.Document;
            try
            {
                //Определение объекта-ссылки для занесения результата указания
                Reference pickedRef = null;
                //Указание группы
                Selection sel = uiApp.ActiveUIDocument.Selection;
                GroupPickFilter selFilter = new GroupPickFilter();
                pickedRef = sel.PickObject(ObjectType.Element, selFilter,
                "Выберите группу");
                Element elem = doc.GetElement(pickedRef);
                Group group = elem as Group;
                //Указание точки
                XYZ point = sel.PickPoint("Укажите точку для размещения группы");
                //Размещение группы
                Transaction trans = new Transaction(doc);
                trans.Start("Lab");
                doc.Create.PlaceGroup(point, group.GroupType);
                trans.Commit();
            }
            //Обработка исключения при щелчке правой кнопкой или нажатии ESC
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                return Result.Cancelled;
            }
            //Обработка других ошибок
            catch (Exception ex)
            {
                message = ex.Message;
                return Result.Failed;
            }

            
            return Result.Succeeded;
        }        
    }

    public class GroupPickFilter : ISelectionFilter
    {
        public bool AllowElement(Element e)
        {
            return (e.Category.Id.IntegerValue.Equals(
            (int)BuiltInCategory.OST_IOSModelGroups));
        }
        public bool AllowReference(Reference r, XYZ p)
        {
            return false;
        }
    }

}
