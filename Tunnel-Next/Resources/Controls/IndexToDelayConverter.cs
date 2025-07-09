using System;
using System.Globalization;
using System.Windows.Data;

namespace Tunnel_Next.Resources.Controls
{
    /// <summary>
    /// 将菜单项索引转换为动画延迟时间
    /// </summary>
    public class IndexToDelayConverter : IValueConverter
    {
        /// <summary>
        /// 将菜单项索引转换为延迟时间
        /// </summary>
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // 默认延迟
            if (value == null || !(value is int index))
                return TimeSpan.Zero;

            // 每个项目延迟20毫秒
            return TimeSpan.FromMilliseconds(index * 20);
        }

        /// <summary>
        /// 不支持反向转换
        /// </summary>
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
} 