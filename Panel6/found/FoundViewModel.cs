using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace TNov
{
    public class FoundViewModel : INotifyPropertyChanged
    {
        public int scenario { get; set; }

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
        private ICommand _scenario4;
        public ICommand scenario4
        {
            get
            {
                if (_scenario4 == null)
                {
                    _scenario4 = new RelayCommand(param => { scenario = 4; }, CanExecute);
                }
                return _scenario4;
            }
        }
        private ICommand _scenario5;
        public ICommand scenario5
        {
            get
            {
                if (_scenario5 == null)
                {
                    _scenario5 = new RelayCommand(param => { scenario = 5; }, CanExecute);
                }
                return _scenario5;
            }
        }
        private ICommand _scenario6;
        public ICommand scenario6
        {
            get
            {
                if (_scenario6 == null)
                {
                    _scenario6 = new RelayCommand(param => { scenario = 6; }, CanExecute);
                }
                return _scenario6;
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
}
