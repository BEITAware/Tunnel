using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using Tunnel_Next.UtilityTools.BatchProcessor.Models;

namespace Tunnel_Next.UtilityTools.BatchProcessor.Controls
{
    /// <summary>
    /// 积木块设定面板控件
    /// </summary>
    public partial class CodeBlockSettingsControl : UserControl
    {
        public static readonly DependencyProperty SelectedCodeBlockProperty =
            DependencyProperty.Register(nameof(SelectedCodeBlock), typeof(CodeBlockBase), typeof(CodeBlockSettingsControl),
                new PropertyMetadata(null, OnSelectedCodeBlockChanged));

        private bool _isUpdating;

        public CodeBlockSettingsControl()
        {
            InitializeComponent();
            DataContextChanged += OnDataContextChanged;
        }

        /// <summary>
        /// 选中的积木块
        /// </summary>
        public CodeBlockBase? SelectedCodeBlock
        {
            get => (CodeBlockBase?)GetValue(SelectedCodeBlockProperty);
            set => SetValue(SelectedCodeBlockProperty, value);
        }

        /// <summary>
        /// 数据上下文变化处理
        /// </summary>
        private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (DataContext is ViewModels.BatchProcessEditorViewModel batchEditor)
            {
                // 绑定到BatchEditor的SelectedBlock属性
                var binding = new Binding("SelectedBlock")
                {
                    Source = batchEditor,
                    Mode = BindingMode.OneWay
                };
                SetBinding(SelectedCodeBlockProperty, binding);
            }
            else
            {
                // 清除绑定
                BindingOperations.ClearBinding(this, SelectedCodeBlockProperty);
                SelectedCodeBlock = null;
            }
        }

        /// <summary>
        /// 选中积木块变化的静态回调
        /// </summary>
        private static void OnSelectedCodeBlockChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is CodeBlockSettingsControl control)
            {
                control.OnSelectedCodeBlockChangedInternal(e.OldValue as CodeBlockBase, e.NewValue as CodeBlockBase);
            }
        }

        /// <summary>
        /// 选中积木块变化的内部处理
        /// </summary>
        private void OnSelectedCodeBlockChangedInternal(CodeBlockBase? oldBlock, CodeBlockBase? newBlock)
        {
            // 取消订阅旧积木块的事件
            if (oldBlock != null)
            {
                oldBlock.SettingsChanged -= OnCodeBlockSettingsChanged;
                oldBlock.PropertyChanged -= OnCodeBlockPropertyChanged;
            }

            // 订阅新积木块的事件
            if (newBlock != null)
            {
                newBlock.SettingsChanged += OnCodeBlockSettingsChanged;
                newBlock.PropertyChanged += OnCodeBlockPropertyChanged;
            }

            UpdateSettingsPanel();
        }

        /// <summary>
        /// 更新设定面板
        /// </summary>
        private void UpdateSettingsPanel()
        {
            if (_isUpdating) return;
            _isUpdating = true;

            try
            {
                SettingsContainer.Children.Clear();

                if (SelectedCodeBlock == null)
                {
                    // 显示提示信息，类似主程序的参数编辑器
                    SettingsContainer.Children.Add(NoSelectionText);
                    return;
                }

                // 添加积木块信息面板
                var infoPanel = CreateCodeBlockInfoPanel();
                SettingsContainer.Children.Add(infoPanel);

                // 添加积木块的设定控件
                try
                {
                    var settingsControl = SelectedCodeBlock.CreateSettingsControl();
                    if (settingsControl != null)
                    {
                        settingsControl.Margin = new Thickness(0, 10, 0, 0);
                        SettingsContainer.Children.Add(settingsControl);
                    }
                    else
                    {
                        var noSettingsText = new TextBlock
                        {
                            Text = "此积木块没有可编辑的设定",
                            Foreground = System.Windows.Media.Brushes.Gray,
                            FontStyle = FontStyles.Italic,
                            Margin = new Thickness(0, 10, 0, 0)
                        };
                        SettingsContainer.Children.Add(noSettingsText);
                    }
                }
                catch (Exception ex)
                {
                    var errorText = new TextBlock
                    {
                        Text = $"创建设定面板时发生错误: {ex.Message}",
                        Foreground = System.Windows.Media.Brushes.Red,
                        FontStyle = FontStyles.Italic,
                        Margin = new Thickness(0, 10, 0, 0),
                        TextWrapping = TextWrapping.Wrap
                    };
                    SettingsContainer.Children.Add(errorText);
                }
            }
            finally
            {
                _isUpdating = false;
            }
        }

        /// <summary>
        /// 创建积木块信息面板
        /// </summary>
        private FrameworkElement CreateCodeBlockInfoPanel()
        {
            var panel = new StackPanel
            {
                Margin = new Thickness(0, 0, 0, 10)
            };

            // 标题
            var titleText = new TextBlock
            {
                Text = "积木块信息",
                FontWeight = FontWeights.Bold,
                FontSize = 14,
                Margin = new Thickness(0, 0, 0, 5)
            };
            panel.Children.Add(titleText);

            // 分隔线
            var separator = new Border
            {
                Height = 1,
                Background = System.Windows.Media.Brushes.Gray,
                Opacity = 0.3,
                Margin = new Thickness(0, 0, 0, 5)
            };
            panel.Children.Add(separator);

            // 积木块类型
            var typeText = new TextBlock
            {
                Text = $"类型: {SelectedCodeBlock?.BlockType}",
                Margin = new Thickness(0, 2, 0, 2)
            };
            panel.Children.Add(typeText);

            // 积木块名称
            var nameText = new TextBlock
            {
                Text = $"名称: {SelectedCodeBlock?.DisplayName}",
                Margin = new Thickness(0, 2, 0, 2)
            };
            panel.Children.Add(nameText);

            // 积木块描述
            if (!string.IsNullOrEmpty(SelectedCodeBlock?.Description))
            {
                var descText = new TextBlock
                {
                    Text = $"描述: {SelectedCodeBlock.Description}",
                    Margin = new Thickness(0, 2, 0, 2),
                    TextWrapping = TextWrapping.Wrap
                };
                panel.Children.Add(descText);
            }

            // 设定摘要
            try
            {
                var summary = SelectedCodeBlock?.GetSettingsSummary();
                if (!string.IsNullOrEmpty(summary))
                {
                    var summaryText = new TextBlock
                    {
                        Text = $"设定摘要: {summary}",
                        Margin = new Thickness(0, 2, 0, 2),
                        TextWrapping = TextWrapping.Wrap,
                        FontStyle = FontStyles.Italic
                    };
                    panel.Children.Add(summaryText);
                }
            }
            catch (Exception ex)
            {
                var errorText = new TextBlock
                {
                    Text = $"获取设定摘要时发生错误: {ex.Message}",
                    Margin = new Thickness(0, 2, 0, 2),
                    TextWrapping = TextWrapping.Wrap,
                    Foreground = System.Windows.Media.Brushes.Red,
                    FontSize = 10
                };
                panel.Children.Add(errorText);
            }



            return panel;
        }

        /// <summary>
        /// 积木块设定变化事件处理
        /// </summary>
        private void OnCodeBlockSettingsChanged(object? sender, SettingsChangedEventArgs e)
        {
            // 当积木块设定变化时，更新设定面板
            Dispatcher.BeginInvoke(() => UpdateSettingsPanel());
        }

        /// <summary>
        /// 积木块属性变化事件处理
        /// </summary>
        private void OnCodeBlockPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            // 当积木块基础属性变化时，更新信息面板
            if (e.PropertyName == nameof(CodeBlockBase.DisplayName) || 
                e.PropertyName == nameof(CodeBlockBase.Description))
            {
                Dispatcher.BeginInvoke(() => UpdateSettingsPanel());
            }
        }
    }
}
