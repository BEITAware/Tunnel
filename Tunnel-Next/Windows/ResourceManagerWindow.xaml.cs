using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Tunnel_Next.Models;
using Tunnel_Next.Services;
using Tunnel_Next.ViewModels;

namespace Tunnel_Next.Windows
{
    /// <summary>
    /// 资源管理器窗口
    /// </summary>
    public partial class ResourceManagerWindow : Window
    {
        private readonly List<ResourceObject> _allResources = new();
        private readonly List<ResourceObject> _selectedItems = new();
        private ResourceObject? _firstSelectedItem;
        private string _searchText = string.Empty;
        private SortMode _currentSortMode = SortMode.Name;
        private ResourceItemType? _currentResourceType;
        private List<ResourceItemType> _availableResourceTypes = new();

        private readonly ResourceScanService? _scanService;
        private readonly ResourceCatalogService? _catalogService;

        public ResourceManagerWindow()
        {
            InitializeComponent();

            // 获取现有的资源服务
            if (Application.Current.MainWindow?.DataContext is ViewModels.MainViewModel mainViewModel)
            {
                _scanService = mainViewModel.ResourceScanService;
                _catalogService = mainViewModel.ResourceCatalogService;
            }

            // 在窗口加载完成后初始化
            Loaded += ResourceManagerWindow_Loaded;
        }

        private void ResourceManagerWindow_Loaded(object sender, RoutedEventArgs e)
        {
            InitializeDynamicTabs();
            LoadResourcesAsync();
        }

        /// <summary>
        /// 显示资源管理器窗口
        /// </summary>
        /// <param name="owner">父窗口</param>
        public static void ShowResourceManager(Window? owner = null)
        {
            var window = new ResourceManagerWindow();
            if (owner != null)
            {
                window.Owner = owner;
            }
            window.Show();
        }

        /// <summary>
        /// 初始化动态Tab
        /// </summary>
        private void InitializeDynamicTabs()
        {
            // 检查UI控件是否已初始化
            if (ResourceTabControl == null) return;

            // 获取所有可扫描的资源类型
            _availableResourceTypes = ResourceTypeRegistry.GetAllTypes()
                .Where(t => t.ScanDelegate != null)
                .OrderBy(t => t.ScanPriority)
                .Select(t => t.Type)
                .ToList();

            // 清空现有的Tab
            ResourceTabControl.Items.Clear();

            // 为每个资源类型创建Tab
            foreach (var resourceType in _availableResourceTypes)
            {
                var tabItem = new TabItem
                {
                    Header = ResourceTypeRegistry.GetDisplayName(resourceType),
                    Style = (Style)FindResource("ResourceTabStyle"),
                    Tag = resourceType
                };
                ResourceTabControl.Items.Add(tabItem);
            }

            // 选择第一个Tab
            if (ResourceTabControl.Items.Count > 0)
            {
                ResourceTabControl.SelectedIndex = 0;
                _currentResourceType = _availableResourceTypes.FirstOrDefault();
            }
        }

        /// <summary>
        /// 加载资源
        /// </summary>
        private async void LoadResourcesAsync()
        {
            try
            {
                if (_scanService != null)
                {
                    // 使用现有的扫描服务加载资源
                    var resources = await _scanService.ScanAllResourcesAsync();
                    _allResources.Clear();
                    _allResources.AddRange(resources);
                }
                else if (_catalogService != null)
                {
                    // 使用目录服务加载资源
                    await _catalogService.LoadCatalogAsync();
                    _allResources.Clear();
                    _allResources.AddRange(_catalogService.Catalog.Resources);
                }

                UpdateResourceDisplay();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ResourceManagerWindow] 加载资源失败: {ex.Message}");
                // 如果加载失败，显示空列表
                UpdateResourceDisplay();
            }
        }

        /// <summary>
        /// 更新资源显示
        /// </summary>
        private void UpdateResourceDisplay()
        {
            // 检查UI控件是否已初始化
            if (ResourceWrapPanel == null) return;

            // 清空当前显示
            ResourceWrapPanel.Children.Clear();

            if (_currentResourceType == null) return;

            // 筛选资源
            var filteredItems = _allResources
                .Where(r => r.ResourceType == _currentResourceType.Value)
                .Where(r => string.IsNullOrEmpty(_searchText) ||
                           r.Name.Contains(_searchText, StringComparison.OrdinalIgnoreCase));

            // 排序
            filteredItems = _currentSortMode switch
            {
                SortMode.Name => filteredItems.OrderBy(r => r.Name),
                SortMode.Time => filteredItems.OrderByDescending(r => r.ModifiedTime),
                SortMode.Size => filteredItems.OrderByDescending(r => r.FileSize),
                _ => filteredItems.OrderBy(r => r.Name)
            };

            // 创建UI元素
            foreach (var item in filteredItems)
            {
                var itemControl = CreateResourceItemControl(item);
                ResourceWrapPanel.Children.Add(itemControl);
            }
        }

        /// <summary>
        /// 创建资源项控件
        /// </summary>
        private Border CreateResourceItemControl(ResourceObject item)
        {
            var border = new Border
            {
                Style = _selectedItems.Contains(item) ?
                    (Style)FindResource("SelectedResourceItemStyle") :
                    (Style)FindResource("ResourceItemStyle"),
                Tag = item
            };

            var stackPanel = new StackPanel();

            // 图标或缩略图
            var icon = new Image
            {
                Width = 48,
                Height = 48,
                Margin = new Thickness(0, 0, 0, 4),
                Source = GetResourceThumbnail(item) ?? GetResourceIcon(item.ResourceType)
            };

            // 名称
            var nameText = new TextBlock
            {
                Text = item.Name,
                FontFamily = new FontFamily("Segoe UI, Microsoft YaHei UI, Arial"),
                FontSize = 10,
                Foreground = (Brush)FindResource("PrimaryForeground"),
                TextAlignment = TextAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis,
                MaxWidth = 100
            };

            stackPanel.Children.Add(icon);
            stackPanel.Children.Add(nameText);
            border.Child = stackPanel;

            // 添加点击事件
            border.MouseLeftButtonDown += (s, e) => ResourceItem_Click(item, e);

            return border;
        }

        /// <summary>
        /// 获取资源缩略图
        /// </summary>
        private ImageSource? GetResourceThumbnail(ResourceObject resource)
        {
            // 如果资源已有缩略图，直接使用
            if (resource.Thumbnail != null)
            {
                return resource.Thumbnail;
            }

            // 对于节点图，使用同文件夹下的{nodegraphname}.png作为缩略图
            if (resource.ResourceType == ResourceItemType.NodeGraph)
            {
                var directory = Path.GetDirectoryName(resource.FilePath);
                var nameWithoutExtension = Path.GetFileNameWithoutExtension(resource.FilePath);
                var thumbnailPath = Path.Combine(directory!, $"{nameWithoutExtension}.png");

                if (File.Exists(thumbnailPath))
                {
                    try
                    {
                        var bitmap = new BitmapImage();
                        bitmap.BeginInit();
                        bitmap.UriSource = new Uri(thumbnailPath);
                        bitmap.DecodePixelWidth = 48; // 限制缩略图大小
                        bitmap.EndInit();
                        return bitmap;
                    }
                    catch
                    {
                        // 如果加载失败，返回null使用默认图标
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// 获取资源类型图标
        /// </summary>
        private ImageSource? GetResourceIcon(ResourceItemType resourceType)
        {
            string iconPath = ResourceTypeRegistry.GetDefaultIconPath(resourceType);

            try
            {
                return new BitmapImage(new Uri(iconPath, UriKind.Relative));
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// 资源项点击事件
        /// </summary>
        private void ResourceItem_Click(ResourceObject item, MouseButtonEventArgs e)
        {
            bool isCtrlPressed = (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control;
            bool isShiftPressed = (Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift;

            if (isShiftPressed && _firstSelectedItem != null)
            {
                // Shift+点击：范围选择
                HandleShiftClick(item);
            }
            else if (isCtrlPressed)
            {
                // Ctrl+点击：切换选中状态，允许多选
                bool isCurrentlySelected = _selectedItems.Contains(item);

                if (isCurrentlySelected)
                {
                    // 如果已选中，则取消选中
                    _selectedItems.Remove(item);
                    if (item == _firstSelectedItem)
                    {
                        _firstSelectedItem = _selectedItems.FirstOrDefault();
                    }
                }
                else
                {
                    // 如果未选中，则添加到选择列表
                    _selectedItems.Add(item);
                    if (_firstSelectedItem == null)
                    {
                        _firstSelectedItem = item;
                    }
                }
            }
            else
            {
                // 普通点击：清除其他选择，只选中当前项
                _selectedItems.Clear();
                _selectedItems.Add(item);
                _firstSelectedItem = item;
            }

            UpdateResourceDisplay();
            UpdatePreview();
        }

        /// <summary>
        /// 处理Shift+点击的范围选择
        /// </summary>
        private void HandleShiftClick(ResourceObject item)
        {
            if (_currentResourceType == null || _firstSelectedItem == null) return;

            // 获取当前显示的资源列表（已筛选和排序）
            var filteredItems = _allResources
                .Where(r => r.ResourceType == _currentResourceType.Value)
                .Where(r => string.IsNullOrEmpty(_searchText) ||
                           r.Name.Contains(_searchText, StringComparison.OrdinalIgnoreCase));

            // 应用相同的排序
            filteredItems = _currentSortMode switch
            {
                SortMode.Name => filteredItems.OrderBy(r => r.Name),
                SortMode.Time => filteredItems.OrderByDescending(r => r.ModifiedTime),
                SortMode.Size => filteredItems.OrderByDescending(r => r.FileSize),
                _ => filteredItems.OrderBy(r => r.Name)
            };

            var currentItems = filteredItems.ToList();
            var firstIndex = currentItems.IndexOf(_firstSelectedItem);
            var currentIndex = currentItems.IndexOf(item);

            if (firstIndex >= 0 && currentIndex >= 0)
            {
                var startIndex = Math.Min(firstIndex, currentIndex);
                var endIndex = Math.Max(firstIndex, currentIndex);

                // 清除当前选择
                _selectedItems.Clear();

                // 选择范围内的项目
                for (int i = startIndex; i <= endIndex; i++)
                {
                    _selectedItems.Add(currentItems[i]);
                }

                // 保持第一个选中项不变
                // _firstSelectedItem 保持原值，用于后续的Shift选择
            }
        }

        /// <summary>
        /// 更新预览
        /// </summary>
        private void UpdatePreview()
        {
            // 检查UI控件是否已初始化
            if (PreviewTitleTextBlock == null || PreviewImage == null ||
                ImagePreviewViewbox == null || NodePreviewControl == null) return;

            if (_selectedItems.Count == 1)
            {
                var selectedItem = _selectedItems.First();
                PreviewTitleTextBlock.Text = selectedItem.Name;

                // 根据资源类型选择预览方式
                if (selectedItem.ResourceType == ResourceItemType.NodeGraph)
                {
                    // 显示节点图预览
                    ShowNodeGraphPreview(selectedItem);
                }
                else
                {
                    // 显示图片预览
                    ShowImagePreview(selectedItem);
                }
            }
            else if (_selectedItems.Count > 1)
            {
                PreviewTitleTextBlock.Text = $"已选择 {_selectedItems.Count} 项";
                HideAllPreviews();
            }
            else
            {
                PreviewTitleTextBlock.Text = "预览";
                HideAllPreviews();
            }

            // 更新扩展操作按钮
            UpdateExtendedOperations();
        }

        /// <summary>
        /// 显示节点图预览
        /// </summary>
        private async void ShowNodeGraphPreview(ResourceObject nodeGraphResource)
        {
            try
            {
                // 隐藏图片预览，显示节点图预览
                ImagePreviewViewbox.Visibility = Visibility.Collapsed;
                NodePreviewControl.Visibility = Visibility.Visible;

                // 加载节点图数据
                if (File.Exists(nodeGraphResource.FilePath))
                {
                    // 获取MainViewModel中的服务
                    if (Application.Current.MainWindow?.DataContext is ViewModels.MainViewModel mainViewModel)
                    {
                        // 使用FileService加载节点图
                        var nodeGraph = await mainViewModel.FileService.LoadNodeGraphAsync(nodeGraphResource.FilePath);
                        if (nodeGraph != null)
                        {
                            // 创建新的NodeEditorViewModel实例用于预览
                            // 使用现有NodeEditor的RevivalScriptManager（通过反射或其他方式）
                            // 为了简化，我们直接创建一个临时的NodeEditorViewModel
                            var nodeEditorViewModel = new NodeEditorViewModel(null);
                            await nodeEditorViewModel.LoadNodeGraphAsync(nodeGraph);
                            NodePreviewControl.DataContext = nodeEditorViewModel;
                        }
                        else
                        {
                            NodePreviewControl.DataContext = null;
                        }
                    }
                    else
                    {
                        NodePreviewControl.DataContext = null;
                    }
                }
                else
                {
                    NodePreviewControl.DataContext = null;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ResourceManagerWindow] 加载节点图预览失败: {ex.Message}");
                // 如果加载失败，显示图片预览（可能有缩略图）
                ShowImagePreview(nodeGraphResource);
            }
        }

        /// <summary>
        /// 显示图片预览
        /// </summary>
        private void ShowImagePreview(ResourceObject resource)
        {
            // 显示图片预览，隐藏节点图预览
            ImagePreviewViewbox.Visibility = Visibility.Visible;
            NodePreviewControl.Visibility = Visibility.Collapsed;

            // 使用缩略图或尝试加载图片
            var thumbnail = GetResourceThumbnail(resource);
            if (thumbnail != null)
            {
                PreviewImage.Source = thumbnail;
            }
            else if (resource.ResourceType == ResourceItemType.Image && File.Exists(resource.FilePath))
            {
                try
                {
                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.UriSource = new Uri(resource.FilePath);
                    bitmap.DecodePixelWidth = 400; // 限制预览图片大小
                    bitmap.EndInit();
                    PreviewImage.Source = bitmap;
                }
                catch
                {
                    PreviewImage.Source = null;
                }
            }
            else
            {
                PreviewImage.Source = null;
            }
        }

        /// <summary>
        /// 隐藏所有预览
        /// </summary>
        private void HideAllPreviews()
        {
            if (ImagePreviewViewbox != null && NodePreviewControl != null)
            {
                ImagePreviewViewbox.Visibility = Visibility.Visible;
                NodePreviewControl.Visibility = Visibility.Collapsed;
                PreviewImage.Source = null;
                NodePreviewControl.DataContext = null;
            }
        }

        /// <summary>
        /// 更新扩展操作按钮
        /// </summary>
        private void UpdateExtendedOperations()
        {
            // 检查UI控件是否已初始化
            if (ExtendedButtonsPanel == null || ExtendedOperationsTitle == null) return;

            // 清空现有按钮
            ExtendedButtonsPanel.Children.Clear();

            if (_selectedItems.Count != 1)
            {
                ExtendedOperationsTitle.Visibility = Visibility.Collapsed;
                return;
            }

            var selectedResource = _selectedItems.First();
            var typeDefinition = ResourceTypeRegistry.GetTypeDefinition(selectedResource.ResourceType);

            if (typeDefinition?.DelegateSet == null)
            {
                ExtendedOperationsTitle.Visibility = Visibility.Collapsed;
                return;
            }

            // 获取所有委托名称，排除基本的四个委托
            var basicDelegates = new HashSet<string> { "Export", "Import", "Delete", "Rename" };
            var extendedDelegates = typeDefinition.DelegateSet.GetDelegateNames()
                .Where(name => !basicDelegates.Contains(name))
                .ToList();

            if (extendedDelegates.Count == 0)
            {
                ExtendedOperationsTitle.Visibility = Visibility.Collapsed;
                return;
            }

            // 显示扩展操作标题
            ExtendedOperationsTitle.Visibility = Visibility.Visible;

            // 为每个扩展委托创建按钮
            foreach (var delegateName in extendedDelegates)
            {
                var button = new Button
                {
                    Content = GetDelegateDisplayName(delegateName),
                    Style = (Style)FindResource("ActionButtonStyle"),
                    Tag = delegateName
                };

                button.Click += ExtendedOperationButton_Click;
                ExtendedButtonsPanel.Children.Add(button);
            }
        }

        /// <summary>
        /// 获取委托的显示名称
        /// </summary>
        private string GetDelegateDisplayName(string delegateName)
        {
            // 可以根据需要添加更多的映射
            return delegateName switch
            {
                "Validate" => "验证",
                "Preview" => "预览",
                "Optimize" => "优化",
                "Convert" => "转换",
                "Backup" => "备份",
                "Restore" => "恢复",
                "Compress" => "压缩",
                "Decompress" => "解压",
                "Encrypt" => "加密",
                "Decrypt" => "解密",
                _ => delegateName // 默认使用原名称
            };
        }

        /// <summary>
        /// 扩展操作按钮点击事件
        /// </summary>
        private async void ExtendedOperationButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button button || button.Tag is not string delegateName)
                return;

            if (_selectedItems.Count != 1)
                return;

            var selectedResource = _selectedItems.First();
            var typeDefinition = ResourceTypeRegistry.GetTypeDefinition(selectedResource.ResourceType);

            if (typeDefinition?.DelegateSet == null)
                return;

            try
            {
                // 尝试获取ResourceOperationDelegate类型的委托
                var operationDelegate = typeDefinition.DelegateSet.GetDelegate<ResourceOperationDelegate>(delegateName);

                if (operationDelegate != null)
                {
                    // 执行操作委托
                    var result = await operationDelegate(selectedResource, null);

                    if (result.Success)
                    {
                        MessageBox.Show($"操作 '{GetDelegateDisplayName(delegateName)}' 执行成功", "成功",
                            MessageBoxButton.OK, MessageBoxImage.Information);

                        // 如果操作可能影响资源，刷新显示
                        UpdateResourceDisplay();
                        UpdatePreview();
                    }
                    else
                    {
                        MessageBox.Show($"操作 '{GetDelegateDisplayName(delegateName)}' 执行失败: {result.ErrorMessage}", "错误",
                            MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
                else
                {
                    // 尝试其他类型的委托
                    var genericDelegate = typeDefinition.DelegateSet.GetDelegate<Delegate>(delegateName);
                    if (genericDelegate != null)
                    {
                        MessageBox.Show($"委托 '{GetDelegateDisplayName(delegateName)}' 存在但类型不匹配，需要自定义处理", "提示",
                            MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    else
                    {
                        MessageBox.Show($"未找到委托 '{delegateName}'", "错误",
                            MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"执行操作 '{GetDelegateDisplayName(delegateName)}' 时发生错误: {ex.Message}", "错误",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // 事件处理方法
        private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (SearchTextBox == null) return;
            _searchText = SearchTextBox.Text;
            UpdateResourceDisplay();
        }

        private void SortComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (SortComboBox == null) return;
            _currentSortMode = SortComboBox.SelectedIndex switch
            {
                0 => SortMode.Name,
                1 => SortMode.Time,
                2 => SortMode.Size,
                _ => SortMode.Name
            };
            UpdateResourceDisplay();
        }

        private void ResourceTabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ResourceTabControl == null) return;

            if (ResourceTabControl.SelectedItem is TabItem selectedTab && selectedTab.Tag is ResourceItemType resourceType)
            {
                _currentResourceType = resourceType;
            }
            else if (ResourceTabControl.SelectedIndex >= 0 && ResourceTabControl.SelectedIndex < _availableResourceTypes.Count)
            {
                _currentResourceType = _availableResourceTypes[ResourceTabControl.SelectedIndex];
            }

            // 清除选择
            _selectedItems.Clear();
            _firstSelectedItem = null;

            UpdateResourceDisplay();
            UpdatePreview();
        }

        // 按钮事件处理
        private async void ExportButton_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedItems.Count == 0)
            {
                MessageBox.Show("请先选择要导出的资源", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            try
            {
                var saveDialog = new Microsoft.Win32.SaveFileDialog
                {
                    Filter = "Zip文件|*.zip",
                    Title = "选择导出位置"
                };

                if (saveDialog.ShowDialog() == true)
                {
                    foreach (var resource in _selectedItems)
                    {
                        var typeDefinition = ResourceTypeRegistry.GetTypeDefinition(resource.ResourceType);
                        var exportDelegate = typeDefinition?.DelegateSet.ExportDelegate;

                        if (exportDelegate != null)
                        {
                            var result = await exportDelegate(resource, saveDialog.FileName);
                            if (!result.Success)
                            {
                                MessageBox.Show($"导出资源 {resource.Name} 失败: {result.ErrorMessage}", "错误",
                                    MessageBoxButton.OK, MessageBoxImage.Error);
                                return;
                            }
                        }
                    }

                    MessageBox.Show("导出完成", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"导出失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void ImportButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var openDialog = new Microsoft.Win32.OpenFileDialog
                {
                    Filter = "Zip文件|*.zip",
                    Title = "选择要导入的资源包"
                };

                if (openDialog.ShowDialog() == true)
                {
                    // 这里需要确定目标目录，可以使用工作文件夹
                    var targetDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "ImportedResources");

                    // 使用默认导入委托
                    var result = await Services.DefaultResourceDelegates.DefaultImportDelegate(openDialog.FileName, targetDirectory);

                    if (result.Success)
                    {
                        MessageBox.Show("导入完成", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
                        // 刷新资源列表
                        LoadResourcesAsync();
                    }
                    else
                    {
                        MessageBox.Show($"导入失败: {result.ErrorMessage}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"导入失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedItems.Count == 0)
            {
                MessageBox.Show("请先选择要删除的资源", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var result = MessageBox.Show($"确定要删除选中的 {_selectedItems.Count} 个资源吗？此操作不可撤销。",
                "确认删除", MessageBoxButton.YesNo, MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    foreach (var resource in _selectedItems.ToList())
                    {
                        var typeDefinition = ResourceTypeRegistry.GetTypeDefinition(resource.ResourceType);
                        var deleteDelegate = typeDefinition?.DelegateSet.DeleteDelegate;

                        if (deleteDelegate != null)
                        {
                            var deleteResult = await deleteDelegate(resource);
                            if (!deleteResult.Success)
                            {
                                MessageBox.Show($"删除资源 {resource.Name} 失败: {deleteResult.ErrorMessage}", "错误",
                                    MessageBoxButton.OK, MessageBoxImage.Error);
                                return;
                            }
                        }
                    }

                    // 清除选择并刷新显示
                    _selectedItems.Clear();
                    _firstSelectedItem = null;
                    LoadResourcesAsync();
                    UpdatePreview();

                    MessageBox.Show("删除完成", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"删除失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private async void RenameButton_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedItems.Count != 1)
            {
                MessageBox.Show("请选择一个资源进行重命名", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var resource = _selectedItems.First();
            var inputDialog = new InputDialog("重命名资源", "请输入新名称:", resource.Name);

            if (inputDialog.ShowDialog() == true && !string.IsNullOrWhiteSpace(inputDialog.InputText))
            {
                try
                {
                    var typeDefinition = ResourceTypeRegistry.GetTypeDefinition(resource.ResourceType);
                    var renameDelegate = typeDefinition?.DelegateSet.RenameDelegate;

                    if (renameDelegate != null)
                    {
                        var result = await renameDelegate(resource, inputDialog.InputText);
                        if (result.Success)
                        {
                            // 刷新显示
                            UpdateResourceDisplay();
                            UpdatePreview();
                            MessageBox.Show("重命名完成", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
                        }
                        else
                        {
                            MessageBox.Show($"重命名失败: {result.ErrorMessage}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"重命名失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
    }

    /// <summary>
    /// 排序模式
    /// </summary>
    public enum SortMode
    {
        Name,
        Time,
        Size
    }
}
