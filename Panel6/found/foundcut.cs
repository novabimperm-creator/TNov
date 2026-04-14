using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.Attributes;
using System.Collections.Generic;
using System.Linq;
using System;
using System.IO;
using Outline = Autodesk.Revit.DB.Outline;
using View = Autodesk.Revit.DB.View;
using System.Windows.Threading;
using System.Threading;
using Newtonsoft.Json;
using TNovCommon;

namespace TNov
{
    [Transaction(TransactionMode.Manual)]
    public class FoundCut : IExternalCommand
    {
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
        private TNovProgressBar foundcutProgressBar;
        private void ThreadStartingPoint()
        {
            this.foundcutProgressBar = new TNovProgressBar();
            this.foundcutProgressBar.Show();
            Dispatcher.Run();
        }
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            string TNovClassName = "Сваи Вырезать фундамент"; DateTime dateTime = DateTime.Now;
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
                if (qok != null && qok == true) { Logger.TurnOffExtendedLogs(); } else Logger.Log("Расширенные логи вкл",2);
            }

            //Список используемых параметров

            BuiltInParameter gm = BuiltInParameter.ALL_MODEL_MODEL; //параметр Группа модели

            Logger.Log("Сбор элементов",1);
            List<Autodesk.Revit.DB.Floor> foundations = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_StructuralFoundation)   //Фундаменты
                                                                         .WhereElementIsNotElementType()
                                                                         .OfClass(typeof(Autodesk.Revit.DB.Floor))  //отсеиваем модели в контексте
                                                                         .Cast<Autodesk.Revit.DB.Floor>()
                                                                         .ToList();
            
            List<FamilyInstance> piles = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_StructuralFoundation)   //Фундаменты семействами
                                                                         .WhereElementIsNotElementType()
                                                                         .OfClass(typeof(Autodesk.Revit.DB.FamilyInstance))
                                                                         .Cast<FamilyInstance>()
                                                                         .ToList();

            List<FamilyInstance> piles1 = new List<FamilyInstance>();

            foreach (var p in piles) //ищем сваи
            {
                string pvalue = p.Symbol.get_Parameter(gm).AsString();
                if (pvalue != null)
                {
                    if (pvalue.Contains("Свая")) { piles1.Add(p); }
                }
            }

            int pc = piles1.Count;
            if(pc ==  0) 
            { 
                new InfoWindow280("В проекте отсутствуют сваи.").ShowDialog();
                Logger.Log("В проекте отсутствуют сваи. Завершение работы.", 3);
                return Result.Failed; 
            }

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

            ElementId workviewid = uidoc.ActiveView.Id;
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

            int allcount = piles1.Count;

            Thread thread = new Thread(new ThreadStart(this.ThreadStartingPoint));
            thread.SetApartmentState(ApartmentState.STA);
            thread.IsBackground = true;
            thread.Start();
            Thread.Sleep(100);

            int PBCount = 0;
            this.foundcutProgressBar.TNov_ProgressBar.Dispatcher.Invoke<double>((Func<double>)(() => this.foundcutProgressBar.TNov_ProgressBar.Minimum = (double)PBCount));
            this.foundcutProgressBar.TNov_ProgressBar.Dispatcher.Invoke<string>((Func<string>)(() => this.foundcutProgressBar.value.Text = PBCount.ToString()));
            this.foundcutProgressBar.TNov_ProgressBar.Dispatcher.Invoke<double>((Func<double>)(() => this.foundcutProgressBar.TNov_ProgressBar.Maximum = (double)allcount));
            this.foundcutProgressBar.TNov_ProgressBar.Dispatcher.Invoke<string>((Func<string>)(() => this.foundcutProgressBar.maxvalue.Text = allcount.ToString()));


            using (Transaction transaction = new Transaction(doc))
            {
                transaction.Start("TNov - вырез свай");
                Logger.Log("Открываем транзакцию",1);

                foreach (FamilyInstance p in piles1)
                {
                    

                    Element elem1 = doc.GetElement(p.Id);
                    BoundingBoxXYZ elem1box = elem1.get_BoundingBox(doc.ActiveView);
                    Outline outline1 = new Outline(elem1box.Min, elem1box.Max);
                    BoundingBoxIntersectsFilter bbfilter = new BoundingBoxIntersectsFilter(outline1);
                    FilteredElementCollector collector = new FilteredElementCollector(doc, workviewid);
                    ICollection<ElementId> idsExclude = new List<ElementId> { elem1.Id };
                    collector.Excluding(idsExclude)
                            .WherePasses(bbfilter);
                    Logger.Log("Свая " + p.Id,1);
                    foreach (var elem2 in collector)
                    {
                        try
                        {
                            bool areJoined = JoinGeometryUtils.AreElementsJoined(doc, elem1, elem2);
                            if (areJoined)
                            {
                                JoinGeometryUtils.UnjoinGeometry(doc, elem1, elem2);
                                Logger.Log("   Элемент " + elem2.Id + ": отсоединено успешно",2);
                            }
                        }
                        catch (Exception ex)
                        {
                            Logger.Log("Свая " + p.Id + "   Элемент " + elem2.Id + " Ошибка: " + ex.Message,4);
                        }
                        
                    }
                    foreach (var elem2 in collector)
                    {
                        try
                        {
                            Intersections.CutElement(doc, elem2, elem1);
                            Logger.Log("   Элемент " + elem2.Id + ": вырезано успешно",2);
                        }
                        catch (Exception ex)
                        {
                            Logger.Log("Свая " + p.Id+"   Элемент " + elem2.Id + " Ошибка: " + ex.Message, 4);
                        }

                    }
                    PBCount++;
                    this.foundcutProgressBar.TNov_ProgressBar.Dispatcher.Invoke<double>((Func<double>)(() => this.foundcutProgressBar.TNov_ProgressBar.Value = (double)PBCount));
                    this.foundcutProgressBar.TNov_ProgressBar.Dispatcher.Invoke<string>((Func<string>)(() => this.foundcutProgressBar.value.Text = PBCount.ToString()));

                }

                //var info1 = new InfoWindow280("Успешно!"); info1.ShowDialog();
                transaction.Commit();
                this.foundcutProgressBar.Dispatcher.Invoke((System.Action)(() => this.foundcutProgressBar.Close()));
                Logger.Log("Закрываем транзакцию",1);

            }

            Logger.Log("Завершение работы.",5);
            return Result.Succeeded;
        }
    }
    
}
