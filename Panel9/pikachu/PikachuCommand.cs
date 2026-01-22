using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using TNov.main;

namespace TNov
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class PikachuCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            string TNovClassName = "Расстановщик СС ПС"; DateTime dateTime = DateTime.Now;
            //подключение приложения и документа
            if (RevitAPI.UiApplication == null) { RevitAPI.Initialize(commandData); }
            UIDocument uidoc = RevitAPI.UiDocument; Document doc = RevitAPI.Document;
            UIApplication uiApp = RevitAPI.UiApplication; Autodesk.Revit.ApplicationServices.Application rvtApp = uiApp.Application;

            //проверка подключения, запись в журнал
            bool check = false; servercheck sc = new servercheck(in TNovClassName, out check); if (check == false) { return Result.Failed; }

            // создание log - файла
            Logger.Initialize(TNovClassName);


            

            try
            {
                Logger.Log("1. Выбор связанного файла", 1);
                // 1. Выбор связанного файла
                LinkSelectionForm linkForm = new LinkSelectionForm(doc);
                if (linkForm.ShowDialog() != DialogResult.OK || linkForm.SelectedLink == null)
                {
                    Logger.Log("Операция отменена пользователем. Завершение работы.", 3); return Result.Cancelled;
                }

                RevitLinkInstance selectedLink = linkForm.SelectedLink;
                Document linkDoc = selectedLink.GetLinkDocument();
                Logger.Log("Выбран связанный файл "+linkDoc.Title, 1);

                Logger.Log("2. Выбор уровня в связанном файле", 1);
                // 2. Выбор уровня в связанном файле (НОВЫЙ ШАГ)
                LevelSelectionForm levelForm = new LevelSelectionForm(linkDoc);
                if (levelForm.ShowDialog() != DialogResult.OK || levelForm.SelectedLevel == null)
                {
                    Logger.Log("Операция отменена пользователем. Завершение работы.", 3); return Result.Cancelled;
                }

                Level selectedLevel = levelForm.SelectedLevel;
                Logger.Log("Выбран уровень " + selectedLevel.Name, 1);

                Logger.Log("3. Выбор семейства из связанного файла (Семейство А)", 1);
                // 3. Выбор семейства из связанного файла (Семейство А)
                LinkedFamilySelectionForm familyAForm = new LinkedFamilySelectionForm(linkDoc, selectedLevel);
                if (familyAForm.ShowDialog() != DialogResult.OK || familyAForm.SelectedFamily == null)
                {
                    Logger.Log("Операция отменена пользователем. Завершение работы.", 3); return Result.Cancelled;
                }

                Family familyA = familyAForm.SelectedFamily;
                Logger.Log("Выбрано семейство " + familyA.Name + " " + familyA.Id.IntegerValue.ToString(), 1);
                int instanceCountOnLevel = familyAForm.InstanceCountOnLevel;

                Logger.Log("4. Выбор семейства из текущего файла (Семейство Б)", 1);
                // 4. Выбор семейства из текущего файла (Семейство Б)
                CurrentFamilySelectionForm familyBForm = new CurrentFamilySelectionForm(doc);
                if (familyBForm.ShowDialog() != DialogResult.OK || familyBForm.SelectedFamily == null)
                {
                    Logger.Log("Операция отменена пользователем. Завершение работы.", 3); return Result.Cancelled;
                }
                
                Family familyB = familyBForm.SelectedFamily;
                Logger.Log("Выбрано семейство " + familyB.Name + " " + familyB.Id.IntegerValue.ToString(), 1);

                Logger.Log("5. Настройка расстояния", 1);
                // 5. Настройка расстояния
                DistanceSettingsForm distanceForm = new DistanceSettingsForm();
                if (distanceForm.ShowDialog() != DialogResult.OK)
                {
                    Logger.Log("Операция отменена пользователем. Завершение работы.", 3); return Result.Cancelled;
                }

                double distance = distanceForm.Distance; Logger.Log("Расстояние: " + distance.ToString(), 1);
                XYZ direction = distanceForm.Direction;

                // Проверка на слишком малое расстояние
                if (Math.Abs(distance) < 0.001)
                {
                    TaskDialog.Show("Внимание",
                        "Расстояние слишком мало! Элементы будут размещены в одной точке.\n" +
                        "Рекомендуется использовать расстояние не менее 0.01 м.");
                    Logger.Log("   Элементы будут размещены в одной точке", 1);
                }

                Logger.Log("6. Подтверждение операции", 1);
                // 6. Подтверждение операции (используем количество на этаже)
                ConfirmationForm confirmForm = new ConfirmationForm(
                    familyA.Name,
                    familyB.Name,
                    instanceCountOnLevel, // Используем количество на этаже
                    $"{distance} м",
                    selectedLevel?.Name ?? "Не определен"
                );

                if (confirmForm.ShowDialog() != DialogResult.OK || !confirmForm.UserConfirmed)
                {
                    return Result.Cancelled;
                }

                // 7. Выполнение размещения
                Transaction transaction = new Transaction(doc, "Pikachu Plugin - Размещение элементов");
                transaction.Start();

                List<ElementId> createdElements = PlaceElementsNearLinkedInstances(
                    doc, linkDoc, selectedLink, familyA, familyB, distance, direction, selectedLevel);

                transaction.Commit();

                // 8. Показ результатов
                ResultForm resultForm = new ResultForm(createdElements);
                resultForm.ShowDialog();

                Logger.Log("Завершение работы", 5);
                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                message = $"Ошибка: {ex.Message}";
                new infowindow280(message).ShowDialog();
                Logger.Log(message, 4);
                return Result.Failed;
            }
        }

        private List<ElementId> PlaceElementsNearLinkedInstances(
           Document doc,
            Document linkDoc,
            RevitLinkInstance linkInstance,
            Family familyA,
            Family familyB,
            double distance,
            XYZ direction,
            Level selectedLevel)
        {
            List<ElementId> createdElements = new List<ElementId>();

            // Находим все экземпляры Семейства А в связанном файле
            var instancesA = new FilteredElementCollector(linkDoc)
                .OfClass(typeof(FamilyInstance))
                .Where(x => ((FamilyInstance)x).Symbol.Family.Id == familyA.Id)
                .Cast<FamilyInstance>();

            // Фильтруем по уровню, если он выбран
            if (selectedLevel != null)
            {
                instancesA = instancesA.Where(i => i.LevelId == selectedLevel.Id);
            }

            var instancesAList = instancesA.ToList();

            // Получаем символы Семейства Б из текущего файла
            var symbolsB = familyB.GetFamilySymbolIds()
                .Select(id => doc.GetElement(id) as FamilySymbol)
                .Where(sym => sym != null && sym.IsActive)
                .ToList();

            if (!symbolsB.Any())
            {
                throw new Exception("В выбранном семействе нет активных типов!");
            }

            FamilySymbol symbolToUse = symbolsB.First();

            // Проверяем, есть ли у символа параметр "Уровень"
            bool requiresLevel = symbolToUse.get_Parameter(BuiltInParameter.FAMILY_LEVEL_PARAM) != null;

            // Преобразование координат из связанного файла в текущий
            Transform linkTransform = linkInstance.GetTotalTransform();

            int placedCount = 0;
            int errorCount = 0;

            foreach (FamilyInstance instanceA in instancesAList)
            {
                Logger.Log("Исходный элемент "+instanceA.Name+" "+instanceA.Id.IntegerValue.ToString(),2); //расширенные логи
                try
                {
                    // Получаем позицию экземпляра в связанном файле
                    LocationPoint location = instanceA.Location as LocationPoint;
                    if (location == null) { Logger.Log("   положение не определено", 2); continue; }

                    XYZ instancePosition = location.Point;

                    // Преобразуем координаты в текущий файл
                    XYZ transformedPosition = linkTransform.OfPoint(instancePosition);

                    // Размещаем с учетом направления и расстояния
                    XYZ placementPoint = transformedPosition + (direction * distance);

                    // Определяем уровень для размещения (используем тот же уровень, что и в связанном файле)
                    Level placementLevel = FindCorrespondingLevel(doc, selectedLevel, placementPoint.Z);


                    // Размещаем элемент
                    FamilyInstance newInstance;

                    if (requiresLevel && placementLevel != null)
                    {
                        newInstance = doc.Create.NewFamilyInstance(
                            placementPoint,
                            symbolToUse,
                            placementLevel,
                            Autodesk.Revit.DB.Structure.StructuralType.NonStructural
                        );
                    }
                    else
                    {
                        newInstance = doc.Create.NewFamilyInstance(
                            placementPoint,
                            symbolToUse,
                            Autodesk.Revit.DB.Structure.StructuralType.NonStructural
                        );

                        // Пытаемся установить уровень вручную, если возможно
                        if (placementLevel != null && newInstance.LevelId != null)
                        {
                            Parameter levelParam = newInstance.get_Parameter(BuiltInParameter.FAMILY_LEVEL_PARAM);
                            if (levelParam != null && levelParam.IsReadOnly == false)
                            {
                                levelParam.Set(placementLevel.Id);
                            }
                        }
                    }


                    if (newInstance != null)
                    {
                        // Записываем ID исходного элемента в параметр "Комментарии"
                        SetSourceIdComment(newInstance, instanceA.Id);

                        createdElements.Add(newInstance.Id);
                        placedCount++;
                        Logger.Log("   создан элемент "+newInstance.Name+" "+newInstance.Id.IntegerValue.ToString(), 2);
                    }
                }
                catch (Exception ex)
                {
                    Logger.Log("Исходный элемент " + instanceA.Name + " " + instanceA.Id.IntegerValue.ToString() + "ошибка: "
                        + ex.Message, 4);

                    errorCount++;
                    System.Diagnostics.Debug.WriteLine($"Ошибка размещения элемента {instanceA.Id}: {ex.Message}");

                    if (errorCount > 10)
                    {
                        TaskDialog.Show("Внимание",
                            $"Слишком много ошибок ({errorCount}). Прекращаем размещение.\n" +
                            $"Успешно размещено: {placedCount} элементов.");
                        break;
                    }
                     
                }
            }

            return createdElements;
        }
        private Level FindCorrespondingLevel(Document doc, Level sourceLevel, double elevation)
        {
            if (sourceLevel == null)
            {
                // Ищем ближайший уровень по отметке
                var levels = new FilteredElementCollector(doc)
                    .OfClass(typeof(Level))
                    .Cast<Level>()
                    .OrderBy(l => Math.Abs(l.Elevation - elevation))
                    .ToList();

                return levels.FirstOrDefault();
            }

            // Ищем уровень с таким же именем
            var levelByName = new FilteredElementCollector(doc)
                .OfClass(typeof(Level))
                .Cast<Level>()
                .FirstOrDefault(l => l.Name == sourceLevel.Name);

            if (levelByName != null) return levelByName;

            // Ищем уровень с близкой отметкой
            var levelByElevation = new FilteredElementCollector(doc)
                .OfClass(typeof(Level))
                .Cast<Level>()
                .OrderBy(l => Math.Abs(l.Elevation - sourceLevel.Elevation))
                .FirstOrDefault();

            return levelByElevation;
        }

        private void SetSourceIdComment(FamilyInstance newInstance, ElementId sourceId)
        {
            try
            {
                // Пытаемся найти параметр "Комментарии"
                Parameter commentParam = newInstance.LookupParameter("Комментарии");
                if (commentParam == null)
                {
                    // Пробуем английское название
                    commentParam = newInstance.LookupParameter("Comments");
                }

                if (commentParam != null && !commentParam.IsReadOnly)
                {
                    // Записываем ID исходного элемента
                    string comment = $"ID исходного элемента: {sourceId}";
                    commentParam.Set(comment);
                }
                else
                {
                    // Если параметр не найден, создаем shared parameter или используем другой
                    Parameter noteParam = newInstance.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS);
                    if (noteParam != null && !noteParam.IsReadOnly)
                    {
                        noteParam.Set($"Источник: {sourceId}");
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Не удалось записать ID источника: {ex.Message}");
            }
        }
    }
}