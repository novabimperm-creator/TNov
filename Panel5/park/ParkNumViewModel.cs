using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
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
    public class ParkSelectionFilter : ISelectionFilter
    {
        public bool AllowElement(Element element)
        {
            if (element.Category.Name == "Парковка") return true; else return false;
        }

        public bool AllowReference(Reference refer, XYZ point)
        {
            return false;
        }
    }
    public class ParkNumViewModel : INotifyPropertyChanged
    {
        UIApplication uiapp = RevitAPI.UiApplication;


        private string _startvalue = "1";
        public string startvalue { get => _startvalue; set { _startvalue = value; OnPropertyChanged(); } }
        private string _prefix = "";
        public string prefix { get => _prefix; set { _prefix = value; OnPropertyChanged(); } }
        public ObservableCollection<string> paramlist { get; set; }
        private string _param;
        public string param { get { return _param; } set { _param = value; OnPropertyChanged(); } }

        public RelayCommand NumerateCommand { get; set; }

        public ParkNumViewModel()
        {
            Param();
            NumerateCommand = new RelayCommand(param => { Numerate(); }, CanNumerate);
        }
        private void Param()
        {
            paramlist = new ObservableCollection<string>
            {
                "Марка",
                "A_Позиция"
            };
            param = paramlist[0];
        }
        public void Numerate()
        {
            //параметры
            Guid adskPositionParamGuid = new Guid("ae8ff999-1f22-4ed7-ad33-61503d85f0f4"); //A_Позиция
            BuiltInParameter markParam = BuiltInParameter.DOOR_NUMBER; //Марка

            RaiseHideRequest();
            int i = 1;
            int.TryParse(startvalue, out i);
            string parameterName = param;
            using (TransactionGroup group = new TransactionGroup(RevitAPI.Document, "TNov - Ручной нумератор парковок"))
            {
                ISelectionFilter _filter = new ParkSelectionFilter();

                group.Start();

                while (true)
                {
                    try
                    {
                        using (Transaction t = new Transaction(RevitAPI.Document, "TNov - Ручной нумератор парковок"))
                        {
                            t.Start();
                            TransactionHandler.SetWarningResolver(t);
                            Reference reference = RevitAPI.UiDocument.Selection.PickObject(ObjectType.Element, _filter, $"Выберите элемент {i}");
                            Autodesk.Revit.DB.Parameter parameter = RevitAPI.Document.GetElement(reference).get_Parameter(markParam);
                            if (parameterName == "A_Позиция") parameter = RevitAPI.Document.GetElement(reference).get_Parameter(adskPositionParamGuid);
                            if (parameter != null)
                            {
                                parameter.Set(prefix + i.ToString());
                                i++;
                                t.Commit();
                            }
                            else
                            {
                                var info1 = new infowindow280($"Ошибка!\nУ элемента {reference.ElementId} нет параметра {parameterName}."); info1.ShowDialog();
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
