using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using TNov.main;
using static System.Windows.Forms.VisualStyles.VisualStyleElement;

namespace TNov.Panel6.tasks
{
    public class task2
    {
        public static bool CompareWithTolerance(double a, double b)
        {
            return Math.Abs(a - b) <= 0.02;
        }
        public List<Hole> HolesInGroup (in Document linkDoc, in Document doc, in string groupName, in bool oldProject)
        {
            List<Hole> holes = new List<Hole>(); //универсальный класс для отверстий и других заданий

            //параметры
            BuiltInParameter mrk = BuiltInParameter.ALL_MODEL_MARK; //Марка
            BuiltInParameter gm = BuiltInParameter.ALL_MODEL_MODEL; //параметр Группа модели
            ElementId familyNameParamId = new ElementId(-1002002); //id параметра Имя семейства

            string widthParam = "A_Отверстие_Ширина";
            if (oldProject == true) { widthParam = "ADSK_Отверстие_Ширина"; }
            string heightParam = "A_Отверстие_Высота";
            if (oldProject == true) { heightParam = "ADSK_Отверстие_Высота"; }
            string diamParam = "A_Размер_Диаметр";
            if (oldProject == true) { diamParam = "ADSK_Размер_Диаметр"; }
            string shaftWidthParam = "A_Размер_Ширина";
            if (oldProject == true) { shaftWidthParam = "ADSK_Размер_Ширина"; }
            string shaftHeightParam = "A_Размер_Высота";
            if (oldProject == true) { shaftHeightParam = "ADSK_Размер_Высота"; }
            string shaftLengthParam = "A_Размер_Длина";
            if (oldProject == true) { shaftLengthParam = "ADSK_Размер_Длина"; }

            List<RevitLinkInstance> links = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_RvtLinks)
            .WhereElementIsNotElementType()
            .Cast<RevitLinkInstance>()
            .ToList();
            List<RevitLinkInstance> taskLinks = new List<RevitLinkInstance>();
            foreach (RevitLinkInstance link in links) if (link.Name.Contains("Задани") || link.Name.Contains("задани") || link.Name.Contains("-ЗД") || link.Name.Contains("_ЗД") || link.Name.Contains("ЗАДАНИЕ")) taskLinks.Add(link);

            List<Group> linkGroups = new FilteredElementCollector(linkDoc).OfCategory(BuiltInCategory.OST_IOSModelGroups)
                .WhereElementIsNotElementType()
                .Cast<Group>()
                .ToList();

            List<Group> groups = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_IOSModelGroups)
                .WhereElementIsNotElementType()
            .Cast<Group>()
            .ToList();

            Logger.Log("   Детальный анализ:",2);
            //проходим по группам в связанной модели
            foreach (var linkGroup in linkGroups)
            {
                string[] nameParts = linkGroup.Name.Split('_');
                string shortName = linkGroup.Name;
                
                if (nameParts.Length > 2) shortName = nameParts[0] + '_' + nameParts[1] + '_' + nameParts[2];
                if (shortName == groupName)
                {
                    Logger.Log("Группа для обработки: " + shortName, 2);
                    //отверстия группы в связанной модели
                    ElementFilter elementFilter = (ElementFilter)new ElementParameterFilter(ParameterFilterRuleFactory.CreateContainsRule(familyNameParamId, "pmN.Отверстие", true));
                    IList<ElementId> linkGroupElems = linkGroup.GetDependentElements(elementFilter);
                    //прочие задания группы в связанной модели
                    ElementFilter elementFilter21 = (ElementFilter)new ElementParameterFilter(ParameterFilterRuleFactory.CreateContainsRule(familyNameParamId, "pmN.Рама под оборудование", true));
                    ElementFilter elementFilter22 = (ElementFilter)new ElementParameterFilter(ParameterFilterRuleFactory.CreateContainsRule(familyNameParamId, "pmN.Задание на шахту", true));
                    ElementFilter elementFilter23 = (ElementFilter)new ElementParameterFilter(ParameterFilterRuleFactory.CreateContainsRule(familyNameParamId, "pmN.Задание на приямок", true));
                    List<ElementId> linkGroupElems21 = linkGroup.GetDependentElements(elementFilter21).ToList();
                    List<ElementId> linkGroupElems22 = linkGroup.GetDependentElements(elementFilter22).ToList();
                    List<ElementId> linkGroupElems23 = linkGroup.GetDependentElements(elementFilter23).ToList();
                    List<ElementId> linkGroupElems2 = linkGroupElems21.Union(linkGroupElems22).Union(linkGroupElems23).ToList();

                    if (groups==null||groups.Count == 0) continue;
                    //ищем задание в текущей модели
                    foreach (var group in groups)
                    {
                        string[] nameParts1 = group.Name.Split('_');
                        string shortName1 = group.Name;
                        if (nameParts1.Length > 2) shortName1 = nameParts1[0] + '_' + nameParts1[1] + '_' + nameParts1[2];
                        if (shortName1 == shortName)
                        {
                            Logger.Log("Группа "+shortName+" найдена в текущей модели", 2);
                            //элементы группы в текущей модели
                            IList<ElementId> groupElems = group.GetDependentElements(elementFilter);
                            List<ElementId> groupElemsToDelete = groupElems.ToList();
                            List<ElementId> groupElems21 = group.GetDependentElements(elementFilter21).ToList();
                            List<ElementId> groupElems22 = group.GetDependentElements(elementFilter22).ToList();
                            List<ElementId> groupElemsToDelete2 = groupElems21.Union(groupElems22).ToList();
                            List<ElementId> groupElems2 = groupElems21.Union(groupElems22).ToList();


                            //отверстия
                            foreach (ElementId linkGroupElem in linkGroupElems)
                            {
                                Element linkElem = linkDoc.GetElement(linkGroupElem);
                                string linkElem_mark = linkElem.get_Parameter(mrk).AsValueString();
                                if (linkElem_mark == null || linkElem_mark.Length == 0) { continue; } //пропускаем отверстия с незаполненной Маркой
                                else
                                {
                                    Logger.Log("Отверстие " + linkElem_mark + ":", 2);
                                    string linkElem_status = "";
                                    bool linkElem_pasted = false;

                                    string widthParam0 =  "ADSK_Отверстие_Ширина"; 
                                    string widthParam1 = widthParam;
                                    string heightParam0 =  "ADSK_Отверстие_Высота"; 
                                    string heightParam1 = heightParam;

                                    bool circleHole = false; string widthParamName = "Ширина: "; string heightParamName = "Высота: ";
                                    foreach (Parameter param in linkElem.ParametersMap) //круглые отв
                                    {
                                        string paramName = param.Definition.Name;
                                        if (paramName == "ADSK_Размер_Диаметр")
                                        {
                                            circleHole = true; widthParamName = "Диаметр: "; heightParamName = "Диаметр: ";
                                            widthParam0 =  "ADSK_Размер_Диаметр"; 
                                            widthParam1 = diamParam;
                                            heightParam0 = "ADSK_Размер_Диаметр"; 
                                            heightParam1 = diamParam;
                                            Logger.Log("   круглое", 2);
                                            break;
                                        }
                                    }
                                    double linkElem_width = linkElem.LookupParameter(widthParam0).AsDouble() * 0.3048 * 1000;
                                    double linkElem_height = linkElem.LookupParameter(heightParam0).AsDouble() * 0.3048 * 1000;
                                    Logger.Log("   " + widthParamName + linkElem_width.ToString(), 2);
                                    Logger.Log("   " + heightParamName+ linkElem_width.ToString(), 2);

                                    string linkElem_coordStatusHead = "-";
                                    int linkElem_coordStatusHead_int = linkElem.LookupParameter("N_Согласовано рук").AsInteger();
                                    if (linkElem_coordStatusHead_int == 1)
                                    {
                                        linkElem_coordStatusHead = "v";
                                        Logger.Log("   согласовано руководителем", 2);
                                    }

                                    string linkElem_coordStatusBIM = "-";
                                    int linkElem_coordStatusBIM_int = linkElem.LookupParameter("N_Согласовано BIM").AsInteger();
                                    if (linkElem_coordStatusBIM_int == 1)
                                    {
                                        linkElem_coordStatusBIM = "v";
                                        Logger.Log("   согласовано BIM", 2);
                                    }

                                    string linkElem_coordStatusST = "-";
                                    int linkElem_coordStatusST_int = linkElem.LookupParameter("N_Согласовано КР").AsInteger();
                                    if (linkElem_coordStatusST_int == 1)
                                    {
                                        linkElem_coordStatusST = "v";
                                        Logger.Log("   согласовано КР", 2);
                                    }
                                    else linkElem_status += "Не согласовано КР. ";

                                    LocationPoint linkElem_lp = (LocationPoint)linkElem.Location;
                                    XYZ p = linkElem_lp.Point; 
                                    
                                    foreach (var link in taskLinks)
                                    {
                                        var transform = link.GetTransform(); p = transform.OfPoint(p); break;
                                    }
                                    
                                    double linkElem_x = p.X * 0.3048; double linkElem_y = p.Y * 0.3048; double linkElem_z = p.Z * 0.3048;
                                    linkElem_x = Math.Round(linkElem_x, 3); linkElem_y = Math.Round(linkElem_y, 3); linkElem_z = Math.Round(linkElem_z, 3);
                                    Logger.Log("   Х: " + linkElem_x.ToString() + " Y: " + linkElem_y.ToString() + " Z: " + linkElem_z.ToString(), 2);

                                    int id1 = 0;

                                    foreach (ElementId groupElem in groupElems)
                                    {
                                        Element elem = doc.GetElement(groupElem);
                                        string elem_mark = elem.get_Parameter(mrk).AsValueString();
                                        if (elem_mark == linkElem_mark) //нашли отверстие с той же маркой
                                        {
                                            linkElem_pasted = true;
                                            id1 = groupElem.IntegerValue;
                                            Logger.Log("   найдено в текущей модели", 2);

                                            int d = 0;
                                            foreach (Parameter param in elem.ParametersMap) //круглые отв
                                            {
                                                
                                                string paramName = param.Definition.Name;
                                                if (paramName == diamParam) 
                                                {
                                                    d++;
                                                    Logger.Log("   круглое", 2);
                                                    break;
                                                }
                                            }
                                            if (circleHole && d == 0) //в Задании - круглое, а в КЖ - нет
                                            {
                                                linkElem_status += "Отверстие в задании изменено на круглое. ";
                                                break;
                                            }
                                            if(!circleHole && d > 0) //в Задании - прямоугольное, а в КЖ - круглое
                                            {
                                                linkElem_status += "Отверстие в задании изменено на прямоугольное. ";
                                                break;
                                            }

                                            double elem_width = elem.LookupParameter(widthParam1).AsDouble() * 0.3048 * 1000;
                                            if (CompareWithTolerance(elem_width,linkElem_width)==false) linkElem_status += widthParamName + elem_width.ToString() + ". ";

                                            Logger.Log("      "+widthParam1+": "+ elem_width.ToString()+"; исходное: "+ linkElem_width.ToString(), 2);

                                            if (!circleHole)
                                            {
                                                double elem_height = elem.LookupParameter(heightParam1).AsDouble() * 0.3048 * 1000;
                                                if (CompareWithTolerance(elem_height, linkElem_height) == false) linkElem_status += "Высота: " + elem_height.ToString() + ". ";
                                                Logger.Log("      " + heightParam1 + ": " + elem_height.ToString() + "; исходное: " + linkElem_height.ToString(), 2);

                                            }

                                            LocationPoint elem_lp = (LocationPoint)elem.Location;
                                            XYZ point = elem_lp.Point; //XYZ point1 = transform.OfPoint(point);
                                            double elem_x = point.X * 0.3048; double elem_y = point.Y * 0.3048; double elem_z = point.Z * 0.3048;
                                            elem_x = Math.Round(elem_x, 3); elem_y = Math.Round(elem_y, 3); elem_z = Math.Round(elem_z, 3);
                                            if (CompareWithTolerance(linkElem_x, elem_x) == false) linkElem_status += "X: " + elem_x.ToString() + ". ";
                                            if (CompareWithTolerance(linkElem_y, elem_y) == false) linkElem_status += "Y: " + elem_y.ToString() + ". ";
                                            if (CompareWithTolerance(linkElem_z, elem_z) == false) linkElem_status += "Z: " + elem_z.ToString() + ". ";

                                            Logger.Log("      " + "X: " + elem_x.ToString() + "; Y: " + elem_y.ToString()+ "; Z: " + 
                                                elem_z.ToString()+ "; исходное: " + "X: " + linkElem_x.ToString() + "; Y: " + linkElem_y.ToString() + "; Z: " + linkElem_z.ToString(), 2);


                                            Logger.Log("   удаляем из списка на удаление", 2);
                                            groupElemsToDelete.Remove(groupElem);
                                            break;
                                        }
                                    }
                                    if (!linkElem_pasted) //если отверстие не найдено в группе
                                    {
                                        int holesOutOfGroupCount = 0; 

                                        List<FamilyInstance> GMs = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_GenericModel)   //фильтр по категории Об модели
                                                                         .WhereElementIsNotElementType()
                                                                         .OfClass(typeof(FamilyInstance))
                                                                         .Cast<FamilyInstance>()
                                                                         .ToList();
                                        List<FamilyInstance> holesGM = new List<FamilyInstance>();

                                        foreach (FamilyInstance GM in GMs)
                                        {
                                            string gmvalue = GM.Symbol.get_Parameter(gm).AsString();
                                            if (gmvalue != null)
                                            {
                                                if (gmvalue.Contains("Отверстие")) holesGM.Add(GM);
                                            }
                                        }
                                        foreach (FamilyInstance hole1 in holesGM) 
                                        {
                                            Element elem = (Element)hole1;
                                            string elem_mark = elem.get_Parameter(mrk).AsValueString();
                                            if (elem_mark != null)
                                            {
                                                if(elem_mark== linkElem_mark) 
                                                {
                                                    linkElem_pasted = true;
                                                    id1 = elem.Id.IntegerValue;
                                                    Logger.Log("   найдено в текущей модели", 2);

                                                    double elem_width = elem.LookupParameter(widthParam1).AsDouble() * 0.3048 * 1000;
                                                    if (CompareWithTolerance(elem_width, linkElem_width) == false) linkElem_status += widthParamName + elem_width.ToString() + ". ";

                                                    if (!circleHole)
                                                    {
                                                        double elem_height = elem.LookupParameter(heightParam1).AsDouble() * 0.3048 * 1000;
                                                        if (CompareWithTolerance(elem_height, linkElem_height) == false) linkElem_status += "Высота: " + elem_height.ToString() + ". ";
                                                    }

                                                    LocationPoint elem_lp = (LocationPoint)elem.Location;
                                                    XYZ point = elem_lp.Point;
                                                    
                                                    foreach (var link in taskLinks)
                                                    {
                                                        var transform = link.GetTransform(); point = transform.OfPoint(point); break;
                                                    }
                                                    
                                                    double elem_x = point.X * 0.3048; double elem_y = point.Y * 0.3048; double elem_z = point.Z * 0.3048;
                                                    elem_x = Math.Round(elem_x, 3); elem_y = Math.Round(elem_y, 3); elem_z = Math.Round(elem_z, 3);
                                                    if (CompareWithTolerance(linkElem_x, elem_x) == false) linkElem_status += "X: " + elem_x.ToString() + ". ";
                                                    if (CompareWithTolerance(linkElem_y, elem_y) == false) linkElem_status += "Y: " + elem_y.ToString() + ". ";
                                                    if (CompareWithTolerance(linkElem_z, elem_z) == false) linkElem_status += "Z: " + elem_z.ToString() + ". ";

                                                    Logger.Log("   удаляем из списка на удаление", 2);
                                                    groupElemsToDelete.Remove(elem.Id);

                                                    linkElem_status += "Вставлено вне группы. ";

                                                    holesOutOfGroupCount++; break;
                                                }
                                            }
                                        }

                                        if (holesOutOfGroupCount == 0) linkElem_status += "Не вставлено. "; 
                                        
                                        Logger.Log("   Статус: " + linkElem_status, 2); 
                                    }
                                    

                                    Logger.Log("   создаем Hole", 2);
                                    //Hole
                                    Hole hole = new Hole()
                                    {
                                        pasted = linkElem_pasted,
                                        mark = linkElem_mark,
                                        mark1 = linkElem_mark,
                                        length = 0,
                                        width = linkElem_width,
                                        height = linkElem_height,
                                        coordStatusHead = linkElem_coordStatusHead,
                                        coordStatusBIM = linkElem_coordStatusBIM,
                                        coordStatusST = linkElem_coordStatusST,
                                        x = linkElem_x,
                                        y = linkElem_y,
                                        z = linkElem_z,
                                        status = linkElem_status,
                                        id1 = id1,
                                    };
                                    holes.Add(hole);

                                }


                            }
                            //другие задания
                            foreach (ElementId linkGroupElem in linkGroupElems2)
                            {
                                Element linkElem = linkDoc.GetElement(linkGroupElem);
                                string linkElem_mark = linkElem.get_Parameter(mrk).AsValueString();
                                if (linkElem_mark == null || linkElem_mark.Length == 0) { continue; } //пропускаем отверстия с незаполненной Маркой
                                else
                                {
                                    Logger.Log("Задание " + linkElem_mark + ":", 2);
                                    string linkElem_status = "";
                                    bool linkElem_pasted = false;

                                    string lengthParam0 = "ADSK_Размер_Длина";
                                    string lengthParam1 = shaftLengthParam;
                                    string widthParam0 = "ADSK_Размер_Ширина";
                                    string widthParam1 = shaftWidthParam;
                                    string heightParam0 = "ADSK_Размер_Высота";
                                    string heightParam1 = shaftHeightParam;

                                    string lengthParamName = "Длина: "; string widthParamName = "Ширина: "; string heightParamName = "Высота: ";

                                    double linkElem_length = linkElem.LookupParameter(lengthParam0).AsDouble() * 0.3048 * 1000;
                                    double linkElem_width = linkElem.LookupParameter(widthParam0).AsDouble() * 0.3048 * 1000;
                                    double linkElem_height = linkElem.LookupParameter(heightParam0).AsDouble() * 0.3048 * 1000;
                                    Logger.Log("   " + lengthParamName + linkElem_length.ToString(), 2);
                                    Logger.Log("   " + widthParamName + linkElem_width.ToString(), 2);
                                    Logger.Log("   " + heightParamName + linkElem_width.ToString(), 2);

                                    string linkElem_coordStatusHead = "-";
                                    int linkElem_coordStatusHead_int = linkElem.LookupParameter("N_Согласовано рук").AsInteger();
                                    if (linkElem_coordStatusHead_int == 1)
                                    {
                                        linkElem_coordStatusHead = "v";
                                        Logger.Log("   согласовано руководителем", 2);
                                    }

                                    string linkElem_coordStatusBIM = "-";
                                    int linkElem_coordStatusBIM_int = linkElem.LookupParameter("N_Согласовано BIM").AsInteger();
                                    if (linkElem_coordStatusBIM_int == 1)
                                    {
                                        linkElem_coordStatusBIM = "v";
                                        Logger.Log("   согласовано BIM", 2);
                                    }

                                    string linkElem_coordStatusST = "-";
                                    int linkElem_coordStatusST_int = linkElem.LookupParameter("N_Согласовано КР").AsInteger();
                                    if (linkElem_coordStatusST_int == 1)
                                    {
                                        linkElem_coordStatusST = "v";
                                        Logger.Log("   согласовано КР", 2);
                                    }
                                    else linkElem_status += "Не согласовано КР. ";

                                    LocationPoint linkElem_lp = (LocationPoint)linkElem.Location;
                                    XYZ p = linkElem_lp.Point;

                                    foreach (var link in taskLinks)
                                    {
                                        var transform = link.GetTransform(); p = transform.OfPoint(p); break;
                                    }

                                    double linkElem_x = p.X * 0.3048; double linkElem_y = p.Y * 0.3048; double linkElem_z = p.Z * 0.3048;
                                    linkElem_x = Math.Round(linkElem_x, 3); linkElem_y = Math.Round(linkElem_y, 3); linkElem_z = Math.Round(linkElem_z, 3);
                                    Logger.Log("   Х: " + linkElem_x.ToString() + " Y: " + linkElem_y.ToString() + " Z: " + linkElem_z.ToString(), 2);

                                    int id1 = 0;

                                    foreach (ElementId groupElem in groupElems2)
                                    {
                                        Element elem = doc.GetElement(groupElem);
                                        string elem_mark = elem.get_Parameter(mrk).AsValueString();
                                        if (elem_mark == linkElem_mark) //нашли задание с той же маркой
                                        {
                                            linkElem_pasted = true;
                                            id1 = groupElem.IntegerValue;
                                            Logger.Log("   найдено в текущей модели", 2);

                                            double elem_length = elem.LookupParameter(lengthParam1).AsDouble() * 0.3048 * 1000;
                                            if (CompareWithTolerance(elem_length, linkElem_length) == false) linkElem_status += lengthParamName + elem_length.ToString() + ". ";
                                            Logger.Log("      " + lengthParam1 + ": " + elem_length.ToString() + "; исходное: " + linkElem_length.ToString(), 2);

                                            double elem_width = elem.LookupParameter(widthParam1).AsDouble() * 0.3048 * 1000;
                                            if (CompareWithTolerance(elem_width, linkElem_width) == false) linkElem_status += widthParamName + elem_width.ToString() + ". ";
                                            Logger.Log("      " + widthParam1 + ": " + elem_width.ToString() + "; исходное: " + linkElem_width.ToString(), 2);

                                            double elem_height = elem.LookupParameter(heightParam1).AsDouble() * 0.3048 * 1000;
                                            if (CompareWithTolerance(elem_height, linkElem_height) == false) linkElem_status += "Высота: " + elem_height.ToString() + ". ";
                                            Logger.Log("      " + heightParam1 + ": " + elem_height.ToString() + "; исходное: " + linkElem_height.ToString(), 2);

                                            

                                            LocationPoint elem_lp = (LocationPoint)elem.Location;
                                            XYZ point = elem_lp.Point; //XYZ point1 = transform.OfPoint(point);
                                            double elem_x = point.X * 0.3048; double elem_y = point.Y * 0.3048; double elem_z = point.Z * 0.3048;
                                            elem_x = Math.Round(elem_x, 3); elem_y = Math.Round(elem_y, 3); elem_z = Math.Round(elem_z, 3);
                                            if (CompareWithTolerance(linkElem_x, elem_x) == false) linkElem_status += "X: " + elem_x.ToString() + ". ";
                                            if (CompareWithTolerance(linkElem_y, elem_y) == false) linkElem_status += "Y: " + elem_y.ToString() + ". ";
                                            if (CompareWithTolerance(linkElem_z, elem_z) == false) linkElem_status += "Z: " + elem_z.ToString() + ". ";

                                            Logger.Log("      " + "X: " + elem_x.ToString() + "; Y: " + elem_y.ToString() + "; Z: " + elem_z.ToString() + "; исходное: " + 
                                                "X: " + linkElem_x.ToString() + "; Y: " + linkElem_y.ToString() + "; Z: " + linkElem_z.ToString(), 2);


                                            Logger.Log("   удаляем из списка на удаление",2);
                                            groupElemsToDelete2.Remove(groupElem);
                                            break;
                                        }
                                    }
                                    if (!linkElem_pasted) //если задание не найдено в группе
                                    {
                                        int holesOutOfGroupCount = 0;

                                        List<FamilyInstance> GMs = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_GenericModel)   //фильтр по категории Об модели
                                                                         .WhereElementIsNotElementType()
                                                                         .OfClass(typeof(FamilyInstance))
                                                                         .Cast<FamilyInstance>()
                                                                         .ToList();
                                        List<FamilyInstance> holesGM = new List<FamilyInstance>();

                                        foreach (FamilyInstance GM in GMs)
                                        {
                                            string gmvalue = GM.Symbol.get_Parameter(gm).AsString();
                                            if (gmvalue != null)
                                            {
                                                if (gmvalue.Contains("Рама под оборудование")) holesGM.Add(GM);
                                            }
                                        }
                                        foreach (FamilyInstance hole1 in holesGM)
                                        {
                                            Element elem = (Element)hole1;
                                            string elem_mark = elem.get_Parameter(mrk).AsValueString();
                                            if (elem_mark != null)
                                            {
                                                if (elem_mark == linkElem_mark)
                                                {
                                                    linkElem_pasted = true;
                                                    id1 = elem.Id.IntegerValue;
                                                    Logger.Log("   найдено в текущей модели", 2);

                                                    double elem_length = elem.LookupParameter(lengthParam1).AsDouble() * 0.3048 * 1000;
                                                    if (CompareWithTolerance(elem_length, linkElem_length) == false) linkElem_status += lengthParamName + elem_length.ToString() + ". ";
                                                    
                                                    double elem_width = elem.LookupParameter(widthParam1).AsDouble() * 0.3048 * 1000;
                                                    if (CompareWithTolerance(elem_width, linkElem_width) == false) linkElem_status += widthParamName + elem_width.ToString() + ". ";

                                                    double elem_height = elem.LookupParameter(heightParam1).AsDouble() * 0.3048 * 1000;
                                                    if (CompareWithTolerance(elem_height, linkElem_height) == false) linkElem_status += "Высота: " + elem_height.ToString() + ". ";
                                                    
                                                    LocationPoint elem_lp = (LocationPoint)elem.Location;
                                                    XYZ point = elem_lp.Point;

                                                    foreach (var link in taskLinks)
                                                    {
                                                        var transform = link.GetTransform(); point = transform.OfPoint(point); break;
                                                    }

                                                    double elem_x = point.X * 0.3048; double elem_y = point.Y * 0.3048; double elem_z = point.Z * 0.3048;
                                                    elem_x = Math.Round(elem_x, 3); elem_y = Math.Round(elem_y, 3); elem_z = Math.Round(elem_z, 3);
                                                    if (CompareWithTolerance(linkElem_x, elem_x) == false) linkElem_status += "X: " + elem_x.ToString() + ". ";
                                                    if (CompareWithTolerance(linkElem_y, elem_y) == false) linkElem_status += "Y: " + elem_y.ToString() + ". ";
                                                    if (CompareWithTolerance(linkElem_z, elem_z) == false) linkElem_status += "Z: " + elem_z.ToString() + ". ";

                                                    Logger.Log("   удаляем из списка на удаление", 2);
                                                    groupElemsToDelete2.Remove(elem.Id);

                                                    linkElem_status += "Вставлено вне группы. ";

                                                    holesOutOfGroupCount++; break;
                                                }
                                            }
                                        }

                                        if (holesOutOfGroupCount == 0) linkElem_status += "Не вставлено. ";

                                        Logger.Log("   Статус: " + linkElem_status, 2);
                                    }


                                    Logger.Log("   создаем Hole", 2);
                                    //Hole
                                    Hole hole = new Hole()
                                    {
                                        pasted = linkElem_pasted,
                                        mark = linkElem_mark,
                                        mark1 = linkElem_mark,
                                        length = linkElem_length,
                                        width = linkElem_width,
                                        height = linkElem_height,
                                        coordStatusHead = linkElem_coordStatusHead,
                                        coordStatusBIM = linkElem_coordStatusBIM,
                                        coordStatusST = linkElem_coordStatusST,
                                        x = linkElem_x,
                                        y = linkElem_y,
                                        z = linkElem_z,
                                        status = linkElem_status,
                                        id1 = id1,
                                    };
                                    holes.Add(hole);

                                }


                            }

                            //список Hole в текущей модели, отсутствующих в связанной
                            //отверстия
                            if (groupElemsToDelete.Count > 0)
                            {
                                Logger.Log("Обрабатываем лишние отверстия:", 2);
                                foreach (var groupElem in groupElemsToDelete)
                                {
                                    Element elem = doc.GetElement(groupElem);
                                    string elem_mark = elem.get_Parameter(mrk).AsValueString();
                                    if (elem_mark == null || elem_mark.Length == 0) elem_mark = "-";
                                    Logger.Log("отверстие " + elem_mark + " id: " + elem.Id.ToString(), 2);

                                    string widthParam1 = widthParam;
                                    string heightParam1 = heightParam;

                                    foreach (Parameter param in elem.ParametersMap) //круглые отв
                                    {
                                        string paramName = param.Definition.Name;
                                        if (paramName == diamParam)
                                        {
                                            widthParam1 = diamParam;
                                            heightParam1 = diamParam;
                                            break;
                                        }
                                    }
                                    double elem_width = elem.LookupParameter(widthParam1).AsDouble() * 0.3048 * 1000;
                                    double elem_height = elem.LookupParameter(widthParam1).AsDouble() * 0.3048 * 1000;

                                    Logger.Log("   ширина: " + elem_width.ToString(), 2);
                                    Logger.Log("   высота: " + elem_height.ToString(), 2);

                                    LocationPoint elem_lp = (LocationPoint)elem.Location;
                                    XYZ point = elem_lp.Point; //XYZ point1 = transform.OfPoint(point);
                                    double elem_x = point.X * 0.3048; double elem_y = point.Y * 0.3048; double elem_z = point.Z * 0.3048;
                                    elem_x = Math.Round(elem_x, 3); elem_y = Math.Round(elem_y, 3); elem_z = Math.Round(elem_z, 3);

                                    Logger.Log("   Х: " + elem_x.ToString() + " Y: " + elem_y.ToString() + " Z: " + elem_z.ToString(), 2);

                                    Logger.Log("   создаем Hole", 2);
                                    string st = "Лишнее отверстие " + elem_mark + " удалено в Задании.";
                                    //if (scenario == 2) st = "Не заполнена позиция (Марка).";
                                    //Hole
                                    Hole hole = new Hole()
                                    {
                                        pasted = true,
                                        mark = "-",
                                        mark1 = elem_mark,
                                        width = elem_width,
                                        height = elem_height,
                                        coordStatusHead = "-",
                                        coordStatusBIM = "-",
                                        coordStatusST = "-",
                                        x = elem_x,
                                        y = elem_y,
                                        z = elem_z,
                                        status = st,
                                        id1= groupElem.IntegerValue,
                                    };
                                    holes.Add(hole);
                                }
                            }
                            //другие задания
                            if (groupElemsToDelete2.Count > 0)
                            {
                                Logger.Log("Обрабатываем лишние задания:", 2);
                                foreach (var groupElem in groupElemsToDelete2)
                                {
                                    Element elem = doc.GetElement(groupElem);
                                    string elem_mark = elem.get_Parameter(mrk).AsValueString();
                                    if (elem_mark == null || elem_mark.Length == 0) elem_mark = "-";
                                    Logger.Log("задание " + elem_mark + " id: " + elem.Id.ToString(), 2);

                                    string lengthParam1 = shaftLengthParam;
                                    string widthParam1 = shaftWidthParam;
                                    string heightParam1 = shaftHeightParam;

                                    double elem_length = elem.LookupParameter(lengthParam1).AsDouble() * 0.3048 * 1000;
                                    double elem_width = elem.LookupParameter(widthParam1).AsDouble() * 0.3048 * 1000;
                                    double elem_height = elem.LookupParameter(widthParam1).AsDouble() * 0.3048 * 1000;

                                    Logger.Log("   длина: " + elem_length.ToString(), 2);
                                    Logger.Log("   ширина: " + elem_width.ToString(), 2);
                                    Logger.Log("   высота: " + elem_height.ToString(), 2);

                                    LocationPoint elem_lp = (LocationPoint)elem.Location;
                                    XYZ point = elem_lp.Point; //XYZ point1 = transform.OfPoint(point);
                                    double elem_x = point.X * 0.3048; double elem_y = point.Y * 0.3048; double elem_z = point.Z * 0.3048;
                                    elem_x = Math.Round(elem_x, 3); elem_y = Math.Round(elem_y, 3); elem_z = Math.Round(elem_z, 3);

                                    Logger.Log("   Х: " + elem_x.ToString() + " Y: " + elem_y.ToString() + " Z: " + elem_z.ToString(), 2);

                                    Logger.Log("   создаем Hole", 2);
                                    string st = "Лишний элемент " + elem_mark + " удалено в Задании.";
                                    //if (scenario == 2) st = "Не заполнена позиция (Марка).";
                                    //Hole
                                    Hole hole = new Hole()
                                    {
                                        pasted = true,
                                        mark = "-",
                                        mark1 = elem_mark,
                                        length = elem_length,
                                        width = elem_width,
                                        height = elem_height,
                                        coordStatusHead = "-",
                                        coordStatusBIM = "-",
                                        coordStatusST = "-",
                                        x = elem_x,
                                        y = elem_y,
                                        z = elem_z,
                                        status = st,
                                        id1 = groupElem.IntegerValue,
                                    };
                                    holes.Add(hole);
                                }
                            }


                            break;
                        }
                    }
                    break;
                }
            }
            return holes;
        }
    }
}
