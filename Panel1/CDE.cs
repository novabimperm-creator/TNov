using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.Attributes;
using System.Collections.Generic;
using TNov.main;
using System.IO;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.StartPanel;

namespace TNov
{
    [Transaction(TransactionMode.Manual)]
    public class CDE : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            //подключение приложения и документа
            if (RevitAPI.UiApplication == null) { RevitAPI.Initialize(commandData); }
            UIDocument uidoc = RevitAPI.UiDocument; Document doc = RevitAPI.Document;
            UIApplication uiApp = RevitAPI.UiApplication; Autodesk.Revit.ApplicationServices.Application rvtApp = uiApp.Application;
            //проверка подключения, запись в журнал
            string TNovClassName = "СОД"; bool check = false; servercheck sc = new servercheck(in TNovClassName, out check); if (check == false) { return Result.Failed; }
            
            string link = "https://vitro.pm-nova.ru/site/3064dc08-2e02-8de4-aa70-1b2ae9eb890b/list/716e8d52-90dc-4c85-bddf-582c94ab505e/view/8b1da051-3973-4fff-a1a0-fed77a0ea8e0?treeList=966e62c5-a803-49a0-a1be-e680d130c481";
            
            string file = File.ReadAllText(nova.novaserver + "_TNov/CDE.txt");
            string[] lines = file.Split('\n');
            List<string> projects = new List<string>(); List<string> links = new List<string>();
            foreach (string line in lines) { string[] elems = line.Split(','); projects.Add(elems[0]); links.Add(elems[1]); }
            string docName = doc.Title.ToString(); docName = docName.Replace(",", " ");
            string userName = rvtApp.Username; string docNameUserName = "_" + userName; docName = docName.Replace(docNameUserName, "");
            int i = 0;
            foreach (string p in projects)
            {
                if (docName.Contains(p)) { link = links[i]; break; }
                i++;
            }

            string commandText = @link;
            var proc = new System.Diagnostics.Process();
            proc.StartInfo.FileName = commandText;
            proc.StartInfo.UseShellExecute = true;
            proc.Start();

            return Result.Succeeded;
        }
    }
}

