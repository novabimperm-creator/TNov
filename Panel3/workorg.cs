using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.Attributes;

namespace TNov
{
    [Transaction(TransactionMode.Manual)]
    public class WorkOrg : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            //подключение приложения и документа
            if (RevitAPI.UiApplication == null) { RevitAPI.Initialize(commandData); }
            UIDocument uidoc = RevitAPI.UiDocument; Document doc = RevitAPI.Document;
            UIApplication uiApp = RevitAPI.UiApplication; Autodesk.Revit.ApplicationServices.Application rvtApp = uiApp.Application;
            //проверка подключения, запись в журнал
            string TNovClassName = "Методички"; bool check = false; servercheck sc = new servercheck(in TNovClassName, out check); if (check == false) { return Result.Failed; }

            string commandText = @"https://portal.talan.group/knowledge/proektirovanie/";
            var proc = new System.Diagnostics.Process();
            proc.StartInfo.FileName = commandText;
            proc.StartInfo.UseShellExecute = true;
            proc.Start();

            return Result.Succeeded;
        }
    }
}

