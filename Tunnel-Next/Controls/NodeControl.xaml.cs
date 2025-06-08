using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using Tunnel_Next.Models;
using Tunnel_Next.Services.ImageProcessing;
using Tunnel_Next.Windows;

namespace Tunnel_Next.Controls
{
    /// <summary>
    /// 节点控件
    /// </summary>
    public partial class NodeControl : UserControl
    {
        private Node? _node;
        private bool _isDragging;
        private Point _lastMousePosition;

        /// <summary>
        /// 移除ImageProcessor依赖，使用MVVM解耦架构
        /// 节点元数据现在通过节点本身获取
        /// </summary>

        public NodeControl()
        {
            InitializeComponent();
            Loaded += OnLoaded;
        }

        public Node? Node
        {
            get => _node;
            set
            {

                // 取消旧节点的属性变化监听
                if (_node != null)
                {
                    _node.PropertyChanged -= OnNodePropertyChanged;
                    _node.InputPorts.CollectionChanged -= OnInputPortsChanged;
                    _node.OutputPorts.CollectionChanged -= OnOutputPortsChanged;
                }

                _node = value;
                DataContext = _node;

                // 监听新节点的属性变化
                if (_node != null)
                {
                    _node.PropertyChanged += OnNodePropertyChanged;
                    _node.InputPorts.CollectionChanged += OnInputPortsChanged;
                    _node.OutputPorts.CollectionChanged += OnOutputPortsChanged;
                }

                UpdatePorts();
                UpdateSelection();
            }
        }

        public event EventHandler<Node>? NodeSelected;
        public event EventHandler<Node>? NodeMoved;
        public event EventHandler<Node>? NodeDeleteRequested;
        public event EventHandler<(Node node, string portName, bool isOutput)>? PortClicked;

        // 新的拖拽事件
        public event EventHandler<(Node node, string portName, bool isOutput)>? PortDragStarted;
        public event EventHandler<(Node node, string portName, bool isOutput, Point position)>? PortDragMove;
        public event EventHandler<(Node node, string portName, bool isOutput, FrameworkElement? targetElement)>? PortDragEnded;

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            if (_node != null)
            {
                Canvas.SetLeft(this, _node.X);
                Canvas.SetTop(this, _node.Y);
            }
        }

        private void UpdatePorts()
        {
            if (_node == null)
            {
                return;
            }


            // 清除现有端口
            InputPortsPanel.Children.Clear();
            OutputPortsPanel.Children.Clear();

            // 添加输入端口
            foreach (var port in _node.InputPorts)
            {
                var portEllipse = new Ellipse
                {
                    Style = (Style)FindResource("PortStyle"),
                    Tag = port,
                    Margin = new Thickness(2, 6, 2, 6), // 增加端口间距到12像素
                    ToolTip = $"{port.Description}\n类型: {port.DataType}\n{(port.IsFlexible ? "灵活端口" : "固定端口")}"
                };

                // 设置端口颜色 - 使用新的端口颜色方法
                var portDataType = PortTypeDefinitions.GetPortDataType(port.DataType.ToString());
                var portColor = PortTypeDefinitions.GetPortWpfColor(portDataType);
                var radialGradient = new RadialGradientBrush();
                radialGradient.GradientStops.Add(new GradientStop(Colors.White, 0.0));
                radialGradient.GradientStops.Add(new GradientStop(portColor, 0.7));
                radialGradient.GradientStops.Add(new GradientStop(Color.FromRgb(
                    (byte)(portColor.R * 0.7),
                    (byte)(portColor.G * 0.7),
                    (byte)(portColor.B * 0.7)), 1.0));
                portEllipse.Fill = radialGradient;

                // 添加多个事件来测试端口是否响应
                portEllipse.MouseEnter += (s, e) =>
                {
                };

                portEllipse.MouseLeave += (s, e) =>
                {
                };

                portEllipse.PreviewMouseLeftButtonDown += (s, e) =>
                {
                    e.Handled = true;
                };

                portEllipse.MouseLeftButtonDown += (s, e) =>
                {
                    // 输入端口不开始拖拽，只能作为连接目标
                    PortClicked?.Invoke(this, (_node, port.Name, false));
                    e.Handled = true;
                };

                InputPortsPanel.Children.Add(portEllipse);

                // 验证Tag绑定
            }

            // 添加输出端口
            foreach (var port in _node.OutputPorts)
            {
                var portEllipse = new Ellipse
                {
                    Style = (Style)FindResource("PortStyle"),
                    Tag = port,
                    Margin = new Thickness(2, 6, 2, 6), // 增加端口间距到12像素
                    ToolTip = $"{port.Description}\n类型: {port.DataType}\n{(port.IsFlexible ? "灵活端口" : "固定端口")}"
                };

                // 设置端口颜色 - 使用新的端口颜色方法
                var portDataType = PortTypeDefinitions.GetPortDataType(port.DataType.ToString());
                var portColor = PortTypeDefinitions.GetPortWpfColor(portDataType);
                var radialGradient = new RadialGradientBrush();
                radialGradient.GradientStops.Add(new GradientStop(Colors.White, 0.0));
                radialGradient.GradientStops.Add(new GradientStop(portColor, 0.7));
                radialGradient.GradientStops.Add(new GradientStop(Color.FromRgb(
                    (byte)(portColor.R * 0.7),
                    (byte)(portColor.G * 0.7),
                    (byte)(portColor.B * 0.7)), 1.0));
                portEllipse.Fill = radialGradient;

                // 添加多个事件来测试端口是否响应
                portEllipse.MouseEnter += (s, e) =>
                {
                };

                portEllipse.MouseLeave += (s, e) =>
                {
                };

                portEllipse.PreviewMouseLeftButtonDown += (s, e) =>
                {

                    try {
                        // 开始拖拽连接
                        PortDragStarted?.Invoke(this, (_node, port.Name, true));

                        // 捕获鼠标，开始拖拽
                        portEllipse.CaptureMouse();
                    }
                    catch (Exception ex) {
                    }
                    e.Handled = true;
                };

                portEllipse.MouseLeftButtonDown += (s, e) =>
                {
                    // 这个事件现在不应该被触发，因为Preview事件已经处理了
                    e.Handled = true;
                };

                portEllipse.MouseMove += (s, e) =>
                {
                    // 如果鼠标被捕获且按下左键，继续拖拽
                    if (portEllipse.IsMouseCaptured && e.LeftButton == MouseButtonState.Pressed)
                    {
                        try {
                            // 获取相对于画布的位置
                            var canvas = FindParentCanvas(this);

                            var position = canvas != null ? e.GetPosition(canvas) : e.GetPosition(Parent as UIElement);

                            PortDragMove?.Invoke(this, (_node, port.Name, true, position));
                        }
                        catch (Exception ex) {
                        }
                    }
                };

                portEllipse.MouseLeftButtonUp += (s, e) =>
                {

                    if (portEllipse.IsMouseCaptured)
                    {

                        try {
                            portEllipse.ReleaseMouseCapture();

                            // 获取鼠标位置下的元素
                            var canvas = FindParentCanvas(this);

                            if (canvas != null)
                            {
                                var mousePosition = e.GetPosition(canvas);

                                var elementUnderMouse = canvas.InputHitTest(mousePosition) as FrameworkElement;

                                PortDragEnded?.Invoke(this, (_node, port.Name, true, elementUnderMouse));
                            }
                            else
                            {
                                PortDragEnded?.Invoke(this, (_node, port.Name, true, null));
                            }
                        }
                        catch (Exception ex) {
                            // 确保即使出现异常也能释放鼠标
                            try { portEllipse.ReleaseMouseCapture(); } catch { }
                        }

                        e.Handled = true;
                    }
                    else
                    {
                    }
                };

                OutputPortsPanel.Children.Add(portEllipse);

                // 验证Tag绑定
            }

        }

        private void UpdateSelection()
        {
            if (_node == null) return;

            SelectionBorder.Visibility = _node.IsSelected ? Visibility.Visible : Visibility.Collapsed;
            HighlightBorder.Visibility = _node.IsHighlighted ? Visibility.Visible : Visibility.Collapsed;
        }

        private void OnNodePropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(Node.IsSelected) || e.PropertyName == nameof(Node.IsHighlighted))
            {
                UpdateSelection();
            }
        }

        /// <summary>
        /// 输入端口集合变化事件处理
        /// </summary>
        private void OnInputPortsChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {

            // 当端口集合发生变化时，重新更新端口UI
            Dispatcher.BeginInvoke(() => UpdatePorts());
        }

        /// <summary>
        /// 输出端口集合变化事件处理
        /// </summary>
        private void OnOutputPortsChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {

            // 当端口集合发生变化时，重新更新端口UI
            Dispatcher.BeginInvoke(() => UpdatePorts());
        }

        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonDown(e);


            if (_node == null)
            {
                return;
            }

            // 检查是否点击的是端口，如果是则不处理节点拖拽
            if (e.OriginalSource is Ellipse ellipse && ellipse.Tag is NodePort)
            {
                return; // 让端口事件处理
            }

            // 检查事件是否已经被处理（端口事件会设置Handled=true）
            if (e.Handled)
            {
                return;
            }

            // 选中节点
            NodeSelected?.Invoke(this, _node);

            // 开始拖拽
            _isDragging = true;
            _lastMousePosition = e.GetPosition(Parent as UIElement);
            CaptureMouse();

            e.Handled = true;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);

            if (_isDragging && _node != null && IsMouseCaptured)
            {
                var currentPosition = e.GetPosition(Parent as UIElement);
                var deltaX = currentPosition.X - _lastMousePosition.X;
                var deltaY = currentPosition.Y - _lastMousePosition.Y;

                _node.X += deltaX;
                _node.Y += deltaY;

                Canvas.SetLeft(this, _node.X);
                Canvas.SetTop(this, _node.Y);

                _lastMousePosition = currentPosition;

                NodeMoved?.Invoke(this, _node);
            }
        }

        protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonUp(e);

            if (_isDragging)
            {
                _isDragging = false;
                ReleaseMouseCapture();
            }
        }

        protected override void OnMouseRightButtonDown(MouseButtonEventArgs e)
        {
            base.OnMouseRightButtonDown(e);

            if (_node == null) return;

            // 选中节点
            NodeSelected?.Invoke(this, _node);

            // 显示上下文菜单
            var contextMenu = new ContextMenu();

            var deleteItem = new MenuItem { Header = "删除节点" };
            deleteItem.Click += (s, args) =>
            {
                // 触发删除请求事件，让ViewModel处理删除逻辑
                NodeDeleteRequested?.Invoke(this, _node);
            };
            contextMenu.Items.Add(deleteItem);

            var propertiesItem = new MenuItem { Header = "属性" };
            propertiesItem.Click += (s, args) =>
            {
                // 显示属性面板
            };
            contextMenu.Items.Add(propertiesItem);

            // 添加分隔符
            contextMenu.Items.Add(new Separator());

            // 添加元数据查看选项
            var metadataItem = new MenuItem { Header = "元数据查看" };
            metadataItem.Click += (s, args) =>
            {
                ShowNodeMetadata();
            };
            contextMenu.Items.Add(metadataItem);

            ContextMenu = contextMenu;
            e.Handled = true;
        }

        /// <summary>
        /// 显示节点元数据
        /// </summary>
        private void ShowNodeMetadata()
        {
            if (_node == null)
            {
                MessageBox.Show("节点信息不可用", "错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                // MVVM解耦：不再直接从ImageProcessor获取元数据
                // 创建基本元数据用于显示
                var metadataManager = new MetadataManager();
                var basicData = new Dictionary<string, object>
                {
                    ["节点ID"] = _node.Id,
                    ["节点标题"] = _node.Title,
                    ["脚本路径"] = _node.ScriptPath ?? "",
                    ["参数数量"] = _node.Parameters.Count,
                    ["输入端口数量"] = _node.InputPorts.Count,
                    ["输出端口数量"] = _node.OutputPorts.Count
                };
                var metadata = metadataManager.CreateMetadata(basicData);

                // 显示元数据查看器窗口
                var metadataWindow = new MetadataViewerWindow(_node, metadata)
                {
                    Owner = Window.GetWindow(this)
                };
                metadataWindow.ShowDialog();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"显示元数据失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 获取端口在画布上的位置 - 修复多端口位置计算
        /// </summary>
        public Point GetPortPosition(string portName, bool isOutput)
        {
            var panel = isOutput ? OutputPortsPanel : InputPortsPanel;
            var ports = isOutput ? _node?.OutputPorts : _node?.InputPorts;

            if (ports == null) return new Point();

            var portIndex = ports.ToList().FindIndex(p => p.Name == portName);
            if (portIndex < 0 || portIndex >= panel.Children.Count)
            {
                return new Point();
            }

            var portElement = panel.Children[portIndex] as FrameworkElement;
            if (portElement == null)
            {
                return new Point();
            }

            try
            {
                // 获取节点在画布上的位置
                var nodePosition = new Point(Canvas.GetLeft(this), Canvas.GetTop(this));

                // 确保节点位置有效
                if (double.IsNaN(nodePosition.X)) nodePosition.X = _node?.X ?? 0;
                if (double.IsNaN(nodePosition.Y)) nodePosition.Y = _node?.Y ?? 0;

                // 获取端口元素的实际渲染边界
                var portBounds = portElement.RenderTransform.TransformBounds(new Rect(portElement.RenderSize));

                // 获取端口相对于节点的位置（端口中心）
                var portRelativePosition = portElement.TransformToAncestor(this).Transform(
                    new Point(portElement.ActualWidth / 2, portElement.ActualHeight / 2));

                // 计算端口在画布上的绝对位置
                var absolutePosition = new Point(
                    nodePosition.X + portRelativePosition.X,
                    nodePosition.Y + portRelativePosition.Y
                );



                return absolutePosition;
            }
            catch (Exception ex)
            {
                // 返回节点中心作为备用位置
                var nodePos = new Point(_node?.X ?? 0, _node?.Y ?? 0);
                return new Point(nodePos.X + 60, nodePos.Y + 40); // 节点中心附近
            }
        }

        /// <summary>
        /// 查找父级Canvas
        /// </summary>
        private Canvas? FindParentCanvas(DependencyObject element)
        {
            var current = element;
            int depth = 0;

            while (current != null && depth < 20) // 防止无限循环
            {
                depth++;

                if (current is Canvas canvas)
                {
                    // 找到Canvas，检查Name属性判断是否为NodeCanvas
                    string? canvasName = canvas.Name;

                    // 尝试获取名为NodeCanvas的画布，这是我们真正想要的
                    if (canvasName == "NodeCanvas")
                    {
                        return canvas;
                    }
                }

                try
                {
                    current = VisualTreeHelper.GetParent(current);
                }
                catch (Exception ex)
                {
                    break;
                }
            }

            // 如果遍历完整个视觉树仍未找到NodeCanvas，尝试从Window获取
            if (current == null)
            {
                try
                {
                    var window = Window.GetWindow(element);
                    if (window != null)
                    {
                        var nodeCanvas = FindElementByName(window, "NodeCanvas") as Canvas;
                        if (nodeCanvas != null)
                        {
                            return nodeCanvas;
                        }
                    }
                }
                catch (Exception ex)
                {
                }
            }

            return null;
        }

        /// <summary>
        /// 通过名称查找元素
        /// </summary>
        private DependencyObject? FindElementByName(DependencyObject parent, string name)
        {
            int count = VisualTreeHelper.GetChildrenCount(parent);

            for (int i = 0; i < count; i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);

                if (child is FrameworkElement fe && fe.Name == name)
                    return child;

                var result = FindElementByName(child, name);
                if (result != null)
                    return result;
            }

            return null;
        }

        /// <summary>
        /// 根据端口数据类型获取Aero风格颜色（已废弃，请使用 PortTypeDefinitions.GetPortWpfColor）
        /// </summary>
        [Obsolete("请使用 PortTypeDefinitions.GetPortWpfColor 方法")]
        private Color GetPortColorByDataType(string dataType)
        {
            var portDataType = PortTypeDefinitions.GetPortDataType(dataType);
            return PortTypeDefinitions.GetPortWpfColor(portDataType);
        }
    }
}
