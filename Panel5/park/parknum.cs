using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.Attributes;
using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Autodesk.Revit.UI.Selection;
using System.Collections.ObjectModel;
using TNov.main;

namespace TNov
{
    public class ParkSelectionFilter : ISelectionFilter
    {
        public bool AllowElement(Element element)
        {
            if (element.Category.Name=="Парковка") return true; else return false;
        }

        public bool AllowReference(Reference refer, XYZ point)
        {
            return false; 
        }
    }
    public class parknumViewModel : INotifyPropertyChanged
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

        public parknumViewModel() 
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
                            Autodesk.Revit.DB.Parameter parameter = RevitAPI.Document.GetElement(reference).LookupParameter(parameterName);
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
    [Transaction(TransactionMode.Manual)]
    public class parknum : IExternalCommand
    {
        
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            string TNovClassName = "Парковки Ручной нумератор"; DateTime dateTime = DateTime.Now;
            //подключение приложения и документа
            if (RevitAPI.UiApplication == null) { RevitAPI.Initialize(commandData); }
            UIDocument uidoc = RevitAPI.UiDocument; Document doc = RevitAPI.Document;
            UIApplication uiApp = RevitAPI.UiApplication; Autodesk.Revit.ApplicationServices.Application rvtApp = uiApp.Application;
            
            //проверка подключения, запись в журнал
            bool check = false; servercheck sc = new servercheck(in TNovClassName, out check); if (check == false) { return Result.Failed; }

            // создание log - файла
            Logger.Initialize(TNovClassName);
            

            Logger.Log("Диалоговое окно",1);

            var viewModel = new parknumViewModel();
            var view = new parknumwpf(viewModel);
            viewModel.CloseRequest += (s, e) => view.Close();
            viewModel.HideRequest += (s, e) => view.Hide();
            viewModel.ShowRequest += (s, e) => view.ShowDialog();
            view.ShowDialog();



            Logger.Log("Завершение работы.",5);
            return Result.Succeeded;
        }
    }
    
}
