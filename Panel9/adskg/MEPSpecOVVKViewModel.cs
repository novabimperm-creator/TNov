using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace TNov
{
    public class MEPSpecOVVKViewModel : INotifyPropertyChanged
    {
        public int selection { get; set; }

        private ICommand _scenario1;
        public ICommand scenario1
        {
            get
            {
                if (_scenario1 == null)
                {
                    _scenario1 = new RelayCommand(param => { selection = 1; }, CanExecute);
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
                    _scenario2 = new RelayCommand(param => { selection = 2; }, CanExecute);
                }
                return _scenario2;
            }
        }
        [JsonIgnore] public ObservableCollection<string> paramlist { get; set; }
        private string _output1; private string _output2; private string _output3; private string _output4; private string _output5; private string _output6;
        private string _output7; private string _output9; private string _output10; private string _output11; private string _output12;
        private string _output13; private string _output14;
        private bool _run1 = true; private bool _run2 = true; private bool _run3 = true; private bool _run4 = true; private bool _run5 = true; private bool _run6 = true;
        private bool _run7 = true; private bool _run8 = true; private bool _run9 = true; private bool _run10 = true; private bool _run11 = true; private bool _run12 = true;
        private bool _run13 = true; private bool _run14 = true;
        private bool _runNonModel = true; private bool _runadskg = true; private bool _runNCat = true; private bool _runCatalogs = true; private bool _runadskp = true;

        public string output1 { get { return _output1; } set { _output1 = value; OnPropertyChanged(); } }
        public string output2 { get { return _output2; } set { _output2 = value; OnPropertyChanged(); } }
        public string output3 { get { return _output3; } set { _output3 = value; OnPropertyChanged(); } }
        public string output4 { get { return _output4; } set { _output4 = value; OnPropertyChanged(); } }
        public string output5 { get { return _output5; } set { _output5 = value; OnPropertyChanged(); } }
        public string output6 { get { return _output6; } set { _output6 = value; OnPropertyChanged(); } }
        public string output7 { get { return _output7; } set { _output7 = value; OnPropertyChanged(); } }

        public string output9 { get { return _output9; } set { _output9 = value; OnPropertyChanged(); } }
        public string output10 { get { return _output10; } set { _output10 = value; OnPropertyChanged(); } }
        public string output11 { get { return _output11; } set { _output11 = value; OnPropertyChanged(); } }
        public string output12 { get { return _output12; } set { _output12 = value; OnPropertyChanged(); } }
        public string output13 { get { return _output13; } set { _output13 = value; OnPropertyChanged(); } }
        public string output14 { get { return _output14; } set { _output14 = value; OnPropertyChanged(); } }

        public bool run1 { get { return _run1; } set { _run1 = value; OnPropertyChanged(); } }
        public bool run2 { get { return _run2; } set { _run2 = value; OnPropertyChanged(); } }
        public bool run3 { get { return _run3; } set { _run3 = value; OnPropertyChanged(); } }
        public bool run4 { get { return _run4; } set { _run4 = value; OnPropertyChanged(); } }
        public bool run5 { get { return _run5; } set { _run5 = value; OnPropertyChanged(); } }
        public bool run6 { get { return _run6; } set { _run6 = value; OnPropertyChanged(); } }
        public bool run7 { get { return _run7; } set { _run7 = value; OnPropertyChanged(); } }
        public bool run8 { get { return _run8; } set { _run8 = value; OnPropertyChanged(); } }
        public bool run9 { get { return _run9; } set { _run9 = value; OnPropertyChanged(); } }
        public bool run10 { get { return _run10; } set { _run10 = value; OnPropertyChanged(); } }
        public bool run11 { get { return _run11; } set { _run11 = value; OnPropertyChanged(); } }
        public bool run12 { get { return _run12; } set { _run12 = value; OnPropertyChanged(); } }
        public bool run13 { get { return _run13; } set { _run13 = value; OnPropertyChanged(); } }
        public bool run14 { get { return _run14; } set { _run14 = value; OnPropertyChanged(); } }
        [JsonIgnore] public bool visibility1; [JsonIgnore] public bool visibility2;
        [JsonIgnore] public bool visibility3; [JsonIgnore] public bool visibility4;
        [JsonIgnore] public bool visibility5; [JsonIgnore] public bool visibility6;
        [JsonIgnore] public bool visibility7; [JsonIgnore] public bool visibility8;
        [JsonIgnore] public bool visibility9; [JsonIgnore] public bool visibility10;
        [JsonIgnore] public bool visibility11; [JsonIgnore] public bool visibility12;
        [JsonIgnore] public bool visibility13; [JsonIgnore] public bool visibility14;
        [JsonIgnore] public string fileName;
        public bool runNonModel { get { return _runNonModel; } set { _runNonModel = value; OnPropertyChanged(); } }
        public bool runadskg { get { return _runadskg; } set { _runadskg = value; OnPropertyChanged(); } }
        public bool runadskp { get { return _runadskp; } set { _runadskp = value; OnPropertyChanged(); } }
        public bool runNCat { get { return _runNCat; } set { _runNCat = value; OnPropertyChanged(); } }
        public bool runCatalogs { get { return _runCatalogs; } set { _runCatalogs = value; OnPropertyChanged(); } }
        private bool _systemcut = true; public bool systemcut { get { return _systemcut; } set { _systemcut = value; OnPropertyChanged(); } }
        private string _types = "";
        [JsonIgnore] public string types { get => _types; set { _types = value; OnPropertyChanged(); } }
        public MEPSpecOVVKViewModel()
        {
            Param();
        }
        private void Param()
        {
            paramlist = new ObservableCollection<string>
            {
                "Имя системы",
                "Сокращение для системы",
                "Тип системы"
            };
            output1 = paramlist[0]; output2 = paramlist[0]; output3 = paramlist[0]; output4 = paramlist[0];
            output5 = paramlist[0]; output6 = paramlist[0]; output7 = paramlist[0];
            output9 = paramlist[1]; output10 = paramlist[1]; output11 = paramlist[1]; output12 = paramlist[1];
            output13 = paramlist[1]; output14 = paramlist[1];
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
}
