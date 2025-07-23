using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using Tunnel_Next.Models;
using Tunnel_Next.ViewModels;

namespace Tunnel_Next.Controls
{
    /// <summary>
    /// NodePreviewControl.xaml 的交互逻辑
    /// 简化版节点预览控件，移除所有交互功能，用于批量处理器中的预览
    /// </summary>
    public partial class NodePreviewControl : UserControl
    {
        private NodeEditorViewModel? _viewModel;
        private readonly Dictionary<int, UIElement> _nodeElements = new();
        private readonly Dictionary<int, Path> _connectionPaths = new();
        private readonly Dictionary<string, UIElement> _portElements = new();
        private Point _lastViewportCenter;
        private double _scaleFactor = 1.0;

        // 添加只读属性（对于预览控件，此属性无实际功能，但保持API兼容性）
        public bool IsReadOnly { get; set; } = true;

        public NodePreviewControl()
        {
            InitializeComponent();
            DataContextChanged += NodePreviewControl_DataContextChanged;
        }

        private void NodePreviewControl_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (e.OldValue is NodeEditorViewModel oldViewModel)
            {
                UnregisterEvents(oldViewModel);
            }

            if (e.NewValue is NodeEditorViewModel newViewModel)
            {
                _viewModel = newViewModel;
                RegisterEvents(newViewModel);
                RebuildVisuals();
            }
            else
            {
                _viewModel = null;
                ClearVisuals();
            }
        }

        private void RegisterEvents(NodeEditorViewModel viewModel)
        {
            // 只注册必要的事件，监听数据变化以更新视图
            viewModel.Nodes.CollectionChanged += Nodes_CollectionChanged;
            viewModel.Connections.CollectionChanged += Connections_CollectionChanged;
        }

        private void UnregisterEvents(NodeEditorViewModel viewModel)
        {
            viewModel.Nodes.CollectionChanged -= Nodes_CollectionChanged;
            viewModel.Connections.CollectionChanged -= Connections_CollectionChanged;
        }

        private void Nodes_CollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            RebuildVisuals();
        }

        private void Connections_CollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            RebuildVisuals();
        }

        /// <summary>
        /// 重建所有可视元素
        /// </summary>
        private void RebuildVisuals()
        {
            ClearVisuals();
            if (_viewModel == null) return;

            // 绘制所有节点
            foreach (var node in _viewModel.Nodes)
            {
                AddNodeVisual(node);
            }

            // 绘制所有连接
            foreach (var connection in _viewModel.Connections)
            {
                AddConnectionVisual(connection);
            }

            // 居中显示所有节点
            CenterViewOnGraph();
        }

        /// <summary>
        /// 清除所有可视元素
        /// </summary>
        private void ClearVisuals()
        {
            NodesCanvas.Children.Clear();
            ConnectionsCanvas.Children.Clear();
            _nodeElements.Clear();
            _connectionPaths.Clear();
            _portElements.Clear();
        }

        /// <summary>
        /// 添加节点的可视元素
        /// </summary>
        private void AddNodeVisual(Node node)
        {
            // 创建节点容器
            var nodeContainer = new Grid
            {
                Width = node.Width,
                Height = node.Height
            };

            // 创建节点背景
            var nodeBorder = new Border
            {
                Style = (Style)FindResource("NodePreviewStyle"),
                Width = node.Width,
                Height = node.Height
            };

            // 创建节点内容区
            var contentStack = new StackPanel();

            // 创建节点标题区域
            var titleBorder = new Border
            {
                Style = (Style)FindResource("NodeTitleStyle")
            };

            // 创建节点标题
            var titleTextBlock = new TextBlock
            {
                Text = node.Title,
                Foreground = new SolidColorBrush(Colors.White),
                FontWeight = FontWeights.Bold,
                HorizontalAlignment = HorizontalAlignment.Center,
                Effect = new DropShadowEffect { 
                    ShadowDepth = 1, 
                    BlurRadius = 2, 
                    Opacity = 0.7,
                    Color = Colors.Black
                }
            };

            titleBorder.Child = titleTextBlock;
            contentStack.Children.Add(titleBorder);

            // 创建节点内容面板
            var contentBorder = new Border
            {
                Style = (Style)FindResource("NodeContentStyle")
            };
            
            var portsStack = new StackPanel();

            // 添加输入端口
            foreach (var port in node.InputPorts)
            {
                var portElement = CreatePortElement(port, node, isInput: true);
                portsStack.Children.Add(portElement);
                _portElements[GetPortKey(node.Id, port.Name)] = portElement;
            }

            // 添加输出端口
            foreach (var port in node.OutputPorts)
            {
                var portElement = CreatePortElement(port, node, isInput: false);
                portsStack.Children.Add(portElement);
                _portElements[GetPortKey(node.Id, port.Name)] = portElement;
            }

            contentBorder.Child = portsStack;
            contentStack.Children.Add(contentBorder);

            // 组装节点UI
            nodeContainer.Children.Add(nodeBorder);
            nodeContainer.Children.Add(contentStack);

            // 将节点添加到画布
            Canvas.SetLeft(nodeContainer, node.X);
            Canvas.SetTop(nodeContainer, node.Y);
            NodesCanvas.Children.Add(nodeContainer);
            _nodeElements[node.Id] = nodeContainer;
        }

        /// <summary>
        /// 创建端口UI元素
        /// </summary>
        private UIElement CreatePortElement(NodePort port, Node node, bool isInput)
        {
            var portBorder = new Border
            {
                Style = (Style)FindResource("PortPanelStyle")
            };

            var portPanel = new DockPanel();

            var portEllipse = new Ellipse
            {
                Style = (Style)FindResource("PortPreviewStyle"),
                // 根据端口类型设置不同颜色
                Fill = new SolidColorBrush(GetPortColor(port.DataType))
            };

            var portText = new TextBlock
            {
                Text = port.Name,
                Style = (Style)FindResource("PortTextStyle"),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(5, 0, 5, 0)
            };

            if (isInput)
            {
                DockPanel.SetDock(portEllipse, Dock.Left);
                portPanel.Children.Add(portEllipse);
                portPanel.Children.Add(portText);
            }
            else
            {
                DockPanel.SetDock(portEllipse, Dock.Right);
                portPanel.Children.Add(portText);
                portPanel.Children.Add(portEllipse);
                portText.TextAlignment = TextAlignment.Right;
            }

            portBorder.Child = portPanel;
            return portBorder;
        }

        /// <summary>
        /// 获取端口颜色
        /// </summary>
        private Color GetPortColor(NodePortDataType portType)
        {
            // 根据端口类型返回不同颜色
            return portType switch
            {
                NodePortDataType.Image => Colors.LightBlue,
                NodePortDataType.F32bmp => Colors.LightBlue,
                NodePortDataType.Number => Colors.LightGreen,
                NodePortDataType.String => Colors.LightYellow,
                NodePortDataType.Boolean => Colors.Orange,
                NodePortDataType.Any => Colors.Pink,
                _ => Colors.Gray
            };
        }

        /// <summary>
        /// 添加连接线的可视元素
        /// </summary>
        private void AddConnectionVisual(NodeConnection connection)
        {
            if (connection.OutputNode == null || connection.InputNode == null)
                return;

            // 获取起始和结束端口的位置
            var outputPort = connection.GetOutputPort();
            var inputPort = connection.GetInputPort();

            if (outputPort == null || inputPort == null)
                return;

            // 创建路径
            var path = new Path
            {
                Style = (Style)FindResource("ConnectionPreviewStyle"),
                Data = new PathGeometry()
            };

            // 获取端口在画布中的位置
            var startPoint = GetPortPosition(connection.OutputNode, outputPort, isInput: false);
            var endPoint = GetPortPosition(connection.InputNode, inputPort, isInput: true);

            // 使用简化的贝塞尔曲线
            var pathSegment = CreateSimpleBezierSegment(startPoint, endPoint);
            
            var figure = new PathFigure
            {
                StartPoint = startPoint
            };
            figure.Segments.Add(pathSegment);
            
            ((PathGeometry)path.Data).Figures.Add(figure);

            // 添加到画布
            ConnectionsCanvas.Children.Add(path);
            _connectionPaths[connection.Id] = path;
        }

        /// <summary>
        /// 创建简化的贝塞尔曲线
        /// </summary>
        private BezierSegment CreateSimpleBezierSegment(Point start, Point end)
        {
            // 使用更精美的贝塞尔曲线，控制点距离由距离决定
            double distance = Math.Abs(end.X - start.X) * 0.7;
            
            // 限制距离，使曲线更优美
            distance = Math.Min(distance, 200);
            distance = Math.Max(distance, 40);

            // 添加Y轴偏移，让曲线更自然
            double yOffset = (end.Y - start.Y) * 0.1;
            
            var control1 = new Point(start.X + distance, start.Y + yOffset);
            var control2 = new Point(end.X - distance, end.Y - yOffset);

            return new BezierSegment(control1, control2, end, true);
        }

        /// <summary>
        /// 获取端口在画布中的位置
        /// </summary>
        private Point GetPortPosition(Node node, NodePort port, bool isInput)
        {
            var nodeElement = _nodeElements.TryGetValue(node.Id, out var element) ? element : null;
            if (nodeElement == null) return new Point(0, 0);

            var nodeX = Canvas.GetLeft(nodeElement);
            var nodeY = Canvas.GetTop(nodeElement);

            // 找到端口在节点中的索引
            var ports = isInput ? node.InputPorts : node.OutputPorts;
            int portIndex = -1;
            for (int i = 0; i < ports.Count; i++)
            {
                if (ports[i].Name == port.Name) // Changed from port.Id to port.Name
                {
                    portIndex = i;
                    break;
                }
            }

            if (portIndex == -1) return new Point(0, 0);

            // 计算端口位置（简化版，不考虑实际UI排列）
            double portY = nodeY + 40 + (portIndex * 20); // 估算位置：标题高度 + 端口索引 * 端口高度

            if (isInput)
            {
                return new Point(nodeX, portY);
            }
            else
            {
                return new Point(nodeX + node.Width, portY);
            }
        }

        /// <summary>
        /// 居中显示所有节点
        /// </summary>
        private void CenterViewOnGraph()
        {
            if (_viewModel?.Nodes.Count == 0) return;

            // 计算所有节点的边界
            double minX = double.MaxValue;
            double minY = double.MaxValue;
            double maxX = double.MinValue;
            double maxY = double.MinValue;

            foreach (var node in _viewModel.Nodes)
            {
                minX = Math.Min(minX, node.X);
                minY = Math.Min(minY, node.Y);
                maxX = Math.Max(maxX, node.X + node.Width);
                maxY = Math.Max(maxY, node.Y + node.Height);
            }

            // 计算中心点
            double centerX = (minX + maxX) / 2;
            double centerY = (minY + maxY) / 2;

            // 滚动到中心
            MainScrollViewer.ScrollToHorizontalOffset(centerX - MainScrollViewer.ViewportWidth / 2);
            MainScrollViewer.ScrollToVerticalOffset(centerY - MainScrollViewer.ViewportHeight / 2);

            // 保存视口中心点
            _lastViewportCenter = new Point(centerX, centerY);
        }

        /// <summary>
        /// 获取端口的唯一键
        /// </summary>
        private string GetPortKey(int nodeId, string portName)
        {
            return $"{nodeId}:{portName}";
        }
    }
} 