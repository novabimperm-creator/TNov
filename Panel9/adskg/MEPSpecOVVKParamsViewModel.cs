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
    public class MEPSpecOVVKParamsViewModel : INotifyPropertyChanged
    {
        [JsonIgnore] public ObservableCollection<string> paramlist { get; set; }
        [JsonIgnore] public ObservableCollection<string> paramlist2 { get; set; }
        [JsonIgnore] public string elemType { get; set; }
        private string _naimPar1 = "Комментарии к типоразмеру"; public string naimPar1 { get => _naimPar1; set { _naimPar1 = value; OnPropertyChanged(); } }
        private string _naimPrefix2 = ""; public string naimPrefix2 { get => _naimPrefix2; set { _naimPrefix2 = value; OnPropertyChanged(); } }
        private string _naimPar2 = ""; public string naimPar2 { get => _naimPar2; set { _naimPar2 = value; OnPropertyChanged(); } }
        private string _naimPrefix3 = ""; public string naimPrefix3 { get => _naimPrefix3; set { _naimPrefix3 = value; OnPropertyChanged(); } }
        private string _naimPar3 = ""; public string naimPar3 { get => _naimPar3; set { _naimPar3 = value; OnPropertyChanged(); } }
        private string _naimPrefix4 = ""; public string naimPrefix4 { get => _naimPrefix4; set { _naimPrefix4 = value; OnPropertyChanged(); } }
        private string _naimPar4 = ""; public string naimPar4 { get => _naimPar4; set { _naimPar4 = value; OnPropertyChanged(); } }
        private string _countPar = "Число"; public string countPar { get => _countPar; set { _countPar = value; OnPropertyChanged(); } }
        private string _countK = "1"; public string countK { get => _countK; set { _countK = value; OnPropertyChanged(); } }

        public MEPSpecOVVKParamsViewModel()
        {
            Param();
        }
        private void Param()
        {
            paramlist = new ObservableCollection<string>
            {
                "выкл",
                "Диаметр",
                "Внешний диаметр",
                "ADSK_Толщина стенки",
                "Толщина изоляции",
                "Размер трубы",
                "Размер",
                "Класс герметичности",
                "ADSK_Размер_УголПоворота"
            };
            _naimPar2 = paramlist[0]; _naimPar3 = paramlist[0]; _naimPar4 = paramlist[0];
            paramlist2 = new ObservableCollection<string>
            {
                "Число",
                "Длина",
                "Площадь",
                "Объем"
            };
            _countPar = paramlist2[0];
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
