using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Electrical;
using Autodesk.Revit.UI;
using Newtonsoft.Json;
using System.IO;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using TNov.main;

namespace TNov
{

    [Transaction(TransactionMode.Manual)]
    public class CableWaysSettings : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            string TNovClassName = "Способы прокладки"; DateTime dateTime = DateTime.Now;
            //подключение приложения и документа
            if (RevitAPI.UiApplication == null) { RevitAPI.Initialize(commandData); }
            UIDocument uidoc = RevitAPI.UiDocument; Autodesk.Revit.DB.Document doc = RevitAPI.Document;
            UIApplication uiApp = RevitAPI.UiApplication; Autodesk.Revit.ApplicationServices.Application rvtApp = uiApp.Application;
            string TNovClassName1 = TNovClassName + ".Настройки";

            //проверка подключения, запись в журнал
            bool check = false; servercheck sc = new servercheck(in TNovClassName1, out check); if (check == false) { return Result.Failed; }

            // создание log - файла
            Logger.Initialize(TNovClassName);

            //Вьюмодель

            var viewModel = new CableWaysViewModel();
            // Десериализация
            bool forProject = true;
            json js = new json(in TNovClassName, in forProject, out bool canserialize, out string jsonpath);
            if (canserialize)
            {
                viewModel = JsonConvert.DeserializeObject<CableWaysViewModel>(File.ReadAllText(jsonpath));
                Logger.Log("Десериализация прошла успешно",1);
            }
            var wpfview = new CableWaysWPF(viewModel); //окно настроек открывается в отдельной функции
            viewModel.CloseRequest += (s, e) => wpfview.Close();
            bool? ok = wpfview.ShowDialog();
            if (ok != null && ok == true) { } else { Logger.Log("Отменено пользователем. Завершение работы.", 3); return Result.Cancelled; }

            //Сериализация
            try
            {
                File.WriteAllText(jsonpath, JsonConvert.SerializeObject(viewModel));
                Logger.Log("Сериализация прошла успешно",1);
            }
            catch (Exception ex) { Logger.Log("Ошибка при сериализации: " + ex.Message,4); }

            Logger.Log("Завершение работы.", 5);



            return Result.Succeeded;
        }
        }
}
