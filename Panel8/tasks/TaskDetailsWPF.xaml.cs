using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Collections;
using System.Windows.Navigation;
using System.Security.Cryptography.X509Certificates;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Newtonsoft.Json;
using TNov.main;

namespace TNov
{
    /// <summary>
    /// Логика взаимодействия для TaskDetailsWPF.xaml
    /// </summary>
    public partial class TaskDetailsWPF : Window
    {
        public TaskDetailsWPF(TaskDetailsViewModel viewModel)
        {
            InitializeComponent();
            this.SizeToContent = SizeToContent.Height;
            this.DataContext = viewModel;
            replaceButton.Tag = viewModel.groupName;
            int scenario = viewModel.scenario;

            if (scenario == 2) replaceButton.Visibility = System.Windows.Visibility.Hidden;
            foreach (Hole hole in viewModel.holes)
            { 
                if(hole.status.Contains("КР")) { replaceButton.Visibility = System.Windows.Visibility.Hidden; break; }
            }

            
            StackPanel sp02 = new StackPanel(); sp02.Orientation = Orientation.Horizontal;
            var nameTitle = new TextBlock { Text = "Поз.", Margin = new Thickness(5, 5, 5, 5), Width = 30, }; sp02.Children.Add(nameTitle);
            var widthTitle = new TextBlock { Text = "Размеры", Margin = new Thickness(5, 5, 5, 5), Width = 120, }; sp02.Children.Add(widthTitle);
            var cTitle = new TextBlock { Text = "Рук", Margin = new Thickness(5, 5, 5, 5), Width = 50, }; sp02.Children.Add(cTitle);
            var bTitle = new TextBlock { Text = "BIM", Margin = new Thickness(5, 5, 5, 5), Width = 50, }; sp02.Children.Add(bTitle);
            var sTitle = new TextBlock { Text = "КР", Margin = new Thickness(5, 5, 5, 5), Width = 50, }; sp02.Children.Add(sTitle);
            var xTitle = new TextBlock { Text = "X", Margin = new Thickness(5, 5, 5, 5), Width = 50, }; sp02.Children.Add(xTitle);
            var yTitle = new TextBlock { Text = "Y", Margin = new Thickness(5, 5, 5, 5), Width = 50, }; sp02.Children.Add(yTitle);
            var zTitle = new TextBlock { Text = "Z", Margin = new Thickness(5, 5, 5, 5), Width = 50, }; sp02.Children.Add(zTitle);
            var statusTitle = new TextBlock { Text = "Статус", Margin = new Thickness(5, 5, 5, 5), Width = 150, }; sp02.Children.Add(statusTitle);
            var buttonTitle = new TextBlock { Text = "Действие", Margin = new Thickness(5, 5, 5, 5), Width = 150, }; sp02.Children.Add(buttonTitle);
            sp0.Children.Add(sp02);
            
            foreach(Hole hole in viewModel.holes)
            {
                StackPanel sp = new StackPanel(); sp.Orientation = Orientation.Horizontal; sp.Background = new SolidColorBrush(Colors.MintCream);
                string buttonText = "Показать и обновить";
                var nameBlock = new TextBlock { Text = hole.mark, TextWrapping = TextWrapping.Wrap, Width = 30, Margin = new Thickness(5, 5, 5, 5), }; sp.Children.Add(nameBlock);
                string dims = "";
                if(hole.length > 0) dims = hole.length.ToString()+"х"+hole.width.ToString()+"х"+hole.height.ToString();
                else dims = hole.width.ToString() + "х" + hole.height.ToString();
                var dimsBlock = new TextBlock { Text = dims, TextWrapping = TextWrapping.Wrap, Width = 120, Margin = new Thickness(5, 5, 5, 5), }; sp.Children.Add(dimsBlock);
                var cBlock = new TextBlock { Text = hole.coordStatusHead, TextWrapping = TextWrapping.Wrap, Width = 50, Margin = new Thickness(5, 5, 5, 5), }; sp.Children.Add(cBlock);
                var bBlock = new TextBlock { Text = hole.coordStatusBIM, TextWrapping = TextWrapping.Wrap, Width = 50, Margin = new Thickness(5, 5, 5, 5), }; sp.Children.Add(bBlock);
                var sBlock = new TextBlock { Text = hole.coordStatusST, TextWrapping = TextWrapping.Wrap, Width = 50, Margin = new Thickness(5, 5, 5, 5), }; sp.Children.Add(sBlock);
                var xBlock = new TextBlock { Text = hole.x.ToString(), TextWrapping = TextWrapping.Wrap, Width = 50, Margin = new Thickness(5, 5, 5, 5), }; sp.Children.Add(xBlock);
                var yBlock = new TextBlock { Text = hole.y.ToString(), TextWrapping = TextWrapping.Wrap, Width = 50, Margin = new Thickness(5, 5, 5, 5), }; sp.Children.Add(yBlock);
                var zBlock = new TextBlock { Text = hole.z.ToString(), TextWrapping = TextWrapping.Wrap, Width = 50, Margin = new Thickness(5, 5, 5, 5), }; sp.Children.Add(zBlock);
                var statusBlock = new TextBlock { Text = hole.status, TextWrapping = TextWrapping.Wrap, Width = 150, Margin = new Thickness(5, 5, 5, 5), }; sp.Children.Add(statusBlock);
                string mark = hole.mark;
                bool showButton = true;
                if(hole.status.Contains("КР")) { showButton = false; }
                else if (hole.status.Contains ("Не вставлено.")) { buttonText = "Вставить и показать"; sp.Background = new SolidColorBrush(Colors.Tomato); }
                else if (hole.status.Length == 0) { showButton = false; sp.Background = new SolidColorBrush(Colors.PeachPuff); }
                else if (hole.status.Contains("Лишнее отверстие")) { buttonText = "Показать и удалить"; sp.Background = new SolidColorBrush(Colors.Tomato); mark = hole.id1.ToString(); }
                else sp.Background = new SolidColorBrush(Colors.Tomato);
                if (scenario == 2) showButton = false;
                if(buttonText== "Показать и обновить") mark=hole.mark+"="+ hole.id1.ToString();
                if (showButton)
                {
                    var btn = new Button
                    { Content = buttonText, Width = 150, Height = 25, Margin = new Thickness(5, 5, 5, 5), VerticalAlignment = VerticalAlignment.Center, Tag = mark, };
                    sp.Children.Add(btn);
                    if (buttonText.Contains("ставить")) btn.Click += new RoutedEventHandler(copy_Click);
                    else if(buttonText.Contains("далить")) btn.Click += new RoutedEventHandler(delete_Click);
                    else btn.Click += new RoutedEventHandler(replace_Click);
                }

                sp0.Children.Add(sp);
            }
        }

        private void copy_Click(object sender, RoutedEventArgs e) 
        {
            string TNovClassName = "Задание получить"; DateTime dateTime = DateTime.Now;
            var viewModel0 = new aboutViewModel();
            
            string jsonpath0 = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "TNovClient/TNovSettings.json"); 
            viewModel0 = JsonConvert.DeserializeObject<aboutViewModel>(File.ReadAllText(jsonpath0));
            // создание log - файла
            Logger.Initialize(TNovClassName);
            if (viewModel0.extendedLogs) Logger.Log("Расширенные логи вкл",2);
            Logger.Log("Старт работы", 2);

            Button button = (Button)sender;
            string name = button.Tag.ToString();
            Logger.Log("Марка: "+ name,2);

            BuiltInParameter gm = BuiltInParameter.ALL_MODEL_MODEL; //параметр Группа модели
            BuiltInParameter mrk = BuiltInParameter.ALL_MODEL_MARK; //Марка
            ElementId familyNameParamId = new ElementId(-1002002); //id параметра Имя семейства

            List<RevitLinkInstance> links = new FilteredElementCollector(RevitAPI.Document).OfCategory(BuiltInCategory.OST_RvtLinks)
                                                                        .WhereElementIsNotElementType()
                                                                        .Cast<RevitLinkInstance>()
                                                                        .ToList();

            List<RevitLinkInstance> taskLinks = new List<RevitLinkInstance>(); //пустой список изменяемых связей

            foreach (var link in links)
            {
                if (link.Name.Contains("Задани") || link.Name.Contains("задани") || link.Name.Contains("-ЗД") || link.Name.Contains("_ЗД") || link.Name.Contains("ЗАДАНИЕ")) taskLinks.Add(link);
            }

            if (taskLinks.Count > 1)
            {
                Logger.Log("Слишком много моделей заданий. Завершение работы.", 3);
                new infowindow280("Ошибка!\nСвязь задания вставлена больше одного раза, либо вставлено несколько разных связей заданий.\nОставьте только одну связь.").ShowDialog();

            }
            else if (taskLinks.Count == 1)
            {
                Document linkDoc = taskLinks[0].GetLinkDocument(); var transform = taskLinks[0].GetTransform();
                Document doc = RevitAPI.Document;

                List<Group> linkGroups = new FilteredElementCollector(linkDoc).OfCategory(BuiltInCategory.OST_IOSModelGroups)
                    .WhereElementIsNotElementType()
                    .Cast<Group>()
                    .ToList();

                List<FamilyInstance> GMs = new FilteredElementCollector(linkDoc).OfCategory(BuiltInCategory.OST_GenericModel)   //фильтр по категории Об модели
                                                                         .WhereElementIsNotElementType()
                                                                         .OfClass(typeof(FamilyInstance))
                                                                         .Cast<FamilyInstance>()
                                                                         .ToList();
                List<FamilyInstance> holesGM = new List<FamilyInstance>();

                List<Group> groups = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_IOSModelGroups)
                            .WhereElementIsNotElementType()
                            .Cast<Group>()
                            .ToList();

                foreach (FamilyInstance GM in GMs)
                {
                    string gmvalue = GM.Symbol.get_Parameter(gm).AsString();
                    if (gmvalue != null)
                    {
                        if (gmvalue.Contains("Отверстие")) holesGM.Add(GM);
                        else if (gmvalue.Contains("Рама")) holesGM.Add(GM);
                    }
                }
                ElementId elementId = new ElementId(0);
                foreach (FamilyInstance hole in holesGM)
                {
                    Element elem = (Element)hole;
                    string elem_mark = elem.get_Parameter(mrk).AsValueString();
                    if (elem_mark == name)
                    {
                        elementId = elem.Id; break;
                    }
                }

                if (elementId.IntegerValue != 0)
                {
                    //копируем отверстие в модель
                    ICollection<ElementId> ids = new HashSet<ElementId>();
                    ids.Add(elementId);
                    string gIds = "";
                    CopyPasteOptions copyOptions = new CopyPasteOptions();

                    using (Transaction t = new Transaction(doc))
                    {
                        t.Start("Задания от ИОС. Копирование элемента");
                        ICollection<ElementId> newElemIds = ElementTransformUtils.CopyElements(linkDoc, ids, doc, transform, copyOptions);
                        List<BoundingBoxXYZ> boxes = new List<BoundingBoxXYZ>();
                        

                        foreach (ElementId newElemId in newElemIds)
                        {
                            Element elem1 = doc.GetElement(newElemId);
                            elem1.LookupParameter("Марка")?.Set(name);
                            BoundingBoxXYZ elem1_box = elem1.get_BoundingBox(doc.ActiveView);
                            boxes.Add(elem1_box); 
                        }
                        var bb = boxes.Aggregate((acc, elem2) => acc._BbUnion(elem2));

                        Logger.Log("Идентификаторы: " + gIds, 2);

                        //подрезка вида по отверстию
                        Autodesk.Revit.DB.View3D view3d;

                        List<Autodesk.Revit.DB.View> views = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_Views)   //фильтр по категории Виды
                                                                                        .WhereElementIsNotElementType()    //фильтр только экземпляры
                                                                                        .Cast<Autodesk.Revit.DB.View>()                     //элементы категории Виды
                                                                                        .ToList();                         //формируем список
                        UIDocument uidoc = RevitAPI.UiDocument;
                        UIApplication uiApp = RevitAPI.UiApplication; Autodesk.Revit.ApplicationServices.Application rvtApp = uiApp.Application;
                        string userName = rvtApp.Username;
                        bool dws = doc.IsWorkshared;
                        string viewName = "{3D}";
                        if (dws)
                        {
                            viewName = "{3D - " + userName + "}";

                        }
                        foreach (Autodesk.Revit.DB.View view in views)
                        {
                            if (view.Name == viewName) { uidoc.ActiveView = view; break; }
                        }

                        view3d = (View3D)uidoc.ActiveGraphicalView;
                        view3d.SetSectionBox(bb);
                        t.Commit(); Logger.Log("Вид подрезан", 2);
                    }
                    //выделение группы и отверстия в модели
                    
                        
                    string groupName = "";
                    foreach (var linkGroup in linkGroups) //ищем группу в связанной модели
                    {
                        string[] nameParts = linkGroup.Name.Split('_');
                        string shortName = linkGroup.Name;
                        if (nameParts.Length > 2) shortName = nameParts[0] + '_' + nameParts[1] + '_' + nameParts[2];

                        //элементы группы в связанной модели
                        ElementFilter elementFilter = (ElementFilter)new ElementParameterFilter(ParameterFilterRuleFactory.CreateContainsRule(familyNameParamId, "pmN.Отверстие", true));
                        IList<ElementId> linkGroupElems = linkGroup.GetDependentElements(elementFilter);

                        foreach (ElementId linkGroupElem in linkGroupElems)
                        {
                            Element linkElem = linkDoc.GetElement(linkGroupElem);
                            string linkElem_mark = linkElem.get_Parameter(mrk).AsValueString();
                            if (linkElem_mark == name)
                            {
                                groupName = shortName; Logger.Log("Группа: " + groupName, 2);
                                break;
                            }
                        }

                    }

                    foreach (var group in groups) //ищем группу в текущей модели
                    {
                        string[] nameParts = group.Name.Split('_');
                        string shortName = group.Name;
                        if (nameParts.Length > 2) shortName = nameParts[0] + '_' + nameParts[1] + '_' + nameParts[2];

                        if (shortName == groupName)
                        {
                            gIds += group.Id.ToString() + ",";
                        }

                    }
                    if (gIds.Length > 0)
                    {
                        gIds = gIds.Substring(0, gIds.Length - 1); Logger.Log("Идентификаторы: " + gIds, 2);
                        RevitAPI.UiDocument.Selection.SetElementIds(gIds.Split(',').Select(s => new ElementId(int.Parse(s))).ToArray()); //выделение 
                    }
                    
                    
                    
                    
                }
                
                               
                        


            }
            Logger.Log("Завершение работы.", 5);
            DialogResult = false;
            this.Close(); // закрытие окна

        }
        
        private void delete_Click(object sender, RoutedEventArgs e)
        {
            Button button = (Button)sender;
            string name = button.Tag.ToString();

            int idint = 1;
            bool isnameId = int.TryParse(name, out idint);
            if (isnameId)
            {
                //подрезаем вид по id элемента (idint)
                Document doc = RevitAPI.Document;
                ElementId elemId=new ElementId(idint);
                Element elem = doc.GetElement(elemId);

                Autodesk.Revit.DB.View3D view3d;

                List<Autodesk.Revit.DB.View> views = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_Views)   //фильтр по категории Виды
                                                                             .WhereElementIsNotElementType()    //фильтр только экземпляры
                                                                             .Cast<Autodesk.Revit.DB.View>()                     //элементы категории Виды
                                                                             .ToList();                         //формируем список
                UIDocument uidoc = RevitAPI.UiDocument;
                UIApplication uiApp = RevitAPI.UiApplication; Autodesk.Revit.ApplicationServices.Application rvtApp = uiApp.Application;
                string userName = rvtApp.Username;
                bool dws = doc.IsWorkshared;
                string viewName = "{3D}";
                if (dws)
                {
                    viewName = "{3D - " + userName + "}";

                }
                foreach (Autodesk.Revit.DB.View view in views)
                {
                    if (view.Name == viewName) { uidoc.ActiveView = view; break; }
                }

                BoundingBoxXYZ el_box = elem.get_BoundingBox(doc.ActiveView);
                view3d = (View3D)uidoc.ActiveGraphicalView;
                using (Transaction t = new Transaction(doc))
                {
                    t.Start("Задания от ИОС. Подрезка для удаления");
                    view3d.SetSectionBox(el_box);
                    t.Commit();
                }
                
                
                //выделяем
                RevitAPI.UiDocument.Selection.SetElementIds(name.Split(',').Select(s => new ElementId(int.Parse(s))).ToArray()); //выделение 

            }
            DialogResult = false;
            this.Close(); // закрытие окна

        }

        private void replace_Click(object sender, RoutedEventArgs e)
        {
            string TNovClassName = "Задание получить"; DateTime dateTime = DateTime.Now;
            var viewModel0 = new aboutViewModel();
            
            string jsonpath0 = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "TNovClient/TNovSettings.json"); 
            viewModel0 = JsonConvert.DeserializeObject<aboutViewModel>(File.ReadAllText(jsonpath0));
            // создание log - файла
            Logger.Initialize(TNovClassName);
            if (viewModel0.extendedLogs) Logger.Log("Расширенные логи вкл", 2);
            Logger.Log("Старт работы", 2);

            Button button = (Button)sender;
            string[] names = button.Tag.ToString().Split('=');
            string mark = names[0]; Logger.Log("Марка: " + mark, 2);
            string id0 = names[1]; Logger.Log("Марка: " + id0,2);

            //"плохое" отверстие
            int idint = 1;
            bool isnameId = int.TryParse(id0, out idint);
            Document doc = RevitAPI.Document;
            ElementId elemId = new ElementId(idint);
            Element elem0 = doc.GetElement(elemId);

            //"хорошее" отверстие
            BuiltInParameter gm = BuiltInParameter.ALL_MODEL_MODEL; //параметр Группа модели
            BuiltInParameter mrk = BuiltInParameter.ALL_MODEL_MARK; //Марка
            ElementId familyNameParamId = new ElementId(-1002002); //id параметра Имя семейства

            List<RevitLinkInstance> links = new FilteredElementCollector(RevitAPI.Document).OfCategory(BuiltInCategory.OST_RvtLinks)
                                                                        .WhereElementIsNotElementType()
                                                                        .Cast<RevitLinkInstance>()
                                                                        .ToList();

            List<RevitLinkInstance> taskLinks = new List<RevitLinkInstance>(); //пустой список изменяемых связей

            foreach (var link in links)
            {
                if (link.Name.Contains("Задани") || link.Name.Contains("задани") || link.Name.Contains("-ЗД") || link.Name.Contains("_ЗД") || link.Name.Contains("ЗАДАНИЕ")) taskLinks.Add(link);
            }

            if (taskLinks.Count > 1)
            {
                Logger.Log("Слишком много моделей заданий. Завершение работы.", 3);
                new infowindow280("Ошибка!\nСвязь задания вставлена больше одного раза, либо вставлено несколько разных связей заданий.\nОставьте только одну связь.").ShowDialog();

            }
            else if (taskLinks.Count == 1)
            {
                Document linkDoc = taskLinks[0].GetLinkDocument();
                var transform = taskLinks[0].GetTransform();

                List<Group> linkGroups = new FilteredElementCollector(linkDoc).OfCategory(BuiltInCategory.OST_IOSModelGroups)
                    .WhereElementIsNotElementType()
                    .Cast<Group>()
                    .ToList();

                List<FamilyInstance> GMs = new FilteredElementCollector(linkDoc).OfCategory(BuiltInCategory.OST_GenericModel)   //фильтр по категории Об модели
                                                                         .WhereElementIsNotElementType()
                                                                         .OfClass(typeof(FamilyInstance))
                                                                         .Cast<FamilyInstance>()
                                                                         .ToList();
                List<FamilyInstance> holesGM = new List<FamilyInstance>();

                List<Group> groups = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_IOSModelGroups)
                            .WhereElementIsNotElementType()
                            .Cast<Group>()
                            .ToList();

                foreach (FamilyInstance GM in GMs)
                {
                    string gmvalue = GM.Symbol.get_Parameter(gm).AsString();
                    if (gmvalue != null)
                    {
                        if (gmvalue.Contains("Отверстие")) holesGM.Add(GM);
                        else if (gmvalue.Contains("Рама")) holesGM.Add(GM);
                    }
                }
                ElementId elementId = new ElementId(0);
                foreach (FamilyInstance hole in holesGM)
                {
                    Element elem = (Element)hole;
                    string elem_mark = elem.get_Parameter(mrk).AsValueString();
                    if (elem_mark == mark)
                    {
                        elementId = elem.Id; break;
                    }
                }
                if (elementId.IntegerValue != 0)
                {
                    string gIds = "";
                    //копируем отверстие в модель
                    ICollection<ElementId> ids = new HashSet<ElementId>();
                    ids.Add(elementId);
                    CopyPasteOptions copyOptions = new CopyPasteOptions();
                    using (Transaction t = new Transaction(doc))
                    {
                        t.Start("Задания от ИОС. Копирование, выделение для удаления");
                        ICollection<ElementId> newElemIds = ElementTransformUtils.CopyElements(linkDoc, ids, doc, transform, copyOptions);

                        List<BoundingBoxXYZ> boxes = new List<BoundingBoxXYZ>();
                        

                        foreach (ElementId newElemId in newElemIds)
                        {
                            Element elem1 = doc.GetElement(newElemId);
                            elem1.LookupParameter("Марка")?.Set(mark);
                            BoundingBoxXYZ elem1_box = elem1.get_BoundingBox(doc.ActiveView);
                            boxes.Add(elem1_box); gIds += newElemId.ToString() + ",";
                        }
                        Logger.Log("Элементы: " + gIds, 2);

                        //подрезка вида по отверстиям
                        Autodesk.Revit.DB.View3D view3d;

                        List<Autodesk.Revit.DB.View> views = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_Views)   //фильтр по категории Виды
                                                                                        .WhereElementIsNotElementType()    //фильтр только экземпляры
                                                                                        .Cast<Autodesk.Revit.DB.View>()                     //элементы категории Виды
                                                                                        .ToList();                         //формируем список
                        UIDocument uidoc = RevitAPI.UiDocument;
                        UIApplication uiApp = RevitAPI.UiApplication; Autodesk.Revit.ApplicationServices.Application rvtApp = uiApp.Application;
                        string userName = rvtApp.Username;
                        bool dws = doc.IsWorkshared;
                        string viewName = "{3D}";
                        if (dws)
                        {
                            viewName = "{3D - " + userName + "}";

                        }
                        foreach (Autodesk.Revit.DB.View view in views)
                        {
                            if (view.Name == viewName) { uidoc.ActiveView = view; break; }
                        }

                        bool isActiveView3D = false;
                        if(uidoc.ActiveView.Title.Contains("3D")) isActiveView3D=true;

                        if (isActiveView3D)
                        {

                            BoundingBoxXYZ el0_box = elem0.get_BoundingBox(doc.ActiveView); boxes.Add(el0_box);
                            var bb = boxes.Aggregate((acc, elem2) => acc._BbUnion(elem2));

                            view3d = (View3D)uidoc.ActiveGraphicalView;
                            view3d.SetSectionBox(bb);
                            t.Commit();
                            Logger.Log("Вид подрезан",2);
                        }
                        else
                        {
                            new infowindow280("Плагину не удалось открыть 3D-вид. Откройте 3D-вид самостоятельно и подрежьте его по элементам, которые выделил плагин (рамка выбора).").ShowDialog();
                        }
                    }
                    //выделение отверстий в модели
                    gIds += elemId; Logger.Log("Элементы: " + gIds, 2);
                    RevitAPI.UiDocument.Selection.SetElementIds(gIds.Split(',').Select(s => new ElementId(int.Parse(s))).ToArray()); //выделение 

                    

                }
                        



            }
            Logger.Log("Завершение работы.", 5);
            DialogResult = false;
            this.Close(); // закрытие окна


        }

        private void replacegroup_Click(object sender, RoutedEventArgs e)
        {
            Button button = (Button)sender;
            string name = button.Tag.ToString();

            //MessageBox.Show("Удаляем группу " + name);

            //удаление
            Document doc = RevitAPI.Document;
            List<Group> groups = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_IOSModelGroups)
                .WhereElementIsNotElementType()
                .Cast<Group>()
                .ToList();
            ICollection<ElementId> elemstoremove = new List<ElementId>();
            foreach (var group in groups)
            {
                string[] nameParts1 = group.Name.Split('_');
                string shortName1 = group.Name;
                if (nameParts1.Length > 2) shortName1 = nameParts1[0] + '_' + nameParts1[1] + '_' + nameParts1[2];
                if (shortName1 == name)
                {
                    elemstoremove.Add(group.Id);
                    //break;не надо - удаляем все такие группы!
                }
            }
            using (Transaction t = new Transaction(doc))
            {
                t.Start("Задания от ИОС. Удаление группы");
                if (elemstoremove.Count > 0) doc.Delete(elemstoremove.ToArray());
                t.Commit();
            }

            //MessageBox.Show("Вставляем группу " + name);

            //вставка
            List<RevitLinkInstance> links = new FilteredElementCollector(RevitAPI.Document).OfCategory(BuiltInCategory.OST_RvtLinks)
                                                                         .WhereElementIsNotElementType()
                                                                         .Cast<RevitLinkInstance>()
                                                                         .ToList();

            List<RevitLinkInstance> taskLinks = new List<RevitLinkInstance>(); //пустой список изменяемых связей

            foreach (var link in links)
            {
                if (link.Name.Contains("Задани") || link.Name.Contains("задани") || link.Name.Contains("-ЗД") || link.Name.Contains("_ЗД") || link.Name.Contains("ЗАДАНИЕ")) taskLinks.Add(link);
            }

            if (taskLinks.Count > 1)
            {
                Logger.Log("Слишком много моделей заданий. Завершение работы.", 3);
                new infowindow280("Ошибка!\nСвязь задания вставлена больше одного раза, либо вставлено несколько разных связей заданий.\nОставьте только одну связь.").ShowDialog();

            }
            else if (taskLinks.Count == 1)
            {
                // группы в связанной модели задания

                Document linkDoc = taskLinks[0].GetLinkDocument(); var transform = taskLinks[0].GetTransform();
                List<Group> linkGroups = new FilteredElementCollector(linkDoc).OfCategory(BuiltInCategory.OST_IOSModelGroups)
                    .WhereElementIsNotElementType()
                    .Cast<Group>()
                    .ToList();
                ICollection<ElementId> ids = new HashSet<ElementId>();
                foreach (var linkGroup in linkGroups)
                {
                    string[] nameParts = linkGroup.Name.Split('_');
                    string shortName = linkGroup.Name;
                    if (nameParts.Length > 2) shortName = nameParts[0] + '_' + nameParts[1] + '_' + nameParts[2];
                    if (shortName == name)
                    {
                        ids.Add(linkGroup.Id);
                        break;
                    }
                }
                CopyPasteOptions copyOptions = new CopyPasteOptions();
                using (Transaction t2 = new Transaction(doc))
                {
                    t2.Start("Задания от ИОС. Вставка группы");
                    ElementTransformUtils.CopyElements(linkDoc, ids, doc, transform, copyOptions);
                    t2.Commit();
                }
                DialogResult = false;
                this.Close(); // закрытие окна
            }
        }
        private void backButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            this.Close(); // закрытие окна
        }
        private void cancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            this.Close(); // закрытие окна
        }

        private void Border_MouseDown(object sender, MouseButtonEventArgs e)
        {

        }
    }
}
