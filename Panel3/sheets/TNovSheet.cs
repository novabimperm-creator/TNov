using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace TNov
{
    public class TNovSheet : INotifyPropertyChanged
    {
        //"Чистый" номер листа
        private string _TNovSheetCleanNumber;
        public string TNovSheetCleanNumber { get => _TNovSheetCleanNumber; set { _TNovSheetCleanNumber = value; OnPropertyChanged(); } }
        //Номер листа
        private string _TNovSheetNumber;
        public string TNovSheetNumber { get => _TNovSheetNumber; set { _TNovSheetNumber = value; OnPropertyChanged(); } }
        //ШНомер
        private string _TNovSheetNumberCustom;
        public string TNovSheetNumberCustom { get => _TNovSheetNumberCustom; set { _TNovSheetNumberCustom = value; OnPropertyChanged(); } }
        //Имя листа
        private string _TNovSheetName;
        public string TNovSheetName { get => _TNovSheetName; set { _TNovSheetName = value; OnPropertyChanged(); } }
        //Комплект чертежей
        private string _TNovSheetSet;
        public string TNovSheetSet { get => _TNovSheetSet; set { _TNovSheetSet = value; OnPropertyChanged(); } }
        //Номер листа после перенумерации
        private string _TNovSheetNewNumber;
        public string TNovSheetNewNumber
        {
            get => _TNovSheetNewNumber;
            set
            {
                if (_TNovSheetNewNumber != value) _TNovSheetNewNumber = value; OnPropertyChanged();
            }
        }
        //Проверочный параметр - можно ли распарсить новый номер в int (заполняется в основном коде)
        public bool TNovSheetCanRenum { get; set; }
        //Целочисленный номер
        private int _TNovSheetNumericNumber;
        public int TNovSheetNumericNumber
        {
            get => _TNovSheetNumericNumber;
            set
            {
                _TNovSheetNumericNumber = value; OnPropertyChanged();
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
