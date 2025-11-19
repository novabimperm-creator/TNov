using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;

namespace TNov
{
    public class _MyProjectSharedParameter
    {
        public string Name { get; set; }
        public Definition def;
        public List<Category> categories = new List<Category>();
        public BuiltInParameterGroup paramGroup;
        public Guid guid;

        public _MyProjectSharedParameter(Parameter param, Document doc)
        {
            
            def = param.Definition;
            Name = def.Name;

            InternalDefinition intDef = def as InternalDefinition;
            if (intDef != null) paramGroup = intDef.ParameterGroup;

            guid = param.GUID;


            ElementBinding elemBind = this.GetBindingByParamName(Name, doc);

            foreach (Category cat in elemBind.Categories)
            {
                categories.Add(cat);
            }
        }

        public bool RemoveOrAddFromRebarCategory(Document doc, Element elem, bool addOrDeleteCat)
        {
            Autodesk.Revit.ApplicationServices.Application app = doc.Application;

            ElementBinding elemBind = this.GetBindingByParamName(Name, doc);

            //получаю список категорий
            CategorySet newCatSet = app.Create.NewCategorySet();
            int rebarcatid = new ElementId(BuiltInCategory.OST_Rebar).IntegerValue;
            foreach (Category cat in elemBind.Categories)
            {
                int catId = cat.Id.IntegerValue;
                if (catId != rebarcatid)
                {
                    newCatSet.Insert(cat);
                }
            }

            if (addOrDeleteCat)
            {
                Category cat = elem.Category;
                newCatSet.Insert(cat);
            }

            TypeBinding newBind = app.Create.NewTypeBinding(newCatSet);
            if (doc.ParameterBindings.Insert(def, newBind, paramGroup))
            {
                return true;
            }
            else
            {
                if (doc.ParameterBindings.ReInsert(def, newBind, paramGroup))
                {
                    return true;
                }
                else
                {
                    return false;
                }
            }
        }

        public void AddToProjectParameters(Document doc, Element elem)
        {
            Autodesk.Revit.ApplicationServices.Application app = doc.Application;
            //string oldSharedParamsFile = app.SharedParametersFilename;

            //Проверка актуальности шаблона

            ProjectInfo projectInfo = doc.ProjectInformation;
            //Guid guid = new Guid ("ae46eb7a - 03bf - 497e-ac96 - 1615c672324b");
            Autodesk.Revit.DB.Parameter template = projectInfo.LookupParameter("N_Орг.ВерсияШаблона");
            bool oldProject = false;
            string templateversion = "v";
            if (template == null) { oldProject = true; }
            else { templateversion = template.AsValueString(); }
            templateversion = templateversion.Replace(" (Talan)", "");
            templateversion = templateversion.Replace("(Talan)", "");
            templateversion = templateversion.Replace(" (UDS)", "");
            templateversion = templateversion.Replace("(UDS)", "");
            if (templateversion.Contains("v"))
            {
                oldProject = true;
            }
            else
            {
                string[] versionparts = templateversion.Split('.');
                double versionMath = Convert.ToDouble(versionparts[0]) * 10 + Convert.ToDouble(versionparts[1]);
                if (versionMath < 20223) { oldProject = true; }
            }

            string Name0 = Name; //запоминаем имя параметра как в проекте
            
            if (oldProject == true) //меняем имя параметра, чтобы забрать его "обратно" из ФОП
            {
                if (Name == "Арм.ВыполненаСемейством") { Name = "A_Арм семейством"; }
                if (Name == "Рзм.Диаметр") { Name = "A_Размер_Диаметр"; }
                if (Name == "Арм.КлассЧисло") { Name = "A_Код металлопроката"; }
                if (Name == "Мрк.НаименованиеИзделия") { Name = "W_Мрк.НаименованиеИзделия"; }
                if (Name == "Арм.Обозначение") { Name = "N_Арм.Обозначение"; }
                if (Name == "Мрк.ПозАрматурыПМ") { Name = "W_Мрк.ПозАрматурыПМ"; }
                if (Name == "Наименование") { Name = "N_Наименование"; }
                if (Name == "Обозначение") { Name = "N_Обозначение"; }
                if (Name == "Рзм.ПогМетрыВкл") { Name = "A_ПогМетрыВкл"; }
                if (Name == "Орг.СпособПодсчетаМассы") { Name = "A_Способ подсчета массы"; }
                if (Name == "Орг.ИзделиеТипПодсчета") { Name = "A_Тип элемента КЖ"; }
                if (Name == "Арм.ТипИзделия") { Name = "W_Арм.ТипИзделия"; }
            }


            ExternalDefinition exDef = null;
            string sharedFile = app.SharedParametersFilename;
            DefinitionFile sharedParamFile = app.OpenSharedParameterFile();
            foreach (DefinitionGroup defgroup in sharedParamFile.Groups)
            {
                foreach (Definition def in defgroup.Definitions)
                {
                    if (def.Name == Name)
                    {
                        exDef = def as ExternalDefinition;
                    }
                }
            }
            if (exDef == null) throw new Exception("В файле общих параметров не найден общий параметр " + Name);

            Name = Name0; //возвращаем имя параметра как в проекте для дальнейших действий

            CategorySet catSet = app.Create.NewCategorySet();
            catSet.Insert(elem.Category);
            TypeBinding newBind = app.Create.NewTypeBinding(catSet);

            doc.ParameterBindings.Insert(exDef, newBind, paramGroup);

            //app.SharedParametersFilename = oldSharedParamsFile;

            Parameter testParam = elem.LookupParameter(Name);
            if (testParam == null) throw new Exception("Не удалось добавить обший параметр " + Name);
        }



        private ElementBinding GetBindingByParamName(String paramName, Document doc)
        {
            Autodesk.Revit.ApplicationServices.Application app = doc.Application;
            DefinitionBindingMapIterator iter = doc.ParameterBindings.ForwardIterator();
            while (iter.MoveNext())
            {
                Definition curDef = iter.Key;
                if (!Name.Equals(curDef.Name)) continue;

                def = curDef;
                ElementBinding elemBind = (ElementBinding)iter.Current;
                return elemBind;
            }
            throw new Exception("не найден параметр " + paramName);
        }
    }
}