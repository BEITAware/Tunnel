using System;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using Tunnel_Next.Models;
using Tunnel_Next.ViewModels;
using Tunnel_Next.Services.ImageProcessing;
using Tunnel_Next.Services;

namespace Tunnel_Next.Controls
{
    /// <summary>
    /// 节点编辑器控件
    /// </summary>
    public partial class NodeEditorControl : UserControl
    {
        private NodeEditorViewModel? _viewModel;
        private readonly Dictionary<Node, NodeControl> _nodeControls = new();
        private readonly Dictionary<Node, PropertyChangedEventHandler> _nodePropertyHandlers = new();
        private Path? _previewConnectionPath;

        // 新的操作逻辑字段
        private bool _isMiddleButtonPanning = false;
        private Point _lastPanPoint;
        private double _currentScale = 1.0;
        private const double MIN_SCALE = 0.2;  // 提高最小缩放，避免看到画布外
        private const double MAX_SCALE = 3.0;  // 降低最大缩放，避免过度放大
        private const double SCALE_FACTOR = 1.15; // 降低缩放步长，更平滑
        private const double PAN_SPEED = 60.0; // 稍微提高平移速度

        /// <summary>
        /// 图像处理服务引用（MVVM解耦），通过DataContext获取
        /// </summary>
        private IImageProcessingService? ProcessingService =>
            (_viewModel as NodeEditorViewModel)?.ProcessingService;

        /// <summary>
        /// 节点菜单服务引用，用于显示节点添加菜单
        /// </summary>
        public INodeMenuService? NodeMenuService { get; set; }

        public NodeEditorControl()
        {
            InitializeComponent();

            // 确保连接线Canvas始终在最下层（Z-Index越小越下层）
            Panel.SetZIndex(ConnectionCanvas, 1);
            // 节点Canvas必须在连接线Canvas之上
            Panel.SetZIndex(NodeCanvas, 2);

            // 启用键盘焦点
            Focusable = true;

            // 添加键盘事件处理
            KeyDown += OnKeyDown;

            // 添加鼠标中键事件处理
            MouseDown += OnMouseDown;
            MouseUp += OnMouseUp;
            MouseMove += OnMouseMove;

            // 确保控件可以获得焦点
            Loaded += (s, e) =>
            {
                Focus();
                // 不再自动居中，保持左上角为起始位置
            };

            DataContextChanged += OnDataContextChanged;
        }

        private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (e.NewValue is NodeEditorViewModel viewModel)
            {
                // 清除旧的事件订阅
                if (_viewModel != null)
                {
                    _viewModel.Nodes.CollectionChanged -= OnNodesCollectionChanged;
                    _viewModel.Connections.CollectionChanged -= OnConnectionsCollectionChanged;
                    _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
                    _viewModel.ClearUIRequested -= OnClearUIRequested;
                    _viewModel.UIClearedEvent -= OnUIClearedEvent; // 解除UI清理完成事件
                }

                _viewModel = viewModel;

                // 订阅新的事件
                _viewModel.Nodes.CollectionChanged += OnNodesCollectionChanged;
                _viewModel.Connections.CollectionChanged += OnConnectionsCollectionChanged;
                _viewModel.PropertyChanged += OnViewModelPropertyChanged;
                _viewModel.ClearUIRequested += OnClearUIRequested;
                _viewModel.UIClearedEvent += OnUIClearedEvent; // 订阅UI清理完成事件

                // 清除现有节点控件
                ClearAllNodeControls();

                // 添加新的节点控件
                foreach (var node in _viewModel.Nodes)
                {
                    AddNodeControl(node);
                }

                // 更新连接线
                UpdateConnections();

                // 更新所有节点的字体大小以适应当前缩放级别
                foreach (var nodeControl in _nodeControls.Values)
                {
                    nodeControl.UpdateFontSizeForScale(_currentScale);
                }
            }
        }
        
        /// <summary>
        /// 处理UI清理请求事件
        /// </summary>
        private async void OnClearUIRequested()
        {
            try
            {
                // 异步清理所有节点控件
                await ClearAllNodeControlsAsync();
                
                // 通知ViewModel清理完成
                _viewModel?.NotifyUIClearedEvent();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"UI清理失败: {ex.Message}");
                // 即使出错也要通知ViewModel清理完成
                _viewModel?.NotifyUIClearedEvent();
            }
        }
        
        /// <summary>
        /// 处理UI清理完成事件
        /// </summary>
        private void OnUIClearedEvent(object? sender, EventArgs e)
        {
            // 这个方法可以用来处理UI清理完成后的逻辑
            System.Diagnostics.Debug.WriteLine("UI清理完成事件已触发");
        }

        private void OnNodesCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.NewItems != null)
            {
                foreach (Node node in e.NewItems)
                {
                    AddNodeControl(node);
                }
            }

            if (e.OldItems != null)
            {
                foreach (Node node in e.OldItems)
                {
                    RemoveNodeControl(node);
                }
            }
        }

        private void OnConnectionsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {

            if (e.NewItems != null)
            {
                foreach (NodeConnection connection in e.NewItems)
                {
                }
            }

            UpdateConnections();
        }

        private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(NodeEditorViewModel.HasPendingConnection))
            {
                // 当连接状态变化时，如果没有待连接状态，清除预览连接线
                if (_viewModel != null && !_viewModel.HasPendingConnection)
                {
                    ClearPreviewConnection();
                }
            }
        }

        private void AddNodeControl(Node node)
        {
            var nodeControl = new NodeControl
            {
                Node = node
            };

            // 设置节点位置
            Canvas.SetLeft(nodeControl, node.X);
            Canvas.SetTop(nodeControl, node.Y);

            // 添加到画布
            NodeCanvas.Children.Add(nodeControl);

            // 应用当前缩放比例设置字体大小
            nodeControl.UpdateFontSizeForScale(_currentScale);

            // 添加到字典中
            _nodeControls[node] = nodeControl;

            // 添加节点出现动画
            nodeControl.PlayAddAnimation();

            // 监听节点属性变化
            var handler = new PropertyChangedEventHandler((s, e) =>
            {
                if (e.PropertyName == nameof(node.X) || e.PropertyName == nameof(node.Y))
                {
                    Canvas.SetLeft(nodeControl, node.X);
                    Canvas.SetTop(nodeControl, node.Y);
                    // 位置改变时重新绘制连接线
                    UpdateConnections();
                }
            });

            node.PropertyChanged += handler;
            _nodePropertyHandlers[node] = handler;

            nodeControl.NodeSelected += (s, n) =>
            {
                _viewModel?.SelectNode(n);
            };
            
            nodeControl.NodeMoved += (s, n) => UpdateConnections();
            nodeControl.NodeDeleteRequested += (s, n) => {
                _viewModel?.SelectNode(n);
                _viewModel?.DeleteNodeCommand?.Execute(null);
            };

            nodeControl.PortClicked += (s, args) => {
                _viewModel?.HandlePortClick(args.node, args.portName, args.isOutput);
            };

            // 绑定新的拖拽事件
            nodeControl.PortDragStarted += (s, args) => {
                _viewModel?.StartPortDrag(args.node, args.portName, args.isOutput);
            };

            nodeControl.PortDragMove += (s, args) => {
                // 更新拖拽预览连接线
                UpdateDragPreviewConnection(args.position);
            };

            // 绑定端口断开连接事件
            nodeControl.PortDisconnectRequested += (s, args) => {
                _viewModel?.DisconnectPort(args.node, args.portName, args.isOutput);
            };

            nodeControl.PortDragEnded += (s, args) => {
                HandlePortDragEnd(args.node, args.portName, args.isOutput, args.targetElement);
            };
        }

        private void RemoveNodeControl(Node node)
        {
            if (_nodeControls.TryGetValue(node, out var nodeControl))
            {
                // 解绑节点属性变化事件处理器
                if (_nodePropertyHandlers.TryGetValue(node, out var propertyHandler))
                {
                    node.PropertyChanged -= propertyHandler;
                    _nodePropertyHandlers.Remove(node);
                }

                // 解绑其他事件处理器，避免内存泄漏
                nodeControl.NodeSelected -= (s, n) => _viewModel?.SelectNode(n);
                nodeControl.NodeMoved -= (s, n) => UpdateConnections();
                nodeControl.NodeDeleteRequested -= (s, n) => {
                    _viewModel?.SelectNode(n);
                    _viewModel?.DeleteNodeCommand?.Execute(null);
                };

                // 播放删除动画，动画完成后移除节点
                try 
                {
                    nodeControl.PlayRemoveAnimation(() => {
                        try 
                        {
                            // 从画布中移除控件
                            NodeCanvas.Children.Remove(nodeControl);

                            // 从字典中移除
                            _nodeControls.Remove(node);
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"Node removal error: {ex.Message}");
                        }
                    });
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"PlayRemoveAnimation error: {ex.Message}");
                    // 出错时直接移除节点
                    NodeCanvas.Children.Remove(nodeControl);
                    _nodeControls.Remove(node);
                }
            }
            else
            {
            }
        }

        /// <summary>
        /// 清除所有节点控件（用于完全清除节点图）
        /// </summary>
        /// <returns>一个任务，表示清除操作完成</returns>
        public Task ClearAllNodeControlsAsync()
        {
            var tcs = new TaskCompletionSource<bool>();
            var nodesToRemove = _nodeControls.Keys.ToList();
            
            // 如果没有节点，直接返回已完成的任务
            if (nodesToRemove.Count == 0)
            {
                tcs.SetResult(true);
                return tcs.Task;
            }
            
            // 立即清理所有节点的事件处理器，防止内存泄漏
            foreach (var node in nodesToRemove)
            {
                if (_nodePropertyHandlers.TryGetValue(node, out var propertyHandler))
                {
                    node.PropertyChanged -= propertyHandler;
                    _nodePropertyHandlers.Remove(node);
                }
            }
                
            // 创建一个计数器来跟踪已完成的动画数量
            int completedAnimations = 0;
            int totalAnimations = nodesToRemove.Count;
            
            // 为每个节点创建延迟动画，使它们不会同时消失
            for (int i = 0; i < nodesToRemove.Count; i++)
            {
                var node = nodesToRemove[i];
                if (_nodeControls.TryGetValue(node, out var nodeControl))
                {
                    // 添加短延迟，使它们不会同时消失，但保持快速感
                    var delay = TimeSpan.FromMilliseconds(i * 15);
                    
                    // 使用Dispatcher延迟执行动画
                    Dispatcher.BeginInvoke(new Action(() => 
                    {
                        try
                        {
                            // 播放删除动画
                            nodeControl.PlayRemoveAnimation(() => 
                            {
                                try
                                {
                                    // 从画布中移除控件
                                    NodeCanvas.Children.Remove(nodeControl);
                                    
                                    // 从字典中移除
                                    _nodeControls.Remove(node);
                                    
                                    // 增加完成计数
                                    completedAnimations++;
                                    
                                    // 如果所有动画都完成了，清理剩余资源并完成任务
                                    if (completedAnimations >= totalAnimations)
                                    {
                                        // 确保画布完全清空，移除所有可能残留的节点控件
                                        NodeCanvas.Children.Clear();
                                        _nodeControls.Clear();
                                        _nodePropertyHandlers.Clear();
                                        
                                        // 通知任务完成
                                        tcs.TrySetResult(true);
                                    }
                                }
                                catch (Exception ex)
                                {
                                    System.Diagnostics.Debug.WriteLine($"Node removal completion error: {ex.Message}");
                                    // 即使出错也要增加计数，确保流程能继续
                                    completedAnimations++;
                                    
                                    if (completedAnimations >= totalAnimations)
                                    {
                                        // 确保任务能完成
                                        tcs.TrySetResult(true);
                                    }
                                }
                            });
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"Node animation start error: {ex.Message}");
                            // 出错时直接移除节点
                            try
                            {
                                NodeCanvas.Children.Remove(nodeControl);
                                _nodeControls.Remove(node);
                            }
                            catch { }
                            
                            completedAnimations++;
                            if (completedAnimations >= totalAnimations)
                            {
                                // 确保任务能完成
                                tcs.TrySetResult(true);
                            }
                        }
                    }), System.Windows.Threading.DispatcherPriority.ApplicationIdle, delay);
                }
                else
                {
                    // 如果找不到节点控件，也要增加计数
                    completedAnimations++;
                    if (completedAnimations >= totalAnimations)
                    {
                        // 确保任务能完成
                        tcs.TrySetResult(true);
                    }
                }
            }
            
            // 设置超时保障，防止任务永远不完成
            var timeoutTask = Task.Delay(3000); // 3秒超时
            timeoutTask.ContinueWith(_ => 
            {
                // 如果任务还没完成，强制完成
                if (!tcs.Task.IsCompleted)
                {
                    System.Diagnostics.Debug.WriteLine("节点清理操作超时，强制完成");
                    // 强制清理
                    Dispatcher.Invoke(() => 
                    {
                        NodeCanvas.Children.Clear();
                        _nodeControls.Clear();
                        _nodePropertyHandlers.Clear();
                    });
                    tcs.TrySetResult(true);
                }
            });
            
            return tcs.Task;
        }
        
        /// <summary>
        /// 清除所有节点控件（同步版本，为了保持兼容性）
        /// </summary>
        public void ClearAllNodeControls()
        {
            // 同步调用异步方法
            try
            {
                // 立即清理所有节点
                var nodesToRemove = _nodeControls.Keys.ToList();
                foreach (var node in nodesToRemove)
                {
                    if (_nodeControls.TryGetValue(node, out var nodeControl))
                    {
                        // 解绑节点属性变化事件处理器
                        if (_nodePropertyHandlers.TryGetValue(node, out var propertyHandler))
                        {
                            node.PropertyChanged -= propertyHandler;
                            _nodePropertyHandlers.Remove(node);
                        }
                        
                        // 立即移除节点控件，不播放动画
                        NodeCanvas.Children.Remove(nodeControl);
                    }
                }
                
                // 清空所有集合
                _nodeControls.Clear();
                _nodePropertyHandlers.Clear();
                NodeCanvas.Children.Clear();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"同步清理节点出错: {ex.Message}");
            }
        }

        private void UpdateConnections()
        {
            ConnectionCanvas.Children.Clear();

            if (_viewModel == null) return;

            foreach (var connection in _viewModel.Connections)
            {
                DrawConnection(connection);
            }
        }

        private void DrawConnection(NodeConnection connection)
        {
            if (!_nodeControls.TryGetValue(connection.OutputNode, out var outputNodeControl))
            {
                return;
            }

            if (!_nodeControls.TryGetValue(connection.InputNode, out var inputNodeControl))
            {
                return;
            }

            var startPoint = outputNodeControl.GetPortPosition(connection.OutputPortName, true);
            var endPoint = inputNodeControl.GetPortPosition(connection.InputPortName, false);

            // 获取端口类型以确定连接线颜色
            var outputPort = connection.OutputNode.OutputPorts.FirstOrDefault(p => p.Name == connection.OutputPortName);
            var portDataType = outputPort?.DataType.ToString() ?? "unknown";
            var portColor = Models.PortTypeDefinitions.GetPortWpfColor(outputPort?.DataType ?? NodePortDataType.Any);

            // 创建Aero风格的渐变连接线
            var connectionBrush = new LinearGradientBrush();
            connectionBrush.StartPoint = new Point(0, 0);
            connectionBrush.EndPoint = new Point(1, 1);
            connectionBrush.GradientStops.Add(new GradientStop(Color.FromArgb(255, portColor.R, portColor.G, portColor.B), 0));
            connectionBrush.GradientStops.Add(new GradientStop(Color.FromArgb(200, portColor.R, portColor.G, portColor.B), 0.5));
            connectionBrush.GradientStops.Add(new GradientStop(Color.FromArgb(255, portColor.R, portColor.G, portColor.B), 1));

            // 创建美化的贝塞尔曲线连接线
            var path = new Path
            {
                Stroke = connectionBrush,
                StrokeThickness = 4,
                Data = CreateConnectionPath(startPoint, endPoint),
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round,
                Opacity = 0.9
            };

            // 添加连接线阴影效果 - Aero风格
            path.Effect = new DropShadowEffect
            {
                Color = Color.FromArgb(100, 0, 0, 0),
                Direction = 315,
                ShadowDepth = 2,
                BlurRadius = 4,
                Opacity = 0.6
            };

            ConnectionCanvas.Children.Add(path);
        }

        private PathGeometry CreateConnectionPath(Point start, Point end)
        {
            var geometry = new PathGeometry();
            var figure = new PathFigure { StartPoint = start };

            // 计算控制点以创建平滑的贝塞尔曲线，类似Python版本
            var deltaX = Math.Abs(end.X - start.X);
            var controlOffset = Math.Max(80, deltaX * 0.4);

            var controlPoint1 = new Point(start.X + controlOffset, start.Y);
            var controlPoint2 = new Point(end.X - controlOffset, end.Y);

            var bezierSegment = new BezierSegment
            {
                Point1 = controlPoint1,
                Point2 = controlPoint2,
                Point3 = end
            };

            figure.Segments.Add(bezierSegment);
            geometry.Figures.Add(figure);

            return geometry;
        }

        private void NodeCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // 点击空白区域取消选择和重置连接状态
            if (e.OriginalSource == NodeCanvas)
            {
                _viewModel?.SelectNode(null!);

                // 如果有待连接状态，取消连接
                if (_viewModel != null && _viewModel.HasPendingConnection)
                {
                    _viewModel.CancelPendingConnection();
                    ClearPreviewConnection();
                }
            }
        }

        private void NodeCanvas_MouseMove(object sender, MouseEventArgs e)
        {
            // 如果正在中键拖拽，不处理Canvas的鼠标移动
            if (_isMiddleButtonPanning)
            {
                return;
            }

            // 如果有待连接状态，显示预览连接线
            if (_viewModel != null && _viewModel.HasPendingConnection)
            {
                var mousePosition = e.GetPosition(NodeCanvas);
                UpdatePreviewConnection(mousePosition);
            }
        }

        private void NodeCanvas_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            // 右键点击空白区域显示上下文菜单
            if (e.OriginalSource == NodeCanvas)
            {
                var position = e.GetPosition(NodeCanvas);
                ShowContextMenu(position);
            }
        }

        private void ShowContextMenu(Point position)
        {

            if (NodeMenuService != null)
            {
                // 使用NodeMenuService显示完整的节点添加菜单
                var contextMenu = NodeMenuService.CreateNodeAddMenu(nodeTypeName =>
                {
                    // 设置节点位置为鼠标位置（右键添加）
                    _viewModel?.SetPendingNodePosition(position);

                    // 选择节点类型后，在指定位置添加节点
                    _viewModel?.AddSpecificNodeCommand?.Execute(nodeTypeName);
                });

                // 添加其他菜单项
                if (_viewModel?.Nodes.Count > 0)
                {
                    contextMenu.Items.Add(new Separator());
                    var clearAllItem = new MenuItem { Header = "清除所有" };
                    clearAllItem.Click += (s, e) => _viewModel?.ClearAllCommand?.Execute(null);
                    contextMenu.Items.Add(clearAllItem);
                }

                // 设置菜单位置并显示
                contextMenu.PlacementTarget = NodeCanvas;
                contextMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.MousePoint;
                contextMenu.IsOpen = true;
            }
            else
            {
                // 回退到简单菜单（如果NodeMenuService未设置）
                var contextMenu = new ContextMenu();

                var addNodeItem = new MenuItem { Header = "添加节点" };
                addNodeItem.Click += (s, e) => _viewModel?.AddNodeCommand?.Execute(position);
                contextMenu.Items.Add(addNodeItem);

                if (_viewModel?.Nodes.Count > 0)
                {
                    contextMenu.Items.Add(new Separator());
                    var clearAllItem = new MenuItem { Header = "清除所有" };
                    clearAllItem.Click += (s, e) => _viewModel?.ClearAllCommand?.Execute(null);
                    contextMenu.Items.Add(clearAllItem);
                }

                NodeCanvas.ContextMenu = contextMenu;
            }
        }

        /// <summary>
        /// 更新预览连接线
        /// </summary>
        private void UpdatePreviewConnection(Point mousePosition)
        {
            if (_viewModel == null || !_viewModel.HasPendingConnection)
            {
                ClearPreviewConnection();
                return;
            }

            try
            {
                // 获取连接起点
                var startPoint = _viewModel.GetPendingConnectionStartPoint();
                if (startPoint == null)
                {
                    return;
                }

                // 清除旧的预览连接线
                ClearPreviewConnection();

                // 创建新的预览连接线 - Aero风格
                var previewBrush = new LinearGradientBrush();
                previewBrush.StartPoint = new Point(0, 0);
                previewBrush.EndPoint = new Point(1, 1);
                previewBrush.GradientStops.Add(new GradientStop(Color.FromRgb(0xFF, 0xD7, 0x00), 0)); // 金色
                previewBrush.GradientStops.Add(new GradientStop(Color.FromRgb(0xFF, 0xE7, 0x66), 0.5)); // 高亮金色
                previewBrush.GradientStops.Add(new GradientStop(Color.FromRgb(0xFF, 0xD7, 0x00), 1)); // 金色

                _previewConnectionPath = new Path
                {
                    Stroke = previewBrush,
                    StrokeThickness = 5,
                    StrokeDashArray = new DoubleCollection { 8, 4 }, // 更密集的虚线
                    Data = CreateConnectionPath(startPoint.Value, mousePosition),
                    StrokeStartLineCap = PenLineCap.Round,
                    StrokeEndLineCap = PenLineCap.Round,
                    Opacity = 0.9
                };

                // 添加发光效果
                _previewConnectionPath.Effect = new DropShadowEffect
                {
                    Color = Color.FromRgb(0xFF, 0xD7, 0x00), // 金色发光
                    Direction = 0,
                    ShadowDepth = 0,
                    BlurRadius = 8,
                    Opacity = 0.8
                };

                ConnectionCanvas.Children.Add(_previewConnectionPath);
            }
            catch (Exception ex)
            {
            }
        }

        /// <summary>
        /// 清除预览连接线
        /// </summary>
        private void ClearPreviewConnection()
        {
            if (_previewConnectionPath != null)
            {
                ConnectionCanvas.Children.Remove(_previewConnectionPath);
                _previewConnectionPath = null;
            }
        }

        /// <summary>
        /// 更新拖拽预览连接线
        /// </summary>
        private void UpdateDragPreviewConnection(Point mousePosition)
        {
            if (_viewModel == null || !_viewModel.HasPendingConnection)
            {
                ClearPreviewConnection();
                return;
            }

            try
            {
                // 获取连接起点
                Point? startPoint = null;

                // 从ViewModel尝试获取起点
                startPoint = _viewModel.GetPendingConnectionStartPoint();

                // 如果ViewModel没有提供有效的起点，自己计算
                if (startPoint == null)
                {
                    var pendingConnection = _viewModel.PendingConnection;
                    if (pendingConnection != null && pendingConnection.OutputNode != null)
                    {
                        var outputNode = pendingConnection.OutputNode;
                        var outputPortName = pendingConnection.OutputPortName;

                        // 查找对应的NodeControl
                        if (_nodeControls.TryGetValue(outputNode, out var nodeControl))
                        {
                            // 获取端口在画布上的位置
                            startPoint = nodeControl.GetPortPosition(outputPortName, true);
                        }
                    }
                }

                // 如果仍然无法获取起点，放弃绘制连接
                if (startPoint == null)
                {
                    return;
                }

                // 清除旧的预览连接线
                ClearPreviewConnection();

                // 创建新的拖拽预览连接线 - Aero风格
                var dragBrush = new LinearGradientBrush();
                dragBrush.StartPoint = new Point(0, 0);
                dragBrush.EndPoint = new Point(1, 1);
                dragBrush.GradientStops.Add(new GradientStop(Color.FromRgb(0xFF, 0xD7, 0x00), 0)); // 金色
                dragBrush.GradientStops.Add(new GradientStop(Color.FromRgb(0xFF, 0xE7, 0x66), 0.5)); // 高亮金色
                dragBrush.GradientStops.Add(new GradientStop(Color.FromRgb(0xFF, 0xD7, 0x00), 1)); // 金色

                _previewConnectionPath = new Path
                {
                    Stroke = dragBrush,
                    StrokeThickness = 5,
                    StrokeDashArray = new DoubleCollection { 8, 4 }, // 更密集的虚线
                    Data = CreateConnectionPath(startPoint.Value, mousePosition),
                    StrokeStartLineCap = PenLineCap.Round,
                    StrokeEndLineCap = PenLineCap.Round,
                    Opacity = 0.9
                };

                // 添加发光效果
                _previewConnectionPath.Effect = new DropShadowEffect
                {
                    Color = Color.FromRgb(0xFF, 0xD7, 0x00), // 金色发光
                    Direction = 0,
                    ShadowDepth = 0,
                    BlurRadius = 8,
                    Opacity = 0.8
                };

                ConnectionCanvas.Children.Add(_previewConnectionPath);
            }
            catch (Exception ex)
            {
            }
        }

        /// <summary>
        /// 处理端口拖拽结束
        /// </summary>
        private void HandlePortDragEnd(Node sourceNode, string sourcePortName, bool isOutput, FrameworkElement? targetElement)
        {

            // 清除预览连接线
            ClearPreviewConnection();

            if (targetElement == null)
            {
                _viewModel?.CancelPendingConnection();
                return;
            }

            // 查找目标端口
            var targetPort = FindTargetPort(targetElement, sourceNode, sourcePortName);
            if (targetPort == null)
            {
                _viewModel?.CancelPendingConnection();
                return;
            }

            var (targetNode, targetPortName, targetIsOutput) = targetPort.Value;

            // 验证连接是否有效（输出端口拖拽到输入端口）
            if (isOutput && !targetIsOutput)
            {
                _viewModel?.CompletePortDrag(targetNode, targetPortName, false);
            }
            else
            {
                _viewModel?.CancelPendingConnection();
            }
        }

        /// <summary>
        /// 查找目标端口
        /// </summary>
        private (Node node, string portName, bool isOutput)? FindTargetPort(FrameworkElement targetElement, Node? sourceNode = null, string? sourcePortName = null)
        {
            // 查找包含目标元素的NodeControl
            var nodeControl = FindParentNodeControl(targetElement);
            if (nodeControl?.Node == null)
            {
                return null;
            }

            // 检查是否是端口元素
            if (targetElement is Ellipse ellipse && ellipse.Tag is NodePort port)
            {
                // 排除拖拽源端口
                if (sourceNode != null && sourcePortName != null &&
                    nodeControl.Node == sourceNode && port.Name == sourcePortName)
                {
                    return null;
                }

                // 确定是输入端口还是输出端口
                bool isOutput = nodeControl.Node.OutputPorts.Contains(port);
                bool isInput = nodeControl.Node.InputPorts.Contains(port);


                if (isOutput && !isInput)
                {
                    return (nodeControl.Node, port.Name, true);
                }
                else if (isInput && !isOutput)
                {
                    return (nodeControl.Node, port.Name, false);
                }
                else
                {
                    return null;
                }
            }

            // 如果直接匹配失败，尝试通过UI元素索引查找端口
            var portByIndex = FindPortByUIIndex(targetElement, nodeControl);
            if (portByIndex != null)
            {
                var (portObj, isOutput) = portByIndex.Value;

                // 排除拖拽源端口
                if (sourceNode != null && sourcePortName != null &&
                    nodeControl.Node == sourceNode && portObj.Name == sourcePortName)
                {
                    return null;
                }

                return (nodeControl.Node, portObj.Name, isOutput);
            }

            // 最后尝试通过位置查找最近的端口
            var nearestPort = FindNearestPortByPosition(targetElement, nodeControl);
            if (nearestPort != null)
            {
                var (nearestPortObj, isNearestOutput) = nearestPort.Value;

                // 排除拖拽源端口
                if (sourceNode != null && sourcePortName != null &&
                    nodeControl.Node == sourceNode && nearestPortObj.Name == sourcePortName)
                {
                    return null;
                }

                return (nodeControl.Node, nearestPortObj.Name, isNearestOutput);
            }

            if (targetElement.Tag != null)
            {
            }
            return null;
        }

        /// <summary>
        /// 通过UI元素索引查找端口 - 精确匹配多端口节点
        /// </summary>
        private (NodePort port, bool isOutput)? FindPortByUIIndex(FrameworkElement targetElement, NodeControl nodeControl)
        {
            try
            {
                // 查找目标元素在输入端口面板中的索引
                var inputPanel = nodeControl.InputPortsPanel;
                var outputPanel = nodeControl.OutputPortsPanel;

                // 检查是否在输入端口面板中
                for (int i = 0; i < inputPanel.Children.Count; i++)
                {
                    var child = inputPanel.Children[i];
                    if (child == targetElement || IsChildOf(targetElement, child))
                    {
                        // 找到了在输入端口面板中的索引
                        if (i < nodeControl.Node.InputPorts.Count)
                        {
                            var port = nodeControl.Node.InputPorts[i];
                            return (port, false);
                        }
                        else
                        {
                        }
                        break;
                    }
                }

                // 检查是否在输出端口面板中
                for (int i = 0; i < outputPanel.Children.Count; i++)
                {
                    var child = outputPanel.Children[i];
                    if (child == targetElement || IsChildOf(targetElement, child))
                    {
                        // 找到了在输出端口面板中的索引
                        if (i < nodeControl.Node.OutputPorts.Count)
                        {
                            var port = nodeControl.Node.OutputPorts[i];
                            return (port, true);
                        }
                        else
                        {
                        }
                        break;
                    }
                }

                return null;
            }
            catch (Exception ex)
            {
                return null;
            }
        }

        /// <summary>
        /// 检查元素是否是另一个元素的子元素
        /// </summary>
        private bool IsChildOf(DependencyObject child, DependencyObject parent)
        {
            var current = child;
            while (current != null)
            {
                if (current == parent)
                    return true;
                current = VisualTreeHelper.GetParent(current);
            }
            return false;
        }

        /// <summary>
        /// 通过位置查找最近的端口 - 修复坐标转换和距离计算
        /// </summary>
        private (NodePort port, bool isOutput)? FindNearestPortByPosition(FrameworkElement targetElement, NodeControl nodeControl)
        {
            try
            {
                // 获取目标元素在画布上的位置
                var targetCanvasPosition = targetElement.TransformToAncestor(NodeCanvas).Transform(new Point(0, 0));

                NodePort? nearestPort = null;
                bool isNearestOutput = false;
                double minDistance = double.MaxValue;
                const double maxSearchDistance = 40.0; // 增加搜索距离到40像素

                // 检查输入端口
                for (int i = 0; i < nodeControl.Node.InputPorts.Count; i++)
                {
                    var port = nodeControl.Node.InputPorts[i];
                    var portCanvasPosition = nodeControl.GetPortPosition(port.Name, false);

                    var distance = Math.Sqrt(Math.Pow(targetCanvasPosition.X - portCanvasPosition.X, 2) +
                                           Math.Pow(targetCanvasPosition.Y - portCanvasPosition.Y, 2));


                    if (distance < minDistance && distance <= maxSearchDistance)
                    {
                        minDistance = distance;
                        nearestPort = port;
                        isNearestOutput = false;
                    }
                }

                // 检查输出端口
                for (int i = 0; i < nodeControl.Node.OutputPorts.Count; i++)
                {
                    var port = nodeControl.Node.OutputPorts[i];
                    var portCanvasPosition = nodeControl.GetPortPosition(port.Name, true);

                    var distance = Math.Sqrt(Math.Pow(targetCanvasPosition.X - portCanvasPosition.X, 2) +
                                           Math.Pow(targetCanvasPosition.Y - portCanvasPosition.Y, 2));

                    if (distance < minDistance && distance <= maxSearchDistance)
                    {
                        minDistance = distance;
                        nearestPort = port;
                        isNearestOutput = true;
                    }
                }

                if (nearestPort != null)
                {
                    return (nearestPort, isNearestOutput);
                }
                else
                {
                    return null;
                }
            }
            catch (Exception ex)
            {
                return null;
            }
        }

        /// <summary>
        /// 查找父级NodeControl
        /// </summary>
        private NodeControl? FindParentNodeControl(DependencyObject element)
        {
            var current = element;
            while (current != null)
            {
                if (current is NodeControl nodeControl)
                    return nodeControl;
                current = VisualTreeHelper.GetParent(current);
            }
            return null;
        }

        /// <summary>
        /// 根据端口类型获取颜色（已废弃，请使用 PortTypeDefinitions.GetPortWpfColor）
        /// </summary>
        [Obsolete("请使用 PortTypeDefinitions.GetPortWpfColor 方法")]
        private Color GetPortColor(string portType)
        {
            var dataType = PortTypeDefinitions.GetPortDataType(portType);
            return PortTypeDefinitions.GetPortWpfColor(dataType);
        }

        #region 新的操作逻辑

        /// <summary>
        /// 鼠标按下事件 - 处理中键
        /// </summary>
        private void OnMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.MiddleButton == MouseButtonState.Pressed)
            {
                _isMiddleButtonPanning = true;
                _lastPanPoint = e.GetPosition(this);
                CaptureMouse();
                Focus(); // 确保获得键盘焦点
                e.Handled = true;
            }
        }

        /// <summary>
        /// 鼠标释放事件 - 处理中键
        /// </summary>
        private void OnMouseUp(object sender, MouseButtonEventArgs e)
        {
            if (e.MiddleButton == MouseButtonState.Released && _isMiddleButtonPanning)
            {
                _isMiddleButtonPanning = false;
                ReleaseMouseCapture();
                e.Handled = true;
            }
        }

        /// <summary>
        /// 控件级别的鼠标移动事件 - 处理中键拖拽
        /// </summary>
        private void OnMouseMove(object sender, MouseEventArgs e)
        {
            if (_isMiddleButtonPanning)
            {
                var currentPoint = e.GetPosition(this);
                var deltaX = currentPoint.X - _lastPanPoint.X;
                var deltaY = currentPoint.Y - _lastPanPoint.Y;

                PanView(deltaX, deltaY);
                _lastPanPoint = currentPoint;
                e.Handled = true;
            }
        }

        /// <summary>
        /// 滚轮事件 - 处理移动和缩放
        /// </summary>
        private void NodeCanvas_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (Keyboard.Modifiers == ModifierKeys.Control)
            {
                // Control + 滚轮 = 缩放
                var mousePosition = e.GetPosition(NodeScrollViewer);
                var scaleDelta = e.Delta > 0 ? SCALE_FACTOR : 1.0 / SCALE_FACTOR;
                ZoomAtPoint(mousePosition, scaleDelta);
            }
            else if (Keyboard.Modifiers == ModifierKeys.Shift)
            {
                // Shift + 滚轮 = 水平移动
                var deltaX = e.Delta > 0 ? PAN_SPEED : -PAN_SPEED;
                PanView(deltaX, 0);
            }
            else
            {
                // 普通滚轮 = 垂直移动
                var deltaY = e.Delta > 0 ? PAN_SPEED : -PAN_SPEED;
                PanView(0, deltaY);
            }

            e.Handled = true;
        }

        /// <summary>
        /// 键盘事件 - 方向键移动
        /// </summary>
        private void OnKeyDown(object sender, KeyEventArgs e)
        {
            const double keyPanSpeed = 30.0;

            switch (e.Key)
            {
                case Key.Left:
                    PanView(keyPanSpeed, 0);
                    e.Handled = true;
                    break;
                case Key.Right:
                    PanView(-keyPanSpeed, 0);
                    e.Handled = true;
                    break;
                case Key.Up:
                    PanView(0, keyPanSpeed);
                    e.Handled = true;
                    break;
                case Key.Down:
                    PanView(0, -keyPanSpeed);
                    e.Handled = true;
                    break;
            }
        }

        /// <summary>
        /// 平移视图
        /// </summary>
        private void PanView(double deltaX, double deltaY)
        {
            // 使用ScrollViewer的滚动功能进行平移
            var newHorizontalOffset = NodeScrollViewer.HorizontalOffset - deltaX;
            var newVerticalOffset = NodeScrollViewer.VerticalOffset - deltaY;

            NodeScrollViewer.ScrollToHorizontalOffset(Math.Max(0, newHorizontalOffset));
            NodeScrollViewer.ScrollToVerticalOffset(Math.Max(0, newVerticalOffset));
        }

        /// <summary>
        /// 在指定点缩放 - 以鼠标位置为缩放中心
        /// </summary>
        private void ZoomAtPoint(Point mousePosition, double scaleDelta)
        {
            var newScale = Math.Max(MIN_SCALE, Math.Min(MAX_SCALE, _currentScale * scaleDelta));

            if (Math.Abs(newScale - _currentScale) < 0.001)
                return;

            // 获取当前滚动位置
            var currentHorizontalOffset = NodeScrollViewer.HorizontalOffset;
            var currentVerticalOffset = NodeScrollViewer.VerticalOffset;

            // 计算鼠标在画布内容中的绝对位置（考虑当前缩放）
            var mouseInContentX = (currentHorizontalOffset + mousePosition.X) / _currentScale;
            var mouseInContentY = (currentVerticalOffset + mousePosition.Y) / _currentScale;

            // 应用新的缩放
            var oldScale = _currentScale;
            _currentScale = newScale;
            ScaleTransform.ScaleX = _currentScale;
            ScaleTransform.ScaleY = _currentScale;

            // 更新所有节点的字体大小
            foreach (var nodeControl in _nodeControls.Values)
            {
                nodeControl.UpdateFontSizeForScale(_currentScale);
            }

            // 强制更新布局以应用缩放
            GridBackground.UpdateLayout();

            // 计算新的滚动位置，使鼠标位置在缩放后保持不变
            var newHorizontalOffset = mouseInContentX * _currentScale - mousePosition.X;
            var newVerticalOffset = mouseInContentY * _currentScale - mousePosition.Y;

            // 确保滚动位置不会超出边界
            var maxHorizontalOffset = Math.Max(0, GridBackground.Width * _currentScale - NodeScrollViewer.ViewportWidth);
            var maxVerticalOffset = Math.Max(0, GridBackground.Height * _currentScale - NodeScrollViewer.ViewportHeight);

            newHorizontalOffset = Math.Max(0, Math.Min(maxHorizontalOffset, newHorizontalOffset));
            newVerticalOffset = Math.Max(0, Math.Min(maxVerticalOffset, newVerticalOffset));

            NodeScrollViewer.ScrollToHorizontalOffset(newHorizontalOffset);
            NodeScrollViewer.ScrollToVerticalOffset(newVerticalOffset);
        }





        #endregion
    }
}
