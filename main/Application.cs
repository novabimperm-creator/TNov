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
using System.Threading;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using TNov.main;
using TNov.Panel13;
using static System.Windows.Forms.LinkLabel;
using adWin = Autodesk.Windows;
using RibbonItem = Autodesk.Revit.UI.RibbonItem;
using RibbonPanel = Autodesk.Revit.UI.RibbonPanel;
using SplitButton = Autodesk.Revit.UI.SplitButton;

/*
git add .
git commit -m "4.0.1"
git push origin main
 */
namespace TNov
{
    [Regeneration(RegenerationOption.Manual)]
    internal class Application : IExternalApplication
    {
        static AddInId addinId = new AddInId(new Guid("83403DB6-EA74-4E10-85B3-508AE241A743"));

        private BasicFileInfo info;
        private Stopwatch stopwatch;
        private string syncOption = "Подсветка 20/30 минут";
        private int time1 = 0;
        private int time2 = 0;
        private static List<RibbonPanel> _ARRibbonItems = new List<RibbonPanel>();
        private static List<RibbonPanel> _STRibbonItems = new List<RibbonPanel>();
        private static List<RibbonPanel> _MEPRibbonItems = new List<RibbonPanel>();
        private static List<RibbonPanel> _BIMRibbonItems = new List<RibbonPanel>();
        private ComboBox _comboBox;
        public Result OnStartup(UIControlledApplication application)
        {
            // Подписываемся на событие создания нового документа
            application.ControlledApplication.DocumentCreated += OnDocumentCreated;

            //Подгрузка настроек времени раскраски вкладок
            var viewModel0 = new aboutViewModel();
            string jsonpath = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "TNovClient/TNovSettings.json");
            try
            {
                viewModel0 = JsonConvert.DeserializeObject<aboutViewModel>(File.ReadAllText(jsonpath));
            }
            catch (Exception) { }
            syncOption = viewModel0.sync1;
            if (syncOption == "Подсветка 20/30 минут")
            {
                time1 = 1200000; time2 = 1800000;
            }
            else if(syncOption == "Подсветка 30/60 минут")
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
            else if(syncOption.Contains( "Подсветка 1/2 минуты"))
            {
                time1 = 60000; time2 = 120000;
            }
            

            //Регистрация событий
            try
            {
                application.ControlledApplication.DocumentOpened += new EventHandler<DocumentOpenedEventArgs>(OnDocumentOpened);
                application.ControlledApplication.DocumentSynchronizingWithCentral += new EventHandler<DocumentSynchronizingWithCentralEventArgs>(OnSyncCentralStart);
                application.ControlledApplication.DocumentSynchronizedWithCentral += new EventHandler<DocumentSynchronizedWithCentralEventArgs>(OnSyncCentralEnd);
                application.ControlledApplication.DocumentClosed += new EventHandler<DocumentClosedEventArgs>(OnDocumentClosed);
                application.Idling += OnIdling;

                application.DialogBoxShowing += new EventHandler<DialogBoxShowingEventArgs>(a_DialogBoxShowing);
            }
            catch (Exception) { }

            //Апдейтеры

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

            // Создание вкладок, панелей, кнопок

            string assemblyLocation = Assembly.GetExecutingAssembly().Location, tabName = "TNov";

            application.CreateRibbonTab(tabName);



            ContextualHelp mainhelp = new ContextualHelp(ContextualHelpType.Url,
                "https://portal.talan.group/knowledge/proektirovanie/");

            // Панель "Настройки"

            RibbonPanel panel0 = application.CreateRibbonPanel(tabName, "Настройки");

            ComboBoxData comboData = new ComboBoxData("Режим");
            
            //проверка актуальности версии 
#if config1 || config2
            string TNovVersion = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString();
            string[] versionparts = TNovVersion.Split('.');
            double versionMath = Convert.ToDouble(versionparts[0] + "000000") + Convert.ToDouble(versionparts[1] + "0000") +
                Convert.ToDouble(versionparts[2] + "00") + Convert.ToDouble(versionparts[3]);
            string verfilePath = nova.novaserver + "_TNov/actual/version.txt";
            string actualVersion = TNovVersion;
            try
            {
                actualVersion = File.ReadAllText(verfilePath);
            }
            catch (Exception) { }
            string[] actualversionparts = actualVersion.Split('.');
            double actualversionMath = Convert.ToDouble(actualversionparts[0] + "000000") + Convert.ToDouble(actualversionparts[1] + "0000") +
                Convert.ToDouble(actualversionparts[2] + "00") + Convert.ToDouble(actualversionparts[3]);

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
 
            string verfilePathC = nova.novaserver + "_TNov/actual/clientversion.txt";
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
                    File.Copy(nova.novafolder + "client/TNovClient.deps.json", Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "TNovClient/TNovClient.deps.json"), true);
                    File.Copy(nova.novafolder + "client/TNovClient.dll", Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "TNovClient/TNovClient.dll"), true);
                    File.Copy(nova.novafolder + "client/TNovClient.exe", Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "TNovClient/TNovClient.exe"), true);
                    File.Copy(nova.novafolder + "client/TNovClient.pdb", Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "TNovClient/TNovClient.pdb"), true);
                    File.Copy(nova.novafolder + "client/TNovClient.runtimeconfig.json", Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "TNovClient/TNovClient.runtimeconfig.json"), true);
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
#endif
            // кнопка "Настройки"

            System.Drawing.Image imgN = Properties.Resources.logo;
#if config1 || config2
            if (actualversionMath > versionMath) { imgN = Properties.Resources.attention32; }
#endif
            System.Drawing.Image imgNmin = Properties.Resources.logomin;
#if config1 || config2
            if (actualversionMath > versionMath) { imgNmin = Properties.Resources.attention16; }
#endif
            PushButtonData buttonDataN = new PushButtonData(nameof(appversion), "Настройки", assemblyLocation, typeof(appversion).FullName)
            {
                //LargeImage = GetImageSource(imgN),
                Image = GetImageSource(imgNmin),
                ToolTip = "Глобальные настройки плагина и сведения о программе."
            };
            buttonDataN.SetContextualHelp(mainhelp);

            IList<RibbonItem> ribbonItemList0 = panel0.AddStackedItems(buttonDataN,(RibbonItemData)comboData);
            _comboBox = ribbonItemList0[1] as ComboBox; 
            _comboBox.AddItem(new ComboBoxMemberData("Все", "Все"));
            _comboBox.AddItem(new ComboBoxMemberData("АР", "АР"));
            _comboBox.AddItem(new ComboBoxMemberData("КЖ", "КЖ"));
            _comboBox.AddItem(new ComboBoxMemberData("Сети", "Сети"));
            _comboBox.AddItem(new ComboBoxMemberData("BIM", "BIM"));
            _comboBox.CurrentChanged += OnComboBoxCurrentChanged; //подписка на событие изменения выбора
            

            // Панель "Проект"

            RibbonPanel panel1 = application.CreateRibbonPanel(tabName, "Проект");

            // кнопка "Реестр замечаний"

            System.Drawing.Image imgCDE = Properties.Resources.CDE32;
            System.Drawing.Image imgCDEmin = Properties.Resources.CDE16;
            PushButtonData buttonDataCDE = new PushButtonData(nameof(CDE), "Реестр\nзамечаний", assemblyLocation, typeof(CDE).FullName)
            {
                LargeImage = GetImageSource(imgCDE),
                Image = GetImageSource(imgCDEmin),
                ToolTip = "Открыть замечания по проекту в среде общих данных Vitro."
            };
            ContextualHelp CDEhelp = new ContextualHelp(ContextualHelpType.Url,
                "https://portal.talan.group/knowledge/proektirovanie/reestrzamechaniy/");
            buttonDataCDE.SetContextualHelp(CDEhelp);

            panel1.AddItem(buttonDataCDE);

            // кнопка "Журнал синхронизаций"

            System.Drawing.Image imgJournal = Properties.Resources.journal32;
            System.Drawing.Image imgJournalmin = Properties.Resources.journal16;
            PushButtonData buttonDataJournal = new PushButtonData(nameof(Journal), "Журнал\nсинхронизаций", assemblyLocation, typeof(Journal).FullName)
            {
                LargeImage = GetImageSource(imgJournal),
                Image = GetImageSource(imgJournalmin),
                ToolTip = "Открыть журнал синхронизаций текущей модели."
            };
            buttonDataJournal.SetContextualHelp(CDEhelp);

            panel1.AddItem(buttonDataJournal);

            

            

            // Панель "Общие"

            RibbonPanel panel3 = application.CreateRibbonPanel(tabName, "Общие");

            // сгруппированная кнопка "Таблица параметров"

            System.Drawing.Image imgParamTable = Properties.Resources.ParamTable32;
            System.Drawing.Image imgParamTablemin = Properties.Resources.ParamTable16;
            PushButtonData buttonDataParamTable = new PushButtonData(nameof(ParamTable), "Таблица параметров", assemblyLocation, typeof(ParamTable).FullName)
            {
                Image = GetImageSource(imgParamTablemin),
                ToolTip = "Открыть таблицу требований к модели."
            };
            buttonDataParamTable.SetContextualHelp(CDEhelp);

            // сгруппированная кнопка "База знаний"

            System.Drawing.Image imgwiki = Properties.Resources.wiki32;
            System.Drawing.Image imgwikimin = Properties.Resources.wiki16;
            PushButtonData buttonDatawiki = new PushButtonData(nameof(WorkOrg), "База знаний", assemblyLocation, typeof(WorkOrg).FullName)
            {
                Image = GetImageSource(imgwikimin),
                ToolTip = "Wiki по работе в Revit и не только."
            };
            buttonDatawiki.SetContextualHelp(mainhelp);

            // сгруппированная кнопка "Учебный портал"

            System.Drawing.Image imgedu = Properties.Resources.edu32;
            System.Drawing.Image imgedumin = Properties.Resources.edu16;
            PushButtonData buttonDataedu = new PushButtonData(nameof(EduPortal), "Учебный портал", assemblyLocation, typeof(EduPortal).FullName)
            {
                Image = GetImageSource(imgedumin),
                ToolTip = "Перейти на учебный портал (moodle.talan.group)."
            };
            ContextualHelp eduhelp = new ContextualHelp(ContextualHelpType.Url,
                "https://moodle.talan.group");
            buttonDataedu.SetContextualHelp(eduhelp);

            // группа кнопок "Таблица параметров", "База знаний", "Учебный портал"

            panel3.AddStackedItems(buttonDataParamTable, buttonDatawiki, buttonDataedu);

            
            // кнопка "Связной"

            System.Drawing.Image imglinks = Properties.Resources.links32;
            System.Drawing.Image imglinksmin = Properties.Resources.links16;
            PushButtonData buttonDatalinks = new PushButtonData(nameof(Links), "Связной", assemblyLocation, typeof(Links).FullName)
            {
                LargeImage = GetImageSource(imglinks),
                Image = GetImageSource(imglinksmin),
                ToolTip = "Пакетная вставка связей с помещением их в рабочие наборы."
            };
            ContextualHelp linkshelp = new ContextualHelp(ContextualHelpType.Url,
                "https://portal.talan.group/knowledge/proektirovanie/plaginyiskriptynovatsiya/");
            buttonDatalinks.SetContextualHelp(linkshelp);
            panel3.AddItem(buttonDatalinks);
           

            // кнопка с выпадающим списком "Закреплятор Уровни Наборы"

            // - подкнопка "Закреплятор Уровни Наборы"

            System.Drawing.Image imgplw = Properties.Resources.plw32;
            System.Drawing.Image imgplwmin = Properties.Resources.plw16;
            PushButtonData buttonDataplw = new PushButtonData(nameof(PLW), "Закреплятор\nУровни Наборы", assemblyLocation, typeof(PLW).FullName)
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
            PushButtonData buttonDataplwSettings = new PushButtonData(nameof(PLWSettings), "Настройки", assemblyLocation, typeof(PLWSettings).FullName)
            {
                LargeImage = GetImageSource(imgplwSettings),
                Image = GetImageSource(imgplwSettingsmin),
                ToolTip = "Настройки плагина Закреплятор Уровни Наборы."
            };
            buttonDataplwSettings.SetContextualHelp(plwhelp);

            
            // - подкнопка "Откреплятор"

            System.Drawing.Image imgunpinner = Properties.Resources.unpinner32;
            System.Drawing.Image imgunpinnermin = Properties.Resources.unpinner16;
            PushButtonData buttonDataunpinner = new PushButtonData(nameof(Unpinner), "Откреплятор", assemblyLocation, typeof(Unpinner).FullName)
            {
                LargeImage = GetImageSource(imgunpinner),
                Image = GetImageSource(imgunpinnermin),
                ToolTip = "Открепить оси, уровни и rvt-связи (на выбор)."
            };
            buttonDataunpinner.SetContextualHelp(plwhelp);

            // - основная кнопка

            SplitButtonData buttonDataplwgroup = new SplitButtonData("Закреплятор\nУровни Наборы", "Закрепить оси, уровни и rvt-связи, переименовать отметки в уровнях, назначить рабочие наборы для связей, осей и уровней.");
            SplitButton groupplw = panel3.AddItem(buttonDataplwgroup) as SplitButton;
            groupplw.AddPushButton(buttonDataplw);
            groupplw.AddPushButton(buttonDataplwSettings);
            groupplw.AddPushButton(buttonDataunpinner);
            groupplw.SetContextualHelp(plwhelp);
                        
            
            // сгруппированная кнопка "Изменения"

            System.Drawing.Image imgchanges = Properties.Resources.changes32;
            System.Drawing.Image imgchangesmin = Properties.Resources.changes16;
            PushButtonData buttonDatachanges = new PushButtonData(nameof(Changes), "Изменения", assemblyLocation, typeof(Changes).FullName)
            {
                Image = GetImageSource(imgchangesmin),
                ToolTip = "Автонумерация облаков и заполнение параметров листов."
            };
            ContextualHelp changeshelp = new ContextualHelp(ContextualHelpType.Url,
                "https://portal.talan.group/knowledge/proektirovanie/oformlenie/");
            buttonDatachanges.SetContextualHelp(changeshelp);


            // сгруппированная кнопка "Выбор по ID"

            System.Drawing.Image imgidselection = Properties.Resources.idselection32;
            System.Drawing.Image imgidselectionmin = Properties.Resources.idselection16;
            PushButtonData buttonDataidselection = new PushButtonData(nameof(IdSelection), "Выбор по ID", assemblyLocation, typeof(IdSelection).FullName)
            {
                Image = GetImageSource(imgidselectionmin),
                ToolTip = "Выбрать и изолировать элементы по ID."
            };
            ContextualHelp idselectionhelp = new ContextualHelp(ContextualHelpType.Url,
                "https://portal.talan.group/knowledge/proektirovanie/rabotaskolliziyami/");
            buttonDataidselection.SetContextualHelp(idselectionhelp);

            // подкнопка "Excel"

            System.Drawing.Image imgexcel = Properties.Resources.excel32;
            System.Drawing.Image imgexcelmin = Properties.Resources.excel16;
            PushButtonData buttonDataexcel = new PushButtonData(nameof(Excel), "Excel", assemblyLocation, typeof(Excel).FullName)
            {
                Image = GetImageSource(imgexcelmin),
                ToolTip = "Экспорт спецификации в Excel."
            };
            ContextualHelp excelhelp = new ContextualHelp(ContextualHelpType.Url,
                "https://portal.talan.group/knowledge/proektirovanie/plaginyiskriptynovatsiya/");
            buttonDataexcel.SetContextualHelp(excelhelp);

            // подкнопка "Excel.Настройки"

            PushButtonData buttonDataexcelSettings = new PushButtonData(nameof(ExcelSettings), "Excel.Настройки", assemblyLocation, typeof(ExcelSettings).FullName)
            {
                Image = GetImageSource(imgexcelmin),
                ToolTip = "Экспорт спецификации в Excel."
            };
            buttonDataexcelSettings.SetContextualHelp(excelhelp);

            // группа кнопок "Изменения", "Выбор по ID", "Excel"

            SplitButtonData splitButtonDataExcel = new SplitButtonData("Excel", "Экспорт спецификации в Excel.");
            IList<RibbonItem> ribbonItemList = panel3.AddStackedItems(buttonDatachanges, buttonDataidselection, (RibbonItemData)splitButtonDataExcel);
            SplitButton splitButtonExcel = ribbonItemList[2] as SplitButton;
            ((PulldownButton)splitButtonExcel).AddPushButton(buttonDataexcel);
            ((PulldownButton)splitButtonExcel).AddPushButton(buttonDataexcelSettings);

            // кнопка "Менеджер листов"

            System.Drawing.Image imgsheets = Properties.Resources.sheets32;
            System.Drawing.Image imgsheetsmin = Properties.Resources.sheets16;
            PushButtonData buttonDatasheets = new PushButtonData(nameof(Sheets), "Менеджер\nлистов", assemblyLocation, typeof(Sheets).FullName)
            {
                LargeImage = GetImageSource(imgsheets),
                Image = GetImageSource(imgsheetsmin),
                ToolTip = "Перенумерация листов, формирование комплектов на печать."
            };
            ContextualHelp sheetshelp = new ContextualHelp(ContextualHelpType.Url,
                "https://portal.talan.group/knowledge/proektirovanie/listynumeratsiyaikomplektynaeksport/");
            buttonDatasheets.SetContextualHelp(sheetshelp);
            panel3.AddItem(buttonDatasheets);

            // кнопка "Типофильтр"

            System.Drawing.Image imgfilter = Properties.Resources.typefilter32;
            System.Drawing.Image imgfiltermin = Properties.Resources.typefilter16;
            PushButtonData buttonDatafilter = new PushButtonData(nameof(Panel3.typefilter.TypeFilter), "Типофильтр", assemblyLocation, typeof(Panel3.typefilter.TypeFilter).FullName)
            {
                LargeImage = GetImageSource(imgfilter),
                Image = GetImageSource(imgfiltermin),
                ToolTip = "Фильтрация на виде по типам элементов, создание фильтров в проекте."
            };
            ContextualHelp filterhelp = new ContextualHelp(ContextualHelpType.Url,
                "https://portal.talan.group/knowledge/proektirovanie/tipofiltr/");
            buttonDatafilter.SetContextualHelp(filterhelp);
            panel3.AddItem(buttonDatafilter);

            // Панель "АР Модель"

            RibbonPanel panel4 = application.CreateRibbonPanel(tabName, "АР Модель");
            _ARRibbonItems.Add(panel4);

            // кнопка "Генератор полов"

            System.Drawing.Image imgfloors = Properties.Resources.floors32;
            System.Drawing.Image imgfloorsmin = Properties.Resources.floors16;
            PushButtonData buttonDatafloors = new PushButtonData(nameof(Floors), "Генератор\nполов", assemblyLocation, typeof(Floors).FullName)
            {
                LargeImage = GetImageSource(imgfloors),
                Image = GetImageSource(imgfloorsmin),
                ToolTip = "Создать полы в помещениях."
            };
            ContextualHelp floorshelp = new ContextualHelp(ContextualHelpType.Url,
                "https://portal.talan.group/knowledge/proektirovanie/poly/");
            buttonDatafloors.SetContextualHelp(floorshelp);
            panel4.AddItem(buttonDatafloors);

            // кнопка "Антизеркало"
            System.Drawing.Image imgmirror = Properties.Resources.mirror32;
            System.Drawing.Image imgmirrormin = Properties.Resources.mirror16;
            PushButtonData buttonDatamirror = new PushButtonData(nameof(Mirror), "Антизеркало", assemblyLocation, typeof(Mirror).FullName)
            {
                LargeImage = GetImageSource(imgmirror),
                Image = GetImageSource(imgmirrormin),
                ToolTip = "Выделить отзеркаленные окна и двери, пометить такие элементы через параметр Марка."
            };
            ContextualHelp mirrorhelp = new ContextualHelp(ContextualHelpType.Url,
                "https://portal.talan.group/knowledge/proektirovanie/okna/");
            buttonDatamirror.SetContextualHelp(mirrorhelp);
            panel4.AddItem(buttonDatamirror);

            // кнопка "Перемычки"

            System.Drawing.Image imgbeamscut = Properties.Resources.beamscut32;
            System.Drawing.Image imgbeamscutmin = Properties.Resources.beamscut16;
            PushButtonData buttonDatabeamscut = new PushButtonData(nameof(Beams), "Перемычки", assemblyLocation, typeof(Beams).FullName)
            {
                LargeImage = GetImageSource(imgbeamscut),
                Image = GetImageSource(imgbeamscutmin),
                ToolTip = "Вырезать объем бетонных перемычек из стен, сформировать эскизы ПР."
            };
            ContextualHelp beamshelp = new ContextualHelp(ContextualHelpType.Url,
                "https://portal.talan.group/knowledge/proektirovanie/vedomostperemychek/");
            buttonDatabeamscut.SetContextualHelp(beamshelp);
            panel4.AddItem(buttonDatabeamscut);

            // Панель "АР Параметры"

            RibbonPanel panel5 = application.CreateRibbonPanel(tabName, "АР Параметры");
            _ARRibbonItems.Add(panel5);

            // кнопка "Эт.Номер"

            System.Drawing.Image imglevelnumber = Properties.Resources.levelnumber32;
            System.Drawing.Image imglevelnumbermin = Properties.Resources.levelnumber16;
            PushButtonData buttonDatalevelnumber = new PushButtonData(nameof(LevelNumber), "Эт.Номер", assemblyLocation, typeof(LevelNumber).FullName)
            {
                LargeImage = GetImageSource(imglevelnumber),
                Image = GetImageSource(imglevelnumbermin),
                ToolTip = "Заполнить Эт.Номер у элементов модели (с выбором категорий)."
            };
            ContextualHelp levelnumberhelp = new ContextualHelp(ContextualHelpType.Url,
                "https://portal.talan.group/knowledge/proektirovanie/specificationsbylevel/");
            buttonDatalevelnumber.SetContextualHelp(levelnumberhelp);
            panel5.AddItem(buttonDatalevelnumber);

            // кнопка с выпадающим списком "Помещения"

            // подкнопка "Номера помещений"

            System.Drawing.Image imgrooms = Properties.Resources.roomsnum32;
            System.Drawing.Image imgroomsmin = Properties.Resources.roomsnum16;
            PushButtonData buttonDatarooms = new PushButtonData(nameof(RoomsNum), "Номера помещений", assemblyLocation, typeof(RoomsNum).FullName)
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
            PushButtonData buttonDataroomsround = new PushButtonData(nameof(RoomsRound), "Округлятор", assemblyLocation, typeof(RoomsRound).FullName)
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
            PushButtonData buttonDataapartsnum = new PushButtonData(nameof(ApartsNumAtLevel), "Нумератор квартир", assemblyLocation, typeof(ApartsNumAtLevel).FullName)
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
            PushButtonData buttonDataaparts = new PushButtonData(nameof(Aparts), "Квартирография", assemblyLocation, typeof(Aparts).FullName)
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
            PushButtonData buttonDataoffices = new PushButtonData(nameof(Offices), "Офисография", assemblyLocation, typeof(Offices).FullName)
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
            PushButtonData buttonDatafailedrooms = new PushButtonData(nameof(PurgeFailedRooms), "Удалить лишние", assemblyLocation, typeof(PurgeFailedRooms).FullName)
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
            PushButtonData buttonDataroomsbackup = new PushButtonData(nameof(RoomsBackup), "Резервные копии", assemblyLocation, typeof(RoomsBackup).FullName)
            {
                LargeImage = GetImageSource(imgroomsbackup),
                Image = GetImageSource(imgroomsbackupmin),
                ToolTip = "Резервное копирование и восстановление значений площадей помещений."
            };
            ContextualHelp roomsbackuphelp = new ContextualHelp(ContextualHelpType.Url,
                "https://portal.talan.group/knowledge/proektirovanie/pomeshcheniyarezervnoekopirovanieivosstanovlenie/");
            buttonDataroomsbackup.SetContextualHelp(roomsbackuphelp);

            // подкнопка "Номера по ТЗ"

            PushButtonData buttonDataroomsTNumber = new PushButtonData(nameof(RoomsTNumber), "Номера по ТЗ", assemblyLocation, typeof(RoomsTNumber).FullName)
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
            PulldownButton groupaparts = panel5.AddItem(buttonDataapartsgroup) as PulldownButton;
            groupaparts.AddPushButton(buttonDatarooms);
            groupaparts.AddPushButton(buttonDataroomsround);
            groupaparts.AddPushButton(buttonDataapartsnum);
            groupaparts.AddPushButton(buttonDataaparts);
            groupaparts.AddPushButton(buttonDataoffices);
            groupaparts.AddPushButton(buttonDatafailedrooms);
            groupaparts.AddPushButton(buttonDataroomsbackup);
            groupaparts.AddPushButton(buttonDataroomsTNumber);

            

            // сгруппированная кнопка "Парковки"

            System.Drawing.Image imgpark = Properties.Resources.park32;
            System.Drawing.Image imgparkmin = Properties.Resources.park16;
            PushButtonData buttonDatapark = new PushButtonData(nameof(Parking), "Парковки", assemblyLocation, typeof(Parking).FullName)
            {
                Image = GetImageSource(imgparkmin),
                ToolTip = "Пакет функций для работы с парковками."
            };
            ContextualHelp parkhelp = new ContextualHelp(ContextualHelpType.Url,
                "https://portal.talan.group/knowledge/proektirovanie/parking/");
            buttonDatapark.SetContextualHelp(parkhelp);


            // сгруппированная кнопка "Ведомость полов"

            System.Drawing.Image imgfloorspec = Properties.Resources.floorimages32;
            System.Drawing.Image imgfloorspecmin = Properties.Resources.floorimages16;
            PushButtonData buttonDatafloorspec = new PushButtonData(nameof(FloorImages), "Ведомость полов", assemblyLocation, typeof(FloorImages).FullName)
            {
                Image = GetImageSource(imgfloorspecmin),
                ToolTip = "Сформировать изображения для ведомости полов."
            };
            buttonDatafloorspec.SetContextualHelp(mainhelp);

            // сгруппированная кнопка "Ведомость отделки"

            System.Drawing.Image imgfinishing = Properties.Resources.finishing32;
            System.Drawing.Image imgfinishingmin = Properties.Resources.finishing16;
            PushButtonData buttonDatafinishing = new PushButtonData(nameof(Finishing), "Ведомость\nотделки", assemblyLocation, typeof(Finishing).FullName)
            {
                Image = GetImageSource(imgfinishingmin),
                ToolTip = "Заполнение параметров для ведомости отделки у стен, полов, потолков."
            };
            ContextualHelp finishinghelp = new ContextualHelp(ContextualHelpType.Url,
                "https://portal.talan.group/knowledge/proektirovanie/vedomostotdelkipomeshcheniy/");
            buttonDatafinishing.SetContextualHelp(finishinghelp);

            // группа кнопок "Парковки", "Ведомость полов", "Ведомость отделки"

            panel5.AddStackedItems(buttonDatapark, buttonDatafloorspec, buttonDatafinishing);

            
            
            // Панель "КЖ Модель"

            RibbonPanel panel6 = application.CreateRibbonPanel(tabName, "КЖ Модель");
            _STRibbonItems.Add(panel6);

            // кнопка с выпадающим списком "Краска+"

            // подкнопка "Краска+"

            System.Drawing.Image imgpaint = Properties.Resources.paint32;
            System.Drawing.Image imgpaintmin = Properties.Resources.paint16;
            PushButtonData buttonDatapaint = new PushButtonData(nameof(Paint), "Краска+", assemblyLocation, typeof(Paint).FullName)
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
            PushButtonData buttonDatarevitpaint = new PushButtonData(nameof(revitpaint), "Краска", assemblyLocation, typeof(revitpaint).FullName)
            {
                LargeImage = GetImageSource(imgrevitpaint),
                Image = GetImageSource(imgrevitpaintmin),
                ToolTip = "Применение материала к грани элемента."
            };
            buttonDatarevitpaint.SetContextualHelp(painthelp);

            // подкнопка "Разделение грани"

            System.Drawing.Image imgrevitsplitface = Properties.Resources.revitsplitface32;
            System.Drawing.Image imgrevitsplitfacemin = Properties.Resources.revitsplitface16;
            PushButtonData buttonDatarevitsplitface = new PushButtonData(nameof(revitsplitface), "Разделение грани", assemblyLocation, typeof(revitsplitface).FullName)
            {
                LargeImage = GetImageSource(imgrevitsplitface),
                Image = GetImageSource(imgrevitsplitfacemin),
                ToolTip = "Разделение грани элемента."
            };
            buttonDatarevitsplitface.SetContextualHelp(painthelp);

            // подкнопка "Материал?"

            System.Drawing.Image imgpaint2 = Properties.Resources.paint2_32;
            System.Drawing.Image imgpaint2min = Properties.Resources.paint2_16;
            PushButtonData buttonDatapaint2 = new PushButtonData(nameof(Paint2), "Материал?", assemblyLocation, typeof(Paint2).FullName)
            {
                LargeImage = GetImageSource(imgpaint2),
                Image = GetImageSource(imgpaint2min),
                ToolTip = "Получить имя материала выбранной грани."
            };
            buttonDatapaint2.SetContextualHelp(painthelp);

            // подкнопка "Удалить краску"

            System.Drawing.Image imgrevitpaintdel = Properties.Resources.revitpaintdel32;
            System.Drawing.Image imgrevitpaintdelmin = Properties.Resources.revitpaintdel16;
            PushButtonData buttonDatarevitpaintdel = new PushButtonData(nameof(revitpaintdel), "Удалить краску", assemblyLocation, typeof(revitpaintdel).FullName)
            {
                LargeImage = GetImageSource(imgrevitpaintdel),
                Image = GetImageSource(imgrevitpaintdelmin),
                ToolTip = "Удалить краску с грани элемента."
            };
            buttonDatarevitpaintdel.SetContextualHelp(painthelp);

            // - основная кнопка

            SplitButtonData buttonDatapaintgroup = new SplitButtonData("Краска+", "Копирование краски.");
            SplitButton grouppaint = panel6.AddItem(buttonDatapaintgroup) as SplitButton;
            grouppaint.AddPushButton(buttonDatapaint);
            grouppaint.AddPushButton(buttonDatarevitpaint);
            grouppaint.AddPushButton(buttonDatarevitsplitface);
            grouppaint.AddPushButton(buttonDatapaint2);
            grouppaint.AddPushButton(buttonDatarevitpaintdel);
            grouppaint.SetContextualHelp(painthelp);


            // сгруппированная кнопка "Ускорить файл"

            System.Drawing.Image imgfixstructurefile = Properties.Resources.fixstructurefile32;
            System.Drawing.Image imgfixstructurefilemin = Properties.Resources.fixstructurefile16;
            PushButtonData buttonDatafixstructurefile = new PushButtonData(nameof(Fixstructurefile), "Ускорить файл", assemblyLocation, typeof(Fixstructurefile).FullName)
            {
                Image = GetImageSource(imgfixstructurefilemin),
                ToolTip = "Ускорить работу модели КЖ путем манипуляций с параметрами несущей арматуры."
            };
            ContextualHelp fixstructurefilehelp = new ContextualHelp(ContextualHelpType.Url,
                "https://portal.talan.group/knowledge/proektirovanie/uskorenierabotyfaylovmodeli_posu/");
            buttonDatafixstructurefile.SetContextualHelp(fixstructurefilehelp);
                  
            

            // сгруппированная кнопка "Сваи"

            System.Drawing.Image imgpiles = Properties.Resources.foundcut32;
            System.Drawing.Image imgpilesmin = Properties.Resources.foundcut16;
            PushButtonData buttonDatapiles = new PushButtonData(nameof(Found), "Сваи", assemblyLocation, typeof(Found).FullName)
            {
                Image = GetImageSource(imgpilesmin),
                ToolTip = "Пакет функций по работе со сваями."
            };
            ContextualHelp pileshelp = new ContextualHelp(ContextualHelpType.Url,
                "https://portal.talan.group/knowledge/proektirovanie/svai_xmqe/");
            buttonDatapiles.SetContextualHelp(pileshelp);

            // группа кнопок "Ускорить файл", "Сваи"

            panel6.AddStackedItems(buttonDatafixstructurefile, buttonDatapiles);
            

            // Панель "КЖ Параметры"

            RibbonPanel panel7 = application.CreateRibbonPanel(tabName, "КЖ Параметры");
            _STRibbonItems.Add(panel7);

            // сгруппированная кнопка "Эскизы деталей"

            System.Drawing.Image imgrebarimages = Properties.Resources.rebarimages32;
            System.Drawing.Image imgrebarimagesmin = Properties.Resources.rebarimages16;
            PushButtonData buttonDatarebarimages = new PushButtonData(nameof(RebarImages), "Эскизы деталей", assemblyLocation, typeof(RebarImages).FullName)
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
            PushButtonData buttonDatasteelschedule = new PushButtonData(nameof(SteelSchedule), "ВРС подчистить", assemblyLocation, typeof(SteelSchedule).FullName)
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
            PushButtonData buttonDataschemespec = new PushButtonData(nameof(Schemespec), "Группировка", assemblyLocation, typeof(Schemespec).FullName)
            {
                Image = GetImageSource(imgschemespecmin),
                ToolTip = "Заполнить параметр A_Группирование для сортировки спецификаций)."
            };
            ContextualHelp schemespechelp = new ContextualHelp(ContextualHelpType.Url,
                "https://portal.talan.group/knowledge/proektirovanie/skhemaraspolozheniyakonstruktsiy/");
            buttonDataschemespec.SetContextualHelp(schemespechelp);

            // группа кнопок "Эскизы деталей", "ВРС подчистить", "Группировка"

            panel7.AddStackedItems(buttonDatarebarimages, buttonDatasteelschedule, buttonDataschemespec);


            

            // Панель "Сети"

            RibbonPanel panel9 = application.CreateRibbonPanel(tabName, "Сети");
            _MEPRibbonItems.Add(panel9);

            // кнопка "Сводная спека"

            System.Drawing.Image imgadskg = Properties.Resources.adskg32;
            System.Drawing.Image imgadskgmin = Properties.Resources.adskg16;
            PushButtonData buttonDataadskg = new PushButtonData(nameof(MEPSpec), "Сводная\nспека", assemblyLocation, typeof(MEPSpec).FullName)
            {
                LargeImage = GetImageSource(imgadskg),
                Image = GetImageSource(imgadskgmin),
                ToolTip = "Заполнить параметры у элементов ВК ОВ / ЭЛ / СС ПС для формирования сводной спецификации."
            };
            ContextualHelp adskghelp = new ContextualHelp(ContextualHelpType.Url,
                "https://portal.talan.group/knowledge/proektirovanie/MEPspec/");
            buttonDataadskg.SetContextualHelp(adskghelp);
            panel9.AddItem(buttonDataadskg);

            // сгруппированная кнопка "Стенки Классы"

            System.Drawing.Image imgadskstenki = Properties.Resources.adskstenki32;
            System.Drawing.Image imgadskstenkimin = Properties.Resources.adskstenki16;
            PushButtonData buttonDataadskstenki = new PushButtonData(nameof(DuctThicknessClasses), "Стенки классы", assemblyLocation, typeof(DuctThicknessClasses).FullName)
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
            PushButtonData buttonDataduct3d = new PushButtonData(nameof(Duct3D), "Схемы ОВ2", assemblyLocation, typeof(Duct3D).FullName)
            {
                Image = GetImageSource(imgduct3dmin),
                ToolTip = "Создать/заменить схемы систем вентиляции."
            };
            ContextualHelp duct3dhelp = new ContextualHelp(ContextualHelpType.Url,
                "https://portal.talan.group/knowledge/proektirovanie/MEPviews/");
            buttonDataduct3d.SetContextualHelp(duct3dhelp);

            // сгруппированная кнопка "Адресатор"

            System.Drawing.Image imgss = Properties.Resources.ssNumberer32;
            System.Drawing.Image imgssmin = Properties.Resources.ssNumberer16;
            PushButtonData buttonDatass = new PushButtonData(nameof(SSNumberer), "Адресатор", assemblyLocation, typeof(SSNumberer).FullName)
            {
                Image = GetImageSource(imgssmin),
                ToolTip = "Пакет функций по адресации устройств СС ПС."
            };
            ContextualHelp sshelp = new ContextualHelp(ContextualHelpType.Url,
                "https://portal.talan.group/knowledge/proektirovanie/");
            buttonDatass.SetContextualHelp(sshelp);

            // группа кнопок "Стенки Классы", "Схемы ОВ2", "Адресатор"

            panel9.AddStackedItems(buttonDataadskstenki, buttonDataduct3d, buttonDatass);

            // подкнопка "ЭЛ Отметки размещения"

            System.Drawing.Image imgefl = Properties.Resources.efl32;
            System.Drawing.Image imgeflmin = Properties.Resources.efl16;
            PushButtonData buttonDataefl = new PushButtonData(nameof(ElElevValues), "ЭЛ Отметки", assemblyLocation, typeof(ElElevValues).FullName)
            {
                Image = GetImageSource(imgeflmin),
                ToolTip = "Заполнить параметры N_ЭЛ.Высота стяжки и N_ЭЛ.Отметка потолка у выключателей, осветительных и электрических приборов, электрооборудования."
            };
            ContextualHelp eflhelp = new ContextualHelp(ContextualHelpType.Url,
                "https://portal.talan.group/knowledge/proektirovanie/plaginyiskriptynovatsiya/");
            buttonDataefl.SetContextualHelp(eflhelp);

            // подкнопка "ЭЛ Отметки размещения. Настройки"

            PushButtonData buttonDataeflsettings = new PushButtonData(nameof(ElElevValuesSettings), "Отметки.Настройки", assemblyLocation, typeof(ElElevValuesSettings).FullName)
            {
                Image = GetImageSource(imgeflmin),
                ToolTip = "Настройки плагина ЭЛ Отметки."
            };
            buttonDataeflsettings.SetContextualHelp(eflhelp);

            // подкнопка "Лотки"

            System.Drawing.Image imgcabletrays = Properties.Resources.cabletrays32;
            System.Drawing.Image imgcabletraysmin = Properties.Resources.cabletrays16;
            PushButtonData buttonDatacabletrays = new PushButtonData(nameof(CableTrays), "Лотки", assemblyLocation, typeof(CableTrays).FullName)
            {
                Image = GetImageSource(imgcabletraysmin),
                ToolTip = "Крышки, перегородки для кабельных лотков, помещение лотков и их элементов в рабочий набор."
            };
            ContextualHelp cabletrayshelp = new ContextualHelp(ContextualHelpType.Url,
                "https://portal.talan.group/knowledge/proektirovanie/lotki/");
            buttonDatacabletrays.SetContextualHelp(cabletrayshelp);

            // подкнопка "Лотки.Настройки"

            PushButtonData buttonDatacabletrayssettings = new PushButtonData(nameof(CableTraysSettings), "Лотки.Настройки", assemblyLocation, typeof(CableTraysSettings).FullName)
            {
                Image = GetImageSource(imgcabletraysmin),
                ToolTip = "Настройки плагина Лотки."
            };
            buttonDatacabletrayssettings.SetContextualHelp(cabletrayshelp);

            // подкнопка "Способы прокладки"

            System.Drawing.Image imgcableways = Properties.Resources.cableways32;
            System.Drawing.Image imgcablewaysmin = Properties.Resources.cableways16;
            PushButtonData buttonDatacableways = new PushButtonData(nameof(CableWays), "Способы прокладки", assemblyLocation, typeof(CableWays).FullName)
            {
                Image = GetImageSource(imgcablewaysmin),
                ToolTip = "Запись значений в параметры автоматического выключателя."
            };
            ContextualHelp cablewayshelp = new ContextualHelp(ContextualHelpType.Url,
                "https://portal.talan.group/knowledge/proektirovanie/");
            buttonDatacableways.SetContextualHelp(cablewayshelp);

            // подкнопка "Прокладка.Настройки"

            PushButtonData buttonDatacablewayssettings = new PushButtonData(nameof(CableWaysSettings), "Прокладка.Настройки", assemblyLocation, typeof(CableWaysSettings).FullName)
            {
                Image = GetImageSource(imgcablewaysmin),
                ToolTip = "Настройки плагина Способы прокладки."
            };
            buttonDatacablewayssettings.SetContextualHelp(cablewayshelp);

            // группа расширенных кнопок "Способы прокладки", "ЭЛ Отметки размещения", "Лотки"

            SplitButtonData splitButtonDataCW = new SplitButtonData("Способы прокладки", "Запись значений в параметры автоматического выключателя.");
            SplitButtonData splitButtonDataEFL = new SplitButtonData("ЭЛ Отметки", "Заполнить параметры N_ЭЛ.Высота стяжки и N_ЭЛ.Отметка потолка у выключателей, осветительных и электрических приборов, электрооборудования.");
            SplitButtonData splitButtonDataCT = new SplitButtonData("Лотки", "Крышки, перегородки для кабельных лотков, помещение лотков и их элементов в рабочий набор.");
            IList<RibbonItem> ribbonItemList2 = panel9.AddStackedItems((RibbonItemData)splitButtonDataCW, (RibbonItemData)splitButtonDataEFL, (RibbonItemData)splitButtonDataCT);
            SplitButton splitButtonCW = ribbonItemList2[0] as SplitButton; 
            SplitButton splitButtonEFL = ribbonItemList2[1] as SplitButton;
            SplitButton splitButtonCT = ribbonItemList2[2] as SplitButton;
            ((PulldownButton)splitButtonCW).AddPushButton(buttonDatacableways);
            ((PulldownButton)splitButtonCW).AddPushButton(buttonDatacablewayssettings);
            ((PulldownButton)splitButtonEFL).AddPushButton(buttonDataefl);
            ((PulldownButton)splitButtonEFL).AddPushButton(buttonDataeflsettings);
            ((PulldownButton)splitButtonCT).AddPushButton(buttonDatacabletrays);
            ((PulldownButton)splitButtonCT).AddPushButton(buttonDatacabletrayssettings);

            // Панель "Pikachu Tools"

            RibbonPanel panel9p = application.CreateRibbonPanel(tabName, "Pikachu Tools");
            _MEPRibbonItems.Add(panel9p);

            // кнопка "FamilyAToFamilyB"
            System.Drawing.Image imgFamilyAToFamilyB = Properties.Resources.pikachu32;
            System.Drawing.Image imgFamilyAToFamilyBmin = Properties.Resources.pikachu16;
            PushButtonData buttonDataFamilyAToFamilyB = new PushButtonData("FamilyAToFamilyB", "Расстановщик\nСС ПС", assemblyLocation, typeof(PikachuCommand).FullName)
            {
                LargeImage = GetImageSource(imgFamilyAToFamilyB),
                Image = GetImageSource(imgFamilyAToFamilyBmin),
                ToolTip = "Универсальное размещение элементов рядом с элементами из связанных файлов",
                LongDescription = "Размещает элементы текущего файла рядом с элементами из связанных файлов\n\nГод напряженный - работаем эффективно!"
            };
            ContextualHelp buttonDataFamilyAToFamilyBhelp = new ContextualHelp(ContextualHelpType.Url,
                "https://portal.talan.group/knowledge/proektirovanie/");
            buttonDataFamilyAToFamilyB.SetContextualHelp(buttonDataFamilyAToFamilyBhelp);
            panel9p.AddItem(buttonDataFamilyAToFamilyB);

            // кнопка "IntersectionCheck"
            System.Drawing.Image imgIntersectionCheck = Properties.Resources.pikachu2_32;
            System.Drawing.Image imgIntersectionCheckmin = Properties.Resources.pikachu2_16;
            PushButtonData buttonDataIntersectionCheck = new PushButtonData("IntersectionCheck", "Проверка\nпересечений", assemblyLocation, typeof(IntersectionCheckCommand).FullName)
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

            // Панель "Отверстия"

            RibbonPanel panel8 = application.CreateRibbonPanel(tabName, "Отверстия");

            // кнопка с выпадающим списком "Задания от ИОС"

            //подкнопка "Задания от ИОС"
            System.Drawing.Image imggettask = Properties.Resources.gettask32;
            System.Drawing.Image imggettaskmin = Properties.Resources.gettask16;
            PushButtonData buttonDatagettask = new PushButtonData(nameof(TasksMenu), "Задания\nот ИОС", assemblyLocation, typeof(TasksMenu).FullName)
            {
                LargeImage = GetImageSource(imggettask),
                Image = GetImageSource(imggettaskmin),
                ToolTip = "Проверить статусы выданных заданий, внедрить/обновить задание."
            };
            ContextualHelp gettaskhelp = new ContextualHelp(ContextualHelpType.Url,
                "https://portal.talan.group/knowledge/proektirovanie/samostoyatelnoemodelirovanieotverstiy/");
            buttonDatagettask.SetContextualHelp(gettaskhelp);

            //подкнопка "Найти элементы"
            System.Drawing.Image imggettaskelems = Properties.Resources.idselectionTasks32;
            System.Drawing.Image imggettaskelemsmin = Properties.Resources.idselectionTasks16;
            PushButtonData buttonDatagettaskelems = new PushButtonData(nameof(IdSelectionTasks), "Найти элементы", assemblyLocation, typeof(IdSelectionTasks).FullName)
            {
                LargeImage = GetImageSource(imggettaskelems),
                Image = GetImageSource(imggettaskelemsmin),
                ToolTip = "Найти отверстия или другие компоненты заданий по Маркам (позициям)."
            };
            buttonDatagettaskelems.SetContextualHelp(gettaskhelp);

            //подкнопка "Проверка отверстий"

            System.Drawing.Image imgholescheckdynamo = Properties.Resources.dynpl32;
            System.Drawing.Image imgholescheckdynamomin = Properties.Resources.dynpl16;
            PushButtonData buttonDataholescheckdynamo = new PushButtonData(nameof(HolesCheckDynamo), "Проверка\nотверстий", assemblyLocation, typeof(HolesCheckDynamo).FullName)
            {
                LargeImage = GetImageSource(imgholescheckdynamo),
                Image = GetImageSource(imgholescheckdynamomin),
                ToolTip = "Запустить скрипт Чек-лист.Отверстия (Dynamo)."
            };
            ContextualHelp holescheckdynamohelp = new ContextualHelp(ContextualHelpType.Url,
                "https://portal.talan.group/knowledge/proektirovanie/samostoyatelnoemodelirovanieotverstiy/");
            buttonDataholescheckdynamo.SetContextualHelp(holescheckdynamohelp);


            SplitButtonData buttonDatataskgroup = new SplitButtonData("Задания\nот ИОС", "Проверить статусы выданных заданий, внедрить/обновить задание.");
            SplitButton grouptask = panel8.AddItem(buttonDatataskgroup) as SplitButton;
            grouptask.AddPushButton(buttonDatagettask);
            grouptask.AddPushButton(buttonDatagettaskelems);
            grouptask.AddPushButton(buttonDataholescheckdynamo);

            // кнопка "Отметки Вырезание"

            System.Drawing.Image imgholes = Properties.Resources.holes32;
            System.Drawing.Image imgholesmin = Properties.Resources.holes16;
            PushButtonData buttonDataholes = new PushButtonData(nameof(Holes), "Отметки\nВырезание", assemblyLocation, typeof(Holes).FullName)
            {
                LargeImage = GetImageSource(imgholes),
                Image = GetImageSource(imgholesmin),
                ToolTip = "Вырезать отверстия из стен и плит, заполнить отметки отверстий."
            };
            ContextualHelp holeshelp = new ContextualHelp(ContextualHelpType.Url,
                "https://portal.talan.group/knowledge/proektirovanie/samostoyatelnoemodelirovanieotverstiy/");
            buttonDataholes.SetContextualHelp(holeshelp);
            panel8.AddItem(buttonDataholes);


            // Панель "BIM Общие"

            RibbonPanel panel10 = application.CreateRibbonPanel(tabName, "BIM Общие");
            _BIMRibbonItems.Add(panel10);

            // кнопка "BIM Экспорт"

            System.Drawing.Image imgnwc = Properties.Resources.nwc32;
            System.Drawing.Image imgnwcmin = Properties.Resources.nwc16;
            PushButtonData buttonDatabim = new PushButtonData(nameof(BimExport), "BIM\nЭкспорт", assemblyLocation, typeof(BimExport).FullName)
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

            // кнопка "Т Назначение"
            PushButtonData buttonDataTParsNazn = new PushButtonData(nameof(TParsNazn), "Т\nНазначение", assemblyLocation, typeof(TParsNazn).FullName);
            panel11.AddItem(buttonDataTParsNazn);

            // кнопка "Т Определение АР"
            PushButtonData buttonDataTParsOpredAR = new PushButtonData(nameof(TParsOpredAR), "Т\nОпределение", assemblyLocation, typeof(TParsOpredAR).FullName);
            panel11.AddItem(buttonDataTParsOpredAR);

            // Панель "BIM КЖ"

            RibbonPanel panel12 = application.CreateRibbonPanel(tabName, "BIM КЖ");
            _BIMRibbonItems.Add(panel12);

            // кнопка "Коды материалов"
            PushButtonData buttonDataMat = new PushButtonData(nameof(AssignMaterialCodesCommand), "Коды\nматериалов", assemblyLocation, typeof(AssignMaterialCodesCommand).FullName);
            panel12.AddItem(buttonDataMat);

            // кнопка "Т Определение КЖ"
            PushButtonData buttonDataTParsOpredST = new PushButtonData(nameof(TParsOpredST), "Т\nОпределение", assemblyLocation, typeof(TParsOpredST).FullName);
            panel12.AddItem(buttonDataTParsOpredST);

            // кнопка "Т Наименование Обозначение КЖ"
            PushButtonData buttonDataTParsNaimOboznST = new PushButtonData(nameof(TParsNaimOboznST), "Т Наименование\nТ Обозначение", assemblyLocation, typeof(TParsNaimOboznST).FullName);
            panel12.AddItem(buttonDataTParsNaimOboznST);

            // Панель "BIM Сети"

            RibbonPanel panel13 = application.CreateRibbonPanel(tabName, "BIM Сети");
            _BIMRibbonItems.Add(panel13);

            // кнопка "Хосты изоляции"
            PushButtonData buttonDataInsulationHosts = new PushButtonData(nameof(InsulationHosts), "Хосты\nизоляции", assemblyLocation, typeof(InsulationHosts).FullName);
            panel13.AddItem(buttonDataInsulationHosts);

            // кнопка "Т Параметры ОВ ВК"
            PushButtonData buttonDataTParsOVVK = new PushButtonData(nameof(TParsSpecOVVK), "Т Параметры\nОВ ВК", assemblyLocation, typeof(TParsSpecOVVK).FullName);
            panel13.AddItem(buttonDataTParsOVVK);

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

            // Результат создания кнопок и панелей
            return Result.Succeeded;

        }

        
        public Result OnShutdown(UIControlledApplication application)
        {
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

            application.ControlledApplication.DocumentOpened -= OnDocumentOpened;
            application.ControlledApplication.DocumentSynchronizingWithCentral -= OnSyncCentralStart;
            application.ControlledApplication.DocumentSynchronizedWithCentral -= OnSyncCentralEnd;
            application.ControlledApplication.DocumentClosed -= OnDocumentClosed;
            application.Idling -= OnIdling;

            application.DialogBoxShowing -= a_DialogBoxShowing;

            return Result.Succeeded;
        }

        //Обработчики событий

        private void OnDocumentCreated(object sender, Autodesk.Revit.DB.Events.DocumentCreatedEventArgs e)
        {
#if config1
            //имя пользователя
            Application revitApp = sender as Application;
            UIApplication uiApp = new UIApplication(e.Document.Application);
            string userName = uiApp.Application.Username;
            string[] rolesFile = File.ReadAllLines("//fs-nova/Distr/0.For Admin/_TNov/roles.txt");
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
                new infowindow280("Ваше имя пользователя в Revit: " + userName + "\n" +
                "Имя должно соответствовать вашему логину в компании (пример: kadysheva.n). Измените имя в настройках Revit.").ShowDialog();

                string link = "https://portal.talan.group/knowledge/proektirovanie/startraboty/";
                string commandText = @link;
                var proc = new System.Diagnostics.Process();
                proc.StartInfo.FileName = commandText;
                proc.StartInfo.UseShellExecute = true;
                proc.Start();
            }
#endif
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
        }

        public void OnDocumentOpened(object sender, DocumentOpenedEventArgs e)
        {
            
            //раскраска
            info = BasicFileInfo.Extract(e.Document.PathName);
            if (info.IsWorkshared)
            {
                
                stopwatch = new Stopwatch();
                stopwatch.Start();
                
                
            }
            else stopwatch.Reset();
        }
        public void OnSyncCentralStart(object sender, DocumentSynchronizingWithCentralEventArgs e)
        {
            //подсветка
            if (syncOption != "Без подсветки панелей (не рекомендуется)") stopwatch.Reset();

            //задания
            Document doc = e.Document;
            Autodesk.Revit.ApplicationServices.Application app = doc.Application;

            ElementId familyNameParamId = new ElementId(-1002002); //id параметра Имя семейства

            string docName = doc.Title.ToString();
            bool taskModel = false; if (docName.Contains("Задани") || docName.Contains("задани") || docName.Contains("-ЗД") || docName.Contains("_ЗД") || docName.Contains("ЗАДАНИЕ")) taskModel = true;

            if (taskModel)
            {
                //проверка подключения к серверу
                string usagefilePath = nova.novaserver + "_TNov/usage.txt";
                bool servercheck = File.Exists(usagefilePath);

                if (servercheck)
                {
                    List<string> groupTxtList = TaskTools.GetGroupsInfo(doc);
                    
                    DateTime dateTime = DateTime.Now;
                    string date = dateTime.ToString();

                    date = dateTime.ToString().Replace(":", "-");
                    string tasksPath = nova.novaserver + "_TNov/tasks/" + date + "_" + docName + ".txt";

                    foreach (string s in groupTxtList)
                    {
                        try
                        {
                            File.AppendAllText(tasksPath, "\n" + s);
                        }
                        catch (Exception) { }
                    }

                    
                }
            }

        }

        public void OnSyncCentralEnd(object sender, DocumentSynchronizedWithCentralEventArgs e)
        {
            //журнал
            info = BasicFileInfo.Extract(e.Document.PathName);
            string docName = e.Document.Title;
            string userName = info.Username;
            string docNameUserName = "_" + userName; docName = docName.Replace(docNameUserName, "");
            docName = docName.Replace(",", "");
            DateTime dateTime = DateTime.Now;
            string date = dateTime.ToString(); date = date.Replace(",", "");
            string usagefilePath = nova.novaserver + "_TNov/synchronizes.txt";
            System.IO.File.AppendAllText(usagefilePath, "\n" + date + "," + userName + "," + docName);

            
            //подсветка
            stopwatch.Start();
            adWin.RibbonControl ribbon = adWin.ComponentManager.Ribbon;

            foreach (adWin.RibbonTab tab in ribbon.Tabs)
            {
                foreach (adWin.RibbonPanel panel in tab.Panels)
                {
                    panel.CustomPanelBackground = (SolidColorBrush)new BrushConverter().ConvertFromString("#F6F6F6");
                    panel.CustomPanelTitleBarBackground = (SolidColorBrush)new BrushConverter().ConvertFromString("#F6F6F6");
                }
            }
        }

        public void OnDocumentClosed(object sender, DocumentClosedEventArgs e)
        {
            
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
            }
        }

        public void OnIdling(object sender, IdlingEventArgs e)
        {
            if (info.IsWorkshared&&time1>0)
            {
                adWin.RibbonControl ribbon = adWin.ComponentManager.Ribbon;
                //цвета
                SolidColorBrush brush1 = new SolidColorBrush(Colors.Gold);
                SolidColorBrush brush2 = new SolidColorBrush(Colors.Firebrick);

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
            }
        }
        private void OnComboBoxCurrentChanged(object sender, EventArgs e)
        {
            ComboBoxChangeSelection();
        }

        private void ComboBoxChangeSelection()
        {
            if (_comboBox.Current != null)
            {
                string selectedMode = _comboBox.Current.ItemText;

                // Обработка выбора

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
        

    }
}

