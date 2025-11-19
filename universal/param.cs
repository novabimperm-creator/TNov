using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TNov
{
    public class param
    {
        public static bool ParamExist(in string pName, Element elem)
        {
            foreach (Parameter p in elem.ParametersMap)
            {
                string paramName = p.Definition.Name;
                if (paramName == pName) { return true; }
            }
            return false;
        }
        public static bool ParamExistByGuid(in Guid pGuid, Element elem)
        {
            foreach (Parameter p in elem.ParametersMap)
            {
                if (p.IsShared)
                {
                    Guid paramGuid = p.GUID;
                    if (paramGuid == pGuid) { return true; }
                }
            }
            return false;
        }

    }
}
