using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace TNov
{
    public class CableTraysViewModel : INotifyPropertyChanged
    {
        private bool _replace = true;
        public bool replace { get => _replace; set { _replace = value; OnPropertyChanged(); } }

        private bool _remove = true;
        public bool remove { get => _remove; set { _remove = value; OnPropertyChanged(); } }

        private string _types = "IEK_ПКЛ,IEK_ОКЛ,EKF_ПКЛ,EKF_ОКЛ,СС_ПКЛ,СС_ОКЛ";
        public string types { get => _types; set { _types = value; OnPropertyChanged(); } }

        private string _filter1 = "с крышкой";
        public string filter1 { get => _filter1; set { _filter1 = value; OnPropertyChanged(); } }

        private string _filter2 = "перегородкой";
        public string filter2 { get => _filter2; set { _filter2 = value; OnPropertyChanged(); } }

        private string _capname = "Крышка";
        public string capname { get => _capname; set { _capname = value; OnPropertyChanged(); } }

        private string _ptname = "Перегородка лотка";
        public string ptname { get => _ptname; set { _ptname = value; OnPropertyChanged(); } }

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
