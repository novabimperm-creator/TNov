using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Electrical;
using Autodesk.Revit.UI;
using Newtonsoft.Json;
using System.IO;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Autodesk.Revit.UI.Selection;
using TNovCommon;

namespace TNov
{
    
    
        [Transaction(TransactionMode.Manual)]
    public class CableWays : IExternalCommand
    {
        public class AnnoSelectionFilter : ISelectionFilter
        {
            public bool AllowElement(Element elem)
            {
                if (elem.Category.Id.IntegerValue == -2000150) { return true; }
                else return false;
            }

            public bool AllowReference(Reference reference, XYZ position)
            {
                return true;
            }
        }
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            string TNovClassName = "Способы прокладки"; DateTime dateTime = DateTime.Now;
            //подключение приложения и документа
            if (RevitAPI.UiApplication == null) { RevitAPI.Initialize(commandData); }
            UIDocument uidoc = RevitAPI.UiDocument; Autodesk.Revit.DB.Document doc = RevitAPI.Document;
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
                if (qok != null && qok == true) { Logger.TurnOffExtendedLogs(); } else Logger.Log( "Расширенные логи вкл", 2);
            }

            //параметры
            string pipeWayParam1 = "К1_Длина_В 1-ой трубе"; string pipeWayParam2 = "К1_Длина_В 2-ой трубе"; string pipeWayParam3 = "К1_Длина_В 3-ой трубе";
            string pipeTypeParam1 = "Т1_Тип"; string pipeTypeParam2 = "Т2_Тип"; string pipeTypeParam3 = "Т3_Тип";

            //сбор элементов 
            List<AnnotationSymbol> annoList = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_GenericAnnotation)
                                                             .WhereElementIsNotElementType()
                                                             .Cast<AnnotationSymbol>()
                                                             .Where(a => a.Name.Contains( "TSL_2D автоматический выключатель_ВРУ"))
                                                             .ToList();

            Logger.Log("Диалоговое окно", 1);
            //Диалог
            var viewModel1 = new CableWaysStartViewModel();
            // Десериализация
            string serializeClassName = TNovClassName + ".Запуск";
            bool forProject = true;
            json js1 = new json(in serializeClassName, in forProject, out bool canserialize1, out string jsonpath1);
            if (canserialize1)
            {
                viewModel1 = JsonConvert.DeserializeObject<CableWaysStartViewModel>(File.ReadAllText(jsonpath1));
                Logger.Log("Десериализация прошла успешно", 1);
            }
            var wpfview1 = new CableWaysStartWPF(viewModel1);
            viewModel1.CloseRequest += (s, e) => wpfview1.Close();
            bool? ok1 = wpfview1.ShowDialog();
            if (ok1 != null && ok1 == true) { }
            else { Logger.Log("Запуск отменен пользователем. Завершение работы.", 3); return Result.Cancelled; }
            //Сериализация
            try
            {
                File.WriteAllText(jsonpath1, JsonConvert.SerializeObject(viewModel1));
                Logger.Log("Сериализация прошла успешно", 1);
            }
            catch (Exception ex) { Logger.Log("Ошибка при сериализации: " + ex.Message,4); }

            bool all = viewModel1.all; bool active = viewModel1.visible;

            //анализ текущей выборки
            Autodesk.Revit.UI.Selection.Selection selection = commandData.Application.ActiveUIDocument.Selection;
            List<AnnotationSymbol> annoList1 = new List<AnnotationSymbol>();

            if (active)
            {
                Logger.Log("Анализ текущей выборки",1);
                annoList1 = CableWays.GetAnnoFromCurrentSelection(doc, selection); //получаем лотки из текущей выборки
                if (annoList1.Count == 0) //запускаем выбор элементов если ничего не выбрано
                {
                    AnnoSelectionFilter CTSelectionFilter = new AnnoSelectionFilter();
                    IList<Reference> referenceList;
                    try
                    {
                        referenceList = selection.PickObjects((ObjectType)1, (ISelectionFilter)CTSelectionFilter, "Выберите автоматы");
                    }
                    catch (Autodesk.Revit.Exceptions.OperationCanceledException ex)
                    {
                        Logger.Log("Отменено: " + ex.Message+". Завершение работы.",3); return Result.Cancelled;
                    }
                    foreach (Reference reference in (IEnumerable<Reference>)referenceList)
                        annoList1.Add(doc.GetElement(reference) as AnnotationSymbol);
                }
                if(annoList1==null|| annoList1.Count<1)
                {
                    Logger.Log("Выборка пуста. Завершение работы.", 3); return Result.Cancelled;
                }
            }
            
            if (all) { foreach (var a in annoList) annoList1.Add(a); }

            //Вьюмодель (без открытия окна)

            var viewModel = new CableWaysViewModel();
            // Десериализация
            //bool forProject = true;
            json js = new json(in TNovClassName, in forProject, out bool canserialize, out string jsonpath);
            if (canserialize)
            {
                viewModel = JsonConvert.DeserializeObject<CableWaysViewModel>(File.ReadAllText(jsonpath));
                Logger.Log("Десериализация прошла успешно",1);
            }
            
            //Сериализация
            try
            {
                File.WriteAllText(jsonpath, JsonConvert.SerializeObject(viewModel));
                Logger.Log("Сериализация прошла успешно",1);
            }
            catch (Exception ex) { Logger.Log("Ошибка при сериализации: " + ex.Message, 4); }

            //списки из параметров vM
            string[] pipeWays = new string[]
            {
viewModel.pipeWay1, viewModel.pipeWay2, viewModel.pipeWay3, viewModel.pipeWay4, viewModel.pipeWay5
//"гофр. ПВХ", "гофр. ПНД(т)", "МР(г)", "гофр. ПА", "ст."
            };
            string[] pipeTypes = new string[]
            {
viewModel.pipeType1, viewModel.pipeType2, viewModel.pipeType3, viewModel.pipeType4, viewModel.pipeType5
//"IEK | Труба гофрированная из ПВХ", "IEK | Труба гофрированная из ПНД (тяжелая)", "DKC | Металлорукав в герметичной ПВХ-оболочке", 
               // "DKC | Труба индустриальная гофрированная из не распространяющего горение полиамида (серия F0)",
               //"Труба стальная электросварная (толщина стенки 1.5 мм)"
            };
            string[] simpleTypePars = new string[]
            {
viewModel.sTypePar1, viewModel.sTypePar2, viewModel.sTypePar3, viewModel.sTypePar4, viewModel.sTypePar5
//"Настройки_Кабели_Способ прокладки 1", "Настройки_Кабели_Способ прокладки 2", "Настройки_Кабели_Способ прокладки 3", "Настройки_Кабели_Способ прокладки 4",
//"Настройки_Кабели_Способ прокладки 5"
            };
            string[] simplePars = new string[]
            {
viewModel.sPar1, viewModel.sPar2, viewModel.sPar3, viewModel.sPar4, viewModel.sPar5
//"К1_Длина_Способ 1", "К1_Длина_Способ 2", "К1_Длина_Способ 3", "К1_Длина_Способ 4", "К1_Длина_Способ 5"
            };

            List<string> badElementIds = new List<string>();

            using (Transaction t = new Transaction(doc))
            {
                t.Start("TNov - Способы прокладки");

                Logger.Log("Открываем транзакцию",1);

                foreach (AnnotationSymbol anno in annoList1) 
                {
                    Element elem = doc.GetElement(anno.Id);
                    //исходная строка
                    string baseString = elem.LookupParameter("Способ прокладки").AsString();
                    if(baseString==null||baseString.Length==0) 
                    {
                        string value = "пустой номер цепи (id элемента "+anno.Id.ToString()+")";
                        Parameter number = elem.LookupParameter("Номер цепи");
                        if (number != null)
                        {
                            string numberValue = number.AsString();
                            if(numberValue!=null&&numberValue.Length>0) value = numberValue;
                        }
                        badElementIds.Add(value); continue; 
                    }
                    if (baseString.Contains("лоток")) baseString = baseString.Replace("лоток", "в лотке");
                    Logger.Log("Элемент " + anno.Id.ToString()+" "+baseString,2);
                    string[] strParts = baseString.Split(';');
                    int count2 = 0;
                    List<string> simpleTypeParsUsed = new List<string>();
                    foreach (var part in strParts)
                    {

                        string param="";
                        double doubleValue = 0;
                        string[] parts = part.Split('-');
                        bool pipeWay = false;

                        foreach (var way in pipeWays) //список pipeWays получаем из viewModel
                        {
                            if (parts[0].Contains(way))
                            {
                                if (parts[0].Contains("пуст")) { } else { count2++; pipeWay = true; } //обработка случая "ст"/"пуст"
                            }
                            
                        }

                        if (pipeWay) //в трубе
                        {
                            string paramPipe="";
                            switch (count2)
                            {
                                case 1:
                                    param = pipeWayParam1;
                                    paramPipe = pipeTypeParam1;
                                    break;
                                case 2:
                                    param = pipeWayParam2;
                                    paramPipe = pipeTypeParam2;
                                    break;
                                case 3:
                                    param = pipeWayParam3;
                                    paramPipe = pipeTypeParam3;
                                    break;
                            }
                            if (param != "")
                            { //назначаем К1_Длина_
                                Double.TryParse(parts[1], out doubleValue);
                                Parameter p = elem.LookupParameter(param);
                                if (p != null)
                                {
                                    p.Set(doubleValue);
                                    Logger.Log("Параметр " + param+ ": "+ doubleValue.ToString(), 2);
                                }
                                //назначаем Т_Тип
                                for (int i = 0; i < pipeWays.Length; i++)
                                {
                                    if (parts[0].Contains(pipeWays[i]))
                                    {
                                        Parameter par = elem.LookupParameter(paramPipe);
                                        if (par != null) 
                                        { 
                                            par.Set(pipeTypes[i]);
                                            Logger.Log("Параметр " + paramPipe + ": " + pipeTypes[i], 2);
                                        }
                                    }
                                }
                            }
                            
                        }
                        else //прочие способы
                        {
                            //имя целевого параметра зависит от значения parts[0] - спорное решение, но окей
                            ElementId typeId = elem.get_Parameter(BuiltInParameter.ELEM_FAMILY_PARAM).AsElementId();
                            Element type = doc.GetElement(typeId);
                            for (int i = 0; i < simpleTypePars.Length; i++)
                            {
                                Parameter typeP = type.LookupParameter(simpleTypePars[i]);
                                if (typeP != null)
                                {
                                    string typePvalue = typeP.AsString();
                                    if (parts[0].Contains(typePvalue)) param = simplePars[i];
                                }
                            }
                            Double.TryParse(parts[1], out doubleValue);
                            Parameter par = elem.LookupParameter(param);
                            if (par != null)
                            {
                                par.Set(doubleValue);
                                Logger.Log("Параметр " + param + ": " + doubleValue.ToString(),2);
                            }
                            simpleTypeParsUsed.Add(param);
                        }


                    }
                    //очистка неиспользуемых параметров
                    Logger.Log("Очистка неиспользуемых параметров",2);
                    switch (count2) //трубы
                    {
                        case 0:
                            Logger.Log("чистим параметры для всех труб", 2);
                            try
                            {
                                elem.LookupParameter(pipeWayParam1).Set(0);
                                elem.LookupParameter(pipeTypeParam1).Set("");
                                elem.LookupParameter(pipeWayParam2).Set(0);
                                elem.LookupParameter(pipeTypeParam2).Set("");
                                elem.LookupParameter(pipeWayParam3).Set(0);
                                elem.LookupParameter(pipeTypeParam3).Set("");
                            }
                            catch (Exception ex) { Logger.Log(elem.Id.ToString() + " ошибка: " + ex.Message,4); }
                            break;
                        case 1:
                            Logger.Log("чистим параметры для 2 и 3 трубы", 2);
                            try
                            {
                                elem.LookupParameter(pipeWayParam2).Set(0);
                                elem.LookupParameter(pipeTypeParam2).Set("");
                                elem.LookupParameter(pipeWayParam3).Set(0);
                                elem.LookupParameter(pipeTypeParam3).Set("");
                            }
                            catch (Exception ex) { Logger.Log(elem.Id.ToString() + " ошибка: " + ex.Message,4); }
                            break;
                        case 2:
                            Logger.Log("чистим параметр для 3-й трубы", 2);
                            try
                            {
                                elem.LookupParameter(pipeWayParam3).Set(0);
                                elem.LookupParameter(pipeTypeParam3).Set("");
                            }
                            catch(Exception ex) {Logger.Log(elem.Id.ToString()+" ошибка: "+ex.Message,4); }
                            break;
                    }
                    //прочие параметры
                    List<string>simpleParsList = simplePars.ToList();
                    List<string> notUsedSimplePars = simpleParsList.Except(simpleTypeParsUsed).ToList();
                    foreach(string notUsedSimplePar in notUsedSimplePars)
                    {
                        elem.LookupParameter(notUsedSimplePar).Set(0);
                        Logger.Log("параметр "+notUsedSimplePar+ " очищен", 2);
                    }
                    
                }

                Logger.Log("Закрываем транзакцию",1);
                t.Commit();
            }

            if (badElementIds.Count > 0)
            { 
                
                Logger.Log("Номера цепей автоматов с пустым исходным параметром: " + String.Join(",", badElementIds),1);
                // Диалоговое окно
                var viewModel2 = new InfoWindowTextFieldViewModel();
                viewModel2.headtxt = "Номера цепей проблемных автоматов:";
                viewModel2.ids = String.Join(",", badElementIds);
                viewModel2.lowtxt = "Эти автоматы имеют пустой исходный параметр.";
                var wpfview2 = new InfoWindowTextField(viewModel2);
                viewModel2.CloseRequest += (s, e) => wpfview2.Close();
                bool? ok2 = wpfview2.ShowDialog();
            }
            Logger.Log("Завершение работы", 5);
            return Result.Succeeded;
        }
        
        private static List<AnnotationSymbol> GetAnnoFromCurrentSelection(Autodesk.Revit.DB.Document doc, Autodesk.Revit.UI.Selection.Selection sel)
        {
            ICollection<ElementId> elementIds = sel.GetElementIds();
            List<AnnotationSymbol> currentSelection = new List<AnnotationSymbol>();
            foreach (ElementId elementId in (IEnumerable<ElementId>)elementIds)
            {
                if (doc.GetElement(elementId) is AnnotationSymbol && doc.GetElement(elementId).Category != null && doc.GetElement(elementId).Category.Id.IntegerValue.Equals(-2000150)&& doc.GetElement(elementId).Name.Contains("TSL_2D автоматический выключатель_ВРУ"))
                    currentSelection.Add(doc.GetElement(elementId) as AnnotationSymbol);
            }
            return currentSelection;
        }
    }
}
