using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Tunnel_Next.Models;
using Tunnel_Next.ViewModels;

namespace Tunnel_Next.Controls
{
    /// <summary>
    /// 胶片预览控件
    /// </summary>
    public partial class FilmPreviewControl : UserControl
    {
        /// <summary>
        /// 胶片项目被选择事件
        /// </summary>
        public event EventHandler<FilmPreviewItem>? FilmItemSelected;

        /// <summary>
        /// 胶片项目被双击事件
        /// </summary>
        public event EventHandler<FilmPreviewItem>? FilmItemDoubleClicked;

        /// <summary>
        /// 刷新请求事件
        /// </summary>
        public event EventHandler? RefreshRequested;

        /// <summary>
        /// 重新生成缩略图事件
        /// </summary>
        public event EventHandler<FilmPreviewItem>? RegenerateThumbnailRequested;

        /// <summary>
        /// 删除节点图事件
        /// </summary>
        public event EventHandler<FilmPreviewItem>? DeleteNodeGraphRequested;

        public FilmPreviewControl()
        {
            InitializeComponent();

            // 添加鼠标滚轮事件处理
            this.PreviewMouseWheel += FilmPreviewControl_PreviewMouseWheel;
        }

        /// <summary>
        /// 处理鼠标滚轮事件，实现水平滚动
        /// </summary>
        private void FilmPreviewControl_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            // 查找ScrollViewer
            var scrollViewer = FindScrollViewer(this);
            if (scrollViewer != null)
            {
                // 水平滚动
                double scrollAmount = e.Delta > 0 ? -50 : 50;
                scrollViewer.ScrollToHorizontalOffset(scrollViewer.HorizontalOffset + scrollAmount);
                e.Handled = true;
            }
        }

        /// <summary>
        /// 查找ScrollViewer控件
        /// </summary>
        private ScrollViewer? FindScrollViewer(DependencyObject parent)
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is ScrollViewer scrollViewer)
                {
                    return scrollViewer;
                }

                var result = FindScrollViewer(child);
                if (result != null)
                {
                    return result;
                }
            }
            return null;
        }

        /// <summary>
        /// 胶片列表选择变化
        /// </summary>
        private void FilmListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.AddedItems.Count > 0 && e.AddedItems[0] is FilmPreviewItem item)
            {
                FilmItemSelected?.Invoke(this, item);
            }
        }

        /// <summary>
        /// 胶片列表双击
        /// </summary>
        private void FilmListBox_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (FilmListBox.SelectedItem is FilmPreviewItem item)
            {
                FilmItemDoubleClicked?.Invoke(this, item);
            }
        }

        /// <summary>
        /// 刷新按钮点击
        /// </summary>
        private void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            RefreshRequested?.Invoke(this, EventArgs.Empty);
        }



        /// <summary>
        /// 菜单按钮点击
        /// </summary>
        private void ContextMenuButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.ContextMenu != null)
            {
                button.ContextMenu.PlacementTarget = button;
                button.ContextMenu.IsOpen = true;
            }
        }

        /// <summary>
        /// 重新生成缩略图菜单项点击
        /// </summary>
        private void RegenerateThumbnail_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem menuItem &&
                menuItem.DataContext is FilmPreviewItem item)
            {
                RegenerateThumbnailRequested?.Invoke(this, item);
            }
        }

        /// <summary>
        /// 删除节点图菜单项点击
        /// </summary>
        private void DeleteNodeGraph_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem menuItem &&
                menuItem.DataContext is FilmPreviewItem item)
            {
                // 显示确认对话框
                var result = MessageBox.Show(
                    $"确定要删除节点图 '{item.Name}' 吗？\n\n此操作将同时删除节点图文件和缩略图，且无法撤销。",
                    "确认删除",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (result == MessageBoxResult.Yes)
                {
                    DeleteNodeGraphRequested?.Invoke(this, item);
                }
            }
        }
    }

    /// <summary>
    /// 转换器类
    /// </summary>
    public static class Converters
    {
        /// <summary>
        /// Null到Visibility转换器
        /// </summary>
        public static readonly NullToVisibilityConverter NullToVisibilityConverter = new();

        /// <summary>
        /// 零值到Visibility转换器
        /// </summary>
        public static readonly ZeroToVisibilityConverter ZeroToVisibilityConverter = new();
    }

    /// <summary>
    /// Null到Visibility转换器
    /// </summary>
    public class NullToVisibilityConverter : System.Windows.Data.IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            return value == null ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// 零值到Visibility转换器
    /// </summary>
    public class ZeroToVisibilityConverter : System.Windows.Data.IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value is int intValue)
                return intValue == 0 ? Visibility.Visible : Visibility.Collapsed;

            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
