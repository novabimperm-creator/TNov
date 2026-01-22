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
    public class FoundAutoNumViewModel : INotifyPropertyChanged
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
        private string _parameterName = "N_Свая.Номер";
        public string parameterName { get => _parameterName; set { _parameterName = value; OnPropertyChanged(); } }
        private string _startvalue = "1"; public string startvalue { get => _startvalue; set { _startvalue = value; OnPropertyChanged(); } }
        private bool _divide = true; public bool divide { get => _divide; set { _divide = value; OnPropertyChanged(); } }
        private string _rule = ""; public string rule { get => _rule; set { _rule = value; OnPropertyChanged(); } }
        [JsonIgnore] public ObservableCollection<string> rules { get; set; }
        //private double _tolerance = 500; public double tolerance { get => _tolerance; set { _tolerance = value; OnPropertyChanged(); } }
        private int _rulenum = 0;
        public int rulenum { get => _rulenum; set { _rulenum = value; OnPropertyChanged(); } }
        public FoundAutoNumViewModel()
        {
            Param();
        }
        private void Param()
        {
            rules = new ObservableCollection<string>
            {
                "Слева направо, снизу вверх",
                "Слева направо, сверху вниз",
                "Справа налево, снизу вверх",
                "Справа налево, сверху вниз",
                "По ID элементов"
            };
            rule = rules[rulenum];
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
        public event EventHandler HideRequest;
        private void RaiseHideRequest()
        {
            HideRequest?.Invoke(this, EventArgs.Empty);
        }
        public event EventHandler ShowRequest;
        private void RaiseShowRequest()
        {
            ShowRequest?.Invoke(this, EventArgs.Empty);
        }

        public event PropertyChangedEventHandler PropertyChanged;

        void OnPropertyChanged([CallerMemberName] string PropertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(PropertyName));
        }
    }
}
