using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TNovCommon;

namespace TNov
{
    [Transaction(TransactionMode.Manual)]
    public class CommandInDev : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            new InfoWindow280("Команда - на завершающей стадии разработки. Спасибо за ваш интерес, скоро всё появится!").ShowDialog();
            return Result.Succeeded;
        }
    }
}
