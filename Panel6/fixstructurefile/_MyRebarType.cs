using System.Collections.Generic;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;

namespace TNov
{
    public class _MyRebarType
    {
        public string Name { get; set; }
        public RebarBarType bartype;
        public Dictionary<string, _MyParameterValue> ValuesStorage = new Dictionary<string, _MyParameterValue>();

        public _MyRebarType(RebarBarType BarType)
        {
            Name = BarType.Name;
            bartype = BarType;

            foreach (Parameter param in BarType.ParametersMap)
            {
                string paramName = param.Definition.Name;
                _MyParameterValue mpv = new _MyParameterValue(param);
                ValuesStorage.Add(paramName, mpv);
            }
        }
    }
}
