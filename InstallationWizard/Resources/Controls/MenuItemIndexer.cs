using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace InstallationWizard.Resources.Controls
{
    /// <summary>
    /// 为菜单项提供索引的附加属性
    /// </summary>
    public static class MenuItemIndexer
    {
        #region Index 附加属性

        public static readonly DependencyProperty IndexProperty =
            DependencyProperty.RegisterAttached("Index", typeof(int), typeof(MenuItemIndexer), new PropertyMetadata(0));

        public static void SetIndex(DependencyObject obj, int value)
        {
            obj.SetValue(IndexProperty, value);
        }

        public static int GetIndex(DependencyObject obj)
        {
            return (int)obj.GetValue(IndexProperty);
        }

        #endregion

        #region AutoIndex 附加属性

        public static readonly DependencyProperty AutoIndexProperty =
            DependencyProperty.RegisterAttached("AutoIndex", typeof(bool), typeof(MenuItemIndexer), 
                new PropertyMetadata(false, OnAutoIndexChanged));

        public static void SetAutoIndex(DependencyObject obj, bool value)
        {
            obj.SetValue(AutoIndexProperty, value);
        }

        public static bool GetAutoIndex(DependencyObject obj)
        {
            return (bool)obj.GetValue(AutoIndexProperty);
        }

        private static void OnAutoIndexChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ContextMenu contextMenu && (bool)e.NewValue)
            {
                // 移除任何现有的处理程序以避免重复注册
                contextMenu.Opened -= ContextMenu_Opened;
                contextMenu.Opened += ContextMenu_Opened;
            }
        }

        private static void ContextMenu_Opened(object sender, RoutedEventArgs e)
        {
            if (sender is ContextMenu menu)
            {
                int index = 0;
                foreach (var item in menu.Items)
                {
                    if (item is MenuItem menuItem)
                    {
                        // 设置索引附加属性
                        SetIndex(menuItem, index);
                        index++;
                    }
                }
            }
        }

        #endregion
    }
} 