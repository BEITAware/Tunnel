using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using Tunnel_Next.Models;
using Microsoft.Win32;
using System;
using System.Collections.Generic;

namespace Tunnel_Next.Controls
{
    /// <summary>
    /// ParameterEditorControl.xaml 的交互逻辑
    /// </summary>
    public partial class ParameterEditorControl : UserControl
    {
        public static readonly DependencyProperty SelectedNodeProperty =
            DependencyProperty.Register(nameof(SelectedNode), typeof(Node), typeof(ParameterEditorControl),
                new PropertyMetadata(null, OnSelectedNodeChanged));

        public Node? SelectedNode
        {
            get => (Node?)GetValue(SelectedNodeProperty);
            set
            {
                SetValue(SelectedNodeProperty, value);
            }
        }

        // 动态UI支持
        private Services.Scripting.IDynamicUIScript? _currentDynamicScript;
        private FrameworkElement? _currentScriptControl;
        private string? _lastUIToken;

        public ParameterEditorControl()
        {
            InitializeComponent();

            // 监听DataContext变化，如果DataContext是NodeEditorViewModel，则绑定其SelectedNode
            DataContextChanged += OnDataContextChanged;
        }

        private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            // 清除旧的事件订阅
            if (e.OldValue is ViewModels.NodeEditorViewModel oldViewModel)
            {
                oldViewModel.ConnectionsChanged -= OnConnectionsChanged;
            }

            if (DataContext is ViewModels.NodeEditorViewModel nodeEditor)
            {
                // 绑定到NodeEditor的SelectedNode属性
                var binding = new System.Windows.Data.Binding("SelectedNode")
                {
                    Source = nodeEditor,
                    Mode = System.Windows.Data.BindingMode.OneWay
                };
                SetBinding(SelectedNodeProperty, binding);

                // 订阅连接变化事件
                nodeEditor.ConnectionsChanged += OnConnectionsChanged;

                // 延迟更新参数编辑器，确保节点重建完成
                _ = DelayedUpdateParameterEditor();
            }
            else
            {
                // 清除绑定
                BindingOperations.ClearBinding(this, SelectedNodeProperty);
                SelectedNode = null;
            }
        }

        /// <summary>
        /// 延迟更新参数编辑器，确保节点重建完成
        /// </summary>
        private async Task DelayedUpdateParameterEditor()
        {
            try
            {
                // 等待UI线程处理完成，确保节点重建完成
                await Dispatcher.InvokeAsync(() => { }, System.Windows.Threading.DispatcherPriority.Loaded);

                // 再次等待，确保所有异步操作完成
                await Task.Delay(100);

                // 在UI线程上更新参数编辑器
                await Dispatcher.InvokeAsync(() =>
                {
                    UpdateParameterEditor();
                }, System.Windows.Threading.DispatcherPriority.Normal);
            }
            catch (Exception ex)
            {
            }
        }

        private static void OnSelectedNodeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ParameterEditorControl control)
            {
                control.OnSelectedNodeChangedInternal(e.OldValue as Node, e.NewValue as Node);
            }
        }

        private void OnSelectedNodeChangedInternal(Node? oldNode, Node? newNode)
        {
            // 清除旧节点的动态UI订阅
            if (_currentDynamicScript != null)
            {
                _currentDynamicScript.UIUpdateRequested -= OnUIUpdateRequested;
                _currentDynamicScript = null;
            }

            // 设置新节点的动态UI订阅
            if (newNode?.Tag is Services.Scripting.IDynamicUIScript dynamicScript)
            {
                _currentDynamicScript = dynamicScript;
                _currentDynamicScript.UIUpdateRequested += OnUIUpdateRequested;
            }

            _currentScriptControl = null;
            _lastUIToken = null;
            UpdateParameterEditor();
        }

        private void OnConnectionsChanged()
        {
            // 连接变化时，通知动态脚本并尝试更新UI
            if (_currentDynamicScript != null && SelectedNode != null)
            {
                var connectionInfo = BuildConnectionInfo(SelectedNode);
                _currentDynamicScript.OnConnectionChanged(connectionInfo);

                // 检查是否需要更新UI
                TryUpdateDynamicUI(connectionInfo);
            }
        }

        private void OnUIUpdateRequested(object? sender, EventArgs e)
        {
            // 脚本请求UI更新
            if (_currentDynamicScript != null && SelectedNode != null)
            {
                var connectionInfo = BuildConnectionInfo(SelectedNode);
                TryUpdateDynamicUI(connectionInfo);
            }
        }

        private Services.Scripting.ScriptConnectionInfo BuildConnectionInfo(Node node)
        {
            var connectionInfo = new Services.Scripting.ScriptConnectionInfo
            {
                ChangeType = Services.Scripting.ScriptConnectionChangeType.Initialized
            };

            // 获取节点编辑器ViewModel来访问连接信息
            if (DataContext is ViewModels.NodeEditorViewModel nodeEditor)
            {
                // 检查输入端口连接状态
                foreach (var inputPort in node.InputPorts)
                {
                    var hasConnection = nodeEditor.Connections.Any(c =>
                        c.InputNode?.Id == node.Id && c.InputPortName == inputPort.Name);
                    connectionInfo.InputConnections[inputPort.Name] = hasConnection;
                }

                // 检查输出端口连接状态
                foreach (var outputPort in node.OutputPorts)
                {
                    var hasConnection = nodeEditor.Connections.Any(c =>
                        c.OutputNode?.Id == node.Id && c.OutputPortName == outputPort.Name);
                    connectionInfo.OutputConnections[outputPort.Name] = hasConnection;
                }
            }

            return connectionInfo;
        }

        private void TryUpdateDynamicUI(Services.Scripting.ScriptConnectionInfo connectionInfo)
        {
            if (_currentDynamicScript == null) return;

            // 获取当前UI标识符
            var currentToken = _currentDynamicScript.GetUIUpdateToken();

            // 如果UI标识符没有变化，尝试增量更新
            if (currentToken != null && currentToken == _lastUIToken && _currentScriptControl != null)
            {
                if (_currentDynamicScript.TryUpdateUI(_currentScriptControl, connectionInfo))
                {
                    // 增量更新成功，无需重建
                    return;
                }
            }

            // 需要重建UI
            _lastUIToken = currentToken;
            UpdateParameterEditor();
        }

        // 防止递归调用导致栈溢出
        private bool _isUpdating = false;

        private void UpdateParameterEditor()
        {
            if (_isUpdating) return;
            _isUpdating = true;

            ParameterContainer.Children.Clear();

            if (SelectedNode == null)
            {
                ParameterContainer.Children.Add(NoSelectionText);
                _isUpdating = false;
                return;
            }

            // 添加节点信息
            var nodeInfoPanel = CreateNodeInfoPanel();
            ParameterContainer.Children.Add(nodeInfoPanel);

            // 检查是否是TunnelExtension Script节点

            // 检查是否需要重建参数
            if (SelectedNode.Tag is Services.Scripting.ITunnelExtensionScript scriptInstance && SelectedNode.Parameters.Count == 0)
            {
                RebuildNodeParameters(SelectedNode, scriptInstance);
            }

            if (TryCreateTunnelExtensionScriptControl(SelectedNode, out var TunnelExtensionScriptControl))
            {
                // 保存当前脚本控件引用，用于动态更新
                _currentScriptControl = TunnelExtensionScriptControl;
                ParameterContainer.Children.Add(TunnelExtensionScriptControl);

                // ---------- 预览接管（参数窗口触发） ----------
                try
                {
                    object? scriptObj = null;
                    if (SelectedNode.Tag is Services.Scripting.ITunnelExtensionScript rs)
                        scriptObj = rs;
                    else if (SelectedNode.ViewModel is Services.Scripting.IScriptViewModel vm)
                        scriptObj = vm.Script;

                    if (scriptObj is Services.Scripting.IScriptPreviewProvider previewProvider)
                    {
                        if (previewProvider.WantsPreview(Tunnel_Next.Services.UI.PreviewTrigger.ParameterWindow))
                        {
                            var ctx = new Services.Scripting.ScriptContext("", "", "", () => new Tunnel_Next.Models.NodeGraph(), _ => { }, _ => new System.Collections.Generic.Dictionary<string, object>(), (a,b,c)=>{});
                            var ctrl = previewProvider.CreatePreviewControl(Tunnel_Next.Services.UI.PreviewTrigger.ParameterWindow, ctx);
                            if (ctrl != null)
                            {
                                Tunnel_Next.Services.UI.PreviewManager.Instance.RequestTakeover(previewProvider, ctrl, Tunnel_Next.Services.UI.PreviewTrigger.ParameterWindow);
                            }
                        }
                    }
                }
                catch { }

                _isUpdating = false;
                return;
            }

            // 使用传统参数编辑器
            foreach (var parameter in SelectedNode.Parameters)
            {
                var parameterControl = CreateParameterControl(parameter);
                if (parameterControl != null)
                {
                    ParameterContainer.Children.Add(parameterControl);
                }
            }

            // 如果没有参数，显示提示
            if (!SelectedNode.Parameters.Any())
            {
                var noParamsText = new TextBlock
                {
                    Text = "此节点没有可编辑的参数",
                    Foreground = System.Windows.Media.Brushes.Gray,
                    FontStyle = FontStyles.Italic,
                    Margin = new Thickness(0, 10, 0, 0)
                };
                ParameterContainer.Children.Add(noParamsText);
            }
            else
            {
            }

            _isUpdating = false;
        }

        private StackPanel CreateNodeInfoPanel()
        {
            // 创建主面板
            var panel = new StackPanel
            {
                Margin = new Thickness(0, 0, 0, 15)
            };
            
            // 使用全局定义的基于设计文件的样式
            var headerBorder = new Border
            {
                Style = (Style)Application.Current.Resources["AeroParameterPanelHeaderStyle"]
            };
            
            // 创建内部内容面板 - 使用StackPanel更简单地排列
            var contentPanel = new StackPanel();
            
            // 添加标题文本
            var titleTextBlock = new TextBlock
            {
                Text = SelectedNode!.Title,
                Style = (Style)Application.Current.Resources["AeroParameterPanelHeaderTextStyle"]
            };
            contentPanel.Children.Add(titleTextBlock);

            // 添加描述文本
            if (!string.IsNullOrEmpty(SelectedNode.Description))
            {
                var descriptionTextBlock = new TextBlock
                {
                    Text = SelectedNode.Description,
                    Style = (Style)Application.Current.Resources["AeroParameterPanelDescriptionTextStyle"]
                };
                contentPanel.Children.Add(descriptionTextBlock);
            }
            
            // 将内容面板添加到标题栏边框
            headerBorder.Child = contentPanel;
            
            // 将标题栏边框添加到主面板
            panel.Children.Add(headerBorder);

            return panel;
        }

        private FrameworkElement? CreateParameterControl(NodeParameter parameter)
        {
            var container = new StackPanel();

            // 参数标签
            var label = new TextBlock
            {
                Text = parameter.DisplayName,
                Style = (Style)FindResource("ParameterLabelStyle")
            };
            container.Children.Add(label);

            // 优先使用原始参数类型来创建控件
            FrameworkElement? control = null;

            if (parameter.OriginalParameterType.HasValue)
            {
                // 根据原始脚本参数类型创建控件
                control = parameter.OriginalParameterType.Value switch
                {
                    Services.Scripting.ParameterType.Slider => CreateSliderParameterControl(parameter),
                    Services.Scripting.ParameterType.Checkbox => CreateBooleanParameterControl(parameter),
                    Services.Scripting.ParameterType.Number => CreateNumericParameterControl(parameter),
                    Services.Scripting.ParameterType.FilePath => CreateFilePathControl(parameter),
                    Services.Scripting.ParameterType.Text => CreateStringParameterControl(parameter),
                    _ => CreateStringParameterControl(parameter) // 默认使用字符串控件
                };
            }
            else
            {
                // 回退到基于.NET类型的判断
                control = parameter.ParameterType.Name switch
                {
                    nameof(String) => CreateStringParameterControl(parameter),
                    nameof(Double) or nameof(Single) or nameof(Int32) => CreateNumericParameterControl(parameter),
                    nameof(Boolean) => CreateBooleanParameterControl(parameter),
                    _ => CreateStringParameterControl(parameter) // 默认使用字符串控件
                };
            }

            if (control != null)
            {
                container.Children.Add(control);

                // 添加描述文本
                if (!string.IsNullOrEmpty(parameter.Description))
                {
                    var description = new TextBlock
                    {
                        Text = parameter.Description,
                        FontSize = 10,
                        Foreground = System.Windows.Media.Brushes.Gray,
                        Margin = new Thickness(0, 2, 0, 0),
                        TextWrapping = TextWrapping.Wrap
                    };
                    container.Children.Add(description);
                }
            }

            return container;
        }

        private FrameworkElement CreateStringParameterControl(NodeParameter parameter)
        {
            if (parameter.Name == "FilePath")
            {
                // 文件路径参数使用特殊的文件选择控件
                return CreateFilePathControl(parameter);
            }

            var textBox = new TextBox
            {
                Style = (Style)FindResource("ParameterTextBoxStyle"),
                IsReadOnly = parameter.IsReadOnly
            };

            // 绑定数据 - 使用LostFocus触发，避免输入时频繁更新
            var binding = new Binding("Value")
            {
                Source = parameter,
                Mode = parameter.IsReadOnly ? BindingMode.OneWay : BindingMode.TwoWay,
                UpdateSourceTrigger = UpdateSourceTrigger.LostFocus
            };
            textBox.SetBinding(TextBox.TextProperty, binding);

            return textBox;
        }

        private FrameworkElement CreateFilePathControl(NodeParameter parameter)
        {
            var panel = new DockPanel();

            var button = new Button
            {
                Content = "浏览...",
                Style = (Style)FindResource("ParameterButtonStyle"),
                Width = 60
            };
            DockPanel.SetDock(button, Dock.Right);

            var textBox = new TextBox
            {
                Style = (Style)FindResource("ParameterTextBoxStyle"),
                IsReadOnly = true,
                Margin = new Thickness(0, 0, 5, 0)
            };

            // 绑定文件路径
            var binding = new Binding("Value")
            {
                Source = parameter,
                Mode = BindingMode.TwoWay
            };
            textBox.SetBinding(TextBox.TextProperty, binding);

            // 浏览按钮事件
            button.Click += (s, e) =>
            {
                var openDialog = new OpenFileDialog
                {
                    Filter = "图像文件 (*.jpg;*.jpeg;*.png;*.bmp;*.tiff)|*.jpg;*.jpeg;*.png;*.bmp;*.tiff|所有文件 (*.*)|*.*",
                    Title = "选择图像文件"
                };

                if (openDialog.ShowDialog() == true)
                {
                    parameter.Value = openDialog.FileName;
                }
            };

            panel.Children.Add(textBox);
            panel.Children.Add(button);

            return panel;
        }

        private FrameworkElement CreateNumericParameterControl(NodeParameter parameter)
        {
            var panel = new StackPanel();

            // 如果有范围限制，使用滑块
            if (parameter.MinValue.HasValue && parameter.MaxValue.HasValue)
            {
                var slider = new Slider
                {
                    Style = (Style)FindResource("ParameterSliderStyle"),
                    Minimum = parameter.MinValue.Value,
                    Maximum = parameter.MaxValue.Value,
                    SmallChange = parameter.Step ?? 1.0,
                    LargeChange = (parameter.MaxValue.Value - parameter.MinValue.Value) / 10.0,
                    IsEnabled = !parameter.IsReadOnly,
                    IsMoveToPointEnabled = true // 允许点击跳转
                };

                // 绑定数据 - 滑块使用实时更新
                var binding = new Binding("Value")
                {
                    Source = parameter,
                    Mode = parameter.IsReadOnly ? BindingMode.OneWay : BindingMode.TwoWay,
                    UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
                };
                slider.SetBinding(Slider.ValueProperty, binding);

                panel.Children.Add(slider);

                // 添加数值显示
                var valueText = new TextBlock
                {
                    FontSize = 10,
                    Foreground = System.Windows.Media.Brushes.Gray,
                    HorizontalAlignment = HorizontalAlignment.Center
                };

                var valueBinding = new Binding("Value")
                {
                    Source = parameter,
                    StringFormat = "F2"
                };
                valueText.SetBinding(TextBlock.TextProperty, valueBinding);

                panel.Children.Add(valueText);
            }
            else
            {
                // 使用文本框
                var textBox = new TextBox
                {
                    Style = (Style)FindResource("ParameterTextBoxStyle"),
                    IsReadOnly = parameter.IsReadOnly
                };

                var binding = new Binding("Value")
                {
                    Source = parameter,
                    Mode = parameter.IsReadOnly ? BindingMode.OneWay : BindingMode.TwoWay,
                    UpdateSourceTrigger = UpdateSourceTrigger.LostFocus
                };
                textBox.SetBinding(TextBox.TextProperty, binding);

                panel.Children.Add(textBox);
            }

            return panel;
        }

        private FrameworkElement CreateBooleanParameterControl(NodeParameter parameter)
        {
            var checkBox = new CheckBox
            {
                Style = (Style)FindResource("ParameterCheckBoxStyle"),
                Content = parameter.DisplayName,
                IsEnabled = !parameter.IsReadOnly
            };

            // 绑定数据
            var binding = new Binding("Value")
            {
                Source = parameter,
                Mode = parameter.IsReadOnly ? BindingMode.OneWay : BindingMode.TwoWay
            };
            checkBox.SetBinding(CheckBox.IsCheckedProperty, binding);

            return checkBox;
        }

        /// <summary>
        /// 创建专门的滑块参数控件
        /// </summary>
        private FrameworkElement CreateSliderParameterControl(NodeParameter parameter)
        {
            var panel = new StackPanel();

            // 创建滑块
            var slider = new Slider
            {
                Style = (Style)FindResource("ParameterSliderStyle"),
                Minimum = parameter.MinValue ?? 0.0,
                Maximum = parameter.MaxValue ?? 100.0,
                SmallChange = parameter.Step ?? 1.0,
                LargeChange = (parameter.MaxValue ?? 100.0 - parameter.MinValue ?? 0.0) / 10.0,
                IsEnabled = !parameter.IsReadOnly,
                IsMoveToPointEnabled = true // 允许点击跳转
            };

            // 绑定数据 - 滑块使用实时更新
            var sliderBinding = new Binding("Value")
            {
                Source = parameter,
                Mode = parameter.IsReadOnly ? BindingMode.OneWay : BindingMode.TwoWay,
                UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
            };
            slider.SetBinding(Slider.ValueProperty, sliderBinding);

            panel.Children.Add(slider);

            // 添加数值显示文本框
            var valueTextBox = new TextBox
            {
                Style = (Style)FindResource("ParameterTextBoxStyle"),
                IsReadOnly = parameter.IsReadOnly,
                Width = 80,
                HorizontalAlignment = HorizontalAlignment.Left,
                Margin = new Thickness(0, 5, 0, 0)
            };

            var textBinding = new Binding("Value")
            {
                Source = parameter,
                Mode = parameter.IsReadOnly ? BindingMode.OneWay : BindingMode.TwoWay,
                UpdateSourceTrigger = UpdateSourceTrigger.LostFocus,
                StringFormat = "F2" // 保留两位小数
            };
            valueTextBox.SetBinding(TextBox.TextProperty, textBinding);

            panel.Children.Add(valueTextBox);

            return panel;
        }

        /// <summary>
        /// 尝试为TunnelExtension Script节点创建控件
        /// </summary>
        private bool TryCreateTunnelExtensionScriptControl(Node node, out FrameworkElement? control)
        {
            control = null;

            try
            {

                // 检查节点是否有TunnelExtension Script实例
                if (node.Tag is Services.Scripting.ITunnelExtensionScript TunnelExtensionScript)
                {

                    try
                    {
                        // 使用脚本提供的WPF控件
                        control = TunnelExtensionScript.CreateParameterControl();

                        if (control != null)
                        {
                            // 设置控件的基本样式
                            control.Margin = new Thickness(0, 10, 0, 0);
                            return true;
                        }
                        else
                        {
                        }
                    }
                    catch (Exception ex)
                    {
                    }
                }

                // 检查节点是否有TunnelExtension Script ViewModel
                if (node.ViewModel is Services.Scripting.IScriptViewModel viewModel)
                {

                    try
                    {
                        // 使用ViewModel的脚本创建控件
                        control = viewModel.Script.CreateParameterControl();

                        if (control != null)
                        {
                            // 绑定DataContext到ViewModel
                            control.DataContext = viewModel;
                            control.Margin = new Thickness(0, 10, 0, 0);
                            return true;
                        }
                        else
                        {
                        }
                    }
                    catch (Exception ex)
                    {
                    }
                }

                // 检查节点Tag是否是ViewModel类型（兼容性检查）
                if (node.Tag is Services.Scripting.IScriptViewModel tagViewModel)
                {

                    try
                    {
                        // 使用ViewModel的脚本创建控件
                        control = tagViewModel.Script.CreateParameterControl();

                        if (control != null)
                        {
                            // 绑定DataContext到ViewModel
                            control.DataContext = tagViewModel;
                            control.Margin = new Thickness(0, 10, 0, 0);
                            return true;
                        }
                        else
                        {
                        }
                    }
                    catch (Exception ex)
                    {
                    }
                }

            }
            catch (Exception ex)
            {
            }

            return false;
        }

        /// <summary>
        /// 重建节点参数
        /// </summary>
        private void RebuildNodeParameters(Node node, Services.Scripting.ITunnelExtensionScript scriptInstance)
        {
            try
            {
                // 避免多次重建导致递归
                if (node.UserData.TryGetValue("ParametersRebuilt", out var rebuilt) && rebuilt is bool b && b)
                    return;

                // 清除现有参数
                node.Parameters.Clear();

                // 从脚本实例获取当前参数值
                var scriptParameters = scriptInstance.SerializeParameters();

                // 重建参数到节点
                foreach (var kvp in scriptParameters)
                {
                    var parameter = new Models.NodeParameter
                    {
                        Name = kvp.Key,
                        Value = kvp.Value,
                        Type = kvp.Value?.GetType().Name ?? "object"
                    };
                    node.Parameters.Add(parameter);
                }

                node.UserData["ParametersRebuilt"] = true;

                // 重新更新参数编辑器
                UpdateParameterEditor();
            }
            catch (Exception ex)
            {
            }
        }
    }
}
