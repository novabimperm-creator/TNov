using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.Attributes;
using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace TNov
{
    public class cabletraycapsViewModel : INotifyPropertyChanged
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
    public class cabletraycaps : IExternalCommand
    {
        
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            //Выбор сценария
            var viewModel = new cabletraycapsViewModel();
            var wpfview = new cabletraycapswpf(viewModel);
            viewModel.CloseRequest += (s, e) => wpfview.Close();
            bool? ok = wpfview.ShowDialog();
            if (ok != null && ok == true) { } else { return Result.Cancelled; }

            int scenario = viewModel.scenario;
            switch (scenario)
            {
                case 1:
                    cabletraycapcreate Command1 = new cabletraycapcreate(); Command1.Execute(commandData, ref message, elements);
                    break;
                case 2:
                    cabletraycapdelete Command2 = new cabletraycapdelete(); Command2.Execute(commandData, ref message, elements);
                    break;
                
            }
            
            return Result.Succeeded;
        }

    }
    
}
