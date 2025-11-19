using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.Attributes;
using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Autodesk.Revit.UI.Selection;
using TNov.main;

namespace TNov
{

    public class apartsnumnewViewModel : INotifyPropertyChanged
    {
        private string _parameterName = "N_Кв.НомерНаЭтаже";
        public string parameterName { get => _parameterName; set { _parameterName = value; OnPropertyChanged(); } }
        private string _startvalue = "1";
        public string startvalue { get => _startvalue; set { _startvalue = value; OnPropertyChanged(); } }
        private bool _recalcnums = true;
        public bool recalcnums { get => _recalcnums; set { _recalcnums = value; OnPropertyChanged(); } }
        public RelayCommand NumerateCommand { get; set; } 

        public apartsnumnewViewModel() 
        {
            NumerateCommand = new RelayCommand(param => { Numerate(); }, CanNumerate);
        }
        public void Numerate()
        {
            RaiseHideRequest();
            int i = 1;
            int.TryParse(startvalue, out i);
            
            using (TransactionGroup group = new TransactionGroup(RevitAPI.Document, "TNov - Ручной нумератор квартир"))
            {
                ISelectionFilter _filter = new RoomSelectionFilter();
                group.Start();

                while (true)
                {
                    try
                    {
                        using (Transaction t = new Transaction(RevitAPI.Document, "TNov - Ручной нумератор квартир"))
                        {
                            t.Start();
                            Reference reference = RevitAPI.UiDocument.Selection.PickObject(ObjectType.Element, _filter, $"Выберите элемент {i}");
                            Autodesk.Revit.DB.Parameter parameter = RevitAPI.Document.GetElement(reference).LookupParameter(parameterName);
                            if (parameter != null)
                            {
                                parameter.Set(i);
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
            i++;
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
    public class apartsnumnew : IExternalCommand
    {
        
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            string TNovClassName = "Нумератор квартир"; DateTime dateTime = DateTime.Now;
            //подключение приложения и документа
            if (RevitAPI.UiApplication == null) { RevitAPI.Initialize(commandData); }
            UIDocument uidoc = RevitAPI.UiDocument; Document doc = RevitAPI.Document;
            UIApplication uiApp = RevitAPI.UiApplication; Autodesk.Revit.ApplicationServices.Application rvtApp = uiApp.Application;
            
            //проверка подключения, запись в журнал
            bool check = false; servercheck sc = new servercheck(in TNovClassName, out check); if (check == false) { return Result.Failed; }

            // создание log - файла
            Logger.Initialize(TNovClassName);
            

            //Проверка актуальности шаблона
            templatecheck tc = new templatecheck(in commandData, out bool oldProject);

            string parameterName = "N_Кв.НомерНаЭтаже"; if (oldProject == true) { parameterName = "Квартира.Номер.ПоЭтажам"; }

            Logger.Log( "Диалоговое окно",1);

            var viewModel = new apartsnumnewViewModel();
            viewModel.parameterName = parameterName;
            var view = new apartsnumnewwpf(viewModel);
            viewModel.CloseRequest += (s, e) => view.Close();
            viewModel.HideRequest += (s, e) => view.Hide();
            viewModel.ShowRequest += (s, e) => view.ShowDialog();
            view.ShowDialog();

            bool runrecalc = viewModel.recalcnums;
            if (runrecalc) { Logger.Log("Завершение работы.", 5); apartsnum Command1 = new apartsnum(); Command1.Execute(commandData, ref message, elements); }

            Logger.Log( "Завершение работы.",5);
            return Result.Succeeded;
        }
    }
    
}
