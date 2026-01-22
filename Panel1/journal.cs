using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.Attributes;
using File = System.IO.File;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System;
using TNov.main;
using System.Collections.Generic;

namespace TNov
{
    [Transaction(TransactionMode.Manual)]
    public class Journal : IExternalCommand
    {
        
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            string TNovClassName = "Журнал синхронизаций"; DateTime dateTime = DateTime.Now;
            //подключение приложения и документа
            if (RevitAPI.UiApplication == null) { RevitAPI.Initialize(commandData); }
            UIDocument uidoc = RevitAPI.UiDocument; Document doc = RevitAPI.Document;
            UIApplication uiApp = RevitAPI.UiApplication; Autodesk.Revit.ApplicationServices.Application rvtApp = uiApp.Application;
            //проверка подключения, запись в журнал
            bool check = false; servercheck sc = new servercheck(in TNovClassName, out check); if (check == false) { return Result.Failed; }

            string usagefilePath = nova.novaserver + "_TNov/synchronizes.txt";

            string[] lines = File.ReadAllLines(usagefilePath);

            string docName = doc.Title.ToString(); docName = docName.Replace(",", " "); 
            string userName = rvtApp.Username; userName = userName.Replace(",", "");
            string docNameUserName = "_" + userName; docName = docName.Replace(docNameUserName, ""); 
            docName = docName.Replace(",", "");

            List<string> docLines = new List<string>();

            foreach (var line in lines)
            {
                string[] parts = line.Split(',');
                if (parts.Length > 2 && parts[2].Equals(docName)) docLines.Add(parts[0]+"   "+ parts[1]);
            }

            if (docLines.Count > 0) 
            {
                int i = 0;
                string mes = "Журнал синхронизаций модели "+docName + ":";
                docLines.Reverse();
                foreach (var line in docLines) 
                {
                    i++;
                    if (i > 1000) break;
                    mes += "\n" + line; 
                }
                new infowindow400(mes).ShowDialog();
            }
            else new infowindow280("В журнале пока отсутствуют записи о синхронизациях модели "+ docName + ". Статистика ведется с 03.04.2025, скоро данные появятся!").ShowDialog();

            return Result.Succeeded;
        }
    }
}
