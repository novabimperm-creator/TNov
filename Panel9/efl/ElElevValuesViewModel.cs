using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace TNov
{
    public class ElElevValuesViewModel : INotifyPropertyChanged
    {

        private string _vs = "90";
        public string vs { get => _vs; set { _vs = value; } }

        private string _op = "2730";
        public string op { get => _op; set { _op = value; } }

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
