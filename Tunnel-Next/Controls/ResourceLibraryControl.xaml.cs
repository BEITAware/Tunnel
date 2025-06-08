using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Tunnel_Next.Models;

namespace Tunnel_Next.Controls
{
    /// <summary>
    /// 资源库控件
    /// </summary>
    public partial class ResourceLibraryControl : UserControl
    {
        /// <summary>
        /// 资源项目被选择事件
        /// </summary>
        public event EventHandler<ResourceLibraryItem>? ResourceItemSelected;

        /// <summary>
        /// 资源项目被双击事件
        /// </summary>
        public event EventHandler<ResourceLibraryItem>? ResourceItemDoubleClicked;

        /// <summary>
        /// 刷新请求事件
        /// </summary>
        public event EventHandler? RefreshRequested;

        /// <summary>
        /// 导入请求事件
        /// </summary>
        public event EventHandler? ImportRequested;

        public ResourceLibraryControl()
        {
            InitializeComponent();
            LoadResourceItems();
        }

        /// <summary>
        /// 加载资源项目
        /// </summary>
        public void LoadResourceItems()
        {
            try
            {
                // 清除现有的图片文件项
                ImageFilesNode.Items.Clear();

                // 从DataContext获取资源项目
                if (DataContext is ViewModels.MainViewModel viewModel)
                {
                    foreach (var item in viewModel.ResourceLibraryItems)
                    {
                        var treeItem = CreateResourceTreeItem(item);
                        ImageFilesNode.Items.Add(treeItem);
                    }

                    // 更新空状态显示
                    EmptyStatePanel.Visibility = viewModel.ResourceLibraryItems.Count == 0
                        ? Visibility.Visible
                        : Visibility.Collapsed;
                }
            }
            catch (Exception ex)
            {
            }
        }

        /// <summary>
        /// 创建资源树项目
        /// </summary>
        private TreeViewItem CreateResourceTreeItem(ResourceLibraryItem item)
        {
            var treeItem = new TreeViewItem
            {
                Tag = item,
                ToolTip = item.ToolTip
            };

            // 创建头部内容
            var headerPanel = new StackPanel { Orientation = Orientation.Horizontal };

            // 添加缩略图
            if (item.Thumbnail != null)
            {
                var image = new Image
                {
                    Source = item.Thumbnail,
                    Width = 16,
                    Height = 16,
                    Margin = new Thickness(0, 0, 5, 0)
                };
                headerPanel.Children.Add(image);
            }
            else
            {
                // 添加默认图标
                var icon = new TextBlock
                {
                    Text = GetIconForItemType(item.ItemType),
                    Margin = new Thickness(0, 0, 5, 0),
                    VerticalAlignment = VerticalAlignment.Center
                };
                headerPanel.Children.Add(icon);
            }

            // 添加文件名
            var nameText = new TextBlock
            {
                Text = item.Name,
                VerticalAlignment = VerticalAlignment.Center
            };
            headerPanel.Children.Add(nameText);

            treeItem.Header = headerPanel;

            return treeItem;
        }

        /// <summary>
        /// 获取项目类型对应的图标
        /// </summary>
        private string GetIconForItemType(ResourceItemType itemType)
        {
            return itemType switch
            {
                ResourceItemType.Image => "[IMG]",
                ResourceItemType.Preset => "[SET]",
                ResourceItemType.Template => "[TPL]",
                ResourceItemType.Material => "[MAT]",
                ResourceItemType.Folder => "[DIR]",
                _ => "[FILE]"
            };
        }

        /// <summary>
        /// 资源树选择变化
        /// </summary>
        private void ResourceTreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (e.NewValue is TreeViewItem treeItem && treeItem.Tag is ResourceLibraryItem item)
            {
                ResourceItemSelected?.Invoke(this, item);
            }
        }

        /// <summary>
        /// 资源树双击
        /// </summary>
        private void ResourceTreeView_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (ResourceTreeView.SelectedItem is TreeViewItem treeItem && treeItem.Tag is ResourceLibraryItem item)
            {
                ResourceItemDoubleClicked?.Invoke(this, item);
            }
        }

        /// <summary>
        /// 刷新按钮点击
        /// </summary>
        private void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            RefreshRequested?.Invoke(this, EventArgs.Empty);
            LoadResourceItems();
        }

        /// <summary>
        /// 导入按钮点击
        /// </summary>
        private void ImportButton_Click(object sender, RoutedEventArgs e)
        {
            ImportRequested?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// 更新资源项目显示
        /// </summary>
        public void UpdateResourceItems()
        {
            LoadResourceItems();
        }
    }
}
