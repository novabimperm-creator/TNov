using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TNov
{
    public class TNovFloorCeilingUpdater : IUpdater
    {
        private static AddInId m_appId;
        private static UpdaterId m_updaterId;

        // Параметр для записи имени помещения
        private const string ROOM_NAME_PARAM = "Имя помещения";

        public TNovFloorCeilingUpdater(AddInId id)
        {
            m_appId = id;
            m_updaterId = new UpdaterId(m_appId, new Guid("3C1F3F47-7B1C-4A7E-8A0A-1A1B1C1D1E1F"));
        }

        public void Execute(UpdaterData data)
        {
            Document doc = data.GetDocument();
            ICollection<ElementId> addedIds = data.GetAddedElementIds();
            ICollection<ElementId> modifiedIds = data.GetModifiedElementIds();

            // Объединяем все измененные элементы
            var allElementIds = new HashSet<ElementId>(addedIds);
            allElementIds.UnionWith(modifiedIds);

            if (!allElementIds.Any()) return;

            

            foreach (ElementId elementId in allElementIds)
            {
                Element element = doc.GetElement(elementId);
                if (element == null) continue;

                // Проверяем категорию элемента
                if (IsFloorOrCeiling(element))
                {
                    UpdateElementRoomParameter(doc, element);
                }
            }

                
        }

        private bool IsFloorOrCeiling(Element element)
        {
            return element.Category?.Id.IntegerValue == (int)BuiltInCategory.OST_Floors ||
                   element.Category?.Id.IntegerValue == (int)BuiltInCategory.OST_Ceilings;
        }

        private void UpdateElementRoomParameter(Document doc, Element element)
        {
            Room room = FindRoomForElementFast(doc, element);

            if (room != null)
            {
                Parameter roomParam = element.LookupParameter("N_Отделка.Помещение");
                Parameter roomParam2 = element.LookupParameter("Отделка.Помещение.Назначение");
                Parameter roomParam3 = element.LookupParameter("N_Отделка.ГруппаТекст");

                string roomName = room.get_Parameter(BuiltInParameter.ROOM_NAME).AsString();
                string roomNazn = room.LookupParameter("Назначение").AsString();
                string roomGroup = room.LookupParameter("N_Отделка.Группа").AsInteger().ToString();

                string currentValue = roomParam.AsString();
                if (currentValue != roomName)
                {
                    roomParam.Set(roomName);
                }

                string currentValue2 = roomParam2.AsString();
                if (currentValue2 != roomNazn)
                {
                    roomParam2.Set(roomNazn);
                }

                string currentValue3 = roomParam3.AsString();
                if (currentValue3 != roomGroup)
                {
                    roomParam3.Set(roomGroup);
                }
            }
        }

        private Room FindRoomForElementFast(Document doc, Element element)
        {
            try
            {
                // Получаем уровень элемента
                Level elementLevel = doc.GetElement(element.LevelId) as Level;
                if (elementLevel == null) return null;

                // Получаем фазу элемента
                Phase phase = doc.GetElement(element.CreatedPhaseId) as Phase;
                ElementId phaseId = phase?.Id;

                // Получаем BoundingBox элемента
                BoundingBoxXYZ elementBBox = element.get_BoundingBox(null);
                if (elementBBox == null) return null;

                // Вычисляем центр элемента
                XYZ center = new XYZ(
                    (elementBBox.Min.X + elementBBox.Max.X) / 2,
                    (elementBBox.Min.Y + elementBBox.Max.Y) / 2,
                    (elementBBox.Min.Z + elementBBox.Max.Z) / 2
                );

                // Для потолков смещаем точку вниз, для полов - вверх
                double verticalOffset = element is Ceiling ? -0.5 : 0.5;
                XYZ testPoint = new XYZ(center.X, center.Y, center.Z + verticalOffset);

                // Метод 1: Используем встроенный метод Revit для поиска помещения по точке
                Room room = doc.GetRoomAtPoint(testPoint);
                if (room != null && IsRoomValid(room, elementLevel, phaseId))
                    return room;

                // Метод 2: Если не нашли, пробуем точку без смещения
                room = doc.GetRoomAtPoint(center);
                if (room != null && IsRoomValid(room, elementLevel, phaseId))
                    return room;

                // Метод 3: Ищем помещение с наибольшим пересечением по BoundingBox
                return FindRoomByBoundingBoxIntersection(doc, element, elementLevel, phaseId);
            }
            catch
            {
                return null;
            }
        }

        private bool IsRoomValid(Room room, Level elementLevel, ElementId phaseId)
        {
            return room.LevelId == elementLevel.Id &&
                   room.Area > 0 &&
                   (phaseId == null || room.get_Parameter(BuiltInParameter.ROOM_PHASE).AsElementId() == phaseId);
        }

        private Room FindRoomByBoundingBoxIntersection(Document doc, Element element, Level elementLevel, ElementId phaseId)
        {
            try
            {
                // Получаем ограничивающую рамку элемента
                BoundingBoxXYZ elementBBox = element.get_BoundingBox(null);
                if (elementBBox == null) return null;

                // Получаем все помещения на том же уровне
                List<Room> roomsOnLevel = GetRoomsOnSameLevel(doc, elementLevel, phaseId);

                // Находим помещение с наибольшей площадью пересечения по BoundingBox
                Room bestRoom = null;
                double maxIntersectionArea = 0;

                foreach (Room room in roomsOnLevel)
                {
                    BoundingBoxXYZ roomBBox = room.get_BoundingBox(null);
                    if (roomBBox == null) continue;

                    double intersectionArea = CalculateBoundingBoxIntersectionArea(roomBBox, elementBBox);
                    if (intersectionArea > maxIntersectionArea)
                    {
                        maxIntersectionArea = intersectionArea;
                        bestRoom = room;
                    }
                }

                return bestRoom;
            }
            catch
            {
                return null;
            }
        }

        private List<Room> GetRoomsOnSameLevel(Document doc, Level elementLevel, ElementId phaseId)
        {
            List<Room> rooms = new List<Room>();

            try
            {
                // Используем более быстрый фильтр для получения помещений
                FilteredElementCollector collector = new FilteredElementCollector(doc);
                collector.OfCategory(BuiltInCategory.OST_Rooms);

                foreach (Element elem in collector)
                {
                    Room room = elem as Room;
                    if (room != null &&
                        room.LevelId == elementLevel.Id &&
                        room.Area > 0 &&
                        (phaseId == null || room.get_Parameter(BuiltInParameter.ROOM_PHASE).AsElementId() == phaseId))
                    {
                        rooms.Add(room);
                    }
                }
            }
            catch
            {
                // В случае ошибки возвращаем пустой список
            }

            return rooms;
        }

        private double CalculateBoundingBoxIntersectionArea(BoundingBoxXYZ roomBBox, BoundingBoxXYZ elementBBox)
        {
            // Вычисляем пересечение по X и Y
            double xOverlap = Math.Max(0,
                Math.Min(roomBBox.Max.X, elementBBox.Max.X) -
                Math.Max(roomBBox.Min.X, elementBBox.Min.X));

            double yOverlap = Math.Max(0,
                Math.Min(roomBBox.Max.Y, elementBBox.Max.Y) -
                Math.Max(roomBBox.Min.Y, elementBBox.Min.Y));

            return xOverlap * yOverlap; // Приблизительная площадь пересечения
        }

        public string GetAdditionalInformation() => "Обновляет имя помещения для перекрытий и потолков";
        public ChangePriority GetChangePriority() => ChangePriority.FloorsRoofsStructuralWalls;
        public UpdaterId GetUpdaterId() => m_updaterId;
        public string GetUpdaterName() => "Room Elements Updater";
    }

}
