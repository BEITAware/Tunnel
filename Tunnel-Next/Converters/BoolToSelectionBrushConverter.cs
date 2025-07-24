using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace Tunnel_Next.Converters
{
    /// <summary>
    /// 将布尔值转换为选择背景画刷的转换器
    /// </summary>
    public class BoolToSelectionBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool isSelected = (bool)value;

            if (isSelected)
            {
                // 选中状态使用蓝色边框
                return Color.FromRgb(68, 102, 255);
            }
            else
            {
                // 未选中状态使用透明边框
                return Color.FromArgb(0, 255, 255, 255);
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
} 