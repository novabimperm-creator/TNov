using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.Attributes;
using System.Collections.Generic;
using System.Linq;
using System;
using TNov.main;
using Newtonsoft.Json;
using System.IO;

namespace TNov
{


    [Transaction(TransactionMode.Manual)]
    public class foundnumspec : IExternalCommand
    {
                
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            string TNovClassName = "Сваи Номера для спеки"; DateTime dateTime = DateTime.Now;
            //подключение приложения и документа
            if (RevitAPI.UiApplication == null) { RevitAPI.Initialize(commandData); }
            UIDocument uidoc = RevitAPI.UiDocument; Document doc = RevitAPI.Document;
            UIApplication uiApp = RevitAPI.UiApplication; Autodesk.Revit.ApplicationServices.Application rvtApp = uiApp.Application;
            
            //проверка подключения, запись в журнал
            bool check = false; servercheck sc = new servercheck(in TNovClassName, out check); if (check == false) { return Result.Failed; }

            // создание log - файла
            Logger.Initialize(TNovClassName);
            

            //Проверка актуальности шаблона
            templatecheck tc = new templatecheck(in commandData, out bool oldProject);

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

            //Список используемых параметров

            BuiltInParameter gm = BuiltInParameter.ALL_MODEL_MODEL; //параметр Группа модели
            string parameterName = "N_Свая.Номер"; if (oldProject == true) { parameterName = "Свая.Номер"; }
            string parameterNum1 = "N_Свая.Группа1"; if (oldProject == true) { parameterNum1 = "Свая.Группа1"; }
            string parameterNum2 = "N_Свая.Группа2"; if (oldProject == true) { parameterNum2 = "Свая.Группа2"; }

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
                new infowindow280("В проекте отсутствуют сваи.").ShowDialog();
                Logger.Log("В проекте отсутствуют сваи. Завершение работы.", 3);
                return Result.Failed;
            }

            List<Pile> pbl = new List<Pile>(); //список свай-Pile
            foreach (var p in piles1)
            {
                Element elem = doc.GetElement(p.Id);
                int.TryParse(p.LookupParameter(parameterName).AsString(), out int num);
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
                            elem.LookupParameter(parameterNum1)?.Set(parameterNum1val);
                            Logger.Log("   Элемент "+ pl.elemid.IntegerValue.ToString() + 
                                " параметр "+ parameterNum1+" заполнен: "+ parameterNum1val, 2);
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
                                elem.LookupParameter(parameterNum2)?.Set(parameterNum2val);
                                Logger.Log("      Элемент " + pl.elemid.IntegerValue.ToString() +
                                    " параметр " + parameterNum2 + " заполнен: " + parameterNum2val, 2);
                            }
                            catch (Exception ex)
                            {
                                Logger.Log("Элемент " + pl.elemid.IntegerValue.ToString() + " ошибка: " + ex.Message, 4);
                            }
                        }
                    }

                }
                
                var info1 = new infowindow280("Успешно!"); info1.ShowDialog();
                transaction.Commit();
                Logger.Log("Закрываем транзакцию",1);

            }
                
            Logger.Log("Завершение работы.",5);
            return Result.Succeeded;
        }
    }
    
}
