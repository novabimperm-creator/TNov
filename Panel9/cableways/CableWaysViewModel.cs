using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace TNov
{
    public class CableWaysViewModel : INotifyPropertyChanged
    {
        private string _sPar1 = "К1_Длина_Способ 1"; public string sPar1 { get => _sPar1; set { _sPar1 = value; OnPropertyChanged(); } }
        private string _sPar2 = "К1_Длина_Способ 2"; public string sPar2 { get => _sPar2; set { _sPar2 = value; OnPropertyChanged(); } }
        private string _sPar3 = "К1_Длина_Способ 3"; public string sPar3 { get => _sPar3; set { _sPar3 = value; OnPropertyChanged(); } }
        private string _sPar4 = "К1_Длина_Способ 4"; public string sPar4 { get => _sPar4; set { _sPar4 = value; OnPropertyChanged(); } }
        private string _sPar5 = "К1_Длина_Способ 5"; public string sPar5 { get => _sPar5; set { _sPar5 = value; OnPropertyChanged(); } }
        private string _sTypePar1 = "Настройки_Кабели_Способ прокладки 1"; public string sTypePar1 { get => _sTypePar1; set { _sTypePar1 = value; OnPropertyChanged(); } }
        private string _sTypePar2 = "Настройки_Кабели_Способ прокладки 2"; public string sTypePar2 { get => _sTypePar2; set { _sTypePar2 = value; OnPropertyChanged(); } }
        private string _sTypePar3 = "Настройки_Кабели_Способ прокладки 3"; public string sTypePar3 { get => _sTypePar3; set { _sTypePar3 = value; OnPropertyChanged(); } }
        private string _sTypePar4 = "Настройки_Кабели_Способ прокладки 4"; public string sTypePar4 { get => _sTypePar4; set { _sTypePar4 = value; OnPropertyChanged(); } }
        private string _sTypePar5 = "Настройки_Кабели_Способ прокладки 5"; public string sTypePar5 { get => _sTypePar5; set { _sTypePar5 = value; OnPropertyChanged(); } }

        private string _pipeType1 = "IEK | Труба гофрированная из ПВХ"; public string pipeType1 { get => _pipeType1; set { _pipeType1 = value; OnPropertyChanged(); } }
        private string _pipeType2 = "IEK | Труба гофрированная из ПНД (тяжелая)"; public string pipeType2 { get => _pipeType2; set { _pipeType2 = value; OnPropertyChanged(); } }
        private string _pipeType3 = "DKC | Металлорукав в герметичной ПВХ-оболочке"; public string pipeType3 { get => _pipeType3; set { _pipeType3 = value; OnPropertyChanged(); } }
        private string _pipeType4 = "DKC | Труба индустриальная гофрированная из не распространяющего горение полиамида (серия F0)"; public string pipeType4 { get => _pipeType4; set { _pipeType4 = value; OnPropertyChanged(); } }
        private string _pipeType5 = "Труба стальная электросварная (толщина стенки 1.5 мм)"; public string pipeType5 { get => _pipeType5; set { _pipeType5 = value; OnPropertyChanged(); } }
        private string _pipeWay1 = "гофр. ПВХ"; public string pipeWay1 { get => _pipeWay1; set { _pipeWay1 = value; OnPropertyChanged(); } }
        private string _pipeWay2 = "гофр. ПНД(т)"; public string pipeWay2 { get => _pipeWay2; set { _pipeWay2 = value; OnPropertyChanged(); } }
        private string _pipeWay3 = "МР(г)"; public string pipeWay3 { get => _pipeWay3; set { _pipeWay3 = value; OnPropertyChanged(); } }
        private string _pipeWay4 = "гофр. ПА"; public string pipeWay4 { get => _pipeWay4; set { _pipeWay4 = value; OnPropertyChanged(); } }
        private string _pipeWay5 = "ст."; public string pipeWay5 { get => _pipeWay5; set { _pipeWay5 = value; OnPropertyChanged(); } }

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
