using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.Attributes;
using System;

namespace TNov
{


    [Transaction(TransactionMode.Manual)]
    public class parksq : IExternalCommand
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
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            
            
            /*
            string TNovClassName = "Парковки Площади"; DateTime dateTime = DateTime.Now;
            //подключение приложения и документа
            if (RevitAPI.UiApplication == null) { RevitAPI.Initialize(commandData); }
            UIDocument uidoc = RevitAPI.UiDocument; Document doc = RevitAPI.Document;
            UIApplication uiApp = RevitAPI.UiApplication; Autodesk.Revit.ApplicationServices.Application rvtApp = uiApp.Application;
            // создание log - файла
            Logger.Log("Старт работы;");
            //проверка подключения, запись в журнал
            bool check = false; servercheck sc = new servercheck(in TNovClassName, out check); if (check == false) { return Result.Failed; }

            //Проверка актуальности шаблона
            templatecheck tc = new templatecheck(in commandData, out bool oldProject);

            //Список используемых параметров

            BuiltInParameter gm = BuiltInParameter.ALL_MODEL_MODEL; //параметр Группа модели
            string param = "A_Размер_Площадь";
            
            Logger.Log("Сбор элементов;");

            List<FamilyInstance> parks = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_Parking)   //фильтр по категории Парковка
                                                                         .WhereElementIsNotElementType()
                                                                         .Cast<FamilyInstance>()
                                                                         .ToList();

            List<Wall> walls = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_Walls)   //фильтр по категории Стены
                                                                         .WhereElementIsNotElementType()    //фильтр только экземпляры
                                                                         .OfClass(typeof(Wall))         //отсеиваем модели в контексте
                                                                         .Cast<Wall>()                     //элементы категории Стены
                                                                         .ToList();                         //формируем список

            List<FamilyInstance> columns = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_StructuralColumns)   //Несущие колонны
                                                                         .WhereElementIsNotElementType()
                                                                         .Cast<FamilyInstance>()
                                                                         .ToList();

            List<Element> elems = new List<Element>();
            foreach(Wall wall in walls) { Element elem = doc.GetElement(wall.Id); elems.Add(elem); }
            foreach (FamilyInstance column in columns) { Element elem = doc.GetElement(column.Id); elems.Add(elem); }

            int pc = parks.Count;
            if(pc ==  0) 
            { var info1 = new infowindow280("В проекте отсутствуют элементы паркинга."); info1.ShowDialog(); return Result.Failed; }

            ElementId workviewid = uidoc.ActiveView.Id;
            Logger.Log("Элементы собраны;");

            //Создаем 3д-вид, где видны все элементы

            Logger.Log("Настраиваем вид TNov;");

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
                foreach (View view in views1) { if (view.Name == "TNov") { workviewid = view.Id; } }
                Autodesk.Revit.DB.View3D workview3d;
                workview3d = (View3D)doc.GetElement(workviewid);

                using (Transaction transaction0 = new Transaction(doc))
                {

                    transaction0.Start("TNov - рабочий 3D-вид");

                    workview3d.IsSectionBoxActive = false;

                    transaction0.Commit();
                }

            }
            Logger.Log("Вид TNov настроен для работы;");

            using (Transaction transaction = new Transaction(doc))
            {
                transaction.Start("TNov - Парковки Площади");
                Logger.Log("Открываем транзакцию.");

                foreach (Element elem1 in elems)
                {
                    int i = 0;
                    BoundingBoxXYZ elem1box = elem1.get_BoundingBox(doc.ActiveView);
                    Outline outline1 = new Outline(elem1box.Min, elem1box.Max);
                    BoundingBoxIntersectsFilter bbfilter = new BoundingBoxIntersectsFilter(outline1, 20 / 304.8);
                    FilteredElementCollector collector = new FilteredElementCollector(doc, workviewid);
                    collector.WherePasses(bbfilter);
                    foreach (var elem in collector)
                    {
                        bool isPark = false;
                        Category category = elem.Category;
                        if (category.Name.Contains("Парковк")) { isPark = true; }
                        if (isPark)
                        {
                            Logger.Log("   Элемент " + elem.Id);

                            //ПРОБЛЕМА: НЕОБХОДИМО РЕАЛИЗОВАТЬ АНАЛОГ НОДА DYNAMO "BoundingBox.Intersection" - 
                            //- прямого метода в API нет, нужно погружаться в работу с геометрией

                            //скопировано из joiner-а:
                            try
                            {
                                bool j = JoinGeometryUtils.AreElementsJoined(doc, elem1, elem);
                                JoinGeometryUtils.JoinGeometry(doc, elem1, elem);
                                string jid = elem.Id.ToString(); if (j == false) { ids.Add(jid); i++; }
                                Logger.Log(" Успешно;");
                            }
                            catch (Exception ex)
                            {
                                Logger.Log(" Exception: " + ex.Message);
                            }
                        }


                    }
                }

                transaction.Commit();
                Logger.Log("Закрываем транзакцию.");
            }


            Logger.Log("Завершение работы.");
            */

            return Result.Succeeded;
        }
    }
    
}
