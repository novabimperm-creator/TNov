using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.Attributes;
using System.Collections.Generic;
using System.Linq;
using System;
using Parameter = Autodesk.Revit.DB.Parameter;
using Outline = Autodesk.Revit.DB.Outline;
using View = Autodesk.Revit.DB.View;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Newtonsoft.Json;
using System.IO;
using System.Windows.Threading;
using System.Threading;
using TNov.main;

namespace TNov
{
    public class holesViewModel : INotifyPropertyChanged
    {

        private bool _all = true;
        public bool all
        {
            get => _all; set { _all = value; OnPropertyChanged(); }
        }
        private bool _visible = false;
        public bool visible
        {
            get => _visible; set { _visible = value; OnPropertyChanged(); }
        }
        private bool _cut = true;
        public bool cut
        {
            get => _cut; set { _cut = value; OnPropertyChanged(); }
        }
        private bool _pars = true;
        public bool pars
        {
            get => _pars; set { _pars = value; OnPropertyChanged(); }
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
    
    [Transaction(TransactionMode.Manual)]
    public class holes : IExternalCommand
    {
        private TNovProgressBar holesProgressBar;
        private void ThreadStartingPoint()
        {
            this.holesProgressBar = new TNovProgressBar();
            this.holesProgressBar.Show();
            Dispatcher.Run();
        }
        private XYZ VectorFromHorizVertAngles(double angleHorizD, double angleVertD)
        {
            // Convert degreess to radians.

            double degToRadian = Math.PI * 2 / 360;
            double angleHorizR = angleHorizD * degToRadian;
            double angleVertR = angleVertD * degToRadian;

            // Return unit vector in 3D

            double a = Math.Cos(angleVertR);
            double b = Math.Cos(angleHorizR);
            double c = Math.Sin(angleHorizR);
            double d = Math.Sin(angleVertR);

            return new XYZ(a * b, a * c, d);
        }
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            string TNovClassName = "Отверстия"; DateTime dateTime = DateTime.Now;
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
                if (qok != null && qok == true) { Logger.TurnOffExtendedLogs(); } else Logger.Log("Расширенные логи вкл",2);
            }

            //Проверка актуальности шаблона
            templatecheck tc = new templatecheck(in commandData, out bool oldProject);

            //сценарий работы
            int scenario = 1; //1 - работа в модели КЖ/АР, 2 - работа в самой модели заданий

            string docName = doc.Title.ToString();
            if (docName.Contains("Задани") || docName.Contains("задани") || docName.Contains("-ЗД") || docName.Contains("_ЗД") || docName.Contains("ЗАДАНИЕ")) scenario = 2;


            //Список используемых параметров

            string holeelevationfromlevel = "A_Отверстие_Отметка от этажа";
            if (oldProject == true) { holeelevationfromlevel = "ADSK_Отверстие_Отметка от этажа"; }
            string holelevelelevation = "A_Отверстие_Отметка этажа";
            if (oldProject == true) { holelevelelevation = "ADSK_Отверстие_Отметка этажа"; }
            string holeelevationfromzero = "A_Отверстие_Отметка от нуля";
            if (oldProject == true) { holeelevationfromzero = "ADSK_Отверстие_Отметка от нуля"; }
            BuiltInParameter gm = BuiltInParameter.ALL_MODEL_MODEL; //параметр Группа модели
            ElementId familyNameParamId = new ElementId(-1002002); //id параметра Имя семейства

            //Список допустимых категорий для вырезания
            List<ElementId> catIds = new List<ElementId>();
            ElementId id1 = new ElementId(-2000032); catIds.Add(id1); //плиты
            ElementId id2 = new ElementId(-2000011); catIds.Add(id2); //стены
            ElementId id3 = new ElementId(-2001320); catIds.Add(id3); //каркас несущий
            ElementId id4 = new ElementId(-2001300); catIds.Add(id4); //фунд

            Logger.Log("Сбор элементов",1);

            List<Wall> walls = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_Walls)   //фильтр по категории Стены
                                                                         .WhereElementIsNotElementType()    //фильтр только экземпляры
                                                                         .OfClass(typeof(Wall))         //отсеиваем модели в контексте
                                                                         .Cast<Wall>()                     //элементы категории Стены
                                                                         .ToList();                         //формируем список

            List<Autodesk.Revit.DB.Floor> floors = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_Floors)   //фильтр по категории Перекрытия
                                                                         .WhereElementIsNotElementType()
                                                                         .OfClass(typeof(Autodesk.Revit.DB.Floor))
                                                                         .Cast<Autodesk.Revit.DB.Floor>()
                                                                         .ToList();

            List<FamilyInstance> GMs = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_GenericModel)   //фильтр по категории Об модели
                                                                         .WhereElementIsNotElementType()
                                                                         .OfClass (typeof(FamilyInstance))
                                                                         .Cast<FamilyInstance>()
                                                                         .ToList();

            List<FamilyInstance> windows = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_Windows)   //фильтр по категории Окна
                                                                         .WhereElementIsNotElementType()
                                                                         .Cast<FamilyInstance>()
                                                                         //.Where(it => it.Symbol.get_Parameter(gm).AsString() == "Окно") //только род семейства
                                                                         .ToList();

            List<Group> groups = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_IOSModelGroups)
                .WhereElementIsNotElementType()
                .Cast<Group>()
                .ToList();

            
            List<FamilyInstance> holesGM = new List<FamilyInstance>();
            List<FamilyInstance> holesW = new List<FamilyInstance>();

            
            foreach (FamilyInstance GM in GMs) //ищем отверстия об мод
            {
                //ElementId typeId = GM.GetTypeId();
                //Element type = doc.GetElement(typeId);
                string gmvalue = GM.Symbol.get_Parameter(gm).AsString(); 
                if(gmvalue != null)
                {
                    if (gmvalue.Contains("Отверстие")) { holesGM.Add(GM); }
                }
                
            }

            foreach (FamilyInstance window in windows) //ищем отверстия окна
            {
                //ElementId typeId = window.GetTypeId();
                //Element type = doc.GetElement(typeId);
                string gmvalue = window.Symbol.get_Parameter(gm).AsString(); 
                if (gmvalue != null)
                {
                    if (gmvalue.Contains("Отверстие")) { holesW.Add(window); }
                }
                    
            }

            foreach (FamilyInstance GM in GMs) //ищем термовкладыши
            {
                //ElementId typeId = GM.GetTypeId();
                //Element type = doc.GetElement(typeId);
                string gmvalue = GM.Symbol.FamilyName;
                if (gmvalue != null)
                {
                    if (gmvalue.Contains("ермовкл")) { holesGM.Add(GM); }
                }

            }

            int hGMcount = holesGM.Count;
            int hWcount = holesW.Count;
            int allholescount = hGMcount + hWcount;


            if (allholescount ==  0) 
            { 
                new infowindow280("В проекте отсутствуют отверстия.").ShowDialog(); 
                Logger.Log("Отверстия отсутствуют. Завершение работы.", 3);
                return Result.Cancelled; 
            }

            ElementId workviewid = uidoc.ActiveView.Id;

            //ищем группы, вставленные в модели больше одного раза

            Logger.Log("Ищем группы, вставленные более 1 раза",1);

            List<string> groupNames = new List<string>(); List<string> badGroups = new List<string>(); List<int> badHoles = new List<int>();
            foreach (var group in groups) groupNames.Add(group.Name);

            groupNames.Sort();
            for(int i = 0; i < groupNames.Count; i++)
            {
                if (i == 0) continue;
                if (groupNames[i] == groupNames[i - 1]) badGroups.Add(groupNames[i]);
            }
            List<string> badGroupsUnique = badGroups.Distinct().ToList();

            if (badGroupsUnique.Count > 0)
            {
                string badGroupNames = "";
                bool groupHasHoles = false;
                foreach(var badGroupName in badGroupsUnique)
                {
                    Logger.Log(badGroupName,2);
                    foreach (var group in groups)
                    {
                        if(group.Name == badGroupName)
                        {
                            //элементы "плохой" группы
                            ElementFilter elementFilter = (ElementFilter)new ElementParameterFilter(ParameterFilterRuleFactory.CreateContainsRule(familyNameParamId, "pmN.Отверстие", true));
                            IList<ElementId> groupElems = group.GetDependentElements(elementFilter);
                            if (groupElems.Count > 0)
                            {
                                groupHasHoles = true; Logger.Log("   отверстия есть", 2);
                                foreach (var groupElem in groupElems) badHoles.Add(groupElem.IntegerValue);
                            }
                        }
                    }
                    if(groupHasHoles) badGroupNames = badGroupNames + badGroupName + ", ";
                }
                int index = badGroupNames.Length - 2;
                if(index>3) badGroupNames = badGroupNames.Remove(index);

                if (badGroupNames.Length > 1) {
                    Logger.Log("Открываем окно с именами проблемных групп",1);
                    // Диалоговое окно
                    var viewModel1 = new infowindowtextfieldViewModel();
                    viewModel1.headtxt = "В проекте присутствуют группы с отверстиями, повторяющиеся в модели. Отверстия в этих группах обработаны не будут:";
                    viewModel1.ids = badGroupNames;
                    viewModel1.lowtxt = "Исключите отверстия из таких групп либо используйте Сборки в качестве альтернативы.";
                    var wpfview1 = new infowindowtextfield(viewModel1);
                    viewModel1.CloseRequest += (s, e) => wpfview1.Close();
                    bool? ok1 = wpfview1.ShowDialog();
                }
                
            }



            Logger.Log("Диалоговое окно",1);
            //Диалог
            var viewModel = new holesViewModel();
            // Десериализация
            bool forProject = true;
            json js = new json(in TNovClassName, in forProject, out bool canserialize, out string jsonpath);
            if (canserialize)
            {
                viewModel = JsonConvert.DeserializeObject<holesViewModel>(File.ReadAllText(jsonpath));
                Logger.Log("Десериализация прошла успешно",1);
            }
            var wpfview = new holeswpf(viewModel);
            viewModel.CloseRequest += (s, e) => wpfview.Close();
            bool? ok = wpfview.ShowDialog();
            if (ok != null && ok == true) { }
            else { Logger.Log("Запуск отменен пользователем. Завершение работы.", 3); return Result.Cancelled; }
            //Сериализация
            try
            {
                File.WriteAllText(jsonpath, JsonConvert.SerializeObject(viewModel));
                Logger.Log("Сериализация прошла успешно", 1);
            }
            catch (Exception ex) { Logger.Log("Ошибка при сериализации: " + ex.Message,4); }

            bool all = viewModel.all; bool visible = viewModel.visible; 


            if (visible == true) { workviewid = uidoc.ActiveView.Id; }

            if (visible != true)
            {
                //Создаем 3д-вид, где видны все элементы
                Logger.Log("Настраиваем вид TNov",1);

                List<View> views = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_Views)   //фильтр по категории Виды
                                                                         .WhereElementIsNotElementType()    //фильтр только экземпляры
                                                                         .Cast<View>()                     //элементы категории Виды
                                                                         .ToList();                         //формируем список

                ViewFamilyType viewFamilyType3D = new FilteredElementCollector(doc)
                                                                                .OfClass(typeof(ViewFamilyType))
                                                                                .Cast<ViewFamilyType>()
                                                                                .FirstOrDefault<ViewFamilyType>(
                                                                                x => ViewFamily.ThreeDimensional == x.ViewFamily);
                double angleHorizD = 90;
                double angleVertD = 0;

                bool viewexist = false;
                foreach (View view in views) { if (view.Name == "TNov") { viewexist = true; } }

                XYZ eye = XYZ.Zero;

                XYZ forward = VectorFromHorizVertAngles(
                  angleHorizD, angleVertD);

                XYZ up = VectorFromHorizVertAngles(
                  angleHorizD, angleVertD + 90);

                ViewOrientation3D viewOrientation3D
                  = new ViewOrientation3D(eye, up, forward);
                
                if (viewexist == false)
                {
                    using (Transaction transaction0 = new Transaction(doc))
                    {

                        transaction0.Start("TNov - рабочий 3D-вид");

                        View3D view3d = View3D.CreateIsometric(doc, viewFamilyType3D.Id);

                        view3d.SetOrientation(viewOrientation3D);

                        view3d.Name = "TNov";

                        workviewid = view3d.Id;

                        transaction0.Commit();
                    }
                }
                else
                {
                    //3d-вид создан либо существует, сбрасываем его подрезку
                    List<View> views1 = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_Views)   //фильтр по категории Виды
                                                                                 .WhereElementIsNotElementType()    //фильтр только экземпляры
                                                                                 .Cast<View>()                     //элементы категории Виды
                                                                                 .ToList();                         //формируем список
                    foreach (View view in views1) { if (view.Name == "TNov") { /*uidoc.ActiveView = view*/; workviewid = view.Id; } }
                    Autodesk.Revit.DB.View3D workview3d;
                    workview3d = (View3D)doc.GetElement(workviewid);

                    using (Transaction transaction0 = new Transaction(doc))
                    {

                        transaction0.Start("TNov - рабочий 3D-вид");

                        workview3d.IsSectionBoxActive = false;

                        transaction0.Commit();
                    }
                }
                Logger.Log("Вид TNov настроен для работы",1);
            }

            //итоговый список отверстий в работу
            List<FamilyInstance> holesFinalList = new List<FamilyInstance>();
            foreach(var h in holesGM)
            {
                int hId = h.Id.IntegerValue; int b = 0;
                foreach(var badHole in badHoles)
                {
                    if (hId == badHole) b++;
                }
                if (b == 0) holesFinalList.Add(h);
            }

            int allcount = holesFinalList.Count;

            

            if (viewModel.cut || viewModel.pars)
            {
                Thread thread = new Thread(new ThreadStart(this.ThreadStartingPoint));
                thread.SetApartmentState(ApartmentState.STA);
                thread.IsBackground = true;
                thread.Start();
                Thread.Sleep(100);

                int PBCount = 0;
                this.holesProgressBar.TNov_ProgressBar.Dispatcher.Invoke<double>((Func<double>)(() => this.holesProgressBar.TNov_ProgressBar.Minimum = (double)PBCount));
                this.holesProgressBar.TNov_ProgressBar.Dispatcher.Invoke<string>((Func<string>)(() => this.holesProgressBar.value.Text = PBCount.ToString()));
                this.holesProgressBar.TNov_ProgressBar.Dispatcher.Invoke<double>((Func<double>)(() => this.holesProgressBar.TNov_ProgressBar.Maximum = (double)allcount));
                this.holesProgressBar.TNov_ProgressBar.Dispatcher.Invoke<string>((Func<string>)(() => this.holesProgressBar.maxvalue.Text = allcount.ToString()));

                if (viewModel.cut)
                {
                    Logger.Log("Вырезание отверстий", 1);

                    using (Transaction transaction = new Transaction(doc))
                    {

                        transaction.Start("TNov - вырезать отверстия");
                        Logger.Log("Открываем транзакцию", 1);

                        foreach (FamilyInstance hole in holesFinalList)
                        {
                            PBCount++;
                            this.holesProgressBar.TNov_ProgressBar.Dispatcher.Invoke<double>((Func<double>)(() => this.holesProgressBar.TNov_ProgressBar.Value = (double)PBCount));
                            this.holesProgressBar.TNov_ProgressBar.Dispatcher.Invoke<string>((Func<string>)(() => this.holesProgressBar.value.Text = "Вырезание отверстий " + PBCount.ToString()));

                            Element elem1 = doc.GetElement(hole.Id);
                            BoundingBoxXYZ elem1box = elem1.get_BoundingBox(doc.ActiveView);
                            Outline outline1 = new Outline(elem1box.Min, elem1box.Max);
                            BoundingBoxIntersectsFilter bbfilter = new BoundingBoxIntersectsFilter(outline1);
                            FilteredElementCollector collector = new FilteredElementCollector(doc, workviewid);
                            ICollection<ElementId> idsExclude = new List<ElementId> { elem1.Id };
                            collector.Excluding(idsExclude)
                                    .WherePasses(bbfilter);
                            Logger.Log("Отверстие " + hole.Id, 2);
                            foreach (Element elem2 in collector)
                            {
                                int catId = elem2.Category.Id.IntegerValue;
                                bool cutElem2 = false;
                                foreach (ElementId i in catIds)
                                {
                                    if (i.IntegerValue == catId) { cutElem2 = true; break; }
                                }
                                if (cutElem2)
                                {
                                    try
                                    {
                                        _Intersections.CutElement(doc, elem2, elem1);
                                        Logger.Log("   Элемент " + elem2.Id + ": вырезано успешно", 2);
                                    }
                                    catch (Exception ex)
                                    {
                                        Logger.Log("   Элемент " + elem2.Id + " Ошибка: " + ex.Message, 4);
                                    }
                                }

                            }

                        }


                        transaction.Commit();
                        Logger.Log("Закрываем транзакцию", 1);
                    }
                }

                int failscount = 0; 
                List<string> failed = new List<string>(); //пустой список id элементов с недоступным параметром

                if (viewModel.pars)
                {
                    allcount = allcount + holesW.Count;
                    PBCount = 0;
                    this.holesProgressBar.TNov_ProgressBar.Dispatcher.Invoke<double>((Func<double>)(() => this.holesProgressBar.TNov_ProgressBar.Value = (double)PBCount));
                    this.holesProgressBar.TNov_ProgressBar.Dispatcher.Invoke<string>((Func<string>)(() => this.holesProgressBar.value.Text = PBCount.ToString()));


                    
                    

                    Logger.Log("Заполнение параметров", 1);

                    using (Transaction transaction2 = new Transaction(doc))
                    {
                        transaction2.Start("TNov - отметки отверстий");
                        Logger.Log("Открываем транзакцию", 1);

                        foreach (var hole in holesFinalList)
                        {
                            PBCount++;
                            this.holesProgressBar.TNov_ProgressBar.Dispatcher.Invoke<double>((Func<double>)(() => this.holesProgressBar.TNov_ProgressBar.Value = (double)PBCount));
                            this.holesProgressBar.TNov_ProgressBar.Dispatcher.Invoke<string>((Func<string>)(() => this.holesProgressBar.value.Text = "Заполнение отметок " + PBCount.ToString()));

                            string gmvalue = hole.Symbol.FamilyName;
                            if (gmvalue != null)
                            {
                                if (gmvalue.Contains("ермовкл")) continue;
                            }

                            string eid = hole.Id.ToString();
                            Logger.Log("   Элемент" + eid, 2);
                            try
                            {
                                double otm = hole.get_Parameter(BuiltInParameter.INSTANCE_ELEVATION_PARAM).AsDouble();
                                Parameter efl = hole.LookupParameter(holeelevationfromlevel);
                                double efl_val = otm;
                                efl.Set(efl_val); //Отметка от уровня
                                Logger.Log("      параметр " + holeelevationfromlevel + ": значение " + efl_val.ToString(), 2);
                                Element level = doc.GetElement(hole.LevelId);
                                double elev = level.LookupParameter("Фасад").AsDouble();
                                Parameter ele = hole.LookupParameter(holelevelelevation);
                                double ele_val = elev;
                                ele.Set(ele_val); //Отметка уровня
                                Logger.Log("      параметр " + holelevelelevation + ": значение " + ele_val.ToString(), 2);
                                Parameter eefz = hole.LookupParameter(holeelevationfromzero);
                                double eefz_val = efl_val + ele_val;
                                eefz.Set(eefz_val); //Отметка от нуля
                                Logger.Log("      параметр " + holeelevationfromzero + ": значение " + eefz_val.ToString(), 2);
                            }
                            catch (Exception ex)
                            {
                                Logger.Log("   Элемент" + eid + " Ошибка: " + ex.Message, 4);
                                failed.Add(eid); failscount++; continue;
                            }
                        }

                        foreach (var hole in holesW)
                        {
                            PBCount++;
                            this.holesProgressBar.TNov_ProgressBar.Dispatcher.Invoke<double>((Func<double>)(() => this.holesProgressBar.TNov_ProgressBar.Value = (double)PBCount));
                            this.holesProgressBar.TNov_ProgressBar.Dispatcher.Invoke<string>((Func<string>)(() => this.holesProgressBar.value.Text = "Заполнение отметок " + PBCount.ToString()));


                            string eid = hole.Id.ToString();
                            Logger.Log("   Элемент" + eid, 2);
                            try
                            {
                                double otm = hole.LookupParameter("Высота нижнего бруса").AsDouble();
                                Parameter efl = hole.LookupParameter(holeelevationfromlevel);
                                double efl_val = otm;
                                efl.Set(efl_val); //Отметка от уровня
                                Logger.Log("      параметр " + holeelevationfromlevel + ": значение " + efl_val.ToString(), 2);
                                Element level = doc.GetElement(hole.LevelId);
                                double elev = level.LookupParameter("Фасад").AsDouble();
                                Parameter ele = hole.LookupParameter(holelevelelevation);
                                double ele_val = elev;
                                ele.Set(ele_val); //Отметка уровня
                                Logger.Log("      параметр " + holelevelelevation + ": значение " + ele_val.ToString(), 2);
                                Parameter eefz = hole.LookupParameter(holeelevationfromzero);
                                double eefz_val = efl_val + ele_val;
                                eefz.Set(eefz_val); //Отметка от нуля
                                Logger.Log("      параметр " + holeelevationfromzero + ": значение " + eefz_val.ToString(), 2);
                            }
                            catch (Exception ex)
                            {
                                Logger.Log("   Элемент" + eid + " Ошибка: " + ex.Message, 4);
                                failed.Add(eid); failscount++; continue;
                            }
                        }


                        transaction2.Commit();

                        Logger.Log("Закрываем транзакцию", 1);

                    }
                    

                    
                }

                this.holesProgressBar.Dispatcher.Invoke((System.Action)(() => this.holesProgressBar.Close()));

                if (failscount > 0)
                {
                    Logger.Log("Открываем окно с ID проблемных элементов", 1);
                    // Диалоговое окно
                    var viewModel1 = new infowindowtextfieldViewModel();
                    viewModel1.headtxt = "Один или несколько элементов не изменены:";
                    viewModel1.ids = String.Join(",", failed);
                    viewModel1.lowtxt = "Проверьте их вручную или посмотрите ошибки в лог-файле.";
                    var wpfview1 = new infowindowtextfield(viewModel1);
                    viewModel1.CloseRequest += (s, e) => wpfview1.Close();
                    bool? ok1 = wpfview1.ShowDialog();
                    Logger.Log(viewModel1.ids, 1);
                }
            }

            //сценарий "Задания": вывести имена проблемных групп

            if (scenario == 2)
            {
                List<string> badNames = new List<string>();
                badNames.Add("В модели присутствуют некорректно названные группы");
                foreach(var group in groupNames)
                {
                    string[] nameParts = group.Split('_');
                    if (nameParts.Length < 3) badNames.Add(group);
                }
                if (badNames.Count > 1) 
                {
                    badNames.Add("необходимо наличие блоков ОтКого_Кому_Этаж.");
                    string badNamesStr = String.Join(", ", badNames);
                    new infowindow400(badNamesStr).ShowDialog();
                    Logger.Log(badNamesStr,1);
                }
            }
            
            Logger.Log("Завершение работы.",5);
            return Result.Succeeded;
        }
    }
    
}
