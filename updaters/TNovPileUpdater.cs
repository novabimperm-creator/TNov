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
            Autodesk.Revit.ApplicationServices.Application app = doc.Application;

            BuiltInParameter gm = BuiltInParameter.ALL_MODEL_MODEL; //параметр Группа модели

            //проверка подключения к серверу
            string usagefilePath = nova.novaserver + "_TNov/usage.txt";
            bool servercheck = File.Exists(usagefilePath);

            if (servercheck)
            {
                BasePoint basePoint = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_ProjectBasePoint).Cast<BasePoint>().First();

                List<ElementId> idsB = data.GetModifiedElementIds().ToList();

                foreach (ElementId id in idsB)
                {
                    Element elem = doc.GetElement(id);
                    string pvalue = elem.Name;
                    if (pvalue != null)
                    {
                        if (pvalue.Contains("Свая"))
                        {
                            LocationPoint linkElem_lp = (LocationPoint)elem.Location;
                            XYZ point = linkElem_lp.Point;
                            double zz = point.Z - basePoint.Position.Z; zz = zz * 304.8;

                            Parameter param = elem.LookupParameter("Свая.ОтмНизаРостверка");
                            if (param != null)
                            {
                                try
                                {
                                    param.Set(zz); 
                                }
                                catch (Exception) { }
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
