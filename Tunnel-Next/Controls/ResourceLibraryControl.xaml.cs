using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Tunnel_Next.Models;
using Tunnel_Next.Services;

namespace Tunnel_Next.Controls
{
    /// <summary>
    /// 资源面板控件
    /// </summary>
    public partial class ResourceLibraryControl : UserControl
    {
        private ResourceCatalogService? _catalogService;
        private ResourceScanService? _scanService;
        private ResourceWatcherService? _watcherService;
        private List<ResourceObject> _allResources = new();
        private List<ResourceObject> _filteredResources = new();
        private string _searchText = string.Empty;

        // 分组展开/收缩状态管理
        private Dictionary<ResourceItemType, bool> _groupExpandedStates = new();

        /// <summary>
        /// 资源对象被选择事件
        /// </summary>
        public event EventHandler<ResourceObject>? ResourceSelected;

        /// <summary>
        /// 资源对象被双击事件
        /// </summary>
        public event EventHandler<ResourceObject>? ResourceDoubleClicked;

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
            Loaded += ResourceLibraryControl_Loaded;
        }

        /// <summary>
        /// 初始化资源服务
        /// </summary>
        public void InitializeServices(ResourceCatalogService catalogService, ResourceScanService scanService, ResourceWatcherService? watcherService = null)
        {
            _catalogService = catalogService;
            _scanService = scanService;
            _watcherService = watcherService;

            if (_catalogService != null)
            {
                _catalogService.CatalogChanged += OnCatalogChanged;
            }

            if (_watcherService != null)
            {
                _watcherService.ResourceChanged += OnResourceChanged;
            }
        }

        /// <summary>
        /// 控件加载完成
        /// </summary>
        private async void ResourceLibraryControl_Loaded(object sender, RoutedEventArgs e)
        {
            if (_catalogService != null && _scanService != null)
            {
                // 初始化时直接扫描资源，不从磁盘加载
                await RefreshResourcesAsync();
            }
        }

        /// <summary>
        /// 加载资源 - 确保在UI线程上执行UI操作
        /// </summary>
        public async Task LoadResourcesAsync()
        {
            if (_catalogService == null) return;

            try
            {
                await Dispatcher.InvokeAsync(() => ShowLoadingState());

                // 获取资源可以在后台线程进行
                var resources = await Task.Run(() => _catalogService.Catalog.Resources.ToList());
                
                // 回到UI线程进行UI更新操作
                await Dispatcher.InvokeAsync(() => {
                    // 更新内存中的资源列表
                    _allResources = resources;
                    
                    // 应用搜索过滤
                    ApplySearchFilter();
                    
                    // 更新UI
                    UpdateResourcesDisplay();
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ResourceLibraryControl] 加载资源失败: {ex.Message}");
                await Dispatcher.InvokeAsync(() => ShowEmptyState());
            }
        }

        /// <summary>
        /// 刷新资源 - 异步操作，不阻塞UI线程
        /// </summary>
        public async Task RefreshResourcesAsync()
        {
            if (_scanService == null || _catalogService == null) return;

            try
            {
                // 在UI线程上显示加载状态
                await Dispatcher.InvokeAsync(() => ShowLoadingState());

                // 在后台线程上扫描所有资源
                var resources = await Task.Run(async () => await _scanService.ScanAllResourcesAsync());

                // 回到UI线程，更新内存中的资源列表
                await Dispatcher.InvokeAsync(() => {
                    // 直接更新内存中的资源列表，不保存到磁盘
                    _catalogService.Catalog.Resources.Clear();
                    _catalogService.Catalog.Resources.AddRange(resources);
                    _catalogService.Catalog.UpdateStatistics();
                });

                // 重新加载UI显示 (LoadResourcesAsync已经在UI线程上执行)
                await LoadResourcesAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ResourceLibraryControl] 刷新资源失败: {ex.Message}");
                await Dispatcher.InvokeAsync(() => ShowEmptyState());
            }
        }

        /// <summary>
        /// 应用搜索过滤
        /// </summary>
        private void ApplySearchFilter()
        {
            if (string.IsNullOrWhiteSpace(_searchText))
            {
                _filteredResources = _allResources.ToList();
            }
            else
            {
                var searchTerm = _searchText.ToLowerInvariant();
                _filteredResources = _allResources.Where(r =>
                    r.Name.ToLowerInvariant().Contains(searchTerm) ||
                    r.ResourceTypeDisplayName.ToLowerInvariant().Contains(searchTerm) ||
                    r.Description.ToLowerInvariant().Contains(searchTerm)
                ).ToList();
            }
        }

        /// <summary>
        /// 更新资源显示
        /// </summary>
        private void UpdateResourcesDisplay()
        {
            ResourcesPanel.Children.Clear();

            if (_filteredResources.Count == 0)
            {
                ShowEmptyState();
                return;
            }

            HideAllStates();

            // 按类型分组显示资源
            var groupedResources = _filteredResources.GroupBy(r => r.ResourceType).OrderBy(g => g.Key);

            foreach (var group in groupedResources)
            {
                // 确保分组状态已初始化（默认展开）
                if (!_groupExpandedStates.ContainsKey(group.Key))
                {
                    _groupExpandedStates[group.Key] = true;
                }

                // 创建分组标题按钮
                var groupHeaderButton = CreateGroupHeaderButton(group.Key, group.Count());
                ResourcesPanel.Children.Add(groupHeaderButton);

                // 只有在展开状态时才添加资源项
                if (_groupExpandedStates[group.Key])
                {
                    foreach (var resource in group.OrderBy(r => r.Name))
                    {
                        var resourceItem = new ContentPresenter
                        {
                            Content = resource,
                            ContentTemplate = (DataTemplate)FindResource("ResourceItemTemplate")
                        };

                        // 添加事件处理
                        resourceItem.MouseLeftButtonDown += (s, e) =>
                        {
                            if (s is ContentPresenter presenter && presenter.Content is ResourceObject res)
                            {
                                ResourceSelected?.Invoke(this, res);
                            }
                        };

                        // ContentPresenter没有MouseDoubleClick事件，需要使用InputBindings或其他方式
                        // 暂时通过检测双击间隔来实现
                        DateTime lastClickTime = DateTime.MinValue;
                        resourceItem.MouseLeftButtonDown += (s, e) =>
                        {
                            var now = DateTime.Now;
                            if ((now - lastClickTime).TotalMilliseconds < 500) // 500ms内的第二次点击视为双击
                            {
                                if (s is ContentPresenter presenter && presenter.Content is ResourceObject res)
                                {
                                    ResourceDoubleClicked?.Invoke(this, res);
                                }
                            }
                            lastClickTime = now;
                        };

                        ResourcesPanel.Children.Add(resourceItem);
                    }
                }
            }
        }

        /// <summary>
        /// 创建分组标题按钮
        /// </summary>
        private Button CreateGroupHeaderButton(ResourceItemType resourceType, int count)
        {
            var isExpanded = _groupExpandedStates[resourceType];
            var iconPath = isExpanded ? "../Resources/ClickToCollapse.png" : "../Resources/ClickToExpand.png";

            var button = new Button
            {
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Left,
                Padding = new Thickness(8, 4, 8, 4),
                Margin = new Thickness(0, 8, 0, 4),
                Cursor = Cursors.Hand
            };

            var stackPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal
            };

            var icon = new Image
            {
                Source = new BitmapImage(new Uri(iconPath, UriKind.Relative)),
                Width = 16,
                Height = 16,
                Margin = new Thickness(0, 0, 6, 0),
                VerticalAlignment = VerticalAlignment.Center
            };

            var textBlock = new TextBlock
            {
                Text = $"{GetResourceTypeDisplayName(resourceType)} ({count})",
                Foreground = (Brush)FindResource("PrimaryForeground"),
                FontWeight = FontWeights.SemiBold,
                FontSize = 14,
                FontFamily = new FontFamily("Segoe UI, Microsoft YaHei UI, Arial"),
                VerticalAlignment = VerticalAlignment.Center
            };

            stackPanel.Children.Add(icon);
            stackPanel.Children.Add(textBlock);
            button.Content = stackPanel;

            // 添加点击事件
            button.Click += (s, e) => ToggleGroupExpansion(resourceType);

            return button;
        }

        /// <summary>
        /// 切换分组展开/收缩状态
        /// </summary>
        private void ToggleGroupExpansion(ResourceItemType resourceType)
        {
            _groupExpandedStates[resourceType] = !_groupExpandedStates[resourceType];
            UpdateResourcesDisplay(); // 重新刷新显示
        }

        /// <summary>
        /// 获取资源类型显示名称
        /// </summary>
        private string GetResourceTypeDisplayName(ResourceItemType resourceType)
        {
            return ResourceTypeRegistry.GetDisplayName(resourceType);
        }

        /// <summary>
        /// 显示空状态
        /// </summary>
        private void ShowEmptyState()
        {
            HideAllStates();
            EmptyStatePanel.Visibility = Visibility.Visible;
        }

        /// <summary>
        /// 显示加载状态
        /// </summary>
        private void ShowLoadingState()
        {
            HideAllStates();
            LoadingStatePanel.Visibility = Visibility.Visible;
        }

        /// <summary>
        /// 隐藏所有状态面板
        /// </summary>
        private void HideAllStates()
        {
            EmptyStatePanel.Visibility = Visibility.Collapsed;
            LoadingStatePanel.Visibility = Visibility.Collapsed;
        }

        /// <summary>
        /// 目录变化事件处理
        /// </summary>
        private async void OnCatalogChanged(ResourceCatalog catalog)
        {
            await Dispatcher.InvokeAsync(async () =>
            {
                // 直接更新UI显示，不重新加载
                await LoadResourcesAsync();
            });
        }

        /// <summary>
        /// 资源文件变化事件处理
        /// </summary>
        private async void OnResourceChanged(string filePath, WatcherChangeTypes changeType)
        {
            await Dispatcher.InvokeAsync(async () =>
            {
                System.Diagnostics.Debug.WriteLine($"[ResourceLibraryControl] 资源变化: {changeType} - {filePath}");

                // 延迟刷新，避免频繁更新UI
                await Task.Delay(1000);
                await RefreshResourcesAsync(); // 重新扫描而不是从磁盘加载
            });
        }

        /// <summary>
        /// 搜索文本变化
        /// </summary>
        private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            _searchText = SearchTextBox.Text;
            ApplySearchFilter();
            UpdateResourcesDisplay();
        }



        /// <summary>
        /// 刷新按钮点击
        /// </summary>
        private async void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            RefreshRequested?.Invoke(this, EventArgs.Empty);
            await RefreshResourcesAsync();
        }

        /// <summary>
        /// 导入按钮点击
        /// </summary>
        private void ImportButton_Click(object sender, RoutedEventArgs e)
        {
            ImportRequested?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// 管理按钮点击
        /// </summary>
        private void ManageButton_Click(object sender, RoutedEventArgs e)
        {
            // 打开资源管理器窗口
            Windows.ResourceManagerWindow.ShowResourceManager(Window.GetWindow(this));
        }
    }
}
