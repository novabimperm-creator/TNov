using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.Attributes;
using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Autodesk.Revit.UI.Selection;
using System.Collections.ObjectModel;
using TNovCommon;

namespace TNov
{
    
    [Transaction(TransactionMode.Manual)]
    public class ParkNum : IExternalCommand
    {
        
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            string TNovClassName = "Парковки Ручной нумератор"; DateTime dateTime = DateTime.Now;
            //подключение приложения и документа
            if (RevitAPI.UiApplication == null) { RevitAPI.Initialize(commandData); }
            UIDocument uidoc = RevitAPI.UiDocument; Document doc = RevitAPI.Document;
            UIApplication uiApp = RevitAPI.UiApplication; Autodesk.Revit.ApplicationServices.Application rvtApp = uiApp.Application;
            
            //проверка подключения, запись в журнал
            if(ServerUtils.CheckConnection(TNovClassName)==false) return Result.Failed;

            // создание log - файла
            Logger.Initialize(TNovClassName);
            

            Logger.Log("Диалоговое окно",1);

            var viewModel = new ParkNumViewModel();
            var view = new ParkNumWPF(viewModel);
            viewModel.CloseRequest += (s, e) => view.Close();
            viewModel.HideRequest += (s, e) => view.Hide();
            viewModel.ShowRequest += (s, e) => view.ShowDialog();
            view.ShowDialog();



            Logger.Log("Завершение работы.",5);
            return Result.Succeeded;
        }
    }
    
}
