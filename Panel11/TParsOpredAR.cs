using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.UI;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
using TNovCommon;
using TNovCommon;

namespace TNov
{
    [Transaction(TransactionMode.Manual)]
    public class TParsOpredAR : IExternalCommand
    {
        private TNovProgressBar TParsOpredSTProgressBar;
        private void ThreadStartingPoint()
        {
            this.TParsOpredSTProgressBar = new TNovProgressBar();
            this.TParsOpredSTProgressBar.Show();
            Dispatcher.Run();
        }
        //параметры
        Guid TOprParamGuid = new Guid("7b538440-ae96-4e43-9dbb-4d35be82eb9c"); //Т_Определение
        Guid NTParamsNotSetParamGuid = new Guid("70879f6b-b838-49de-8ff5-35e1c7d97e0c");
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            string TNovClassName = "Т Параметры Определение АР"; DateTime dateTime = DateTime.Now;
            //подключение приложения и документа
            if (RevitAPI.UiApplication == null) { RevitAPI.Initialize(commandData); }
            UIDocument uidoc = RevitAPI.UiDocument; Document doc = RevitAPI.Document;
            UIApplication uiApp = RevitAPI.UiApplication; Autodesk.Revit.ApplicationServices.Application rvtApp = uiApp.Application;

            //проверка подключения, запись в журнал
            if(ServerUtils.CheckConnection(TNovClassName)==false) return Result.Failed;

            // создание log - файла
            Logger.Initialize(TNovClassName);

            var viewModel0 = new AppVersionViewModel();

            string jsonpath0 = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "TNovClient/TNovSettings.json");
            viewModel0 = JsonConvert.DeserializeObject<AppVersionViewModel>(File.ReadAllText(jsonpath0));

            if (viewModel0.extendedLogs)

            {
                var qViewModel = new QuestionWindowViewModel();
                qViewModel.headtxt = "Включены расширенные логи. " +
                    "Плагин будет работать медленнее, но соберет больше данных. " +
                    "Выключить расширенные логи для ускорения работы?";
                var qwpfview = new QuestionWindow280(qViewModel);
                qViewModel.CloseRequest += (s, e) => qwpfview.Close();
                bool? qok = qwpfview.ShowDialog();
                if (qok != null && qok == true) { Logger.TurnOffExtendedLogs(); } else Logger.Log("Расширенные логи вкл", 2);
            }

                        
            //сбор элементов
            List<ElementId> ids = new List<ElementId>();
            List<string> badCats = new List<string>();

            List<FamilyInstance> beams = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_StructuralFraming)   //Каркас несущий
                                                                         .WhereElementIsNotElementType()
                                                                         .Cast<FamilyInstance>()
                                                                         .ToList();
            if (beams.Count() > 0)
            {
                if (Param.ParamExistByGuid(NTParamsNotSetParamGuid, doc.GetElement(beams.First().Id)) == false)
                { badCats.Add("Каркас несущий"); Logger.Log("Отсутствует параметр N_Т Параметры вручную у категории Каркас несущий", 1); }
                else { foreach (var e in beams) ids.Add(e.Id); }
            }
            
            
            List<Wall> walls = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_Walls)   //Стены
                                                                         .WhereElementIsNotElementType()    
                                                                         .OfClass(typeof(Wall))         //отсеиваем модели в контексте
                                                                         .Cast<Wall>()                     
                                                                         .ToList();
            if (walls.Count() > 0)
            {
                if (Param.ParamExistByGuid(NTParamsNotSetParamGuid, doc.GetElement(walls.First().Id)) == false)
                { badCats.Add("Стены"); Logger.Log("Отсутствует параметр N_Т Параметры вручную у категории Стены", 1); }
                else { foreach (var e in walls) ids.Add(e.Id); }
            }
                

            List<Autodesk.Revit.DB.Floor> floors = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_Floors)   //Перекрытия
                                                                         .WhereElementIsNotElementType()
                                                                         .OfClass(typeof(Autodesk.Revit.DB.Floor))  //отсеиваем модели в контексте
                                                                         .Cast<Autodesk.Revit.DB.Floor>()
                                                                         .ToList();
            if (floors.Count() > 0)
            {
                if (Param.ParamExistByGuid(NTParamsNotSetParamGuid, doc.GetElement(floors.First().Id)) == false)
                { badCats.Add("Перекрытия"); Logger.Log("Отсутствует параметр N_Т Параметры вручную у категории Перекрытия", 1); }
                else { foreach (var e in floors) ids.Add(e.Id); }
            }
                

            List<Autodesk.Revit.DB.Architecture.Stairs> stairs = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_Stairs)   //Лестницы
                                                                         .WhereElementIsNotElementType()
                                                                         .OfClass(typeof(Autodesk.Revit.DB.Architecture.Stairs))  //отсеиваем модели в контексте
                                                                         .Cast<Autodesk.Revit.DB.Architecture.Stairs>()
                                                                         .ToList();
            if (stairs.Count() > 0)
            {
                if (Param.ParamExistByGuid(NTParamsNotSetParamGuid, doc.GetElement(stairs.First().Id)) == false)
                { badCats.Add("Лестницы"); Logger.Log("Отсутствует параметр N_Т Параметры вручную у категории Лестницы", 1); }
                else { foreach (var e in stairs) ids.Add(e.Id); }
            }
            

            List<FamilyInstance> stairs2 = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_Stairs)   //Лестницы семействами
                                                                         .WhereElementIsNotElementType()
                                                                         .OfClass(typeof(Autodesk.Revit.DB.FamilyInstance))
                                                                         .Cast<FamilyInstance>()
                                                                         .ToList();
            if (stairs2.Count() > 0)
            {
                if (Param.ParamExistByGuid(NTParamsNotSetParamGuid, doc.GetElement(stairs2.First().Id)) == false)
                { badCats.Add("Лестницы"); Logger.Log("Отсутствует параметр N_Т Параметры вручную у категории Лестницы", 1); }
                else { foreach (var e in stairs2) ids.Add(e.Id); }
            }

            List<StairsRun> stairRuns = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_StairsRuns)
                .WhereElementIsNotElementType().OfClass(typeof(StairsRun)).Cast<StairsRun>().ToList(); //марши
            if (stairRuns.Count() > 0)
            {
                /*if (Param.ParamExistByGuid(NTParamsNotSetParamGuid, doc.GetElement(stairRuns.First().Id)) == false)
                { badCats.Add("Лестницы Марши"); Logger.Log("Отсутствует параметр N_Т Параметры вручную у категории Лестницы Марши", 1); }
                else {*/ 
                foreach (var e in stairRuns) ids.Add(e.Id); //} //упрощение: всем вложенным элементам лестниц назначаем единое значение
            }

            List<StairsLanding> stairLandings = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_StairsLandings)
                .WhereElementIsNotElementType().OfClass(typeof(StairsLanding)).Cast<StairsLanding>().ToList(); //площадки
            if (stairLandings.Count() > 0)
            {
                /*if (Param.ParamExistByGuid(NTParamsNotSetParamGuid, doc.GetElement(stairLandings.First().Id)) == false)
                { badCats.Add("Лестницы Площадки"); Logger.Log("Отсутствует параметр N_Т Параметры вручную у категории Лестницы Площадки", 1); }
                else {*/
                foreach (var e in stairLandings) ids.Add(e.Id); //}
            }

            ElementId stairSupportsСategoryId = new ElementId(-2000123);
            ElementCategoryFilter stairSupportsСategoryFilter = new ElementCategoryFilter(stairSupportsСategoryId);
            List<Element> stairSupports = new FilteredElementCollector(doc)
                .WhereElementIsNotElementType()
                .WherePasses(stairSupportsСategoryFilter)
                .Cast<Element>()
                .ToList(); //опоры
            if (stairSupports.Count() > 0)
            {
                /*if (Param.ParamExistByGuid(NTParamsNotSetParamGuid, doc.GetElement(stairSupports.First().Id)) == false)
                { badCats.Add("Лестницы Опоры"); Logger.Log("Отсутствует параметр N_Т Параметры вручную у категории Лестницы Опоры", 1); }
                else {*/
                foreach (var e in stairSupports) ids.Add(e.Id); //}
            }

            List<Autodesk.Revit.DB.Architecture.Railing> railings = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_StairsRailing)   //Ограждения
                                                                         .WhereElementIsNotElementType()
                                                                         .OfClass(typeof(Autodesk.Revit.DB.Architecture.Railing)) //отсеиваем модели в контексте
                                                                         .Cast<Autodesk.Revit.DB.Architecture.Railing>()
                                                                         .ToList();
            if (railings.Count() > 0)
            {
                if (Param.ParamExistByGuid(NTParamsNotSetParamGuid, doc.GetElement(railings.First().Id)) == false)
                { badCats.Add("Ограждения"); Logger.Log("Отсутствует параметр N_Т Параметры вручную у категории Ограждения", 1); }
                else { foreach (var e in railings) ids.Add(e.Id); }
            }
            

            List<FamilyInstance> railings2 = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_StairsRailing)   //Ограждения семействами
                                                                         .WhereElementIsNotElementType()
                                                                         .OfClass(typeof(Autodesk.Revit.DB.FamilyInstance))
                                                                         .Cast<FamilyInstance>()
                                                                         .ToList();
            if (railings2.Count() > 0)
            {
                if (Param.ParamExistByGuid(NTParamsNotSetParamGuid, doc.GetElement(railings2.First().Id)) == false)
                { badCats.Add("Ограждения"); Logger.Log("Отсутствует параметр N_Т Параметры вручную у категории Ограждения", 1); }
                else { foreach (var e in railings2) ids.Add(e.Id); }
            }

            ElementId slabEdgesСategoryId = new ElementId(-2001392);
            ElementCategoryFilter slabEdgesСategoryFilter = new ElementCategoryFilter(slabEdgesСategoryId);
            List<Element> slabEdges = new FilteredElementCollector(doc)
                .WhereElementIsNotElementType()
                .WherePasses(slabEdgesСategoryFilter)
                .Cast<Element>()
                .ToList(); //ребра плит
            if (slabEdges.Count() > 0)
            {
                if (Param.ParamExistByGuid(NTParamsNotSetParamGuid, doc.GetElement(slabEdges.First().Id)) == false)
                { badCats.Add("Ребра плит"); Logger.Log("Отсутствует параметр N_Т Параметры вручную у категории Ребра плит", 1); }
                else { foreach (var e in slabEdges) ids.Add(e.Id); }
            }

            List<FamilyInstance> GMs = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_GenericModel)   //Об мод
                                                                         .WhereElementIsNotElementType()
                                                                         .OfClass(typeof(Autodesk.Revit.DB.FamilyInstance))
                                                                         .Cast<FamilyInstance>()
                                                                         .ToList();
            if (GMs.Count() > 0)
            {
                if (Param.ParamExistByGuid(NTParamsNotSetParamGuid, doc.GetElement(GMs.First().Id)) == false)
                { badCats.Add("Обобщенные модели"); Logger.Log("Отсутствует параметр N_Т Параметры вручную у категории Обобщенные модели", 1); }
                else { foreach (var e in GMs) ids.Add(e.Id); }
            }

            var ceilings = new FilteredElementCollector(doc) //потолки
                .OfCategory(BuiltInCategory.OST_Ceilings)
                .WhereElementIsNotElementType()
                .OfClass(typeof(Ceiling))
                .Cast<Ceiling>()
                .ToList();
            if (ceilings.Count() > 0)
            {
                if (Param.ParamExistByGuid(NTParamsNotSetParamGuid, doc.GetElement(ceilings.First().Id)) == false)
                { badCats.Add("Потолки"); Logger.Log("Отсутствует параметр N_Т Параметры вручную у категории Потолки", 1); }
                else { foreach (var e in ceilings) ids.Add(e.Id); }
            }

            List<FamilyInstance> windows = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_Windows)   //фильтр по категории Окна
                                                                         .WhereElementIsNotElementType()
                                                                         .Cast<FamilyInstance>()
                                                                         .ToList();
            if (windows.Count() > 0)
            {
                if (Param.ParamExistByGuid(NTParamsNotSetParamGuid, doc.GetElement(windows.First().Id)) == false)
                { badCats.Add("Окна"); Logger.Log("Отсутствует параметр N_Т Параметры вручную у категории Окна", 1); }
                else { foreach (var e in windows) ids.Add(e.Id); }
            }

            List<FamilyInstance> doors = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_Doors)   //фильтр по категории Двери
                                                                         .WhereElementIsNotElementType()
                                                                         .Cast<FamilyInstance>()
                                                                         .ToList();
            if (doors.Count() > 0)
            {
                if (Param.ParamExistByGuid(NTParamsNotSetParamGuid, doc.GetElement(doors.First().Id)) == false)
                { badCats.Add("Двери"); Logger.Log("Отсутствует параметр N_Т Параметры вручную у категории Двери", 1); }
                else { foreach (var e in doors) ids.Add(e.Id); }
            }

            List<Element> Santeh = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_PlumbingFixtures)
                    .WhereElementIsNotElementType()
                    .Cast<Element>()
                    .ToList();
            if (Santeh.Count() > 0)
            {
                if (Param.ParamExistByGuid(NTParamsNotSetParamGuid, doc.GetElement(Santeh.First().Id)) == false)
                { badCats.Add("Сантехнические приборы"); Logger.Log("Отсутствует параметр N_Т Параметры вручную у категории Сантехника", 1); }
                else { foreach (var e in Santeh) ids.Add(e.Id); }
            }

            List<Element> nurseCallDevices = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_NurseCallDevices)
                .WhereElementIsNotElementType()
                    .Cast<Element>()
                    .ToList();
            if (nurseCallDevices.Count() > 0)
            {
                if (Param.ParamExistByGuid(NTParamsNotSetParamGuid, doc.GetElement(nurseCallDevices.First().Id)) == false)
                { badCats.Add("Устройства вызова и оповещения"); Logger.Log("Отсутствует параметр N_Т Параметры вручную у категории Устройства вызова и оповещения", 1); }
                else { foreach (var e in nurseCallDevices) ids.Add(e.Id); }
            }

            List<Element> Obor = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_MechanicalEquipment)
                    .WhereElementIsNotElementType()
                    .Cast<Element>()
                    .ToList();
            if (Obor.Count() > 0)
            {
                if (Param.ParamExistByGuid(NTParamsNotSetParamGuid, doc.GetElement(Obor.First().Id)) == false)
                { badCats.Add("Оборудование"); Logger.Log("Отсутствует параметр N_Т Параметры вручную у категории Оборудование", 1); }
                else { foreach (var e in Obor) ids.Add(e.Id); }
            }

            int allcount = ids.Count;
            if(allcount == 0)
            {
                string mes = "Параметр N_Т Параметры вручную не добавлен ни к одной из нужных категорий.";
                new InfoWindow280(mes).ShowDialog();
                Logger.Log(mes + " Завершение работы.", 3);
                return Result.Failed;
            }

            Thread thread = new Thread(new ThreadStart(this.ThreadStartingPoint));
            thread.SetApartmentState(ApartmentState.STA);
            thread.IsBackground = true;
            thread.Start();
            Thread.Sleep(100);

            int PBCount = 0;
            this.TParsOpredSTProgressBar.TNov_ProgressBar.Dispatcher.Invoke<double>((Func<double>)(() => this.TParsOpredSTProgressBar.TNov_ProgressBar.Minimum = (double)PBCount));
            this.TParsOpredSTProgressBar.TNov_ProgressBar.Dispatcher.Invoke<string>((Func<string>)(() => this.TParsOpredSTProgressBar.value.Text = PBCount.ToString()));
            this.TParsOpredSTProgressBar.TNov_ProgressBar.Dispatcher.Invoke<double>((Func<double>)(() => this.TParsOpredSTProgressBar.TNov_ProgressBar.Maximum = allcount));
            this.TParsOpredSTProgressBar.TNov_ProgressBar.Dispatcher.Invoke<string>((Func<string>)(() => this.TParsOpredSTProgressBar.maxvalue.Text = allcount.ToString()));

            //назначение параметров
            using (Transaction transaction = new Transaction(doc))
            {
                transaction.Start("TNov - Т Параметры Определение АР");
                Logger.Log("Открываем транзакцию", 1);

                foreach (var id in ids)
                {
                    PBCount++;
                    this.TParsOpredSTProgressBar.TNov_ProgressBar.Dispatcher.Invoke<double>((Func<double>)(() => this.TParsOpredSTProgressBar.TNov_ProgressBar.Value = (double)PBCount));
                    this.TParsOpredSTProgressBar.TNov_ProgressBar.Dispatcher.Invoke<string>((Func<string>)(() => this.TParsOpredSTProgressBar.value.Text = PBCount.ToString()));

                    Element elem = doc.GetElement(id); Logger.Log("Элемент " + id.IntegerValue.ToString(), 2);
                    string value = "";
                    if (elem.get_Parameter(NTParamsNotSetParamGuid).AsDouble() == 1)
                    {
                        Logger.Log("   пропуск", 2); continue;
                    }

                    if (Param.ParamExistByGuid(TOprParamGuid, elem) && elem.get_Parameter(TOprParamGuid).IsReadOnly == false)
                    {
                        Parameter param = elem.get_Parameter(TOprParamGuid); //Т_Определение

                        string gmValue = GetGMValue(doc, elem); string type = GetTypeName(doc, elem);

                        int catId = elem.Category.Id.IntegerValue;

                        //лестницы и вложенные лестниц
                        if (catId == -2000919 || catId == -2000920 || catId == -2000123 || catId == -2000120)
                        {
                            param.Set("Лестница"); Logger.Log("   назначено Лестница", 2); 
                            continue;
                        }
                        //ограждения
                        if (catId == -2000126)
                        {
                            value = "Ограждение";
                            if (gmValue.Contains("алкон") || gmValue.Contains("Балк") || gmValue.Contains("Лодж") || gmValue.Contains("лодж") ||
                                type.Contains("алкон") || type.Contains("Балк") || type.Contains("Лодж") || type.Contains("лодж")) value = "Фасад";
                            if (elem is FamilyInstance familyInstance1)
                            {
                                FamilySymbol symbol = familyInstance1.Symbol;
                                if (symbol.Family.Name.Contains("Окно")) value = "Фасад";
                            }
                            if (value.Length > 0)
                            {
                                param.Set(value); Logger.Log("   назначено " + value, 2);
                                continue;
                            }
                        }
                        //потолки
                        if (catId == -2000038)
                        {
                            if (gmValue.Contains("Фасад") || type.StartsWith("Фасад") || gmValue.Contains("одшивка")
                                || type.Contains("Композ") || type.Contains("озырек") || type.Contains("озырьк")) 
                                value = "Фасад навесной";
                            else value = "Отделка";
                            if (value.Length > 0)
                            {
                                param.Set(value); Logger.Log("   назначено " + value, 2);
                                continue;
                            }
                        }
                        //перекрытия
                        if(catId == -2000032)
                        {
                            if (gmValue.Contains("Пол")||type.StartsWith("Пол")||elem.Name.StartsWith("Пол")) value = "Отделка";
                            else if (gmValue.Contains("Кровл") || type.StartsWith("Кровл")) value = "Кровля";
                            if (value.Length > 0)
                            {
                                param.Set(value); Logger.Log("   назначено " + value, 2);
                                continue;
                            }
                        }
                        //ребра плит
                        if (catId == -2001392)
                        {
                            param.Set("Элемент фасонный"); Logger.Log("   назначено Элемент фасонный", 2);
                            continue;
                        }
                        //стены
                        if(catId== -2000011&&elem is Wall wall)
                        {
                            if (wall.CurtainGrid != null)//витражи
                            {
                                if (type.Contains("алкон") || gmValue.Contains("алкон")) value = "Витраж холодный";
                                if (type.Contains("олодн") || gmValue.Contains("олодн")) value = "Витраж холодный";
                                if (type.Contains("ермоиз") || gmValue.Contains("ермоиз")||type.Contains("еплый") || gmValue.Contains("еплый")) 
                                    value = "Витраж теплый";
                            }
                            else
                            {
                                if (gmValue.Contains("Фасад"))
                                {
                                    if (type.Contains("Кирп") || type.Contains("кирп") || type.Contains("Пенопл") || type.Contains("пенопл") || type.Contains("Мембр") || type.Contains("блок"))
                                        value = "Стена наружная";
                                    else if (type.Contains("Шт") || type.Contains("шт") || type.Contains("раска")
                                        || type.Contains("ГИ") || type.Contains("идроиз"))
                                        value = "Фасад мокрый";
                                    else value = "Фасад навесной";
                                    if (type.Contains("Хриз") || type.Contains("хриз")) value = "Кровля";
                                }
                                else if (gmValue.Contains("Вент"))
                                {
                                    if (type.Contains("Кирп") || type.Contains("кирп")) value = "Стена наружная";
                                    if (type.Contains("Хриз") || type.Contains("хриз")) value = "Кровля";
                                }
                                else if (gmValue.Contains("Перег")) value = "Стена внутренняя";
                                else if (gmValue.Contains("аруж")) value = "Стена наружная";
                                else if (gmValue.Contains("Отделка")) value = "Отделка";
                                if (type.Contains("ГКЛ") || type.Contains("ГВЛ") || type.Contains("борд")) value = "Отделка";
                            }
                            if (value.Length > 0)
                            {
                                param.Set(value); Logger.Log("   назначено " + value, 2);
                                continue;
                            }
                        }
                        //устройства вызова
                        if(catId== -2008077) 
                        {
                            param.Set("Фасад"); Logger.Log("   назначено Фасад", 2);
                            continue;
                        }
                        //общие правила для оставшихся элементов - по имени семейства и далее
                        if (elem is FamilyInstance familyInstance)
                        {
                            FamilySymbol symbol = familyInstance.Symbol;
                            string family = symbol.Family.Name;
                            if (family.Contains("pmN.Откос кирпичный")) value = "Стена наружная";
                            if (family.Contains("pmN.Пол")) value = "Отделка";
                            if (family.Contains("Лифт")|| family.Contains("Эскалатор")|| family.Contains("одъемник")) value = "Лифт, подъемник, эскалатор";
                            if (family.Contains("Вент")) value = "Блок вентиляционный";
                            if (family.Contains("Люк")) value = "Люк кровельный";
                            if (family.Contains("Аэратор")) value = "Аэратор";
                            if (family.Contains("Лестн")) value = "Лестница";
                            if (family.Contains("Водосток")) value = "Кровля";
                            if (family.Contains("Козырек")|| family.Contains("Корзина")) value = "Фасад";
                            if (family.Contains("Перем")) value = "Перемычка";
                            if (family.Contains("Окно") && family.Contains("Проем") == false)
                            {
                                if (family.Contains("Балк") || family.Contains("балк") || type.Contains("Балк") || type.Contains("балк")) value = "Блок балконный";
                                else if (gmValue.Contains("Отлив")) value = "Отлив";
                                else if (gmValue.Contains("Откос")) value = "Откос";
                                else if (gmValue.Contains("Наличник")) value = "Откос";
                                else
                                {
                                    string naimKvalue = GetTextParamValue(doc, elem, new Guid("f194bf60-b880-4217-b793-1e0c30dda5e9"));//Наим краткое
                                    if (naimKvalue.Contains("Б-П")) value = "Блок балконный";
                                    else value = "Блок оконный";
                                }
                            }
                            if(family.Contains("Проем.Решетка")) value = "Фасад";
                            if (family.Contains("Дверь")&&family.Contains("Проем")==false)
                            {
                                if (catId == -2000014) { }
                                else if (type.Contains("Полотно")) { }
                                else if (type.Contains("Ручка")) { }
                                else if (type.Contains("Откос")) { }
                                else value = "Блок дверной";
                                

                                if (type.Contains("Вход") || type.Contains("вход"))
                                {
                                    elem.get_Parameter(new Guid("7d68b956-732c-4da9-99a8-13be56ccaf94"))?.Set("Входные группы"); //Т_Положение
                                }
                                else elem.get_Parameter(new Guid("7d68b956-732c-4da9-99a8-13be56ccaf94"))?.Set("");

                            }
                            if (family.Contains("Ворота")) value = "Ворота";
                            if (value.Length > 0)
                            {
                                param.Set(value); Logger.Log("   назначено " + value, 2);
                                continue;
                            }
                        }
                        //общие правила для оставшихся элементов - по группе модели и типу
                        if (gmValue.Contains("Фасад") || type.Contains("Фасад")) value = "Фасад";
                        if (gmValue.Contains("аруж") || type.Contains("аруж")) value = "Стена наружная";
                        if (gmValue.Contains("Перег") || type.Contains("Перег")) value = "Стена внутренняя";
                        if (gmValue.Contains("Отделка") || type.Contains("Отделка") ||
                            gmValue.StartsWith("Пол") || type.StartsWith("Пол")
                            || gmValue.StartsWith("Потолок") || type.StartsWith("Потолок")) value = "Отделка";
                        if (gmValue.Contains("Кровля") || type.Contains("Кровля")) value = "Кровля";
                        if (gmValue.Contains("Огражд") || type.Contains("Огражд")) value = "Ограждение";
                        if (gmValue.Contains("Лестн") || type.Contains("Лестн")) value = "Лестница";
                        if (gmValue.Contains("Откос") || type.Contains("Откос")) value = "Откос";

                        param.Set(value); Logger.Log("   назначено " + value, 2);
                        
                    }


                }

                transaction.Commit(); Logger.Log("Закрываем транзакцию", 1);
            }

            this.TParsOpredSTProgressBar.Dispatcher.Invoke((System.Action)(() => this.TParsOpredSTProgressBar.Close()));

            if (badCats.Count > 0)
            {
                badCats.Distinct();
                new InfoWindow280("Параметр N_Т Параметры вручную отсутствует у категорий: " + string.Join(", ", badCats) +
                    ". Эти категории не обработаны.").ShowDialog();
            }
                

            Logger.Log("Завершение работы.", 5);
            return Result.Succeeded;
        }

        String GetGMValue(in Document doc, in Element elem)
        {
            string gmValue = "";
            ElementId typeId = elem.GetTypeId(); if (typeId != null && typeId.IntegerValue != -1)
            {
                Element type = doc.GetElement(typeId);
                if(type.get_Parameter(BuiltInParameter.ALL_MODEL_MODEL).HasValue) gmValue = type.get_Parameter(BuiltInParameter.ALL_MODEL_MODEL).AsString();
            }
            return gmValue;
        }
        String GetTypeName(in Document doc, in Element elem)
        {
            string typeName = "";
            ElementId typeId = elem.GetTypeId(); if (typeId != null && typeId.IntegerValue != -1)
            {
                Element type = doc.GetElement(typeId);
                typeName = type.Name;
            }
            return typeName;
        }
        String GetTextParamValue(in Document doc, in Element elem, in Guid paramGuid)
        {
            string paramValue = "";
            Element elem1 = doc.GetElement(elem.Id);
            if (Param.ParamExistByGuid(paramGuid, elem) == false)
            {
                ElementId typeId = elem.GetTypeId(); if (typeId != null && typeId.IntegerValue != -1)
                {
                    Element type = doc.GetElement(typeId);
                    if (Param.ParamExistByGuid(paramGuid, type)) elem1 = doc.GetElement(type.Id);
                }
            }
            if (Param.ParamExistByGuid(paramGuid, elem1) && elem1.get_Parameter(paramGuid).HasValue)
            {
                paramValue = elem1.get_Parameter(paramGuid).AsString();
            }
            return paramValue;
        }
    }
}
