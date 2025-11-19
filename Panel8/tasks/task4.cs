using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Controls;
using TNov.Panel6;
using System.Windows;

namespace TNov.Panel6.tasks
{
    public class task4
    {
        public int GetHoleMaxNumber (in Document doc)
        {
            //параметры
            BuiltInParameter mrk = BuiltInParameter.ALL_MODEL_MARK; //Марка
            ElementId familyNameParamId = new ElementId(-1002002); //id параметра Имя семейства
            
            List<FamilyInstance> GMs = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_GenericModel)   //фильтр по категории Об модели
                                                                         .WhereElementIsNotElementType()
                                                                         .OfClass(typeof(FamilyInstance))
                                                                         .Cast<FamilyInstance>()
                                                                         .ToList();
            BuiltInParameter gm = BuiltInParameter.ALL_MODEL_MODEL; //параметр Группа модели

            List<FamilyInstance> holesGM = new List<FamilyInstance>();

            foreach (FamilyInstance GM in GMs) //ищем отверстия об мод
            {
                string gmvalue = GM.Symbol.get_Parameter(gm).AsString();
                if (gmvalue != null)
                {
                    if (gmvalue.Contains("Отверстие")) { holesGM.Add(GM); }
                }

            }
            int maxNumber = 0; string groupName = "-";

            if (holesGM.Count < 1) return 0;// "-";

            foreach (var holeGM in holesGM)
            {
                int elemNum = 0;
                Element elem = doc.GetElement(holeGM.Id);
                string mrkvalue = elem.get_Parameter(mrk).AsValueString();
                if (mrkvalue == null || mrkvalue == "") elemNum = 0;
                int.TryParse(mrkvalue, out elemNum);
                if (elemNum > maxNumber) 
                {
                    maxNumber = elemNum;
                    ElementId groupId = elem.GroupId; 
                    if(groupId.IntegerValue>0)
                    {
                        Element g = doc.GetElement(groupId);
                        groupName = g.Name;
                    }
                }
            }

            int res = maxNumber;//.ToString();
            //if (groupName != "-") res += " в группе " + groupName; else res += " (вне группы)";
             
            return res;
        }
    }
}
