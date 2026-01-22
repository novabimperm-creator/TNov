using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TNov.main;

namespace TNov
{
    public class TaskTools
    {
        public static string GetGroupNames(in Document linkDoc, in Document doc)
        {
            //параметры
            BuiltInParameter mrk = BuiltInParameter.ALL_MODEL_MARK; //Марка
            ElementId familyNameParamId = new ElementId(-1002002); //id параметра Имя семейства
            Guid adskGparamGuid = new Guid("3de5f1a4-d560-4fa8-a74f-25d250fb3401");//ADSK_Группирование
            Guid NTaskApprovedBIMParamGuid = new Guid("94587b6e-5bdd-4fe8-bea4-4996c32801c4");//N_Согласовано BIM
            Guid NTaskApprovedSTParamGuid = new Guid("7cb33aa5-8106-4e4c-8038-6691e34f438c");//N_Согласовано КР
            Guid NTaskApprovedMEPParamGuid = new Guid("5c117e3e-c32b-4ab9-9cbb-99557e7c20c5");//N_Согласовано рук

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
                Logger.Log(linkGroup.Name, 1);
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
                    int linkElem_coordStatusST_int = elem.get_Parameter(NTaskApprovedSTParamGuid).AsInteger();
                    if (linkElem_coordStatusST_int != 1) k++;
                    int linkElem_coordStatusBIM_int = elem.get_Parameter(NTaskApprovedBIMParamGuid).AsInteger();
                    if (linkElem_coordStatusBIM_int != 1) k++;
                }
                //прочие задания
                foreach (var linkGroupElem in linkGroupElems2)
                {
                    Element elem = linkDoc.GetElement(linkGroupElem);
                    string mrkvalue = elem.get_Parameter(mrk).AsValueString();
                    if (mrkvalue == null || mrkvalue == "") badElementIds += linkGroupElem.ToString() + " ";
                    int linkElem_coordStatusST_int = elem.get_Parameter(NTaskApprovedSTParamGuid).AsInteger();
                    if (linkElem_coordStatusST_int != 1) k++;
                    int linkElem_coordStatusBIM_int = elem.get_Parameter(NTaskApprovedBIMParamGuid).AsInteger();
                    if (linkElem_coordStatusBIM_int != 1) k++;
                }
                if (badElementIds.Length > 0) Logger.Log("Элементы с незаполненной Маркой: " + badElementIds, 1);

                //adsk группирование
                string gSet = "Не заполнено";
                bool setExist = Param.ParamExistByGuid(adskGparamGuid, linkGroup);
                if (setExist)
                {
                    if (linkGroup.get_Parameter(adskGparamGuid).HasValue)
                    {
                        gSet = linkGroup.get_Parameter(adskGparamGuid).AsString();
                    }
                }

                //ищем задание в текущей модели
                int i = 0; int j = 0;
                if (groups == null || groups.Count == 0)
                {
                    status = "Задание еще не вставлялось.";
                    if (badElementIds.Length > 0) status += " Не у всех элементов в задании заполнена позиция (Марка).";
                    if (k > 0) status += " Не все элементы согласованы КР или BIM.";
                    string groupText0 = shortName + "=" + status + "=" + gSet;
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
                            List<Hole> holes = HolesInGroup(linkDoc, doc, shortName);

                            foreach (var hole in holes)
                            {
                                if (hole.status.Length > 0) j++;
                            }
                            if (status == "" || status == "Некорректное имя группы в модели задания.")
                            { if (j > 0) status += "Есть проблемы - см. детальный анализ. "; }
                            if (status.Length > 0) Logger.Log("      статус " + status, 2);
                            break;
                        }
                    }
                    if (i == 0) status = "Задание еще не вставлялось.";
                    if (badElementIds.Length > 0) status += " Не у всех элементов в задании заполнена позиция (Марка).";
                    if (k > 0) status += " Не все элементы согласованы КР или BIM.";
                    if (status == "") status = "Актуально.";
                    string groupText = shortName + "=" + status + "=" + gSet;
                    groupTxtList.Add(groupText); Logger.Log(groupText, 1);

                }


            }
            string groups1 = "";
            foreach (string group in groupTxtList) groups1 += group + "|";

            return groups1;
        }
        public static bool CompareWithTolerance(double a, double b)
        {
            return Math.Abs(a - b) <= 0.02;
        }
        public static List<Hole> HolesInGroup(in Document linkDoc, in Document doc, in string groupName)
        {
            List<Hole> holes = new List<Hole>(); //универсальный класс для отверстий и других заданий

            //параметры
            BuiltInParameter mrk = BuiltInParameter.ALL_MODEL_MARK; //Марка
            BuiltInParameter gm = BuiltInParameter.ALL_MODEL_MODEL; //параметр Группа модели
            ElementId familyNameParamId = new ElementId(-1002002); //id параметра Имя семейства

            Guid adskHoleWidthParamGuid = new Guid("096bc30e-3c95-4637-84d5-9f6bf45d8676");//ADSK_Отверстие_Ширина
            Guid adskHoleHeightParamGuid = new Guid("bc4e92d8-db66-4e93-8923-3af6e2dc8599");//ADSK_Отверстие_Высота
            Guid adskDiamParamGuid = new Guid("9b679ab7-ea2e-49ce-90ab-0549d5aa36ff");//ADSK_Размер_Диаметр
            Guid adskWidthParamGuid = new Guid("8f2e4f93-9472-4941-a65d-0ac468fd6a5d");//ADSK_Размер_Ширина
            Guid adskHeightParamGuid = new Guid("da753fe3-ecfa-465b-9a2c-02f55d0c2ff1");//ADSK_Размер_Высота
            Guid adskLengthParamGuid = new Guid("748a2515-4cc9-4b74-9a69-339a8d65a212");//ADSK_Размер_Длина
            Guid NTaskApprovedBIMParamGuid = new Guid("94587b6e-5bdd-4fe8-bea4-4996c32801c4");//N_Согласовано BIM
            Guid NTaskApprovedSTParamGuid = new Guid("7cb33aa5-8106-4e4c-8038-6691e34f438c");//N_Согласовано КР
            Guid NTaskApprovedMEPParamGuid = new Guid("5c117e3e-c32b-4ab9-9cbb-99557e7c20c5");//N_Согласовано рук

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

            Logger.Log("   Детальный анализ:", 2);
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

                    if (groups == null || groups.Count == 0) continue;
                    //ищем задание в текущей модели
                    foreach (var group in groups)
                    {
                        string[] nameParts1 = group.Name.Split('_');
                        string shortName1 = group.Name;
                        if (nameParts1.Length > 2) shortName1 = nameParts1[0] + '_' + nameParts1[1] + '_' + nameParts1[2];
                        if (shortName1 == shortName)
                        {
                            Logger.Log("Группа " + shortName + " найдена в текущей модели", 2);
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

                                    Guid widthParam = adskHoleWidthParamGuid; Guid heightParam = adskHoleHeightParamGuid;
                                    bool circleHole = false; string widthParamName = "Ширина: "; string heightParamName = "Высота: ";
                                    foreach (Parameter param in linkElem.ParametersMap) //круглые отв
                                    {
                                        if (param.IsShared)
                                        {
                                            Guid paramGUID = param.GUID;
                                            if (paramGUID == adskDiamParamGuid)
                                            {
                                                circleHole = true; widthParamName = "Диаметр: "; heightParamName = "Диаметр: ";
                                                widthParam = adskDiamParamGuid; heightParam = adskDiamParamGuid;
                                                Logger.Log("   круглое", 2);
                                                break;
                                            }
                                        }
                                        
                                    }
                                    double linkElem_width = linkElem.get_Parameter(widthParam).AsDouble() * 0.3048 * 1000;
                                    double linkElem_height = linkElem.get_Parameter(heightParam).AsDouble() * 0.3048 * 1000;
                                    Logger.Log("   " + widthParamName + linkElem_width.ToString(), 2);
                                    Logger.Log("   " + heightParamName + linkElem_width.ToString(), 2);

                                    string linkElem_coordStatusHead = "-";
                                    int linkElem_coordStatusHead_int = linkElem.get_Parameter(NTaskApprovedMEPParamGuid).AsInteger();
                                    if (linkElem_coordStatusHead_int == 1)
                                    {
                                        linkElem_coordStatusHead = "v";
                                        Logger.Log("   согласовано руководителем", 2);
                                    }

                                    string linkElem_coordStatusBIM = "-";
                                    int linkElem_coordStatusBIM_int = linkElem.get_Parameter(NTaskApprovedBIMParamGuid).AsInteger();
                                    if (linkElem_coordStatusBIM_int == 1)
                                    {
                                        linkElem_coordStatusBIM = "v";
                                        Logger.Log("   согласовано BIM", 2);
                                    }

                                    string linkElem_coordStatusST = "-";
                                    int linkElem_coordStatusST_int = linkElem.get_Parameter(NTaskApprovedSTParamGuid).AsInteger();
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
                                                if (param.IsShared)
                                                {
                                                    Guid paramGUID = param.GUID;
                                                    if (paramGUID == adskDiamParamGuid)
                                                    {
                                                        d++;
                                                        Logger.Log("   круглое", 2);
                                                        break;
                                                    }
                                                }
                                                
                                            }
                                            if (circleHole && d == 0) //в Задании - круглое, а в КЖ - нет
                                            {
                                                linkElem_status += "Отверстие в задании изменено на круглое. ";
                                                break;
                                            }
                                            if (!circleHole && d > 0) //в Задании - прямоугольное, а в КЖ - круглое
                                            {
                                                linkElem_status += "Отверстие в задании изменено на прямоугольное. ";
                                                break;
                                            }

                                            double elem_width = elem.get_Parameter(widthParam).AsDouble() * 0.3048 * 1000;
                                            if (CompareWithTolerance(elem_width, linkElem_width) == false) linkElem_status += widthParamName + elem_width.ToString() + ". ";

                                            Logger.Log("      " + elem.get_Parameter(widthParam).Definition.Name + ": " + elem_width.ToString() + "; исходное: " + linkElem_width.ToString(), 2);

                                            if (!circleHole)
                                            {
                                                double elem_height = elem.get_Parameter(heightParam).AsDouble() * 0.3048 * 1000;
                                                if (CompareWithTolerance(elem_height, linkElem_height) == false) linkElem_status += "Высота: " + elem_height.ToString() + ". ";
                                                Logger.Log("      " + elem.get_Parameter(heightParam).Definition.Name + ": " + elem_height.ToString() + "; исходное: " + linkElem_height.ToString(), 2);

                                            }

                                            LocationPoint elem_lp = (LocationPoint)elem.Location;
                                            XYZ point = elem_lp.Point; //XYZ point1 = transform.OfPoint(point);
                                            double elem_x = point.X * 0.3048; double elem_y = point.Y * 0.3048; double elem_z = point.Z * 0.3048;
                                            elem_x = Math.Round(elem_x, 3); elem_y = Math.Round(elem_y, 3); elem_z = Math.Round(elem_z, 3);
                                            if (CompareWithTolerance(linkElem_x, elem_x) == false) linkElem_status += "X: " + elem_x.ToString() + ". ";
                                            if (CompareWithTolerance(linkElem_y, elem_y) == false) linkElem_status += "Y: " + elem_y.ToString() + ". ";
                                            if (CompareWithTolerance(linkElem_z, elem_z) == false) linkElem_status += "Z: " + elem_z.ToString() + ". ";

                                            Logger.Log("      " + "X: " + elem_x.ToString() + "; Y: " + elem_y.ToString() + "; Z: " +
                                                elem_z.ToString() + "; исходное: " + "X: " + linkElem_x.ToString() + "; Y: " + linkElem_y.ToString() + "; Z: " + linkElem_z.ToString(), 2);


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
                                                if (elem_mark == linkElem_mark)
                                                {
                                                    linkElem_pasted = true;
                                                    id1 = elem.Id.IntegerValue;
                                                    Logger.Log("   найдено в текущей модели", 2);

                                                    double elem_width = elem.get_Parameter(widthParam).AsDouble() * 0.3048 * 1000;
                                                    if (CompareWithTolerance(elem_width, linkElem_width) == false) linkElem_status += widthParamName + elem_width.ToString() + ". ";

                                                    if (!circleHole)
                                                    {
                                                        double elem_height = elem.get_Parameter(heightParam).AsDouble() * 0.3048 * 1000;
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
                                    
                                    string lengthParamName = "Длина: "; string widthParamName = "Ширина: "; string heightParamName = "Высота: ";

                                    double linkElem_length = linkElem.get_Parameter(adskLengthParamGuid).AsDouble() * 0.3048 * 1000;
                                    double linkElem_width = linkElem.get_Parameter(adskWidthParamGuid).AsDouble() * 0.3048 * 1000;
                                    double linkElem_height = linkElem.get_Parameter(adskHeightParamGuid).AsDouble() * 0.3048 * 1000;
                                    Logger.Log("   " + lengthParamName + linkElem_length.ToString(), 2);
                                    Logger.Log("   " + widthParamName + linkElem_width.ToString(), 2);
                                    Logger.Log("   " + heightParamName + linkElem_width.ToString(), 2);

                                    string linkElem_coordStatusHead = "-";
                                    int linkElem_coordStatusHead_int = linkElem.get_Parameter(NTaskApprovedMEPParamGuid).AsInteger();
                                    if (linkElem_coordStatusHead_int == 1)
                                    {
                                        linkElem_coordStatusHead = "v";
                                        Logger.Log("   согласовано руководителем", 2);
                                    }

                                    string linkElem_coordStatusBIM = "-";
                                    int linkElem_coordStatusBIM_int = linkElem.get_Parameter(NTaskApprovedBIMParamGuid).AsInteger();
                                    if (linkElem_coordStatusBIM_int == 1)
                                    {
                                        linkElem_coordStatusBIM = "v";
                                        Logger.Log("   согласовано BIM", 2);
                                    }

                                    string linkElem_coordStatusST = "-";
                                    int linkElem_coordStatusST_int = linkElem.get_Parameter(NTaskApprovedSTParamGuid).AsInteger();
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

                                            double elem_length = elem.get_Parameter(adskLengthParamGuid).AsDouble() * 0.3048 * 1000;
                                            if (CompareWithTolerance(elem_length, linkElem_length) == false) linkElem_status += lengthParamName + elem_length.ToString() + ". ";
                                            Logger.Log("      " + elem.get_Parameter(adskLengthParamGuid).Definition.Name + ": " + elem_length.ToString() + "; исходное: " + linkElem_length.ToString(), 2);

                                            double elem_width = elem.get_Parameter(adskWidthParamGuid).AsDouble() * 0.3048 * 1000;
                                            if (CompareWithTolerance(elem_width, linkElem_width) == false) linkElem_status += widthParamName + elem_width.ToString() + ". ";
                                            Logger.Log("      " + elem.get_Parameter(adskWidthParamGuid).Definition.Name + ": " + elem_width.ToString() + "; исходное: " + linkElem_width.ToString(), 2);

                                            double elem_height = elem.get_Parameter(adskHeightParamGuid).AsDouble() * 0.3048 * 1000;
                                            if (CompareWithTolerance(elem_height, linkElem_height) == false) linkElem_status += "Высота: " + elem_height.ToString() + ". ";
                                            Logger.Log("      " + elem.get_Parameter(adskHeightParamGuid).Definition.Name + ": " + elem_height.ToString() + "; исходное: " + linkElem_height.ToString(), 2);



                                            LocationPoint elem_lp = (LocationPoint)elem.Location;
                                            XYZ point = elem_lp.Point; //XYZ point1 = transform.OfPoint(point);
                                            double elem_x = point.X * 0.3048; double elem_y = point.Y * 0.3048; double elem_z = point.Z * 0.3048;
                                            elem_x = Math.Round(elem_x, 3); elem_y = Math.Round(elem_y, 3); elem_z = Math.Round(elem_z, 3);
                                            if (CompareWithTolerance(linkElem_x, elem_x) == false) linkElem_status += "X: " + elem_x.ToString() + ". ";
                                            if (CompareWithTolerance(linkElem_y, elem_y) == false) linkElem_status += "Y: " + elem_y.ToString() + ". ";
                                            if (CompareWithTolerance(linkElem_z, elem_z) == false) linkElem_status += "Z: " + elem_z.ToString() + ". ";

                                            Logger.Log("      " + "X: " + elem_x.ToString() + "; Y: " + elem_y.ToString() + "; Z: " + elem_z.ToString() + "; исходное: " +
                                                "X: " + linkElem_x.ToString() + "; Y: " + linkElem_y.ToString() + "; Z: " + linkElem_z.ToString(), 2);


                                            Logger.Log("   удаляем из списка на удаление", 2);
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

                                                    double elem_length = elem.get_Parameter(adskLengthParamGuid).AsDouble() * 0.3048 * 1000;
                                                    if (CompareWithTolerance(elem_length, linkElem_length) == false) linkElem_status += lengthParamName + elem_length.ToString() + ". ";

                                                    double elem_width = elem.get_Parameter(adskWidthParamGuid).AsDouble() * 0.3048 * 1000;
                                                    if (CompareWithTolerance(elem_width, linkElem_width) == false) linkElem_status += widthParamName + elem_width.ToString() + ". ";

                                                    double elem_height = elem.get_Parameter(adskHeightParamGuid).AsDouble() * 0.3048 * 1000;
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

                                    Guid widthParam = adskHoleWidthParamGuid; Guid heightParam = adskHoleHeightParamGuid;
                                    foreach (Parameter param in elem.ParametersMap) //круглые отв
                                    {
                                        if (param.IsShared)
                                        {
                                            Guid paramGUID = param.GUID;
                                            if (paramGUID == adskDiamParamGuid)
                                            {
                                                widthParam = adskDiamParamGuid; heightParam = adskDiamParamGuid;
                                                break;
                                            }
                                        }
                                    }
                                    double elem_width = elem.get_Parameter(widthParam).AsDouble() * 0.3048 * 1000;
                                    double elem_height = elem.get_Parameter(widthParam).AsDouble() * 0.3048 * 1000;

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
                                        id1 = groupElem.IntegerValue,
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

                                    double elem_length = elem.get_Parameter(adskLengthParamGuid).AsDouble() * 0.3048 * 1000;
                                    double elem_width = elem.get_Parameter(adskWidthParamGuid).AsDouble() * 0.3048 * 1000;
                                    double elem_height = elem.get_Parameter(adskHeightParamGuid).AsDouble() * 0.3048 * 1000;

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
        public static List<string> GetGroupsInfo(in Document doc)
        {
            //параметры
            BuiltInParameter mrk = BuiltInParameter.ALL_MODEL_MARK; //Марка
            ElementId familyNameParamId = new ElementId(-1002002); //id параметра Имя семейства
            Guid adskHoleWidthParamGuid = new Guid("096bc30e-3c95-4637-84d5-9f6bf45d8676");//ADSK_Отверстие_Ширина
            Guid adskHoleHeightParamGuid = new Guid("bc4e92d8-db66-4e93-8923-3af6e2dc8599");//ADSK_Отверстие_Высота
            Guid adskDiamParamGuid = new Guid("9b679ab7-ea2e-49ce-90ab-0549d5aa36ff");//ADSK_Размер_Диаметр
            Guid NTaskApprovedBIMParamGuid = new Guid("94587b6e-5bdd-4fe8-bea4-4996c32801c4");//N_Согласовано BIM
            Guid NTaskApprovedSTParamGuid = new Guid("7cb33aa5-8106-4e4c-8038-6691e34f438c");//N_Согласовано КР
            Guid NTaskApprovedMEPParamGuid = new Guid("5c117e3e-c32b-4ab9-9cbb-99557e7c20c5");//N_Согласовано рук

            List<Group> groups = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_IOSModelGroups)
                .WhereElementIsNotElementType()
                .Cast<Group>()
                .ToList();

            List<string> groupTxtList = new List<string>();
            //проходим по группам в модели
            foreach (var group in groups)
            {
                Logger.Log(group.Name, 1);
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

                List<Hole> holes0 = new List<Hole>();

                groupTxtList.Add(group.Name);
                foreach (var groupElem in groupElems)
                {
                    Element elem = doc.GetElement(groupElem);
                    string mrkvalue = elem.get_Parameter(mrk).AsValueString();
                    if (mrkvalue == null || mrkvalue == "") mrkvalue = "0";
                    int elem_coordStatusST_int = elem.get_Parameter(NTaskApprovedSTParamGuid).AsInteger();
                    string STstatus = "- (КР)"; if (elem_coordStatusST_int > 0) STstatus = "согласовано КР";
                    int elem_coordStatusHead_int = elem.get_Parameter(NTaskApprovedMEPParamGuid).AsInteger();
                    string Headstatus = "- (рук)"; if (elem_coordStatusST_int > 0) Headstatus = "согласовано рук";
                    int elem_coordStatusBIM_int = elem.get_Parameter(NTaskApprovedBIMParamGuid).AsInteger();
                    string BIMstatus = "- (BIM)"; if (elem_coordStatusST_int > 0) BIMstatus = "согласовано BIM";

                    string holeType = "Прямоугольное";
                    Guid widthParam = adskHoleWidthParamGuid; Guid heightParam = adskHoleHeightParamGuid;
                    foreach (Parameter param in elem.ParametersMap) //круглые отв
                    {
                        if (param.IsShared)
                        {
                            Guid paramGUID = param.GUID;
                            if (paramGUID == adskDiamParamGuid)
                            {
                                holeType = "Круглое";
                                widthParam = adskDiamParamGuid;
                                heightParam = adskDiamParamGuid;
                                break;
                            }
                        }
                        
                    }
                    double elem_width = elem.get_Parameter(widthParam).AsDouble() * 0.3048 * 1000;
                    double elem_height = elem.get_Parameter(heightParam).AsDouble() * 0.3048 * 1000;
                    LocationPoint elem_lp = (LocationPoint)elem.Location;
                    XYZ p = elem_lp.Point;

                    double elem_x = p.X * 0.3048; double elem_y = p.Y * 0.3048; double elem_z = p.Z * 0.3048;

                    Hole hole = new Hole()
                    {
                        pasted = true,
                        mark = mrkvalue,
                        mark1 = mrkvalue,
                        status = holeType + ", статус: ",
                        coordStatusBIM = BIMstatus,
                        coordStatusHead = Headstatus,
                        coordStatusST = STstatus,
                        width = elem_width,
                        height = elem_height,
                        x = elem_x,
                        y = elem_y,
                        z = elem_z,
                        id1 = elem.Id.IntegerValue
                    };
                    holes0.Add(hole);


                }

                List<Hole> holes1 = holes0.OrderBy(h => h.mark.Length)
                        .ThenBy(h => h.mark)
                        .ToList();

                foreach (var hole in holes1) groupTxtList.Add(hole.mark + "__id: " + hole.id1 + "__" + hole.width + "x" + hole.height + "__" + hole.status +
                    hole.coordStatusHead + ", " + hole.coordStatusBIM + ", " + hole.coordStatusST + "__" + "местоположение: " +
                    hole.x.ToString() + " " + hole.y.ToString() + " " + hole.z.ToString() + " ");
            }

            return groupTxtList;
        }
        public static int GetHoleMaxNumber(in Document doc)
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
                    if (groupId.IntegerValue > 0)
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
