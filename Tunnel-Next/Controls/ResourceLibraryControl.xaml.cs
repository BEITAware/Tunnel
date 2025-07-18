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
            if (_catalogService != null)
            {
                await LoadResourcesAsync();
            }
        }

        /// <summary>
        /// 加载资源
        /// </summary>
        public async Task LoadResourcesAsync()
        {
            if (_catalogService == null) return;

            try
            {
                ShowLoadingState();

                // 加载目录
                await _catalogService.LoadCatalogAsync();

                // 获取所有资源
                _allResources = _catalogService.Catalog.Resources.ToList();

                // 应用搜索过滤
                ApplySearchFilter();

                // 更新UI
                UpdateResourcesDisplay();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ResourceLibraryControl] 加载资源失败: {ex.Message}");
                ShowEmptyState();
            }
        }

        /// <summary>
        /// 刷新资源
        /// </summary>
        public async Task RefreshResourcesAsync()
        {
            if (_scanService == null || _catalogService == null) return;

            try
            {
                ShowLoadingState();

                // 扫描所有资源
                var resources = await _scanService.ScanAllResourcesAsync();

                // 批量添加到目录
                await _catalogService.AddResourcesAsync(resources);

                // 清理无效资源
                await _catalogService.CleanupInvalidResourcesAsync();

                // 重新加载
                await LoadResourcesAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ResourceLibraryControl] 刷新资源失败: {ex.Message}");
                ShowEmptyState();
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
            return resourceType switch
            {
                ResourceItemType.NodeGraph => "节点图",
                ResourceItemType.Template => "模板",
                ResourceItemType.Script => "脚本",
                ResourceItemType.Image => "图像",
                ResourceItemType.Preset => "预设",
                ResourceItemType.Material => "素材",
                ResourceItemType.Folder => "文件夹",
                _ => "其他"
            };
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
                await LoadResourcesAsync();
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
    }
}
