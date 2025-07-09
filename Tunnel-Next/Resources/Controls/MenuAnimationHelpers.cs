using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;

namespace Tunnel_Next.Resources.Controls
{
    /// <summary>
    /// 将菜单项在菜单中的位置转换为动画延迟时间
    /// </summary>
    public class PositionToDelayConverter : IValueConverter
    {
        /// <summary>
        /// 将菜单项在列表中的索引位置转换为时间跨度
        /// </summary>
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // 获取菜单项
            var menuItem = value as MenuItem;
            if (menuItem == null) return TimeSpan.Zero;
            
            // 尝试获取其在父容器中的索引
            var parent = VisualTreeHelper.GetParent(menuItem);
            while (parent != null && !(parent is ItemsControl))
            {
                parent = VisualTreeHelper.GetParent(parent);
            }
            
            if (parent is ItemsControl itemsControl)
            {
                var items = itemsControl.Items;
                for (int i = 0; i < items.Count; i++)
                {
                    if (items[i] == menuItem)
                    {
                        // 每个项目的延迟递增，但保持总体动画快速
                        return TimeSpan.FromMilliseconds(i * 25); // 25毫秒间隔
                    }
                }
            }
            
            // 默认延迟
            return TimeSpan.Zero;
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