using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.Attributes;
using System.Collections.Generic;
using System.Linq;
using System;

using Parameter = Autodesk.Revit.DB.Parameter;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Newtonsoft.Json;
using System.IO;
using TNov.main;

namespace TNov
{
    public class eflwpfinputViewModel : INotifyPropertyChanged 
    {

        private string _vs = "90";
        public string vs { get => _vs; set { _vs = value; } }

        private string _op = "2730";
        public string op { get => _op; set { _op = value; } }
                
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

    [Transaction(TransactionMode.Manual)]
    public class efl : IExternalCommand
    {
        
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            string TNovClassName = "ЭЛ Отметки размещения"; DateTime dateTime = DateTime.Now;
            //подключение приложения и документа
            if (RevitAPI.UiApplication == null) { RevitAPI.Initialize(commandData); }
            UIDocument uidoc = RevitAPI.UiDocument; Document doc = RevitAPI.Document;
            UIApplication uiApp = RevitAPI.UiApplication; Autodesk.Revit.ApplicationServices.Application rvtApp = uiApp.Application;
            
            //проверка подключения, запись в журнал
            bool check = false; servercheck sc = new servercheck(in TNovClassName, out check); if (check == false) { return Result.Failed; }

            // создание log - файла
            Logger.Initialize(TNovClassName);

            var viewModel0 = new aboutViewModel();
            
            string jsonpath0 = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "TNovClient/TNovSettings.json");
            viewModel0 = JsonConvert.DeserializeObject<aboutViewModel>(File.ReadAllText(jsonpath0));
            if (viewModel0.extendedLogs)
            
            {
                var qViewModel = new qwindow280ViewModel();
                qViewModel.headtxt = "Включены расширенные логи. " +
                    "Плагин будет работать медленнее, но соберет больше данных. " +
                    "Выключить расширенные логи для ускорения работы?";
                var qwpfview = new qwindow280(qViewModel);
                qViewModel.CloseRequest += (s, e) => qwpfview.Close();
                bool? qok = qwpfview.ShowDialog();
                if (qok != null && qok == true) { Logger.TurnOffExtendedLogs(); } else Logger.Log( "Расширенные логи вкл", 2);
            }

            Logger.Log("Сбор элементов",1);
            List<Level> levels = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_Levels)   //фильтр по категории Уровни
                                                                         .WhereElementIsNotElementType()    //фильтр только экземпляры
                                                                         .Cast<Level>()                     //элементы категории Уровни
                                                                         .ToList();                         //формируем список
            
           

            
            ElementId workviewid = uidoc.ActiveView.Id;
            FilteredElementCollector collector = new FilteredElementCollector(doc, workviewid);
            
            List<FamilyInstance> els1 = new FilteredElementCollector(doc, workviewid).OfCategory(BuiltInCategory.OST_LightingDevices).WhereElementIsNotElementType().Cast<FamilyInstance>().ToList();
            List<FamilyInstance> els2 = new FilteredElementCollector(doc, workviewid).OfCategory(BuiltInCategory.OST_LightingFixtures).WhereElementIsNotElementType().Cast<FamilyInstance>().ToList();
            List<FamilyInstance> els3 = new FilteredElementCollector(doc, workviewid).OfCategory(BuiltInCategory.OST_ElectricalFixtures).WhereElementIsNotElementType().Cast<FamilyInstance>().ToList();
            List<FamilyInstance> els4 = new FilteredElementCollector(doc, workviewid).OfCategory(BuiltInCategory.OST_ElectricalEquipment).WhereElementIsNotElementType().Cast<FamilyInstance>().ToList();

            List<FamilyInstance> els = new List<FamilyInstance>(els1.Count + els2.Count + els3.Count+ els4.Count); //общий список 
            els.AddRange(els1);
            els.AddRange(els2);
            els.AddRange(els3);
            els.AddRange(els4);

            //MessageBox.Show(els.Count.ToString());

            Logger.Log("Элементы собраны. Открываем окно задания значений по умолчанию",1);

            //Вьюмодель (без открытия окна)
            var viewModel = new eflwpfinputViewModel();
            // Десериализация
            bool forProject = true;
            json js = new json(in TNovClassName, in forProject, out bool canserialize, out string jsonpath);
            if (canserialize)
            {
                viewModel = JsonConvert.DeserializeObject<eflwpfinputViewModel>(File.ReadAllText(jsonpath));
                Logger.Log("Десериализация прошла успешно",1);
            }
            
            //Сериализация
            try
            {
                File.WriteAllText(jsonpath, JsonConvert.SerializeObject(viewModel));
                Logger.Log("Сериализация прошла успешно",1);
            }
            catch (Exception ex) { Logger.Log("Ошибка при сериализации: " + ex.Message,4); }

            string vs = viewModel.vs;
            string op = viewModel.op;

            if (double.TryParse(vs, out double vs_d) && double.TryParse(op, out double op_d)) { }
            else { string info1txt = "Введенная информация содержит недопустимые символы."; 
                var info1 = new infowindow280(info1txt); info1.ShowDialog();
                Logger.Log("Значения по умолчанию заданы в неверном формате. Завершение работы.",3); 
                return Result.Cancelled; }
            
            //double vs_d = 90; double op_d = 2730;

            Logger.Log("Значения по умолчанию заданы: "+ vs_d.ToString() + " и " + op_d.ToString() + ". Открываем транзакцию",1);

            using (Transaction transaction = new Transaction(doc))
            {
                transaction.Start("TNov - Заполнятор ЭЛ");
                foreach (var elem in els)
                {
                    Element e = doc.GetElement(elem.Id);
                    Logger.Log("Элемент " + elem.Id,2);
                    string elemlevel = "";
                    try
                    {
                        elemlevel += e.LookupParameter("Уровень").AsString();
                    }
                    catch (Exception) { }
                    string elemplane = "";
                    try
                    {
                        elemplane += e.LookupParameter("Рабочая плоскость").AsString();
                    }
                    catch ( Exception) {  }
                    string elembase = "";
                    try
                    {
                        elembase += e.LookupParameter("Основа").AsString();
                    }
                    catch (Exception) {  }
                    
                    
                    if (elemlevel.Contains("Уровень"))
                    {
                        foreach (Level level in levels)
                        {
                            if (elemlevel.Contains(level.Name))
                            {
                                //i1++;
                                Logger.Log("    Уровень " + level.Name,2);
                                double par_vs = level.LookupParameter("N_ЭЛ.Высота стяжки").AsDouble() ;
                                string vs_s;
                                if (par_vs == 0) { par_vs = vs_d / 304.8; vs_s = vs_d.ToString(); } else { vs_s = (par_vs*304.8).ToString(); }
                                Logger.Log("    Высота стяжки " + vs_s,2);
                                double par_op = level.LookupParameter("N_ЭЛ.Отметка потолка").AsDouble() ;
                                string op_s;
                                if (par_op == 0) { par_op = op_d / 304.8; op_s = op_d.ToString(); } else { op_s = (par_op * 304.8).ToString(); }
                                Logger.Log("    Отметка потолка " + op_s,2);
                                try
                                {
                                    Parameter elem_par_vs = e.LookupParameter("N_ЭЛ.Высота стяжки");
                                    Parameter elem_par_op = e.LookupParameter("N_ЭЛ.Отметка потолка"); 
                                    elem_par_vs.Set(par_vs);
                                    elem_par_op.Set(par_op);
                                    Logger.Log("    Успех",2);
                                }
                                catch (Exception ex) { Logger.Log("    Ошибка: "+ex.Message,4); }

                            }
                        }
                    }
                    else if (elemplane.Contains("Уровень"))
                    {
                        foreach (Level level in levels)
                        {
                            if (elemplane.Contains(level.Name))
                            {
                                //i2++;
                                Logger.Log("    Рабочая плоскость " + level.Name, 2);
                                double par_vs = level.LookupParameter("N_ЭЛ.Высота стяжки").AsDouble();
                                string vs_s;
                                if (par_vs == 0) { par_vs = vs_d / 304.8; vs_s = vs_d.ToString(); } else { vs_s = (par_vs * 304.8).ToString(); }
                                Logger.Log("    Высота стяжки " + vs_s, 2);
                                double par_op = level.LookupParameter("N_ЭЛ.Отметка потолка").AsDouble();
                                string op_s;
                                if (par_op == 0) { par_op = op_d / 304.8; op_s = op_d.ToString(); } else { op_s = (par_op * 304.8).ToString(); }
                                Logger.Log("    Отметка потолка " + op_s, 2);
                                try
                                {
                                    Parameter elem_par_vs = e.LookupParameter("N_ЭЛ.Высота стяжки");
                                    Parameter elem_par_op = e.LookupParameter("N_ЭЛ.Отметка потолка");
                                    elem_par_vs.Set(par_vs);
                                    elem_par_op.Set(par_op);
                                    Logger.Log("    Успех", 2);
                                }
                                catch (Exception ex) { Logger.Log("    Ошибка: " + ex.Message,4); }

                            }
                        }
                    }
                    else if (elembase.Contains("Уровень"))
                    {
                        foreach (Level level in levels)
                        {
                            if (elembase.Contains(level.Name))
                            {
                                //i3++;
                                Logger.Log("    Основа " + level.Name,2);
                                double par_vs = level.LookupParameter("N_ЭЛ.Высота стяжки").AsDouble();
                                string vs_s;
                                if (par_vs == 0) { par_vs = vs_d / 304.8; vs_s = vs_d.ToString(); } else { vs_s = (par_vs * 304.8).ToString(); }
                                Logger.Log("    Высота стяжки " + vs_s,2);
                                double par_op = level.LookupParameter("N_ЭЛ.Отметка потолка").AsDouble();
                                string op_s;
                                if (par_op == 0) { par_op = op_d / 304.8; op_s = op_d.ToString(); } else { op_s = (par_op * 304.8).ToString(); }
                                Logger.Log("    Отметка потолка " + op_s,2);
                                try
                                {
                                    Parameter elem_par_vs = e.LookupParameter("N_ЭЛ.Высота стяжки");
                                    Parameter elem_par_op = e.LookupParameter("N_ЭЛ.Отметка потолка");
                                    elem_par_vs.Set(par_vs);
                                    elem_par_op.Set(par_op);
                                    Logger.Log("    Успех", 2);
                                }
                                catch (Exception ex) { Logger.Log("    Ошибка: " + ex.Message,4); }

                            }
                        }
                    }
                    else
                    {
                        //i4++;
                        Logger.Log("    Элемент без уровня и основы",2);
                        try
                        {
                            Parameter elem_par_vs = e.LookupParameter("N_ЭЛ.Высота стяжки");
                            Parameter elem_par_op = e.LookupParameter("N_ЭЛ.Отметка потолка"); 
                            elem_par_vs.Set(vs_d/304.8);
                            elem_par_op.Set(op_d / 304.8);
                            Logger.Log("    Назначены значения "+vs_d.ToString()+" мм и " + op_d.ToString()+" мм", 2);
                        }
                        catch (Exception ex) { Logger.Log("    Ошибка: " + ex.Message,4); }
                    }
                }
                /*MessageBox.Show(i1.ToString());
                MessageBox.Show(i2.ToString());
                MessageBox.Show(i3.ToString());
                MessageBox.Show(i4.ToString());*/

                transaction.Commit();
                Logger.Log("Закрываем транзакцию.",1);

                string info1txt;
                if (els.Count > 0)
                { info1txt = "Параметры заполнены у " + els.Count.ToString() + " элементов.";} else { info1txt = "Нечего обрабатывать."; }
                var info1 = new infowindow280(info1txt); info1.ShowDialog();
                

            }


            Logger.Log("Завершение работы.", 5);
            return Result.Succeeded;
        }
    }

}
