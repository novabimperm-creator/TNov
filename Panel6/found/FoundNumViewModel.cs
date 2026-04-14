using Autodesk.Revit.DB;
using Autodesk.Revit.UI.Selection;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace TNov
{
    public class FoundNumViewModel : INotifyPropertyChanged
    {
        private string _parameterName = "N_Свая.Номер";
        public string parameterName { get => _parameterName; set { _parameterName = value; OnPropertyChanged(); } }
        private string _startvalue = "1";
        public string startvalue { get => _startvalue; set { _startvalue = value; OnPropertyChanged(); } }

        public RelayCommand NumerateCommand { get; set; }

        public FoundNumViewModel()
        {
            NumerateCommand = new RelayCommand(param => { Numerate(); }, CanNumerate);
        }
        public void Numerate()
        {
            RaiseHideRequest();
            int i = 1;
            int.TryParse(startvalue, out i);


            using (TransactionGroup group = new TransactionGroup(RevitAPI.Document, "TNov - Ручной нумератор свай"))
            {
                ISelectionFilter _filter = new PileSelectionFilter();
                group.Start();

                while (true)
                {
                    try
                    {
                        using (Transaction t = new Transaction(RevitAPI.Document, "TNov - Ручной нумератор свай"))
                        {
                            t.Start();
                            Reference reference = RevitAPI.UiDocument.Selection.PickObject(ObjectType.Element, _filter, $"Выберите элемент {i}");
                            Autodesk.Revit.DB.Parameter parameter = RevitAPI.Document.GetElement(reference).LookupParameter(parameterName);
                            if (parameter != null)
                            {
                                parameter.Set(i.ToString());
                                i++;
                                t.Commit();
                            }
                            else
                            {
                                var info1 = new InfoWindow280($"Ошибка!\nУ элемента {reference.ElementId} нет параметра {parameterName}."); info1.ShowDialog();
                                t.Commit();
                                group.Assimilate();
                                break;
                            }
                        }
                    }
                    catch
                    {
                        group.Assimilate();
                        break;
                    }
                }
            }
            startvalue = i.ToString();
            RaiseShowRequest();
        }

        private bool CanNumerate(object param)
        {
            return int.TryParse(startvalue, out _);
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
