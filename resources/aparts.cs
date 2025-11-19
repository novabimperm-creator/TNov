using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.Attributes;
using System.Collections.Generic;
using System.Windows.Forms;
using System.Linq;
using Autodesk.Revit.DB.Architecture;
using System;

namespace TNov
{
    [Transaction(TransactionMode.Manual)]
    public class aparts : IExternalCommand
    {
        private static DialogResult ShowInputDialog(out string output1, out string output2, out bool output3)
        {
            System.Drawing.Size size = new System.Drawing.Size(360, 214);
            System.Windows.Forms.Form inputBox = new System.Windows.Forms.Form();

            inputBox.FormBorderStyle = FormBorderStyle.FixedDialog;
            inputBox.ClientSize = size;
            inputBox.Text = "Исходные данные";
            inputBox.StartPosition = FormStartPosition.CenterParent;

            System.Windows.Forms.Label label1 = new System.Windows.Forms.Label();
            label1.Text = "k=0,5:";
            label1.Location = new System.Drawing.Point(5, 5);
            inputBox.Controls.Add(label1);

            System.Windows.Forms.TextBox textBox1 = new System.Windows.Forms.TextBox();
            textBox1.Size = new System.Drawing.Size(size.Width - 10, 23);
            textBox1.Location = new System.Drawing.Point(5, 29);
            textBox1.Text = "Лоджия";
            inputBox.Controls.Add(textBox1);

            System.Windows.Forms.Label label2 = new System.Windows.Forms.Label();
            label2.Text = "k=0,3:";
            label2.Location = new System.Drawing.Point(5, 60);
            inputBox.Controls.Add(label2);

            System.Windows.Forms.TextBox textBox2 = new System.Windows.Forms.TextBox();
            textBox2.Size = new System.Drawing.Size(size.Width - 10, 23);
            textBox2.Location = new System.Drawing.Point(5, 84);
            textBox2.Text = "Балкон,Французский балкон,Терраса";
            inputBox.Controls.Add(textBox2);

            System.Windows.Forms.Label label3 = new System.Windows.Forms.Label();
            label3.Text = "Пересчитать площади:";
            label3.Size = new System.Drawing.Size(size.Width - 10, 23);
            label3.Location = new System.Drawing.Point(5, 115);
            inputBox.Controls.Add(label3);

            System.Windows.Forms.CheckBox box1 = new System.Windows.Forms.CheckBox();
            box1.Location = new System.Drawing.Point(5, 134);
            box1.Checked = true;
            inputBox.Controls.Add(box1);

            System.Windows.Forms.Button okButton = new System.Windows.Forms.Button();
            okButton.DialogResult = DialogResult.OK;
            okButton.Name = "okButton";
            okButton.Size = new System.Drawing.Size(120, 23);
            okButton.Text = "&ОК";
            okButton.Location = new System.Drawing.Point(size.Width - 130, 174);
            inputBox.Controls.Add(okButton);

            inputBox.AcceptButton = okButton;

            System.Windows.Forms.Button cancelButton = new System.Windows.Forms.Button();
            cancelButton.DialogResult = DialogResult.Cancel;
            cancelButton.Name = "cancelButton";
            cancelButton.Size = new System.Drawing.Size(75, 23);
            cancelButton.Text = "&Закрыть";
            cancelButton.Location = new System.Drawing.Point(size.Width - 350, 174);
            inputBox.Controls.Add(cancelButton);

            inputBox.CancelButton = cancelButton;

            inputBox.MaximizeBox = false;
            inputBox.MinimizeBox = false;

            DialogResult result;

            result = inputBox.ShowDialog();


            if (result == DialogResult.OK) { output1 = textBox1.Text; output2 = textBox2.Text; output3 = box1.Checked; }
            else { output1 = "Отменено"; output2 = textBox2.Text; output3 = box1.Checked; }

            return result;
        }

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {

            //получаем UIDocument
            UIDocument uidoc = commandData.Application.ActiveUIDocument;
            //получаем Document
            Document doc = uidoc.Document;
            //получаем uiApp
            UIApplication uiApp = commandData.Application;
            //получаем App
            Autodesk.Revit.ApplicationServices.Application rvtApp = uiApp.Application;

            //запись в журнал
            string usagefilePath = nova.novaserver + "_TNov/usage.txt";
            string docName = doc.Title.ToString();
            string userName = rvtApp.Username;
            DateTime dateTime = DateTime.Now;
            string date = dateTime.ToString();
            try
            {
                System.IO.File.AppendAllText(usagefilePath, "\n" + date + "," + docName + "," + "Квартирография" + "," + userName);
            }
            catch (Exception ex)
            {
                TaskDialog.Show("Ошибка подключения", "Отсутствует подключение к корпоративной сети ПМ Новация. Проверьте подключение.");
                return Result.Failed;
            }

            //Проверка актуальности шаблона

            ProjectInfo projectInfo = doc.ProjectInformation;
            //Guid guid = new Guid ("ae46eb7a - 03bf - 497e-ac96 - 1615c672324b");
            Autodesk.Revit.DB.Parameter template = projectInfo.LookupParameter("N_Орг.ВерсияШаблона");
            bool oldProject = false;
            if (template == null) { oldProject = true; }

            //Список используемых параметров

            string N_Par_sq = "N_Площадь.Округленная";
            if (oldProject == true) { N_Par_sq = "Площадь.Округленная"; }
            string N_Par_sqk = "N_Площадь.ОкруглСКоэффициентом";
            if (oldProject == true) { N_Par_sqk = "Площадь.ОкруглСКоэффициентом"; }
            string N_Par_apartment = "N_Квартира";
            if (oldProject == true) { N_Par_apartment = "квартира"; }
            string N_Par_apartnum = "N_Кв.Номер";
            if (oldProject == true) { N_Par_apartnum = "квартира.номер"; }
            string N_Par_livingroom = "N_Кв.Комната.Жилая";
            if (oldProject == true) { N_Par_livingroom = "комната.жилая"; }
            string N_Par_apsqo = "N_Кв.Площадь.Общая";
            if (oldProject == true) { N_Par_apsqo = "квартира.площадь.общая"; }
            string N_Par_apsqok = "N_Кв.Площадь.ОбщаяСКоэффициентом";
            if (oldProject == true) { N_Par_apsqok = "Квартира.Площадь.ОбщаяСКоэффициентом"; }
            string N_Par_apsq = "N_Кв.Площадь";
            if (oldProject == true) { N_Par_apsq = "Квартира.Площадь"; }
            string N_Par_apsqb = "N_Кв.Площадь.Балконы";
            if (oldProject == true) { N_Par_apsqb = "Квартира.Площадь.Балконы"; }
            string N_Par_apsqbk = "N_Кв.Площадь.БалконыСКоэффициентом";
            if (oldProject == true) { N_Par_apsqbk = "Квартира.Площадь.БалконыСКоэффициентом"; }
            string N_Par_apsqliv = "N_Кв.Площадь.Жилая";
            if (oldProject == true) { N_Par_apsqliv = "квартира.площадь.жилая"; }
            string N_Par_aprn = "N_Кв.Комнаты.Количество";
            if (oldProject == true) { N_Par_aprn = "Квартира.Комнаты.Количество"; }


            List<Room> rooms = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_Rooms)   //фильтр по категории Помещения
                                                                         .WhereElementIsNotElementType()    //фильтр только экземпляры
                                                                         .Cast<Room>()                     //элементы категории Помещения
                                                                         .ToList();                         //формируем список
            List<Room> arooms = new List<Room>();

            int ec = 0; //счетчик неразмещенных помещений

            foreach (Room room in rooms) //проверка наличия неразмещенных помещений
            {
                double area = room.get_Parameter(BuiltInParameter.ROOM_AREA).AsDouble();
                if (area == 0) { ec++; }
            }

            if (ec > 0) //если есть неразмещенные помещения - прерываем процесс
            {
                TaskDialog.Show("Ошибка", "В проекте присутствуют неразмещенные или избыточные помещения в количестве " + ec + " шт. Необходимо скорректировать модель.");
                return Result.Failed;
            }

            int ap = 0; //счетчик количества помещений с включенным параметром N_Квартира

            foreach (Room room in rooms) //проверка наличия квартир
            {
                int apart = room.LookupParameter(N_Par_apartment).AsInteger();
                if (apart == 1) { ap++; arooms.Add(room); }
            }

            if (ap == 0) //если нет квартир - прерываем процесс
            {
                TaskDialog.Show("Ошибка", "В проекте отсутствуют помещения с включенным параметром " + N_Par_apartment + ". Необходимо скорректировать модель.");
                return Result.Failed;
            }

            // Диалоговое окно

            ShowInputDialog(out string names1, out string names2, out bool recalc);
            if (names1 == "Отменено") { return Result.Failed; } //если отмена в диалоговом окне - прерываем процесс
            
            string[] n1 = names1.Split(',');
            string[] n2 = names2.Split(',');
            
            //Округлятор

            if (recalc == true) //если активна галочка Перерасчета - запускаем транзакцию
            {
                using (Transaction transaction = new Transaction(doc))
                {
                    transaction.Start("TNov - Округлятор");

                    foreach (Room room in rooms) //проверка наличия неразмещенных помещений
                    {
                        double area = room.get_Parameter(BuiltInParameter.ROOM_AREA).AsDouble() * 0.3048 * 0.3048;
                        double areaR = Math.Round(area, 1);
                        string name = room.Name;
                        double k = 1;
                        foreach (string n in n1) { if (name.Contains(n)) { k = 0.5; } }
                        foreach (string n in n2) { if (name.Contains(n)) { k = 0.3; } }
                        double areaRK = Math.Round((areaR * k + 0.000001), 1);
                        room.LookupParameter(N_Par_sq)?.Set(areaR);
                        room.LookupParameter(N_Par_sqk)?.Set(areaRK);
                    }

                    transaction.Commit();
                }
            }

            //Квартирография

            int failedrooms = 0;

            foreach (Room aroom in arooms)
            {
                string apart = aroom.LookupParameter(N_Par_apartnum).AsValueString();
                if (apart == "") { failedrooms++; }
            }

            if (failedrooms > 0) //если у некоторых помещений квартир не заполнен параметр N_Кв.Номер - прерываем процесс
            {
                TaskDialog.Show("Ошибка", "В проекте присутствуют помещения квартир с незаполненным параметром " + N_Par_apartnum + ". Необходимо скорректировать модель.");
                return Result.Failed;
            }

            var aroomssortbynum = from aroom in arooms //сортированный список помещений по номеру квартиры
                              orderby aroom.LookupParameter(N_Par_apartnum).AsValueString()
                                select aroom;

            var aparts = from aroom in aroomssortbynum //список квартир
                         group aroom by aroom.LookupParameter(N_Par_apartnum).AsValueString();

            using (Transaction transaction = new Transaction(doc))
            {
                transaction.Start("TNov - Квартирография");
                
                //N_Кв.Площадь.Общая
                foreach (var apart in aparts) //проходим по каждой квартире в списке квартир
                {
                    double apsqo = 0; //объявляем переменную для заполнения значения параметра N_Кв.Площадь.Общая
                    
                    foreach (var aroom in apart) //проходим по каждой комнате в квартире
                    {
                        double sq = aroom.LookupParameter(N_Par_sq).AsDouble(); //объявляем переменную, получаем площадь каждого помещения в квартире
                        apsqo = apsqo + sq; //добавляем значение площади помещения к общей площади квартиры
                    }
                    foreach (var aroom in apart) //проходим по каждой комнате в квартире
                    {
                        Autodesk.Revit.DB.Parameter param_apsqo = aroom.LookupParameter(N_Par_apsqo); //получаем параметр по имени параметра
                        param_apsqo.Set(apsqo / 0.3048 / 0.3048); //назначаем параметр каждому помещению в квартире
                    }
                }
                //N_Кв.Площадь.ОбщаяСКоэффициентом
                foreach (var apart in aparts)
                {
                    double apsqok = 0;

                    foreach (var aroom in apart)
                    {
                        double sqk = aroom.LookupParameter(N_Par_sqk).AsDouble(); //получаем площадь с коэфф каждого помещения в квартире
                        apsqok = apsqok + sqk;
                    }
                    foreach (var aroom in apart)
                    {
                        Autodesk.Revit.DB.Parameter param_apsqok = aroom.LookupParameter(N_Par_apsqok);
                        param_apsqok.Set(apsqok / 0.3048 / 0.3048); //назначаем параметр каждому помещению в квартире
                    }
                }
                //N_Кв.Площадь
                foreach (var apart in aparts)
                {
                    double apsq = 0;

                    foreach (var aroom in apart)
                    {
                        double sq = aroom.LookupParameter(N_Par_sq).AsDouble(); //получаем площадь каждого помещения в квартире, исключая летние
                        string name = aroom.Name;
                        foreach (string n in n1) { if (name.Contains(n)) { sq = 0; } }
                        foreach (string n in n2) { if (name.Contains(n)) { sq = 0; } }
                        apsq = apsq + sq;
                    }
                    foreach (var aroom in apart)
                    {
                        Autodesk.Revit.DB.Parameter param_apsq = aroom.LookupParameter(N_Par_apsq);
                        param_apsq.Set(apsq / 0.3048 / 0.3048); //назначаем параметр каждому помещению в квартире
                    }
                }
                //N_Кв.Площадь.Балконы
                foreach (var apart in aparts)
                {
                    double apsqb = 0;

                    foreach (var aroom in apart)
                    {
                        double sqb = 0; //получаем площадь каждого летнего помещения в квартире
                        string name = aroom.Name;
                        foreach (string n in n1) { if (name.Contains(n)) { sqb = aroom.LookupParameter(N_Par_sq).AsDouble(); } }
                        foreach (string n in n2) { if (name.Contains(n)) { sqb = aroom.LookupParameter(N_Par_sq).AsDouble(); } }
                        apsqb = apsqb + sqb;
                    }
                    foreach (var aroom in apart)
                    {
                        Autodesk.Revit.DB.Parameter param_apsqb = aroom.LookupParameter(N_Par_apsqb);
                        param_apsqb.Set(apsqb / 0.3048 / 0.3048); //назначаем параметр каждому помещению в квартире
                    }
                }
                //N_Кв.Площадь.БалконыСКоэффициентом
                foreach (var apart in aparts)
                {
                    double apsqbk = 0;

                    foreach (var aroom in apart)
                    {
                        double sqbk = 0; //получаем площадь с коэффициентом каждого летнего помещения в квартире
                        string name = aroom.Name;
                        foreach (string n in n1) { if (name.Contains(n)) { sqbk = aroom.LookupParameter(N_Par_sqk).AsDouble(); } }
                        foreach (string n in n2) { if (name.Contains(n)) { sqbk = aroom.LookupParameter(N_Par_sqk).AsDouble(); } }
                        apsqbk = apsqbk + sqbk;
                    }
                    foreach (var aroom in apart)
                    {
                        Autodesk.Revit.DB.Parameter param_apsqbk = aroom.LookupParameter(N_Par_apsqbk);
                        param_apsqbk.Set(apsqbk / 0.3048 / 0.3048); //назначаем параметр каждому помещению в квартире
                    }
                }
                //N_Кв.Площадь.Жилая
                foreach (var apart in aparts)
                {
                    double apsqliv = 0;

                    foreach (var aroom in apart)
                    {
                        double sqliv = 0; //получаем площадь каждого жилого помещения в квартире
                        int livingroom = aroom.LookupParameter(N_Par_livingroom).AsInteger();
                        if (livingroom == 1) { sqliv = aroom.LookupParameter(N_Par_sq).AsDouble(); }
                        apsqliv = apsqliv + sqliv;
                    }
                    foreach (var aroom in apart)
                    {
                        Autodesk.Revit.DB.Parameter param_apsqliv = aroom.LookupParameter(N_Par_apsqliv);
                        param_apsqliv.Set(apsqliv / 0.3048 / 0.3048); //назначаем параметр каждому помещению в квартире
                    }
                }
                //N_Кв.Комнаты.Количество
                foreach (var apart in aparts)
                {
                    double aprn = 0;

                    foreach (var aroom in apart)
                    {
                        int livingroom = aroom.LookupParameter(N_Par_livingroom).AsInteger(); //определяем, является ли комната жилой
                        if (livingroom == 1) { aprn++; }
                    }
                    foreach (var aroom in apart)
                    {
                        Autodesk.Revit.DB.Parameter param_aprn = aroom.LookupParameter(N_Par_aprn);
                        param_aprn.Set(aprn); //назначаем параметр каждому помещению в квартире
                    }
                }
                transaction.Commit();
                
            }

            return Result.Succeeded;
        }
    }
}
