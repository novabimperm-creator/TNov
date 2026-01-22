using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Electrical;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Controls;
using System.Windows.Media;
using TNov.main;
using Document = Autodesk.Revit.DB.Document;

namespace TNov
{
    
    
    

    
    [Transaction(TransactionMode.Manual)]
    public class TasksMenu : IExternalCommand
    {
        public class GroupSelectionFilter : ISelectionFilter
        {
            public bool AllowElement(Element elem)
            {
                if (elem.Category.Name == "Группы модели") { return true; }
                else return false;
            }

            public bool AllowReference(Reference reference, XYZ position)
            {
                return true;
            }
        }
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            string TNovClassName = "Задание получить"; DateTime dateTime = DateTime.Now;
            //подключение приложения и документа
            if (RevitAPI.UiApplication == null) { RevitAPI.Initialize(commandData); }
            UIDocument uidoc = RevitAPI.UiDocument; Autodesk.Revit.DB.Document doc = RevitAPI.Document;
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

            
            
            

            //сценарий работы
            int scenario = 1; //1 - работа в модели КЖ/АР, 2 - работа в самой модели заданий

            string docName = doc.Title.ToString();
            if (docName.Contains("Задани") || docName.Contains("задани") || docName.Contains("-ЗД") || docName.Contains("_ЗД") || docName.Contains("ЗАДАНИЕ")) scenario = 2;

            //поиск модели задания
            Logger.Log("Ищем модель задания",1);
            List<RevitLinkInstance> links = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_RvtLinks)
            .WhereElementIsNotElementType()
            .Cast<RevitLinkInstance>()
            .ToList();

            List<RevitLinkInstance> taskLinks = new List<RevitLinkInstance>(); //пустой список изменяемых связей

            if (scenario == 1)
            {
                if(links.Count == 0)
                {
                    Logger.Log("Модель задания не загружена. Завершение работы.",3);
                    new infowindow280("Ошибка!\nСвязь задания не вставлена в текущую модель.").ShowDialog();
                    return Result.Failed;
                }

                foreach (var link in links)
                {
                    Logger.Log(link.Name,2); 
                    if (link.Name.Contains("Не общедоступное")) continue;
                    if (link.Name.Contains("Задани") || link.Name.Contains("задани") || link.Name.Contains("-ЗД") || link.Name.Contains("_ЗД") || link.Name.Contains("ЗАДАНИЕ")) taskLinks.Add(link);
                }

                if (taskLinks.Count == 0)
                {
                    Logger.Log("Модель задания не загружена. Завершение работы.", 3);
                    new infowindow280("Ошибка!\nСвязь задания не вставлена в текущую модель, либо не загружена, либо вставлена не по общим координатам.").ShowDialog();
                    return Result.Failed;
                }
            }

            string userName = rvtApp.Username;

            //группы в модели задания

            Document linkDoc = doc; if (scenario == 1) linkDoc = taskLinks[0].GetLinkDocument();

            switch (scenario)
            {
                case 1:

                    bool showFirstWindow = true;

                    while (showFirstWindow)
                    {
                        List<HoleGroup> holeGroups = GetHoleGroups(linkDoc, doc, userName); //новый класс (подкласс 1 внутри)

                        Logger.Log("Стартовое окно", 1);

                        //стартовое окно
                        var wpfview = new TaskListNewWPF(holeGroups);
                        /*
                        var viewModel = new taskViewModel();
                        viewModel.groups = groups1; viewModel.scenario = scenario;
                        gettaskwpf wpfview = new gettaskwpf(viewModel);*/
                        bool? ok = wpfview.ShowDialog();
                        if (ok != null && ok == true) { }
                        else showFirstWindow = false; //{ Logger.Log("Окно закрыто пользователем. Завершение работы.", 5); return Result.Succeeded; }

                        string groupName = wpfview.groupName;

                        if (wpfview.pasted)
                        {
                            copyGroupFromLink(groupName, doc);


                            Logger.Log("Вставлена группа " + groupName + ". Завершение работы.", 5); //return Result.Succeeded;
                        }
                        else
                        {
                            if (groupName == "-")
                            {
                                showFirstWindow = false; //Logger.Log("Окно закрыто пользователем. Завершение работы.", 3); return Result.Succeeded;
                            }
                            else if (wpfview.details)
                            {
                                //подкласс 2
                                List<Hole> holes = TaskTools.HolesInGroup(linkDoc, doc, groupName);

                                var holes1 = holes.OrderBy(h => h.mark.Length)
                                    .ThenBy(h => h.mark)
                                    .ToList();

                                Logger.Log("Открываем окно детального анализа", 2);
                                //окно детального анализа
                                var viewModel1 = new TaskDetailsViewModel(holes1);
                                viewModel1.groupName = groupName; viewModel1.scenario = scenario;
                                TaskDetailsWPF wpfview1 = new TaskDetailsWPF(viewModel1);
                                bool? ok1 = wpfview1.ShowDialog();
                                if (ok1 != null && ok1 == true) { }
                                else showFirstWindow = false;//{ Logger.Log("Окно закрыто пользователем. Завершение работы.", 3); return Result.Succeeded; }

                            }
                        }
                    }

                    //подкласс 1 (для отчета)
                    string groups1 = TaskTools.GetGroupNames(linkDoc, doc);

                    //вывод информации в отчет
                    Logger.Log("Формирование txt-файла для отчета", 1);

                    string dName = doc.Title.ToString().Replace(",", " ");
                    string docNameUserName = "_" + userName; dName = dName.Replace(docNameUserName, "");

                    string taskPath = @"\\fs-nova\Distr\0.For Admin\_TNov\tasks\" + dName + ".txt";
                    File.WriteAllText(taskPath, groups1 + linkDoc.Title.ToString());

                    break;



                case 2:

                    
                    int holeMaxNum = TaskTools.GetHoleMaxNumber(doc);

                    //диалог
                    var qViewModel1 = new qwindow280ViewModel();
                    qViewModel1.headtxt = "Последний взятый номер отверстия: " + holeMaxNum +
                        ". Выберем группу, заполним недостающие номера?";
                    var qwpfview1 = new qwindow280(qViewModel1);
                    qViewModel1.CloseRequest += (s, e) => qwpfview1.Close();
                    bool? qok1 = qwpfview1.ShowDialog();
                    if (qok1 != null && qok1 == true) { } 
                    else
                    {
                        Logger.Log("Отменено. Завершение работы", 3); return Result.Cancelled;
                    }
                    //new infowindow280("Последний взятый номер отверстия: "+holeMaxNum).ShowDialog();

                    //выбираем группу (либо запускаем для уже выбранной)
                    Logger.Log("Анализ текущей выборки", 1);
                    Autodesk.Revit.UI.Selection.Selection selection = commandData.Application.ActiveUIDocument.Selection;
                    List<Group> groupsList = new List<Group>();
                    groupsList = GetGroupsFromCurrentSelection(doc, selection); //получаем лотки из текущей выборки
                    if (groupsList.Count == 0) //запускаем выбор элементов если ничего не выбрано
                    {
                        GroupSelectionFilter CTSelectionFilter = new GroupSelectionFilter();
                        IList<Reference> referenceList;
                        try
                        {
                            referenceList = selection.PickObjects((ObjectType)1, (ISelectionFilter)CTSelectionFilter, "Выберите группу модели");
                        }
                        catch (Autodesk.Revit.Exceptions.OperationCanceledException ex)
                        {
                            Logger.Log("Отменено: " + ex.Message + ". Завершение работы", 3); return Result.Cancelled;
                        }
                        foreach (Reference reference in (IEnumerable<Reference>)referenceList)
                            groupsList.Add(doc.GetElement(reference) as Group);
                    }

                    if (groupsList.Count < 1) { Logger.Log("Отсутствуют группы в выборке. Завершение работы", 3); return Result.Cancelled; }

                    if(holeMaxNum == 0)  Logger.Log("Номера еще не заполнялись, начнем с 1", 1); 

                    //имя и роль пользователя
                    string userDepartment = "-"; 
                    string[] rolesFile = File.ReadAllLines("//fs-nova/Distr/0.For Admin/_TNov/roles.txt");
                    foreach (string role in rolesFile)
                    {
                        if (role.Contains(userName))
                        {
                            string[] line = role.Split(','); userDepartment = line[1]; break;
                        }
                    }

                    List<string> newNums = new List<string>();

                    using (Transaction t1 = new Transaction(doc))
                    {
                        t1.Start("Задания от ИОС. Нумерация отверстий");
                        Logger.Log("Нумерация отверстий", 1);

                        foreach (Group group in groupsList)
                        {
                            Logger.Log("Группа "+group.Name, 1);
                            bool approveDepartment = true;
                            string[] parts = group.Name.Split('_');
                            switch (parts[0]) //вытаскиваем "ВК", "ОВ", "ЭО"/"ЭЛ" или "СС" из имени группы
                            {
                                case "ВК":
                                    if (userDepartment == "OV" || userDepartment == "EL" || userDepartment == "SS") approveDepartment = false;
                                    break;
                                case "ОВ":
                                    if (userDepartment == "VK" || userDepartment == "EL" || userDepartment == "SS") approveDepartment = false;
                                    break;
                                case "ЭО":
                                    if (userDepartment == "OV" || userDepartment == "VK" || userDepartment == "SS") approveDepartment = false;
                                    break;
                                case "ЭЛ":
                                    if (userDepartment == "OV" || userDepartment == "VK" || userDepartment == "SS") approveDepartment = false;
                                    break;
                                case "СС":
                                    if (userDepartment == "OV" || userDepartment == "EL" || userDepartment == "VK") approveDepartment = false;
                                    break;
                            }

                            //если текущий пользователь - ИОС и его отдел не соответствует отделу в имени группы - выводим предупреждение да/нет
                            if (!approveDepartment)
                            {
                                var qViewModel = new qwindow280ViewModel();
                                qViewModel.headtxt = "Группа " + group.Name +
                                    " относится к отверстиям отдела " + parts[0] +
                                    ". Вы уверены, что хотите ее редактировать?";
                                var qwpfview = new qwindow280(qViewModel);
                                qViewModel.CloseRequest += (s, e) => qwpfview.Close();
                                bool? qok = qwpfview.ShowDialog();
                                if (qok != null && qok == true) { } else continue;
                            }

                            //заполняем Марку если пустая - пока только для отверстий
                            ElementFilter elementFilter = (ElementFilter)new ElementParameterFilter(ParameterFilterRuleFactory.CreateContainsRule(new ElementId(-1002002), "pmN.Отверстие", true));
                            IList<ElementId> groupElems = group.GetDependentElements(elementFilter);

                            List<Element> elemsWithoutMark = new List<Element>();
                            foreach (var groupElem in groupElems)
                            {
                                Element elem = doc.GetElement(groupElem);
                                bool hasValue = elem.get_Parameter(BuiltInParameter.ALL_MODEL_MARK).HasValue;
                                if (hasValue && elem.get_Parameter(BuiltInParameter.ALL_MODEL_MARK).AsString().Length == 0) hasValue = false;
                                if (!hasValue) elemsWithoutMark.Add(elem);
                            }

                            foreach (var elem in elemsWithoutMark)
                            {
                                Logger.Log("Отверстие " + elem.Id.ToString(), 2);
                                holeMaxNum++;
                                elem.get_Parameter(BuiltInParameter.ALL_MODEL_MARK).Set(holeMaxNum.ToString());
                                newNums.Add(holeMaxNum.ToString());
                                Logger.Log("   " + holeMaxNum.ToString(), 2);
                            }



                            new infowindow280("Отверстиям без номеров в группе " + group.Name + " назначены позиции: " +
                                String.Join(", ", newNums)).ShowDialog();
                        }


                        t1.Commit();
                        Logger.Log("Нумерация завершена", 1);
                    }

                    List<string> strings = TaskTools.GetGroupsInfo(doc);

                    string date = dateTime.ToString(); date = date.Replace(":", "-");
                    string tasksPath = nova.novaserver + "_TNov/tasks/" + date + "_" + docName + ".txt";

                    foreach (string s in strings)
                    {
                        try
                        {
                            File.AppendAllText(tasksPath, "\n" + s);
                        }
                        catch (Exception) { }
                    }

                    break;
            }


            Logger.Log("Завершение работы.",5);
            
            return Result.Succeeded;
        }
        private static List<Group> GetGroupsFromCurrentSelection(Autodesk.Revit.DB.Document doc, Autodesk.Revit.UI.Selection.Selection sel)
        {
            ICollection<ElementId> elementIds = sel.GetElementIds();
            List<Group> currentSelection = new List<Group>();
            foreach (ElementId elementId in (IEnumerable<ElementId>)elementIds)
            {
                if (doc.GetElement(elementId) is Group)
                    currentSelection.Add(doc.GetElement(elementId) as Group);
            }
            return currentSelection;
        }
        void copyGroupFromLink(string name, Document doc)
        {
            Logger.Log("Копирование группы "+name, 2);

            List<RevitLinkInstance> links = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_RvtLinks)
                                                                         .WhereElementIsNotElementType()
                                                                         .Cast<RevitLinkInstance>()
                                                                         .ToList();

            List<RevitLinkInstance> taskLinks = new List<RevitLinkInstance>(); //пустой список связей заданий

            Logger.Log("Ищем связь задания", 2);

            foreach (var link in links)
            {
                if (link.Name.Contains("Задани") || link.Name.Contains("задани") || link.Name.Contains("-ЗД") || link.Name.Contains("_ЗД") || link.Name.Contains("ЗАДАНИЕ")) taskLinks.Add(link);
            }
            if(taskLinks.Count > 1)
            {
                Logger.Log("Слишком много моделей заданий. Завершение работы.", 3);
                new infowindow280("Ошибка!\nСвязь задания вставлена больше одного раза, либо вставлено несколько разных связей заданий.\nОставьте только одну связь.").ShowDialog();
                
            }
            else if (taskLinks.Count == 1)
            {
                // группы в связанной модели задания

                Document linkDoc = taskLinks[0].GetLinkDocument();
                var transform = taskLinks[0].GetTransform();
                List<Group> linkGroups = new FilteredElementCollector(linkDoc).OfCategory(BuiltInCategory.OST_IOSModelGroups)
                    .WhereElementIsNotElementType()
                    .Cast<Group>()
                    .ToList();
                ICollection<ElementId> ids = new HashSet<ElementId>();

                foreach (var linkGroup in linkGroups)
                {
                    string shortName = linkGroup.Name;
                    string[] nameParts = linkGroup.Name.Split('_');
                    if (nameParts.Length > 2) shortName = nameParts[0] + '_' + nameParts[1] + '_' + nameParts[2]; //учет групп, созданных по старой концепции

                    if (shortName == name)
                    {
                        LocationPoint point = (LocationPoint)linkGroup.Location;
                        ids.Add(linkGroup.Id);
                        Logger.Log("Группа найдена", 2);
                        break;
                    }
                }
                CopyPasteOptions copyOptions = new CopyPasteOptions();
                using (Transaction t = new Transaction(doc))
                {

                    t.Start("Задания от ИОС. Вставка группы");
                    ICollection<ElementId> newElemIds = ElementTransformUtils.CopyElements(linkDoc, ids, doc, transform, copyOptions);
                    t.Commit();
                    Logger.Log("Группа вставлена", 2);
                }
            }


        }
        List<HoleGroup> GetHoleGroups(in Document linkDoc,in Document doc, in string userName)
        {
            //подкласс 1
            string groups1 = TaskTools.GetGroupNames(linkDoc, doc);

            int index = groups1.LastIndexOf('|');
            groups1 = groups1.Remove(index);
            string[] groups = groups1.Split('|');

            List<HoleGroup> holeGroups = new List<HoleGroup>();
            foreach (string group in groups)
            {
                string[] nameParts = group.Split('=');
                string[] shortNameParts = nameParts[0].Split('_');
                string pt2 = ""; if (shortNameParts.Length > 1) pt2 = shortNameParts[1];
                string pt3 = ""; if (shortNameParts.Length > 2) pt3 = shortNameParts[2];
                int order = 0;
                bool buttonVisibility = true; string buttonText = "Детальный анализ";
                string buttonToolTip = "Просмотреть информацию о каждом отверстии в группе";
                string status = nameParts[1];
                if (status.Contains("не вставлялось"))
                {
                    buttonText = "Вставить"; order = 2;
                    buttonToolTip = "Первичная вставка группы с отверстиями в текущую модель";
                }
                if (status.Contains("Марка") || status.Contains("КР"))
                {
                    buttonVisibility = false; order = 1;
                }

                if (status.Contains("Актуально")) order = 3;

                holeGroups.Add(new HoleGroup
                {
                    HoleGroupName = group,
                    HoleGroupNamePart1 = shortNameParts[0],
                    HoleGroupNamePart2 = pt2,
                    HoleGroupNamePart3 = pt3,
                    HoleGroupSet = nameParts[2],
                    HoleGroupStatus = nameParts[1],
                    ButtonText = buttonText,
                    ButtonToolTip = buttonToolTip,
                    IsButtonVisible = buttonVisibility,
                    Order = order,
                });
            }
            holeGroups = holeGroups.OrderBy(h => h.Order).ToList();
            return holeGroups;
        }
    }
}
