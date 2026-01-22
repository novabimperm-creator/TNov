using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.Attributes;
using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Autodesk.Revit.UI.Selection;
using TNov.main;

namespace TNov
{

    
    [Transaction(TransactionMode.Manual)]
    public class ApartsNumAtLevel : IExternalCommand
    {
        
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            string TNovClassName = "Нумератор квартир"; DateTime dateTime = DateTime.Now;
            //подключение приложения и документа
            if (RevitAPI.UiApplication == null) { RevitAPI.Initialize(commandData); }
            UIDocument uidoc = RevitAPI.UiDocument; Document doc = RevitAPI.Document;
            UIApplication uiApp = RevitAPI.UiApplication; Autodesk.Revit.ApplicationServices.Application rvtApp = uiApp.Application;
            
            //проверка подключения, запись в журнал
            bool check = false; servercheck sc = new servercheck(in TNovClassName, out check); if (check == false) { return Result.Failed; }

            // создание log - файла
            Logger.Initialize(TNovClassName);

            
            Logger.Log( "Диалоговое окно",1);

            var viewModel = new ApartsNumAtLevelViewModel();
            var view = new ApartsNumAtLevelWPF(viewModel);
            viewModel.CloseRequest += (s, e) => view.Close();
            viewModel.HideRequest += (s, e) => view.Hide();
            viewModel.ShowRequest += (s, e) => view.ShowDialog();
            view.ShowDialog();

            bool runrecalc = viewModel.recalcnums;
            if (runrecalc) { Logger.Log("Завершение работы.", 5); ApartsNum Command1 = new ApartsNum(); Command1.Execute(commandData, ref message, elements); }

            Logger.Log( "Завершение работы.",5);
            return Result.Succeeded;
        }
    }
    
}
