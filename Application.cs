#region Ссылки
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Events;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Events;
using CleanLinks.Commands;
using LevelMover.Commands;
using Newtonsoft.Json;
using QOVETER.Commands;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Principal;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using TNovBeams;
using TNovBIMUtils;
using TNovCommon.Help;
using TNovCommon;
using TNovElectrical;
using TNovFinishing;
using TNovMEPSpec;
using TNovParking;
using TNovPiles;
using TNovRooms;
using TNovSS;
using SchemeBuilder.Commands;
using TNovTasks;
using TNovUtils;
using TNovUtils.Checklist.Commands;
using TNovUtils.Issues.Commands;
using TNovUtils.Issues.ModelSync;
using TNovUtils.LinkWorksets;
using TNovUtilsAR;
using TNovUtilsST;
using TNovVent;
using TNovViewsSheets;
using adWin = Autodesk.Windows;
using ComboBox = Autodesk.Revit.UI.ComboBox;
using RibbonItem = Autodesk.Revit.UI.RibbonItem;
using RibbonPanel = Autodesk.Revit.UI.RibbonPanel;
using SplitButton = Autodesk.Revit.UI.SplitButton;
using TypeFilter = TNovUtils.TypeFilter;

#endregion

namespace TNov
{
    [Regeneration(RegenerationOption.Manual)]
    internal class Application : IExternalApplication
    {
        static Application()
        {
            // Preserialized .resx ссылаются на System.Resources.Extensions 4.0.0.0,
            // а NuGet-пакет может иметь другую AssemblyVersion. Revit не применяет binding redirect.
            AppDomain.CurrentDomain.AssemblyResolve += (_, args) =>
            {
                if (new AssemblyName(args.Name).Name != "System.Resources.Extensions")
                    return null;

                string path = Path.Combine(
                    Path.GetDirectoryName(typeof(Application).Assembly.Location) ?? string.Empty,
                    "System.Resources.Extensions.dll");
                return File.Exists(path) ? Assembly.LoadFrom(path) : null;
            };
        }

        #region Переменные класса
        static AddInId addinId = new AddInId(new Guid("83403DB6-EA74-4E10-85B3-508AE241A743"));

        private DateTime? _startTime = null;
        public static Application ThisApp { get; private set; }
        private BasicFileInfo info;
        //параметры запретных команд
        private bool _canPurge;
        private bool _canCreateParts;
        private AddInCommandBinding _purgeBinding;
        private AddInCommandBinding _partsBinding;
        private bool _purgeExecutedSubscribed = false;
        private bool _partsExecutedSubscribed = false;
        //параметры раскраски вкладок
        private string syncOption = "Подсветка 20/30 минут";
        private int time1 = 0;
        private int time2 = 0;
        private readonly Dictionary<Document, Stopwatch> _docStopwatches = new Dictionary<Document, Stopwatch>();
        private Document _activeDocument;
        private enum PanelColorState { None, Gold, IndianRed }
        private PanelColorState _currentColor = PanelColorState.None;
        private static readonly SolidColorBrush BrushGold = new SolidColorBrush(Colors.Gold);
        private static readonly SolidColorBrush BrushIndianRed = new SolidColorBrush(Colors.IndianRed);
        //параметры переключения ленты
        private static List<RibbonPanel> _CommonRibbonItems = new List<RibbonPanel>();
        private static List<RibbonPanel> _ARRibbonItems = new List<RibbonPanel>();
        private static List<RibbonPanel> _STRibbonItems = new List<RibbonPanel>();
        private static List<RibbonPanel> _MEPRibbonItems = new List<RibbonPanel>();
        private static List<RibbonPanel> _BIMRibbonItems = new List<RibbonPanel>();
        private static List<RibbonPanel> _TestRibbonItems = new List<RibbonPanel>();
        private ComboBox _comboBox;
        private BitmapSource _ribbonTabIconSource;
        private readonly Stopwatch _ribbonTabIconWatch = new Stopwatch();
        private const string RibbonTabName = "TNov";
        private const string RibbonTabIconName = "TNovRibbonTabIcon";
        private const int RibbonTabIconCheckMs = 500;

        TNovConfig _config = new TNovConfig();
        static string clientFolderPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "TNovClient");
        static string serverPath = clientFolderPath; //"//fs-nova/Distr/0.For Admin/_TNov/"
        /// <summary>
        /// Клиент с этой версии обновляет себя сам. Должно совпадать с ClientSelfUpdate.SelfUpdateSince.
        /// Пока локальный клиент старше — TNov по-прежнему делает Kill + copy.
        /// </summary>
        static readonly Version MinSelfUpdatingClientVersion = new Version(2, 5, 0, 0);
        #endregion
        public Result OnStartup(UIControlledApplication application)
        {
            #region Конфигурация и настройки программы
            //конфиг
            _config = LoadConfig();
            if (_config == null)
            {
                // Нечитаемый TNovConfig.json раньше ронял весь плагин (NullReferenceException ниже).
                // Работаем без корпоративных функций и говорим пользователю в первый Idling.
                _config = new TNovConfig();
                _startupMessage = "Не удалось прочитать настройки TNov: " +
                    Path.Combine(clientFolderPath, "TNovConfig.json") +
                    "\nПроверьте файл (формат JSON) или удалите его — TNovClient создаст новый. " +
                    "До исправления корпоративные функции плагина отключены.";
            }
            // Досылка журналов, не ушедших на сервер в прошлых сессиях.
            TNovCommon.Server.ServerOutbox.Start();
            // Фоновое чтение общих настроек TNovApi ({ServerPath}tnovapi.json), чтобы первая команда их уже видела.
            TNovConfigLoad.GetCachedConfig();
            if (_config.LicenseType != null)
            {
                Debug.WriteLine($"Конфигурация загружена: LicenseType={_config.LicenseType}, CorpName={_config.CorpName}, ServerPath={_config.ServerPath}");
                if(_config.LicenseType=="corp") serverPath = _config.ServerPath;
                serverPath = serverPath.Replace('/', '\\');
                // Локальный путь с буквой диска (тестовая папка) не превращаем в UNC.
                if (!serverPath.StartsWith(@"\\") && !Path.IsPathRooted(serverPath))
                    serverPath = @"\\" + serverPath.TrimStart('/');
            }
            // «Модель» TNovPRO: ключ синхронизации лежит на корпоративной папке раздачи —
            // синхронизация работает у всех, у кого стоит плагин, без входа в TNovPRO.
            try { ModelSyncService.Configure(_config.LicenseType == "corp" ? serverPath : null); } catch { }
            //настройки программы
            var viewModel0 = new AppVersionViewModel();
            string jsonpath = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "TNovClient/TNovSettings.json");
            try
            {
                viewModel0 = JsonConvert.DeserializeObject<AppVersionViewModel>(File.ReadAllText(jsonpath));
            }
            catch (Exception) { }
            #endregion
            #region Запретные кнопки
            _canPurge = viewModel0.canPurge;
            _canCreateParts = viewModel0.canCreateParts;
            RevitCommandId purgeCmdId = RevitCommandId.LookupCommandId("ID_PURGE_UNUSED");
            _purgeBinding = application.CreateAddInCommandBinding(purgeCmdId);
            _purgeBinding.CanExecute += (s, e) => e.CanExecute = _canPurge;   // всегда проверяет актуальный флаг
            if (!_canPurge)
            {
                _purgeBinding.Executed += OnPurgeExecuted;
                _purgeExecutedSubscribed = true;
            }
            var partsCmdId = RevitCommandId.LookupPostableCommandId(PostableCommand.CreateParts);
            _partsBinding = application.CreateAddInCommandBinding(partsCmdId);
            _partsBinding.CanExecute += (s, e) => e.CanExecute = _canCreateParts;
            if (!_canCreateParts)
            {
                _partsBinding.Executed += OnPurgeExecuted;
                _partsExecutedSubscribed = true;
            }
            /*
            if (!viewModel0.canPurge)
            {
                // 1. Запрещаем "Удалить неиспользуемые"
                RevitCommandId purgeCmdId = RevitCommandId.LookupCommandId("ID_PURGE_UNUSED");
                var purgeBinding = application.CreateAddInCommandBinding(purgeCmdId);
                purgeBinding.CanExecute += (s, e) => e.CanExecute = false;
                purgeBinding.Executed += OnPurgeExecuted;
            }
            if (!viewModel0.canCreateParts)
            {
                // 2. Запрещаем "Создать части"
                var partsCmdId = RevitCommandId.LookupPostableCommandId(PostableCommand.CreateParts);
                var partsBinding = application.CreateAddInCommandBinding(partsCmdId);
                partsBinding.CanExecute += (s, e) => e.CanExecute = false;
                partsBinding.Executed += OnPurgeExecuted;
            }*/
            #endregion
            #region События
            //Регистрация событий
            try
            {
                application.ControlledApplication.DocumentOpening += new EventHandler<DocumentOpeningEventArgs>(OnDocumentOpening);
                application.ControlledApplication.DocumentOpened += new EventHandler<DocumentOpenedEventArgs>(OnDocumentOpened);
                application.ControlledApplication.DocumentSynchronizingWithCentral += new EventHandler<DocumentSynchronizingWithCentralEventArgs>(OnSyncCentralStart);
                application.ControlledApplication.DocumentSynchronizedWithCentral += new EventHandler<DocumentSynchronizedWithCentralEventArgs>(OnSyncCentralEnd);
                application.ControlledApplication.DocumentClosing += new EventHandler<DocumentClosingEventArgs>(OnDocumentClosing);
                // «Модель» TNovPRO: учёт изменений и отправка на сайт при синхронизации.
                application.ControlledApplication.DocumentChanged += ModelSyncService.OnDocumentChanged;
                application.ControlledApplication.DocumentSaved += OnDocumentSavedForTNovPro;
                application.Idling += OnIdling;
                application.ViewActivated += OnViewActivated;
                application.ControlledApplication.DocumentCreated += OnDocumentCreated;
                application.DialogBoxShowing += new EventHandler<DialogBoxShowingEventArgs>(a_DialogBoxShowing);
#if R2027
                application.ThemeChanged += OnThemeChanged;
#endif
            }
            catch (Exception) { }
            #endregion
            #region Панель справки
            // Регистрируем до построения ленты: Revit восстанавливает раскладку докинга
            // на старте, поэтому поздняя регистрация оставит панель вне раскладки сессии.
            // Исключение наружу выпускать нельзя — оно оборвёт построение всей ленты.
            try
            {
                // OnStartup идёт на UI-потоке Revit — именно этот диспетчер нужен автоконтексту.
                HelpPaneHost.Initialize(System.Windows.Threading.Dispatcher.CurrentDispatcher);

                if (!DockablePane.PaneIsRegistered(HelpPaneIds.Help))
                    application.RegisterDockablePane(HelpPaneIds.Help, HelpPaneIds.Title, new HelpPaneProvider());
            }
            catch (Exception) { }
            #endregion
            #region Раскраска вкладок
            //Подгрузка настроек времени раскраски вкладок
            ThisApp = this;
            LoadSettings();
            #endregion
            #region Revit.ini
            //Проверка ключей в файле revit.ini
            try
            {
                string revitVersion = application.ControlledApplication.VersionNumber;
                string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                string revitIniPath = Path.Combine(appDataPath, "Autodesk", "Revit", $"Autodesk Revit {revitVersion}", "revit.ini");

                if (File.Exists(revitIniPath))
                {
                    // Определяем кодировку файла
                    Encoding encoding = DetectEncoding(revitIniPath);

                    // Читаем все строки с определённой кодировкой
                    string[] lines = File.ReadAllLines(revitIniPath, encoding);
                    bool changed = false;
                    bool messagesSectionFound = false;

                    for (int i = 0; i < lines.Length; i++)
                    {
                        if (lines[i].StartsWith("[Messages]"))
                        {
                            messagesSectionFound = true;
                            // Ищем ключи в этой секции
                            int j = i + 1;
                            while (j < lines.Length && !lines[j].StartsWith("["))
                            {
                                if (lines[j].StartsWith("SuppressConfirmLevelRename="))
                                {
                                    string val = lines[j].Substring(lines[j].IndexOf('=') + 1);
                                    if (val != "7")
                                    {
                                        lines[j] = "SuppressConfirmLevelRename=7";
                                        changed = true;
                                    }
                                }
                                else if (lines[j].StartsWith("SuppressConfirmPlanViewRename="))
                                {
                                    string val = lines[j].Substring(lines[j].IndexOf('=') + 1);
                                    if (val != "7")
                                    {
                                        lines[j] = "SuppressConfirmPlanViewRename=7";
                                        changed = true;
                                    }
                                }
                                j++;
                            }

                            // Если ключи не найдены, добавляем их в конец секции
                            bool levelFound = false;
                            bool viewFound = false;
                            for (int k = i + 1; k < j; k++)
                            {
                                if (lines[k].StartsWith("SuppressConfirmLevelRename=")) levelFound = true;
                                if (lines[k].StartsWith("SuppressConfirmPlanViewRename=")) viewFound = true;
                            }
                            if (!levelFound)
                            {
                                // Вставляем новую строку перед закрытием секции (перед j, который указывает на следующую секцию или конец)
                                Array.Resize(ref lines, lines.Length + 1);
                                for (int k = lines.Length - 1; k > j; k--)
                                    lines[k] = lines[k - 1];
                                lines[j] = "SuppressConfirmLevelRename=7";
                                changed = true;
                                j++; // увеличиваем, так как добавили строку
                            }
                            if (!viewFound)
                            {
                                Array.Resize(ref lines, lines.Length + 1);
                                for (int k = lines.Length - 1; k > j; k--)
                                    lines[k] = lines[k - 1];
                                lines[j] = "SuppressConfirmPlanViewRename=7";
                                changed = true;
                            }
                            break; // секция обработана
                        }
                    }

                    // Если секции [Messages] нет вообще, добавляем её в конец файла с нужными ключами
                    if (!messagesSectionFound)
                    {
                        var newLines = new System.Collections.Generic.List<string>(lines);
                        newLines.Add("[Messages]");
                        newLines.Add("SuppressConfirmLevelRename=7");
                        newLines.Add("SuppressConfirmPlanViewRename=7");
                        lines = newLines.ToArray();
                        changed = true;
                    }

                    if (changed)
                    {
                        File.WriteAllLines(revitIniPath, lines, encoding);
                    }
                }
            }
            catch (Exception ex)
            {
                // Можно залогировать ошибку, но не прерываем запуск Revit
                new InfoWindow280($"Ошибка при изменении файла revit.ini: {ex.Message}").Show();
            }
            #endregion
            #region Апдейтеры
            //фильтры для апдейтеров

            ElementCategoryFilter filterGM = new ElementCategoryFilter(BuiltInCategory.OST_GenericModel);
            ElementCategoryFilter filterWalls = new ElementCategoryFilter(BuiltInCategory.OST_Walls);
            ElementCategoryFilter filterF = new ElementCategoryFilter(BuiltInCategory.OST_StructuralFoundation);
            ElementCategoryFilter filterFloors = new ElementCategoryFilter(BuiltInCategory.OST_Floors);
            ElementCategoryFilter filterCT = new ElementCategoryFilter(BuiltInCategory.OST_CableTray);
            ElementCategoryFilter filterDuct = new ElementCategoryFilter(BuiltInCategory.OST_DuctCurves);
            ElementCategoryFilter filterObor = new ElementCategoryFilter(BuiltInCategory.OST_MechanicalEquipment);
            ElementCategoryFilter filterPipe = new ElementCategoryFilter(BuiltInCategory.OST_PipeCurves);
            ElementCategoryFilter filterGrid = new ElementCategoryFilter(BuiltInCategory.OST_Grids);
            ElementCategoryFilter filterLevel = new ElementCategoryFilter(BuiltInCategory.OST_Levels);
            ElementCategoryFilter filterRebar = new ElementCategoryFilter(BuiltInCategory.OST_Rebar);
            ElementCategoryFilter filterKorob = new ElementCategoryFilter(BuiltInCategory.OST_Conduit);
            ElementCategoryFilter filterLight = new ElementCategoryFilter(BuiltInCategory.OST_LightingDevices);
            ElementCategoryFilter filterLightF = new ElementCategoryFilter(BuiltInCategory.OST_LightingFixtures);
            ElementCategoryFilter filterElEq = new ElementCategoryFilter(BuiltInCategory.OST_ElectricalEquipment);
            ElementCategoryFilter filterLinks = new ElementCategoryFilter(BuiltInCategory.OST_RvtLinks);
            ElementCategoryFilter filterFound = new ElementCategoryFilter(BuiltInCategory.OST_StructuralFoundation);
            ElementCategoryFilter filterGroups = new ElementCategoryFilter(BuiltInCategory.OST_IOSModelGroups);
            ElementCategoryFilter filterCeilings = new ElementCategoryFilter(BuiltInCategory.OST_Ceilings);
            ElementCategoryFilter filterRooms = new ElementCategoryFilter(BuiltInCategory.OST_Rooms);
            ElementCategoryFilter filterPipeInsulations = new ElementCategoryFilter(BuiltInCategory.OST_PipeInsulations);
            ElementCategoryFilter filterDuctInsulations = new ElementCategoryFilter(BuiltInCategory.OST_DuctInsulations);
            ElementCategoryFilter filterDuctLining = new ElementCategoryFilter(BuiltInCategory.OST_DuctLinings);
            ElementFilter combinedFilterST = CombinedElementFilter.CombinedFilterST();
            ElementFilter combinedFilterOVVK = CombinedElementFilter.CombinedFilterOVVK();
            ElementFilter combinedFilterAR = CombinedElementFilter.CombinedFilterAR();

            //объявление апдейтеров
            
            TNovHoleUpdater holeUpdater = new TNovHoleUpdater(application.ActiveAddInId); //отверстия
            UpdaterRegistry.RegisterUpdater(holeUpdater, true);
            UpdaterRegistry.AddTrigger(holeUpdater.GetUpdaterId(), filterGM, Element.GetChangeTypeAny());

            TNovShaftUpdater shaftUpdater = new TNovShaftUpdater(application.ActiveAddInId); //другие задания
            UpdaterRegistry.RegisterUpdater(shaftUpdater, true);
            UpdaterRegistry.AddTrigger(shaftUpdater.GetUpdaterId(), filterGM, Element.GetChangeTypeAny());

            TNovWorksetUpdater worksetUpdater = new TNovWorksetUpdater(application.ActiveAddInId); //рабочие наборы
            UpdaterRegistry.RegisterUpdater(worksetUpdater, true);
            UpdaterRegistry.AddTrigger(worksetUpdater.GetUpdaterId(), filterGM, Element.GetChangeTypeAny());
            UpdaterRegistry.AddTrigger(worksetUpdater.GetUpdaterId(), filterWalls, Element.GetChangeTypeAny());
            UpdaterRegistry.AddTrigger(worksetUpdater.GetUpdaterId(), filterF, Element.GetChangeTypeAny());
            UpdaterRegistry.AddTrigger(worksetUpdater.GetUpdaterId(), filterFloors, Element.GetChangeTypeAny());
            UpdaterRegistry.AddTrigger(worksetUpdater.GetUpdaterId(), filterCT, Element.GetChangeTypeAny());
            UpdaterRegistry.AddTrigger(worksetUpdater.GetUpdaterId(), filterDuct, Element.GetChangeTypeAny());
            UpdaterRegistry.AddTrigger(worksetUpdater.GetUpdaterId(), filterObor, Element.GetChangeTypeAny());
            UpdaterRegistry.AddTrigger(worksetUpdater.GetUpdaterId(), filterPipe, Element.GetChangeTypeAny());
            UpdaterRegistry.AddTrigger(worksetUpdater.GetUpdaterId(), filterGrid, Element.GetChangeTypeAny());
            UpdaterRegistry.AddTrigger(worksetUpdater.GetUpdaterId(), filterLevel, Element.GetChangeTypeAny());
            UpdaterRegistry.AddTrigger(worksetUpdater.GetUpdaterId(), filterRebar, Element.GetChangeTypeAny());
            UpdaterRegistry.AddTrigger(worksetUpdater.GetUpdaterId(), filterKorob, Element.GetChangeTypeAny());
            UpdaterRegistry.AddTrigger(worksetUpdater.GetUpdaterId(), filterLight, Element.GetChangeTypeAny());
            UpdaterRegistry.AddTrigger(worksetUpdater.GetUpdaterId(), filterLightF, Element.GetChangeTypeAny());
            UpdaterRegistry.AddTrigger(worksetUpdater.GetUpdaterId(), filterElEq, Element.GetChangeTypeAny());
            UpdaterRegistry.AddTrigger(worksetUpdater.GetUpdaterId(), filterLinks, Element.GetChangeTypeAny());
            UpdaterRegistry.AddTrigger(worksetUpdater.GetUpdaterId(), filterFound, Element.GetChangeTypeAny());
            
            TNovPinUpdater pinUpdater = new TNovPinUpdater(application.ActiveAddInId); //закрепление связей
            UpdaterRegistry.RegisterUpdater(pinUpdater, true);
            UpdaterRegistry.AddTrigger(pinUpdater.GetUpdaterId(), filterLinks, Element.GetChangeTypeElementAddition());
            
            TNovPileUpdater pileUpdater = new TNovPileUpdater(application.ActiveAddInId); //отметки свай
            UpdaterRegistry.RegisterUpdater(pileUpdater,true);
            UpdaterRegistry.AddTrigger(pileUpdater.GetUpdaterId(), filterFound, Element.GetChangeTypeAny());

            TNovTaskUpdater taskUpdater = new TNovTaskUpdater(application.ActiveAddInId); //задания
            UpdaterRegistry.RegisterUpdater(taskUpdater, true);
            UpdaterRegistry.AddTrigger(taskUpdater.GetUpdaterId(), filterGroups, Element.GetChangeTypeAny());
            
            TNovWallUpdater wallUpdater = new TNovWallUpdater(application.ActiveAddInId); //отделка стен
            UpdaterRegistry.RegisterUpdater(wallUpdater, true);
            UpdaterRegistry.AddTrigger(wallUpdater.GetUpdaterId(), filterWalls, Element.GetChangeTypeElementAddition());
            UpdaterRegistry.AddTrigger(wallUpdater.GetUpdaterId(), filterWalls, Element.GetChangeTypeAny());
            
            TNovRoomUpdater roomUpdater = new TNovRoomUpdater(application.ActiveAddInId); //помещения
            UpdaterRegistry.RegisterUpdater(roomUpdater, true);
            UpdaterRegistry.AddTrigger(roomUpdater.GetUpdaterId(), filterRooms, Element.GetChangeTypeElementAddition());
            UpdaterRegistry.AddTrigger(roomUpdater.GetUpdaterId(), filterRooms, Element.GetChangeTypeAny());
            
            TNovFloorCeilingUpdater floorCeilingUpdater = new TNovFloorCeilingUpdater(application.ActiveAddInId); //отделка полов потолков
            UpdaterRegistry.RegisterUpdater(floorCeilingUpdater, true);
            UpdaterRegistry.AddTrigger(floorCeilingUpdater.GetUpdaterId(), filterFloors, Element.GetChangeTypeElementAddition());
            UpdaterRegistry.AddTrigger(floorCeilingUpdater.GetUpdaterId(), filterFloors, Element.GetChangeTypeAny());
            UpdaterRegistry.AddTrigger(floorCeilingUpdater.GetUpdaterId(), filterCeilings, Element.GetChangeTypeElementAddition());
            UpdaterRegistry.AddTrigger(floorCeilingUpdater.GetUpdaterId(), filterCeilings, Element.GetChangeTypeAny());
            
            TNovInsulationUpdater insulationUpdater = new TNovInsulationUpdater(application.ActiveAddInId); //изоляция
            UpdaterRegistry.RegisterUpdater(insulationUpdater, true);
            UpdaterRegistry.AddTrigger(insulationUpdater.GetUpdaterId(), filterPipeInsulations, Element.GetChangeTypeElementAddition());
            UpdaterRegistry.AddTrigger(insulationUpdater.GetUpdaterId(), filterPipeInsulations, Element.GetChangeTypeAny());
            UpdaterRegistry.AddTrigger(insulationUpdater.GetUpdaterId(), filterDuctInsulations, Element.GetChangeTypeElementAddition());
            UpdaterRegistry.AddTrigger(insulationUpdater.GetUpdaterId(), filterDuctInsulations, Element.GetChangeTypeAny());
            UpdaterRegistry.AddTrigger(insulationUpdater.GetUpdaterId(), filterDuctLining, Element.GetChangeTypeElementAddition());
            UpdaterRegistry.AddTrigger(insulationUpdater.GetUpdaterId(), filterDuctLining, Element.GetChangeTypeAny());

            TNovParsOpredSTUpdater parsOpredSTUpdater = new TNovParsOpredSTUpdater(application.ActiveAddInId); //Т Опред КЖ
            UpdaterRegistry.RegisterUpdater(parsOpredSTUpdater, true);
            //UpdaterRegistry.AddTrigger(parsOpredSTUpdater.GetUpdaterId(), combinedFilterST, Element.GetChangeTypeElementAddition());
            UpdaterRegistry.AddTrigger(parsOpredSTUpdater.GetUpdaterId(), combinedFilterST, Element.GetChangeTypeAny());

            TNovParsOVVKUpdater parsOVVKUpdater = new TNovParsOVVKUpdater(application.ActiveAddInId); //Т параметры ОВ ВК
            UpdaterRegistry.RegisterUpdater(parsOVVKUpdater, true);
            //UpdaterRegistry.AddTrigger(parsOVVKUpdater.GetUpdaterId(), combinedFilterOVVK, Element.GetChangeTypeElementAddition());
            UpdaterRegistry.AddTrigger(parsOVVKUpdater.GetUpdaterId(), combinedFilterOVVK, Element.GetChangeTypeAny());

            TNovParsNaimOboznSTUpdater parsNaimOboznSTUpdater = new TNovParsNaimOboznSTUpdater(application.ActiveAddInId); //Т Наим Обозн КЖ
            UpdaterRegistry.RegisterUpdater(parsNaimOboznSTUpdater, true);
            //UpdaterRegistry.AddTrigger(parsOpredSTUpdater.GetUpdaterId(), combinedFilterST, Element.GetChangeTypeElementAddition());
            UpdaterRegistry.AddTrigger(parsNaimOboznSTUpdater.GetUpdaterId(), combinedFilterST, Element.GetChangeTypeAny());

            TNovParsOpredARUpdater parsOpredARUpdater = new TNovParsOpredARUpdater(application.ActiveAddInId); //Т Опред АР
            UpdaterRegistry.RegisterUpdater(parsOpredARUpdater, true);
            UpdaterRegistry.AddTrigger(parsOpredARUpdater.GetUpdaterId(), combinedFilterAR, Element.GetChangeTypeAny());

            TNovSectionNumberUpdater sectionNumberUpdater = new TNovSectionNumberUpdater(application.ActiveAddInId); //Т Номер секции
            UpdaterRegistry.RegisterUpdater(sectionNumberUpdater, true);
            ElementFilter filterSectionNumber = new LogicalOrFilter(new List<ElementFilter>
            {
                // все категории остальных апдейтеров
                filterGM, filterWalls, filterF, filterFloors, filterCT, filterDuct, filterObor, filterPipe,
                filterGrid, filterLevel, filterRebar, filterKorob, filterLight, filterLightF, filterElEq,
                filterLinks, filterFound, filterGroups, filterCeilings, filterRooms,
                filterPipeInsulations, filterDuctInsulations, filterDuctLining,
                combinedFilterST, combinedFilterOVVK, combinedFilterAR
            });
            UpdaterRegistry.AddTrigger(sectionNumberUpdater.GetUpdaterId(), filterSectionNumber, Element.GetChangeTypeAny());

            // Для блокировки плагина сервером (ApplyPluginBlock) — все апдейтеры TNov.
            _updaterIds.AddRange(new[]
            {
                holeUpdater.GetUpdaterId(), shaftUpdater.GetUpdaterId(), worksetUpdater.GetUpdaterId(),
                pinUpdater.GetUpdaterId(), pileUpdater.GetUpdaterId(), taskUpdater.GetUpdaterId(),
                wallUpdater.GetUpdaterId(), roomUpdater.GetUpdaterId(), floorCeilingUpdater.GetUpdaterId(),
                insulationUpdater.GetUpdaterId(), parsOpredSTUpdater.GetUpdaterId(), parsOVVKUpdater.GetUpdaterId(),
                parsNaimOboznSTUpdater.GetUpdaterId(), parsOpredARUpdater.GetUpdaterId(),
                sectionNumberUpdater.GetUpdaterId()
            });
            #endregion
            #region Клиент
            // Старые клиенты (< 2.1.7) обновляет этот блок (Kill + copy).
            // 2.1.4–2.1.6 на шаре могли быть без самообновления — порог 2.1.7.
            // Начиная с 2.1.7 клиент обновляет себя сам; здесь только запуск, если процесса нет.
            EnsureTNovClient();
            #endregion
            
            // Создание вкладок, панелей, кнопок

            string assebblyLocation = Assembly.GetExecutingAssembly().Location, tabName = RibbonTabName;

            application.CreateRibbonTab(tabName);
            // Иконка вкладки применяется в OnIdling (visual tree ленты готов не сразу).

            ContextualHelp mainhelp = new ContextualHelp(ContextualHelpType.Url,
                HelpLinks.GetHelpLink("-"));

            #region Панель "Настройки"

            // Панель "Настройки"

            RibbonPanel panel0 = application.CreateRibbonPanel(tabName, "Настройки");

            ComboBoxData comboData = new ComboBoxData("Режим");


            // кнопка "Настройки"

            PushButtonData buttonDataN = new PushButtonData(nameof(AppVersion), "Настройки", typeof(AppVersion).Assembly.Location, typeof(AppVersion).FullName)
            {
                //LargeImage = GetImageSource(imgN),
                ToolTip = "Глобальные настройки плагина и сведения о программе."
            };
            RibbonIcons.Set(buttonDataN, nameof(Properties.Resources.logomin));

            ContextualHelp settingshelp = new ContextualHelp(ContextualHelpType.Url,
                HelpLinks.GetHelpLink("Настройки"));
            buttonDataN.SetContextualHelp(settingshelp);
                        
            
            // кнопка "Справка"

            PushButtonData buttonDataHelp = new PushButtonData(nameof(ShowHelpPaneCommand), "Справка", typeof(ShowHelpPaneCommand).Assembly.Location, typeof(ShowHelpPaneCommand).FullName)
            {
                ToolTip = "Панель справки по функциям плагина.",
                LongDescription = "Открывает панель со статьями из базы знаний. Панель сама переключается на раздел запущенной функции."
            };
            buttonDataHelp.SetContextualHelp(mainhelp);

            IList<RibbonItem> ribbonItemList0 = panel0.AddStackedItems(buttonDataN, (RibbonItemData)comboData, buttonDataHelp);
            _comboBox = ribbonItemList0[1] as ComboBox;
            _comboBox.AddItem(new ComboBoxMemberData("Все", "Все"));
            _comboBox.AddItem(new ComboBoxMemberData("Общие", "Общие"));
            _comboBox.AddItem(new ComboBoxMemberData("АР", "АР"));
            _comboBox.AddItem(new ComboBoxMemberData("КЖ", "КЖ"));
            _comboBox.AddItem(new ComboBoxMemberData("Сети", "Сети"));
            _comboBox.AddItem(new ComboBoxMemberData("BIM", "BIM"));
            _comboBox.AddItem(new ComboBoxMemberData("Тесты", "Тесты"));
            _comboBox.CurrentChanged += OnComboBoxCurrentChanged; //подписка на событие изменения выбора


            #endregion

            #region Панель "Общее"

            // Панель "Общее"

            RibbonPanel panelСommon = application.CreateRibbonPanel(tabName, "Общее");

            // кнопка "TNovPRO Вопросы"

            PushButtonData buttonDataProQ = new PushButtonData(nameof(ShowIssuesCommand), "TNovPRO\nВопросы", typeof(ShowIssuesCommand).Assembly.Location, typeof(ShowIssuesCommand).FullName)
            {
                ToolTip = "Модуль Вопросы в TNovPRO.",
                LongDescription = "Просмотр замечаний и коллизий, поиск в модели, работа со статусами."
            };
            RibbonIcons.Set(buttonDataProQ, nameof(Properties.Resources.tnovproq16), nameof(Properties.Resources.tnovproq32));
            ContextualHelp issuesHelp = new ContextualHelp(ContextualHelpType.Url,
                HelpLinks.GetHelpLink("Вопросы"));
            buttonDataProQ.SetContextualHelp(issuesHelp);
            panelСommon.AddItem(buttonDataProQ);

            // кнопка "Чек-лист" (TNovUtils)

            PushButtonData buttonDataChecklist = new PushButtonData(nameof(ShowChecklistCommand), "Чек-лист", typeof(ShowChecklistCommand).Assembly.Location, typeof(ShowChecklistCommand).FullName)
            {
                ToolTip = "Чек-лист проверок модели и задач проектировщика."
            };
            RibbonIcons.Set(buttonDataChecklist, nameof(Properties.Resources.checklist16), nameof(Properties.Resources.checklist32));
            ContextualHelp checklistHelp = new ContextualHelp(ContextualHelpType.Url,
                HelpLinks.GetHelpLink("Чек-лист"));
            buttonDataChecklist.SetContextualHelp(checklistHelp);
            panelСommon.AddItem(buttonDataChecklist);

            // сгруппированная кнопка "Журнал синхронизаций"

            PushButtonData buttonDataSyncJournal = new PushButtonData(nameof(SyncJournal), "Журнал\nсинхронизаций", typeof(SyncJournal).Assembly.Location, typeof(SyncJournal).FullName)
            {
                ToolTip = "Журнал синхронизаций текущей модели."
            };
            RibbonIcons.Set(buttonDataSyncJournal, nameof(Properties.Resources.journal16));
            ContextualHelp syncJournalHelp = new ContextualHelp(ContextualHelpType.Url,
                HelpLinks.GetHelpLink("Журнал синхронизаций"));
            buttonDataSyncJournal.SetContextualHelp(syncJournalHelp);

            // сгруппированная кнопка "Журнал заданий"

            PushButtonData buttonDataTasksJournal = new PushButtonData(nameof(TasksJournal), "Журнал\nзаданий", typeof(TasksJournal).Assembly.Location, typeof(TasksJournal).FullName)
            {
                ToolTip = "Журнал выдачи заданий по проектам."
            };
            RibbonIcons.Set(buttonDataTasksJournal, nameof(Properties.Resources.gettask16));
            ContextualHelp tasksJournalHelp = new ContextualHelp(ContextualHelpType.Url,
                HelpLinks.GetHelpLink("Журнал заданий"));
            buttonDataTasksJournal.SetContextualHelp(tasksJournalHelp);
            
            // сгруппированная кнопка "Таблица параметров"

            PushButtonData buttonDataParamTable = new PushButtonData(nameof(ParamTable), "Таблица параметров", typeof(ParamTable).Assembly.Location, typeof(ParamTable).FullName)
            {
                ToolTip = "Открыть таблицу требований к модели."
            };
            RibbonIcons.Set(buttonDataParamTable, nameof(Properties.Resources.ParamTable16));
            buttonDataParamTable.SetContextualHelp(mainhelp);

            // группа кнопок "Журнал синхронизаций", "Журнал заданий", "Таблица параметров"

            panelСommon.AddStackedItems(buttonDataSyncJournal, buttonDataTasksJournal, buttonDataParamTable);

            #endregion

            #region Панель "Виды и листы"

            // Панель "Виды и листы"

            RibbonPanel panelViewsSheets = application.CreateRibbonPanel(tabName, "Виды и листы");
            _CommonRibbonItems.Add(panelViewsSheets);

            // кнопка "Менеджер листов"

            PushButtonData buttonDatasheets = new PushButtonData(nameof(Sheets), "Менеджер\nлистов", typeof(Sheets).Assembly.Location, typeof(Sheets).FullName)
            {
                ToolTip = "Перенумерация листов, формирование комплектов на печать."
            };
            RibbonIcons.Set(buttonDatasheets, nameof(Properties.Resources.sheets16), nameof(Properties.Resources.sheets32));
            ContextualHelp sheetshelp = new ContextualHelp(ContextualHelpType.Url,
                HelpLinks.GetHelpLink("Менеджер листов"));
            buttonDatasheets.SetContextualHelp(sheetshelp);
            panelViewsSheets.AddItem(buttonDatasheets);
            
            
            // сгруппированная кнопка "Изменения"

            PushButtonData buttonDatachanges = new PushButtonData(nameof(Changes), "Изменения", typeof(Changes).Assembly.Location, typeof(Changes).FullName)
            {
                ToolTip = "Менеджер изменений: ревизии проекта, штамп по комплектам, ведомость изменений."
            };
            RibbonIcons.Set(buttonDatachanges, nameof(Properties.Resources.changes16));
            ContextualHelp changeshelp = new ContextualHelp(ContextualHelpType.Url,
                HelpLinks.GetHelpLink("Изменения"));
            buttonDatachanges.SetContextualHelp(changeshelp);

            // подкнопка "Excel"

            PushButtonData buttonDataexcel = new PushButtonData(nameof(Excel), "Excel", typeof(Excel).Assembly.Location, typeof(Excel).FullName)
            {
                ToolTip = "Экспорт спецификации в Excel."
            };
            RibbonIcons.Set(buttonDataexcel, nameof(Properties.Resources.excel16));
            ContextualHelp excelhelp = new ContextualHelp(ContextualHelpType.Url,
                HelpLinks.GetHelpLink("Excel"));
            buttonDataexcel.SetContextualHelp(excelhelp);

            // подкнопка "Excel.Настройки"

            PushButtonData buttonDataexcelSettings = new PushButtonData(nameof(ExcelSettings), "Excel.Настройки", typeof(ExcelSettings).Assembly.Location, typeof(ExcelSettings).FullName)
            {
                ToolTip = "Экспорт спецификации в Excel."
            };
            RibbonIcons.Set(buttonDataexcelSettings, nameof(Properties.Resources.excel16));
            buttonDataexcelSettings.SetContextualHelp(excelhelp);

            // группа кнопок "Изменения", "Excel"

            SplitButtonData splitButtonDataExcel = new SplitButtonData("Excel", "Экспорт спецификации в Excel.");
            IList<RibbonItem> ribbonItemList = panelViewsSheets.AddStackedItems(buttonDatachanges, (RibbonItemData)splitButtonDataExcel);
            SplitButton splitButtonExcel = ribbonItemList[1] as SplitButton;
            ((PulldownButton)splitButtonExcel).AddPushButton(buttonDataexcel);
            ((PulldownButton)splitButtonExcel).AddPushButton(buttonDataexcelSettings);

            // кнопка "Экспорт листов"

            PushButtonData buttonDataexport = new PushButtonData(nameof(ExportSheetsCommand), "Экспорт\nлистов", typeof(ExportSheetsCommand).Assembly.Location, typeof(ExportSheetsCommand).FullName)
            {
                ToolTip = "Пакетный экспорт в DWG (единый файл) и PDF."
            };
            RibbonIcons.Set(buttonDataexport, nameof(Properties.Resources.exportsheets16), nameof(Properties.Resources.exportsheets32));
            ContextualHelp exporthelp = new ContextualHelp(ContextualHelpType.Url,
                HelpLinks.GetHelpLink("Экспорт листов"));
            buttonDataexport.SetContextualHelp(exporthelp);
            panelViewsSheets.AddItem(buttonDataexport);


            #endregion

            #region Панель "Утилиты"

            // Панель "Утилиты"

            RibbonPanel panelUtils = application.CreateRibbonPanel(tabName, "Утилиты");
            _CommonRibbonItems.Add(panelUtils);

            // кнопка "Связной"

            PushButtonData buttonDatalinks = new PushButtonData(nameof(Links), "Связной", typeof(Links).Assembly.Location, typeof(Links).FullName)
            {
                ToolTip = "Пакетная вставка связей с помещением их в рабочие наборы."
            };
            RibbonIcons.Set(buttonDatalinks, nameof(Properties.Resources.links16), nameof(Properties.Resources.links32));
            ContextualHelp linkshelp = new ContextualHelp(ContextualHelpType.Url,
                HelpLinks.GetHelpLink("Связной"));
            buttonDatalinks.SetContextualHelp(linkshelp);
            panelUtils.AddItem(buttonDatalinks);

            // кнопка "Связи проекта"

            PushButtonData buttonDataManageLinks = new PushButtonData(nameof(ManageLinksCommand), "Связи\nпроекта", typeof(ManageLinksCommand).Assembly.Location, typeof(ManageLinksCommand).FullName)
            {
                ToolTip = "Таблица всех RVT-связей: оси и рабочие наборы, выгрузка и загрузка, графика в текущем виде."
            };
            RibbonIcons.Set(buttonDataManageLinks, nameof(Properties.Resources.worksets16), nameof(Properties.Resources.worksets32));
            ContextualHelp manageLinksHelp = new ContextualHelp(ContextualHelpType.Url,
                HelpLinks.GetHelpLink("Связи проекта"));
            buttonDataManageLinks.SetContextualHelp(manageLinksHelp);
            panelUtils.AddItem(buttonDataManageLinks);

            // стопка мини-кнопок: «Оси и уровни», «Арматура», «Перенести»

            PushButtonData buttonDataGridsInLinks = new PushButtonData(nameof(DisableGridsInLinksCommand), "Оси и уровни", typeof(DisableGridsInLinksCommand).Assembly.Location, typeof(DisableGridsInLinksCommand).FullName)
            {
                ToolTip = "Гасит оси и уровни во всех RVT-связях — закрывает их рабочие наборы. Действует на весь проект, включая 3D и разрезы."
            };
            RibbonIcons.Set(buttonDataGridsInLinks, nameof(Properties.Resources.levels16));

            PushButtonData buttonDataRebarInLinks = new PushButtonData(nameof(DisableRebarInLinksCommand), "Арматура", typeof(DisableRebarInLinksCommand).Assembly.Location, typeof(DisableRebarInLinksCommand).FullName)
            {
                ToolTip = "Гасит арматуру во всех RVT-связях — закрывает арматурные рабочие наборы."
            };
            RibbonIcons.Set(buttonDataRebarInLinks, nameof(Properties.Resources.rebarnomark16));

            PushButtonData buttonDataMoveToLevel = new PushButtonData(nameof(MoveToLevelCommand), "Перенести", typeof(MoveToLevelCommand).Assembly.Location, typeof(MoveToLevelCommand).FullName)
            {
                ToolTip = "Меняет уровень выделенных элементов, не сдвигая их с места."
            };
            RibbonIcons.Set(buttonDataMoveToLevel, nameof(Properties.Resources.levelnumber16));

            // «Оси и уровни» и «Арматура» — частные случаи работы со связями,
            // справка у них общая со «Связями проекта».
            buttonDataGridsInLinks.SetContextualHelp(manageLinksHelp);
            buttonDataRebarInLinks.SetContextualHelp(manageLinksHelp);

            ContextualHelp moveToLevelHelp = new ContextualHelp(ContextualHelpType.Url,
                HelpLinks.GetHelpLink("Перенести"));
            buttonDataMoveToLevel.SetContextualHelp(moveToLevelHelp);
            panelUtils.AddStackedItems(buttonDataGridsInLinks, buttonDataRebarInLinks, buttonDataMoveToLevel);

            // кнопка с выпадающим списком "Закреплятор Уровни Наборы"

            // - подкнопка "Закреплятор Уровни Наборы"

            PushButtonData buttonDataplw = new PushButtonData(nameof(PLW), "Закреплятор\nУровни Наборы", typeof(PLW).Assembly.Location, typeof(PLW).FullName)
            {
                ToolTip = "Закрепить оси, уровни и rvt-связи, переименовать отметки в уровнях, назначить рабочие наборы для связей, осей и уровней."
            };
            RibbonIcons.Set(buttonDataplw, nameof(Properties.Resources.plw16), nameof(Properties.Resources.plw32));
            ContextualHelp plwhelp = new ContextualHelp(ContextualHelpType.Url,
                HelpLinks.GetHelpLink("Закреплятор"));
            buttonDataplw.SetContextualHelp(plwhelp);

            
            // - подкнопка "Настройки"

            PushButtonData buttonDataplwSettings = new PushButtonData(nameof(PLWSettings), "Настройки", typeof(PLWSettings).Assembly.Location, typeof(PLWSettings).FullName)
            {
                ToolTip = "Настройки плагина Закреплятор Уровни Наборы."
            };
            RibbonIcons.Set(buttonDataplwSettings, nameof(Properties.Resources.worksets16), nameof(Properties.Resources.worksets32));
            buttonDataplwSettings.SetContextualHelp(plwhelp);

            
            // - подкнопка "Откреплятор"

            PushButtonData buttonDataunpinner = new PushButtonData(nameof(Unpinner), "Откреплятор", typeof(Unpinner).Assembly.Location, typeof(Unpinner).FullName)
            {
                ToolTip = "Открепить оси, уровни и rvt-связи (на выбор)."
            };
            RibbonIcons.Set(buttonDataunpinner, nameof(Properties.Resources.unpinner16), nameof(Properties.Resources.unpinner32));
            buttonDataunpinner.SetContextualHelp(plwhelp);

            // - основная кнопка

            SplitButtonData buttonDataplwgroup = new SplitButtonData("Закреплятор\nУровни Наборы", "Закрепить оси, уровни и rvt-связи, переименовать отметки в уровнях, назначить рабочие наборы для связей, осей и уровней.");
            SplitButton groupplw = panelUtils.AddItem(buttonDataplwgroup) as SplitButton;
            groupplw.AddPushButton(buttonDataplw);
            groupplw.AddPushButton(buttonDataplwSettings);
            groupplw.AddPushButton(buttonDataunpinner);
            groupplw.SetContextualHelp(plwhelp);
                        
            
            


            // сгруппированная кнопка "Выбор по ID"

            PushButtonData buttonDataidselection = new PushButtonData(nameof(IdSelection), "Выбор по ID", typeof(IdSelection).Assembly.Location, typeof(IdSelection).FullName)
            {
                ToolTip = "Выбрать и изолировать элементы по ID."
            };
            RibbonIcons.Set(buttonDataidselection, nameof(Properties.Resources.idselection16));
            ContextualHelp idselectionhelp = new ContextualHelp(ContextualHelpType.Url,
                HelpLinks.GetHelpLink("Выбор по ID"));
            buttonDataidselection.SetContextualHelp(idselectionhelp);

            // сгруппированная кнопка "Типофильтр"

            PushButtonData buttonDatafilter = new PushButtonData(nameof(TypeFilter), "Типофильтр", typeof(TypeFilter).Assembly.Location, typeof(TypeFilter).FullName)
            {
                ToolTip = "Фильтрация на виде по типам элементов, создание фильтров в проекте."
            };
            RibbonIcons.Set(buttonDatafilter, nameof(Properties.Resources.typefilter16));
            ContextualHelp filterhelp = new ContextualHelp(ContextualHelpType.Url,
                HelpLinks.GetHelpLink("Типофильтр"));
            buttonDatafilter.SetContextualHelp(filterhelp);

            // кнопка "Семейный"

            PushButtonData buttonDatafamilies = new PushButtonData(nameof(LoadFamiliesFromServer), "Семейный", typeof(LoadFamiliesFromServer).Assembly.Location, typeof(LoadFamiliesFromServer).FullName)
            {
                //LargeImage = GetImageSource(imgfamilies),
                ToolTip = "Библиотека семейств и заявки на семейства."
            };
            RibbonIcons.Set(buttonDatafamilies, nameof(Properties.Resources.families16));
            ContextualHelp familieshelp = new ContextualHelp(ContextualHelpType.Url,
                HelpLinks.GetHelpLink("Семейный"));
            buttonDatafamilies.SetContextualHelp(familieshelp);

            // группа кнопок "Типофильтр", "Выбор по ID", "Семейный"

            panelUtils.AddStackedItems(buttonDatafilter, buttonDataidselection, buttonDatafamilies);

            // кнопка с выпадающим списком "Краска+"

            // подкнопка "Краска+"

            PushButtonData buttonDatapaint = new PushButtonData(nameof(Paint), "Краска+", typeof(Paint).Assembly.Location, typeof(Paint).FullName)
            {
                ToolTip = "Копирование краски."
            };
            RibbonIcons.Set(buttonDatapaint, nameof(Properties.Resources.paint16), nameof(Properties.Resources.paint32));
            buttonDatapaint.SetContextualHelp(mainhelp);

            // подкнопка "Краска"

            PushButtonData buttonDatarevitpaint = new PushButtonData(nameof(revitpaint), "Краска", typeof(revitpaint).Assembly.Location, typeof(revitpaint).FullName)
            {
                ToolTip = "Применение материала к грани элемента."
            };
            RibbonIcons.Set(buttonDatarevitpaint, nameof(Properties.Resources.revitpaint16), nameof(Properties.Resources.revitpaint32));
            buttonDatarevitpaint.SetContextualHelp(mainhelp);

            // подкнопка "Разделение грани"

            PushButtonData buttonDatarevitsplitface = new PushButtonData(nameof(revitsplitface), "Разделение грани", typeof(revitsplitface).Assembly.Location, typeof(revitsplitface).FullName)
            {
                ToolTip = "Разделение грани элемента."
            };
            RibbonIcons.Set(buttonDatarevitsplitface, nameof(Properties.Resources.revitsplitface16), nameof(Properties.Resources.revitsplitface32));
            buttonDatarevitsplitface.SetContextualHelp(mainhelp);

            // подкнопка "Материал?"

            PushButtonData buttonDatapaint2 = new PushButtonData(nameof(Paint2), "Материал?", typeof(Paint2).Assembly.Location, typeof(Paint2).FullName)
            {
                ToolTip = "Получить имя материала выбранной грани."
            };
            RibbonIcons.Set(buttonDatapaint2, nameof(Properties.Resources.paint2_16), nameof(Properties.Resources.paint2_32));
            buttonDatapaint2.SetContextualHelp(mainhelp);

            // подкнопка "Удалить краску"

            PushButtonData buttonDatarevitpaintdel = new PushButtonData(nameof(revitpaintdel), "Удалить краску", typeof(revitpaintdel).Assembly.Location, typeof(revitpaintdel).FullName)
            {
                ToolTip = "Удалить краску с грани элемента."
            };
            RibbonIcons.Set(buttonDatarevitpaintdel, nameof(Properties.Resources.revitpaintdel16), nameof(Properties.Resources.revitpaintdel32));
            buttonDatarevitpaintdel.SetContextualHelp(mainhelp);

            // - основная кнопка

            SplitButtonData buttonDatapaintgroup = new SplitButtonData("Краска+", "Копирование краски.");
            SplitButton grouppaint = panelUtils.AddItem(buttonDatapaintgroup) as SplitButton;
            grouppaint.AddPushButton(buttonDatapaint);
            grouppaint.AddPushButton(buttonDatarevitpaint);
            grouppaint.AddPushButton(buttonDatarevitsplitface);
            grouppaint.AddPushButton(buttonDatapaint2);
            grouppaint.AddPushButton(buttonDatarevitpaintdel);
            grouppaint.SetContextualHelp(mainhelp);

            

            #endregion

            #region Панель "Помещения"

            // Панель "Помещения"

            RibbonPanel panelRooms = application.CreateRibbonPanel(tabName, "Помещения");
            _ARRibbonItems.Add(panelRooms);

            // кнопка с выпадающим списком "Помещения"

            // подкнопка "Номера помещений"

            PushButtonData buttonDatarooms = new PushButtonData(nameof(RoomsNum), "Номера помещений", typeof(RoomsNum).Assembly.Location, typeof(RoomsNum).FullName)
            {
                ToolTip = "Пронумеровать помещения c последовательным выбором элементов."
            };
            RibbonIcons.Set(buttonDatarooms, nameof(Properties.Resources.roomsnum16), nameof(Properties.Resources.roomsnum32));
            ContextualHelp roomshelp = new ContextualHelp(ContextualHelpType.Url,
                HelpLinks.GetHelpLink("Номера помещений"));
            buttonDatarooms.SetContextualHelp(roomshelp);

            // подкнопка "Округлятор"

            PushButtonData buttonDataroomsround = new PushButtonData(nameof(RoomsRound), "Округлятор", typeof(RoomsRound).Assembly.Location, typeof(RoomsRound).FullName)
            {
                ToolTip = "Округлить площади помещений."
            };
            RibbonIcons.Set(buttonDataroomsround, nameof(Properties.Resources.roomsround16), nameof(Properties.Resources.roomsround32));
            ContextualHelp roomsroundhelp = new ContextualHelp(ContextualHelpType.Url,
                HelpLinks.GetHelpLink("Округлятор"));
            buttonDataroomsround.SetContextualHelp(roomsroundhelp);

            // подкнопка "Нумератор квартир"

            PushButtonData buttonDataapartsnum = new PushButtonData(nameof(ApartsNumAtLevel), "Нумератор квартир", typeof(ApartsNumAtLevel).Assembly.Location, typeof(ApartsNumAtLevel).FullName)
            {
                ToolTip = "Пронумеровать квартиры (номер на этаже - в ручном режиме, сквозные номера - автоматически)."
            };
            RibbonIcons.Set(buttonDataapartsnum, nameof(Properties.Resources.apartsnum16), nameof(Properties.Resources.apartsnum32));
            ContextualHelp apartsnumhelp = new ContextualHelp(ContextualHelpType.Url,
                HelpLinks.GetHelpLink("Нумератор квартир"));
            buttonDataapartsnum.SetContextualHelp(apartsnumhelp);

            // подкнопка "Квартирография"

            PushButtonData buttonDataaparts = new PushButtonData(nameof(Aparts), "Квартирография", typeof(Aparts).Assembly.Location, typeof(Aparts).FullName)
            {
                ToolTip = "Выполнить расчет квартирографии (с перерасчетом площадей или без него)."
            };
            RibbonIcons.Set(buttonDataaparts, nameof(Properties.Resources.aparts16), nameof(Properties.Resources.aparts32));
            ContextualHelp apartshelp = new ContextualHelp(ContextualHelpType.Url,
                HelpLinks.GetHelpLink("Квартирография"));
            buttonDataaparts.SetContextualHelp(apartshelp);

            // подкнопка "Офисография"

            PushButtonData buttonDataoffices = new PushButtonData(nameof(Offices), "Офисография", typeof(Offices).Assembly.Location, typeof(Offices).FullName)
            {
                ToolTip = "Выполнить расчет офисографии (с перерасчетом площадей или без него)."
            };
            RibbonIcons.Set(buttonDataoffices, nameof(Properties.Resources.offices16), nameof(Properties.Resources.offices32));
            ContextualHelp officeshelp = new ContextualHelp(ContextualHelpType.Url,
                HelpLinks.GetHelpLink("Офисография"));
            buttonDataoffices.SetContextualHelp(officeshelp);

            // подкнопка "Удалить лишние"

            PushButtonData buttonDatafailedrooms = new PushButtonData(nameof(PurgeFailedRooms), "Удалить лишние", typeof(PurgeFailedRooms).Assembly.Location, typeof(PurgeFailedRooms).FullName)
            {
                ToolTip = "Удалить лишние помещения (неразмещенные и избыточные)."
            };
            RibbonIcons.Set(buttonDatafailedrooms, nameof(Properties.Resources.failedrooms16), nameof(Properties.Resources.failedrooms32));
            ContextualHelp failedroomshelp = new ContextualHelp(ContextualHelpType.Url,
                HelpLinks.GetHelpLink("Удалить лишние"));
            buttonDatafailedrooms.SetContextualHelp(failedroomshelp);

            // подкнопка "Резервные копии"

            PushButtonData buttonDataroomsbackup = new PushButtonData(nameof(RoomsBackup), "Резервные копии", typeof(RoomsBackup).Assembly.Location, typeof(RoomsBackup).FullName)
            {
                ToolTip = "Резервное копирование и восстановление значений площадей помещений."
            };
            RibbonIcons.Set(buttonDataroomsbackup, nameof(Properties.Resources.roomsbackup16), nameof(Properties.Resources.roomsbackup32));
            ContextualHelp roomsbackuphelp = new ContextualHelp(ContextualHelpType.Url,
                HelpLinks.GetHelpLink("Помещения Резервные копии"));
            buttonDataroomsbackup.SetContextualHelp(roomsbackuphelp);

            // подкнопка "Номера по ТЗ"

            PushButtonData buttonDataroomsTNumber = new PushButtonData(nameof(RoomsTNumber), "Номера по ТЗ", typeof(RoomsTNumber).Assembly.Location, typeof(RoomsTNumber).FullName)
            {
                ToolTip = "Дозаполнить номера по ТЗ у продаваемых помещений."
            };
            RibbonIcons.Set(buttonDataroomsTNumber, nameof(Properties.Resources.roomsnum16), nameof(Properties.Resources.roomsnum32));
            buttonDataroomsbackup.SetContextualHelp(roomsroundhelp);

            // - основная кнопка

            PulldownButtonData buttonDataapartsgroup = new PulldownButtonData("Помещения", "Помещения")
            {
                ToolTip = "Пакет функций для работы с помещениями."
            };
            RibbonIcons.Set(buttonDataapartsgroup, nameof(Properties.Resources.rooms16), nameof(Properties.Resources.rooms32));
            ContextualHelp apartsgrouphelp = new ContextualHelp(ContextualHelpType.Url,
                HelpLinks.GetHelpLink("Номера по ТЗ"));
            buttonDataapartsgroup.SetContextualHelp(apartsgrouphelp);
            PulldownButton groupaparts = panelRooms.AddItem(buttonDataapartsgroup) as PulldownButton;
            groupaparts.AddPushButton(buttonDatarooms);
            groupaparts.AddPushButton(buttonDataroomsround);
            groupaparts.AddPushButton(buttonDataapartsnum);
            groupaparts.AddPushButton(buttonDataaparts);
            groupaparts.AddPushButton(buttonDataoffices);
            groupaparts.AddPushButton(buttonDatafailedrooms);
            groupaparts.AddPushButton(buttonDataroomsbackup);
            groupaparts.AddPushButton(buttonDataroomsTNumber);

            // кнопка "Менеджер помещений"

            PushButtonData buttonDataroomsManager = new PushButtonData(nameof(RoomsManager), "Менеджер\nпомещений", typeof(RoomsManager).Assembly.Location, typeof(RoomsManager).FullName)
            {
                ToolTip = "Общий интерфейс функций по помещениям: обязательные проверки параметров, Округлятор, сверка с резервными копиями площадей."
            };
            RibbonIcons.Set(buttonDataroomsManager, nameof(Properties.Resources.rooms16), nameof(Properties.Resources.rooms32));
            buttonDataroomsManager.SetContextualHelp(apartsgrouphelp);
            panelRooms.AddItem(buttonDataroomsManager);

            #endregion

            #region Панель "Отделка"

            // Панель "Отделка"

            RibbonPanel panelFinishing = application.CreateRibbonPanel(tabName, "Отделка");
            _ARRibbonItems.Add(panelFinishing);

            // кнопка "Генератор полов"

            PushButtonData buttonDatafloors = new PushButtonData(nameof(Floors), "Генератор\nполов", typeof(Floors).Assembly.Location, typeof(Floors).FullName)
            {
                ToolTip = "Создать полы в помещениях."
            };
            RibbonIcons.Set(buttonDatafloors, nameof(Properties.Resources.floors16), nameof(Properties.Resources.floors32));
            ContextualHelp floorshelp = new ContextualHelp(ContextualHelpType.Url,
                HelpLinks.GetHelpLink("Генератор полов"));
            buttonDatafloors.SetContextualHelp(floorshelp);
            panelFinishing.AddItem(buttonDatafloors);

            // сгруппированная кнопка "Ведомость полов"

            PushButtonData buttonDatafloorspec = new PushButtonData(nameof(FloorImages), "Ведомость полов", typeof(FloorImages).Assembly.Location, typeof(FloorImages).FullName)
            {
                ToolTip = "Сформировать изображения для ведомости полов."
            };
            RibbonIcons.Set(buttonDatafloorspec, nameof(Properties.Resources.floorimages16));
            buttonDatafloorspec.SetContextualHelp(mainhelp);

            // сгруппированная кнопка "Ведомость отделки"

            PushButtonData buttonDatafinishing = new PushButtonData(nameof(Finishing), "Ведомость отделки", typeof(Finishing).Assembly.Location, typeof(Finishing).FullName)
            {
                ToolTip = "Заполнение параметров для ведомости отделки у стен, полов, потолков."
            };
            RibbonIcons.Set(buttonDatafinishing, nameof(Properties.Resources.finishing16));
            ContextualHelp finishinghelp = new ContextualHelp(ContextualHelpType.Url,
                HelpLinks.GetHelpLink("Ведомость отделки"));
            buttonDatafinishing.SetContextualHelp(finishinghelp);

            // группа кнопок "Ведомость полов", "Ведомость отделки"

            panelFinishing.AddStackedItems(buttonDatafloorspec, buttonDatafinishing);

            #endregion

            #region Панель "Утилиты АР"

            // Панель "Утилиты АР"

            RibbonPanel panelUtilsAR = application.CreateRibbonPanel(tabName, "Утилиты АР");
            _ARRibbonItems.Add(panelUtilsAR);

            // кнопка "Оформлятор АР"

            PushButtonData buttonDataAutoDim = new PushButtonData(nameof(PluginPanelCommand), "Оформлятор\nАР", typeof(PluginPanelCommand).Assembly.Location, typeof(PluginPanelCommand).FullName)
            {
                ToolTip = "Автоматическая простановка размеров, марок помещений, окон, дверей."
            };
            RibbonIcons.Set(buttonDataAutoDim, nameof(Properties.Resources.autodim16), nameof(Properties.Resources.autodim32));
            buttonDataAutoDim.SetContextualHelp(mainhelp);
            panelUtilsAR.AddItem(buttonDataAutoDim);

            // сгруппированная кнопка "Антизеркало"
            PushButtonData buttonDatamirror = new PushButtonData(nameof(Mirror), "Антизеркало", typeof(Mirror).Assembly.Location, typeof(Mirror).FullName)
            {
                ToolTip = "Выделить отзеркаленные окна и двери, пометить такие элементы через параметр Марка."
            };
            RibbonIcons.Set(buttonDatamirror, nameof(Properties.Resources.mirror16), nameof(Properties.Resources.mirror32));
            ContextualHelp mirrorhelp = new ContextualHelp(ContextualHelpType.Url,
                HelpLinks.GetHelpLink("Антизеркало"));
            buttonDatamirror.SetContextualHelp(mirrorhelp);

            // сгруппированная кнопка "Проемщик"

            PushButtonData buttonDataCopyWindows = new PushButtonData(nameof(CopyWindows), "Проемщик", typeof(CopyWindows).Assembly.Location, typeof(CopyWindows).FullName)
            {
                ToolTip = "Создать обобщенные модели из окон/дверей связанной модели (_АР)",
                LongDescription = "Находит в связанных моделях с _АР все окна и двери, позволяет выбрать нужные и копирует их как семейства pmN.Отверстие Стена.ПОФ с параметрами."
            };
            RibbonIcons.Set(buttonDataCopyWindows, nameof(Properties.Resources.CopyWindows16));
            ContextualHelp CopyWindowsHelp = new ContextualHelp(ContextualHelpType.Url,
                HelpLinks.GetHelpLink("Проемщик"));
            buttonDataCopyWindows.SetContextualHelp(CopyWindowsHelp);

            // сгруппированная кнопка "Эт.Номер"

            PushButtonData buttonDatalevelnumber = new PushButtonData(nameof(LevelNumber), "Эт.Номер", typeof(LevelNumber).Assembly.Location, typeof(LevelNumber).FullName)
            {
                ToolTip = "Заполнить Эт.Номер у элементов модели (с выбором категорий)."
            };
            RibbonIcons.Set(buttonDatalevelnumber, nameof(Properties.Resources.levelnumber16));
            ContextualHelp levelnumberhelp = new ContextualHelp(ContextualHelpType.Url,
                HelpLinks.GetHelpLink("Эт.Номер"));
            buttonDatalevelnumber.SetContextualHelp(levelnumberhelp);

            // группа кнопок 

            panelUtilsAR.AddStackedItems(buttonDatalevelnumber, buttonDatamirror, buttonDataCopyWindows);

            // кнопка "АМ ПСО"

            PushButtonData buttonDataAM = new PushButtonData(nameof(CreateApartmentViewsCommand), "АМ\nПСО", typeof(CreateApartmentViewsCommand).Assembly.Location, typeof(CreateApartmentViewsCommand).FullName)
            {
                ToolTip = "Сформировать виды квартир для АМ ПСО."
            };
            RibbonIcons.Set(buttonDataAM, nameof(Properties.Resources.AM16), nameof(Properties.Resources.AM32));
            ContextualHelp AMhelp = new ContextualHelp(ContextualHelpType.Url,
                HelpLinks.GetHelpLink("АМ ПСО"));
            buttonDataAM.SetContextualHelp(AMhelp);

            panelUtilsAR.AddItem(buttonDataAM);

            #endregion

            #region Панель "Парковки"

            // Панель "Парковки"

            RibbonPanel panelParking = application.CreateRibbonPanel(tabName, "Парковки");
            _ARRibbonItems.Add(panelParking);

            // кнопка "Парковки"

            PushButtonData buttonDatapark = new PushButtonData(nameof(Parking), "Парковки", typeof(Parking).Assembly.Location, typeof(Parking).FullName)
            {
                ToolTip = "Пакет функций для работы с парковками."
            };
            RibbonIcons.Set(buttonDatapark, nameof(Properties.Resources.park16), nameof(Properties.Resources.park32));
            ContextualHelp parkhelp = new ContextualHelp(ContextualHelpType.Url,
                HelpLinks.GetHelpLink("Парковки"));
            buttonDatapark.SetContextualHelp(parkhelp);
            panelParking.AddItem(buttonDatapark);

            // Панель "Перемычки"

            RibbonPanel panelBeams = application.CreateRibbonPanel(tabName, "Перемычки");
            _ARRibbonItems.Add(panelBeams);

            // кнопка "Перемычки"

            PushButtonData buttonDatabeamscut = new PushButtonData(nameof(Beams), "Перемычки", typeof(Beams).Assembly.Location, typeof(Beams).FullName)
            {
                ToolTip = "Вырезать объем бетонных перемычек из стен, сформировать эскизы ПР."
            };
            RibbonIcons.Set(buttonDatabeamscut, nameof(Properties.Resources.beamscut16), nameof(Properties.Resources.beamscut32));
            ContextualHelp beamshelp = new ContextualHelp(ContextualHelpType.Url,
                HelpLinks.GetHelpLink("Перемычки"));
            buttonDatabeamscut.SetContextualHelp(beamshelp);
            panelBeams.AddItem(buttonDatabeamscut);

            #endregion

            #region Панель "Сваи"

            // Панель "Сваи"

            RibbonPanel panelPiles = application.CreateRibbonPanel(tabName, "Сваи");
            _STRibbonItems.Add(panelPiles);

            // кнопка "Сваи"

            PushButtonData buttonDatapiles = new PushButtonData(nameof(Found), "Сваи", typeof(Found).Assembly.Location, typeof(Found).FullName)
            {
                ToolTip = "Пакет функций по работе со сваями."
            };
            RibbonIcons.Set(buttonDatapiles, nameof(Properties.Resources.foundcut16), nameof(Properties.Resources.foundcut32));
            ContextualHelp pileshelp = new ContextualHelp(ContextualHelpType.Url,
                HelpLinks.GetHelpLink("Сваи"));
            buttonDatapiles.SetContextualHelp(pileshelp);
            panelPiles.AddItem(buttonDatapiles);

            #endregion

            #region Панель "Утилиты КЖ"

            // Панель "Утилиты КЖ"

            RibbonPanel panelUtilsST = application.CreateRibbonPanel(tabName, "Утилиты КЖ");
            _STRibbonItems.Add(panelUtilsST);

            // кнопка "Ускорить файл"

            PushButtonData buttonDatafixstructurefile = new PushButtonData(nameof(Fixstructurefile), "Ускорить\nфайл", typeof(Fixstructurefile).Assembly.Location, typeof(Fixstructurefile).FullName)
            {
                ToolTip = "Ускорить работу модели КЖ путем манипуляций с параметрами несущей арматуры."
            };
            RibbonIcons.Set(buttonDatafixstructurefile, nameof(Properties.Resources.fixstructurefile16), nameof(Properties.Resources.fixstructurefile32));
            ContextualHelp fixstructurefilehelp = new ContextualHelp(ContextualHelpType.Url,
                HelpLinks.GetHelpLink("Ускорить файл"));
            buttonDatafixstructurefile.SetContextualHelp(fixstructurefilehelp);
            panelUtilsST.AddItem(buttonDatafixstructurefile);

            // сгруппированная кнопка "Эскизы деталей"

            PushButtonData buttonDatarebarimages = new PushButtonData(nameof(RebarImages), "Эскизы деталей", typeof(RebarImages).Assembly.Location, typeof(RebarImages).FullName)
            {
                ToolTip = "Заполнить параметр A_Арм Эскиз формы у системной арматуры для ведомости деталей."
            };
            RibbonIcons.Set(buttonDatarebarimages, nameof(Properties.Resources.rebarimages16));
            ContextualHelp rebarimageshelp = new ContextualHelp(ContextualHelpType.Url,
                HelpLinks.GetHelpLink("Эскизы деталей"));
            buttonDatarebarimages.SetContextualHelp(rebarimageshelp);

            // сгруппированная кнопка "ВРС подчистить"

            PushButtonData buttonDatasteelschedule = new PushButtonData(nameof(SteelSchedule), "ВРС подчистить", typeof(SteelSchedule).Assembly.Location, typeof(SteelSchedule).FullName)
            {
                ToolTip = "Подчистить все ведомости расхода стали в проекте (скрыть столбцы с нулевыми значениями)."
            };
            RibbonIcons.Set(buttonDatasteelschedule, nameof(Properties.Resources.steelschedule16));
            ContextualHelp steelschedulehelp = new ContextualHelp(ContextualHelpType.Url,
                HelpLinks.GetHelpLink("ВРС подчистить"));
            buttonDatasteelschedule.SetContextualHelp(steelschedulehelp);

            // сгруппированная кнопка "Группировка"

            PushButtonData buttonDataschemespec = new PushButtonData(nameof(Schemespec), "Группировка", typeof(Schemespec).Assembly.Location, typeof(Schemespec).FullName)
            {
                ToolTip = "Заполнить параметр A_Группирование для сортировки спецификаций)."
            };
            RibbonIcons.Set(buttonDataschemespec, nameof(Properties.Resources.grouping16));
            ContextualHelp schemespechelp = new ContextualHelp(ContextualHelpType.Url,
                HelpLinks.GetHelpLink("Группировка"));
            buttonDataschemespec.SetContextualHelp(schemespechelp);

            // группа кнопок "Эскизы деталей", "ВРС подчистить", "Группировка"

            panelUtilsST.AddStackedItems(buttonDatarebarimages, buttonDatasteelschedule, buttonDataschemespec);

            //RebarNoMark
            // кнопка "Арматура без марки"

            PushButtonData buttonDataRebarNoMark = new PushButtonData(nameof(RebarNoMark), "Арматура\nбез марки", typeof(RebarNoMark).Assembly.Location, typeof(RebarNoMark).FullName)
            {
                ToolTip = "Изолирует на открытом 3D-виде несущую арматуру с незаполненным параметром A_Марка конструкции."
            };
            RibbonIcons.Set(buttonDataRebarNoMark, nameof(Properties.Resources.rebarnomark16), nameof(Properties.Resources.rebarnomark32));
            ContextualHelp RebarNoMarkhelp = new ContextualHelp(ContextualHelpType.Url,
                HelpLinks.GetHelpLink("Арматура без марки"));
            buttonDataRebarNoMark.SetContextualHelp(RebarNoMarkhelp);
            panelUtilsST.AddItem(buttonDataRebarNoMark);

            #endregion

            #region Панель "СО"

            // Панель "СО"

            RibbonPanel panelMEPSpec = application.CreateRibbonPanel(tabName, "СО");
            _MEPRibbonItems.Add(panelMEPSpec);

            // кнопка "Сводная спека"

            PushButtonData buttonDataadskg = new PushButtonData(nameof(MEPSpec), "Сводная\nспека", typeof(MEPSpec).Assembly.Location, typeof(MEPSpec).FullName)
            {
                ToolTip = "Заполнить параметры у элементов ВК ОВ / ЭЛ / СС ПС для формирования сводной спецификации."
            };
            RibbonIcons.Set(buttonDataadskg, nameof(Properties.Resources.adskg16), nameof(Properties.Resources.adskg32));
            ContextualHelp adskghelp = new ContextualHelp(ContextualHelpType.Url,
                HelpLinks.GetHelpLink("Сводная спека"));
            buttonDataadskg.SetContextualHelp(adskghelp);
            panelMEPSpec.AddItem(buttonDataadskg);

            #endregion

            #region Панель "ОВ"

            // Панель "ОВ"

            RibbonPanel panelVent = application.CreateRibbonPanel(tabName, "ОВ");
            _MEPRibbonItems.Add(panelVent);

            // кнопка "Теплопотери Qoveter"

            PushButtonData buttonDataQoveter = new PushButtonData(nameof(CalculateHeatLossCommand), "Теплопотери\nQoveter", typeof(CalculateHeatLossCommand).Assembly.Location, typeof(CalculateHeatLossCommand).FullName)
            {
                ToolTip = "Выполнить расчет теплопотерь."
            };
            RibbonIcons.Set(buttonDataQoveter, nameof(Properties.Resources.Qoveter16), nameof(Properties.Resources.Qoveter32));
            ContextualHelp Qoveterhelp = new ContextualHelp(ContextualHelpType.Url,
                HelpLinks.GetHelpLink("Теплопотери"));
            buttonDataQoveter.SetContextualHelp(Qoveterhelp);
            panelVent.AddItem(buttonDataQoveter);

            // сгруппированная кнопка "Стенки Классы"

            PushButtonData buttonDataadskstenki = new PushButtonData(nameof(DuctThicknessClasses), "Стенки классы", typeof(DuctThicknessClasses).Assembly.Location, typeof(DuctThicknessClasses).FullName)
            {
                ToolTip = "Заполнить толщины стенок и класс герметичности воздуховодов."
            };
            RibbonIcons.Set(buttonDataadskstenki, nameof(Properties.Resources.adskstenki16));
            ContextualHelp adskstenkihelp = new ContextualHelp(ContextualHelpType.Url,
                HelpLinks.GetHelpLink("ADSK Стенки"));
            buttonDataadskstenki.SetContextualHelp(adskstenkihelp);


            // сгруппированная кнопка "Схемы ОВ2"

            PushButtonData buttonDataduct3d = new PushButtonData(nameof(Duct3D), "Схемы ОВ2", typeof(Duct3D).Assembly.Location, typeof(Duct3D).FullName)
            {
                ToolTip = "Создать/заменить схемы систем вентиляции."
            };
            RibbonIcons.Set(buttonDataduct3d, nameof(Properties.Resources.vent16));
            ContextualHelp duct3dhelp = new ContextualHelp(ContextualHelpType.Url,
                HelpLinks.GetHelpLink("Схемы ОВ2"));
            buttonDataduct3d.SetContextualHelp(duct3dhelp);

            // группа кнопок "Стенки Классы", "Схемы ОВ2"

            panelVent.AddStackedItems(buttonDataadskstenki, buttonDataduct3d);

            #endregion

            #region Панель "Электрика"

            // Панель "Электрика"

            RibbonPanel panelElectrical = application.CreateRibbonPanel(tabName, "Электрика");
            _MEPRibbonItems.Add(panelElectrical);

            // подкнопка "ЭЛ Отметки размещения"

            PushButtonData buttonDataefl = new PushButtonData(nameof(ElElevValues), "ЭЛ Отметки", typeof(ElElevValues).Assembly.Location, typeof(ElElevValues).FullName)
            {
                ToolTip = "Заполнить параметры N_ЭЛ.Высота стяжки и N_ЭЛ.Отметка потолка у выключателей, осветительных и электрических приборов, электрооборудования."
            };
            RibbonIcons.Set(buttonDataefl, nameof(Properties.Resources.efl16));
            ContextualHelp eflhelp = new ContextualHelp(ContextualHelpType.Url,
                HelpLinks.GetHelpLink("ЭЛ Отметки"));
            buttonDataefl.SetContextualHelp(eflhelp);

            // подкнопка "ЭЛ Отметки размещения. Настройки"

            PushButtonData buttonDataeflsettings = new PushButtonData(nameof(ElElevValuesSettings), "Отметки.Настройки", typeof(ElElevValuesSettings).Assembly.Location, typeof(ElElevValuesSettings).FullName)
            {
                ToolTip = "Настройки плагина ЭЛ Отметки."
            };
            RibbonIcons.Set(buttonDataeflsettings, nameof(Properties.Resources.efl16));
            buttonDataeflsettings.SetContextualHelp(eflhelp);

            // подкнопка "Лотки"

            PushButtonData buttonDatacabletrays = new PushButtonData(nameof(CableTrays), "Лотки", typeof(CableTrays).Assembly.Location, typeof(CableTrays).FullName)
            {
                ToolTip = "Крышки, перегородки для кабельных лотков, помещение лотков и их элементов в рабочий набор."
            };
            RibbonIcons.Set(buttonDatacabletrays, nameof(Properties.Resources.cabletrays16), nameof(Properties.Resources.cabletrays32));
            ContextualHelp cabletrayshelp = new ContextualHelp(ContextualHelpType.Url,
                HelpLinks.GetHelpLink("Лотки"));
            buttonDatacabletrays.SetContextualHelp(cabletrayshelp);

            // подкнопка "Лотки.Настройки"

            PushButtonData buttonDatacabletrayssettings = new PushButtonData(nameof(CableTraysSettings), "Лотки.Настройки", typeof(CableTraysSettings).Assembly.Location, typeof(CableTraysSettings).FullName)
            {
                ToolTip = "Настройки плагина Лотки."
            };
            RibbonIcons.Set(buttonDatacabletrayssettings, nameof(Properties.Resources.cabletrays16), nameof(Properties.Resources.cabletrays32));
            buttonDatacabletrayssettings.SetContextualHelp(cabletrayshelp);

            // - основная кнопка "Лотки"

            SplitButtonData buttonDatacabletraysgroup = new SplitButtonData("Лотки", "Крышки, перегородки для кабельных лотков, помещение лотков и их элементов в рабочий набор.");
            SplitButton groupcabletrays = panelElectrical.AddItem(buttonDatacabletraysgroup) as SplitButton;
            groupcabletrays.AddPushButton(buttonDatacabletrays);
            groupcabletrays.AddPushButton(buttonDatacabletrayssettings);

            // подкнопка "Синхронизатор"

            PushButtonData buttonDataElSystemSync = new PushButtonData(nameof(ElSystemSync), "Синхронизатор", typeof(ElSystemSync).Assembly.Location, typeof(ElSystemSync).FullName)
            {
                ToolTip = "Запись данных из цепей связанного файла в параметры автоматического выключателя."
            };
            RibbonIcons.Set(buttonDataElSystemSync, nameof(Properties.Resources.elsync16));
            ContextualHelp ElSystemSynchelp = new ContextualHelp(ContextualHelpType.Url,
                HelpLinks.GetHelpLink("Синхронизатор"));
            buttonDataElSystemSync.SetContextualHelp(ElSystemSynchelp);
                        
            // подкнопка "Способы прокладки"

            PushButtonData buttonDatacableways = new PushButtonData(nameof(CableWays), "Способы прокладки", typeof(CableWays).Assembly.Location, typeof(CableWays).FullName)
            {
                ToolTip = "Запись значений в параметры автоматического выключателя."
            };
            RibbonIcons.Set(buttonDatacableways, nameof(Properties.Resources.cableways16));
            ContextualHelp cablewayshelp = new ContextualHelp(ContextualHelpType.Url,
                HelpLinks.GetHelpLink("Способы прокладки"));
            buttonDatacableways.SetContextualHelp(cablewayshelp);

            // подкнопка "Прокладка.Настройки"

            PushButtonData buttonDatacablewayssettings = new PushButtonData(nameof(CableWaysSettings), "Прокладка.Настройки", typeof(CableWaysSettings).Assembly.Location, typeof(CableWaysSettings).FullName)
            {
                ToolTip = "Настройки плагина Способы прокладки."
            };
            RibbonIcons.Set(buttonDatacablewayssettings, nameof(Properties.Resources.cableways16));
            buttonDatacablewayssettings.SetContextualHelp(cablewayshelp);

            // группа расширенных кнопок "Синхронизатор", "Способы прокладки", "ЭЛ Отметки размещения"

            SplitButtonData splitButtonDataCW = new SplitButtonData("Способы прокладки", "Запись значений в параметры автоматического выключателя.");
            SplitButtonData splitButtonDataEFL = new SplitButtonData("ЭЛ Отметки", "Заполнить параметры N_ЭЛ.Высота стяжки и N_ЭЛ.Отметка потолка у выключателей, осветительных и электрических приборов, электрооборудования.");
            IList<RibbonItem> ribbonItemList2 = panelElectrical.AddStackedItems(buttonDataElSystemSync, (RibbonItemData)splitButtonDataCW, (RibbonItemData)splitButtonDataEFL);
            SplitButton splitButtonCW = ribbonItemList2[1] as SplitButton;
            SplitButton splitButtonEFL = ribbonItemList2[2] as SplitButton;
            ((PulldownButton)splitButtonCW).AddPushButton(buttonDatacableways);
            ((PulldownButton)splitButtonCW).AddPushButton(buttonDatacablewayssettings);
            ((PulldownButton)splitButtonEFL).AddPushButton(buttonDataefl);
            ((PulldownButton)splitButtonEFL).AddPushButton(buttonDataeflsettings);

            #endregion

            #region Панель "Слаботочка"

            // Панель "Слаботочка"

            RibbonPanel panelSS = application.CreateRibbonPanel(tabName, "Слаботочка");
            _MEPRibbonItems.Add(panelSS);

            // сгруппированная кнопка "Адресатор"

            PushButtonData buttonDatass = new PushButtonData(nameof(SSNumberer), "Адресатор", typeof(SSNumberer).Assembly.Location, typeof(SSNumberer).FullName)
            {
                ToolTip = "Пакет функций по адресации устройств СС ПС."
            };
            RibbonIcons.Set(buttonDatass, nameof(Properties.Resources.ssNumberer16));
            ContextualHelp sshelp = new ContextualHelp(ContextualHelpType.Url,
                HelpLinks.GetHelpLink("Адресатор"));
            buttonDatass.SetContextualHelp(sshelp);


            // сгруппированная кнопка "FamilyAToFamilyB"
            PushButtonData buttonDataFamilyAToFamilyB = new PushButtonData("FamilyAToFamilyB", "Расстановщик\nСС ПС", typeof(PikachuCommand).Assembly.Location, typeof(PikachuCommand).FullName)
            {
                ToolTip = "Универсальное размещение элементов рядом с элементами из связанных файлов",
                LongDescription = "Размещает элементы текущего файла рядом с элементами из связанных файлов\n\nГод напряженный - работаем эффективно!"
            };
            RibbonIcons.Set(buttonDataFamilyAToFamilyB, nameof(Properties.Resources.pikachu16), nameof(Properties.Resources.pikachu32));
            ContextualHelp buttonDataFamilyAToFamilyBhelp = new ContextualHelp(ContextualHelpType.Url,
                HelpLinks.GetHelpLink("Расстановщик СС ПС"));
            buttonDataFamilyAToFamilyB.SetContextualHelp(buttonDataFamilyAToFamilyBhelp);

            // группа

            panelSS.AddStackedItems(buttonDatass, buttonDataFamilyAToFamilyB);

            // стопка SchemeBuilder: конструктор, УГО, анализ

            PushButtonData buttonDataSchemeWizard = new PushButtonData(nameof(OpenWizardCommand), "Конструктор", typeof(OpenWizardCommand).Assembly.Location, typeof(OpenWizardCommand).FullName)
            {
                ToolTip = "Пошаговый сбор раздела СС: оборудование, коды, зоны, легенда, схема.",
                LongDescription =
                    "Шесть шагов в том порядке, в котором решения всё равно приходится принимать:\n\n" +
                    "1. Оборудование — какие категории считать своими.\n" +
                    "2. Коды и наименования — код из префикса марки, наименование из ADSK_Наименование, " +
                    "и то и другое правится.\n" +
                    "3. Зоны — чем считать ячейку схемы; помещения ищутся и в связях.\n" +
                    "4. Оформление — легенда, размеры колонок, проверка компонента-образца.\n" +
                    "5. Схема — матрица целиком, как она встанет на лист.\n" +
                    "6. Построение — что именно будет нарисовано.\n\n" +
                    "Каждый шаг показывает результат предыдущего, поэтому ошибка видна сразу, а не на " +
                    "готовом листе. Настройки и правки хранятся в модели."
            };
            RibbonIcons.Set(buttonDataSchemeWizard, RibbonIcons.SchemeBuilderPrefix + "legend16", RibbonIcons.SchemeBuilderPrefix + "legend32");
            ContextualHelp schemeWizardHelp = new ContextualHelp(ContextualHelpType.Url,
                HelpLinks.GetHelpLink("Конструктор СС"));
            buttonDataSchemeWizard.SetContextualHelp(schemeWizardHelp);

            PushButtonData buttonDataSchemeUgo = new PushButtonData(nameof(MapUgoCommand), "УГО", typeof(MapUgoCommand).Assembly.Location, typeof(MapUgoCommand).FullName)
            {
                ToolTip = "Находит УГО внутри семейств приборов и запоминает, какое из них ставить в схему.",
                LongDescription =
                    "Плоское УГО прибора лежит вложенным семейством внутри его собственного: типовой " +
                    "аннотацией либо элементом узла. Плагин открывает каждое семейство, показывает найденное " +
                    "и даёт выбрать нужное, если УГО несколько.\n\n" +
                    "Выбранное загружается в проект и ставится в блоки схемы; то, что в проекте уже есть, " +
                    "не перезаписывается. Обход долгий — семейства открываются по одному, зато делается он " +
                    "один раз: выбор хранится в модели."
            };
            RibbonIcons.Set(buttonDataSchemeUgo, RibbonIcons.SchemeBuilderPrefix + "legend16", RibbonIcons.SchemeBuilderPrefix + "legend32");
            buttonDataSchemeUgo.SetContextualHelp(schemeWizardHelp);

            PushButtonData buttonDataSchemeAnalyze = new PushButtonData(nameof(AnalyzeModelCommand), "Анализ", typeof(AnalyzeModelCommand).Assembly.Location, typeof(AnalyzeModelCommand).FullName)
            {
                ToolTip = "Выгружает устройство модели в текстовый файл — для настройки плагина под проект.",
                LongDescription =
                    "В отчёт попадает: заполненность категорий, типы оборудования с примерами марок, полный " +
                    "список параметров образца каждой категории, наличие помещений в модели и в связях, " +
                    "содержимое легенд, типы текста и уровни.\n\n" +
                    "Модель не изменяется — команда только читает. Файл кладётся в папку профиля, путь " +
                    "показывается после выгрузки."
            };
            RibbonIcons.Set(buttonDataSchemeAnalyze, RibbonIcons.SchemeBuilderPrefix + "scope16", RibbonIcons.SchemeBuilderPrefix + "scope32");
            buttonDataSchemeAnalyze.SetContextualHelp(schemeWizardHelp);

            panelSS.AddStackedItems(buttonDataSchemeWizard, buttonDataSchemeUgo, buttonDataSchemeAnalyze);

            /*
            // кнопка "IntersectionCheck"
            PushButtonData buttonDataIntersectionCheck = new PushButtonData("IntersectionCheck", "Проверка\nпересечений", typeof(???).Assembly.Location, typeof(IntersectionCheckCommand).FullName)
            {
                ToolTip = "Проверка пересечений между элементами текущего и связанных файлов с навигацией в 3D",
                LongDescription = "Показывает пересечения элементов текущего файла со связанными файлами\n\nГод напряженный - ищем и устраняем коллизии!"
            };
            RibbonIcons.Set(buttonDataIntersectionCheck, nameof(Properties.Resources.pikachu2_16), nameof(Properties.Resources.pikachu2_32));
            ContextualHelp buttonDataIntersectionCheckhelp = new ContextualHelp(ContextualHelpType.Url,
                HelpLinks.GetHelpLink("Проверка пересечений"));
            buttonDataIntersectionCheck.SetContextualHelp(buttonDataIntersectionCheckhelp);
            panel9p.AddItem(buttonDataIntersectionCheck);
            */
            #endregion

            #region Панель "Задания"

            // Панель "Задания"

            RibbonPanel panelTasks = application.CreateRibbonPanel(tabName, "Задания");

            // кнопка "Выдать задание"

            PushButtonData buttonDatatasksend = new PushButtonData(nameof(TaskSend), "Отправить\nзадание", typeof(TaskSend).Assembly.Location, typeof(TaskSend).FullName)
            {
                ToolTip = "Выдать/перевыдать задание в систему выдачи заданий."
            };
            RibbonIcons.Set(buttonDatatasksend, nameof(Properties.Resources.tasksend16), nameof(Properties.Resources.tasksend32));
            ContextualHelp gettaskhelp = new ContextualHelp(ContextualHelpType.Url,
                HelpLinks.GetHelpLink("Отправить задание"));
            buttonDatatasksend.SetContextualHelp(gettaskhelp);
            panelTasks.AddItem(buttonDatatasksend);

            // кнопка "Задания от ИОС"

            PushButtonData buttonDatagettask = new PushButtonData(nameof(TasksMenu), "Задания\nот ИОС", typeof(TasksMenu).Assembly.Location, typeof(TasksMenu).FullName)
            {
                ToolTip = "Проверить статусы выданных заданий, внедрить/обновить задание."
            };
            RibbonIcons.Set(buttonDatagettask, nameof(Properties.Resources.gettask16), nameof(Properties.Resources.gettask32));
            buttonDatagettask.SetContextualHelp(gettaskhelp);
            panelTasks.AddItem(buttonDatagettask);

            // группа кнопок "Автонумерация", "Найти по номеру", "Проверка отверстий"

            // сгруппированная кнопка "Автонумерация"

            PushButtonData buttonDatataskauto = new PushButtonData(nameof(TasksAutoMark), "Автонумерация", typeof(TasksAutoMark).Assembly.Location, typeof(TasksAutoMark).FullName)
            {
                ToolTip = "Пронумеровать элементы заданий в выбранной группе (в модели Заданий)."
            };
            RibbonIcons.Set(buttonDatataskauto, nameof(Properties.Resources.taskautomark16));
            ContextualHelp taskautohelp = new ContextualHelp(ContextualHelpType.Url,
                HelpLinks.GetHelpLink("Автонумерация заданий"));
            buttonDatataskauto.SetContextualHelp(taskautohelp);

            // сгруппированная кнопка "Найти по номеру"

            PushButtonData buttonDatagettaskelems = new PushButtonData(nameof(IdSelectionTasks), "Найти элементы", typeof(IdSelectionTasks).Assembly.Location, typeof(IdSelectionTasks).FullName)
            {
                ToolTip = "Найти отверстия или другие компоненты заданий по Маркам (позициям)."
            };
            RibbonIcons.Set(buttonDatagettaskelems, nameof(Properties.Resources.idselectionTasks16));
            buttonDatagettaskelems.SetContextualHelp(taskautohelp);

            // сгруппированная кнопка "Проверка отверстий"

            PushButtonData buttonDataholescheckdynamo = new PushButtonData(nameof(HolesCheckDynamo), "Проверка\nотверстий", typeof(HolesCheckDynamo).Assembly.Location, typeof(HolesCheckDynamo).FullName)
            {
                ToolTip = "Запустить скрипт Чек-лист.Отверстия (Dynamo)."
            };
            RibbonIcons.Set(buttonDataholescheckdynamo, nameof(Properties.Resources.dynpl16));
            buttonDataholescheckdynamo.SetContextualHelp(taskautohelp);

            panelTasks.AddStackedItems(buttonDatataskauto, buttonDatagettaskelems, buttonDataholescheckdynamo);

            // кнопка "Копировать отверстия"

            PushButtonData buttonDatacopyholes = new PushButtonData(nameof(CopyHolesCommand), "Копировать\nотверстия", typeof(CopyHolesCommand).Assembly.Location, typeof(CopyHolesCommand).FullName)
            {
                ToolTip = "Скопировать отверстия выбранной группы по нужным уровням либо обновить их на уровнях."
            };
            RibbonIcons.Set(buttonDatacopyholes, nameof(Properties.Resources.copyholes16), nameof(Properties.Resources.copyholes32));
            ContextualHelp holeshelp = new ContextualHelp(ContextualHelpType.Url,
                HelpLinks.GetHelpLink("Копировать отверстия"));
            buttonDatacopyholes.SetContextualHelp(holeshelp);
            panelTasks.AddItem(buttonDatacopyholes);

            // кнопка "Отметки Вырезание"

            PushButtonData buttonDataholes = new PushButtonData(nameof(Holes), "Отметки\nВырезание", typeof(Holes).Assembly.Location, typeof(Holes).FullName)
            {
                ToolTip = "Вырезать отверстия из стен и плит, заполнить отметки отверстий."
            };
            RibbonIcons.Set(buttonDataholes, nameof(Properties.Resources.holes16), nameof(Properties.Resources.holes32));
            buttonDataholes.SetContextualHelp(holeshelp);
            panelTasks.AddItem(buttonDataholes);

            
            #endregion

            #region Панели "BIM"

            // Панель "BIM Общие"

            RibbonPanel panel10 = application.CreateRibbonPanel(tabName, "BIM Общие");
            _BIMRibbonItems.Add(panel10);

            // кнопка "BIM Экспорт"

            PushButtonData buttonDatabim = new PushButtonData(nameof(BimExport), "BIM\nЭкспорт", typeof(BimExport).Assembly.Location, typeof(BimExport).FullName)
            {
                ToolTip = "Пакетный экспорт NWC, RVT (с очисткой)."
            };
            RibbonIcons.Set(buttonDatabim, nameof(Properties.Resources.nwc16), nameof(Properties.Resources.nwc32));
            ContextualHelp bimhelp = new ContextualHelp(ContextualHelpType.Url,
            HelpLinks.GetHelpLink("BIM Экспорт"));
            buttonDatabim.SetContextualHelp(bimhelp);
            panel10.AddItem(buttonDatabim);

            // кнопка "Открывашка"

            PushButtonData buttonDataOtkryvashka = new PushButtonData(nameof(Otkryvashka), "Открывашка", typeof(Otkryvashka).Assembly.Location, typeof(Otkryvashka).FullName)
            {
                ToolTip = "Пакетное открытие моделей с Revit Server (создать новый локальный)."
            };
            RibbonIcons.Set(buttonDataOtkryvashka, nameof(Properties.Resources.otkryvashka16), nameof(Properties.Resources.otkryvashka32));
            ContextualHelp otkryvashkahelp = new ContextualHelp(ContextualHelpType.Url,
                HelpLinks.GetHelpLink("Открывашка"));
            buttonDataOtkryvashka.SetContextualHelp(otkryvashkahelp);

            // кнопка "Закрывашка"

            PushButtonData buttonDataZakryvashka = new PushButtonData(nameof(Zakryvashka), "Закрывашка", typeof(Zakryvashka).Assembly.Location, typeof(Zakryvashka).FullName)
            {
                ToolTip = "Пакетная синхронизация локальных моделей с Revit Server и сохранение обычных файлов."
            };
            RibbonIcons.Set(buttonDataZakryvashka, nameof(Properties.Resources.zakryvashka16), nameof(Properties.Resources.zakryvashka32));
            ContextualHelp zakryvashkahelp = new ContextualHelp(ContextualHelpType.Url,
                HelpLinks.GetHelpLink("Закрывашка"));
            buttonDataZakryvashka.SetContextualHelp(zakryvashkahelp);

            // кнопка "Т Номер секции"

            PushButtonData buttonDataTParsSection = new PushButtonData(nameof(TParsSection), "Т Номер секции", typeof(TParsSection).Assembly.Location, typeof(TParsSection).FullName)
            {
                ToolTip = "Номер секции из Сведений о проекте в Т_Номер секции у всех элементов (АР, КР, КЖ, ОВ, ВК). " +
                          "При первом запуске добавляет параметр в Сведения о проекте и запрашивает значение."
            };

            panel10.AddStackedItems(buttonDataOtkryvashka, buttonDataZakryvashka, buttonDataTParsSection);

            // кнопка "Отчет" (TNovUtils): свод чек-листов по моделям за 7 дней

            PushButtonData buttonDataReport = new PushButtonData(nameof(ShowChecklistReportCommand), "Отчет", typeof(ShowChecklistReportCommand).Assembly.Location, typeof(ShowChecklistReportCommand).FullName)
            {
                ToolTip = "Свод чек-листов по моделям, изменённым проектировщиками за 7 дней: автопроверки, BIM-проверки, актуальность NWC."
            };
            RibbonIcons.Set(buttonDataReport, nameof(Properties.Resources.checklist16), nameof(Properties.Resources.checklist32));
            ContextualHelp reporthelp = new ContextualHelp(ContextualHelpType.Url,
                HelpLinks.GetHelpLink("Отчет"));
            buttonDataReport.SetContextualHelp(reporthelp);
            panel10.AddItem(buttonDataReport);

            // кнопка "Загрузить проект в TNovPRO" (вкладка «Модель» на сайте)

            PushButtonData buttonDataProModel = new PushButtonData(nameof(UploadProjectCommand), "TNovPRO\nМодель", typeof(UploadProjectCommand).Assembly.Location, typeof(UploadProjectCommand).FullName)
            {
                ToolTip = "Загрузить проект в TNovPRO: вопросы о модели на сайте с ответом в 3D.",
                LongDescription = "Выгружает дом целиком (сводный файл и выбранные связи) один раз. " +
                                  "Дальше сайт обновляется сам при каждой синхронизации с центральной моделью."
            };
            RibbonIcons.Set(buttonDataProModel, nameof(Properties.Resources.tnovpromodels16), nameof(Properties.Resources.tnovpromodels32));
            panel10.AddItem(buttonDataProModel);

            // Панель "BIM АР"

            RibbonPanel panel11 = application.CreateRibbonPanel(tabName, "BIM АР");
            _BIMRibbonItems.Add(panel11);

            // сгруппированная кнопка "Т Назначение"
            PushButtonData buttonDataTParsNazn = new PushButtonData(nameof(TParsNazn), "Т Назначение", typeof(TParsNazn).Assembly.Location, typeof(TParsNazn).FullName);

            // сгруппированная кнопка "Т Определение АР"
            PushButtonData buttonDataTParsOpredAR = new PushButtonData(nameof(TParsOpredAR), "Т Определение", typeof(TParsOpredAR).Assembly.Location, typeof(TParsOpredAR).FullName);
            
            //группа

            panel11.AddStackedItems(buttonDataTParsOpredAR, buttonDataTParsNazn);

            // Панель "BIM КЖ"

            RibbonPanel panel12 = application.CreateRibbonPanel(tabName, "BIM КЖ");
            _BIMRibbonItems.Add(panel12);

            // кнопка "Коды материалов"
            PushButtonData buttonDataMat = new PushButtonData(nameof(AssignMaterialCodesCommand), "Коды материалов", typeof(AssignMaterialCodesCommand).Assembly.Location, typeof(AssignMaterialCodesCommand).FullName);
            
            // кнопка "Т Определение КЖ"
            PushButtonData buttonDataTParsOpredST = new PushButtonData(nameof(TParsOpredST), "Т Определение", typeof(TParsOpredST).Assembly.Location, typeof(TParsOpredST).FullName);
            
            // кнопка "Т Наименование Обозначение КЖ"
            PushButtonData buttonDataTParsNaimOboznST = new PushButtonData(nameof(TParsNaimOboznST), "Т Наим Обозн", typeof(TParsNaimOboznST).Assembly.Location, typeof(TParsNaimOboznST).FullName);

            //группа

            panel12.AddStackedItems(buttonDataMat, buttonDataTParsOpredST, buttonDataTParsNaimOboznST);

            // Панель "BIM Сети"

            RibbonPanel panel13 = application.CreateRibbonPanel(tabName, "BIM Сети");
            _BIMRibbonItems.Add(panel13);

            // кнопка "Хосты изоляции"
            PushButtonData buttonDataInsulationHosts = new PushButtonData(nameof(InsulationHosts), "Хосты изоляции", typeof(InsulationHosts).Assembly.Location, typeof(InsulationHosts).FullName);
            
            // кнопка "Т Параметры ОВ ВК"
            PushButtonData buttonDataTParsOVVK = new PushButtonData(nameof(TParsSpecOVVK), "Т Параметры", typeof(TParsSpecOVVK).Assembly.Location, typeof(TParsSpecOVVK).FullName);

            // кнопка "Исключения Т_Диаметр"
            PushButtonData buttonDataTDiamExceptions = new PushButtonData(nameof(TDiamExceptionsCommand), "Исключения", typeof(TDiamExceptionsCommand).Assembly.Location, typeof(TDiamExceptionsCommand).FullName)
            {
                ToolTip = "Исключения заполнения Т_Диаметр (общий файл на сервере)."
            };
            
            //группа

            panel13.AddStackedItems(buttonDataInsulationHosts, buttonDataTParsOVVK, buttonDataTDiamExceptions);

            #endregion

            #region Панель "Тесты"

            // Панель "Тесты"

            RibbonPanel panelTests = application.CreateRibbonPanel(tabName, "Тесты");
            _TestRibbonItems.Add(panelTests);

            

            #endregion

            //после создания панелей скрываем лишние
            string appComboBoxJson = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "TNovClient/appComboBox.json");
            try
            {
                int comboBoxIndex = JsonConvert.DeserializeObject<int>(File.ReadAllText(appComboBoxJson));
                IList<ComboBoxMember> comboBoxItems = _comboBox.GetItems();
                if (comboBoxIndex >= 0 && comboBoxIndex < comboBoxItems.Count)
                {
                    _comboBox.Current = comboBoxItems[comboBoxIndex];
                    ComboBoxChangeSelection();
                }
            }
            catch { }

            // Блокировка сервером (tnovapi.json: BlockBelowVersion / BlockFilesMode). Сейчас — по
            // локальной копии политики; после фонового чтения шары — через ServerSettings.Changed.
            _controlledApp = application;
            TNovCommon.Server.ServerSettings.Changed += () => _blockPolicyDirty = true;
            ApplyPluginBlock();

            return Result.Succeeded;
        }

        #region Блокировка плагина сервером
        private static readonly List<UpdaterId> _updaterIds = new List<UpdaterId>();
        private static UIControlledApplication _controlledApp;
        private static volatile bool _blockPolicyDirty;
        private static string _blockReason;            // null — не заблокирован
        private static bool _blockMessagePending;
        private static string _startupMessage;         // ошибка запуска, показывается в первый Idling      // показать окно в ближайший Idling (вне запуска и модальных окон)
        private static readonly HashSet<string> _blockNotifiedDocs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>Плагин заблокирован сервером: обработчики событий пропускают свою работу.</summary>
        internal static bool IsPluginBlocked => _blockReason != null;

        /// <summary>
        /// Привести ленту и апдейтеры в соответствие с политикой блокировки. Только UI-поток Revit.
        /// Заблокирован — все кнопки TNov неактивны (кроме «Настройки»/«Справка» и списка «Режим»),
        /// апдейтеры выключены, окно с причиной. Разблокирован — всё обратно, без перезапуска Revit.
        /// </summary>
        private static void ApplyPluginBlock()
        {
            string reason = PluginVersionGate.BlockReason(TNovConfigLoad.GetCachedConfig());
            if (reason == _blockReason) return;

            bool blocked = reason != null;
            _blockReason = reason;
            if (blocked) _blockMessagePending = true;
            _blockNotifiedDocs.Clear();

            try
            {
                foreach (RibbonPanel panel in _controlledApp.GetRibbonPanels(RibbonTabName))
                    foreach (RibbonItem item in panel.GetItems())
                        SetBlocked(item, blocked);
            }
            catch (Exception ex) { Debug.WriteLine("Блокировка ленты: " + ex.Message); }

            foreach (UpdaterId id in _updaterIds)
            {
                try
                {
                    if (blocked) UpdaterRegistry.DisableUpdater(id);
                    else UpdaterRegistry.EnableUpdater(id);
                }
                catch (Exception ex) { Debug.WriteLine("Блокировка апдейтера: " + ex.Message); }
            }
        }

        private static void SetBlocked(RibbonItem item, bool blocked)
        {
            if (item is ComboBox) return; // «Режим» только скрывает панели
            if (item is PulldownButton pulldown) // в т.ч. SplitButton
                foreach (PushButton child in pulldown.GetItems())
                    SetBlocked(child, blocked);
            item.Enabled = !blocked || PluginVersionGate.IsAllowedWhenBlocked(item.Name);
        }

        /// <summary>Idling: применить изменившуюся политику и показать отложенное окно.</summary>
        private static void DrainPluginBlock()
        {
            if (_startupMessage != null)
            {
                string message = _startupMessage;
                _startupMessage = null;
                new InfoWindow280(message).ShowDialog();
            }
            if (_blockPolicyDirty)
            {
                _blockPolicyDirty = false;
                ApplyPluginBlock();
            }
            if (_blockMessagePending && _blockReason != null)
            {
                _blockMessagePending = false;
                new InfoWindow280(_blockReason).ShowDialog();
            }
        }

        /// <summary>Окно с причиной блокировки при открытии модели — не чаще раза на документ.</summary>
        private static void NotifyBlockedOnDocument(Document doc)
        {
            if (_blockReason == null || doc == null) return;
            string key = string.IsNullOrEmpty(doc.PathName) ? doc.Title : doc.PathName;
            if (_blockNotifiedDocs.Add(key)) _blockMessagePending = true;
        }
        #endregion
        public Result OnShutdown(UIControlledApplication application)
        {
            // Дописать журналы (usage, открытия, синхронизации); что не успело — сохранится локально.
            TNovCommon.Server.ServerOutbox.Shutdown(TimeSpan.FromSeconds(5));

            #region Апдейтеры отписка
            TNovHoleUpdater holeUpdater = new TNovHoleUpdater(application.ActiveAddInId);
            UpdaterRegistry.UnregisterUpdater(holeUpdater.GetUpdaterId());

            TNovShaftUpdater shaftUpdater = new TNovShaftUpdater(application.ActiveAddInId);
            UpdaterRegistry.UnregisterUpdater(shaftUpdater.GetUpdaterId());

            TNovWorksetUpdater worksetUpdater = new TNovWorksetUpdater(application.ActiveAddInId);
            UpdaterRegistry.UnregisterUpdater(worksetUpdater.GetUpdaterId());

            TNovPinUpdater pinUpdater = new TNovPinUpdater(application.ActiveAddInId);
            UpdaterRegistry.UnregisterUpdater(pinUpdater.GetUpdaterId());

            TNovPileUpdater pileUpdater = new TNovPileUpdater(application.ActiveAddInId);
            UpdaterRegistry.UnregisterUpdater(pileUpdater.GetUpdaterId());

            TNovTaskUpdater taskUpdater = new TNovTaskUpdater(application.ActiveAddInId);
            UpdaterRegistry.UnregisterUpdater(taskUpdater.GetUpdaterId());

            TNovWallUpdater wallUpdater = new TNovWallUpdater(application.ActiveAddInId);
            UpdaterRegistry.UnregisterUpdater(wallUpdater.GetUpdaterId());

            TNovRoomUpdater roomUpdater = new TNovRoomUpdater(application.ActiveAddInId);
            UpdaterRegistry.UnregisterUpdater(roomUpdater.GetUpdaterId());

            TNovFloorCeilingUpdater floorCeilingUpdater = new TNovFloorCeilingUpdater(application.ActiveAddInId);
            UpdaterRegistry.UnregisterUpdater(floorCeilingUpdater.GetUpdaterId());

            TNovInsulationUpdater insulationUpdater = new TNovInsulationUpdater(application.ActiveAddInId);
            UpdaterRegistry.UnregisterUpdater(insulationUpdater.GetUpdaterId());

            TNovParsOpredSTUpdater parsOpredSTUpdater = new TNovParsOpredSTUpdater(application.ActiveAddInId);
            UpdaterRegistry.UnregisterUpdater(parsOpredSTUpdater.GetUpdaterId());

            TNovParsOVVKUpdater parsOVVKUpdater = new TNovParsOVVKUpdater(application.ActiveAddInId);
            UpdaterRegistry.UnregisterUpdater(parsOVVKUpdater.GetUpdaterId());

            TNovParsNaimOboznSTUpdater parsNaimOboznSTUpdater = new TNovParsNaimOboznSTUpdater(application.ActiveAddInId);
            UpdaterRegistry.UnregisterUpdater(parsNaimOboznSTUpdater.GetUpdaterId());

            TNovParsOpredARUpdater parsOpredARUpdater = new TNovParsOpredARUpdater(application.ActiveAddInId); 
            UpdaterRegistry.UnregisterUpdater(parsOpredARUpdater.GetUpdaterId());

            TNovSectionNumberUpdater sectionNumberUpdater = new TNovSectionNumberUpdater(application.ActiveAddInId);
            UpdaterRegistry.UnregisterUpdater(sectionNumberUpdater.GetUpdaterId());
            #endregion
            #region События отписка
            application.ControlledApplication.DocumentOpening -= OnDocumentOpening;
            application.ControlledApplication.DocumentOpened -= OnDocumentOpened;
            application.ControlledApplication.DocumentSynchronizingWithCentral -= OnSyncCentralStart;
            application.ControlledApplication.DocumentSynchronizedWithCentral -= OnSyncCentralEnd;
            application.ControlledApplication.DocumentClosing -= OnDocumentClosing;
            application.ControlledApplication.DocumentChanged -= ModelSyncService.OnDocumentChanged;
            application.ControlledApplication.DocumentSaved -= OnDocumentSavedForTNovPro;
            application.Idling -= OnIdling;
            application.ViewActivated -= OnViewActivated;
            application.DialogBoxShowing -= a_DialogBoxShowing;
#if R2027
            application.ThemeChanged -= OnThemeChanged;
#endif
            HelpPaneHost.Shutdown();
            #endregion
            return Result.Succeeded;
        }
        #region Обработчики событий

        private void OnDocumentCreated(object sender, Autodesk.Revit.DB.Events.DocumentCreatedEventArgs e)
        {
            LoadSettings();
            if(_config.LicenseType=="corp"&&_config.CorpName=="ООО ПМ Новация"&&!IsPluginBlocked) //в перспективе - запускать для любой корп конфигурации (считывая с сайта)
            {
                //Проверка имени пользователя
                Application revitApp = sender as Application;
                UIApplication uiApp = new UIApplication(e.Document.Application);
                string userName = uiApp.Application.Username;
                string[] rolesFile = TNovCommon.Server.ServerData.ReadAllLines("roles.txt");
                bool correctUserName = false;
                foreach (string role in rolesFile)
                {
                    if (role.Contains(userName))
                    {
                        correctUserName = true; break;
                    }
                }

                if (!correctUserName)
                {
                    new InfoWindow280("Ваше имя пользователя в Revit: " + userName + "\n" +
                    "Имя должно соответствовать вашему логину в компании (пример: kadysheva.n). Измените имя в настройках Revit.").ShowDialog();

                    string link = HelpLinks.GetHelpLink("Старт работы");
                    string commandText = @link;
                    var proc = new System.Diagnostics.Process();
                    proc.StartInfo.FileName = commandText;
                    proc.StartInfo.UseShellExecute = true;
                    proc.Start();
                }
            }
        }
        private void OnDocumentOpening(object sender, DocumentOpeningEventArgs e)
        {
            //время открытия
            if (e.DocumentType == DocumentType.Project) _startTime = DateTime.Now;

            LoadSettings();
        }
        void a_DialogBoxShowing(object sender, DialogBoxShowingEventArgs e)
        {
            TaskDialogShowingEventArgs e2
              = e as TaskDialogShowingEventArgs;
            if (e2.Message == "RICOH MP C2011 PCL 6_2 - не может быть использовано с настройками печати А2А. Будут установлены <сеансные> настройки.") { e.OverrideResult(1); }
            if (e2.Message == "RICOH MP C2011 PCL 6 - не может быть использовано с настройками печати А1А. Будут установлены <сеансные> настройки.") { e.OverrideResult(1); }
            if (e2.Message == "RustDesk Printer - не может быть использовано с настройками печати А2А. Будут установлены <сеансные> настройки.") { e.OverrideResult(1); }
            if (e2.Message == "При импорте не обнаружено подходящих элементов в пространстве Бумага. Импортировать их из пространства модели?") { e.OverrideResult(1); }
            if (e2.DialogId== "TaskDialog_Missing_Third_Party_Updater") { e.OverrideResult(1); }
            if (e2.DialogId== "Dialog_Revit_DocWarnDialog") { e.OverrideResult(1); }
            /*if (e.DialogId == "Dialog_Revit_PurgeUnusedTree")
            {
                e.OverrideResult(1); // 1 = Cancel
                new InfoWindow280("Немедленно прекратите! Запрещено!").ShowDialog();
            }*/
        }
        public void OnDocumentOpened(object sender, DocumentOpenedEventArgs e)
        {
            LoadSettings();

            info = BasicFileInfo.Extract(e.Document.PathName);
            Document doc = e.Document;
            NotifyBlockedOnDocument(doc);

            if (_config.LicenseType == "corp" && !IsPluginBlocked) // заблокирован сервером — журнал не пишем
            {
                //время открытия (запись уходит в фоновую очередь, доступность сервера здесь не проверяем)
                if (_startTime.HasValue && info.IsWorkshared)
                {
                    double seconds = (DateTime.Now - _startTime.Value).TotalSeconds;
                    seconds = Math.Round(seconds);
                    string modelPath = e.Document.PathName;
                    string docName = Path.GetFileName(modelPath);
                    docName = docName.Replace(",", " ");
                    Autodesk.Revit.ApplicationServices.Application rvtApp = e.Document.Application;
                    string userName = rvtApp.Username; string docNameUserName = "_" + userName; docName = docName.Replace(docNameUserName, "");
                    docName = docName.Replace(".rvt", "");
                    string path = $"users/{userName},{docName}.txt";
                    // Получаем таблицу рабочих наборов
                    WorksetTable worksetTable = doc.GetWorksetTable();
                    FilteredWorksetCollector collector = new FilteredWorksetCollector(doc);
                    collector.OfKind(WorksetKind.UserWorkset);
                    List<string> openWorksets = new List<string>();
                    List<string> closedWorksets = new List<string>();
                    foreach (Workset workset in collector)
                    {
                        string wsName = workset.Name;
                        wsName = wsName.Replace(",", " ");
                        if (workset.IsOpen)
                            openWorksets.Add(wsName);
                        else
                            closedWorksets.Add(wsName);
                    }
                    string opened = String.Join(" ", openWorksets);
                    string closed = String.Join(" ", closedWorksets);
                    DateTime dateTime = DateTime.Now;
                    string date = dateTime.ToString(); date = date.Replace(":", "-"); date = date.Replace("/", "-"); date = date.Replace(" 0-00-00", "");
                    string fullUserName = WindowsIdentity.GetCurrent().Name;
                    string filePath = doc.PathName;
                    double fileSize = 0;
                    if (!string.IsNullOrEmpty(filePath))
                    {
                        FileInfo fileInfo = new FileInfo(filePath);
                        if (fileInfo.Exists)
                        {
                            fileSize = fileInfo.Length / 1048576.0;
                            fileSize = Math.Round(fileSize);
                        }
                    }
                    TNovCommon.Server.ServerOutbox.AppendLine(path, $"{date},{seconds},pc: {fullUserName},opened: {opened},closed: {closed},{fileSize}");
                }

            }

            //раскраска
            if (doc != null && doc.IsWorkshared)
            {
                if (!_docStopwatches.ContainsKey(doc))
                {
                    var sw = new Stopwatch();
                    sw.Start();
                    _docStopwatches[doc] = sw;
                }
            }
            /*if (info.IsWorkshared)
            {
                stopwatch = new Stopwatch();
                stopwatch.Start();
            }
            else stopwatch.Reset();*/
        }
        private void OnViewActivated(object sender, ViewActivatedEventArgs e)
        {
            LoadSettings();

            Document doc = e.Document;
            if (doc == null || !doc.IsWorkshared)
            {
                // Активный документ не поддерживает совместную работу – сбрасываем цвет
                _activeDocument = null;
                SetPanelColor(PanelColorState.None);
            }
            else
            {
                // Переключаемся на workshared-документ
                _activeDocument = doc;
                // Если по какой-то причине для него нет Stopwatch – создаём
                if (!_docStopwatches.ContainsKey(doc))
                {
                    var sw = new Stopwatch();
                    sw.Start();
                    _docStopwatches[doc] = sw;
                }
                // Принудительно обновим цвет в следующем Idling (там будет использован Stopwatch этого документа)
                _currentColor = PanelColorState.None; // чтобы гарантированно перерисовалось
            }
        }
        public void OnSyncCentralStart(object sender, DocumentSynchronizingWithCentralEventArgs e)
        {
            LoadSettings();

            Document doc = e.Document;
            if (_config.LicenseType == "corp" && !IsPluginBlocked) //подразумевается, что Корпоративная подписка содержит весь функционал
            {
                //задания
                
                Autodesk.Revit.ApplicationServices.Application app = doc.Application;

                string docName = doc.Title.ToString();
                bool taskModel = false; if (docName.Contains("Задани") || docName.Contains("задани") || docName.Contains("-ЗД") || docName.Contains("_ЗД") || docName.Contains("ЗАДАНИЕ")) taskModel = true;

                if (taskModel)
                {
                    string usagefilePath = serverPath + "usage.txt";
                    if (File.Exists(usagefilePath))
                    {
                        //сохранение заданий в базу
                        info = BasicFileInfo.Extract(e.Document.PathName);
                        string userName = info.Username;
                        TaskTools.SaveGroupsData(doc, userName);
                    }
                }
            }
            //подсветка
            if (syncOption != "Без подсветки панелей (не рекомендуется)") return;//stopwatch.Reset();

            if (doc != null && _docStopwatches.TryGetValue(doc, out Stopwatch sw))
            {
                sw.Reset(); // обнуляем таймер (остановлен)
                            // Если синхронизируется активный документ – сразу убираем подсветку
                if (doc.Equals(_activeDocument))
                {
                    SetPanelColor(PanelColorState.None);
                }
            }
        }

        /// <summary>«Модель» TNovPRO: файл без совместной работы обновляет сайт по сохранению.</summary>
        private void OnDocumentSavedForTNovPro(object sender, DocumentSavedEventArgs e)
        {
            try { ModelSyncService.OnSaved(e.Document); } catch { }
        }

        public void OnSyncCentralEnd(object sender, DocumentSynchronizedWithCentralEventArgs e)
        {
            // «Модель» TNovPRO — первой: журнал ниже пишет на сетевую папку и при
            // недоступной папке бросает исключение, обрывая всё, что после него.
            try { ModelSyncService.OnSynchronized(e.Document); } catch { }

            if (_config.LicenseType == "corp" && !IsPluginBlocked)
            {
                //журнал
                info = BasicFileInfo.Extract(e.Document.PathName);
                string docName = e.Document.Title;
                string userName = info.Username;
                string docNameUserName = "_" + userName; docName = docName.Replace(docNameUserName, "");
                docName = docName.Replace(",", "");
                DateTime dateTime = DateTime.Now; string TNovVersion = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString();
                string date = dateTime.ToString(); date = date.Replace(",", "");
                TNovCommon.Server.ServerOutbox.AppendLine($"projects/{docName},synchronizes.txt", date + "," + userName + "," + docName);
            }

            //подсветка
            Document doc = e.Document;
            if (doc != null && _docStopwatches.TryGetValue(doc, out Stopwatch sw))
            {
                sw.Restart(); // запускаем отсчёт заново
                              // Если это активный документ – цвет обновится при следующем Idling
            }/*
            stopwatch.Start();
            adWin.RibbonControl ribbon = adWin.ComponentManager.Ribbon;

            foreach (adWin.RibbonTab tab in ribbon.Tabs)
            {
                foreach (adWin.RibbonPanel panel in tab.Panels)
                {
                    panel.CustomPanelBackground = (SolidColorBrush)new BrushConverter().ConvertFromString("#F6F6F6");
                    panel.CustomPanelTitleBarBackground = (SolidColorBrush)new BrushConverter().ConvertFromString("#F6F6F6");
                }
            }*/
        }

        public void OnDocumentClosing(object sender, DocumentClosingEventArgs e)
        {
            LoadSettings();

            Document doc = e.Document;
            if (doc != null && _docStopwatches.ContainsKey(doc))
            {
                _docStopwatches.Remove(doc);
                if (doc.Equals(_activeDocument))
                {
                    _activeDocument = null;
                    SetPanelColor(PanelColorState.None);
                }
            }/*
            if (info.IsWorkshared)
            {
                stopwatch.Stop();
                adWin.RibbonControl ribbon = adWin.ComponentManager.Ribbon;
                foreach (adWin.RibbonTab tab in ribbon.Tabs)
                {
                    foreach (adWin.RibbonPanel panel in tab.Panels)
                    {
                        panel.CustomPanelBackground = (SolidColorBrush)new BrushConverter().ConvertFromString("#F6F6F6");
                        panel.CustomPanelTitleBarBackground = (SolidColorBrush)new BrushConverter().ConvertFromString("#F6F6F6");

                    }
                }
            }*/
        }

        public void OnIdling(object sender, IdlingEventArgs e)
        {
            // Блокировка плагина сервером: смена политики и отложенное окно с причиной.
            DrainPluginBlock();

            // Revit периодически пересоздаёт visual tree заголовков — восстанавливаем иконку при пропаже.
            EnsureRibbonTabIcon();

            // Отложенный показ панели справки: Idling гарантированно вне модального диалога.
            HelpPaneHost.DrainPendingShow(sender as UIApplication);
            // 1. Если нет активного workshared-документа или таймера – сбрасываем цвет
            if (_activeDocument == null ||
                !_docStopwatches.TryGetValue(_activeDocument, out Stopwatch sw) ||
                !sw.IsRunning)
            {
                SetPanelColor(PanelColorState.None);
                return;
            }

            // 2. Если подсветка отключена – сбрасываем
            if (time1 <= 0)
            {
                SetPanelColor(PanelColorState.None);
                return;
            }

            // 3. Вычисляем желаемое состояние
            long ms = sw.ElapsedMilliseconds;
            PanelColorState desired;
            if (ms > time2)
                desired = PanelColorState.IndianRed;
            else if (ms > time1)
                desired = PanelColorState.Gold;
            else
                desired = PanelColorState.None;

            // 4. Всегда перекрашиваем, если желаемое состояние не None,
            //    чтобы преодолеть возможный сброс цвета Revit'ом.
            //    Если желаемое None, красим только при реальной смене состояния.
            if (desired != PanelColorState.None)
            {
                SetPanelColor(desired);
            }
            else
            {
                if (_currentColor != PanelColorState.None)
                    SetPanelColor(PanelColorState.None);
            }

            /*
            if (info.IsWorkshared&&time1>0)
            {
                adWin.RibbonControl ribbon = adWin.ComponentManager.Ribbon;
                //цвета
                SolidColorBrush brush1 = new SolidColorBrush(Colors.Gold);
                SolidColorBrush brush2 = new SolidColorBrush(Colors.IndianRed);

                if (stopwatch.ElapsedMilliseconds > time1 && stopwatch.ElapsedMilliseconds < time2) 
                {
                    //перекраска ленты через time1
                    
                    foreach (adWin.RibbonTab tab in ribbon.Tabs)
                    {
                        foreach (adWin.RibbonPanel panel in tab.Panels)
                        {
                            panel.CustomPanelBackground = brush1;
                            panel.CustomPanelTitleBarBackground = brush1;
                        }
                    }
                    
                    
                }
                
                if (stopwatch.ElapsedMilliseconds > time2) 
                {
                    //перекраска ленты через time2

                    foreach (adWin.RibbonTab tab in ribbon.Tabs)
                    {
                        foreach (adWin.RibbonPanel panel in tab.Panels)
                        {
                            panel.CustomPanelBackground = brush2;
                            panel.CustomPanelTitleBarBackground = brush2;
                        }
                    }
                    stopwatch.Stop();
                    

                }
            }*/
        }
        private void OnCanExecutePurge(object sender, CanExecuteEventArgs e)
        {
            // Запрещаем выполнение команды. Кнопка в интерфейсе станет неактивной (серой).
            e.CanExecute = false;
        }
        private void OnPurgeExecuted(object sender, ExecutedEventArgs e)
        {
            // Это событие полностью ЗАМЕНЯЕТ стандартное поведение команды.
            // Revit не выполнит очистку, а просто выведет наше сообщение.
            new InfoWindow280("Эта команда отключена.").ShowDialog();
        }
        private void OnComboBoxCurrentChanged(object sender, EventArgs e)
        {
            ComboBoxChangeSelection();
        }
        #endregion
        #region Прочее
        //Обработчик изменения группы кнопок
        private void ComboBoxChangeSelection()
        {
            if (_comboBox.Current != null)
            {
                string selectedMode = _comboBox.Current.ItemText;

                // Обработка выбора

                foreach (var ribbonItem in _CommonRibbonItems)
                {
                    if (selectedMode == "Все" || selectedMode == "Общие")
                        ribbonItem.Visible = true;
                    else ribbonItem.Visible = false;
                }
                foreach (var ribbonItem in _BIMRibbonItems)
                {
                    if (selectedMode == "Все" || selectedMode == "BIM")
                        ribbonItem.Visible = true;
                    else ribbonItem.Visible = false;
                }
                foreach (var ribbonItem in _ARRibbonItems)
                {
                    if (selectedMode == "Все" || selectedMode == "АР")
                        ribbonItem.Visible = true;
                    else ribbonItem.Visible = false;
                }
                foreach (var ribbonItem in _STRibbonItems)
                {
                    if (selectedMode == "Все" || selectedMode == "КЖ")
                        ribbonItem.Visible = true;
                    else ribbonItem.Visible = false;
                }
                foreach (var ribbonItem in _MEPRibbonItems)
                {
                    if (selectedMode == "Все" || selectedMode == "Сети")
                        ribbonItem.Visible = true;
                    else ribbonItem.Visible = false;
                }
                foreach (var ribbonItem in _TestRibbonItems)
                {
                    if (selectedMode == "Все" || selectedMode == "Тесты")
                        ribbonItem.Visible = true;
                    else ribbonItem.Visible = false;
                }

                //Сериализация
                string appComboBoxJson = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "TNovClient/appComboBox.json");
                try
                {
                    IList<ComboBoxMember> comboBoxItems = _comboBox.GetItems();
                    int comboBoxIndex = 0;
                    foreach (var comboBoxMember in comboBoxItems)
                    {
                        if (selectedMode == comboBoxMember.ItemText)
                        {
                            File.WriteAllText(appComboBoxJson, JsonConvert.SerializeObject(comboBoxIndex)); break;
                        }
                        comboBoxIndex++;
                    }
                }
                catch { }

            }
        }

        // раскраска
        internal void ResetPanelColors() => SetPanelColor(PanelColorState.None);

        private void SetPanelColor(PanelColorState state)
        {
            adWin.RibbonControl ribbon = adWin.ComponentManager.Ribbon;
            if (ribbon == null) return;

            Brush backgroundBrush, titleBrush;
            switch (state)
            {
                case PanelColorState.Gold:
                    backgroundBrush = BrushGold;
                    titleBrush = BrushGold;
                    break;
                case PanelColorState.IndianRed:
                    backgroundBrush = BrushIndianRed;
                    titleBrush = BrushIndianRed;
                    break;
                default:
                    backgroundBrush = null;
                    titleBrush = null;
                    break;
            }

            foreach (adWin.RibbonTab tab in ribbon.Tabs)
            {
                foreach (adWin.RibbonPanel panel in tab.Panels)
                {
                    panel.CustomPanelBackground = backgroundBrush;
                    panel.CustomPanelTitleBarBackground = titleBrush;
                }
            }
            _currentColor = state;
        }
        private void LoadSettings()
        {
            string jsonpath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "TNovClient/TNovSettings.json");

            if (!File.Exists(jsonpath)) return;

            try
            {
                var viewModel = JsonConvert.DeserializeObject<AppVersionViewModel>(File.ReadAllText(jsonpath));
                syncOption = viewModel.sync1;

                // Определяем временные интервалы
                if (syncOption == "Подсветка 20/30 минут")
                {
                    time1 = 1200000; time2 = 1800000;
                }
                else if (syncOption == "Подсветка 30/60 минут")
                {
                    time1 = 1800000; time2 = 3600000;
                }
                else if (syncOption == "Подсветка 40/60 минут")
                {
                    time1 = 2400000; time2 = 3600000;
                }
                else if (syncOption == "Подсветка 60/90 минут")
                {
                    time1 = 3600000; time2 = 4800000;
                }
                else if (syncOption.Contains("Подсветка 1/2 минуты"))
                {
                    time1 = 60000; time2 = 120000;
                }

                bool newCanPurge = viewModel.canPurge;
                bool newCanCreateParts = viewModel.canCreateParts;
                _canPurge = newCanPurge;
                if (_canPurge && _purgeExecutedSubscribed)
                {
                    _purgeBinding.Executed -= OnPurgeExecuted;
                    _purgeExecutedSubscribed = false;
                }
                else if (!_canPurge && !_purgeExecutedSubscribed)
                {
                    _purgeBinding.Executed += OnPurgeExecuted;
                    _purgeExecutedSubscribed = true;
                }
                _canCreateParts = newCanCreateParts;
                if (_canCreateParts && _partsExecutedSubscribed)
                {
                    _partsBinding.Executed -= OnPurgeExecuted;
                    _partsExecutedSubscribed = false;
                }
                else if (!_canCreateParts && !_partsExecutedSubscribed)
                {
                    _partsBinding.Executed += OnPurgeExecuted;
                    _partsExecutedSubscribed = true;
                }
            }
            catch {}
        }
        public void ReloadSettings()
        {
            LoadSettings();
            // Если сейчас активен workshared-документ, принудительно обновим цвет
            _currentColor = PanelColorState.None;
        }

#if R2027
        /// <summary>Смена темы Revit: значки кнопок и вкладки перечитываются под новую тему.</summary>
        private void OnThemeChanged(object sender, ThemeChangedEventArgs e)
        {
            if (e.ThemeChangedType != ThemeType.UITheme || _controlledApp == null)
                return;

            RibbonIcons.Apply(_controlledApp, RibbonTabName);
            _ribbonTabIconSource = null;
            _ribbonTabIconWatch.Reset(); // значок вкладки обновится в ближайший Idling
        }
#endif
        /// <summary>
        /// Держит иконку на заголовке вкладки (AdWindows/WPF). Revit может сбросить visual tree —
        /// поэтому вызывается из OnIdling и восстанавливает иконку, если её уже нет.
        /// </summary>
        private void EnsureRibbonTabIcon()
        {
            if (_ribbonTabIconWatch.IsRunning
                && _ribbonTabIconWatch.ElapsedMilliseconds < RibbonTabIconCheckMs)
                return;

            _ribbonTabIconWatch.Restart();

            try
            {
                adWin.RibbonControl ribbon = adWin.ComponentManager.Ribbon;
                if (ribbon == null || !ribbon.IsLoaded)
                    return;

                if (_ribbonTabIconSource == null)
                    _ribbonTabIconSource = RibbonIcons.Get(nameof(Properties.Resources.logomin));

                // Уже на месте — ничего не трогаем (избегаем мерцания); после смены темы только меняем картинку.
                foreach (Image existing in FindVisualChildren<Image>(ribbon))
                {
                    if (existing.Name == RibbonTabIconName)
                    {
                        if (existing.Source != _ribbonTabIconSource)
                            existing.Source = _ribbonTabIconSource;
                        return;
                    }
                }

                foreach (TextBlock textBlock in FindVisualChildren<TextBlock>(ribbon))
                {
                    if (!string.Equals(textBlock.Text, RibbonTabName, StringComparison.Ordinal))
                        continue;

                    if (VisualTreeHelper.GetParent(textBlock) is System.Windows.Controls.Panel siblingPanel
                        && siblingPanel.Children.OfType<Image>().Any(img => img.Name == RibbonTabIconName))
                        return;

                    Image image = CreateRibbonTabIcon();

                    // Предпочтительно вставить Image рядом с существующим TextBlock
                    // (не ломаем binding Title у ContentPresenter).
                    if (VisualTreeHelper.GetParent(textBlock) is System.Windows.Controls.Panel panel)
                    {
                        int index = panel.Children.IndexOf(textBlock);
                        if (index >= 0)
                        {
                            panel.Children.Insert(index, image);
                            return;
                        }
                    }

                    for (DependencyObject current = textBlock; current != null; current = VisualTreeHelper.GetParent(current))
                    {
                        DependencyObject parent = VisualTreeHelper.GetParent(current);

                        if (parent is Decorator decorator
                            && ReferenceEquals(decorator.Child, current)
                            && current is UIElement uiChild)
                        {
                            decorator.Child = null;
                            var stack = new StackPanel
                            {
                                Orientation = Orientation.Horizontal,
                                VerticalAlignment = VerticalAlignment.Center
                            };
                            stack.Children.Add(image);
                            stack.Children.Add(uiChild);
                            decorator.Child = stack;
                            return;
                        }

                        if (parent is ContentPresenter presenter
                            && (ReferenceEquals(presenter.Content, current)
                                || (presenter.Content is string title
                                    && string.Equals(title, RibbonTabName, StringComparison.Ordinal))))
                        {
                            var stack = new StackPanel
                            {
                                Orientation = Orientation.Horizontal,
                                VerticalAlignment = VerticalAlignment.Center
                            };
                            stack.Children.Add(image);
                            if (current is UIElement keep)
                                stack.Children.Add(keep);
                            else
                                stack.Children.Add(new TextBlock
                                {
                                    Text = RibbonTabName,
                                    VerticalAlignment = VerticalAlignment.Center
                                });
                            presenter.Content = stack;
                            return;
                        }
                    }
                }
            }
            catch
            {
                // AdWindows — unsupported API.
            }
        }

        private Image CreateRibbonTabIcon()
        {
            return new Image
            {
                Name = RibbonTabIconName,
                Source = _ribbonTabIconSource,
                Width = 16,
                Height = 16,
                Margin = new Thickness(0, 0, 4, 0),
                VerticalAlignment = VerticalAlignment.Center,
                SnapsToDevicePixels = true
            };
        }

        private static IEnumerable<T> FindVisualChildren<T>(DependencyObject parent) where T : DependencyObject
        {
            if (parent == null)
                yield break;

            int count = VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < count; i++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(parent, i);
                if (child is T match)
                    yield return match;

                foreach (T descendant in FindVisualChildren<T>(child))
                    yield return descendant;
            }
        }

        private Encoding DetectEncoding(string filePath)
        {
            byte[] buffer = File.ReadAllBytes(filePath);
            // Пробуем интерпретировать как UTF-8 с проверкой на недопустимые последовательности
            try
            {
                var utf8 = new UTF8Encoding(false, true); // encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true
                utf8.GetString(buffer);
                // Если исключение не выброшено – скорее всего, файл в UTF-8
                return new UTF8Encoding(false);
            }
            catch
            {
                // Не является валидным UTF-8 – используем системную кодировку (ANSI)
                return Encoding.Default;
            }
        }

        private void EnsureTNovClient()
        {
            string localFolder = clientFolderPath;
            string localExe = Path.Combine(localFolder, "TNovClient.exe");
            string localDll = Path.Combine(localFolder, "TNovClient.dll");
            string serverClientFolder = Path.Combine(serverPath, "actual", "client");
            string serverVersionFile = Path.Combine(serverPath, "actual", "clientversion.txt");

            Version localVersion = TryReadClientFileVersion(localDll) ?? new Version(1, 0, 0, 0);
            Version actualVersion = TryReadClientTextVersion(serverVersionFile)
                ?? TryReadClientFileVersion(Path.Combine(serverClientFolder, "TNovClient.dll"))
                ?? localVersion;

            bool selfUpdating = localVersion >= MinSelfUpdatingClientVersion;
            bool needsUpdate = actualVersion > localVersion;

            if (needsUpdate && !selfUpdating)
            {
                try
                {
                    foreach (Process process in Process.GetProcessesByName("TNovClient"))
                    {
                        try { process.Kill(); }
                        catch (Exception) { }
                    }

                    Thread.Sleep(5000);
                    Directory.CreateDirectory(localFolder);
                    if (Directory.Exists(serverClientFolder))
                    {
                        foreach (string file in Directory.GetFiles(serverClientFolder))
                            File.Copy(file, Path.Combine(localFolder, Path.GetFileName(file)), true);
                    }
                }
                catch (Exception) { }
            }

            if (!Process.GetProcessesByName("TNovClient").Any() && File.Exists(localExe))
            {
                try
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = localExe,
                        UseShellExecute = true
                    });
                }
                catch (Exception) { }
            }
        }

        private static Version TryReadClientFileVersion(string path)
        {
            try
            {
                if (!File.Exists(path))
                    return null;
                return TryParseClientVersion(FileVersionInfo.GetVersionInfo(path).FileVersion)
                    ?? TryParseClientVersion(FileVersionInfo.GetVersionInfo(path).ProductVersion);
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static Version TryReadClientTextVersion(string path)
        {
            try
            {
                if (!File.Exists(path))
                    return null;
                return TryParseClientVersion(File.ReadAllText(path));
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static Version TryParseClientVersion(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return null;

            string cleaned = value.Trim();
            int cut = cleaned.IndexOfAny(new[] { '+', '-', ' ', '\t' });
            if (cut >= 0)
                cleaned = cleaned.Substring(0, cut);

            string[] parts = cleaned.Split('.');
            if (parts.Length == 0)
                return null;

            int count = Math.Min(parts.Length, 4);
            int[] nums = new int[4];
            for (int i = 0; i < count; i++)
            {
                if (!int.TryParse(parts[i], out nums[i]) || nums[i] < 0)
                    return null;
            }

            return new Version(nums[0], nums[1], nums[2], nums[3]);
        }

        public static TNovConfig LoadConfig() 
        {
            string configPath = Path.Combine(clientFolderPath, "TNovConfig.json");

            try
            {
                string jsonContent = File.ReadAllText(configPath);
                TNovConfig config = JsonConvert.DeserializeObject<TNovConfig>(jsonContent);
                return config;
            }
            catch (JsonException ex)
            {
                Debug.WriteLine($"Ошибка при десериализации JSON: {ex.Message}");
                return null;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Неожиданная ошибка: {ex.Message}");
                return null;
            }
        }
        #endregion
    }
}