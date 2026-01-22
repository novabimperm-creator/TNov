using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TNov
{
    public class MEPSpecTools
    {
        public static void MEPSpecOVVKBaseParams(in string mark, in string fileName, MEPSpecOVVKParamsViewModel viewModel2)
        {
            //базовые значения
            if (mark.Contains("Трубы"))
            {
                viewModel2.naimPrefix2 = " ø";
                if (fileName.Contains("-ОВ") || fileName.Contains("_ОВ")) viewModel2.naimPrefix3 = "х";
                if (fileName.Contains("-ОВ") || fileName.Contains("_ОВ")) viewModel2.naimPar3 = "ADSK_Толщина стенки";
                if (mark.Contains("Днар")) viewModel2.naimPar2 = "Внешний диаметр";
                else viewModel2.naimPar2 = "Диаметр";
                viewModel2.countK = "1.1";
                viewModel2.countPar = "Длина";
            }
            else if (mark.Contains("Материалы изоляции труб"))
            {
                viewModel2.naimPrefix2 = ", b=";
                viewModel2.naimPar2 = "Толщина изоляции";
                viewModel2.naimPrefix3 = " для ";
                viewModel2.naimPar3 = "Размер трубы";
                viewModel2.countPar = "Длина";
                viewModel2.countK = "1.3";
                if (mark.Contains("Цилиндры"))
                {
                    viewModel2.countK = "1";
                    viewModel2.countPar = "Объем";
                }
                else if (mark.Contains("Трубки"))
                {
                    viewModel2.countK = "1.1";
                }
            }
            else if (mark.Contains("Гибкие трубы"))
            {
                viewModel2.naimPrefix2 = " ø";
                viewModel2.naimPar2 = "Диаметр";
                viewModel2.countPar = "Длина";
                if (mark.Contains("Подводка стальная")) viewModel2.naimPar2 = "Внешний диаметр";
            }
            else if (mark.Contains("Воздуховоды"))
            {
                viewModel2.naimPrefix2 = " ";
                viewModel2.naimPar2 = "Размер";
                viewModel2.countPar = "Длина";
                if (mark.Contains("Пластик")) { }
                else
                {
                    viewModel2.naimPrefix3 = ", b=";
                    viewModel2.naimPar3 = "ADSK_Толщина стенки";
                    viewModel2.naimPrefix4 = ", класс герметичности ";
                    viewModel2.naimPar4 = "Класс герметичности";
                }
            }
            else if (mark.Contains("Материалы изоляции воздуховодов"))
            {
                viewModel2.naimPrefix2 = " ";
                if (mark.Contains("Огнезащита")) { }
                else viewModel2.naimPar2 = "Толщина изоляции";
            }
            else if (mark.Contains("Гибкие воздуховоды"))
            {
                viewModel2.naimPrefix2 = " ";
                viewModel2.naimPar2 = "Размер";
                viewModel2.countPar = "Длина";
            }
            else if (mark.Contains("Соединительные детали воздуховодов"))
            {
                viewModel2.naimPrefix2 = " ";
                viewModel2.naimPar2 = "ADSK_Размер_УголПоворота";
                viewModel2.naimPrefix3 = " ";
                viewModel2.naimPar2 = "Размер";
            }
        }
    }
}
