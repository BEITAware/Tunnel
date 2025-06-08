using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using Tunnel_Next.Models;
using Microsoft.Win32;
using System;

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

        public ParameterEditorControl()
        {
            InitializeComponent();

            // 监听DataContext变化，如果DataContext是NodeEditorViewModel，则绑定其SelectedNode
            DataContextChanged += OnDataContextChanged;
        }

        private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {

            if (DataContext is ViewModels.NodeEditorViewModel nodeEditor)
            {

                // 绑定到NodeEditor的SelectedNode属性
                var binding = new System.Windows.Data.Binding("SelectedNode")
                {
                    Source = nodeEditor,
                    Mode = System.Windows.Data.BindingMode.OneWay
                };
                SetBinding(SelectedNodeProperty, binding);


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
                control.UpdateParameterEditor();
            }
        }

        private void UpdateParameterEditor()
        {
            ParameterContainer.Children.Clear();

            if (SelectedNode == null)
            {
                ParameterContainer.Children.Add(NoSelectionText);
                return;
            }

            // 添加节点信息
            var nodeInfoPanel = CreateNodeInfoPanel();
            ParameterContainer.Children.Add(nodeInfoPanel);

            // 检查是否是Revival Script节点

            // 检查是否需要重建参数
            if (SelectedNode.Tag is Services.Scripting.IRevivalScript scriptInstance && SelectedNode.Parameters.Count == 0)
            {
                RebuildNodeParameters(SelectedNode, scriptInstance);
            }

            if (TryCreateRevivalScriptControl(SelectedNode, out var revivalScriptControl))
            {
                ParameterContainer.Children.Add(revivalScriptControl);
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
        }

        private StackPanel CreateNodeInfoPanel()
        {
            var panel = new StackPanel
            {
                Margin = new Thickness(0, 0, 0, 15),
                Background = System.Windows.Media.Brushes.LightBlue
            };

            panel.Children.Add(new TextBlock
            {
                Text = SelectedNode!.Title,
                FontWeight = FontWeights.Bold,
                FontSize = 14,
                Margin = new Thickness(8, 6, 8, 2)
            });

            panel.Children.Add(new TextBlock
            {
                Text = SelectedNode.Description,
                FontSize = 11,
                Foreground = System.Windows.Media.Brushes.DarkBlue,
                Margin = new Thickness(8, 0, 8, 6),
                TextWrapping = TextWrapping.Wrap
            });

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
        /// 尝试为Revival Script节点创建控件
        /// </summary>
        private bool TryCreateRevivalScriptControl(Node node, out FrameworkElement? control)
        {
            control = null;

            try
            {

                // 检查节点是否有Revival Script实例
                if (node.Tag is Services.Scripting.IRevivalScript revivalScript)
                {

                    try
                    {
                        // 使用脚本提供的WPF控件
                        control = revivalScript.CreateParameterControl();

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

                // 检查节点是否有Revival Script ViewModel
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
        private void RebuildNodeParameters(Node node, Services.Scripting.IRevivalScript scriptInstance)
        {
            try
            {

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


                // 重新更新参数编辑器
                UpdateParameterEditor();
            }
            catch (Exception ex)
            {
            }
        }
    }
}
