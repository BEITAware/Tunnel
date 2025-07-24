using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Tunnel_Next.UtilityTools.BatchProcessor.Converters
{
    /// <summary>
    /// 布尔值到可见性转换器，支持反转
    /// </summary>
    public class BooleanToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool boolValue = value is bool b && b;
            bool invert = parameter?.ToString()?.ToLowerInvariant() == "invert";

            if (invert)
                boolValue = !boolValue;

            return boolValue ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool isVisible = value is Visibility visibility && visibility == Visibility.Visible;
            bool invert = parameter?.ToString()?.ToLowerInvariant() == "invert";

            if (invert)
                isVisible = !isVisible;

            return isVisible;
        }
    }

    /// <summary>
    /// 计数到可见性转换器
    /// </summary>
    public class CountToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            int count = value is int i ? i : 0;
            return count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
