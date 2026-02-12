using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using Autodesk.Revit.DB.Events;
using Autodesk.Revit.Attributes;
using TNov.main;

namespace TNov
{
    [Transaction(TransactionMode.Manual)]
    public class TNovPileUpdater : IUpdater
    {
        static AddInId _appId;
        static UpdaterId _updaterId;

        public TNovPileUpdater(AddInId id)
        {
            _appId = id;

            _updaterId = new UpdaterId(_appId, new Guid("aac9978d-bbb9-45bc-8f04-e8c584763f9a"));
        }

        public void Execute(UpdaterData data)
        {
            Document doc = data.GetDocument();

            //проверка подключения к серверу
            string usagefilePath = nova.novaserver + "_TNov/usage.txt";
            bool servercheck = File.Exists(usagefilePath);

            if (servercheck)
            {
                string docName = doc.Title.ToString();
                if (docName.Contains("-КЖ") || docName.Contains("_КЖ") || docName.Contains("-КР-") || docName.Contains("_КР_"))
                {
                    BasePoint basePoint = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_ProjectBasePoint).Cast<BasePoint>().First();

                    List<ElementId> idsB = data.GetModifiedElementIds().ToList();

                    foreach (ElementId id in idsB)
                    {
                        Element elem = doc.GetElement(id);
                        if (elem != null & elem.Name != null)
                        {
                            if (elem.Name.Contains("Свая"))
                            {
                                LocationPoint elem_lp = (LocationPoint)elem.Location;
                                if (elem_lp != null)
                                {
                                    XYZ point = elem_lp.Point;
                                    double zz = point.Z - basePoint.Position.Z; zz = zz * 304.8;

                                    if (Param.ParamExist("Свая.ОтмНизаРостверка", elem))
                                    {
                                        Parameter param = elem.LookupParameter("Свая.ОтмНизаРостверка");
                                        if (param != null)
                                        {
                                            param.Set(zz);
                                        }
                                    }
                                }
                            }
                        }
                    }
                }

                    

            }



        }

        public string GetAdditionalInformation()
        {
            return "TNov, bim@pm-nova.ru";
        }

        public ChangePriority GetChangePriority()
        {
            return ChangePriority.FloorsRoofsStructuralWalls;
        }

        public UpdaterId GetUpdaterId()
        {
            return _updaterId;
        }

        public string GetUpdaterName()
        {
            return "TNovPileUpdater";
        }
    }
}
