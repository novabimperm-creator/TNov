using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.UI;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using TNov.main;

namespace TNov
{
    
    public class sheetChange
    {
        public int number;
        public string description;
        public int cloudcount;
    }

    public class changesViewModel : INotifyPropertyChanged
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
        private bool _purge = false;
        public bool purge
        {
            get => _purge; set { _purge = value; OnPropertyChanged(); }
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
    public class changes : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            string TNovClassName = "Изменения"; DateTime dateTime = DateTime.Now;
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

            // Проверка актуальности шаблона
            templatecheck tc = new templatecheck(in commandData, out bool oldProject);

            Logger.Log("Сбор элементов", 1);
            //получаем элементы

            List<RevisionCloud> clouds = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_RevisionClouds)
            .WhereElementIsNotElementType()
            .Cast<RevisionCloud>()
            .ToList();

            List<ViewSheet> sheets = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_Sheets)
            .WhereElementIsNotElementType()
            .Cast<ViewSheet>()
            .ToList();

            List<Revision> revs = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_Revisions)
            .WhereElementIsNotElementType()
            .Cast<Revision>()
            .ToList();

            //параметры
            string sheetComm = "A_Примечание"; if (oldProject) sheetComm = "ADSK_Примечание";
            
            Logger.Log("Диалоговое окно",1);
            //Диалог
            var viewModel = new changesViewModel();
            // Десериализация
            bool forProject = true;
            json js = new json(in TNovClassName, in forProject, out bool canserialize, out string jsonpath);
            if (canserialize)
            {
                viewModel = JsonConvert.DeserializeObject<changesViewModel>(File.ReadAllText(jsonpath));
                Logger.Log("Десериализация прошла успешно",1);
            }
            var wpfview = new changeswpf(viewModel);
            viewModel.CloseRequest += (s, e) => wpfview.Close();
            bool? ok = wpfview.ShowDialog();
            if (ok != null && ok == true) { } else { Logger.Log("Запуск отменен пользователем. Завершение работы.", 3); return Result.Cancelled; }
            //Сериализация
            try
            {
                File.WriteAllText(jsonpath, JsonConvert.SerializeObject(viewModel));
                Logger.Log("Сериализация прошла успешно",1);
            }
            catch (Exception ex) { Logger.Log("Ошибка при сериализации: " + ex.Message,4); }

            Logger.Log("Проверяем, является ли открытый вид листом", 2);

            View v = doc.ActiveView;
            ElementId sheetCatId = new ElementId(-2003100);
            bool sheetView = v.Category.Id == sheetCatId;

            using (Transaction t = new Transaction(doc))
            {
                if (clouds.Count>0)
                {
                
                    if (viewModel.visible) Logger.Log("Сценарий: активный лист",1);
                    else Logger.Log("Сценарий: все листы",1);
                    if (viewModel.visible&&sheetView==false)
                    {
                        new infowindow280("Ошибка! В настоящий момент активным окном является не лист.\n" +
                            "Если все же является - щелкните мышью 1 раз в пространстве листа.").ShowDialog();
                        Logger.Log("Текущий вид не является листом. Завершение работы.", 3);
                        return Result.Cancelled;
                    }
                    ElementId activeViewId = v.Id;

                    //обработка элементов
                    t.Start("TNov - Пометочные облака");
                    Logger.Log("Открываем транзакцию 1 (пометочные облака)",1);

                    var сloudsSortedBySheet = from cloud in clouds //сортированный список облаков по листам
                                                orderby cloud.OwnerViewId.ToString()
                                                select cloud;

                    var sheetsWithClouds = from cloud in сloudsSortedBySheet //список листов
                                           group cloud by cloud.OwnerViewId.ToString();

                    foreach (var sheet in sheetsWithClouds) 
                    {
                        var firstCloud = sheet.First();
                        string firstCloudSheetId = firstCloud.OwnerViewId.ToString();
                        if(viewModel.visible&&v.Id.ToString()!= firstCloudSheetId) continue; //обработка сценария "текущий лист"

                        //Нумерация облаков

                        var sCloudsSortedByRevision = from sCloud in sheet //сортированный список облаков по изменению
                                                      orderby sCloud.get_Parameter(BuiltInParameter.REVISION_CLOUD_REVISION_NUM).AsString()
                                                      select sCloud;

                        var revisions = from sCloud in sCloudsSortedByRevision //список изменений
                                        group sCloud by sCloud.get_Parameter(BuiltInParameter.REVISION_CLOUD_REVISION_NUM).AsString();

                        int j = 0;
                        foreach (var revision in revisions)
                        {
                            j++;
                            int i = 0;
                            foreach (var rCloud in revision)
                            {
                                if (j == 1)
                                {
                                    ElementId sheetId = rCloud.OwnerViewId;
                                    Element cloudSheet = doc.GetElement(sheetId);
                                    Logger.Log("Лист " + cloudSheet.Name,2);
                                }
                                i++;
                                if (i == 1)
                                {
                                    string revisionName = rCloud.get_Parameter(BuiltInParameter.REVISION_CLOUD_REVISION_NUM).AsString();
                                    Logger.Log("   Изменение " + revisionName, 2);
                                }
                                List<Curve> curves = rCloud.GetSketchCurves().ToList();
                                double lengthSum = 0;
                                foreach (var curve in curves) lengthSum += curve.Length;
                                Parameter comm = rCloud.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS);
                                if (lengthSum>0.03) 
                                { 
                                    comm.Set(i.ToString());
                                    Logger.Log("      Облако " + rCloud.Id.ToString() + ": назначен номер " + i.ToString(),2); 
                                }
                                else Logger.Log("      Облако " + rCloud.Id.ToString() + ": номер не назначен, облако малой длины", 2);
                            }
                            
                        }

                        

                        

                    }

                    t.Commit(); Logger.Log("Закрываем транзакцию 1",1);



                }
            }
            
            
            //Заполнение параметров листов

            using (Transaction t2 = new Transaction(doc))
            {
                t2.Start("TNov - заполнение параметров листов"); Logger.Log("Открываем транзакцию 2 (параметры листов)",1);

                foreach (ViewSheet viewSheet in sheets) 
                {
                    if (viewModel.visible && v.Id.ToString() != viewSheet.Id.ToString()) continue; //обработка сценария "текущий лист"

                    string sheetComments = viewSheet.LookupParameter(sheetComm).AsString();
                    if (!string.IsNullOrEmpty(sheetComments))
                    {
                        if (sheetComments.Contains("Аннул") || sheetComments.Contains("аннул")) continue; //пропуск аннулированных листов
                    }

                    Logger.Log("Лист "+viewSheet.Name, 1);

                    Logger.Log("Сбор данных для листа",2);
                    var revisionsOnSheet = new List<Revision>();
                    //получаем id изменений на листе
                    IList<ElementId> revisionIds = viewSheet.GetAllRevisionIds(); //получение изменений на листе
                    foreach (ElementId revisionId in revisionIds)
                    {
                        Revision rev = (Revision)doc.GetElement(revisionId);
                        Logger.Log("   "+rev.RevisionNumber+" "+rev.Description, 2);
                        Revision revision = (Revision)(doc.GetElement(revisionId));
                        revisionsOnSheet.Add(revision);
                    }
                    //получаем облака на листе
                    List<RevisionCloud> cloudsOnSheet = new List<RevisionCloud>();
                    Logger.Log("лист " + viewSheet.Id.ToString(),2);
                    foreach (var cloud in clouds)
                    {
                        //id листа
                        string revListId = cloud.OwnerViewId.ToString();
                        Logger.Log("   " + revListId, 2);
                        //проверка соответствия
                        if (revListId == viewSheet.Id.ToString()) 
                        { cloudsOnSheet.Add(cloud); Logger.Log("      добавлено", 2); }
                    }
                    Logger.Log("Заполнение списка элементов класса sheetChange", 2);
                    List<sheetChange> sheetChanges = new List<sheetChange>();
                    foreach (var revision in revisionsOnSheet)
                    {
                        Logger.Log("изм " + revision.RevisionNumber, 2);
                        int cloudsOfRevision = 0;
                        foreach (var cloud in cloudsOnSheet)
                        {
                            //проверка соответствия облака  изменению
                            string revNum = cloud.get_Parameter(BuiltInParameter.REVISION_CLOUD_REVISION_NUM).AsString();
                            Logger.Log("облако " + revNum,2);
                            //проверка длины облака 
                            List<Curve> curves = cloud.GetSketchCurves().ToList();
                            double lengthSum = 0;
                            foreach (var curve in curves) lengthSum += curve.Length;
                            if (revNum ==revision.RevisionNumber&& lengthSum>0.03) cloudsOfRevision++;
                        }
                        sheetChange sChange = new sheetChange();
                        sChange.cloudcount = cloudsOfRevision; Logger.Log("кол-во облаков " + cloudsOfRevision.ToString(), 2);
                        sChange.description = revision.Description;
                        sChange.number = int.Parse(revision.RevisionNumber);
                        sheetChanges.Add(sChange);
                    }

                    var sheetChangesSorted = sheetChanges.OrderBy(s => s.number).ToList();

                    List<sheetChange> sheetChangesFinal=new List<sheetChange>();

                    //логика если галочка

                    if (viewModel.purge)
                    {
                        Logger.Log("Оставляем только последнее заменяющее изменение и последующие", 2);
                        sheetChangesSorted.Reverse();
                        int replaces = 0;
                        foreach (var sChange in sheetChangesSorted)
                        {
                            if(replaces==0) sheetChangesFinal.Add(sChange);
                            if (sChange.cloudcount == 0) replaces++;
                        }
                        sheetChangesFinal.Reverse();
                         
                        {
                            foreach (var sChange in sheetChangesFinal) 
                                Logger.Log("   "+sChange.number+" " + sChange.description + " " + sChange.cloudcount,2);
                        }
                        
                    }
                    else
                    {
                        foreach (var sChange in sheetChangesSorted)
                        {
                            sheetChangesFinal.Add(sChange);
                        }
                        
                        {
                            foreach (var sChange in sheetChangesFinal)
                                Logger.Log("   " + sChange.number + " " + sChange.description + " " + sChange.cloudcount, 2);
                        }
                    }

                    Logger.Log("Заполняем переменные", 2);
                    //переменные для заполнения значений
                    string commValue ="";
                    List<string> changesStrK = new List<string>();
                    List<string> changesStrL = new List<string>();

                    //заполнение переменных
                    foreach (var sC in sheetChangesFinal)
                    {
                        if (sC.cloudcount == 0)
                        {
                            commValue += sC.description +" (Зам.), ";
                            changesStrK.Add("-");
                            changesStrL.Add("Зам.");
                        }
                        else
                        {
                            commValue += sC.description +", ";
                            changesStrK.Add(sC.cloudcount.ToString());
                            changesStrL.Add("-");
                        }
                    }
                    if (commValue.Length > 0)
                    {
                        commValue = "Изм. " + commValue;
                        commValue = commValue.Substring(0, commValue.Length - 2);
                    }

                    Logger.Log("Заполняем параметры", 2);
                    //Примечание
                    if (commValue.Length > 0) 
                    {
                        try
                        {
                            viewSheet.LookupParameter(sheetComm).Set(commValue);
                            Logger.Log("   Примечание: " + commValue,2);
                        }
                        catch (Exception ex) { Logger.Log(viewSheet.Name + " ошибка заполнения Примечания: " + ex.Message,4); }
                    }
                    
                    
                    //параметры штампа
                    string paramStringPrefix = "N_Изм.Строка"; if (oldProject) paramStringPrefix = "Изм.Строка";
                    for (int i = 1; i < 15; i++)
                    {
                        string paramK = paramStringPrefix + i.ToString() + ".Кол.уч"; 
                        string paramL = paramStringPrefix + i.ToString() + ".Лист"; 
                        if (i < changesStrK.Count + 1)
                        {
                            //запись значений из списков
                            try
                            {
                                viewSheet.LookupParameter(paramK).Set(changesStrK[i - 1]);
                                Logger.Log("      " + paramK+": "+ changesStrK[i - 1], 2);
                                viewSheet.LookupParameter(paramL).Set(changesStrL[i - 1]);
                                Logger.Log("      " + paramL + ": " + changesStrL[i - 1],2);
                            }
                            catch (Exception ex) { Logger.Log(viewSheet.Name + " ошибка заполнения штампа: " + ex.Message,4); }
                        }
                        else
                        {
                            //запись пустых значений
                            try
                            {
                                viewSheet.LookupParameter(paramK).Set("");
                                Logger.Log("      " + paramK + ": очищаем", 2);
                                viewSheet.LookupParameter(paramL).Set("");
                                Logger.Log("      " + paramL + ": очищаем",2);
                            }
                            catch (Exception ex) { Logger.Log(viewSheet.Name + " ошибка заполнения штампа: " + ex.Message,4); }
                        }
                    }


                }
                t2.Commit(); Logger.Log("Закрываем транзакцию 2",1);

            }

            
            Logger.Log("Завершение работы.",5);
            return Result.Succeeded;
        }
    }
}
