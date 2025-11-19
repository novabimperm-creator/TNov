using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System;
using static System.Windows.Forms.LinkLabel;

namespace TNov
{
    public class templatecheck
    {
        public templatecheck(in ExternalCommandData commandData, out bool oldProject)
        {
            if (RevitAPI.UiApplication == null) { RevitAPI.Initialize(commandData); }
            UIDocument uidoc = RevitAPI.UiDocument; Document doc = RevitAPI.Document;
            UIApplication uiApp = RevitAPI.UiApplication; Autodesk.Revit.ApplicationServices.Application rvtApp = uiApp.Application; 
            ProjectInfo projectInfo = doc.ProjectInformation;
            Autodesk.Revit.DB.Parameter template = projectInfo.LookupParameter("N_Орг.ВерсияШаблона");
            oldProject = false;
            string templateversion = "v";
            if (template == null) { oldProject = true; }
            else { templateversion = template.AsValueString(); }
            templateversion = templateversion.Replace(" (Talan)", "");
            templateversion = templateversion.Replace("(Talan)", "");
            templateversion = templateversion.Replace(" (UDS)", "");
            templateversion = templateversion.Replace("(UDS)", "");
            if (templateversion.Contains("v"))
            {
                oldProject = true;
            }
            else
            {
                string[] versionparts = templateversion.Split('.');
                double versionMath = Convert.ToDouble(versionparts[0]) * 10 + Convert.ToDouble(versionparts[1]);
                if (versionMath < 20223) { oldProject = true; }
            }
            string docName = doc.Title.ToString(); //для разделов инженерных сетей - всегда "старый" шаблон
            if (docName.Contains("-ВК")|| docName.Contains("_ВК")) { oldProject = true; }
            if (docName.Contains("-ОВ") || docName.Contains("_ОВ")) { oldProject = true; }
            if (docName.Contains("-ЭО") || docName.Contains("_ЭО")) { oldProject = true; }
            if (docName.Contains("-ЭЛ") || docName.Contains("_ЭЛ")) { oldProject = true; }
            if (docName.Contains("-ЭЭ") || docName.Contains("_ЭЭ")) { oldProject = true; }
            if (docName.Contains("-ЭС") || docName.Contains("_ЭС")) { oldProject = true; }
            if (docName.Contains("-СС") || docName.Contains("_СС")) { oldProject = true; }
            if (docName.Contains("-ССВ") || docName.Contains("_ССВ")) { oldProject = true; }
            if (docName.Contains("-АПС") || docName.Contains("_АПС")) { oldProject = true; }
            if (docName.Contains("Задани") || docName.Contains("задани") || docName.Contains("-ЗД") || docName.Contains("_ЗД") || docName.Contains("ЗАДАНИЕ")) { oldProject = true; }
        }
    }
}
