using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Microsoft.Office.Interop.Excel;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Controls;
using TNov.Panel6;
using System.Windows;
using TNov.main;

namespace TNov.Panel6.tasks
{
    public class task1
    {
        public string GetGroupNames (in Document linkDoc, in Document doc, in bool oldProject)
        {
            //параметры
            BuiltInParameter mrk = BuiltInParameter.ALL_MODEL_MARK; //Марка
            ElementId familyNameParamId = new ElementId(-1002002); //id параметра Имя семейства
            string groupSetParam =  "ADSK_Группирование"; 


            List<Group> linkGroups = new FilteredElementCollector(linkDoc).OfCategory(BuiltInCategory.OST_IOSModelGroups)
                .WhereElementIsNotElementType()
                .Cast<Group>()
                .ToList();

            List<Group> groups = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_IOSModelGroups)
                .WhereElementIsNotElementType()
                .Cast<Group>()
                .ToList();

            List<string> groupTxtList = new List<string>();
            //проходим по группам в связанной модели
            foreach (var linkGroup in linkGroups)
            {
                Logger.Log(linkGroup.Name,1);
                string[] nameParts = linkGroup.Name.Split('_');
                string shortName = linkGroup.Name;
                if (nameParts.Length > 2) shortName = nameParts[0] + '_' + nameParts[1] + '_' + nameParts[2]; //учет групп, созданных по старой концепции
                string status = ""; 
                //проверяем элементы в задании на заполненность Марки
                ElementFilter elementFilter = (ElementFilter)new ElementParameterFilter(ParameterFilterRuleFactory.CreateContainsRule(familyNameParamId, "pmN.Отверстие", true));
                IList<ElementId> linkGroupElems = linkGroup.GetDependentElements(elementFilter);
                ElementFilter elementFilter21 = (ElementFilter)new ElementParameterFilter(ParameterFilterRuleFactory.CreateContainsRule(familyNameParamId, "pmN.Рама под оборудование", true));
                ElementFilter elementFilter22 = (ElementFilter)new ElementParameterFilter(ParameterFilterRuleFactory.CreateContainsRule(familyNameParamId, "pmN.Задание на шахту", true));
                ElementFilter elementFilter23 = (ElementFilter)new ElementParameterFilter(ParameterFilterRuleFactory.CreateContainsRule(familyNameParamId, "pmN.Задание на приямок", true));
                List<ElementId> linkGroupElems21 = linkGroup.GetDependentElements(elementFilter21).ToList();
                List<ElementId> linkGroupElems22 = linkGroup.GetDependentElements(elementFilter22).ToList();
                List<ElementId> linkGroupElems23 = linkGroup.GetDependentElements(elementFilter23).ToList();
                List<ElementId> linkGroupElems2 = linkGroupElems21.Union(linkGroupElems22).Union(linkGroupElems23).ToList();
                string badElementIds = "";
                int k = 0;
                //отверстия
                foreach (var linkGroupElem in linkGroupElems)
                {
                    Element elem = linkDoc.GetElement(linkGroupElem); 
                    string mrkvalue = elem.get_Parameter(mrk).AsValueString();
                    if (mrkvalue == null || mrkvalue == "") badElementIds += linkGroupElem.ToString() + " ";
                    int linkElem_coordStatusST_int = elem.LookupParameter("N_Согласовано КР").AsInteger();
                    if (linkElem_coordStatusST_int != 1) k++;
                    int linkElem_coordStatusBIM_int = elem.LookupParameter("N_Согласовано BIM").AsInteger();
                    if (linkElem_coordStatusBIM_int != 1) k++;
                }
                //прочие задания
                foreach (var linkGroupElem in linkGroupElems2)
                {
                    Element elem = linkDoc.GetElement(linkGroupElem);
                    string mrkvalue = elem.get_Parameter(mrk).AsValueString();
                    if (mrkvalue == null || mrkvalue == "") badElementIds += linkGroupElem.ToString() + " ";
                    int linkElem_coordStatusST_int = elem.LookupParameter("N_Согласовано КР").AsInteger();
                    if (linkElem_coordStatusST_int != 1) k++;
                    int linkElem_coordStatusBIM_int = elem.LookupParameter("N_Согласовано BIM").AsInteger();
                    if (linkElem_coordStatusBIM_int != 1) k++;
                }
                if (badElementIds.Length > 0) Logger.Log("Элементы с незаполненной Маркой: " + badElementIds,1);

                //adsk группирование
                string gSet = "Не заполнено";
                bool setExist = param.ParamExist(groupSetParam, linkGroup);
                if (setExist)
                {
                    if (linkGroup.LookupParameter(groupSetParam).HasValue)
                    {
                        gSet = linkGroup.LookupParameter(groupSetParam).AsString();
                    }
                }
                
                //ищем задание в текущей модели
                int i = 0; int j = 0;
                if (groups == null || groups.Count == 0) 
                {
                    status = "Задание еще не вставлялось.";
                    if (badElementIds.Length > 0) status += " Не у всех элементов в задании заполнена позиция (Марка).";
                    if (k > 0) status += " Не все элементы согласованы КР или BIM.";
                    string groupText0 = shortName + "=" + status+"="+ gSet;
                    groupTxtList.Add(groupText0); Logger.Log(groupText0, 1);
                    continue; 
                }
                else
                {
                    foreach (var group in groups)
                    {
                        status = "";
                        string[] nameParts1 = group.Name.Split('_');
                        string shortName1 = group.Name;
                        if (nameParts1.Length > 2) shortName1 = nameParts1[0] + '_' + nameParts1[1] + '_' + nameParts1[2]; //учет групп, созданных по старой концепции
                        if (nameParts.Length < 3) status = "Некорректное имя группы в модели задания.";
                        if (shortName1 == shortName)
                        {
                            i++;
                            //углубленный анализ
                            task2 task2 = new task2();
                            List<Hole> holes = task2.HolesInGroup(linkDoc, doc, shortName, oldProject);

                            foreach (var hole in holes)
                            {
                                if (hole.status.Length > 0) j++;
                            }
                            if (status == "" || status == "Некорректное имя группы в модели задания.") 
                            { if( j > 0) status += "Есть проблемы - см. детальный анализ. "; }
                            if (status.Length > 0) Logger.Log("      статус " + status,2);
                            break;
                        }
                    }
                    if (i == 0) status = "Задание еще не вставлялось.";
                    if (badElementIds.Length > 0) status += " Не у всех элементов в задании заполнена позиция (Марка).";
                    if (k > 0) status += " Не все элементы согласованы КР или BIM.";
                    if (status == "") status = "Актуально.";
                    string groupText = shortName + "=" + status+"="+ gSet;
                    groupTxtList.Add(groupText); Logger.Log(groupText, 1);

                }


            }
            string groups1 = "";
            foreach (string group in groupTxtList) groups1 += group + "|";

            return groups1;
        }
    }
}
