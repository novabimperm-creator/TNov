using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace TNov
{
    public class adskssViewModel : INotifyPropertyChanged
    {
        private string _conduitCoeff = "1.3"; public string ConduitCoeff { get => _conduitCoeff; set { _conduitCoeff = value; OnPropertyChanged(); } }
        private string _conduitCoeffPipe = "1.3"; public string ConduitCoeffPipe { get => _conduitCoeffPipe; set { _conduitCoeffPipe = value; OnPropertyChanged(); } }
        private string _conduitStep = "500"; public string ConduitStep { get => _conduitStep; set { _conduitStep = value; OnPropertyChanged(); } }

        private string _elSystemCoeffCable = "1.5"; public string ElSystemCoeffCable { get => _elSystemCoeffCable; set { _elSystemCoeffCable = value; OnPropertyChanged(); } }
        private string _elSystemCoeffPipe = "1.3"; public string ElSystemCoeffPipe { get => _elSystemCoeffPipe; set { _elSystemCoeffPipe = value; OnPropertyChanged(); } }
        private string _elSystemStep = "500"; public string ElSystemStep { get => _elSystemStep; set { _elSystemStep = value; OnPropertyChanged(); } }

        private string _cableTrayCoeffCable = "1.3"; public string CableTrayCoeffCable { get => _cableTrayCoeffCable; set { _cableTrayCoeffCable = value; OnPropertyChanged(); } }

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
