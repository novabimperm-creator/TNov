using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System;
using static System.Windows.Forms.LinkLabel;

namespace TNov
{
    public class CheckOldTemplateProject
    {
        public static bool OldTemplateProject(ExternalCommandData commandData) //устаревший класс, используется локально в некоторых функциях
        {
            if (RevitAPI.UiApplication == null) { RevitAPI.Initialize(commandData); }
            UIDocument uidoc = RevitAPI.UiDocument; Document doc = RevitAPI.Document;
            UIApplication uiApp = RevitAPI.UiApplication; Autodesk.Revit.ApplicationServices.Application rvtApp = uiApp.Application; 
            ProjectInfo projectInfo = doc.ProjectInformation;
            Autodesk.Revit.DB.Parameter template = projectInfo.LookupParameter("N_Орг.ВерсияШаблона");
            string templateversion = "v";
            if (template == null) return true; 
            else { templateversion = template.AsValueString(); }
            templateversion = templateversion.Replace(" (Talan)", "");
            templateversion = templateversion.Replace("(Talan)", "");
            templateversion = templateversion.Replace(" (UDS)", "");
            templateversion = templateversion.Replace("(UDS)", "");
            if (templateversion.Contains("v")) return true;
            else
            {
                string[] versionparts = templateversion.Split('.');
                double versionMath = Convert.ToDouble(versionparts[0]) * 10 + Convert.ToDouble(versionparts[1]);
                if (versionMath < 20223) return true;
            }
            string docName = doc.Title.ToString(); //для разделов инженерных сетей - всегда "старый" шаблон
            if (docName.Contains("-ВК")|| docName.Contains("_ВК")) return true;
            if (docName.Contains("-ОВ") || docName.Contains("_ОВ")) return true;
            if (docName.Contains("-ЭО") || docName.Contains("_ЭО")) return true;
            if (docName.Contains("-ЭЛ") || docName.Contains("_ЭЛ")) return true;
            if (docName.Contains("-ЭЭ") || docName.Contains("_ЭЭ")) return true;
            if (docName.Contains("-ЭС") || docName.Contains("_ЭС")) return true;
            if (docName.Contains("-СС") || docName.Contains("_СС")) return true;
            if (docName.Contains("-ССВ") || docName.Contains("_ССВ")) return true;
            if (docName.Contains("-АПС") || docName.Contains("_АПС")) return true;
            if (docName.Contains("Задани") || docName.Contains("задани") || docName.Contains("-ЗД") || docName.Contains("_ЗД") || docName.Contains("ЗАДАНИЕ")) return true;

            return false;
        }
    }
}
