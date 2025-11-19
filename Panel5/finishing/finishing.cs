using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
using TNov.main;

namespace TNov
{
    [Transaction(TransactionMode.Manual)]
    public class finishing : IExternalCommand
    {
        private TNovProgressBar levnumProgressBar;
        private void ThreadStartingPoint()
        {
            this.levnumProgressBar = new TNovProgressBar();
            this.levnumProgressBar.Show();
            Dispatcher.Run();
        }
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            string TNovClassName = "Эт.Номер"; DateTime dateTime = DateTime.Now;
            //подключение приложения и документа
            if (RevitAPI.UiApplication == null) { RevitAPI.Initialize(commandData); }
            UIDocument uidoc = RevitAPI.UiDocument; Document doc = RevitAPI.Document;
            UIApplication uiApp = RevitAPI.UiApplication; Autodesk.Revit.ApplicationServices.Application rvtApp = uiApp.Application;

            //проверка подключения, запись в журнал
            bool check = false; servercheck sc = new servercheck(in TNovClassName, out check); if (check == false) { return Result.Failed; }

            // создание log - файла
            Logger.Initialize(TNovClassName);

            var walls0 = new FilteredElementCollector(doc)
                .OfCategory(BuiltInCategory.OST_Walls)
                .WhereElementIsNotElementType()
                .Cast<Wall>()
                .Where(w => w.WallType != null && w.WallType.Kind == WallKind.Basic)
                .ToList();

            List<Wall> walls = new List<Wall>();
            foreach(var wall in walls0)
            {
                Element type = doc.GetElement(wall.GetTypeId());
                if(type.get_Parameter(BuiltInParameter.ALL_MODEL_MODEL).AsString().Contains("Отделка")) walls.Add(wall);
            }
            if (walls.Count == 0) { Logger.Log("Отсутствуют стены отделки. Завершение работы", 3); return Result.Cancelled; }

            int allcount = walls.Count;
            List<string> badIds = new List<string>();

            using (Transaction transaction = new Transaction(doc))
            {
                transaction.Start("TNov - Ведомость отделки");
                Logger.Log("Открываем транзакцию", 1);

                Thread thread = new Thread(new ThreadStart(this.ThreadStartingPoint));
                thread.SetApartmentState(ApartmentState.STA);
                thread.IsBackground = true;
                thread.Start();
                Thread.Sleep(100);

                int PBCount = 0;
                this.levnumProgressBar.TNov_ProgressBar.Dispatcher.Invoke<double>((Func<double>)(() => this.levnumProgressBar.TNov_ProgressBar.Minimum = (double)PBCount));
                this.levnumProgressBar.TNov_ProgressBar.Dispatcher.Invoke<string>((Func<string>)(() => this.levnumProgressBar.value.Text = PBCount.ToString()));
                this.levnumProgressBar.TNov_ProgressBar.Dispatcher.Invoke<double>((Func<double>)(() => this.levnumProgressBar.TNov_ProgressBar.Maximum = (double)allcount));
                this.levnumProgressBar.TNov_ProgressBar.Dispatcher.Invoke<string>((Func<string>)(() => this.levnumProgressBar.maxvalue.Text = allcount.ToString()));

                foreach(var wall in walls)
                {
                    
                    try
                    {
                        Room room = FindWallRoom(wall, doc);

                        if (room != null)
                        {
                            bool set = SetWallRoomParameter(wall, room);
                            if (set)
                            {
                                
                            }
                            else
                            {
                                
                            }
                        }
                        else
                        {
                            badIds.Add(wall.Id.ToString());
                        }
                    }
                    catch (Exception)
                    {
                        
                    }

                    

                    PBCount++;
                    this.levnumProgressBar.TNov_ProgressBar.Dispatcher.Invoke<double>((Func<double>)(() => this.levnumProgressBar.TNov_ProgressBar.Value = (double)PBCount));
                    this.levnumProgressBar.TNov_ProgressBar.Dispatcher.Invoke<string>((Func<string>)(() => this.levnumProgressBar.value.Text = PBCount.ToString()));

                }


                transaction.Commit();
                this.levnumProgressBar.Dispatcher.Invoke((System.Action)(() => this.levnumProgressBar.Close()));
                Logger.Log("Закрываем транзакцию.", 1);
            }

            if (badIds.Count > 0) 
            {
                Logger.Log("Открываем окно с ID проблемных элементов: " + String.Join(", ", badIds), 1);
                // Диалоговое окно
                var viewModel1 = new infowindowtextfieldViewModel();
                viewModel1.headtxt = "У данных стен не удалось программно определить помещения:";
                viewModel1.ids = String.Join(", ", badIds);
                viewModel1.lowtxt = "Возможно, нужно доназначить им параметры вручную.";
                var wpfview1 = new infowindowtextfield(viewModel1);
                viewModel1.CloseRequest += (s, e) => wpfview1.Close();
                bool? ok1 = wpfview1.ShowDialog();
            }

            Logger.Log("Завершение работы.", 5);
            return Result.Succeeded;
        }
        private Room FindWallRoom(Wall wall, Document doc)
        {
            // Агрессивный поиск с использованием всех доступных методов
            var searchMethods = new Func<Wall, Document, Room>[]
            {
            FindRoomByBoundary,                    // Метод 1: Стандартные границы
            FindRoomByExtendedBoundary,            // Метод 2: Расширенные границы
            FindRoomByBoundingBox,                 // Метод 3: BoundingBox
            FindRoomByAdjacentWalls,               // Метод 4: Соседние стены
            FindRoomByDensePointGrid,              // Метод 5: Плотная сетка точек
            FindRoomByProximity,                   // Метод 6: Ближайшее помещение
            FindRoomByRayCasting,                  // Метод 7: Ray casting
            FindRoomBySolidIntersection,           // Метод 8: Пересечение солидов
            FindRoomByTemporaryExtrusion,          // Метод 9: Временное удлинение
            FindRoomByAllMeans                    // Метод 10: Агрессивный комбинированный
            };

            foreach (var method in searchMethods)
            {
                try
                {
                    Room room = method(wall, doc);
                    if (room != null)
                    {
                        return room;
                    }
                }
                catch
                {
                    // Продолжаем попытки с другими методами
                    continue;
                }
            }

            return null;
        }
        private bool SetWallRoomParameter(Wall wall, Room room)
        {
            try
            {
                Parameter roomParam = wall.LookupParameter("N_Отделка.Помещение");
                Parameter roomParam2 = wall.LookupParameter("Отделка.Помещение.Назначение");
                Parameter roomParam3 = wall.LookupParameter("N_Отделка.ГруппаТекст");

                string roomName = room.get_Parameter(BuiltInParameter.ROOM_NAME).AsString();
                string roomNazn = room.LookupParameter("Назначение").AsString();
                string roomGroup = room.LookupParameter("N_Отделка.Группа").AsInteger().ToString();

                if (roomParam == null || roomParam.IsReadOnly)
                    return false;

                bool p1 = false;
                string currentValue = roomParam.AsString();
                if (currentValue != roomName)
                {
                    roomParam.Set(roomName); p1= true;
                }

                bool p2 = false;
                string currentValue2 = roomParam2.AsString();
                if (currentValue2 != roomNazn)
                {
                    roomParam2.Set(roomNazn); p2 = true;
                }

                bool p3 = false;
                string currentValue3 = roomParam3.AsString();
                if (currentValue3 != roomGroup)
                {
                    roomParam3.Set(roomGroup); p3 = true;
                }

                if(p1&&p2&&p3) return true;
                else return false;
            }
            catch
            {
                return false;
            }
        }

        

        // МЕТОДЫ ПОИСКА ПОМЕЩЕНИЙ (копии из апдейтера)
        // НОВЫЙ МЕТОД: Ray casting в нескольких направлениях
        private Room FindRoomByRayCasting(Wall wall, Document doc)
        {
            try
            {
                LocationCurve locationCurve = wall.Location as LocationCurve;
                if (locationCurve == null) return null;

                Curve curve = locationCurve.Curve;
                XYZ wallCenter = curve.Evaluate(0.5, true);
                double wallHeight = GetWallHeight(wall);
                XYZ testPoint = new XYZ(wallCenter.X, wallCenter.Y, wallCenter.Z + wallHeight / 2);

                // Создаем временный 3D вид для ReferenceIntersector
                View3D view3D = GetTemporary3DView(doc);
                if (view3D == null) return null;

                // Направления для ray casting
                var directions = new[]
                {
                    new XYZ(1, 0, 0), new XYZ(-1, 0, 0),
                    new XYZ(0, 1, 0), new XYZ(0, -1, 0),
                    new XYZ(0.707, 0.707, 0), new XYZ(-0.707, 0.707, 0),
                    new XYZ(0.707, -0.707, 0), new XYZ(-0.707, -0.707, 0)
                };

                var roomHits = new Dictionary<Room, int>();

                foreach (var direction in directions)
                {
                    try
                    {
                        ReferenceIntersector intersector = new ReferenceIntersector(
                            new ElementClassFilter(typeof(Room)),
                            FindReferenceTarget.Face,
                            view3D);

                        IList<ReferenceWithContext> references = intersector.Find(testPoint, direction);

                        foreach (var reference in references)
                        {
                            Room room = doc.GetElement(reference.GetReference().ElementId) as Room;
                            if (room != null && room.Area > 0)
                            {
                                if (roomHits.ContainsKey(room))
                                    roomHits[room]++;
                                else
                                    roomHits[room] = 1;

                                // Прерываем после первого найденного помещения в этом направлении
                                break;
                            }
                        }
                    }
                    catch
                    {
                        continue;
                    }
                }

                return roomHits.OrderByDescending(x => x.Value).FirstOrDefault().Key;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка в FindRoomByRayCasting: {ex.Message}");
                return null;
            }
        }

        // НОВЫЙ МЕТОД: Пересечение солидов (геометрия)
        private Room FindRoomBySolidIntersection(Wall wall, Document doc)
        {
            try
            {
                // Получаем геометрию стены
                Options options = new Options();
                options.ComputeReferences = true;
                options.DetailLevel = ViewDetailLevel.Fine;

                GeometryElement wallGeometry = wall.get_Geometry(options);
                if (wallGeometry == null) return null;

                // Получаем солид стены
                Solid wallSolid = GetWallSolid(wallGeometry);
                if (wallSolid == null || wallSolid.Faces.IsEmpty) return null;

                // Ищем пересекающиеся помещения через BoundingBox
                BoundingBoxXYZ wallBox = wall.get_BoundingBox(null);
                if (wallBox == null) return null;

                Outline wallOutline = new Outline(wallBox.Min, wallBox.Max);
                BoundingBoxIntersectsFilter filter = new BoundingBoxIntersectsFilter(wallOutline);

                var candidateRooms = new FilteredElementCollector(doc)
                    .OfClass(typeof(Room))
                    .WherePasses(filter)
                    .Cast<Room>()
                    .Where(r => r != null && r.Area > 0)
                    .ToList();

                var intersectingRooms = new List<Room>();

                foreach (Room room in candidateRooms)
                {
                    try
                    {
                        // Получаем солид помещения через его границы
                        Solid roomSolid = GetRoomSolidFromBoundaries(room, doc);
                        if (roomSolid == null || roomSolid.Faces.IsEmpty) continue;

                        // Проверяем пересечение
                        if (SolidsIntersect(wallSolid, roomSolid))
                        {
                            intersectingRooms.Add(room);
                        }
                    }
                    catch
                    {
                        continue;
                    }
                }

                // Возвращаем помещение с наибольшей площадью пересечения
                return intersectingRooms.OrderByDescending(r => r.Area).FirstOrDefault();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка в FindRoomBySolidIntersection: {ex.Message}");
                return null;
            }
        }

        // НОВЫЙ МЕТОД: Временное "удлинение" стены для поиска
        private Room FindRoomByTemporaryExtrusion(Wall wall, Document doc)
        {
            try
            {
                LocationCurve locationCurve = wall.Location as LocationCurve;
                if (locationCurve == null) return null;

                Curve curve = locationCurve.Curve;
                double wallLength = curve.Length;

                // Если стена очень короткая, "удлиняем" ее виртуально
                if (wallLength < 0.5) // менее 500 мм
                {
                    XYZ startPoint = curve.GetEndPoint(0);
                    XYZ endPoint = curve.GetEndPoint(1);

                    // Вычисляем направление стены
                    XYZ direction = (endPoint - startPoint).Normalize();

                    // Создаем "удлиненные" точки
                    XYZ extendedStart = startPoint - direction * 1.0; // удлиняем на 1 метр
                    XYZ extendedEnd = endPoint + direction * 1.0;

                    // Создаем временную кривую для проверки
                    Line extendedLine = Line.CreateBound(extendedStart, extendedEnd);

                    // Ищем помещения вдоль удлиненной линии
                    return FindRoomAlongCurve(extendedLine, wall, doc);
                }

                return null;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка в FindRoomByTemporaryExtrusion: {ex.Message}");
                return null;
            }
        }

        // НОВЫЙ МЕТОД: Агрессивный комбинированный поиск
        private Room FindRoomByAllMeans(Wall wall, Document doc)
        {
            try
            {
                // Собираем все возможные кандидаты всеми методами
                var allCandidates = new Dictionary<Room, int>();

                // Список всех методов поиска (кроме текущего)
                var searchMethods = new Func<Wall, Document, Room>[]
                {
                    FindRoomByBoundary,
                    FindRoomByExtendedBoundary,
                    FindRoomByBoundingBox,
                    FindRoomByAdjacentWalls,
                    FindRoomByDensePointGrid,
                    FindRoomByProximity,
                    FindRoomByRayCasting,
                    FindRoomBySolidIntersection,
                    FindRoomByTemporaryExtrusion
                };

                // Запускаем все методы и собираем результаты
                foreach (var method in searchMethods)
                {
                    try
                    {
                        Room candidate = method(wall, doc);
                        if (candidate != null)
                        {
                            if (allCandidates.ContainsKey(candidate))
                                allCandidates[candidate] += 2; // Двойной вес за повторное нахождение
                            else
                                allCandidates[candidate] = 1;
                        }
                    }
                    catch
                    {
                        continue;
                    }
                }

                // Дополнительно: проверяем все помещения в радиусе 2 метров
                LocationCurve locationCurve = wall.Location as LocationCurve;
                if (locationCurve != null)
                {
                    Curve curve = locationCurve.Curve;
                    XYZ wallCenter = curve.Evaluate(0.5, true);

                    var nearbyRooms = new FilteredElementCollector(doc)
                        .OfClass(typeof(Room))
                        .WhereElementIsNotElementType()
                        .Cast<Room>()
                        .Where(r => r != null && r.Area > 0)
                        .ToList();

                    foreach (Room room in nearbyRooms)
                    {
                        XYZ roomCenter = GetRoomCenter(room);
                        double distance = roomCenter.DistanceTo(wallCenter);

                        if (distance < 2.0) // в радиусе 2 метров
                        {
                            if (allCandidates.ContainsKey(room))
                                allCandidates[room] += 1;
                            else
                                allCandidates[room] = 1;
                        }
                    }
                }

                // Выбираем лучшего кандидата
                var bestCandidate = allCandidates.OrderByDescending(x => x.Value).FirstOrDefault();
                if (bestCandidate.Value >= 1) // хотя бы одно подтверждение
                {
                    return bestCandidate.Key;
                }

                return null;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка в FindRoomByAllMeans: {ex.Message}");
                return null;
            }
        }

        // ВСПОМОГАТЕЛЬНЫЕ МЕТОДЫ ДЛЯ НОВЫХ ФУНКЦИЙ

        private View3D GetTemporary3DView(Document doc)
        {
            try
            {
                // Пытаемся найти существующий 3D вид
                var view3D = new FilteredElementCollector(doc)
                    .OfClass(typeof(View3D))
                    .Cast<View3D>()
                    .FirstOrDefault(v => !v.IsTemplate && v.Name == "TempViewForRoomDetection");

                if (view3D != null) return view3D;

                // Создаем новый временный вид
                var viewFamilyType = new FilteredElementCollector(doc)
                    .OfClass(typeof(ViewFamilyType))
                    .Cast<ViewFamilyType>()
                    .FirstOrDefault(vft => vft.ViewFamily == ViewFamily.ThreeDimensional);

                if (viewFamilyType == null) return null;

                using (Transaction trans = new Transaction(doc, "Create Temp 3D View"))
                {
                    trans.Start();
                    view3D = View3D.CreateIsometric(doc, viewFamilyType.Id);
                    view3D.Name = "TempViewForRoomDetection";
                    trans.Commit();
                }

                return view3D;
            }
            catch
            {
                return null;
            }
        }

        private Solid GetWallSolid(GeometryElement geometry)
        {
            foreach (GeometryObject obj in geometry)
            {
                if (obj is Solid solid && solid.Volume > 0)
                    return solid;

                if (obj is GeometryInstance instance)
                {
                    GeometryElement instanceGeometry = instance.GetSymbolGeometry();
                    foreach (GeometryObject symbolObj in instanceGeometry)
                    {
                        if (symbolObj is Solid symbolSolid && symbolSolid.Volume > 0)
                            return symbolSolid;
                    }
                }
            }
            return null;
        }

        private Solid GetRoomSolidFromBoundaries(Room room, Document doc)
        {
            try
            {
                // Получаем границы помещения
                SpatialElementBoundaryOptions options = new SpatialElementBoundaryOptions();
                IList<IList<BoundarySegment>> boundaries = room.GetBoundarySegments(options);

                if (boundaries == null || boundaries.Count == 0) return null;

                // Создаем полигональную кривую из границ
                List<CurveLoop> curveLoops = new List<CurveLoop>();

                foreach (var boundaryLoop in boundaries)
                {
                    List<Curve> curves = new List<Curve>();
                    foreach (var segment in boundaryLoop)
                    {
                        curves.Add(segment.GetCurve());
                    }
                    curveLoops.Add(CurveLoop.Create(curves));
                }

                // Создаем солид из полигональных кривых
                SolidOptions solidOptions = new SolidOptions(ElementId.InvalidElementId, ElementId.InvalidElementId);
                return GeometryCreationUtilities.CreateExtrusionGeometry(curveLoops, XYZ.BasisZ, 3.0, solidOptions);
            }
            catch
            {
                return null;
            }
        }

        private bool SolidsIntersect(Solid solid1, Solid solid2)
        {
            try
            {
                return BooleanOperationsUtils.ExecuteBooleanOperation(solid1, solid2, BooleanOperationsType.Intersect).Volume > 0.001;
            }
            catch
            {
                return false;
            }
        }
        private XYZ GetRoomCenter(Room room)
        {
            try
            {
                // Способ 1: Через BoundingBox (самый надежный)
                BoundingBoxXYZ bbox = room.get_BoundingBox(null);
                if (bbox != null)
                {
                    return (bbox.Min + bbox.Max) * 0.5;
                }

                // Способ 2: Через LocationPoint
                Location location = room.Location;
                if (location is LocationPoint locPoint)
                {
                    return locPoint.Point;
                }

                // Способ 3: Вычисляем среднюю точку из границ
                return CalculateRoomCenterFromBoundaries(room);
            }
            catch
            {
                return XYZ.Zero;
            }
        }
        private XYZ CalculateRoomCenterFromBoundaries(Room room)
        {
            try
            {
                SpatialElementBoundaryOptions options = new SpatialElementBoundaryOptions();
                IList<IList<BoundarySegment>> boundaries = room.GetBoundarySegments(options);

                if (boundaries == null || boundaries.Count == 0)
                    return XYZ.Zero;

                double totalX = 0, totalY = 0, totalZ = 0;
                int pointCount = 0;

                foreach (var boundaryLoop in boundaries)
                {
                    foreach (var segment in boundaryLoop)
                    {
                        Curve curve = segment.GetCurve();
                        XYZ startPoint = curve.GetEndPoint(0);
                        XYZ endPoint = curve.GetEndPoint(1);

                        totalX += startPoint.X + endPoint.X;
                        totalY += startPoint.Y + endPoint.Y;
                        totalZ += startPoint.Z + endPoint.Z;
                        pointCount += 2;
                    }
                }

                if (pointCount > 0)
                {
                    return new XYZ(totalX / pointCount, totalY / pointCount, totalZ / pointCount);
                }

                return XYZ.Zero;
            }
            catch
            {
                return XYZ.Zero;
            }
        }
        private Room FindRoomAlongCurve(Curve curve, Wall wall, Document doc)
        {
            try
            {
                double wallHeight = GetWallHeight(wall);
                var roomCandidates = new Dictionary<Room, int>();

                // Проверяем точки вдоль кривой
                for (int i = 0; i <= 10; i++)
                {
                    double parameter = (double)i / 10;
                    XYZ testPoint = curve.Evaluate(parameter, true);
                    XYZ elevatedPoint = new XYZ(testPoint.X, testPoint.Y, testPoint.Z + wallHeight / 2);

                    var roomsAtPoint = new FilteredElementCollector(doc)
                        .OfClass(typeof(Room))
                        .WhereElementIsNotElementType()
                        .Cast<Room>()
                        .Where(r => r != null && IsPointInRoom(r, elevatedPoint, doc))
                        .ToList();

                    foreach (Room room in roomsAtPoint)
                    {
                        if (roomCandidates.ContainsKey(room))
                            roomCandidates[room]++;
                        else
                            roomCandidates[room] = 1;
                    }
                }

                return roomCandidates.OrderByDescending(x => x.Value).FirstOrDefault().Key;
            }
            catch
            {
                return null;
            }

        }
        private Room FindRoomForShortWall(Wall wall, Document doc)
        {
            try
            {
                // Метод 2A: Поиск через расширенные границы с разными параметрами
                Room room = FindRoomByExtendedBoundary(wall, doc);
                if (room != null) return room;

                // Метод 2B: Поиск через анализ соседних стен
                room = FindRoomByAdjacentWalls(wall, doc);
                if (room != null) return room;

                // Метод 2C: Поиск через очень плотную сетку точек
                room = FindRoomByDensePointGrid(wall, doc);
                return room;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка в FindRoomForShortWall: {ex.Message}");
                return null;
            }
        }
        private Room FindRoomByExtendedBoundary(Wall wall, Document doc)
        {
            try
            {
                // Пробуем разные варианты SpatialElementBoundaryLocation
                var locations = new[]
                {
                    SpatialElementBoundaryLocation.Center,
                    SpatialElementBoundaryLocation.Finish,
                    SpatialElementBoundaryLocation.CoreCenter,
                    SpatialElementBoundaryLocation.CoreBoundary,
                };

                foreach (var location in locations)
                {
                    var options = new SpatialElementBoundaryOptions
                    {
                        SpatialElementBoundaryLocation = location,
                        StoreFreeBoundaryFaces = true
                    };

                    var rooms = new FilteredElementCollector(doc)
                        .OfClass(typeof(Room))
                        .WhereElementIsNotElementType()
                        .Cast<Room>()
                        .Where(r => r != null && r.Area > 0);

                    foreach (Room room in rooms)
                    {
                        try
                        {
                            var boundaries = room.GetBoundarySegments(options);
                            if (boundaries == null) continue;

                            foreach (var boundaryLoop in boundaries)
                            {
                                foreach (var segment in boundaryLoop)
                                {
                                    if (segment.ElementId == wall.Id)
                                    {
                                        return room;
                                    }
                                }
                            }
                        }
                        catch
                        {
                            continue;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка в FindRoomByExtendedBoundary: {ex.Message}");
            }

            return null;
        }
        private Room FindRoomByAdjacentWalls(Wall wall, Document doc)
        {
            try
            {
                // Получаем конечные точки стены
                LocationCurve locationCurve = wall.Location as LocationCurve;
                if (locationCurve == null) return null;

                Curve curve = locationCurve.Curve;
                XYZ startPoint = curve.GetEndPoint(0);
                XYZ endPoint = curve.GetEndPoint(1);

                // Ищем стены, соединенные с нашей короткой стеной
                var connectedWalls = FindConnectedWalls(wall, startPoint, endPoint, doc);

                // Для каждой соединенной стены находим ее помещение
                var roomCandidates = new Dictionary<Room, int>();

                foreach (Wall connectedWall in connectedWalls)
                {
                    Room connectedRoom = FindRoomByBoundary(connectedWall, doc) ??
                                        FindRoomByBoundingBox(connectedWall, doc);

                    if (connectedRoom != null)
                    {
                        if (roomCandidates.ContainsKey(connectedRoom))
                            roomCandidates[connectedRoom]++;
                        else
                            roomCandidates[connectedRoom] = 1;
                    }
                }

                // Выбираем помещение, к которому подключено больше всего стен
                return roomCandidates.OrderByDescending(x => x.Value).FirstOrDefault().Key;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка в FindRoomByAdjacentWalls: {ex.Message}");
                return null;
            }
        }
        private List<Wall> FindConnectedWalls(Wall wall, XYZ startPoint, XYZ endPoint, Document doc)
        {
            var connectedWalls = new List<Wall>();
            double tolerance = 0.01; // 10 мм допуск

            try
            {
                // Ищем стены, которые соединены с нашей стеной
                var allWalls = new FilteredElementCollector(doc)
                    .OfCategory(BuiltInCategory.OST_Walls)
                    .WhereElementIsNotElementType()
                    .Cast<Wall>()
                    .Where(w => w.Id != wall.Id && w.WallType != null && w.WallType.Kind == WallKind.Basic);

                foreach (Wall otherWall in allWalls)
                {
                    LocationCurve otherLocation = otherWall.Location as LocationCurve;
                    if (otherLocation == null) continue;

                    Curve otherCurve = otherLocation.Curve;
                    XYZ otherStart = otherCurve.GetEndPoint(0);
                    XYZ otherEnd = otherCurve.GetEndPoint(1);

                    // Проверяем соединение по конечным точкам
                    if (startPoint.DistanceTo(otherStart) < tolerance ||
                        startPoint.DistanceTo(otherEnd) < tolerance ||
                        endPoint.DistanceTo(otherStart) < tolerance ||
                        endPoint.DistanceTo(otherEnd) < tolerance)
                    {
                        connectedWalls.Add(otherWall);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка в FindConnectedWalls: {ex.Message}");
            }

            return connectedWalls;
        }
        private Room FindRoomByDensePointGrid(Wall wall, Document doc)
        {
            try
            {
                LocationCurve locationCurve = wall.Location as LocationCurve;
                if (locationCurve == null) return null;

                BoundingBoxXYZ wallBox = wall.get_BoundingBox(null);
                if (wallBox == null) return null;

                Outline wallOutline = new Outline(wallBox.Min, wallBox.Max);
                BoundingBoxIntersectsFilter bbFilter = new BoundingBoxIntersectsFilter(wallOutline);

                var candidateRooms = new FilteredElementCollector(doc)
                    .OfClass(typeof(Room))
                    .WherePasses(bbFilter)
                    .Cast<Room>()
                    .Where(r => r != null && r.Area > 0)
                    .ToList();

                if (candidateRooms.Count == 0) return null;

                Curve curve = locationCurve.Curve;
                double wallLength = curve.Length;

                // Для коротких стен используем очень плотную сетку точек
                int samplePoints = Math.Max(10, (int)(wallLength / 0.01)); // Минимум 10 точек, максимум каждые 10 мм
                samplePoints = Math.Min(samplePoints, 50); // Ограничиваем максимум 50 точками

                var roomCandidates = new Dictionary<Room, int>();
                double wallHeight = GetWallHeight(wall);

                for (int i = 0; i <= samplePoints; i++)
                {
                    double parameter = (double)i / samplePoints;
                    XYZ testPoint = curve.Evaluate(parameter, true);
                    XYZ elevatedPoint = new XYZ(testPoint.X, testPoint.Y, testPoint.Z + wallHeight / 2);

                    foreach (Room room in candidateRooms)
                    {
                        if (IsPointInRoom(room, elevatedPoint, doc))
                        {
                            if (roomCandidates.ContainsKey(room))
                                roomCandidates[room]++;
                            else
                                roomCandidates[room] = 1;
                        }
                    }
                }

                // Для коротких стен требуем большее количество попаданий
                int minHits = Math.Max(3, samplePoints / 3);
                var bestCandidate = roomCandidates.Where(x => x.Value >= minHits)
                                                 .OrderByDescending(x => x.Value)
                                                 .FirstOrDefault().Key;

                return bestCandidate;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка в FindRoomByDensePointGrid: {ex.Message}");
                return null;
            }
        }
        private Room FindRoomByProximity(Wall wall, Document doc)
        {
            try
            {
                LocationCurve locationCurve = wall.Location as LocationCurve;
                if (locationCurve == null) return null;

                Curve curve = locationCurve.Curve;
                XYZ centerPoint = curve.Evaluate(0.5, true);
                double wallHeight = GetWallHeight(wall);
                XYZ elevatedPoint = new XYZ(centerPoint.X, centerPoint.Y, centerPoint.Z + wallHeight / 2);

                // Ищем все помещения в радиусе 1 метр от центра стены
                var nearbyRooms = new FilteredElementCollector(doc)
                    .OfClass(typeof(Room))
                    .WhereElementIsNotElementType()
                    .Cast<Room>()
                    .Where(r => r != null && r.Area > 0)
                    .OrderBy(r => GetRoomDistance(r, elevatedPoint, doc))
                    .ToList();

                // Берем ближайшее помещение
                return nearbyRooms.FirstOrDefault();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка в FindRoomByProximity: {ex.Message}");
                return null;
            }
        }

        private double GetRoomDistance(Room room, XYZ point, Document doc)
        {
            try
            {
                // Получаем местоположение комнаты через LocationPoint
                LocationPoint roomLocationPoint = room.Location as LocationPoint;
                if (roomLocationPoint != null)
                {
                    return roomLocationPoint.Point.DistanceTo(point);
                }

                // Альтернативный метод через BoundingBox
                BoundingBoxXYZ roomBox = room.get_BoundingBox(null);
                if (roomBox != null)
                {
                    XYZ roomCenter = (roomBox.Min + roomBox.Max) * 0.5;
                    return roomCenter.DistanceTo(point);
                }

                return double.MaxValue;
            }
            catch
            {
                return double.MaxValue;
            }
        }

        private double GetWallLength(Wall wall)
        {
            try
            {
                LocationCurve locationCurve = wall.Location as LocationCurve;
                if (locationCurve == null) return 0;

                Curve curve = locationCurve.Curve;
                return curve.Length;
            }
            catch
            {
                return 0;
            }
        }
        private Room FindRoomByBoundary(Wall wall, Document doc)
        {
            try
            {
                SpatialElementBoundaryOptions options = new SpatialElementBoundaryOptions
                {
                    SpatialElementBoundaryLocation = SpatialElementBoundaryLocation.Center,
                    StoreFreeBoundaryFaces = true
                };

                var adjacentRooms = new List<Room>();

                // Используем оптимизированный сбор помещений
                var rooms = new FilteredElementCollector(doc)
                    .OfClass(typeof(SpatialElement))
                    .WhereElementIsNotElementType()
                    .Cast<Room>()
                    .Where(r => r != null && r.Area > 0);

                foreach (Room room in rooms)
                {
                    try
                    {
                        IList<IList<BoundarySegment>> boundaries = room.GetBoundarySegments(options);
                        if (boundaries == null) continue;

                        foreach (IList<BoundarySegment> boundaryLoop in boundaries)
                        {
                            foreach (BoundarySegment segment in boundaryLoop)
                            {
                                if (segment.ElementId == wall.Id)
                                {
                                    adjacentRooms.Add(room);
                                    break;
                                }
                            }
                        }
                    }
                    catch
                    {
                        continue;
                    }
                }

                if (adjacentRooms.Count > 0)
                {
                    // Выбираем помещение с наибольшей площадью
                    return adjacentRooms.OrderByDescending(r => r.Area).First();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Boundary method error: {ex.Message}");
            }

            return null;
        }

        private Room FindRoomByBoundingBox(Wall wall, Document doc)
        {
            try
            {
                BoundingBoxXYZ wallBox = wall.get_BoundingBox(null);
                if (wallBox == null) return null;

                Outline wallOutline = new Outline(wallBox.Min, wallBox.Max);
                BoundingBoxIntersectsFilter bbFilter = new BoundingBoxIntersectsFilter(wallOutline);

                var candidateRooms = new FilteredElementCollector(doc)
                    .OfClass(typeof(SpatialElement))
                    .WherePasses(bbFilter)
                    .WhereElementIsNotElementType()
                    .Cast<Room>()
                    .Where(r => r != null && r.Area > 0)
                    .ToList();

                LocationCurve wallLocation = wall.Location as LocationCurve;
                if (wallLocation == null) return null;

                Curve wallCurve = wallLocation.Curve;
                int samplePoints = 3; // Уменьшаем количество точек для производительности
                var roomCandidates = new Dictionary<Room, int>();

                for (int i = 0; i <= samplePoints; i++)
                {
                    double parameter = (double)i / samplePoints;
                    XYZ testPoint = wallCurve.Evaluate(parameter, true);

                    double wallHeight = GetWallHeight(wall);
                    XYZ elevatedPoint = new XYZ(testPoint.X, testPoint.Y, testPoint.Z + wallHeight / 2);

                    foreach (Room room in candidateRooms)
                    {
                        if (IsPointInRoom(room, elevatedPoint, doc))
                        {
                            if (roomCandidates.ContainsKey(room))
                                roomCandidates[room]++;
                            else
                                roomCandidates[room] = 1;
                        }
                    }
                }

                return roomCandidates.OrderByDescending(x => x.Value).FirstOrDefault().Key;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"BoundingBox method error: {ex.Message}");
                return null;
            }
        }

        private double GetWallHeight(Wall wall)
        {
            Parameter heightParam = wall.get_Parameter(BuiltInParameter.WALL_USER_HEIGHT_PARAM);
            if (heightParam != null && heightParam.HasValue)
                return heightParam.AsDouble();

            BoundingBoxXYZ bbox = wall.get_BoundingBox(null);
            if (bbox != null)
                return bbox.Max.Z - bbox.Min.Z;

            return 3.0;
        }

        private bool IsPointInRoom(Room room, XYZ point, Document doc)
        {
            try
            {
                // Используем встроенный метод Revit для проверки
                if (room.IsPointInRoom(point))
                    return true;

                // Альтернативная проверка через границы
                SpatialElementBoundaryOptions options = new SpatialElementBoundaryOptions
                {
                    SpatialElementBoundaryLocation = SpatialElementBoundaryLocation.Center
                };

                IList<IList<BoundarySegment>> boundaries = room.GetBoundarySegments(options);
                if (boundaries == null || boundaries.Count == 0) return false;

                return IsPointInPolygon(point, boundaries.First());
            }
            catch
            {
                return false;
            }
        }

        private bool IsPointInPolygon(XYZ point, IList<BoundarySegment> boundary)
        {
            int crossings = 0;
            int segmentCount = boundary.Count;

            for (int i = 0; i < segmentCount; i++)
            {
                XYZ p1 = boundary[i].GetCurve().GetEndPoint(0);
                XYZ p2 = boundary[(i + 1) % segmentCount].GetCurve().GetEndPoint(0);

                if (((p1.Y > point.Y) != (p2.Y > point.Y)) &&
                    (point.X < (p2.X - p1.X) * (point.Y - p1.Y) / (p2.Y - p1.Y) + p1.X))
                {
                    crossings++;
                }
            }

            return (crossings % 2 == 1);
        }

        
    }
}
