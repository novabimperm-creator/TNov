using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.Attributes;
using System.Collections.Generic;
using System.Linq;
using System;
using Newtonsoft.Json;
using System.IO;
using TNovCommon;

namespace TNov
{


    [Transaction(TransactionMode.Manual)]
    public class FoundNumSpec : IExternalCommand
    {
                
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            string TNovClassName = "Сваи Номера для спеки"; DateTime dateTime = DateTime.Now;
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
                if (qok != null && qok == true) { Logger.TurnOffExtendedLogs(); } else Logger.Log( "Расширенные логи вкл", 2);
            }

            //параметры
            BuiltInParameter gm = BuiltInParameter.ALL_MODEL_MODEL; //параметр Группа модели
            Guid pileNumberParamGuid = new Guid("3df328ab-5e4d-4da0-9138-42f1a8bb54a7"); //N_Свая.Номер
            Guid pileGroup1ParamGuid = new Guid("dd989087-a6af-486d-986e-d83b14c2d064"); //Свая.Группа1
            Guid pileGroup2ParamGuid = new Guid("471aa5c6-a7b8-4d38-a1d7-ac20794b4920"); //Свая.Группа2


            Logger.Log("Сбор элементов",1);
            
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

            List<Pile> pbl = new List<Pile>(); //список свай-Pile
            foreach (var p in piles1)
            {
                Element elem = doc.GetElement(p.Id);
                int.TryParse(p.get_Parameter(pileNumberParamGuid).AsString(), out int num);
                double z = 0;
                z = (double)(elem.LookupParameter("Свая.ОтмНизаРостверка")?.AsDouble()); //Свая.ОтмНизаРостверка
                Pile pl = new Pile();
                pl.elemid = p.Id; pl.sort = num; pl.z = z; pl.type = elem.GetTypeId().ToString();
                pbl.Add(pl);
            }

            using (Transaction transaction = new Transaction(doc))
            {
                transaction.Start("TNov - автонумерация свай");
                Logger.Log("Открываем транзакцию",1);

                //заполняем номера свай для спецификации элементов
                Logger.Log("Заполняем номера свай для спецификации элементов", 1);

                var psorted1 = from pl in pbl //сортированный список свай-Pile по свойству type
                              orderby pl.type
                                select pl;
                var types1 = from pl in pbl //список типов
                             group pl by pl.type;
                foreach (var type in types1)
                {
                    Logger.Log("Тип " + type.First().type, 2);

                    string parameterNum1val = string.Empty;

                    List<int>nums1= new List<int>();

                    List<Pile> pilesoftype = new List<Pile>(); //список свай-Pile определенного типа
                    foreach (var pl in type)
                    {
                        pilesoftype.Add(pl);
                    }
                    var pilesoftypesorted = from pl in pilesoftype //сортированный список свай-Pile определенного типа по номеру
                                                    orderby pl.sort
                                                    select pl;
                    
                    foreach (var pl in pilesoftypesorted)
                    {
                        int plnum = (int)pl.sort;
                        nums1.Add(plnum);
                    }
                    numstostring nts = new numstostring(in nums1,out parameterNum1val);

                    foreach (var pl in pilesoftypesorted)
                    {
                        Element elem = doc.GetElement(pl.elemid);
                        try
                        {
                            elem.get_Parameter(pileGroup1ParamGuid)?.Set(parameterNum1val);
                            Logger.Log("   Элемент "+ pl.elemid.IntegerValue.ToString() + 
                                " параметр N_Свая.Группа1 заполнен: "+ parameterNum1val, 2);
                        }
                        catch (Exception ex) 
                        {
                            Logger.Log("Элемент " + pl.elemid.IntegerValue.ToString() +" ошибка: " + ex.Message, 4);
                        }
                        
                    }

                }

                //заполняем номера свай для таблицы отметок
                Logger.Log("Заполняем номера свай для таблицы отметок", 1);

                var pilessorted = from pl in pbl //сортированный список свай-Pile по z
                                  orderby pl.z
                                  select pl;
                var levels = from pl in pbl //список z
                             group pl by pl.z;

                foreach (var level in levels)
                {
                    Logger.Log("Отметка " + level.First().z, 2);

                    List<Pile> pilesatlevel = new List<Pile>(); //список свай-Pile на уровне
                    foreach (var pl in level)
                    {
                        pilesatlevel.Add(pl);
                    }
                    var psorted = from pl in pilesatlevel //сортированный список свай-Pile по свойству type на уровне
                                  orderby pl.type
                                  select pl;
                    var types = from pl in pilesatlevel //список типов на уровне
                                group pl by pl.type;
                    foreach (var type in types)
                    {
                        Logger.Log("   Тип " + type.First().type, 2);

                        string parameterNum2val = string.Empty;

                        List<int> nums = new List<int>();

                        List<Pile> pilesatleveloftype = new List<Pile>(); //список свай-Pile определенного типа на уровне
                        foreach (var pl in type)
                        {
                            pilesatleveloftype.Add(pl);
                        }
                        var pilesatleveloftypesorted = from pl in pilesatleveloftype //сортированный список свай-Pile определенного типа на уровне по номеру
                                                       orderby pl.sort
                                                       select pl;

                        foreach (var pl in pilesatleveloftypesorted)
                        {
                            int plnum = (int)pl.sort;
                            nums.Add(plnum);
                        }
                        numstostring nts = new numstostring(in nums, out parameterNum2val);

                        foreach (var pl in pilesatleveloftypesorted)
                        {
                            Element elem = doc.GetElement(pl.elemid);
                            try
                            {
                                elem.get_Parameter(pileGroup2ParamGuid)?.Set(parameterNum2val);
                                Logger.Log("      Элемент " + pl.elemid.IntegerValue.ToString() +
                                    " параметр N_Свая.Группа2 заполнен: " + parameterNum2val, 2);
                            }
                            catch (Exception ex)
                            {
                                Logger.Log("Элемент " + pl.elemid.IntegerValue.ToString() + " ошибка: " + ex.Message, 4);
                            }
                        }
                    }

                }
                
                var info1 = new InfoWindow280("Успешно!"); info1.ShowDialog();
                transaction.Commit();
                Logger.Log("Закрываем транзакцию",1);

            }
                
            Logger.Log("Завершение работы.",5);
            return Result.Succeeded;
        }
        private string Numstostring(in List<int> nums)
        {
            string str = "";
            int i = 0;
            string div = "";
            //первый этап, получаем массивы смежных чисел
            while (i < nums.Count)
            {
                if (i > 0)
                {
                    if (nums[i] - nums[i - 1] > 1)
                    {
                        div = ",";
                        str += div + nums[i].ToString();
                    }
                    else
                    {
                        div = "-";
                        str += div + nums[i].ToString();
                    }
                }
                else str += div + nums[i].ToString();
                i++;
            }

            //второй этап, убираем лишние числа в массивах чисел
            string result = "";
            string[] parts = str.Split(',');
            int i2 = 0;
            string div2 = "";
            foreach (string part in parts)
            {
                if (i2 > 0) { div2 = ","; }
                int counthyphens = 0;
                foreach (char ch in part)
                {
                    char chr = '-';
                    if (ch == chr) { counthyphens++; }
                }
                switch (counthyphens)
                {
                    case 0:
                        result += div2 + part;
                        i2++;
                        break;
                    case 1:
                        string[] partsofpart = part.Split('-');
                        result += div2 + part.Replace("-", ",");
                        i2++;
                        break;
                    default:
                        string[] partsofpart1 = part.Split('-');
                        result += div2 + partsofpart1[0] + "-" + partsofpart1[partsofpart1.Length - 1];
                        i2++;
                        break;
                }

            }
            result = result.Replace("-", " - "); result = result.Replace(",", ", ");

            return result;
        }
    }
    
}
