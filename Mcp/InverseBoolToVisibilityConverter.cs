using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace TNov.Mcp
{
    /// <summary>true → Collapsed, false → Visible (например, «файлов нет»).</summary>
    public sealed class InverseBoolToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
            value is bool b && b ? Visibility.Collapsed : Visibility.Visible;

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
            throw new NotSupportedException();
    }
}
