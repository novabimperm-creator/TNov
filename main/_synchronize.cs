using Autodesk.Revit.UI;
using Autodesk.Revit.DB;
using System;
using TNov.main;

namespace TNov
{
    public class synchronize
    {
        
        public synchronize(string docName, string userName)
        {
            //if (RevitAPI.UiApplication == null) { RevitAPI.Initialize(commandData); }
            UIDocument uidoc = RevitAPI.UiDocument;
            UIApplication uiApp = RevitAPI.UiApplication;
            Autodesk.Revit.ApplicationServices.Application rvtApp = uiApp.Application; 
            string usagefilePath = nova.novaserver + "_TNov/synchronizes.txt";
            string docNameUserName = "_" + userName; docName = docName.Replace(docNameUserName, ""); 
            docName = docName.Replace(",", "");
            DateTime dateTime = DateTime.Now;
            string date = dateTime.ToString(); date = date.Replace(",", "");
            try
            {
                System.IO.File.AppendAllText(usagefilePath, "\n" + date + "," + userName + "," + docName);
            }
            catch (Exception)
            {
                
            }
        }
    }
}
