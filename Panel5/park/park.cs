using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.Attributes;
using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace TNov
{
    public class Park
    {
        public ElementId elemid;
        public int mark;
        public double elevation;
    }
    public class parkViewModel : INotifyPropertyChanged
    {
        public int scenario { get; set ; }

        private ICommand _scenario1;
        public ICommand scenario1
        {
            get
            {
                if (_scenario1 == null)
                {
                    _scenario1 = new RelayCommand(param => { scenario = 1; }, CanExecute);
                }
                return _scenario1;
            }
        }
        private ICommand _scenario2;
        public ICommand scenario2
        {
            get
            {
                if (_scenario2 == null)
                {
                    _scenario2 = new RelayCommand(param => { scenario = 2; }, CanExecute);
                }
                return _scenario2;
            }
        }
        private ICommand _scenario3;
        public ICommand scenario3
        {
            get
            {
                if (_scenario3 == null)
                {
                    _scenario3 = new RelayCommand(param => { scenario = 3; }, CanExecute);
                }
                return _scenario3;
            }
        }
        
        
        private bool CanExecute(object param)
        {
            return true;
        }

        public event EventHandler CloseRequest;
        private void RaiseCloseRequest()
        {
            CloseRequest?.Invoke(this, EventArgs.Empty);
        }
        public event PropertyChangedEventHandler PropertyChanged;

        void OnPropertyChanged([CallerMemberName] string PropertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(PropertyName));
        }
    }
    [Transaction(TransactionMode.Manual)]
    public class park : IExternalCommand
    {
        
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            if (RevitAPI.UiApplication == null) { RevitAPI.Initialize(commandData); } UIApplication uiApp = RevitAPI.UiApplication;
            //Выбор сценария
            var viewModel = new parkViewModel();
            var wpfview = new parkwpf(viewModel);
            viewModel.CloseRequest += (s, e) => wpfview.Close();
            bool? ok = wpfview.ShowDialog();
            if (ok != null && ok == true) { } else { return Result.Cancelled; }

            int scenario = viewModel.scenario;
            switch (scenario)
            {
                case 1:
                    parknum Command1 = new parknum(); Command1.Execute(commandData, ref message, elements);
                    break;
                case 2:
                    parkmark Command2 = new parkmark(); Command2.Execute(commandData, ref message, elements);
                    break;
                case 3:
                    var info1 = new infowindow280("Сейчас откроется Проигрыватель Dynamo.\nВ нем найдите и запустите скрипт Паркоместа.Площади."); info1.ShowDialog();
                    RevitCommandId id_built_in = RevitCommandId.LookupPostableCommandId(PostableCommand.DynamoPlayer);
                    uiApp.PostCommand(id_built_in);
                    break;
            }
            
            return Result.Succeeded;
        }
    }
    
}
