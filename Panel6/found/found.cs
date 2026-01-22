using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.Attributes;
using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace TNov
{
    
    [Transaction(TransactionMode.Manual)]
    public class Found : IExternalCommand
    {
        
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            //Выбор сценария
            var viewModel = new FoundViewModel();
            var wpfview = new FoundWPF(viewModel);
            viewModel.CloseRequest += (s, e) => wpfview.Close();
            bool? ok = wpfview.ShowDialog();
            if (ok != null && ok == true) { } else { return Result.Cancelled; }

            int scenario = viewModel.scenario;
            switch (scenario)
            {
                case 1:
                    FoundNum Command1 = new FoundNum(); Command1.Execute(commandData, ref message, elements);
                    break;
                case 2:
                    FoundAutoNum Command2 = new FoundAutoNum(); Command2.Execute(commandData, ref message, elements);
                    break;
                case 3:
                    FoundNumPurge Command3 = new FoundNumPurge(); Command3.Execute(commandData, ref message, elements);
                    break;
                case 4:
                    FoundNumSpec Command4 = new FoundNumSpec(); Command4.Execute(commandData, ref message, elements);
                    break;
                case 5:
                    FoundCut Command5 = new FoundCut(); Command5.Execute(commandData, ref message, elements);
                    break;
                case 6:
                    FoundOtm Command6 = new FoundOtm(); Command6.Execute(commandData, ref message, elements);
                    break;
            }
            
            return Result.Succeeded;
        }
    }
    
}
