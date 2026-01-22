using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace TNov
{
    public class ParkMarkViewModel : INotifyPropertyChanged
    {
        private string _startvalue = "1";
        public string startvalue { get => _startvalue; set { _startvalue = value; OnPropertyChanged(); } }
        private string _prefix = "ММ-";
        public string prefix { get => _prefix; set { _prefix = value; OnPropertyChanged(); } }
        [JsonIgnore] public ObservableCollection<string> scenariolist { get; set; }
        private string _scenario;
        public string scenario { get { return _scenario; } set { _scenario = value; OnPropertyChanged(); } }
        private bool _all = true;
        public bool all { get => _all; set { _all = value; OnPropertyChanged(); } }
        private int _scenarionum = 0;
        public int scenarionum { get => _scenarionum; set { _scenarionum = value; OnPropertyChanged(); } }
        public ParkMarkViewModel()
        {
            Scenario();
        }
        private void Scenario()
        {
            scenariolist = new ObservableCollection<string>
            {
                "Марку в Позицию",
                "Позицию в Марку"
            };
            scenario = scenariolist[scenarionum];
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
