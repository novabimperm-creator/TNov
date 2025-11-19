using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using TNov.main;

namespace TNov
{
    public class task3
    {
        public List<string> GetGroupsInfo (in Document doc)
        {
            //параметры
            BuiltInParameter mrk = BuiltInParameter.ALL_MODEL_MARK; //Марка
            ElementId familyNameParamId = new ElementId(-1002002); //id параметра Имя семейства
                        
            List<Group> groups = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_IOSModelGroups)
                .WhereElementIsNotElementType()
                .Cast<Group>()
                .ToList();

            List<string> groupTxtList = new List<string>();
            //проходим по группам в модели
            foreach (var group in groups)
            {
                Logger.Log(group.Name,1);
                string[] nameParts = group.Name.Split('_');
                string shortName = group.Name;
                if (nameParts.Length > 2) shortName = nameParts[0] + '_' + nameParts[1] + '_' + nameParts[2]; //учет групп, созданных по старой концепции
                string status = string.Empty;
                if (nameParts.Length < 3) status = "Некорректное имя группы в модели задания.";
                //проверяем отверстия в задании на заполненность Марки
                ElementFilter elementFilter = (ElementFilter)new ElementParameterFilter(ParameterFilterRuleFactory.CreateContainsRule(familyNameParamId, "pmN.Отверстие", true));
                IList<ElementId> groupElems = group.GetDependentElements(elementFilter);
                //string ids = ""; foreach(ElementId elementId in linkGroupElems) ids += elementId.ToString()+", ";
                //Logger.Log(ids);
                
                List<Hole>holes0 = new List<Hole>();

                groupTxtList.Add(group.Name);
                foreach (var groupElem in groupElems)
                {
                    Element elem = doc.GetElement(groupElem);
                    string mrkvalue = elem.get_Parameter(mrk).AsValueString();
                    if (mrkvalue == null || mrkvalue == "") mrkvalue = "0";
                    int elem_coordStatusST_int = elem.LookupParameter("N_Согласовано КР").AsInteger();
                    string STstatus = "- (КР)"; if (elem_coordStatusST_int > 0) STstatus = "согласовано КР";
                    int elem_coordStatusHead_int = elem.LookupParameter("N_Согласовано рук").AsInteger();
                    string Headstatus = "- (рук)"; if (elem_coordStatusST_int > 0) Headstatus = "согласовано рук";
                    int elem_coordStatusBIM_int = elem.LookupParameter("N_Согласовано BIM").AsInteger();
                    string BIMstatus = "- (BIM)"; if (elem_coordStatusST_int > 0) BIMstatus = "согласовано BIM";
                    string widthParam = "ADSK_Отверстие_Ширина";
                    string heightParam = "ADSK_Отверстие_Высота";

                    string holeType = "Прямоугольное"; 
                    foreach (Parameter param in elem.ParametersMap) //круглые отв
                    {
                        string paramName = param.Definition.Name;
                        if (paramName == "ADSK_Размер_Диаметр")
                        {
                            holeType = "Круглое"; 
                            widthParam = "ADSK_Размер_Диаметр";
                            heightParam = "ADSK_Размер_Диаметр";
                            break;
                        }
                    }
                    double elem_width = elem.LookupParameter(widthParam).AsDouble() * 0.3048 * 1000;
                    double elem_height = elem.LookupParameter(heightParam).AsDouble() * 0.3048 * 1000;
                    LocationPoint elem_lp = (LocationPoint)elem.Location;
                    XYZ p = elem_lp.Point;

                    double elem_x = p.X * 0.3048; double elem_y = p.Y * 0.3048; double elem_z = p.Z * 0.3048;

                    Hole hole = new Hole()
                    {
                        pasted = true,
                        mark = mrkvalue,
                        mark1 = mrkvalue,
                        status = holeType+", статус: ",
                        coordStatusBIM = BIMstatus,
                        coordStatusHead = Headstatus,
                        coordStatusST = STstatus,
                        width = elem_width,
                        height = elem_height,
                        x = elem_x, y = elem_y, z = elem_z,
                        id1 = elem.Id.IntegerValue
                    };
                    holes0.Add(hole);
                    
                    
                }
                
                List<Hole> holes1 = holes0.OrderBy(h => h.mark.Length)
                        .ThenBy(h => h.mark)
                        .ToList();

                foreach (var hole in holes1) groupTxtList.Add(hole.mark + "__id: " + hole.id1 +"__" + hole.width + "x" + hole.height + "__" + hole.status +
                    hole.coordStatusHead + ", " + hole.coordStatusBIM + ", " + hole.coordStatusST + "__" + "местоположение: " +
                    hole.x.ToString() + " " + hole.y.ToString() + " " + hole.z.ToString() + " ");
            }
             
            return groupTxtList;
        }
    }
}