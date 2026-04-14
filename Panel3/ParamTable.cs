using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.Attributes;

namespace TNov
{
    [Transaction(TransactionMode.Manual)]
    public class ParamTable : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            //подключение приложения и документа
            if (RevitAPI.UiApplication == null) { RevitAPI.Initialize(commandData); }
            UIDocument uidoc = RevitAPI.UiDocument; Document doc = RevitAPI.Document;
            UIApplication uiApp = RevitAPI.UiApplication; Autodesk.Revit.ApplicationServices.Application rvtApp = uiApp.Application;
            //проверка подключения, запись в журнал
            string TNovClassName = "Таблица параметров"; if(ServerUtils.CheckConnection(TNovClassName)==false) return Result.Failed;

            string commandText = @"https://docs.google.com/spreadsheets/d/1wy0jC_Cu88-CwqFlOf7DTTegnbq6EKwA/edit?usp=sharing&ouid=108474421924088534006&rtpof=true&sd=true";
            var proc = new System.Diagnostics.Process();
            proc.StartInfo.FileName = commandText;
            proc.StartInfo.UseShellExecute = true;
            proc.Start();

            return Result.Succeeded;
        }
    }
}
