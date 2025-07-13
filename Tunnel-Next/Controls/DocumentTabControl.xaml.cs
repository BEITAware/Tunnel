using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using Tunnel_Next.Models;
using Tunnel_Next.Services;

namespace Tunnel_Next.Controls
{
    /// <summary>
    /// 文档Tab控件 - 支持多文档界面，作为DocumentManagerService的UI呈现层
    /// </summary>
    public partial class DocumentTabControl : UserControl
    {
        #region 私有字段

        /// <summary>
        /// 文档ID到TabItem的映射，用于快速查找
        /// </summary>
        private readonly Dictionary<string, TabItem> _documentTabMap = new Dictionary<string, TabItem>();

        #endregion

        #region 依赖属性

        public static readonly DependencyProperty DocumentManagerProperty =
            DependencyProperty.Register(
                "DocumentManager",
                typeof(DocumentManagerService),
                typeof(DocumentTabControl),
                new PropertyMetadata(null, OnDocumentManagerChanged));

        /// <summary>
        /// 文档管理器
        /// </summary>
        public DocumentManagerService? DocumentManager
        {
            get => (DocumentManagerService?)GetValue(DocumentManagerProperty);
            set => SetValue(DocumentManagerProperty, value);
        }

        private static void OnDocumentManagerChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is DocumentTabControl control)
            {
                control.OnDocumentManagerChanged(e.OldValue as DocumentManagerService, e.NewValue as DocumentManagerService);
            }
        }

        #endregion

        #region 构造函数

        public DocumentTabControl()
        {
            InitializeComponent();

            // 在控件加载完成后显示欢迎页面
            Loaded += DocumentTabControl_Loaded;
        }

        private void DocumentTabControl_Loaded(object sender, RoutedEventArgs e)
        {
            // 如果没有DocumentManager或没有文档，显示欢迎页面
            if (DocumentManager == null || DocumentManager.Documents.Count() == 0)
            {
                ShowWelcomeTab();
            }
            else
            {
                // 如果有文档，隐藏欢迎页面
                HideWelcomeTab();
            }
        }

        #endregion

        #region 公共方法

        /// <summary>
        /// 刷新显示
        /// </summary>
        public void RefreshDisplay()
        {
            if (DocumentManager == null)
                return;

            // 同步Tab项
            SynchronizeTabs();
        }

        #endregion

        #region 私有方法

        private void OnDocumentManagerChanged(DocumentManagerService? oldManager, DocumentManagerService? newManager)
        {
            // 取消旧管理器事件订阅
            if (oldManager != null)
            {
                oldManager.DocumentAdded -= DocumentManager_DocumentAdded;
                oldManager.DocumentClosed -= DocumentManager_DocumentClosed;
                oldManager.ActiveDocumentChanged -= DocumentManager_ActiveDocumentChanged;
            }

            // 清空文档Tab（保留XAML中的静态欢迎页面）
            ClearDocumentTabs();

            // 如果没有新管理器，显示欢迎页面并返回
            if (newManager == null)
            {
                ShowWelcomeTab();
                return;
            }

            // 订阅新管理器事件
            newManager.DocumentAdded += DocumentManager_DocumentAdded;
            newManager.DocumentClosed += DocumentManager_DocumentClosed;
            newManager.ActiveDocumentChanged += DocumentManager_ActiveDocumentChanged;

            // 初始化现有文档
            foreach (var document in newManager.Documents)
            {
                AddDocumentTab(document);
            }

            // 如果没有文档，显示欢迎页面
            if (newManager.Documents.Count() == 0)
            {
                ShowWelcomeTab();
            }
            // 如果有活动文档，激活它
            else if (newManager.ActiveDocument != null)
            {
                // 激活当前文档
                var tabItem = FindTabItemByDocumentId(newManager.ActiveDocument.Id);
                if (tabItem != null)
                {
                    DocumentTabs.SelectedItem = tabItem;
                }
            }
        }

        private void DocumentManager_DocumentAdded(object? sender, DocumentEventArgs e)
        {
            // 在UI线程执行
            Dispatcher.Invoke(() =>
            {
                HideWelcomeTab();
                AddDocumentTab(e.Document);
            });
        }

        private void DocumentManager_DocumentClosed(object? sender, DocumentEventArgs e)
        {
            // 在UI线程执行
            Dispatcher.Invoke(() =>
            {
                // 使用映射直接查找TabItem
                if (_documentTabMap.TryGetValue(e.DocumentId, out var tabItem))
                {
                    DocumentTabs.Items.Remove(tabItem);
                    _documentTabMap.Remove(e.DocumentId);
                }

                // 如果没有文档了，显示欢迎页面
                if (_documentTabMap.Count == 0)
                {
                    ShowWelcomeTab();
                }
            });
        }

        private void DocumentManager_ActiveDocumentChanged(object? sender, Services.DocumentChangedEventArgs e)
        {
            // 在UI线程执行
            Dispatcher.Invoke(() =>
            {
                if (e.NewDocument != null)
                {
                    var tabItem = FindTabItemByDocumentId(e.NewDocument.Id);
                    if (tabItem != null && !ReferenceEquals(DocumentTabs.SelectedItem, tabItem))
                    {
                        DocumentTabs.SelectedItem = tabItem;
                    }
                }
            });
        }

        private void AddDocumentTab(IDocumentContent document)
        {
            try
            {
                // 创建新的Tab项
                var tabItem = new TabItem
                {
                    Header = document.Title,
                    Tag = document.CanClose, // 用Tag标记是否可关闭
                    Content = document.GetContentControl(),
                    ToolTip = document.FilePath ?? document.Title
                };

                // 添加到TabControl
                DocumentTabs.Items.Add(tabItem);

                // 添加到映射
                _documentTabMap[document.Id] = tabItem;

                // 订阅文档标题变化事件
                document.TitleChanged += Document_TitleChanged;


                // 如果这是活动文档，选中它
                if (DocumentManager?.ActiveDocumentId == document.Id)
                {
                    DocumentTabs.SelectedItem = tabItem;
                }
            }
            catch (Exception ex)
            {
            }
        }

        private void SynchronizeTabs()
        {
            if (DocumentManager == null)
                return;

            try
            {
                // 移除已关闭的文档
                var documentsToRemove = new List<string>();
                foreach (var kvp in _documentTabMap.ToList())
                {
                    var documentId = kvp.Key;
                    var tabItem = kvp.Value;

                    // 检查文档是否还存在
                    bool documentExists = DocumentManager.Documents.Any(doc => doc.Id == documentId);

                    if (!documentExists)
                    {
                        DocumentTabs.Items.Remove(tabItem);
                        documentsToRemove.Add(documentId);
                    }
                }

                // 从映射中移除已删除的文档
                foreach (var docId in documentsToRemove)
                {
                    _documentTabMap.Remove(docId);
                }

                // 添加新文档
                foreach (var document in DocumentManager.Documents)
                {
                    if (!_documentTabMap.ContainsKey(document.Id))
                    {
                        AddDocumentTab(document);
                    }
                }

                // 如果没有文档，显示欢迎页面
                if (_documentTabMap.Count == 0)
                {
                    ShowWelcomeTab();
                }
                // 激活当前文档（如果有的话）
                else if (DocumentManager.ActiveDocument != null)
                {
                    // 激活当前文档
                    var tabItem = FindTabItemByDocumentId(DocumentManager.ActiveDocument.Id);
                    if (tabItem != null)
                    {
                        DocumentTabs.SelectedItem = tabItem;
                    }
                }
            }
            catch (Exception ex)
            {
            }
        }

        private void Document_TitleChanged(object? sender, EventArgs e)
        {
            if (sender is IDocumentContent document)
            {
                // 在UI线程执行
                Dispatcher.Invoke(() =>
                {
                    var tabItem = FindTabItemByDocumentId(document.Id);
                    if (tabItem != null)
                    {
                        tabItem.Header = document.Title;
                        tabItem.ToolTip = document.FilePath ?? document.Title;
                    }
                });
            }
        }

        private TabItem? FindTabItemByDocumentId(string documentId)
        {
            // 优先使用映射查找
            if (_documentTabMap.TryGetValue(documentId, out var tabItem))
            {
                return tabItem;
            }

            // 如果映射中没有，回退到原来的方法
            if (DocumentManager == null)
                return null;

            var document = DocumentManager.GetDocument(documentId);
            if (document == null)
                return null;

            var contentControl = document.GetContentControl();
            return DocumentTabs.Items.Cast<TabItem>()
                .FirstOrDefault(tab => ReferenceEquals(tab.Content, contentControl));
        }



        /// <summary>
        /// 清除所有文档Tab（保留XAML中的静态欢迎页面）
        /// </summary>
        private void ClearDocumentTabs()
        {
            // 只移除文档Tab，保留欢迎页面
            var tabsToRemove = DocumentTabs.Items.Cast<TabItem>()
                .Where(tab => tab.Header.ToString() != "欢迎")
                .ToList();

            foreach (var tab in tabsToRemove)
            {
                DocumentTabs.Items.Remove(tab);
            }

            _documentTabMap.Clear();
        }

        /// <summary>
        /// 隐藏欢迎页面
        /// </summary>
        private void HideWelcomeTab()
        {
            var welcomeTab = GetWelcomeTab();
            if (welcomeTab != null)
            {
                welcomeTab.Visibility = Visibility.Collapsed;
            }
        }

        /// <summary>
        /// 显示欢迎页面（带动画效果）
        /// </summary>
        private void ShowWelcomeTab()
        {
            var welcomeTab = GetWelcomeTab();
            if (welcomeTab != null)
            {
                welcomeTab.Visibility = Visibility.Visible;
                DocumentTabs.SelectedItem = welcomeTab;

                // 添加欢迎页面动画
                PlayWelcomeAnimation(welcomeTab);
            }
            else
            {
                // 如果XAML中的欢迎页面不存在，创建一个动态的作为后备
                CreateFallbackWelcomeTab();
            }
        }

        /// <summary>
        /// 获取欢迎页面Tab（XAML中定义的静态版本）
        /// </summary>
        private TabItem? GetWelcomeTab()
        {
            return DocumentTabs.Items.Cast<TabItem>()
                .FirstOrDefault(tab => tab.Header.ToString() == "欢迎");
        }

        /// <summary>
        /// 创建后备欢迎页面（仅在XAML中的静态版本不存在时使用）
        /// </summary>
        private void CreateFallbackWelcomeTab()
        {
            // 创建背景图片画笔
            var backgroundBrush = new ImageBrush();
            try
            {
                // 使用pack URI加载图片资源
                var imageUri = new Uri("pack://application:,,,/Resources/Aurora.png");
                backgroundBrush.ImageSource = new BitmapImage(imageUri);
                backgroundBrush.Stretch = Stretch.Fill;
            }
            catch
            {
                // 如果图片加载失败，使用纯色背景作为后备
                backgroundBrush = null;
            }

            // 创建欢迎页面内容
            var welcomeGrid = new Grid();

            // 设置背景
            if (backgroundBrush != null)
            {
                welcomeGrid.Background = backgroundBrush;
            }
            else
            {
                welcomeGrid.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF1A1F28"));
            }

            // 添加Aero风格半透明背景层
            var overlayBorder = new Border();
            var overlayBrush = new LinearGradientBrush();
            overlayBrush.StartPoint = new Point(0, 0);
            overlayBrush.EndPoint = new Point(0, 1);
            overlayBrush.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString("#B0000000"), 0));
            overlayBrush.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString("#90000000"), 1));
            overlayBorder.Background = overlayBrush;
            welcomeGrid.Children.Add(overlayBorder);

            // 创建ScrollViewer
            var scrollViewer = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Padding = new Thickness(30, 20, 30, 20)
            };

            // 创建主要内容面板
            var mainPanel = new StackPanel
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Top,
                MaxWidth = 700
            };

            // 创建Aero风格标题区域
            var titleBorder = CreateAeroTitleSection();
            mainPanel.Children.Add(titleBorder);

            // 创建Aero风格GroupBox区域
            mainPanel.Children.Add(CreateAeroGroupBox("如何开始？", "pack://application:,,,/Resources/bootup.png",
                "软件图形界面顶部的标签栏为你提供了丰富的软件功能。在\"文件\"标签页点选\"新建按钮\"来创建一张新的节点图。更多内容请查询随附文档。"));

            mainPanel.Children.Add(CreateAeroGroupBox("关于", "pack://application:,,,/Resources/imgpp.png",
                "本软件以MPL 2.0协议授权。\n开放源代码软件，按原样提供，不附带任何保证。\n\nCopyright © BEITAware\n\n欢迎参与本项目，感谢所有贡献者为Tunnel付出的努力。\n诚邀天下贤士，共襄千秋盛举。"));

            // 添加开源程序集信息
            mainPanel.Children.Add(CreateOpenSourceLibrariesGroupBox());

            scrollViewer.Content = mainPanel;
            welcomeGrid.Children.Add(scrollViewer);

            var welcomeTab = new TabItem
            {
                Header = "欢迎",
                Tag = false, // 不可关闭
                Content = welcomeGrid
            };

            DocumentTabs.Items.Add(welcomeTab);
            DocumentTabs.SelectedItem = welcomeTab;

            // 添加欢迎页面动画
            PlayWelcomeAnimation(welcomeTab);
        }



        private void DocumentTabs_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (DocumentManager == null || e.AddedItems.Count == 0)
                return;

            if (e.AddedItems[0] is TabItem selectedTab && selectedTab.Header.ToString() != "欢迎")
            {
                // 查找对应的文档
                foreach (var document in DocumentManager.Documents)
                {
                    if (ReferenceEquals(document.GetContentControl(), selectedTab.Content))
                    {
                        // 通知DocumentManager激活此文档
                        DocumentManager.SetActiveDocument(document.Id);
                        return;
                    }
                }
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            if (DocumentManager == null || !(sender is Button button))
                return;

            // 查找包含此按钮的TabItem
            var tabItem = FindParent<TabItem>(button);
            if (tabItem == null)
                return;

            // 查找对应的文档
            foreach (var document in DocumentManager.Documents)
            {
                if (ReferenceEquals(document.GetContentControl(), tabItem.Content))
                {
                    // 请求关闭文档
                    DocumentManager.CloseDocument(document.Id);
                    return;
                }
            }
        }

        /// <summary>
        /// 查找父级控件
        /// </summary>
        private static T? FindParent<T>(DependencyObject child) where T : DependencyObject
        {
            var parent = VisualTreeHelper.GetParent(child);
            if (parent == null) return null;

            if (parent is T parentT)
                return parentT;

            return FindParent<T>(parent);
        }

        private void NewDocumentButton_Click(object sender, RoutedEventArgs e)
        {
            DocumentManager?.RequestNewDocument();
        }

        /// <summary>
        /// 创建图像控件
        /// </summary>
        private Image CreateImage(string source, double width, double height)
        {
            var image = new Image
            {
                Width = width,
                Height = height
            };

            try
            {
                var imageUri = new Uri(source);
                image.Source = new BitmapImage(imageUri);
            }
            catch
            {
                // 如果图片加载失败，返回空图像
            }

            return image;
        }

        /// <summary>
        /// 创建Aero风格标题区域
        /// </summary>
        private Border CreateAeroTitleSection()
        {
            var border = new Border
            {
                Margin = new Thickness(0, 20, 0, 30),
                Padding = new Thickness(20, 15, 20, 15),
                CornerRadius = new CornerRadius(3),
                BorderThickness = new Thickness(1)
            };

            // 设置Aero风格背景
            var backgroundBrush = new LinearGradientBrush();
            backgroundBrush.StartPoint = new Point(0, 0);
            backgroundBrush.EndPoint = new Point(0, 1);
            backgroundBrush.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString("#40FFFFFF"), 0));
            backgroundBrush.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString("#20FFFFFF"), 1));
            border.Background = backgroundBrush;

            // 设置边框
            var borderBrush = new LinearGradientBrush();
            borderBrush.StartPoint = new Point(0, 0);
            borderBrush.EndPoint = new Point(0, 1);
            borderBrush.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString("#60FFFFFF"), 0));
            borderBrush.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString("#30FFFFFF"), 1));
            border.BorderBrush = borderBrush;

            var stackPanel = new StackPanel
            {
                HorizontalAlignment = HorizontalAlignment.Center
            };

            // 标题和Logo
            var titlePanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 8)
            };

            var titleText = new TextBlock
            {
                Text = "欢迎使用Tunnel",
                FontSize = 28,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Colors.White),
                VerticalAlignment = VerticalAlignment.Center
            };

            var logoImage = CreateImage("pack://application:,,,/Resources/TunnelAppLogo.png", 40, 40);
            logoImage.Margin = new Thickness(12, 0, 0, 0);
            logoImage.VerticalAlignment = VerticalAlignment.Center;

            titlePanel.Children.Add(titleText);
            titlePanel.Children.Add(logoImage);
            stackPanel.Children.Add(titlePanel);

            // 副标题
            var subtitleText = new TextBlock
            {
                Text = "\"预览版本\"",
                FontSize = 14,
                FontStyle = FontStyles.Italic,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E8E8E8")),
                HorizontalAlignment = HorizontalAlignment.Center
            };

            stackPanel.Children.Add(subtitleText);
            border.Child = stackPanel;

            return border;
        }

        /// <summary>
        /// 创建Aero风格GroupBox
        /// </summary>
        private GroupBox CreateAeroGroupBox(string header, string iconSource, string content)
        {
            var groupBox = new GroupBox
            {
                Header = header,
                Margin = new Thickness(0, 0, 0, 15),
                Padding = new Thickness(15, 10, 15, 10),
                Foreground = new SolidColorBrush(Colors.White),
                FontSize = 14,
                FontWeight = FontWeights.Bold,
                BorderThickness = new Thickness(1)
            };

            // 设置Aero风格背景
            var backgroundBrush = new LinearGradientBrush();
            backgroundBrush.StartPoint = new Point(0, 0);
            backgroundBrush.EndPoint = new Point(0, 1);
            backgroundBrush.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString("#30FFFFFF"), 0));
            backgroundBrush.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString("#15FFFFFF"), 1));
            groupBox.Background = backgroundBrush;

            groupBox.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#50FFFFFF"));

            var stackPanel = new StackPanel();

            var contentPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(0, 0, 0, 8)
            };

            var iconImage = CreateImage(iconSource, 20, 20);
            iconImage.VerticalAlignment = VerticalAlignment.Top;
            iconImage.Margin = new Thickness(0, 2, 8, 0);

            var textPanel = new StackPanel
            {
                MaxWidth = 500
            };

            var contentText = new TextBlock
            {
                Text = content,
                FontSize = 12,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F5F5F5")),
                TextWrapping = TextWrapping.Wrap,
                FontWeight = FontWeights.Normal,
                LineHeight = 16
            };

            textPanel.Children.Add(contentText);
            contentPanel.Children.Add(iconImage);
            contentPanel.Children.Add(textPanel);
            stackPanel.Children.Add(contentPanel);

            groupBox.Content = stackPanel;

            return groupBox;
        }

        /// <summary>
        /// 创建开源程序集信息GroupBox
        /// </summary>
        private GroupBox CreateOpenSourceLibrariesGroupBox()
        {
            var groupBox = new GroupBox
            {
                Header = "Tunnel使用的开放原代码程序集",
                Margin = new Thickness(0, 0, 0, 20),
                Padding = new Thickness(15, 10, 15, 10),
                Foreground = new SolidColorBrush(Colors.White),
                FontSize = 14,
                FontWeight = FontWeights.Bold,
                BorderThickness = new Thickness(1)
            };

            // 设置Aero风格背景
            var backgroundBrush = new LinearGradientBrush();
            backgroundBrush.StartPoint = new Point(0, 0);
            backgroundBrush.EndPoint = new Point(0, 1);
            backgroundBrush.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString("#30FFFFFF"), 0));
            backgroundBrush.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString("#15FFFFFF"), 1));
            groupBox.Background = backgroundBrush;

            groupBox.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#50FFFFFF"));

            var stackPanel = new StackPanel();

            // 开源库信息数据
            var libraries = new[]
            {
                new { Name = "MathNet.Numerics 5.0.0", License = "MIT License", Copyright = "Copyright (c) 2002-2022 Math.NET Project" },
                new { Name = "Microsoft.CodeAnalysis 4.14.0", License = "Apache License 2.0", Copyright = "Copyright (c) Microsoft Corporation" },
                new { Name = "Microsoft.ML 4.0.2", License = "MIT License", Copyright = "Copyright (c) Microsoft Corporation" },
                new { Name = "Newtonsoft.Json 13.0.3", License = "MIT License", Copyright = "Copyright (c) James Newton-King" },
                new { Name = "OpenCvSharp4 4.11.0", License = "Apache License 2.0", Copyright = "Copyright 2008 shimat" },
                new { Name = "Sdcb.LibRaw 0.21.1.7", License = "MIT License (wrapper) / LGPL-2.1 OR CDDL-1.0 (runtime)", Copyright = "Copyright (c) Zhou Jie" },
                new { Name = "System.Drawing.Common 8.0.11", License = "MIT License", Copyright = "Copyright (c) .NET Foundation and Contributors" },
                new { Name = "System.Reactive 6.0.1", License = "MIT License", Copyright = "Copyright (c) .NET Foundation and Contributors" },
                new { Name = "System.Windows.Forms 8.0.11", License = "MIT License", Copyright = "Copyright (c) .NET Foundation and Contributors" }
            };

            // 为每个库创建信息块
            foreach (var lib in libraries)
            {
                var border = new Border
                {
                    Margin = new Thickness(0, 0, 0, 8),
                    Padding = new Thickness(8),
                    CornerRadius = new CornerRadius(2),
                    Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#20FFFFFF")),
                    BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#40FFFFFF")),
                    BorderThickness = new Thickness(1)
                };

                var libStackPanel = new StackPanel();

                var nameText = new TextBlock
                {
                    Text = lib.Name,
                    FontWeight = FontWeights.Bold,
                    FontSize = 11,
                    Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F5F5F5")),
                    Margin = new Thickness(0, 0, 0, 2)
                };

                var licenseText = new TextBlock
                {
                    Text = lib.License,
                    FontSize = 10,
                    Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E0E0E0")),
                    Margin = new Thickness(0, 0, 0, 2)
                };

                var copyrightText = new TextBlock
                {
                    Text = lib.Copyright,
                    FontSize = 9,
                    Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#D0D0D0"))
                };



                libStackPanel.Children.Add(nameText);
                libStackPanel.Children.Add(licenseText);
                libStackPanel.Children.Add(copyrightText);

                // 创建Grid来布局内容和按钮
                var grid = new Grid();
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                // 创建查看许可证按钮
                var licenseButton = new Button
                {
                    Content = "查看许可证",
                    Width = 80,
                    Height = 25,
                    Margin = new Thickness(8, 0, 0, 0),
                    VerticalAlignment = VerticalAlignment.Center
                };

                // 加载按钮样式
                try
                {
                    var styleUri = new Uri("pack://application:,,,/Tunnel-Next;component/Resources/ScriptsControls/ScriptButtonStyles.xaml");
                    var resourceDict = new ResourceDictionary { Source = styleUri };
                    if (resourceDict["SelectFileScriptButtonStyle"] is Style buttonStyle)
                    {
                        licenseButton.Style = buttonStyle;
                    }
                }
                catch
                {
                    // 如果样式加载失败，使用默认样式
                }

                // 绑定点击事件
                licenseButton.Click += (sender, e) => ShowLicenseDialog(lib.Name);

                // 设置Grid布局
                Grid.SetColumn(libStackPanel, 0);
                Grid.SetColumn(licenseButton, 1);
                grid.Children.Add(libStackPanel);
                grid.Children.Add(licenseButton);

                border.Child = grid;
                stackPanel.Children.Add(border);
            }

            // 添加法律声明
            var legalText = new TextBlock
            {
                Text = "本软件使用的第三方开源库均遵循其各自的开源协议。\n完整的许可证文本和源代码可在各项目的官方仓库中找到。\n对于LGPL许可的组件，源代码可通过相应项目的官方渠道获取。",
                FontSize = 9,
                FontStyle = FontStyles.Italic,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#C0C0C0")),
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 8, 0, 0)
            };

            stackPanel.Children.Add(legalText);
            groupBox.Content = stackPanel;

            return groupBox;
        }

        /// <summary>
        /// 播放欢迎页面动画效果
        /// </summary>
        private void PlayWelcomeAnimation(TabItem welcomeTab)
        {
            if (welcomeTab?.Content is not FrameworkElement content)
                return;

            try
            {
                // 创建动画故事板
                var storyboard = new Storyboard();

                // 1. 淡入动画
                var fadeInAnimation = new DoubleAnimation
                {
                    From = 0.0,
                    To = 1.0,
                    Duration = TimeSpan.FromMilliseconds(800),
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                };
                Storyboard.SetTarget(fadeInAnimation, content);
                Storyboard.SetTargetProperty(fadeInAnimation, new PropertyPath("Opacity"));
                storyboard.Children.Add(fadeInAnimation);

                // 2. 缩放动画 - 从小到正常大小
                var scaleXAnimation = new DoubleAnimation
                {
                    From = 0.8,
                    To = 1.0,
                    Duration = TimeSpan.FromMilliseconds(600),
                    EasingFunction = new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.3 }
                };

                var scaleYAnimation = new DoubleAnimation
                {
                    From = 0.8,
                    To = 1.0,
                    Duration = TimeSpan.FromMilliseconds(600),
                    EasingFunction = new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.3 }
                };

                // 确保内容有RenderTransform
                if (content.RenderTransform == null || content.RenderTransform == Transform.Identity)
                {
                    content.RenderTransform = new ScaleTransform();
                    content.RenderTransformOrigin = new Point(0.5, 0.5);
                }

                if (content.RenderTransform is ScaleTransform scaleTransform)
                {
                    Storyboard.SetTarget(scaleXAnimation, scaleTransform);
                    Storyboard.SetTargetProperty(scaleXAnimation, new PropertyPath("ScaleX"));
                    storyboard.Children.Add(scaleXAnimation);

                    Storyboard.SetTarget(scaleYAnimation, scaleTransform);
                    Storyboard.SetTargetProperty(scaleYAnimation, new PropertyPath("ScaleY"));
                    storyboard.Children.Add(scaleYAnimation);
                }

                // 3. 向上滑动动画
                var translateAnimation = new DoubleAnimation
                {
                    From = 30,
                    To = 0,
                    Duration = TimeSpan.FromMilliseconds(700),
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                };

                // 为平移动画创建TransformGroup
                var transformGroup = new TransformGroup();
                transformGroup.Children.Add(content.RenderTransform ?? Transform.Identity);
                transformGroup.Children.Add(new TranslateTransform());
                content.RenderTransform = transformGroup;

                var translateTransform = transformGroup.Children.OfType<TranslateTransform>().FirstOrDefault();
                if (translateTransform != null)
                {
                    Storyboard.SetTarget(translateAnimation, translateTransform);
                    Storyboard.SetTargetProperty(translateAnimation, new PropertyPath("Y"));
                    storyboard.Children.Add(translateAnimation);
                }

                // 设置初始状态
                content.Opacity = 0;

                // 播放动画
                storyboard.Begin();

                // 添加一个延迟的微妙脉冲效果
                var pulseStoryboard = new Storyboard();
                var pulseAnimation = new DoubleAnimation
                {
                    From = 1.0,
                    To = 1.05,
                    Duration = TimeSpan.FromMilliseconds(1000),
                    AutoReverse = true,
                    EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
                };

                pulseStoryboard.BeginTime = TimeSpan.FromMilliseconds(1200);

                if (content.RenderTransform is TransformGroup group)
                {
                    var scaleTransformInGroup = group.Children.OfType<ScaleTransform>().FirstOrDefault();
                    if (scaleTransformInGroup != null)
                    {
                        Storyboard.SetTarget(pulseAnimation, scaleTransformInGroup);
                        Storyboard.SetTargetProperty(pulseAnimation, new PropertyPath("ScaleX"));
                        pulseStoryboard.Children.Add(pulseAnimation);

                        var pulseAnimationY = pulseAnimation.Clone();
                        Storyboard.SetTarget(pulseAnimationY, scaleTransformInGroup);
                        Storyboard.SetTargetProperty(pulseAnimationY, new PropertyPath("ScaleY"));
                        pulseStoryboard.Children.Add(pulseAnimationY);

                        pulseStoryboard.Begin();
                    }
                }
            }
            catch
            {
                // 如果动画失败，至少确保内容可见
                content.Opacity = 1.0;
            }
        }

        /// <summary>
        /// 显示许可证协议对话框
        /// </summary>
        private void ShowLicenseDialog(string libraryName)
        {
            try
            {
                var licenseFileName = GetLicenseFileName(libraryName);
                if (string.IsNullOrEmpty(licenseFileName))
                {
                    MessageBox.Show($"未找到 {libraryName} 的许可证文件。", "许可证", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                var licenseFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Licenses", licenseFileName);
                if (!File.Exists(licenseFilePath))
                {
                    MessageBox.Show($"许可证文件不存在：{licenseFileName}\n完整路径：{licenseFilePath}", "许可证", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var licenseContent = File.ReadAllText(licenseFilePath);
                ShowLicenseWindow(libraryName, licenseContent);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"读取许可证文件时发生错误：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 根据库名称获取对应的许可证文件名
        /// </summary>
        private string GetLicenseFileName(string libraryName)
        {
            // 提取库的基础名称（去掉版本号）
            var baseName = libraryName.Split(' ')[0];

            return baseName switch
            {
                "MathNet.Numerics" => "MathNet.Numerics-LICENSE.txt",
                "Microsoft.CodeAnalysis" => "Microsoft.CodeAnalysis-LICENSE.txt",
                "Microsoft.ML" => "Microsoft.ML-LICENSE.txt",
                "Newtonsoft.Json" => "Newtonsoft.Json-LICENSE.txt",
                "OpenCvSharp4" => "OpenCvSharp4-LICENSE.txt",
                "Sdcb.LibRaw" => "Sdcb.LibRaw-LICENSE.txt",
                "System.Drawing.Common" => "DotNet-Foundation-LICENSE.txt",
                "System.Reactive" => "DotNet-Foundation-LICENSE.txt",
                "System.Windows.Forms" => "DotNet-Foundation-LICENSE.txt",
                _ => null
            };
        }

        /// <summary>
        /// 显示许可证窗口
        /// </summary>
        private void ShowLicenseWindow(string libraryName, string licenseContent)
        {
            try
            {
                var window = new Window
                {
                    Title = $"{libraryName} - 许可证协议",
                    Width = 700,
                    Height = 600,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner,
                    Owner = Window.GetWindow(this),
                    ResizeMode = ResizeMode.CanResize,
                    MinWidth = 500,
                    MinHeight = 400
                };

                // 设置窗口背景为Aero风格
                var windowBackgroundBrush = new ImageBrush();
                try
                {
                    var imageUri = new Uri("pack://application:,,,/Resources/Aurora.png");
                    windowBackgroundBrush.ImageSource = new BitmapImage(imageUri);
                    windowBackgroundBrush.Stretch = Stretch.Fill;
                    window.Background = windowBackgroundBrush;
                }
                catch
                {
                    // 如果图片加载失败，使用渐变背景
                    var gradientBrush = new LinearGradientBrush();
                    gradientBrush.StartPoint = new Point(0, 0);
                    gradientBrush.EndPoint = new Point(0, 1);
                    gradientBrush.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString("#FF1A1F28"), 0));
                    gradientBrush.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString("#FF2E4A6C"), 1));
                    window.Background = gradientBrush;
                }

                // 创建主容器
                var mainGrid = new Grid();

                // 添加半透明背景层
                var overlayBorder = new Border();
                var overlayBrush = new LinearGradientBrush();
                overlayBrush.StartPoint = new Point(0, 0);
                overlayBrush.EndPoint = new Point(0, 1);
                overlayBrush.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString("#B0000000"), 0));
                overlayBrush.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString("#90000000"), 1));
                overlayBorder.Background = overlayBrush;
                mainGrid.Children.Add(overlayBorder);

                // 内容容器
                var contentBorder = new Border
                {
                    Margin = new Thickness(20),
                    CornerRadius = new CornerRadius(5),
                    Padding = new Thickness(0, 0, 0, 0)
                };

                // 设置内容边框的Aero风格背景
                var contentBackgroundBrush = new LinearGradientBrush();
                contentBackgroundBrush.StartPoint = new Point(0, 0);
                contentBackgroundBrush.EndPoint = new Point(0, 1);
                contentBackgroundBrush.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString("#40FFFFFF"), 0));
                contentBackgroundBrush.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString("#20FFFFFF"), 1));
                contentBorder.Background = contentBackgroundBrush;
                contentBorder.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#60FFFFFF"));
                contentBorder.BorderThickness = new Thickness(1);

                var grid = new Grid();
                grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
                grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

                // 标题区域
                var titleBorder = new Border
                {
                    Padding = new Thickness(20, 15, 20, 15),
                    CornerRadius = new CornerRadius(5, 5, 0, 0)
                };

                var titleBackgroundBrush = new LinearGradientBrush();
                titleBackgroundBrush.StartPoint = new Point(0, 0);
                titleBackgroundBrush.EndPoint = new Point(0, 1);
                titleBackgroundBrush.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString("#60FFFFFF"), 0));
                titleBackgroundBrush.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString("#30FFFFFF"), 1));
                titleBorder.Background = titleBackgroundBrush;
                titleBorder.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#80FFFFFF"));
                titleBorder.BorderThickness = new Thickness(0, 0, 0, 1);

                var titleText = new TextBlock
                {
                    Text = $"{libraryName} 许可证协议",
                    FontSize = 18,
                    FontWeight = FontWeights.Bold,
                    Foreground = Brushes.White,
                    TextWrapping = TextWrapping.Wrap,
                    FontFamily = new FontFamily("Segoe UI, Microsoft YaHei UI, Arial")
                };
                titleBorder.Child = titleText;
                Grid.SetRow(titleBorder, 0);
                grid.Children.Add(titleBorder);

                // 内容区域
                var contentScrollViewer = new ScrollViewer
                {
                    Margin = new Thickness(20, 15, 20, 15),
                    VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                    HorizontalScrollBarVisibility = ScrollBarVisibility.Auto
                };

                var textBox = new TextBox
                {
                    Text = licenseContent,
                    IsReadOnly = true,
                    TextWrapping = TextWrapping.Wrap,
                    FontFamily = new FontFamily("Consolas, Courier New, monospace"),
                    FontSize = 12,
                    Padding = new Thickness(15),
                    BorderThickness = new Thickness(0),
                    Foreground = Brushes.White,
                    Background = Brushes.Transparent,
                    SelectionBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#40FFFFFF"))
                };

                contentScrollViewer.Content = textBox;
                Grid.SetRow(contentScrollViewer, 1);
                grid.Children.Add(contentScrollViewer);

                // 按钮区域
                var buttonBorder = new Border
                {
                    Padding = new Thickness(20, 15, 20, 15),
                    CornerRadius = new CornerRadius(0, 0, 5, 5)
                };

                var buttonBackgroundBrush = new LinearGradientBrush();
                buttonBackgroundBrush.StartPoint = new Point(0, 0);
                buttonBackgroundBrush.EndPoint = new Point(0, 1);
                buttonBackgroundBrush.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString("#30FFFFFF"), 0));
                buttonBackgroundBrush.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString("#60FFFFFF"), 1));
                buttonBorder.Background = buttonBackgroundBrush;
                buttonBorder.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#80FFFFFF"));
                buttonBorder.BorderThickness = new Thickness(0, 1, 0, 0);

                var buttonPanel = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Right
                };

                // 关闭按钮 - 使用Aero风格
                var closeButton = new Button
                {
                    Content = "关闭",
                    Width = 100,
                    Height = 35,
                    Margin = new Thickness(0),
                    IsDefault = true,
                    IsCancel = true,
                    FontSize = 14,
                    FontWeight = FontWeights.Bold,
                    Foreground = Brushes.White,
                    FontFamily = new FontFamily("Segoe UI, Microsoft YaHei UI, Arial")
                };

                // 设置按钮的Aero风格
                var buttonBackgroundBrush2 = new LinearGradientBrush();
                buttonBackgroundBrush2.StartPoint = new Point(0, 0);
                buttonBackgroundBrush2.EndPoint = new Point(0, 1);
                buttonBackgroundBrush2.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString("#FF2E4A6C"), 0));
                buttonBackgroundBrush2.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString("#FF1D2E54"), 0.4));
                buttonBackgroundBrush2.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString("#FE070714"), 0.6));
                buttonBackgroundBrush2.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString("#FF162F67"), 1));
                closeButton.Background = buttonBackgroundBrush2;
                closeButton.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF1A1F28"));
                closeButton.BorderThickness = new Thickness(1);

                // 添加按钮悬停效果
                closeButton.MouseEnter += (s, e) =>
                {
                    var hoverBrush = new LinearGradientBrush();
                    hoverBrush.StartPoint = new Point(0, 0);
                    hoverBrush.EndPoint = new Point(0, 1);
                    hoverBrush.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString("#FF3E5A7C"), 0));
                    hoverBrush.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString("#FF2D3E64"), 0.4));
                    hoverBrush.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString("#FE171724"), 0.6));
                    hoverBrush.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString("#FF263F77"), 1));
                    closeButton.Background = hoverBrush;
                };

                closeButton.MouseLeave += (s, e) =>
                {
                    closeButton.Background = buttonBackgroundBrush2;
                };

                closeButton.Click += (s, e) => window.Close();
                buttonPanel.Children.Add(closeButton);
                buttonBorder.Child = buttonPanel;
                Grid.SetRow(buttonBorder, 2);
                grid.Children.Add(buttonBorder);

                contentBorder.Child = grid;
                mainGrid.Children.Add(contentBorder);
                window.Content = mainGrid;
                window.ShowDialog();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"显示许可证窗口时发生错误：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #region 许可证按钮事件处理

        private void MathNetLicenseButton_Click(object sender, RoutedEventArgs e)
        {
            ShowLicenseDialog("MathNet.Numerics 5.0.0");
        }

        private void MicrosoftCodeAnalysisLicenseButton_Click(object sender, RoutedEventArgs e)
        {
            ShowLicenseDialog("Microsoft.CodeAnalysis 4.14.0");
        }

        private void MicrosoftMLLicenseButton_Click(object sender, RoutedEventArgs e)
        {
            ShowLicenseDialog("Microsoft.ML 4.0.2");
        }

        private void NewtonsoftJsonLicenseButton_Click(object sender, RoutedEventArgs e)
        {
            ShowLicenseDialog("Newtonsoft.Json 13.0.3");
        }

        private void OpenCvSharp4LicenseButton_Click(object sender, RoutedEventArgs e)
        {
            ShowLicenseDialog("OpenCvSharp4 4.11.0");
        }

        private void SdcbLibRawLicenseButton_Click(object sender, RoutedEventArgs e)
        {
            ShowLicenseDialog("Sdcb.LibRaw 0.21.1.7");
        }

        private void SystemDrawingCommonLicenseButton_Click(object sender, RoutedEventArgs e)
        {
            ShowLicenseDialog("System.Drawing.Common 8.0.11");
        }

        private void SystemReactiveLicenseButton_Click(object sender, RoutedEventArgs e)
        {
            ShowLicenseDialog("System.Reactive 6.0.1");
        }

        private void SystemWindowsFormsLicenseButton_Click(object sender, RoutedEventArgs e)
        {
            ShowLicenseDialog("System.Windows.Forms 8.0.11");
        }

        #endregion

        #endregion
    }
}