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
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;

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
        private const double DEFAULT_FONT_SIZE = 12.0; // 默认字体大小
        private Random _random = new Random(); // 用于随机动画效果

        /// <summary>
        /// 移除ImageProcessor依赖，使用MVVM解耦架构
        /// 节点元数据现在通过节点本身获取
        /// </summary>

        public NodeControl()
        {
            InitializeComponent();
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        /// <summary>
        /// 播放节点添加动画
        /// </summary>
        public void PlayAddAnimation()
        {
            // 初始状态设置
            this.Opacity = 0;
            
            // 设置变换原点
            this.RenderTransformOrigin = new Point(0.5, 0.5);
            
            // 创建缩放变换，但保留现有变换类型（如果已存在）
            if (this.RenderTransform is ScaleTransform existingTransform)
            {
                existingTransform.ScaleX = 0.5;
                existingTransform.ScaleY = 0.5;
            }
            else
            {
                this.RenderTransform = new ScaleTransform(0.5, 0.5);
            }
            
            // 创建动画故事板
            var storyboard = new Storyboard();
            
            // 透明度动画
            var opacityAnimation = new DoubleAnimation
            {
                From = 0,
                To = 1,
                Duration = TimeSpan.FromMilliseconds(400),
                EasingFunction = new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.3 }
            };
            Storyboard.SetTarget(opacityAnimation, this);
            Storyboard.SetTargetProperty(opacityAnimation, new PropertyPath("Opacity"));
            storyboard.Children.Add(opacityAnimation);
            
            // X轴缩放动画 - 从小到大
            var scaleXAnimation = new DoubleAnimation
            {
                From = 0.5,
                To = 1.0,
                Duration = TimeSpan.FromMilliseconds(500),
                EasingFunction = new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.3 }
            };
            Storyboard.SetTarget(scaleXAnimation, this);
            Storyboard.SetTargetProperty(scaleXAnimation, new PropertyPath("RenderTransform.ScaleX"));
            storyboard.Children.Add(scaleXAnimation);
            
            // Y轴缩放动画 - 从小到大
            var scaleYAnimation = new DoubleAnimation
            {
                From = 0.5,
                To = 1.0,
                Duration = TimeSpan.FromMilliseconds(500),
                EasingFunction = new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.3 }
            };
            Storyboard.SetTarget(scaleYAnimation, this);
            Storyboard.SetTargetProperty(scaleYAnimation, new PropertyPath("RenderTransform.ScaleY"));
            storyboard.Children.Add(scaleYAnimation);
            
            // 添加阴影动画
            var shadowEffect = NodeBorder.Effect as DropShadowEffect;
            if (shadowEffect != null)
            {
                var shadowAnimation = new DoubleAnimation
                {
                    From = 0,
                    To = shadowEffect.BlurRadius,
                    Duration = TimeSpan.FromMilliseconds(500),
                    EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
                };
                Storyboard.SetTarget(shadowAnimation, NodeBorder);
                Storyboard.SetTargetProperty(shadowAnimation, new PropertyPath("Effect.BlurRadius"));
                storyboard.Children.Add(shadowAnimation);
            }
            
            // 保存故事板引用以便清理
            _addAnimationStoryboard = storyboard;
            
            // 动画完成后清理
            storyboard.Completed += OnAddAnimationCompleted;
            
            // 启动动画，明确指定目标对象
            storyboard.Begin(this, true);
        }
        
        // 添加动画的故事板引用
        private Storyboard? _addAnimationStoryboard;
        
        /// <summary>
        /// 添加动画完成事件处理
        /// </summary>
        private void OnAddAnimationCompleted(object sender, EventArgs e)
        {
            try
            {
                // 清理故事板事件
                if (_addAnimationStoryboard != null)
                {
                    _addAnimationStoryboard.Completed -= OnAddAnimationCompleted;
                    _addAnimationStoryboard = null;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Add animation completion error: {ex.Message}");
            }
        }
        
        /// <summary>
        /// 控件卸载事件处理
        /// </summary>
        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            // 控件卸载时清理所有资源
            Cleanup();
        }
        
        /// <summary>
        /// 播放节点移除动画
        /// </summary>
        /// <param name="completedCallback">动画完成后的回调</param>
        public void PlayRemoveAnimation(Action completedCallback)
        {
            // 保存回调以供后续使用
            _removeAnimationCallback = completedCallback;
            
            // 创建动画故事板
            var storyboard = new Storyboard();
            
            // 透明度动画 - 更快速
            var opacityAnimation = new DoubleAnimation
            {
                From = 1,
                To = 0,
                Duration = TimeSpan.FromMilliseconds(250), // 更快的动画
                EasingFunction = new ExponentialEase { EasingMode = EasingMode.EaseIn, Exponent = 3 } // 更急促的缓动
            };
            Storyboard.SetTarget(opacityAnimation, this);
            Storyboard.SetTargetProperty(opacityAnimation, new PropertyPath("Opacity"));
            storyboard.Children.Add(opacityAnimation);
            
            // 随机选择动画效果，增加灵动感
            var random = new Random();
            int effectType = random.Next(3); // 0, 1, 2 三种效果
            
            if (effectType == 0) // 缩小并旋转效果
            {
                // X轴缩放动画 - 快速缩小
                var scaleXAnimation = new DoubleAnimation
                {
                    From = 1.0,
                    To = 0.1,
                    Duration = TimeSpan.FromMilliseconds(250),
                    EasingFunction = new BackEase { EasingMode = EasingMode.EaseIn, Amplitude = 0.3 }
                };
                Storyboard.SetTarget(scaleXAnimation, this);
                Storyboard.SetTargetProperty(scaleXAnimation, new PropertyPath("RenderTransform.ScaleX"));
                storyboard.Children.Add(scaleXAnimation);
                
                // Y轴缩放动画 - 快速缩小
                var scaleYAnimation = new DoubleAnimation
                {
                    From = 1.0,
                    To = 0.1,
                    Duration = TimeSpan.FromMilliseconds(250),
                    EasingFunction = new BackEase { EasingMode = EasingMode.EaseIn, Amplitude = 0.3 }
                };
                Storyboard.SetTarget(scaleYAnimation, this);
                Storyboard.SetTargetProperty(scaleYAnimation, new PropertyPath("RenderTransform.ScaleY"));
                storyboard.Children.Add(scaleYAnimation);
                
                // 添加旋转动画
                var rotateAnimation = new DoubleAnimation
                {
                    To = random.Next(2) == 0 ? 90 : -90, // 随机顺时针或逆时针旋转
                    Duration = TimeSpan.FromMilliseconds(250),
                    EasingFunction = new CircleEase { EasingMode = EasingMode.EaseIn }
                };
                
                // 确保有RotateTransform
                if (this.RenderTransform is ScaleTransform)
                {
                    // 创建TransformGroup来组合缩放和旋转
                    var oldTransform = this.RenderTransform as ScaleTransform;
                    var transformGroup = new TransformGroup();
                    transformGroup.Children.Add(new ScaleTransform(oldTransform.ScaleX, oldTransform.ScaleY));
                    transformGroup.Children.Add(new RotateTransform(0));
                    this.RenderTransform = transformGroup;
                    
                    // 设置旋转动画目标
                    Storyboard.SetTarget(rotateAnimation, this);
                    Storyboard.SetTargetProperty(rotateAnimation, new PropertyPath("RenderTransform.Children[1].Angle"));
                    storyboard.Children.Add(rotateAnimation);
                }
            }
            else if (effectType == 1) // 弹出效果
            {
                // X轴缩放动画 - 先变大再快速缩小
                var scaleXAnimation = new DoubleAnimation
                {
                    From = 1.0,
                    To = 0.1,
                    Duration = TimeSpan.FromMilliseconds(250),
                    EasingFunction = new ElasticEase { EasingMode = EasingMode.EaseIn, Oscillations = 1, Springiness = 3 }
                };
                Storyboard.SetTarget(scaleXAnimation, this);
                Storyboard.SetTargetProperty(scaleXAnimation, new PropertyPath("RenderTransform.ScaleX"));
                storyboard.Children.Add(scaleXAnimation);
                
                // Y轴缩放动画 - 先变大再快速缩小
                var scaleYAnimation = new DoubleAnimation
                {
                    From = 1.0,
                    To = 0.1,
                    Duration = TimeSpan.FromMilliseconds(250),
                    EasingFunction = new ElasticEase { EasingMode = EasingMode.EaseIn, Oscillations = 1, Springiness = 3 }
                };
                Storyboard.SetTarget(scaleYAnimation, this);
                Storyboard.SetTargetProperty(scaleYAnimation, new PropertyPath("RenderTransform.ScaleY"));
                storyboard.Children.Add(scaleYAnimation);
            }
            else // 快速收缩效果
            {
                // X轴缩放动画 - 快速收缩
                var scaleXAnimation = new DoubleAnimation
                {
                    From = 1.0,
                    To = 0.1,
                    Duration = TimeSpan.FromMilliseconds(250),
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
                };
                Storyboard.SetTarget(scaleXAnimation, this);
                Storyboard.SetTargetProperty(scaleXAnimation, new PropertyPath("RenderTransform.ScaleX"));
                storyboard.Children.Add(scaleXAnimation);
                
                // Y轴缩放动画 - 快速收缩
                var scaleYAnimation = new DoubleAnimation
                {
                    From = 1.0,
                    To = 0.1,
                    Duration = TimeSpan.FromMilliseconds(250),
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
                };
                Storyboard.SetTarget(scaleYAnimation, this);
                Storyboard.SetTargetProperty(scaleYAnimation, new PropertyPath("RenderTransform.ScaleY"));
                storyboard.Children.Add(scaleYAnimation);
            }
            
            // 添加阴影动画
            var shadowEffect = NodeBorder.Effect as DropShadowEffect;
            if (shadowEffect != null)
            {
                var shadowAnimation = new DoubleAnimation
                {
                    From = shadowEffect.BlurRadius,
                    To = 0,
                    Duration = TimeSpan.FromMilliseconds(200), // 更快速的阴影消失
                    EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn }
                };
                Storyboard.SetTarget(shadowAnimation, NodeBorder);
                Storyboard.SetTargetProperty(shadowAnimation, new PropertyPath("Effect.BlurRadius"));
                storyboard.Children.Add(shadowAnimation);
            }
            
            // 动画完成后执行回调 - 使用实例方法而非lambda表达式
            storyboard.Completed += OnRemoveAnimationCompleted;
            
            // 保存故事板引用以便清理
            _removeAnimationStoryboard = storyboard;
            
            // 启动动画，明确指定目标对象
            storyboard.Begin(this, true);
        }
        
        // 移除动画的回调和故事板引用
        private Action? _removeAnimationCallback;
        private Storyboard? _removeAnimationStoryboard;
        
        /// <summary>
        /// 移除动画完成事件处理
        /// </summary>
        private void OnRemoveAnimationCompleted(object sender, EventArgs e)
        {
            try
            {
                // 清理故事板事件
                if (_removeAnimationStoryboard != null)
                {
                    _removeAnimationStoryboard.Completed -= OnRemoveAnimationCompleted;
                    _removeAnimationStoryboard = null;
                }
                
                // 执行回调
                _removeAnimationCallback?.Invoke();
                _removeAnimationCallback = null;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Remove animation completion error: {ex.Message}");
            }
        }
        
        /// <summary>
        /// 清理所有资源和事件
        /// </summary>
        public void Cleanup()
        {
            try
            {
                // 清理移除动画故事板
                if (_removeAnimationStoryboard != null)
                {
                    _removeAnimationStoryboard.Completed -= OnRemoveAnimationCompleted;
                    _removeAnimationStoryboard = null;
                }
                
                // 清理添加动画故事板
                if (_addAnimationStoryboard != null)
                {
                    _addAnimationStoryboard.Completed -= OnAddAnimationCompleted;
                    _addAnimationStoryboard = null;
                }
                
                // 清理回调
                _removeAnimationCallback = null;
                
                // 清理节点引用
                if (_node != null)
                {
                    _node.PropertyChanged -= OnNodePropertyChanged;
                    _node.InputPorts.CollectionChanged -= OnInputPortsChanged;
                    _node.OutputPorts.CollectionChanged -= OnOutputPortsChanged;
                    _node = null;
                }
                
                // 移除事件处理器
                Loaded -= OnLoaded;
                Unloaded -= OnUnloaded;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Cleanup error: {ex.Message}");
            }
        }

        /// <summary>
        /// 播放鼠标按下动画效果 (Metro风格)
        /// </summary>
        private void PlayMousePressAnimation(bool isPressed)
        {
            
            var storyboard = new Storyboard();
            
            if (isPressed)
            {
                // 按下效果：轻微缩小
                var scaleXAnimation = new DoubleAnimation
                {
                    To = 0.95,
                    Duration = TimeSpan.FromMilliseconds(100),
                    EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
                };
                Storyboard.SetTarget(scaleXAnimation, this);
                Storyboard.SetTargetProperty(scaleXAnimation, new PropertyPath("RenderTransform.ScaleX"));
                storyboard.Children.Add(scaleXAnimation);
                
                var scaleYAnimation = new DoubleAnimation
                {
                    To = 0.95,
                    Duration = TimeSpan.FromMilliseconds(100),
                    EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
                };
                Storyboard.SetTarget(scaleYAnimation, this);
                Storyboard.SetTargetProperty(scaleYAnimation, new PropertyPath("RenderTransform.ScaleY"));
                storyboard.Children.Add(scaleYAnimation);
                
                // 修改阴影效果
                var shadowEffect = NodeBorder.Effect as DropShadowEffect;
                if (shadowEffect != null)
                {
                    var shadowBlurAnimation = new DoubleAnimation
                    {
                        To = shadowEffect.BlurRadius * 0.7,
                        Duration = TimeSpan.FromMilliseconds(100)
                    };
                    Storyboard.SetTarget(shadowBlurAnimation, NodeBorder);
                    Storyboard.SetTargetProperty(shadowBlurAnimation, new PropertyPath("Effect.BlurRadius"));
                    storyboard.Children.Add(shadowBlurAnimation);
                    
                    var shadowDepthAnimation = new DoubleAnimation
                    {
                        To = 3,
                        Duration = TimeSpan.FromMilliseconds(100)
                    };
                    Storyboard.SetTarget(shadowDepthAnimation, NodeBorder);
                    Storyboard.SetTargetProperty(shadowDepthAnimation, new PropertyPath("Effect.ShadowDepth"));
                    storyboard.Children.Add(shadowDepthAnimation);
                }
            }
            else
            {
                // 释放效果：恢复正常大小，带有弹性效果
                var scaleXAnimation = new DoubleAnimation
                {
                    To = 1.0,
                    Duration = TimeSpan.FromMilliseconds(200),
                    EasingFunction = new ElasticEase { EasingMode = EasingMode.EaseOut, Oscillations = 1, Springiness = 2 }
                };
                Storyboard.SetTarget(scaleXAnimation, this);
                Storyboard.SetTargetProperty(scaleXAnimation, new PropertyPath("RenderTransform.ScaleX"));
                storyboard.Children.Add(scaleXAnimation);
                
                var scaleYAnimation = new DoubleAnimation
                {
                    To = 1.0,
                    Duration = TimeSpan.FromMilliseconds(200),
                    EasingFunction = new ElasticEase { EasingMode = EasingMode.EaseOut, Oscillations = 1, Springiness = 2 }
                };
                Storyboard.SetTarget(scaleYAnimation, this);
                Storyboard.SetTargetProperty(scaleYAnimation, new PropertyPath("RenderTransform.ScaleY"));
                storyboard.Children.Add(scaleYAnimation);
                
                // 恢复阴影效果
                var shadowEffect = NodeBorder.Effect as DropShadowEffect;
                if (shadowEffect != null)
                {
                    var shadowBlurAnimation = new DoubleAnimation
                    {
                        To = 12,
                        Duration = TimeSpan.FromMilliseconds(200)
                    };
                    Storyboard.SetTarget(shadowBlurAnimation, NodeBorder);
                    Storyboard.SetTargetProperty(shadowBlurAnimation, new PropertyPath("Effect.BlurRadius"));
                    storyboard.Children.Add(shadowBlurAnimation);
                    
                    var shadowDepthAnimation = new DoubleAnimation
                    {
                        To = 6,
                        Duration = TimeSpan.FromMilliseconds(200)
                    };
                    Storyboard.SetTarget(shadowDepthAnimation, NodeBorder);
                    Storyboard.SetTargetProperty(shadowDepthAnimation, new PropertyPath("Effect.ShadowDepth"));
                    storyboard.Children.Add(shadowDepthAnimation);
                }
            }
            
            storyboard.Begin();
        }

        /// <summary>
        /// 根据当前缩放比例更新字体大小
        /// </summary>
        /// <param name="scale">当前缩放比例</param>
        public void UpdateFontSizeForScale(double scale)
        {
            // 查找标题文本块
            var titleTextBlock = TitleTextBlock;
            
            if (titleTextBlock != null)
            {
                // 当缩放比例小于1时，增大字体大小，但不超过节点本身
                if (scale < 1.0)
                {
                    // 反比例调整：缩放越小，字体越大，但有上限
                    double scaleFactor = Math.Min(3.0, 1.0 / scale); // 最大放大3倍
                    double newFontSize = DEFAULT_FONT_SIZE * Math.Min(scaleFactor, 1.5); // 限制最大字体大小
                    titleTextBlock.FontSize = newFontSize;
                }
                else
                {
                    // 恢复默认字体大小
                    titleTextBlock.FontSize = DEFAULT_FONT_SIZE;
                }
            }
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

        // 端口断开连接事件
        public event EventHandler<(Node node, string portName, bool isOutput)>? PortDisconnectRequested;
        
        /// <summary>
        /// 保存为静态节点请求事件
        /// </summary>
        public event EventHandler<(Node node, string portName, bool isOutput)>? SaveAsStaticNodeRequested;

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            if (_node != null)
            {
                Canvas.SetLeft(this, _node.X);
                Canvas.SetTop(this, _node.Y);
            }
            
            // 设置变换原点，但不重置变换本身
            if (this.RenderTransformOrigin != new Point(0.5, 0.5))
            {
                this.RenderTransformOrigin = new Point(0.5, 0.5);
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
                    // 确保_node不为null
                    if (_node != null)
                    {
                        // 输入端口不开始拖拽，只能作为连接目标
                        PortClicked?.Invoke(this, (_node, port.Name, false));
                    }
                    e.Handled = true;
                };

                // 添加右键菜单处理
                portEllipse.MouseRightButtonDown += (s, e) =>
                {
                    // 确保_node不为null
                    if (_node != null)
                    {
                        ShowPortContextMenu(port, false, portEllipse);
                    }
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
                        // 确保_node不为null
                        if (_node != null)
                        {
                            // 开始拖拽连接
                            PortDragStarted?.Invoke(this, (_node, port.Name, true));

                            // 捕获鼠标，开始拖拽
                            portEllipse.CaptureMouse();
                        }
                    }
                    catch (Exception ex) {
                        System.Diagnostics.Debug.WriteLine($"Port drag start error: {ex.Message}");
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
                    if (portEllipse.IsMouseCaptured && e.LeftButton == MouseButtonState.Pressed && _node != null)
                    {
                        try {
                            // 获取相对于画布的位置
                            var canvas = FindParentCanvas(this);

                            var position = canvas != null ? e.GetPosition(canvas) : e.GetPosition(Parent as UIElement);

                            // 确保_node不为null
                            PortDragMove?.Invoke(this, (_node, port.Name, true, position));
                        }
                        catch (Exception ex) {
                            // 记录异常信息
                            System.Diagnostics.Debug.WriteLine($"Port drag move error: {ex.Message}");
                        }
                    }
                };

                portEllipse.MouseLeftButtonUp += (s, e) =>
                {
                    if (portEllipse.IsMouseCaptured)
                    {
                        try {
                            portEllipse.ReleaseMouseCapture();

                            // 确保_node不为null
                            if (_node != null)
                            {
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
                        }
                        catch (Exception ex) {
                            // 确保即使出现异常也能释放鼠标
                            try { portEllipse.ReleaseMouseCapture(); } catch { }
                            System.Diagnostics.Debug.WriteLine($"Port drag end error: {ex.Message}");
                        }

                        e.Handled = true;
                    }
                };

                // 添加右键菜单处理
                portEllipse.MouseRightButtonDown += (s, e) =>
                {
                    // 确保_node不为null
                    if (_node != null)
                    {
                        ShowPortContextMenu(port, true, portEllipse);
                    }
                    e.Handled = true;
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
            Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Normal, new Action(UpdatePorts));
        }

        /// <summary>
        /// 输出端口集合变化事件处理
        /// </summary>
        private void OnOutputPortsChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            // 当端口集合发生变化时，重新更新端口UI
            Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Normal, new Action(UpdatePorts));
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

            // 播放Metro风格按下动画
            PlayMousePressAnimation(true);

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

            // 播放Metro风格释放动画
            PlayMousePressAnimation(false);

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
                // 获取节点的实际输出元数据
                var metadata = new Dictionary<string, object>();

                // 尝试从节点的ProcessedOutputs中获取元数据
                if (_node.ProcessedOutputs.TryGetValue("_metadata", out var nodeMetadata) &&
                    nodeMetadata is Dictionary<string, object> metadataDict)
                {
                    metadata = metadataDict;
                }

                // 显示元数据查看器窗口
                var metadataWindow = new MetadataViewerWindow(_node, metadata);

                // 安全地设置Owner属性
                var parentWindow = Window.GetWindow(this);
                if (parentWindow != null && parentWindow != metadataWindow)
                {
                    metadataWindow.Owner = parentWindow;
                }

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

        /// <summary>
        /// 显示端口上下文菜单
        /// </summary>
        private void ShowPortContextMenu(NodePort port, bool isOutput, FrameworkElement portElement)
        {
            if (_node == null || port == null || portElement == null) return;

            System.Diagnostics.Debug.WriteLine($"[NodeControl] 显示端口上下文菜单: 节点={_node.Title}, 端口名称={port.Name}, 是否为输出端口={isOutput}, 端口值={(port.Value != null ? "有值" : "无值")}");

            var contextMenu = new ContextMenu();

            // 输出端口添加"保存为静态节点"选项
            if (isOutput && _node != null)
            {
                // 检查ProcessedOutputs中是否有此端口的数据
                if (_node.ProcessedOutputs.TryGetValue(port.Name, out var portValue) && portValue != null)
                {
                    System.Diagnostics.Debug.WriteLine($"[NodeControl] 添加保存静态节点菜单项: 端口值类型={portValue.GetType().Name}");
                    var saveAsStaticNodeItem = new MenuItem { Header = "保存为静态节点" };
                    saveAsStaticNodeItem.Click += (s, args) =>
                    {
                        // 触发保存静态节点事件
                        SaveAsStaticNodeRequested?.Invoke(this, (_node, port.Name, isOutput));
                    };
                    contextMenu.Items.Add(saveAsStaticNodeItem);
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"[NodeControl] 未添加保存静态节点菜单项: ProcessedOutputs中找不到端口值");
                }
            }

            // 检查端口是否有连接
            if (port.IsConnected)
            {
                var disconnectItem = new MenuItem { Header = "断开此链接" };
                disconnectItem.Click += (s, args) =>
                {
                    // 触发端口断开连接请求事件
                    PortDisconnectRequested?.Invoke(this, (_node, port.Name, isOutput));
                };
                contextMenu.Items.Add(disconnectItem);
            }

            // 如果菜单没有项目，不显示
            if (contextMenu.Items.Count == 0)
            {
                System.Diagnostics.Debug.WriteLine($"[NodeControl] 上下文菜单没有项目，不显示");
                return;
            }

            System.Diagnostics.Debug.WriteLine($"[NodeControl] 显示上下文菜单: 项目数量={contextMenu.Items.Count}");

            // 设置菜单位置并显示 - 使用端口元素作为目标，在端口下方显示
            contextMenu.PlacementTarget = portElement;
            contextMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
            contextMenu.IsOpen = true;
        }
    }
}
