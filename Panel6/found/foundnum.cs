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
    public class PileSelectionFilter : ISelectionFilter
    {
        public bool AllowElement(Element element)
        {
            //BuiltInParameter gm = BuiltInParameter.ALL_MODEL_MODEL; //параметр Группа модели
            
            if (element is FamilyInstance)
            {
                FamilyInstance fi = element as FamilyInstance;
                if (fi.Symbol.FamilyName.Contains("Свая"))
                    return true;
                else return false;
            }
            else return false;
        }

        public bool AllowReference(Reference refer, XYZ point)
        {
            return false;
        }
    }
    
    [Transaction(TransactionMode.Manual)]
    public class FoundNum : IExternalCommand
    {
        
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            string TNovClassName = "Сваи Ручной нумератор"; DateTime dateTime = DateTime.Now;
            //подключение приложения и документа
            if (RevitAPI.UiApplication == null) { RevitAPI.Initialize(commandData); }
            UIDocument uidoc = RevitAPI.UiDocument; Document doc = RevitAPI.Document;
            UIApplication uiApp = RevitAPI.UiApplication; Autodesk.Revit.ApplicationServices.Application rvtApp = uiApp.Application;
            
            //проверка подключения, запись в журнал
            bool check = false; servercheck sc = new servercheck(in TNovClassName, out check); if (check == false) { return Result.Failed; }

            // создание log - файла
            Logger.Initialize(TNovClassName);

            //параметры
            Guid pileNumberParamGuid = new Guid("3df328ab-5e4d-4da0-9138-42f1a8bb54a7"); //N_Свая.Номер

            Logger.Log("Диалоговое окно",1);

            var viewModel = new FoundNumViewModel();
            viewModel.parameterName = "N_Свая.Номер";
            var view = new FoundNumWPF(viewModel);
            viewModel.CloseRequest += (s, e) => view.Close();
            viewModel.HideRequest += (s, e) => view.Hide();
            viewModel.ShowRequest += (s, e) => view.ShowDialog();
            view.ShowDialog();



            Logger.Log("Завершение работы.",5);
            return Result.Succeeded;
        }
    }
    
}
