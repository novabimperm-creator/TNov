using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace TNov
{
    public class Hole
    {
        public bool pasted;
        public string mark;
        public string mark1;
        public string status;
        public double length;
        public double width;
        public double height;
        public string coordStatusHead;
        public string coordStatusBIM;
        public string coordStatusST;
        public double x;
        public double y;
        public double z;
        public int id1;
        public int holeorder;
    }
    public class HoleGroup : INotifyPropertyChanged
    {
        //Имя
        private string _HoleGroupName;
        public string HoleGroupName { get => _HoleGroupName; set { _HoleGroupName = value; OnPropertyChanged(); } }
        //От кого
        private string _HoleGroupNamePart1;
        public string HoleGroupNamePart1 { get => _HoleGroupNamePart1; set { _HoleGroupNamePart1 = value; OnPropertyChanged(); } }
        //Кому
        private string _HoleGroupNamePart2;
        public string HoleGroupNamePart2 { get => _HoleGroupNamePart2; set { _HoleGroupNamePart2 = value; OnPropertyChanged(); } }
        //Этаж
        private string _HoleGroupNamePart3;
        public string HoleGroupNamePart3 { get => _HoleGroupNamePart3; set { _HoleGroupNamePart3 = value; OnPropertyChanged(); } }
        //Статус
        private string _HoleGroupStatus;
        public string HoleGroupStatus { get => _HoleGroupStatus; set { _HoleGroupStatus = value; OnPropertyChanged(); } }
        //Группирование
        private string _HoleGroupSet;
        public string HoleGroupSet { get => _HoleGroupSet; set { _HoleGroupSet = value; OnPropertyChanged(); } }
        //Порядок
        private int _Order;
        public int Order { get => _Order; set { _Order = value; OnPropertyChanged(); } }

        private bool _isButtonVisible;
        public bool IsButtonVisible
        {
            get => _isButtonVisible;
            set
            {
                _isButtonVisible = value;
                OnPropertyChanged(nameof(IsButtonVisible));
            }
        }

        private string _buttonText;
        public string ButtonText
        {
            get => _buttonText;
            set
            {
                _buttonText = value;
                OnPropertyChanged(nameof(ButtonText));
            }
        }

        private string _buttonToolTip;
        public string ButtonToolTip
        {
            get => _buttonToolTip;
            set
            {
                _buttonToolTip = value;
                OnPropertyChanged(nameof(ButtonToolTip));
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
