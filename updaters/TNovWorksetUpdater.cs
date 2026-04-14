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

namespace TNov
{
    [Transaction(TransactionMode.Manual)]
    public class TNovWorksetUpdater : IUpdater
    {
        static AddInId _appId;
        static UpdaterId _updaterId;

        public TNovWorksetUpdater(AddInId id)
        {
            _appId = id;

            _updaterId = new UpdaterId(_appId, new Guid(
                                                   "71274837-12b3-48de-a7b8-347600158bb3"));
        }

        public void Execute(UpdaterData data)
        {
            Document doc = data.GetDocument();
            Autodesk.Revit.ApplicationServices.Application app = doc.Application;

#if config1

            //параметры
            ElementId familyNameParamId = new ElementId(-1002002); //id параметра Имя семейства

            //проверка файла на наличие наборов
            bool dws = doc.IsWorkshared;
            
            if (dws) 
            {
                //проверка подключения к серверу
                string usagefilePath = nova.novaserver + "_TNov/usage.txt";
                bool servercheck = File.Exists(usagefilePath);

                if (servercheck)
                {
                    List<ElementId> idsA = data.GetAddedElementIds().ToList();
                    List<ElementId> idsM = data.GetModifiedElementIds().ToList();
                    List<ElementId> ids = new List<ElementId>();

                    //ElementFilter elementFilter = (ElementFilter)new ElementParameterFilter(ParameterFilterRuleFactory.CreateContainsRule(familyNameParamId, "pmN.Отверстие", true));

                    foreach (var id in idsA)
                    {
                        Element elem = doc.GetElement(id); ids.Add(id);
                        //if (elementFilter.PassesFilter(elem)) ids.Add(id);
                    }
                    foreach (var id in idsM)
                    {
                        Element elem = doc.GetElement(id); ids.Add(id);
                        //if (elementFilter.PassesFilter(elem)) ids.Add(id);
                    }


                    List<Workset> worksets = new FilteredWorksetCollector(doc)  //рабочие наборы документа
                                        .Cast<Workset>()                   //элементы категории Рабочие наборы
                                        .ToList();                         //формируем список
                    foreach (var workset in worksets)
                    {
                        bool isActive = workset.IsVisibleByDefault;
                        if(workset.Kind==WorksetKind.UserWorkset&&!isActive)
                        {
                            WorksetDefaultVisibilitySettings defaultVisibility = WorksetDefaultVisibilitySettings.GetWorksetDefaultVisibilitySettings(doc);
                            defaultVisibility.SetWorksetVisibility(workset.Id, true);
                        }
                    }

                    foreach (ElementId id in ids)
                    {
                        Element elem = doc.GetElement(id);
                        if (null != elem)
                        {
                            


                            
                        }

                    }
                    
                }
            }

#endif
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
            return "TNovWorksetUpdater";
        }
    }
}
