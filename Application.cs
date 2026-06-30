#region Ссылки
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Events;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Events;
using Autodesk.Windows;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Reflection;
using System.Security.Principal;
using System.Text;
using System.Threading;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using TNovBeams;
using TNovBIMUtils;
using TNovCommon;
using TNovElectrical;
using TNovFinishing;
using TNovMEPSpec;
using TNovParking;
using TNovPiles;
using TNovRooms;
using TNovSS;
using TNovTasks;
using TNovUtils;
using TNovUtilsAR;
using TNovUtilsST;
using TNovVent;
using TNovViewsSheets;
using static System.Windows.Forms.LinkLabel;
using adWin = Autodesk.Windows;
using ComboBox = Autodesk.Revit.UI.ComboBox;
using RibbonItem = Autodesk.Revit.UI.RibbonItem;
using RibbonPanel = Autodesk.Revit.UI.RibbonPanel;
using SplitButton = Autodesk.Revit.UI.SplitButton;
using TypeFilter = TNovUtils.TypeFilter;

/*
git add .
git commit -m "4.0.1"
git push origin main
 */
#endregion

namespace TNov
{
    [Regeneration(RegenerationOption.Manual)]
    internal class Application : IExternalApplication
    {
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
        private static readonly SolidColorBrush BrushDefault =
            (SolidColorBrush)new BrushConverter().ConvertFromString("#F6F6F6");
        //параметры переключения ленты
        private static List<RibbonPanel> _CommonRibbonItems = new List<RibbonPanel>();
        private static List<RibbonPanel> _ARRibbonItems = new List<RibbonPanel>();
        private static List<RibbonPanel> _STRibbonItems = new List<RibbonPanel>();
        private static List<RibbonPanel> _MEPRibbonItems = new List<RibbonPanel>();
        private static List<RibbonPanel> _BIMRibbonItems = new List<RibbonPanel>();
        private ComboBox _comboBox;

        TNovConfig _config = new TNovConfig();
        static string clientFolderPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "TNovClient");
        static string serverPath = clientFolderPath; //"//fs-nova/Distr/0.For Admin/_TNov/"
        #endregion
        public Result OnStartup(UIControlledApplication application)
        {
            #region Конфигурация и настройки программы
            //конфиг
            _config = LoadConfig();
            if (_config.LicenseType != null)
            {
                Debug.WriteLine($"Конфигурация загружена: LicenseType={_config.LicenseType}, CorpName={_config.CorpName}, ServerPath={_config.ServerPath}");
                if(_config.LicenseType=="corp") serverPath = _config.ServerPath;
                serverPath = serverPath.Replace('/', '\\');
                if (!serverPath.StartsWith(@"\\"))
                    serverPath = @"\\" + serverPath.TrimStart('/');
            }
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
                application.Idling += OnIdling;
                application.ViewActivated += OnViewActivated;
                application.ControlledApplication.DocumentCreated += OnDocumentCreated;
                application.DialogBoxShowing += new EventHandler<DialogBoxShowingEventArgs>(a_DialogBoxShowing);
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
            #endregion
            #region Клиент   
            // проверка актуальности клиента, переустановка и перезапуск клиента

            bool run = Process.GetProcessesByName("TNovClient").Any();
            //C:\Users\%username%\TNovClient
            string curClientVersion = "1.0.0.0";
            try
            {
                curClientVersion = FileVersionInfo.GetVersionInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "TNovClient/TNovClient.dll")).FileVersion;
            }
            catch (Exception) { }
            string[] versionpartsC = curClientVersion.Split('.');
            double versionMathC = Convert.ToDouble(versionpartsC[0] + "000000") + Convert.ToDouble(versionpartsC[1] + "0000") +
                Convert.ToDouble(versionpartsC[2] + "00") + Convert.ToDouble(versionpartsC[3]);
 
            string verfilePathC = serverPath + "actual/clientversion.txt";
            string actualVersionC = curClientVersion;
            try
            {
                actualVersionC = File.ReadAllText(verfilePathC);
            }
            catch (Exception) { }
            string[] actversionpartsC = actualVersionC.Split('.');
            double actversionMathC = Convert.ToDouble(actversionpartsC[0] + "000000") + Convert.ToDouble(actversionpartsC[1] + "0000") +
                Convert.ToDouble(actversionpartsC[2] + "00") + Convert.ToDouble(actversionpartsC[3]);

            if (actversionMathC > versionMathC)
            {
                try
                {
                    if (run) { Process.GetProcessesByName("TNovClient").First().Kill(); }
                    Thread.Sleep(5000);
                    string[] filesInFolder = Directory.GetFiles(serverPath + "actual/client")
                              .ToArray();
                    foreach (var file in filesInFolder)
                    { 
                        var fileName = Path.GetFileName(file);
                        File.Copy(file, Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), $"TNovClient/{fileName}"), true);
                    }
                }
                catch (Exception) { }
            }
            bool run1 = Process.GetProcessesByName("TNovClient").Any();
            if (!run1)
            {
                try
                {
                    Process.Start(@"C://Users/" + Environment.UserName + "/TNovClient/TNovClient.exe");
                }
                catch (Exception) { }
            }
#endregion
            
            // Создание вкладок, панелей, кнопок

            string assebblyLocation = Assembly.GetExecutingAssembly().Location, tabName = "TNov";

            application.CreateRibbonTab(tabName);

            ContextualHelp mainhelp = new ContextualHelp(ContextualHelpType.Url,
                "https://portal.talan.group/knowledge/proektirovanie/");

            #region Панель "Настройки"

            // Панель "Настройки"

            RibbonPanel panel0 = application.CreateRibbonPanel(tabName, "Настройки");

            ComboBoxData comboData = new ComboBoxData("Режим");


            // кнопка "Настройки"

            System.Drawing.Image imgN = Properties.Resources.logo;
            System.Drawing.Image imgNmin = Properties.Resources.logomin;
            PushButtonData buttonDataN = new PushButtonData(nameof(AppVersion), "Настройки", typeof(AppVersion).Assembly.Location, typeof(AppVersion).FullName)
            {
                //LargeImage = GetImageSource(imgN),
                Image = GetImageSource(imgNmin),
                ToolTip = "Глобальные настройки плагина и сведения о программе."
            };
            buttonDataN.SetContextualHelp(mainhelp);

            // кнопка "Настройки"
            /*
            PushButtonData buttonDataTest = new PushButtonData(nameof(PluginSettingsCommand), "Настройки", typeof(PluginSettingsCommand).Assembly.Location, typeof(PluginSettingsCommand).FullName)
            {
                //LargeImage = GetImageSource(imgN),
                Image = GetImageSource(imgNmin),
                ToolTip = "Глобальные настройки плагина и сведения о программе."
            };
            buttonDataTest.SetContextualHelp(mainhelp);
            */
            IList<RibbonItem> ribbonItemList0 = panel0.AddStackedItems(buttonDataN, (RibbonItemData)comboData);//, buttonDataTest);
            _comboBox = ribbonItemList0[1] as ComboBox; 
            _comboBox.AddItem(new ComboBoxMemberData("Все", "Все"));
            _comboBox.AddItem(new ComboBoxMemberData("Общие", "Общие"));
            _comboBox.AddItem(new ComboBoxMemberData("АР", "АР"));
            _comboBox.AddItem(new ComboBoxMemberData("КЖ", "КЖ"));
            _comboBox.AddItem(new ComboBoxMemberData("Сети", "Сети"));
            _comboBox.AddItem(new ComboBoxMemberData("BIM", "BIM"));
            _comboBox.CurrentChanged += OnComboBoxCurrentChanged; //подписка на событие изменения выбора

            // кнопка "Тестовая команда"
            /*
            PushButtonData buttonDataMindmap = new PushButtonData(nameof(Mindmap), "Mindmap", typeof(Mindmap).Assembly.Location, typeof(Mindmap).FullName)
            {
                LargeImage = GetImageSource(imgN),
                Image = GetImageSource(imgNmin),
                ToolTip = "Mindmap."
            };
            buttonDataMindmap.SetContextualHelp(mainhelp);
            panel0.AddItem(buttonDataMindmap);
            */
            #endregion

            #region Панель "Проект"

            // Панель "Проект"

            RibbonPanel panelСommon = application.CreateRibbonPanel(tabName, "Общее");
            _CommonRibbonItems.Add(panelСommon);

            // кнопка "Журнал проекта"

            System.Drawing.Image imgCDE = Properties.Resources.CDE32;
            System.Drawing.Image imgCDEmin = Properties.Resources.CDE16;
            PushButtonData buttonDataCDE = new PushButtonData(nameof(Journal), "Журнал\nпроекта", typeof(Journal).Assembly.Location, typeof(Journal).FullName)
            {
                LargeImage = GetImageSource(imgCDE),
                Image = GetImageSource(imgCDEmin),
                ToolTip = "Открыть Журнал проекта - чек-лист задач по модели, журнал синхронизаций."
            };
            ContextualHelp CDEhelp = new ContextualHelp(ContextualHelpType.Url,
                "https://portal.talan.group/knowledge/proektirovanie/");
            buttonDataCDE.SetContextualHelp(CDEhelp);
                        
            panelСommon.AddItem(buttonDataCDE);

            // сгруппированная кнопка "Таблица параметров"

            System.Drawing.Image imgParamTable = Properties.Resources.ParamTable32;
            System.Drawing.Image imgParamTablemin = Properties.Resources.ParamTable16;
            PushButtonData buttonDataParamTable = new PushButtonData(nameof(ParamTable), "Таблица параметров", typeof(ParamTable).Assembly.Location, typeof(ParamTable).FullName)
            {
                Image = GetImageSource(imgParamTablemin),
                ToolTip = "Открыть таблицу требований к модели."
            };
            buttonDataParamTable.SetContextualHelp(CDEhelp);

            // сгруппированная кнопка "База знаний"

            System.Drawing.Image imgwiki = Properties.Resources.wiki32;
            System.Drawing.Image imgwikimin = Properties.Resources.wiki16;
            PushButtonData buttonDatawiki = new PushButtonData(nameof(WorkOrg), "База знаний", typeof(WorkOrg).Assembly.Location, typeof(WorkOrg).FullName)
            {
                Image = GetImageSource(imgwikimin),
                ToolTip = "Wiki по работе в Revit и не только."
            };
            buttonDatawiki.SetContextualHelp(mainhelp);

            // сгруппированная кнопка "Учебный портал"

            System.Drawing.Image imgedu = Properties.Resources.edu32;
            System.Drawing.Image imgedumin = Properties.Resources.edu16;
            PushButtonData buttonDataedu = new PushButtonData(nameof(EduPortal), "Учебный портал", typeof(EduPortal).Assembly.Location, typeof(EduPortal).FullName)
            {
                Image = GetImageSource(imgedumin),
                ToolTip = "Перейти на учебный портал (moodle.talan.group)."
            };
            ContextualHelp eduhelp = new ContextualHelp(ContextualHelpType.Url,
                "https://moodle.talan.group");
            buttonDataedu.SetContextualHelp(eduhelp);

            // группа кнопок "Таблица параметров", "База знаний", "Учебный портал"

            panelСommon.AddStackedItems(buttonDataParamTable, buttonDatawiki, buttonDataedu);

            #endregion

            #region Панель "Виды и листы"

            // Панель "Виды и листы"

            RibbonPanel panelViewsSheets = application.CreateRibbonPanel(tabName, "Виды и листы");
            _CommonRibbonItems.Add(panelViewsSheets);

            // кнопка "Менеджер листов"

            System.Drawing.Image imgsheets = Properties.Resources.sheets32;
            System.Drawing.Image imgsheetsmin = Properties.Resources.sheets16;
            PushButtonData buttonDatasheets = new PushButtonData(nameof(Sheets), "Менеджер\nлистов", typeof(Sheets).Assembly.Location, typeof(Sheets).FullName)
            {
                LargeImage = GetImageSource(imgsheets),
                Image = GetImageSource(imgsheetsmin),
                ToolTip = "Перенумерация листов, формирование комплектов на печать."
            };
            ContextualHelp sheetshelp = new ContextualHelp(ContextualHelpType.Url,
                "https://portal.talan.group/knowledge/proektirovanie/listynumeratsiyaikomplektynaeksport/");
            buttonDatasheets.SetContextualHelp(sheetshelp);
            panelViewsSheets.AddItem(buttonDatasheets);
            
            
            // сгруппированная кнопка "Изменения"

            System.Drawing.Image imgchanges = Properties.Resources.changes32;
            System.Drawing.Image imgchangesmin = Properties.Resources.changes16;
            PushButtonData buttonDatachanges = new PushButtonData(nameof(Changes), "Изменения", typeof(Changes).Assembly.Location, typeof(Changes).FullName)
            {
                Image = GetImageSource(imgchangesmin),
                ToolTip = "Автонумерация облаков и заполнение параметров листов."
            };
            ContextualHelp changeshelp = new ContextualHelp(ContextualHelpType.Url,
                "https://portal.talan.group/knowledge/proektirovanie/oformlenie/");
            buttonDatachanges.SetContextualHelp(changeshelp);

            // подкнопка "Excel"

            System.Drawing.Image imgexcel = Properties.Resources.excel32;
            System.Drawing.Image imgexcelmin = Properties.Resources.excel16;
            PushButtonData buttonDataexcel = new PushButtonData(nameof(Excel), "Excel", typeof(Excel).Assembly.Location, typeof(Excel).FullName)
            {
                Image = GetImageSource(imgexcelmin),
                ToolTip = "Экспорт спецификации в Excel."
            };
            ContextualHelp excelhelp = new ContextualHelp(ContextualHelpType.Url,
                "https://portal.talan.group/knowledge/proektirovanie/plaginyiskriptynovatsiya/");
            buttonDataexcel.SetContextualHelp(excelhelp);

            // подкнопка "Excel.Настройки"

            PushButtonData buttonDataexcelSettings = new PushButtonData(nameof(ExcelSettings), "Excel.Настройки", typeof(ExcelSettings).Assembly.Location, typeof(ExcelSettings).FullName)
            {
                Image = GetImageSource(imgexcelmin),
                ToolTip = "Экспорт спецификации в Excel."
            };
            buttonDataexcelSettings.SetContextualHelp(excelhelp);

            // группа кнопок "Изменения", "Excel"

            SplitButtonData splitButtonDataExcel = new SplitButtonData("Excel", "Экспорт спецификации в Excel.");
            IList<RibbonItem> ribbonItemList = panelViewsSheets.AddStackedItems(buttonDatachanges, (RibbonItemData)splitButtonDataExcel);
            SplitButton splitButtonExcel = ribbonItemList[1] as SplitButton;
            ((PulldownButton)splitButtonExcel).AddPushButton(buttonDataexcel);
            ((PulldownButton)splitButtonExcel).AddPushButton(buttonDataexcelSettings);

            // кнопка "Экспорт листов"

            System.Drawing.Image imgexport = Properties.Resources.exportsheets32;
            System.Drawing.Image imgexportmin = Properties.Resources.exportsheets16;
            PushButtonData buttonDataexport = new PushButtonData(nameof(ExportSheetsCommand), "Экспорт\nлистов", typeof(ExportSheetsCommand).Assembly.Location, typeof(ExportSheetsCommand).FullName)
            {
                LargeImage = GetImageSource(imgexport),
                Image = GetImageSource(imgexportmin),
                ToolTip = "Пакетный экспорт в DWG (единый файл) и PDF."
            };
            ContextualHelp exporthelp = new ContextualHelp(ContextualHelpType.Url,
                "https://portal.talan.group/knowledge/proektirovanie/eksportpdfidwgizrevit/");
            buttonDataexport.SetContextualHelp(sheetshelp);
            panelViewsSheets.AddItem(buttonDataexport);


            #endregion

            #region Панель "Утилиты"

            // Панель "Утилиты"

            RibbonPanel panelUtils = application.CreateRibbonPanel(tabName, "Утилиты");
            _CommonRibbonItems.Add(panelUtils);

            // кнопка "Связной"

            System.Drawing.Image imglinks = Properties.Resources.links32;
            System.Drawing.Image imglinksmin = Properties.Resources.links16;
            PushButtonData buttonDatalinks = new PushButtonData(nameof(Links), "Связной", typeof(Links).Assembly.Location, typeof(Links).FullName)
            {
                LargeImage = GetImageSource(imglinks),
                Image = GetImageSource(imglinksmin),
                ToolTip = "Пакетная вставка связей с помещением их в рабочие наборы."
            };
            ContextualHelp linkshelp = new ContextualHelp(ContextualHelpType.Url,
                "https://portal.talan.group/knowledge/proektirovanie/plaginyiskriptynovatsiya/");
            buttonDatalinks.SetContextualHelp(linkshelp);
            panelUtils.AddItem(buttonDatalinks);
           

            // кнопка с выпадающим списком "Закреплятор Уровни Наборы"

            // - подкнопка "Закреплятор Уровни Наборы"

            System.Drawing.Image imgplw = Properties.Resources.plw32;
            System.Drawing.Image imgplwmin = Properties.Resources.plw16;
            PushButtonData buttonDataplw = new PushButtonData(nameof(PLW), "Закреплятор\nУровни Наборы", typeof(PLW).Assembly.Location, typeof(PLW).FullName)
            {
                LargeImage = GetImageSource(imgplw),
                Image = GetImageSource(imgplwmin),
                ToolTip = "Закрепить оси, уровни и rvt-связи, переименовать отметки в уровнях, назначить рабочие наборы для связей, осей и уровней."
            };
            ContextualHelp plwhelp = new ContextualHelp(ContextualHelpType.Url,
                "https://portal.talan.group/knowledge/proektirovanie/zakreplyatorurovninabory/");
            buttonDataplw.SetContextualHelp(plwhelp);

            
            // - подкнопка "Настройки"

            System.Drawing.Image imgplwSettings = Properties.Resources.worksets32;
            System.Drawing.Image imgplwSettingsmin = Properties.Resources.worksets16;
            PushButtonData buttonDataplwSettings = new PushButtonData(nameof(PLWSettings), "Настройки", typeof(PLWSettings).Assembly.Location, typeof(PLWSettings).FullName)
            {
                LargeImage = GetImageSource(imgplwSettings),
                Image = GetImageSource(imgplwSettingsmin),
                ToolTip = "Настройки плагина Закреплятор Уровни Наборы."
            };
            buttonDataplwSettings.SetContextualHelp(plwhelp);

            
            // - подкнопка "Откреплятор"

            System.Drawing.Image imgunpinner = Properties.Resources.unpinner32;
            System.Drawing.Image imgunpinnermin = Properties.Resources.unpinner16;
            PushButtonData buttonDataunpinner = new PushButtonData(nameof(Unpinner), "Откреплятор", typeof(Unpinner).Assembly.Location, typeof(Unpinner).FullName)
            {
                LargeImage = GetImageSource(imgunpinner),
                Image = GetImageSource(imgunpinnermin),
                ToolTip = "Открепить оси, уровни и rvt-связи (на выбор)."
            };
            buttonDataunpinner.SetContextualHelp(plwhelp);

            // - основная кнопка

            SplitButtonData buttonDataplwgroup = new SplitButtonData("Закреплятор\nУровни Наборы", "Закрепить оси, уровни и rvt-связи, переименовать отметки в уровнях, назначить рабочие наборы для связей, осей и уровней.");
            SplitButton groupplw = panelUtils.AddItem(buttonDataplwgroup) as SplitButton;
            groupplw.AddPushButton(buttonDataplw);
            groupplw.AddPushButton(buttonDataplwSettings);
            groupplw.AddPushButton(buttonDataunpinner);
            groupplw.SetContextualHelp(plwhelp);
                        
            
            


            // сгруппированная кнопка "Выбор по ID"

            System.Drawing.Image imgidselection = Properties.Resources.idselection32;
            System.Drawing.Image imgidselectionmin = Properties.Resources.idselection16;
            PushButtonData buttonDataidselection = new PushButtonData(nameof(IdSelection), "Выбор по ID", typeof(IdSelection).Assembly.Location, typeof(IdSelection).FullName)
            {
                Image = GetImageSource(imgidselectionmin),
                ToolTip = "Выбрать и изолировать элементы по ID."
            };
            ContextualHelp idselectionhelp = new ContextualHelp(ContextualHelpType.Url,
                "https://portal.talan.group/knowledge/proektirovanie/rabotaskolliziyami/");
            buttonDataidselection.SetContextualHelp(idselectionhelp);

            // сгруппированная кнопка "Типофильтр"

            System.Drawing.Image imgfilter = Properties.Resources.typefilter32;
            System.Drawing.Image imgfiltermin = Properties.Resources.typefilter16;
            PushButtonData buttonDatafilter = new PushButtonData(nameof(TypeFilter), "Типофильтр", typeof(TypeFilter).Assembly.Location, typeof(TypeFilter).FullName)
            {
                Image = GetImageSource(imgfiltermin),
                ToolTip = "Фильтрация на виде по типам элементов, создание фильтров в проекте."
            };
            ContextualHelp filterhelp = new ContextualHelp(ContextualHelpType.Url,
                "https://portal.talan.group/knowledge/proektirovanie/tipofiltr/");
            buttonDatafilter.SetContextualHelp(filterhelp);

            // группа кнопок "Типофильтр", "Выбор по ID"

            panelUtils.AddStackedItems(buttonDatafilter, buttonDataidselection);

            // кнопка с выпадающим списком "Краска+"

            // подкнопка "Краска+"

            System.Drawing.Image imgpaint = Properties.Resources.paint32;
            System.Drawing.Image imgpaintmin = Properties.Resources.paint16;
            PushButtonData buttonDatapaint = new PushButtonData(nameof(Paint), "Краска+", typeof(Paint).Assembly.Location, typeof(Paint).FullName)
            {
                LargeImage = GetImageSource(imgpaint),
                Image = GetImageSource(imgpaintmin),
                ToolTip = "Копирование краски."
            };
            ContextualHelp painthelp = new ContextualHelp(ContextualHelpType.Url,
                "https://portal.talan.group/knowledge/proektirovanie/kraska/");
            buttonDatapaint.SetContextualHelp(painthelp);

            // подкнопка "Краска"

            System.Drawing.Image imgrevitpaint = Properties.Resources.revitpaint32;
            System.Drawing.Image imgrevitpaintmin = Properties.Resources.revitpaint16;
            PushButtonData buttonDatarevitpaint = new PushButtonData(nameof(revitpaint), "Краска", typeof(revitpaint).Assembly.Location, typeof(revitpaint).FullName)
            {
                LargeImage = GetImageSource(imgrevitpaint),
                Image = GetImageSource(imgrevitpaintmin),
                ToolTip = "Применение материала к грани элемента."
            };
            buttonDatarevitpaint.SetContextualHelp(painthelp);

            // подкнопка "Разделение грани"

            System.Drawing.Image imgrevitsplitface = Properties.Resources.revitsplitface32;
            System.Drawing.Image imgrevitsplitfacemin = Properties.Resources.revitsplitface16;
            PushButtonData buttonDatarevitsplitface = new PushButtonData(nameof(revitsplitface), "Разделение грани", typeof(revitsplitface).Assembly.Location, typeof(revitsplitface).FullName)
            {
                LargeImage = GetImageSource(imgrevitsplitface),
                Image = GetImageSource(imgrevitsplitfacemin),
                ToolTip = "Разделение грани элемента."
            };
            buttonDatarevitsplitface.SetContextualHelp(painthelp);

            // подкнопка "Материал?"

            System.Drawing.Image imgpaint2 = Properties.Resources.paint2_32;
            System.Drawing.Image imgpaint2min = Properties.Resources.paint2_16;
            PushButtonData buttonDatapaint2 = new PushButtonData(nameof(Paint2), "Материал?", typeof(Paint2).Assembly.Location, typeof(Paint2).FullName)
            {
                LargeImage = GetImageSource(imgpaint2),
                Image = GetImageSource(imgpaint2min),
                ToolTip = "Получить имя материала выбранной грани."
            };
            buttonDatapaint2.SetContextualHelp(painthelp);

            // подкнопка "Удалить краску"

            System.Drawing.Image imgrevitpaintdel = Properties.Resources.revitpaintdel32;
            System.Drawing.Image imgrevitpaintdelmin = Properties.Resources.revitpaintdel16;
            PushButtonData buttonDatarevitpaintdel = new PushButtonData(nameof(revitpaintdel), "Удалить краску", typeof(revitpaintdel).Assembly.Location, typeof(revitpaintdel).FullName)
            {
                LargeImage = GetImageSource(imgrevitpaintdel),
                Image = GetImageSource(imgrevitpaintdelmin),
                ToolTip = "Удалить краску с грани элемента."
            };
            buttonDatarevitpaintdel.SetContextualHelp(painthelp);

            // - основная кнопка

            SplitButtonData buttonDatapaintgroup = new SplitButtonData("Краска+", "Копирование краски.");
            SplitButton grouppaint = panelUtils.AddItem(buttonDatapaintgroup) as SplitButton;
            grouppaint.AddPushButton(buttonDatapaint);
            grouppaint.AddPushButton(buttonDatarevitpaint);
            grouppaint.AddPushButton(buttonDatarevitsplitface);
            grouppaint.AddPushButton(buttonDatapaint2);
            grouppaint.AddPushButton(buttonDatarevitpaintdel);
            grouppaint.SetContextualHelp(painthelp);

            // кнопка "Семейства"

            System.Drawing.Image imgfamilies = Properties.Resources.families32;
            System.Drawing.Image imgfamiliesmin = Properties.Resources.families16;
            PushButtonData buttonDatafamilies = new PushButtonData(nameof(LoadFamiliesFromServer), "Семейный", typeof(LoadFamiliesFromServer).Assembly.Location, typeof(LoadFamiliesFromServer).FullName)
            {
                LargeImage = GetImageSource(imgfamilies),
                Image = GetImageSource(imgfamiliesmin),
                ToolTip = "Пакетная вставка связей с помещением их в рабочие наборы."
            };
            ContextualHelp familieshelp = new ContextualHelp(ContextualHelpType.Url,
                "https://portal.talan.group/knowledge/proektirovanie/zayavkinasemeystva/");
            buttonDatafamilies.SetContextualHelp(familieshelp);
            panelUtils.AddItem(buttonDatafamilies);

            #endregion

            #region Панель "Помещения"

            // Панель "Помещения"

            RibbonPanel panelRooms = application.CreateRibbonPanel(tabName, "Помещения");
            _ARRibbonItems.Add(panelRooms);

            // кнопка с выпадающим списком "Помещения"

            // подкнопка "Номера помещений"

            System.Drawing.Image imgrooms = Properties.Resources.roomsnum32;
            System.Drawing.Image imgroomsmin = Properties.Resources.roomsnum16;
            PushButtonData buttonDatarooms = new PushButtonData(nameof(RoomsNum), "Номера помещений", typeof(RoomsNum).Assembly.Location, typeof(RoomsNum).FullName)
            {
                LargeImage = GetImageSource(imgrooms),
                Image = GetImageSource(imgroomsmin),
                ToolTip = "Пронумеровать помещения c последовательным выбором элементов."
            };
            ContextualHelp roomshelp = new ContextualHelp(ContextualHelpType.Url,
                "https://portal.talan.group/knowledge/proektirovanie/kladovye/");
            buttonDatarooms.SetContextualHelp(roomshelp);

            // подкнопка "Округлятор"

            System.Drawing.Image imgroomsround = Properties.Resources.roomsround32;
            System.Drawing.Image imgroomsroundmin = Properties.Resources.roomsround16;
            PushButtonData buttonDataroomsround = new PushButtonData(nameof(RoomsRound), "Округлятор", typeof(RoomsRound).Assembly.Location, typeof(RoomsRound).FullName)
            {
                LargeImage = GetImageSource(imgroomsround),
                Image = GetImageSource(imgroomsroundmin),
                ToolTip = "Округлить площади помещений."
            };
            ContextualHelp roomsroundhelp = new ContextualHelp(ContextualHelpType.Url,
                "https://portal.talan.group/knowledge/proektirovanie/pomeshcheniya/");
            buttonDataroomsround.SetContextualHelp(roomsroundhelp);

            // подкнопка "Нумератор квартир"

            System.Drawing.Image imgapartsnum = Properties.Resources.apartsnum32;
            System.Drawing.Image imgapartsnummin = Properties.Resources.apartsnum16;
            PushButtonData buttonDataapartsnum = new PushButtonData(nameof(ApartsNumAtLevel), "Нумератор квартир", typeof(ApartsNumAtLevel).Assembly.Location, typeof(ApartsNumAtLevel).FullName)
            {
                LargeImage = GetImageSource(imgapartsnum),
                Image = GetImageSource(imgapartsnummin),
                ToolTip = "Пронумеровать квартиры (номер на этаже - в ручном режиме, сквозные номера - автоматически)."
            };
            ContextualHelp apartsnumhelp = new ContextualHelp(ContextualHelpType.Url,
                "https://portal.talan.group/knowledge/proektirovanie/kvartirografiya/");
            buttonDataapartsnum.SetContextualHelp(apartsnumhelp);

            // подкнопка "Квартирография"

            System.Drawing.Image imgaparts = Properties.Resources.aparts32;
            System.Drawing.Image imgapartsmin = Properties.Resources.aparts16;
            PushButtonData buttonDataaparts = new PushButtonData(nameof(Aparts), "Квартирография", typeof(Aparts).Assembly.Location, typeof(Aparts).FullName)
            {
                LargeImage = GetImageSource(imgaparts),
                Image = GetImageSource(imgapartsmin),
                ToolTip = "Выполнить расчет квартирографии (с перерасчетом площадей или без него)."
            };
            ContextualHelp apartshelp = new ContextualHelp(ContextualHelpType.Url,
                "https://portal.talan.group/knowledge/proektirovanie/kvartirografiya/");
            buttonDataaparts.SetContextualHelp(apartshelp);

            // подкнопка "Офисография"

            System.Drawing.Image imgoffices = Properties.Resources.offices32;
            System.Drawing.Image imgofficesmin = Properties.Resources.offices16;
            PushButtonData buttonDataoffices = new PushButtonData(nameof(Offices), "Офисография", typeof(Offices).Assembly.Location, typeof(Offices).FullName)
            {
                LargeImage = GetImageSource(imgoffices),
                Image = GetImageSource(imgofficesmin),
                ToolTip = "Выполнить расчет офисографии (с перерасчетом площадей или без него)."
            };
            ContextualHelp officeshelp = new ContextualHelp(ContextualHelpType.Url,
                "https://portal.talan.group/knowledge/proektirovanie/ofisografiya/");
            buttonDataoffices.SetContextualHelp(officeshelp);

            // подкнопка "Удалить лишние"

            System.Drawing.Image imgfailedrooms = Properties.Resources.failedrooms32;
            System.Drawing.Image imgfailedroomsmin = Properties.Resources.failedrooms16;
            PushButtonData buttonDatafailedrooms = new PushButtonData(nameof(PurgeFailedRooms), "Удалить лишние", typeof(PurgeFailedRooms).Assembly.Location, typeof(PurgeFailedRooms).FullName)
            {
                LargeImage = GetImageSource(imgfailedrooms),
                Image = GetImageSource(imgfailedroomsmin),
                ToolTip = "Удалить лишние помещения (неразмещенные и избыточные)."
            };
            ContextualHelp failedroomshelp = new ContextualHelp(ContextualHelpType.Url,
                "https://portal.talan.group/knowledge/proektirovanie/pomeshcheniya/");
            buttonDatafailedrooms.SetContextualHelp(failedroomshelp);

            // подкнопка "Резервные копии"

            System.Drawing.Image imgroomsbackup = Properties.Resources.roomsbackup32;
            System.Drawing.Image imgroomsbackupmin = Properties.Resources.roomsbackup16;
            PushButtonData buttonDataroomsbackup = new PushButtonData(nameof(RoomsBackup), "Резервные копии", typeof(RoomsBackup).Assembly.Location, typeof(RoomsBackup).FullName)
            {
                LargeImage = GetImageSource(imgroomsbackup),
                Image = GetImageSource(imgroomsbackupmin),
                ToolTip = "Резервное копирование и восстановление значений площадей помещений."
            };
            ContextualHelp roomsbackuphelp = new ContextualHelp(ContextualHelpType.Url,
                "https://portal.talan.group/knowledge/proektirovanie/pomeshcheniyarezervnoekopirovanieivosstanovlenie/");
            buttonDataroomsbackup.SetContextualHelp(roomsbackuphelp);

            // подкнопка "Номера по ТЗ"

            PushButtonData buttonDataroomsTNumber = new PushButtonData(nameof(RoomsTNumber), "Номера по ТЗ", typeof(RoomsTNumber).Assembly.Location, typeof(RoomsTNumber).FullName)
            {
                LargeImage = GetImageSource(imgrooms),
                Image = GetImageSource(imgroomsmin),
                ToolTip = "Дозаполнить номера по ТЗ у продаваемых помещений."
            };
            buttonDataroomsbackup.SetContextualHelp(roomsroundhelp);

            // - основная кнопка

            System.Drawing.Image imgroom = Properties.Resources.rooms32;
            System.Drawing.Image imgroommin = Properties.Resources.rooms16;
            PulldownButtonData buttonDataapartsgroup = new PulldownButtonData("Помещения", "Помещения")
            {
                LargeImage = GetImageSource(imgroom),
                Image = GetImageSource(imgroommin),
                ToolTip = "Пакет функций для работы с помещениями."
            };
            ContextualHelp apartsgrouphelp = new ContextualHelp(ContextualHelpType.Url,
                "https://portal.talan.group/knowledge/proektirovanie/pomeshcheniya/");
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

            #endregion

            #region Панель "Отделка"

            // Панель "Отделка"

            RibbonPanel panelFinishing = application.CreateRibbonPanel(tabName, "Отделка");
            _ARRibbonItems.Add(panelFinishing);

            // кнопка "Генератор полов"

            System.Drawing.Image imgfloors = Properties.Resources.floors32;
            System.Drawing.Image imgfloorsmin = Properties.Resources.floors16;
            PushButtonData buttonDatafloors = new PushButtonData(nameof(Floors), "Генератор\nполов", typeof(Floors).Assembly.Location, typeof(Floors).FullName)
            {
                LargeImage = GetImageSource(imgfloors),
                Image = GetImageSource(imgfloorsmin),
                ToolTip = "Создать полы в помещениях."
            };
            ContextualHelp floorshelp = new ContextualHelp(ContextualHelpType.Url,
                "https://portal.talan.group/knowledge/proektirovanie/poly/");
            buttonDatafloors.SetContextualHelp(floorshelp);
            panelFinishing.AddItem(buttonDatafloors);

            // сгруппированная кнопка "Ведомость полов"

            System.Drawing.Image imgfloorspec = Properties.Resources.floorimages32;
            System.Drawing.Image imgfloorspecmin = Properties.Resources.floorimages16;
            PushButtonData buttonDatafloorspec = new PushButtonData(nameof(FloorImages), "Ведомость полов", typeof(FloorImages).Assembly.Location, typeof(FloorImages).FullName)
            {
                Image = GetImageSource(imgfloorspecmin),
                ToolTip = "Сформировать изображения для ведомости полов."
            };
            buttonDatafloorspec.SetContextualHelp(mainhelp);

            // сгруппированная кнопка "Ведомость отделки"

            System.Drawing.Image imgfinishing = Properties.Resources.finishing32;
            System.Drawing.Image imgfinishingmin = Properties.Resources.finishing16;
            PushButtonData buttonDatafinishing = new PushButtonData(nameof(Finishing), "Ведомость отделки", typeof(Finishing).Assembly.Location, typeof(Finishing).FullName)
            {
                Image = GetImageSource(imgfinishingmin),
                ToolTip = "Заполнение параметров для ведомости отделки у стен, полов, потолков."
            };
            ContextualHelp finishinghelp = new ContextualHelp(ContextualHelpType.Url,
                "https://portal.talan.group/knowledge/proektirovanie/vedomostotdelkipomeshcheniy/");
            buttonDatafinishing.SetContextualHelp(finishinghelp);

            // группа кнопок "Ведомость полов", "Ведомость отделки"

            panelFinishing.AddStackedItems(buttonDatafloorspec, buttonDatafinishing);

            #endregion

            #region Панель "Утилиты АР"

            // Панель "Утилиты АР"

            RibbonPanel panelUtilsAR = application.CreateRibbonPanel(tabName, "Утилиты АР");
            _ARRibbonItems.Add(panelUtilsAR);

            // сгруппированная кнопка "Антизеркало"
            System.Drawing.Image imgmirror = Properties.Resources.mirror32;
            System.Drawing.Image imgmirrormin = Properties.Resources.mirror16;
            PushButtonData buttonDatamirror = new PushButtonData(nameof(Mirror), "Антизеркало", typeof(Mirror).Assembly.Location, typeof(Mirror).FullName)
            {
                LargeImage = GetImageSource(imgmirror),
                Image = GetImageSource(imgmirrormin),
                ToolTip = "Выделить отзеркаленные окна и двери, пометить такие элементы через параметр Марка."
            };
            ContextualHelp mirrorhelp = new ContextualHelp(ContextualHelpType.Url,
                "https://portal.talan.group/knowledge/proektirovanie/okna/");
            buttonDatamirror.SetContextualHelp(mirrorhelp);

            // сгруппированная кнопка "Проемщик"

            System.Drawing.Image imgCopyWindows = Properties.Resources.CopyWindows32;
            System.Drawing.Image imgCopyWindowsmin = Properties.Resources.CopyWindows16;
            PushButtonData buttonDataCopyWindows = new PushButtonData(nameof(CopyWindows), "Проемщик", typeof(CopyWindows).Assembly.Location, typeof(CopyWindows).FullName)
            {
                Image = GetImageSource(imgCopyWindowsmin),
                ToolTip = "Создать обобщенные модели из окон/дверей связанной модели (_АР)",
                LongDescription = "Находит в связанных моделях с _АР все окна и двери, позволяет выбрать нужные и копирует их как семейства pmN.Отверстие Стена.ПОФ с параметрами."
            };
            ContextualHelp CopyWindowsHelp = new ContextualHelp(ContextualHelpType.Url,
                "https://portal.talan.group/knowledge/proektirovanie/");
            buttonDataCopyWindows.SetContextualHelp(CopyWindowsHelp);

            // сгруппированная кнопка "Эт.Номер"

            System.Drawing.Image imglevelnumber = Properties.Resources.levelnumber32;
            System.Drawing.Image imglevelnumbermin = Properties.Resources.levelnumber16;
            PushButtonData buttonDatalevelnumber = new PushButtonData(nameof(LevelNumber), "Эт.Номер", typeof(LevelNumber).Assembly.Location, typeof(LevelNumber).FullName)
            {
                Image = GetImageSource(imglevelnumbermin),
                ToolTip = "Заполнить Эт.Номер у элементов модели (с выбором категорий)."
            };
            ContextualHelp levelnumberhelp = new ContextualHelp(ContextualHelpType.Url,
                "https://portal.talan.group/knowledge/proektirovanie/specificationsbylevel/");
            buttonDatalevelnumber.SetContextualHelp(levelnumberhelp);

            // группа кнопок 

            panelUtilsAR.AddStackedItems(buttonDatalevelnumber, buttonDatamirror, buttonDataCopyWindows);

            // кнопка "АМ ПСО"

            System.Drawing.Image imgAM = Properties.Resources.AM32;
            System.Drawing.Image imgAMmin = Properties.Resources.AM16;
            PushButtonData buttonDataAM = new PushButtonData(nameof(CreateApartmentViewsCommand), "АМ\nПСО", typeof(CreateApartmentViewsCommand).Assembly.Location, typeof(CreateApartmentViewsCommand).FullName)
            {
                LargeImage = GetImageSource(imgAM),
                Image = GetImageSource(imgAMmin),
                ToolTip = "Сформировать виды квартир для АМ ПСО."
            };
            ContextualHelp AMhelp = new ContextualHelp(ContextualHelpType.Url,
                "https://portal.talan.group/knowledge/proektirovanie/");
            buttonDataAM.SetContextualHelp(AMhelp);

            panelUtilsAR.AddItem(buttonDataAM);

            #endregion

            #region Панель "Парковки"

            // Панель "Парковки"

            RibbonPanel panelParking = application.CreateRibbonPanel(tabName, "Парковки");
            _ARRibbonItems.Add(panelParking);

            // кнопка "Парковки"

            System.Drawing.Image imgpark = Properties.Resources.park32;
            System.Drawing.Image imgparkmin = Properties.Resources.park16;
            PushButtonData buttonDatapark = new PushButtonData(nameof(Parking), "Парковки", typeof(Parking).Assembly.Location, typeof(Parking).FullName)
            {
                LargeImage = GetImageSource(imgpark),
                Image = GetImageSource(imgparkmin),
                ToolTip = "Пакет функций для работы с парковками."
            };
            ContextualHelp parkhelp = new ContextualHelp(ContextualHelpType.Url,
                "https://portal.talan.group/knowledge/proektirovanie/parking/");
            buttonDatapark.SetContextualHelp(parkhelp);
            panelParking.AddItem(buttonDatapark);

            // Панель "Перемычки"

            RibbonPanel panelBeams = application.CreateRibbonPanel(tabName, "Перемычки");
            _ARRibbonItems.Add(panelBeams);

            // кнопка "Перемычки"

            System.Drawing.Image imgbeamscut = Properties.Resources.beamscut32;
            System.Drawing.Image imgbeamscutmin = Properties.Resources.beamscut16;
            PushButtonData buttonDatabeamscut = new PushButtonData(nameof(Beams), "Перемычки", typeof(Beams).Assembly.Location, typeof(Beams).FullName)
            {
                LargeImage = GetImageSource(imgbeamscut),
                Image = GetImageSource(imgbeamscutmin),
                ToolTip = "Вырезать объем бетонных перемычек из стен, сформировать эскизы ПР."
            };
            ContextualHelp beamshelp = new ContextualHelp(ContextualHelpType.Url,
                "https://portal.talan.group/knowledge/proektirovanie/vedomostperemychek/");
            buttonDatabeamscut.SetContextualHelp(beamshelp);
            panelBeams.AddItem(buttonDatabeamscut);

            #endregion

            #region Панель "Сваи"

            // Панель "Сваи"

            RibbonPanel panelPiles = application.CreateRibbonPanel(tabName, "Сваи");
            _STRibbonItems.Add(panelPiles);

            // кнопка "Сваи"

            System.Drawing.Image imgpiles = Properties.Resources.foundcut32;
            System.Drawing.Image imgpilesmin = Properties.Resources.foundcut16;
            PushButtonData buttonDatapiles = new PushButtonData(nameof(Found), "Сваи", typeof(Found).Assembly.Location, typeof(Found).FullName)
            {
                LargeImage = GetImageSource(imgpiles),
                Image = GetImageSource(imgpilesmin),
                ToolTip = "Пакет функций по работе со сваями."
            };
            ContextualHelp pileshelp = new ContextualHelp(ContextualHelpType.Url,
                "https://portal.talan.group/knowledge/proektirovanie/svai_xmqe/");
            buttonDatapiles.SetContextualHelp(pileshelp);
            panelPiles.AddItem(buttonDatapiles);

            #endregion

            #region Панель "Утилиты КЖ"

            // Панель "Утилиты КЖ"

            RibbonPanel panelUtilsST = application.CreateRibbonPanel(tabName, "Утилиты КЖ");
            _STRibbonItems.Add(panelUtilsST);

            // кнопка "Ускорить файл"

            System.Drawing.Image imgfixstructurefile = Properties.Resources.fixstructurefile32;
            System.Drawing.Image imgfixstructurefilemin = Properties.Resources.fixstructurefile16;
            PushButtonData buttonDatafixstructurefile = new PushButtonData(nameof(Fixstructurefile), "Ускорить\nфайл", typeof(Fixstructurefile).Assembly.Location, typeof(Fixstructurefile).FullName)
            {
                LargeImage = GetImageSource(imgfixstructurefile),
                Image = GetImageSource(imgfixstructurefilemin),
                ToolTip = "Ускорить работу модели КЖ путем манипуляций с параметрами несущей арматуры."
            };
            ContextualHelp fixstructurefilehelp = new ContextualHelp(ContextualHelpType.Url,
                "https://portal.talan.group/knowledge/proektirovanie/uskorenierabotyfaylovmodeli_posu/");
            buttonDatafixstructurefile.SetContextualHelp(fixstructurefilehelp);
            panelUtilsST.AddItem(buttonDatafixstructurefile);

            // сгруппированная кнопка "Эскизы деталей"

            System.Drawing.Image imgrebarimages = Properties.Resources.rebarimages32;
            System.Drawing.Image imgrebarimagesmin = Properties.Resources.rebarimages16;
            PushButtonData buttonDatarebarimages = new PushButtonData(nameof(RebarImages), "Эскизы деталей", typeof(RebarImages).Assembly.Location, typeof(RebarImages).FullName)
            {
                Image = GetImageSource(imgrebarimagesmin),
                ToolTip = "Заполнить параметр A_Арм Эскиз формы у системной арматуры для ведомости деталей."
            };
            ContextualHelp rebarimageshelp = new ContextualHelp(ContextualHelpType.Url,
                "https://portal.talan.group/knowledge/proektirovanie/vedomostdetaley/");
            buttonDatarebarimages.SetContextualHelp(rebarimageshelp);

            // сгруппированная кнопка "ВРС подчистить"

            System.Drawing.Image imgsteelschedule = Properties.Resources.steelschedule32;
            System.Drawing.Image imgsteelschedulemin = Properties.Resources.steelschedule16;
            PushButtonData buttonDatasteelschedule = new PushButtonData(nameof(SteelSchedule), "ВРС подчистить", typeof(SteelSchedule).Assembly.Location, typeof(SteelSchedule).FullName)
            {
                Image = GetImageSource(imgsteelschedulemin),
                ToolTip = "Подчистить все ведомости расхода стали в проекте (скрыть столбцы с нулевыми значениями)."
            };
            ContextualHelp steelschedulehelp = new ContextualHelp(ContextualHelpType.Url,
                "https://portal.talan.group/knowledge/proektirovanie/vedomostraskhodastali/");
            buttonDatasteelschedule.SetContextualHelp(steelschedulehelp);

            // сгруппированная кнопка "Группировка"

            System.Drawing.Image imgschemespec = Properties.Resources.grouping32;
            System.Drawing.Image imgschemespecmin = Properties.Resources.grouping16;
            PushButtonData buttonDataschemespec = new PushButtonData(nameof(Schemespec), "Группировка", typeof(Schemespec).Assembly.Location, typeof(Schemespec).FullName)
            {
                Image = GetImageSource(imgschemespecmin),
                ToolTip = "Заполнить параметр A_Группирование для сортировки спецификаций)."
            };
            ContextualHelp schemespechelp = new ContextualHelp(ContextualHelpType.Url,
                "https://portal.talan.group/knowledge/proektirovanie/skhemaraspolozheniyakonstruktsiy/");
            buttonDataschemespec.SetContextualHelp(schemespechelp);

            // группа кнопок "Эскизы деталей", "ВРС подчистить", "Группировка"

            panelUtilsST.AddStackedItems(buttonDatarebarimages, buttonDatasteelschedule, buttonDataschemespec);

            //RebarNoMark
            // кнопка "Арматура без марки"

            System.Drawing.Image imgRebarNoMark = Properties.Resources.rebarnomark32;
            System.Drawing.Image imgRebarNoMarkmin = Properties.Resources.rebarnomark16;
            PushButtonData buttonDataRebarNoMark = new PushButtonData(nameof(RebarNoMark), "Арматура\nбез марки", typeof(RebarNoMark).Assembly.Location, typeof(RebarNoMark).FullName)
            {
                LargeImage = GetImageSource(imgRebarNoMark),
                Image = GetImageSource(imgRebarNoMarkmin),
                ToolTip = "Изолирует на открытом 3D-виде несущую арматуру с незаполненным параметром A_Марка конструкции."
            };
            ContextualHelp RebarNoMarkhelp = new ContextualHelp(ContextualHelpType.Url,
                "https://portal.talan.group/knowledge/proektirovanie/plaginyiskriptynovatsiya/");
            buttonDataRebarNoMark.SetContextualHelp(RebarNoMarkhelp);
            panelUtilsST.AddItem(buttonDataRebarNoMark);

            #endregion

            #region Панель "СО"

            // Панель "СО"

            RibbonPanel panelMEPSpec = application.CreateRibbonPanel(tabName, "СО");
            _MEPRibbonItems.Add(panelMEPSpec);

            // кнопка "Сводная спека"

            System.Drawing.Image imgadskg = Properties.Resources.adskg32;
            System.Drawing.Image imgadskgmin = Properties.Resources.adskg16;
            PushButtonData buttonDataadskg = new PushButtonData(nameof(MEPSpec), "Сводная\nспека", typeof(MEPSpec).Assembly.Location, typeof(MEPSpec).FullName)
            {
                LargeImage = GetImageSource(imgadskg),
                Image = GetImageSource(imgadskgmin),
                ToolTip = "Заполнить параметры у элементов ВК ОВ / ЭЛ / СС ПС для формирования сводной спецификации."
            };
            ContextualHelp adskghelp = new ContextualHelp(ContextualHelpType.Url,
                "https://portal.talan.group/knowledge/proektirovanie/MEPspec/");
            buttonDataadskg.SetContextualHelp(adskghelp);
            panelMEPSpec.AddItem(buttonDataadskg);

            #endregion

            #region Панель "Вентиляция"

            // Панель "Вентиляция"

            RibbonPanel panelVent = application.CreateRibbonPanel(tabName, "Вентиляция");
            _MEPRibbonItems.Add(panelVent);

            // сгруппированная кнопка "Стенки Классы"

            System.Drawing.Image imgadskstenki = Properties.Resources.adskstenki32;
            System.Drawing.Image imgadskstenkimin = Properties.Resources.adskstenki16;
            PushButtonData buttonDataadskstenki = new PushButtonData(nameof(DuctThicknessClasses), "Стенки классы", typeof(DuctThicknessClasses).Assembly.Location, typeof(DuctThicknessClasses).FullName)
            {
                Image = GetImageSource(imgadskstenkimin),
                ToolTip = "Заполнить толщины стенок и класс герметичности воздуховодов."
            };
            ContextualHelp adskstenkihelp = new ContextualHelp(ContextualHelpType.Url,
                "https://portal.talan.group/knowledge/proektirovanie/MEPductthickness/");
            buttonDataadskstenki.SetContextualHelp(adskstenkihelp);


            // сгруппированная кнопка "Схемы ОВ2"

            System.Drawing.Image imgduct3d = Properties.Resources.vent32;
            System.Drawing.Image imgduct3dmin = Properties.Resources.vent16;
            PushButtonData buttonDataduct3d = new PushButtonData(nameof(Duct3D), "Схемы ОВ2", typeof(Duct3D).Assembly.Location, typeof(Duct3D).FullName)
            {
                Image = GetImageSource(imgduct3dmin),
                ToolTip = "Создать/заменить схемы систем вентиляции."
            };
            ContextualHelp duct3dhelp = new ContextualHelp(ContextualHelpType.Url,
                "https://portal.talan.group/knowledge/proektirovanie/MEPviews/");
            buttonDataduct3d.SetContextualHelp(duct3dhelp);

            // группа кнопок "Стенки Классы", "Схемы ОВ2"

            panelVent.AddStackedItems(buttonDataadskstenki, buttonDataduct3d);

            #endregion

            #region Панель "Электрика"

            // Панель "Электрика"

            RibbonPanel panelElectrical = application.CreateRibbonPanel(tabName, "Электрика");
            _MEPRibbonItems.Add(panelElectrical);

            // подкнопка "ЭЛ Отметки размещения"

            System.Drawing.Image imgefl = Properties.Resources.efl32;
            System.Drawing.Image imgeflmin = Properties.Resources.efl16;
            PushButtonData buttonDataefl = new PushButtonData(nameof(ElElevValues), "ЭЛ Отметки", typeof(ElElevValues).Assembly.Location, typeof(ElElevValues).FullName)
            {
                Image = GetImageSource(imgeflmin),
                ToolTip = "Заполнить параметры N_ЭЛ.Высота стяжки и N_ЭЛ.Отметка потолка у выключателей, осветительных и электрических приборов, электрооборудования."
            };
            ContextualHelp eflhelp = new ContextualHelp(ContextualHelpType.Url,
                "https://portal.talan.group/knowledge/proektirovanie/plaginyiskriptynovatsiya/");
            buttonDataefl.SetContextualHelp(eflhelp);

            // подкнопка "ЭЛ Отметки размещения. Настройки"

            PushButtonData buttonDataeflsettings = new PushButtonData(nameof(ElElevValuesSettings), "Отметки.Настройки", typeof(ElElevValuesSettings).Assembly.Location, typeof(ElElevValuesSettings).FullName)
            {
                Image = GetImageSource(imgeflmin),
                ToolTip = "Настройки плагина ЭЛ Отметки."
            };
            buttonDataeflsettings.SetContextualHelp(eflhelp);

            // подкнопка "Лотки"

            System.Drawing.Image imgcabletrays = Properties.Resources.cabletrays32;
            System.Drawing.Image imgcabletraysmin = Properties.Resources.cabletrays16;
            PushButtonData buttonDatacabletrays = new PushButtonData(nameof(CableTrays), "Лотки", typeof(CableTrays).Assembly.Location, typeof(CableTrays).FullName)
            {
                LargeImage = GetImageSource(imgcabletrays),
                Image = GetImageSource(imgcabletraysmin),
                ToolTip = "Крышки, перегородки для кабельных лотков, помещение лотков и их элементов в рабочий набор."
            };
            ContextualHelp cabletrayshelp = new ContextualHelp(ContextualHelpType.Url,
                "https://portal.talan.group/knowledge/proektirovanie/lotki/");
            buttonDatacabletrays.SetContextualHelp(cabletrayshelp);

            // подкнопка "Лотки.Настройки"

            PushButtonData buttonDatacabletrayssettings = new PushButtonData(nameof(CableTraysSettings), "Лотки.Настройки", typeof(CableTraysSettings).Assembly.Location, typeof(CableTraysSettings).FullName)
            {
                LargeImage = GetImageSource(imgcabletrays),
                Image = GetImageSource(imgcabletraysmin),
                ToolTip = "Настройки плагина Лотки."
            };
            buttonDatacabletrayssettings.SetContextualHelp(cabletrayshelp);

            // - основная кнопка "Лотки"

            SplitButtonData buttonDatacabletraysgroup = new SplitButtonData("Лотки", "Крышки, перегородки для кабельных лотков, помещение лотков и их элементов в рабочий набор.");
            SplitButton groupcabletrays = panelElectrical.AddItem(buttonDatacabletraysgroup) as SplitButton;
            groupcabletrays.AddPushButton(buttonDatacabletrays);
            groupcabletrays.AddPushButton(buttonDatacabletrayssettings);

            // подкнопка "Синхронизатор"

            System.Drawing.Image imgElSystemSync = Properties.Resources.elsync32;
            System.Drawing.Image imgElSystemSyncmin = Properties.Resources.elsync16;
            PushButtonData buttonDataElSystemSync = new PushButtonData(nameof(ElSystemSync), "Синхронизатор", typeof(ElSystemSync).Assembly.Location, typeof(ElSystemSync).FullName)
            {
                Image = GetImageSource(imgElSystemSyncmin),
                ToolTip = "Запись данных из цепей связанного файла в параметры автоматического выключателя."
            };
            ContextualHelp ElSystemSynchelp = new ContextualHelp(ContextualHelpType.Url,
                "https://portal.talan.group/knowledge/proektirovanie/");
            buttonDataElSystemSync.SetContextualHelp(ElSystemSynchelp);
                        
            // подкнопка "Способы прокладки"

            System.Drawing.Image imgcableways = Properties.Resources.cableways32;
            System.Drawing.Image imgcablewaysmin = Properties.Resources.cableways16;
            PushButtonData buttonDatacableways = new PushButtonData(nameof(CableWays), "Способы прокладки", typeof(CableWays).Assembly.Location, typeof(CableWays).FullName)
            {
                Image = GetImageSource(imgcablewaysmin),
                ToolTip = "Запись значений в параметры автоматического выключателя."
            };
            ContextualHelp cablewayshelp = new ContextualHelp(ContextualHelpType.Url,
                "https://portal.talan.group/knowledge/proektirovanie/");
            buttonDatacableways.SetContextualHelp(cablewayshelp);

            // подкнопка "Прокладка.Настройки"

            PushButtonData buttonDatacablewayssettings = new PushButtonData(nameof(CableWaysSettings), "Прокладка.Настройки", typeof(CableWaysSettings).Assembly.Location, typeof(CableWaysSettings).FullName)
            {
                Image = GetImageSource(imgcablewaysmin),
                ToolTip = "Настройки плагина Способы прокладки."
            };
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

            System.Drawing.Image imgss = Properties.Resources.ssNumberer32;
            System.Drawing.Image imgssmin = Properties.Resources.ssNumberer16;
            PushButtonData buttonDatass = new PushButtonData(nameof(SSNumberer), "Адресатор", typeof(SSNumberer).Assembly.Location, typeof(SSNumberer).FullName)
            {
                Image = GetImageSource(imgssmin),
                ToolTip = "Пакет функций по адресации устройств СС ПС."
            };
            ContextualHelp sshelp = new ContextualHelp(ContextualHelpType.Url,
                "https://portal.talan.group/knowledge/proektirovanie/");
            buttonDatass.SetContextualHelp(sshelp);


            // сгруппированная кнопка "FamilyAToFamilyB"
            System.Drawing.Image imgFamilyAToFamilyB = Properties.Resources.pikachu32;
            System.Drawing.Image imgFamilyAToFamilyBmin = Properties.Resources.pikachu16;
            PushButtonData buttonDataFamilyAToFamilyB = new PushButtonData("FamilyAToFamilyB", "Расстановщик\nСС ПС", typeof(PikachuCommand).Assembly.Location, typeof(PikachuCommand).FullName)
            {
                LargeImage = GetImageSource(imgFamilyAToFamilyB),
                Image = GetImageSource(imgFamilyAToFamilyBmin),
                ToolTip = "Универсальное размещение элементов рядом с элементами из связанных файлов",
                LongDescription = "Размещает элементы текущего файла рядом с элементами из связанных файлов\n\nГод напряженный - работаем эффективно!"
            };
            ContextualHelp buttonDataFamilyAToFamilyBhelp = new ContextualHelp(ContextualHelpType.Url,
                "https://portal.talan.group/knowledge/proektirovanie/");
            buttonDataFamilyAToFamilyB.SetContextualHelp(buttonDataFamilyAToFamilyBhelp);

            // группа

            panelSS.AddStackedItems(buttonDatass, buttonDataFamilyAToFamilyB);

            /*
            // кнопка "IntersectionCheck"
            System.Drawing.Image imgIntersectionCheck = Properties.Resources.pikachu2_32;
            System.Drawing.Image imgIntersectionCheckmin = Properties.Resources.pikachu2_16;
            PushButtonData buttonDataIntersectionCheck = new PushButtonData("IntersectionCheck", "Проверка\nпересечений", typeof(???).Assembly.Location, typeof(IntersectionCheckCommand).FullName)
            {
                LargeImage = GetImageSource(imgIntersectionCheck),
                Image = GetImageSource(imgIntersectionCheckmin),
                ToolTip = "Проверка пересечений между элементами текущего и связанных файлов с навигацией в 3D",
                LongDescription = "Показывает пересечения элементов текущего файла со связанными файлами\n\nГод напряженный - ищем и устраняем коллизии!"
            };
            ContextualHelp buttonDataIntersectionCheckhelp = new ContextualHelp(ContextualHelpType.Url,
                "https://portal.talan.group/knowledge/proektirovanie/");
            buttonDataIntersectionCheck.SetContextualHelp(buttonDataIntersectionCheckhelp);
            panel9p.AddItem(buttonDataIntersectionCheck);
            */
            #endregion

            #region Панель "Задания"

            // Панель "Задания"

            RibbonPanel panelTasks = application.CreateRibbonPanel(tabName, "Задания");

            // кнопка "Выдать задание"

            System.Drawing.Image imgtasksend = Properties.Resources.tasksend32;
            System.Drawing.Image imgtasksendmin = Properties.Resources.tasksend16;
            PushButtonData buttonDatatasksend = new PushButtonData(nameof(TaskSend), "Отправить\nзадание", typeof(TaskSend).Assembly.Location, typeof(TaskSend).FullName)
            {
                LargeImage = GetImageSource(imgtasksend),
                Image = GetImageSource(imgtasksendmin),
                ToolTip = "Выдать/перевыдать задание в систему выдачи заданий."
            };
            ContextualHelp gettaskhelp = new ContextualHelp(ContextualHelpType.Url,
                "https://portal.talan.group/knowledge/proektirovanie/samostoyatelnoemodelirovanieotverstiy/");
            buttonDatatasksend.SetContextualHelp(gettaskhelp);
            panelTasks.AddItem(buttonDatatasksend);

            // кнопка "Задания от ИОС"

            System.Drawing.Image imggettask = Properties.Resources.gettask32;
            System.Drawing.Image imggettaskmin = Properties.Resources.gettask16;
            PushButtonData buttonDatagettask = new PushButtonData(nameof(TasksMenu), "Задания\nот ИОС", typeof(TasksMenu).Assembly.Location, typeof(TasksMenu).FullName)
            {
                LargeImage = GetImageSource(imggettask),
                Image = GetImageSource(imggettaskmin),
                ToolTip = "Проверить статусы выданных заданий, внедрить/обновить задание."
            };
            buttonDatagettask.SetContextualHelp(gettaskhelp);
            panelTasks.AddItem(buttonDatagettask);

            // группа кнопок "Автонумерация", "Найти по номеру", "Проверка отверстий"

            // сгруппированная кнопка "Автонумерация"

            System.Drawing.Image imgtaskauto = Properties.Resources.taskautomark32;
            System.Drawing.Image imgtaskautomin = Properties.Resources.taskautomark16;
            PushButtonData buttonDatataskauto = new PushButtonData(nameof(TasksAutoMark), "Автонумерация", typeof(TasksAutoMark).Assembly.Location, typeof(TasksAutoMark).FullName)
            {
                Image = GetImageSource(imgtaskautomin),
                ToolTip = "Пронумеровать элементы заданий в выбранной группе (в модели Заданий)."
            };
            ContextualHelp taskautohelp = new ContextualHelp(ContextualHelpType.Url,
                "https://portal.talan.group/knowledge/proektirovanie/MEPtasks/");
            buttonDatataskauto.SetContextualHelp(taskautohelp);

            // сгруппированная кнопка "Найти по номеру"

            System.Drawing.Image imggettaskelems = Properties.Resources.idselectionTasks32;
            System.Drawing.Image imggettaskelemsmin = Properties.Resources.idselectionTasks16;
            PushButtonData buttonDatagettaskelems = new PushButtonData(nameof(IdSelectionTasks), "Найти элементы", typeof(IdSelectionTasks).Assembly.Location, typeof(IdSelectionTasks).FullName)
            {
                Image = GetImageSource(imggettaskelemsmin),
                ToolTip = "Найти отверстия или другие компоненты заданий по Маркам (позициям)."
            };
            buttonDatagettaskelems.SetContextualHelp(taskautohelp);

            // сгруппированная кнопка "Проверка отверстий"

            System.Drawing.Image imgholescheckdynamo = Properties.Resources.dynpl32;
            System.Drawing.Image imgholescheckdynamomin = Properties.Resources.dynpl16;
            PushButtonData buttonDataholescheckdynamo = new PushButtonData(nameof(HolesCheckDynamo), "Проверка\nотверстий", typeof(HolesCheckDynamo).Assembly.Location, typeof(HolesCheckDynamo).FullName)
            {
                Image = GetImageSource(imgholescheckdynamomin),
                ToolTip = "Запустить скрипт Чек-лист.Отверстия (Dynamo)."
            };
            buttonDataholescheckdynamo.SetContextualHelp(taskautohelp);

            panelTasks.AddStackedItems(buttonDatataskauto, buttonDatagettaskelems, buttonDataholescheckdynamo);

            // кнопка "Копировать отверстия"

            System.Drawing.Image imgcopyholes = Properties.Resources.copyholes32;
            System.Drawing.Image imgcopyholesmin = Properties.Resources.copyholes16;
            PushButtonData buttonDatacopyholes = new PushButtonData(nameof(CopyHolesCommand), "Копировать\nотверстия", typeof(CopyHolesCommand).Assembly.Location, typeof(CopyHolesCommand).FullName)
            {
                LargeImage = GetImageSource(imgcopyholes),
                Image = GetImageSource(imgcopyholesmin),
                ToolTip = "Скопировать отверстия выбранной группы по нужным уровням либо обновить их на уровнях."
            };
            ContextualHelp holeshelp = new ContextualHelp(ContextualHelpType.Url,
                "https://portal.talan.group/knowledge/proektirovanie/samostoyatelnoemodelirovanieotverstiy/");
            buttonDatacopyholes.SetContextualHelp(holeshelp);
            panelTasks.AddItem(buttonDatacopyholes);

            // кнопка "Отметки Вырезание"

            System.Drawing.Image imgholes = Properties.Resources.holes32;
            System.Drawing.Image imgholesmin = Properties.Resources.holes16;
            PushButtonData buttonDataholes = new PushButtonData(nameof(Holes), "Отметки\nВырезание", typeof(Holes).Assembly.Location, typeof(Holes).FullName)
            {
                LargeImage = GetImageSource(imgholes),
                Image = GetImageSource(imgholesmin),
                ToolTip = "Вырезать отверстия из стен и плит, заполнить отметки отверстий."
            };
            buttonDataholes.SetContextualHelp(holeshelp);
            panelTasks.AddItem(buttonDataholes);

            
            #endregion

            #region Панели "BIM"

            // Панель "BIM Общие"

            RibbonPanel panel10 = application.CreateRibbonPanel(tabName, "BIM Общие");
            _BIMRibbonItems.Add(panel10);

            // кнопка "BIM Экспорт"

            System.Drawing.Image imgnwc = Properties.Resources.nwc32;
            System.Drawing.Image imgnwcmin = Properties.Resources.nwc16;
            PushButtonData buttonDatabim = new PushButtonData(nameof(BimExport), "BIM\nЭкспорт", typeof(BimExport).Assembly.Location, typeof(BimExport).FullName)
            {
                LargeImage = GetImageSource(imgnwc),
                Image = GetImageSource(imgnwcmin),
                ToolTip = "Пакетный экспорт NWC, RVT (с очисткой)."
            };
            ContextualHelp bimhelp = new ContextualHelp(ContextualHelpType.Url,
            "https://portal.talan.group/knowledge/proektirovanie/eksportmodeleyvnavisworks/");
            buttonDatabim.SetContextualHelp(bimhelp);
            panel10.AddItem(buttonDatabim);

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
            
            //группа

            panel13.AddStackedItems(buttonDataInsulationHosts, buttonDataTParsOVVK);

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

            
            return Result.Succeeded;
        }
        public Result OnShutdown(UIControlledApplication application)
        {
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
            #endregion
            #region События отписка
            application.ControlledApplication.DocumentOpening -= OnDocumentOpening;
            application.ControlledApplication.DocumentOpened -= OnDocumentOpened;
            application.ControlledApplication.DocumentSynchronizingWithCentral -= OnSyncCentralStart;
            application.ControlledApplication.DocumentSynchronizedWithCentral -= OnSyncCentralEnd;
            application.ControlledApplication.DocumentClosing -= OnDocumentClosing;
            application.Idling -= OnIdling;
            application.ViewActivated -= OnViewActivated;
            application.DialogBoxShowing -= a_DialogBoxShowing;
            #endregion
            return Result.Succeeded;
        }
        #region Обработчики событий

        private void OnDocumentCreated(object sender, Autodesk.Revit.DB.Events.DocumentCreatedEventArgs e)
        {
            LoadSettings();
            if(_config.LicenseType=="corp"&&_config.CorpName=="ООО ПМ Новация") //в перспективе - запускать для любой корп конфигурации (считывая с сайта)
            {
                //Проверка имени пользователя
                Application revitApp = sender as Application;
                UIApplication uiApp = new UIApplication(e.Document.Application);
                string userName = uiApp.Application.Username;
                string[] rolesFile = File.ReadAllLines($"{serverPath}roles.txt");
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

                    string link = "https://portal.talan.group/knowledge/proektirovanie/startraboty/";
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

            if (_config.LicenseType == "corp") 
            {
                string usagefilePath = serverPath + "usage.txt";
                //время открытия
                if (File.Exists(usagefilePath) && _startTime.HasValue && info.IsWorkshared)
                {
                    double seconds = (DateTime.Now - _startTime.Value).TotalSeconds;
                    seconds = Math.Round(seconds);
                    string modelPath = e.Document.PathName;
                    string docName = Path.GetFileName(modelPath);
                    docName = docName.Replace(",", " ");
                    Autodesk.Revit.ApplicationServices.Application rvtApp = e.Document.Application;
                    string userName = rvtApp.Username; string docNameUserName = "_" + userName; docName = docName.Replace(docNameUserName, "");
                    docName = docName.Replace(".rvt", "");
                    string path = $"{serverPath}users/{userName},{docName}.txt";
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
                    if (File.Exists(path)) date = "\n" + date;
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
                    File.AppendAllText(path, $"{date},{seconds},pc: {fullUserName},opened: {opened},closed: {closed},{fileSize}");
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
            if (_config.LicenseType == "corp") //подразумевается, что Корпоративная подписка содержит весь функционал
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

        public void OnSyncCentralEnd(object sender, DocumentSynchronizedWithCentralEventArgs e)
        {
            if (_config.LicenseType == "corp")
            {
                //журнал
                info = BasicFileInfo.Extract(e.Document.PathName);
                string docName = e.Document.Title;
                string userName = info.Username;
                string docNameUserName = "_" + userName; docName = docName.Replace(docNameUserName, "");
                docName = docName.Replace(",", "");
                DateTime dateTime = DateTime.Now; string TNovVersion = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString();
                string date = dateTime.ToString(); date = date.Replace(",", "");
                string usagefilePath = $"{serverPath}projects/{docName},synchronizes.txt";
                System.IO.File.AppendAllText(usagefilePath, "\n" + date + "," + userName + "," + docName);
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
        private void SetPanelColor(PanelColorState state)
        {
            adWin.RibbonControl ribbon = adWin.ComponentManager.Ribbon;
            if (ribbon == null) return;

            SolidColorBrush backgroundBrush, titleBrush;
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
                    backgroundBrush = BrushDefault;
                    titleBrush = BrushDefault;
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

        // Конвертер изображения
        private BitmapSource GetImageSource(System.Drawing.Image img)
        {
            BitmapImage bmp = new BitmapImage();
            using (MemoryStream ms = new MemoryStream())
            {
                img.Save(ms, ImageFormat.Png);
                ms.Position = 0;

                bmp.BeginInit();

                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.UriSource = null;
                bmp.StreamSource = ms;

                bmp.EndInit();
            }
            return bmp;
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