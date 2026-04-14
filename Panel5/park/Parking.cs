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
    public class Parking : IExternalCommand
    {
        
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            if (RevitAPI.UiApplication == null) { RevitAPI.Initialize(commandData); } UIApplication uiApp = RevitAPI.UiApplication;
            //Выбор сценария
            var viewModel = new ParkViewModel();
            var wpfview = new ParkWPF(viewModel);
            viewModel.CloseRequest += (s, e) => wpfview.Close();
            bool? ok = wpfview.ShowDialog();
            if (ok != null && ok == true) { } else { return Result.Cancelled; }

            int scenario = viewModel.scenario;
            switch (scenario)
            {
                case 1:
                    ParkNum Command1 = new ParkNum(); Command1.Execute(commandData, ref message, elements);
                    break;
                case 2:
                    ParkMark Command2 = new ParkMark(); Command2.Execute(commandData, ref message, elements);
                    break;
                case 3:
                    var info1 = new InfoWindow280("Сейчас откроется Проигрыватель Dynamo.\nВ нем найдите и запустите скрипт Паркоместа.Площади."); info1.ShowDialog();
                    RevitCommandId id_built_in = RevitCommandId.LookupPostableCommandId(PostableCommand.DynamoPlayer);
                    uiApp.PostCommand(id_built_in);
                    break;
            }
            
            return Result.Succeeded;
        }
    }
    
}
