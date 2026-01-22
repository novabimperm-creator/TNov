using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using Autodesk.Revit.UI;
using Autodesk.Revit.Attributes;
using System.Windows.Threading;
using System.Threading;
using Newtonsoft.Json;
using System.IO;
using TNov.main;

namespace TNov
{
    [Transaction(TransactionMode.Manual)]
    public class Fixstructurefile : IExternalCommand
    {
        private TNovProgressBar fixProgressBar;
        private void ThreadStartingPoint()
        {
            this.fixProgressBar = new TNovProgressBar();
            this.fixProgressBar.Show();
            Dispatcher.Run();
        }
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            string TNovClassName = "Ускорить файл КЖ"; DateTime dateTime = DateTime.Now;
            //подключение приложения и документа
            if (RevitAPI.UiApplication == null) { RevitAPI.Initialize(commandData); }
            UIDocument uidoc = RevitAPI.UiDocument; Document doc = RevitAPI.Document;
            UIApplication uiApp = RevitAPI.UiApplication; Autodesk.Revit.ApplicationServices.Application rvtApp = uiApp.Application;
            
            //проверка подключения, запись в журнал
            bool check = false; servercheck sc = new servercheck(in TNovClassName, out check); if (check == false) { return Result.Failed; }

            // создание log - файла
            Logger.Initialize(TNovClassName);
            

            var viewModel0 = new aboutViewModel();
            
            string jsonpath0 = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "TNovClient/TNovSettings.json"); 
            viewModel0 = JsonConvert.DeserializeObject<aboutViewModel>(File.ReadAllText(jsonpath0));
            if (viewModel0.extendedLogs)
            
            {
                var qViewModel = new qwindow280ViewModel();
                qViewModel.headtxt = "Включены расширенные логи. " +
                    "Плагин будет работать медленнее, но соберет больше данных. " +
                    "Выключить расширенные логи для ускорения работы?";
                var qwpfview = new qwindow280(qViewModel);
                qViewModel.CloseRequest += (s, e) => qwpfview.Close();
                bool? qok = qwpfview.ShowDialog();
                if (qok != null && qok == true) { Logger.TurnOffExtendedLogs(); } else Logger.Log( "Расширенные логи вкл", 2);
            }

            Logger.Log("Сбор элементов",1);
            //получаем все типы арматуры
            List<RebarBarType> rebarTypes = new FilteredElementCollector(doc)
                .WhereElementIsElementType()
                .OfClass(typeof(RebarBarType))
                .Cast<RebarBarType>()
                .ToList();
            Logger.Log("Найдены типы арматуры в кол-ве "+ rebarTypes.Count.ToString()+" шт",1);
            
            if (rebarTypes.Count == 0)
            {
                new infowindow280("В данной модели отсутствуют типы арматурных стержней!").ShowDialog();
                Logger.Log("Отсутствуют типы арматурных стержней. Завершение работы.", 3);
                return Result.Failed;
            }

            Logger.Log("Собираем общие параметры проекта, добавленные для арматуры по типу",1);
            Dictionary<string, _MyProjectSharedParameter> projectParamsStorage = new Dictionary<string, _MyProjectSharedParameter>();
            RebarBarType firstBarType = rebarTypes.First();
            foreach (Parameter param in firstBarType.ParametersMap)
            {
                string paramName = param.Definition.Name;
                if (!param.IsShared) continue;
                _MyProjectSharedParameter mpsp = new _MyProjectSharedParameter(param, doc);
                projectParamsStorage.Add(paramName, mpsp);
                Logger.Log("Общий параметр найден: "+paramName,2);
            }

            Logger.Log("Запоминаем типы арматуры",1);
            //запоминаем все типы арматуры со значениями параметров
            List<_MyRebarType> myrebarTypes = new List<_MyRebarType>();

            foreach (RebarBarType rbt in rebarTypes)
            {
                Logger.Log("Тип " + rbt.Name,2);
                try
                {
                    ParameterMap parameterMap = rbt.ParametersMap; //обход ошибки в API
                }
                catch (Exception ex) 
                {
                    Logger.Log("   ошибка: "+ex.Message,4); continue;
                }
                _MyRebarType mrt = new _MyRebarType(rbt);
                myrebarTypes.Add(mrt);
                Logger.Log("   _MyRebarType сохранен",2);
            }

            Logger.Log("Открываем ФОП",1);
            DefinitionFile deffile = null;
            try
            {
                deffile = commandData.Application.Application.OpenSharedParameterFile();
            }
            catch
            {
                var info1 = new infowindow280("Не найден файл общих параметров!"); info1.ShowDialog();
                string commandText = @"https://portal.talan.group/knowledge/proektirovanie/startraboty/";
                var proc = new System.Diagnostics.Process();
                proc.StartInfo.FileName = commandText;
                proc.StartInfo.UseShellExecute = true;
                proc.Start();
                Logger.Log("Не найден файл общих параметров. Завершение работы.",3);
                return Result.Cancelled;
            }

            if (deffile == null)
            {
                var info1 = new infowindow280("Некорректный файл общих параметров!"); info1.ShowDialog();
                Logger.Log("Некорректный файл общих параметров. Завершение работы.",3);
                return Result.Cancelled;
            }

            int allcount=projectParamsStorage.Count;

            Thread thread = new Thread(new ThreadStart(this.ThreadStartingPoint));
            thread.SetApartmentState(ApartmentState.STA);
            thread.IsBackground = true;
            thread.Start();
            Thread.Sleep(100);

            int PBCount = 0;
            this.fixProgressBar.TNov_ProgressBar.Dispatcher.Invoke<double>((Func<double>)(() => this.fixProgressBar.TNov_ProgressBar.Minimum = (double)PBCount));
            this.fixProgressBar.TNov_ProgressBar.Dispatcher.Invoke<string>((Func<string>)(() => this.fixProgressBar.value.Text = PBCount.ToString()));
            this.fixProgressBar.TNov_ProgressBar.Dispatcher.Invoke<double>((Func<double>)(() => this.fixProgressBar.TNov_ProgressBar.Maximum = (double)allcount));
            this.fixProgressBar.TNov_ProgressBar.Dispatcher.Invoke<string>((Func<string>)(() => this.fixProgressBar.maxvalue.Text = allcount.ToString()));


            //удаляем параметр проекта (если только 1 категория) или снимаем флажок с категории несущей арматуры (если категорий несколько)
            Logger.Log("Чистим параметры арматуры в проекте",1);
            using (Transaction t = new Transaction(doc))
            {
                Logger.Log("Открываем транзакцию 1 (удалить параметры)",1);
                t.Start("TNov - ускорить файл КЖ (1 этап)");
                {
                    foreach (var kvp in projectParamsStorage)
                    {
                        PBCount++;
                        this.fixProgressBar.TNov_ProgressBar.Dispatcher.Invoke<double>((Func<double>)(() => this.fixProgressBar.TNov_ProgressBar.Value = (double)PBCount));
                        this.fixProgressBar.TNov_ProgressBar.Dispatcher.Invoke<string>((Func<string>)(() => this.fixProgressBar.value.Text = "Удаление параметров "+PBCount.ToString()));


                        _MyProjectSharedParameter myProjectParam = kvp.Value;
                        if (myProjectParam.categories.Count == 1)
                        {
                            //параметр только для несущей арматуры, значит надо удалить целиком
                            //перед этим проверяем, есть ли параметр в ФОП

                            bool checkParamExistsInDefFile = _SharedParamsFileTools.CheckParameterExistsInFile(deffile, myProjectParam.guid);
                            if (!checkParamExistsInDefFile)
                            {
                                _SharedParamsFileTools.AddParameterToDefFile(deffile, "NonTemplate parameters", myProjectParam);
                            }


                            doc.ParameterBindings.Remove(myProjectParam.def);
                            Logger.Log("   Удален: " + myProjectParam.Name, 2);
                        }
                        else
                        {
                            //категорий несколько, надо убрать флажок с категории несущей арматуры
                            myProjectParam.RemoveOrAddFromRebarCategory(doc, firstBarType, false);
                            Logger.Log("   Снят флажок с несущей арматуры: " + myProjectParam.Name, 2);
                        }
                    }
                }
                t.Commit();
                Logger.Log("Закрываем транзакцию 1",1);
            }

            Logger.Log("Параметры удалены, возвращаем обратно", 1);
            PBCount = 0;
            this.fixProgressBar.TNov_ProgressBar.Dispatcher.Invoke<double>((Func<double>)(() => this.fixProgressBar.TNov_ProgressBar.Value = (double)PBCount));


            //возвращаем параметры обратно
            using (Transaction t2 = new Transaction(doc))
            {
                t2.Start("TNov - ускорить файл КЖ (2 этап)");
                Logger.Log("Открываем транзакцию 2 (возвращение параметров)", 1);

                foreach (var kvp in projectParamsStorage)
                {
                    PBCount++;
                    this.fixProgressBar.TNov_ProgressBar.Dispatcher.Invoke<double>((Func<double>)(() => this.fixProgressBar.TNov_ProgressBar.Value = (double)PBCount));
                    this.fixProgressBar.TNov_ProgressBar.Dispatcher.Invoke<string>((Func<string>)(() => this.fixProgressBar.value.Text = "Возвращение параметров " + PBCount.ToString()));

                    _MyProjectSharedParameter myProjectParam = kvp.Value;
                    if (myProjectParam.categories.Count == 1)
                    {
                        //параметр был назначен только несущей арматуре, был удален совсем, значит создаем параметр
                        myProjectParam.AddToProjectParameters(doc, firstBarType);
                        Logger.Log("   Добавлен: " + myProjectParam.Name, 2);
                    }
                    else
                    {
                        //категорий было несколько, возвращаем флажок к категории несущей арматуры
                        myProjectParam.RemoveOrAddFromRebarCategory(doc, firstBarType, true);
                        Logger.Log("   Добавлен флажок для несущей арматуры: " + myProjectParam.Name, 2);
                    }
                }

                t2.Commit();
                Logger.Log("Закрываем транзакцию 2", 1);
            }

            Logger.Log("Восстанавливаем значения параметров", 1);
            PBCount = 0; allcount=myrebarTypes.Count;
            this.fixProgressBar.TNov_ProgressBar.Dispatcher.Invoke<double>((Func<double>)(() => this.fixProgressBar.TNov_ProgressBar.Value = (double)PBCount));
            this.fixProgressBar.TNov_ProgressBar.Dispatcher.Invoke<double>((Func<double>)(() => this.fixProgressBar.TNov_ProgressBar.Maximum = (double)allcount));
            this.fixProgressBar.TNov_ProgressBar.Dispatcher.Invoke<string>((Func<string>)(() => this.fixProgressBar.maxvalue.Text = allcount.ToString()));

            //восстанавливаем значения у типов арматуры
            using (Transaction t3 = new Transaction(doc))
            {
                t3.Start("TNov - ускорить файл КЖ (3 этап)");
                Logger.Log("Открываем транзакцию 3 (возвращение значений)",1);

                foreach (_MyRebarType mrt in myrebarTypes)
                {
                    PBCount++;
                    this.fixProgressBar.TNov_ProgressBar.Dispatcher.Invoke<double>((Func<double>)(() => this.fixProgressBar.TNov_ProgressBar.Value = (double)PBCount));
                    this.fixProgressBar.TNov_ProgressBar.Dispatcher.Invoke<string>((Func<string>)(() => this.fixProgressBar.value.Text = "Возвращение значений " + PBCount.ToString()));

                    RebarBarType rbt = mrt.bartype;
                    Logger.Log("Тип: " + mrt.Name, 2);

                    foreach (Parameter param in rbt.ParametersMap)
                    {
                        string paramName = param.Definition.Name;
                        _MyParameterValue mpv = mrt.ValuesStorage[paramName];
                        if (mpv.IsNull) continue;
                        mpv.SetValue(param);
                        Logger.Log("   Параметр: " + paramName + ", значение " + mpv.ToString(),2);
                    }
                }

                t3.Commit();
                Logger.Log("Закрываем транзакцию 3",1);
            }
            //string endTime = DateTime.Now.ToLongTimeString();
            //string msg = "Выполнено! Время старта: " + startTime + ", окончания: " + endTime;

            this.fixProgressBar.Dispatcher.Invoke((System.Action)(() => this.fixProgressBar.Close()));

            //var info2 = new infowindow280("Успешно! Файл станет быстрее."); info2.ShowDialog();
            //Debug.WriteLine(msg);

            Logger.Log("Завершение работы.",5);
            return Result.Succeeded;
        }
    }
}
