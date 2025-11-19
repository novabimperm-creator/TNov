using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.Attributes;
using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.UI.Selection;
using Autodesk.Revit.DB.Electrical;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Newtonsoft.Json;
using System.IO;
using System.Collections;
using TNov.main;

namespace TNov
{
    public class cabletraycapcreateViewModel : INotifyPropertyChanged
    {
        private bool _replace = true;
        public bool replace { get => _replace; set { _replace = value; OnPropertyChanged(); } }

        private bool _remove = true;
        public bool remove { get => _remove; set { _remove = value; OnPropertyChanged(); } }

        private string _types = "IEK_ПКЛ,IEK_ОКЛ,EKF_ПКЛ,EKF_ОКЛ,СС_ПКЛ,СС_ОКЛ";
        public string types { get => _types; set { _types = value; OnPropertyChanged(); } }

        private string _filter = "с крышкой";
        public string filter {get => _filter; set{_filter = value; OnPropertyChanged();}}

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
    public class cabletraycapcreate : IExternalCommand
    {
        public class CableTraySelectionFilter : ISelectionFilter
        {
            public bool AllowElement(Element elem)
            {
                if (elem.Category.Name == "Кабельные лотки") { return true; }
                else return false;
            }

            public bool AllowReference(Reference reference, XYZ position)
            {
                return true;
            }
        }
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            string TNovClassName = "Создать крышки"; DateTime dateTime = DateTime.Now;
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
                if (qok != null && qok == true) { Logger.TurnOffExtendedLogs(); } else Logger.Log( "Расширенные логи вкл", 2);
            }

            BuiltInParameter mark = BuiltInParameter.DOOR_NUMBER; //параметр Марка
            BuiltInParameter height = BuiltInParameter.RBS_CABLETRAY_HEIGHT_PARAM; //параметр Высота (лотка)
            BuiltInParameter width = BuiltInParameter.RBS_CABLETRAY_WIDTH_PARAM; //параметр Ширина (лотка)

            //сбор элементов

            Logger.Log("Сбор элементов",1);

            //проверка наличия крышек в проекте
            List<FamilyInstance> GMs = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_GenericModel)   //фильтр по категории Об модели
                                                                         .WhereElementIsNotElementType()
                                                                         .OfClass(typeof(FamilyInstance))
                                                                         .Cast<FamilyInstance>()
                                                                         .ToList();

            List<FamilySymbol> familytypes = new FilteredElementCollector(doc).OfClass(typeof(FamilySymbol))
                .Cast<FamilySymbol>()
                .ToList();
            
            //заполняем списки

            List<FamilyInstance> caps = new List<FamilyInstance>(); //список крышек в проекте            
            foreach(FamilyInstance g in GMs)
            {
                if (g.Symbol.FamilyName.Contains("Крышка")) 
                { 
                    caps.Add(g); 
                }
            }
            List<FamilySymbol> captypes = new List<FamilySymbol>(); //список типов крышек в проекте
            Logger.Log("Типы крышек:",1);
            foreach (FamilySymbol t in familytypes)
            {
                
                if (t.FamilyName.Contains("Крышка на лоток"))
                {
                    captypes.Add(t); Logger.Log("   " + t.Name,1);
                }
            }


            Logger.Log("Проверяем наличие крышек в проекте", 1);
            if (captypes.Count == 0) 
            { 
                new infowindow280("Ошибка! В проекте отсутствуют загруженные семейства крышек.").ShowDialog();
                Logger.Log("Отсутствуют семейства крышек. Завершение работы.", 3);
                return Result.Failed; 
            }

            //анализ текущей выборки
            Logger.Log("Анализ текущей выборки", 1);
            Autodesk.Revit.UI.Selection.Selection selection = commandData.Application.ActiveUIDocument.Selection;
            List<CableTray> CTList = new List<CableTray>();
            CTList = cabletraycapcreate.GetCableTraysFromCurrentSelection(doc, selection); //получаем лотки из текущей выборки
            if (CTList.Count == 0) //запускаем выбор элементов если ничего не выбрано
            {
                CableTraySelectionFilter CTSelectionFilter = new CableTraySelectionFilter();
                IList<Reference> referenceList;
                try
                {
                    referenceList = selection.PickObjects((ObjectType)1, (ISelectionFilter)CTSelectionFilter, "Выберите кабельные лотки");
                }
                catch (Autodesk.Revit.Exceptions.OperationCanceledException ex)
                {
                    Logger.Log("Отменено: " + ex.Message + ". Завершение работы", 3); return Result.Cancelled;
                }
                foreach (Reference reference in (IEnumerable<Reference>)referenceList)
                    CTList.Add(doc.GetElement(reference) as CableTray);
            }

            if (CTList.Count < 1) { Logger.Log("Отсутствуют лотки в выборке. Завершение работы", 3); return Result.Cancelled; }

            //отсеиваем лотки "не специфицировать"

            List<CableTray> CTList1 = new List<CableTray>();

            foreach(CableTray ct in CTList)
            {
                Element elem = doc.GetElement(ct.Id);
                int parval = elem.LookupParameter("N_ЭЛ.Не специфицировать").AsInteger();
                if (parval != 1) { CTList1.Add(ct); }
            }

            Logger.Log("Элементы собраны. Диалоговое окно", 1);
            //Диалог

            var viewModel = new cabletraycapcreateViewModel();
            // Десериализация
            bool forProject = true;
            json js = new json(in TNovClassName, in forProject, out bool canserialize, out string jsonpath);
            if (canserialize)
            {
                viewModel = JsonConvert.DeserializeObject<cabletraycapcreateViewModel>(File.ReadAllText(jsonpath));
                Logger.Log("Десериализация прошла успешно", 1);
            }
            var wpfview = new cabletraycapcreatewpf(viewModel);
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

            string types = viewModel.types; string filter = viewModel.filter; bool replace = viewModel.replace; bool remove = viewModel.remove;
            Logger.Log(types, 1);

            //получаем типовые сочетания в именах типов лотков, удаляем возможные пробелы в начале и конце имен
            string[] types1 = types.Split(','); for (int i = 0; i < types1.Length; i++) { types1[i] = types1[i].Trim(); }

            List<CableTray> CTList2out = new List<CableTray>(); //список лотков на исключение
            List<CableTray> CTListFinal = new List<CableTray>(); //список лотков в работу

            Logger.Log("Исключаем лотки, rоторым не нужны крышки", 1);
            //исключаем лотки, которым не нужны крышки
            foreach (CableTray ct in CTList1)
            {
                Element elem1 = doc.GetElement(ct.Id);
                string type = doc.GetElement(elem1.GetTypeId()).Name;
                bool withcap = false; if (type.Contains(filter)) { withcap = true; }
                if (withcap == false) { CTList2out.Add(ct); }
            }

            //если галочка Пересоздать выключена и в модели есть крышки - проверяем наличие крышек у лотков и исключаем лотки с крышками
            if (replace == false&&caps.Count>0)
            {
                Logger.Log("Исключаем лотки, у которых уже есть крышки", 1);
                foreach (FamilyInstance cap in caps)
                {
                    Element elem = doc.GetElement(cap.Id);
                    int mrkint = 0;
                    string mrkstr = elem.get_Parameter(mark).AsString();
                    if(mrkstr != null) 
                    { 
                        int.TryParse(mrkstr, out mrkint); 
                        if(mrkint != 0)
                        {
                            foreach (CableTray ct in CTList1)
                            {
                                Element elem1 = doc.GetElement(ct.Id);
                                string type = doc.GetElement(elem1.GetTypeId()).Name;
                                bool withcap = false; if (type.Contains(filter)) { withcap=true; }
                                int ctid = elem1.Id.IntegerValue;
                                if(ctid== mrkint||withcap==false) { CTList2out.Add(ct);break; }
                            }
                        }
                    }
                }
            }

            if (CTList2out.Count > 0) //если есть лотки на исключение - исключаем их из финального списка
            {
                foreach (CableTray ct1 in CTList1)
                {
                    bool add = true;
                    foreach (CableTray ct2 in CTList2out)
                    {
                        if (ct2.Id == ct1.Id) { add = false; break; }
                    }
                    if (add) { CTListFinal.Add(ct1); }
                }
            }
            else foreach(CableTray ct in CTList1) { CTListFinal.Add(ct); }

            int count = 0;

            //транзакция
            using (Transaction transaction = new Transaction(doc))
            {
                
                transaction.Start("TNov - Создать крышки");
                Logger.Log("Открываем транзакцию 1 (создание крышек)",1);

                if (remove) 
                {
                    //удаление крышек без марки
                    Logger.Log("Ищем крышки без марки", 1);

                    ICollection<ElementId> capswithoutmark = new List<ElementId>();
                    int cwm = 0;
                    foreach (FamilyInstance cap in caps)
                    {
                        string capmark = cap.get_Parameter(mark).AsString();
                        if (capmark != null) { } else { capswithoutmark.Add(cap.Id); cwm++; }
                    }
                    if (cwm > 0)
                    {
                        doc.Delete(capswithoutmark.ToArray());
                        Logger.Log("Удалено " + cwm.ToString() + " элементов", 2);
                    }
                    else { Logger.Log("крышки без марки отсутствуют", 1); }
                }
                

                //обновленные списки

                List<FamilyInstance> GMs1 = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_GenericModel)   //фильтр по категории Об модели
                                                             .WhereElementIsNotElementType()
                                                             .OfClass(typeof(FamilyInstance))
                                                             .Cast<FamilyInstance>()
                                                             .ToList();
                List<FamilyInstance> caps1 = new List<FamilyInstance>();
                foreach (FamilyInstance g in GMs1)
                {
                    if (g.Symbol.FamilyName.Contains("Крышка"))
                    {
                        caps1.Add(g);
                    }
                }

                //удаляем крышки, которые будут пересозданы
                Logger.Log("Удаляем крышки, которые будут пересозданы", 1);
                ICollection<ElementId> capstoremove = new List<ElementId>();
                int ctr = 0;
                if(replace)
                {
                    foreach (FamilyInstance cap in caps1)
                    {
                        Element elem = doc.GetElement(cap.Id);
                        int mrkint = 0;
                        string mrkstr = elem.get_Parameter(mark).AsString();
                        if (mrkstr != null)
                        {
                            int.TryParse(mrkstr, out mrkint);
                            if (mrkint != 0)
                            {
                                foreach (CableTray ct in CTList1)
                                {
                                    Element elem1 = doc.GetElement(ct.Id);
                                    int ctid = elem1.Id.IntegerValue;
                                    if (ctid == mrkint) { capstoremove.Add(cap.Id); ctr++; break; }
                                }
                            }
                        }
                    }
                    if (capstoremove.Count > 0) 
                    { doc.Delete(capstoremove.ToArray()); Logger.Log("Удалено " + ctr.ToString() + " элементов", 2); }
                    else { Logger.Log("лишние крышки отсутствуют", 1); }
                }

                //создание крышек
                Logger.Log("Создаем крышки для лотков", 1);
                foreach (CableTray ct in CTListFinal)
                {
                    Element element1 = doc.GetElement(ct.Id); 
                    
                    double num2 = element1.get_Parameter(height).AsDouble();
                    double num3 = element1.get_Parameter(width).AsDouble();
                    XYZ endPoint1 = (element1.Location as LocationCurve).Curve.GetEndPoint(0);
                    XYZ endPoint2 = (element1.Location as LocationCurve).Curve.GetEndPoint(1);
                    Element element2 = (Element)null;
                    foreach (Connector connector in ((IEnumerable)((MEPCurve)(element1 as CableTray)).ConnectorManager.Connectors).Cast<Connector>())
                    {
                        foreach (Connector allRef in connector.AllRefs)
                        {
                            if (allRef.Owner is FamilyInstance)
                                element2 = allRef.Owner;
                        }
                    }

                    string ctType = ct.Name; //тип лотка
                    Logger.Log("Лоток " + ct.Id.ToString()+" : тип "+ctType, 2);
                    bool run = false;
                    foreach (string t in types1) 
                    {
                        if (ctType.Contains(t)) { ctType = t; run = true; } //принципиальный тип лотка
                    } 
                    FamilySymbol familySymbol = null;
                    foreach (FamilySymbol captype in captypes)
                    {
                        if (captype.Name.Contains(ctType)) {  familySymbol = captype; break; }
                    }
                    if (run&&familySymbol != null)
                    {
                        Logger.Log("   тип крышки: " + familySymbol.Name, 2);
                        //создание крышки
                        FamilyInstance componentInstance = AdaptiveComponentInstanceUtils.CreateAdaptiveComponentInstance(doc, familySymbol);
                        IList<ElementId> pointElementRefIds = AdaptiveComponentInstanceUtils.GetInstancePlacementPointElementRefIds(componentInstance);
                        ReferencePoint element3 = doc.GetElement(pointElementRefIds.First<ElementId>()) as ReferencePoint;
                        ReferencePoint element4 = doc.GetElement(pointElementRefIds.Last<ElementId>()) as ReferencePoint;
                        if (Math.Round(endPoint1.X, 3) == Math.Round(endPoint2.X, 3) && Math.Round(endPoint1.Y, 3) == Math.Round(endPoint2.Y, 3))
                        {
                            element3.SetCoordinateSystem((element2 as Instance).GetTransform());
                            element3.SetCoordinateSystem((element2 as Instance).GetTransform());
                            element3.Position = endPoint1;
                            element4.Position = endPoint2;
                            ((Element)componentInstance).LookupParameter("Высота лотка").Set(num2);
                            ((Element)componentInstance).LookupParameter("Ширина лотка").Set(num3);
                            ((Location)(((Element)componentInstance).Location as LocationPoint)).Rotate(Line.CreateBound(element3.Position, element4.Position), -1.0 * Math.PI / 2.0);
                        }
                        else
                        {
                            element3.Position = endPoint1;
                            element4.Position = endPoint2;
                            ((Element)componentInstance).LookupParameter("Высота лотка").Set(num2);
                            ((Element)componentInstance).LookupParameter("Ширина лотка").Set(num3);
                        }
                        Element cap = (Element)componentInstance;
                        Parameter elmrk = cap.get_Parameter(mark);
                        elmrk.Set(ct.Id.ToString()); //запись id лотка в параметр Марка у крышки
                        count++;
                    }
                }
                
                transaction.Commit();
                Logger.Log("Закрываем транзакцию 1", 1);

                if (count > 0)
                {
                    if (count == 1) { var info1 = new infowindow280("Успешно!\nКрышка для лотка создана."); info1.ShowDialog(); }
                    else { var info1 = new infowindow280("Успешно!\nСозданы крышки в количестве " + count.ToString() + " шт."); info1.ShowDialog(); }
                }

                Logger.Log("Помещение крышек в набор Лотки", 1);

                bool ss = false;
                string docName = doc.Title.ToString();
                if (docName.Contains("СС") || docName.Contains("-СС") || docName.Contains("_СС")) ss = true;

                bool dws = doc.IsWorkshared;
                if (dws==false&&ss)
                {
                    Logger.Log("Модель НЕ является файлом хранилища. Завершение работы.", 3);
                    string info1txt = "Ошибка!\nТекущий документ не является файлом хранилища. Наборы не созданы.";
                    var info1 = new infowindow280(info1txt); info1.ShowDialog();
                    return Result.Succeeded;
                }

                //обновленные списки

                List<FamilyInstance> GMs2 = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_GenericModel)   //фильтр по категории Об модели
                                                             .WhereElementIsNotElementType()
                                                             .OfClass(typeof(FamilyInstance))
                                                             .Cast<FamilyInstance>()
                                                             .ToList();
                List<FamilyInstance> caps2 = new List<FamilyInstance>();
                foreach (FamilyInstance g in GMs2)
                {
                    if (g.Symbol.FamilyName.Contains("Крышка"))
                    {
                        caps2.Add(g);
                    }
                }

                List<Workset> worksets = new FilteredWorksetCollector(doc)  //рабочие наборы документа
                                         .Cast<Workset>()                   //элементы категории Рабочие наборы
                                         .ToList();                         //формируем список
                transaction.Start("TNov - Крышки в рабочий набор");
                //Назначаем набор крышкам
                Logger.Log("Открываем транзакцию 2. Ищем набор Лотки",1);
                List<Workset> worksetsL = new List<Workset>();
                foreach (Workset ws in worksets) //ищем наличие набора лотков, добавляем его в список РН лотков
                {
                    string wname = ws.Name;
                    if (wname == "Лотки") worksetsL.Add(ws);
                }

                List<int> widsL = new List<int>(); //пустой список номеров РН лотков

                foreach (Workset wsL in worksetsL) //заполняем список номеров РН лотков
                {
                    int widL = wsL.Id.IntegerValue;
                    widsL.Add(widL);
                }

                Logger.Log("Назначаем набор крышкам", 1);
                foreach (var cap in caps2) //назначаем набор осям
                {
                    Element capelement = doc.GetElement(cap.Id);
                    Autodesk.Revit.DB.Parameter param = capelement.get_Parameter(BuiltInParameter.ELEM_PARTITION_PARAM);//получаем параметр "РН"
                    param.Set(widsL[0]); //берем первое значение из списка номеров РН лотков
                    Logger.Log("   Крышка " + cap.Id,2);
                }
                transaction.Commit();
                Logger.Log("Закрываем транзакцию 2", 1);
            }

            Logger.Log("Завершение работы.", 5);

            return Result.Succeeded;
        }
        private static List<CableTray> GetCableTraysFromCurrentSelection(Autodesk.Revit.DB.Document doc, Autodesk.Revit.UI.Selection.Selection sel)
        {
            ICollection<ElementId> elementIds = sel.GetElementIds();
            List<CableTray> currentSelection = new List<CableTray>();
            foreach (ElementId elementId in (IEnumerable<ElementId>)elementIds)
            {
                if (doc.GetElement(elementId) is CableTray && doc.GetElement(elementId).Category != null && doc.GetElement(elementId).Category.Id.IntegerValue.Equals(-2008130))
                    currentSelection.Add(doc.GetElement(elementId) as CableTray);
            }
            return currentSelection;
        }
    }
}
