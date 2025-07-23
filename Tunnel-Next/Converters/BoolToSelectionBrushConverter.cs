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
                // 选中状态使用淡蓝色背景
                return new SolidColorBrush(Color.FromRgb(217, 240, 255));
            }
            else
            {
                // 未选中状态使用白色背景
                return new SolidColorBrush(Colors.White);
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
} 