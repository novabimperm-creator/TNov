using Autodesk.Revit.UI;
using Autodesk.Revit.DB;
using System;
using TNov.main;

namespace TNov
{
    public class servercheck
    {
        
        public servercheck(in string TNovclassname, out bool check)
        {
            UIDocument uidoc = RevitAPI.UiDocument;
            Document doc = RevitAPI.Document;
            UIApplication uiApp = RevitAPI.UiApplication;
            Autodesk.Revit.ApplicationServices.Application rvtApp = uiApp.Application; 
            string usagefilePath = nova.novaserver + "_TNov/usage.txt";
            string docName = doc.Title.ToString(); docName = docName.Replace(",", " "); // --версия 1.0.2--
            string userName = rvtApp.Username; userName = userName.Replace(",", "");
            string docNameUserName = "_" + userName; docName = docName.Replace(docNameUserName, ""); // --версия 1.0.2--
            docName = docName.Replace(",", "");
            DateTime dateTime = DateTime.Now;
            string date = dateTime.ToString(); date = date.Replace(",", "");
            string TNovVersion = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString();
            try
            {
                System.IO.File.AppendAllText(usagefilePath, "\n" + date + "," + userName + "," + docName + "," + TNovclassname + "," + TNovVersion);
                check = true;
            }
            catch (Exception ex)
            {
                string info1txt = "Отсутствует подключение к корпоративной сети ПМ Новация. Проверьте подключение к адресу fs-nova.";
                var info1 = new infowindow280(info1txt); info1.ShowDialog();
                check = false;
                string msg = ex.Message;
            }
        }
    }
}
