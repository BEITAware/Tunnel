using System;
using System.Windows;
using System.Windows.Controls;

namespace InstallationWizard.Resources.Controls
{
    /// <summary>
    /// 上下文菜单扩展方法
    /// </summary>
    public static class ContextMenuExtensions
    {
        /// <summary>
        /// 当菜单打开时，为每个菜单项添加索引
        /// </summary>
        public static void ContextMenu_Opened(object sender, RoutedEventArgs e)
        {
            if (sender is ContextMenu menu)
            {
                int index = 0;
                foreach (var item in menu.Items)
                {
                    if (item is MenuItem menuItem)
                    {
                        // 使用Tag属性存储菜单项索引
                        menuItem.Tag = index.ToString();
                        index++;
                    }
                }
            }
        }
    }
} 