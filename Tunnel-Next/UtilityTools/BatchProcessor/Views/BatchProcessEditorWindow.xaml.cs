using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Input;
using Tunnel_Next.Models;
using Tunnel_Next.UtilityTools.BatchProcessor.Models;
using Tunnel_Next.UtilityTools.BatchProcessor.ViewModels;

namespace Tunnel_Next.UtilityTools.BatchProcessor.Views
{
    /// <summary>
    /// 批处理编辑器窗口
    /// </summary>
    public partial class BatchProcessEditorWindow : Window
    {
        private readonly BatchProcessEditorViewModel _viewModel;

        public BatchProcessEditorWindow(IEnumerable<BatchProcessNodeGraphItem> selectedItems)
        {
            InitializeComponent();
            
            // 创建并设置视图模型
            _viewModel = new BatchProcessEditorViewModel(selectedItems);
            DataContext = _viewModel;
            
            // 订阅视图模型事件
            _viewModel.BackRequested += OnBackRequested;
            _viewModel.CancelRequested += OnCancelRequested;
            _viewModel.ProcessingStarted += OnProcessingStarted;
            
            // 注册窗口加载事件
            Loaded += BatchProcessEditorWindow_Loaded;
        }

        /// <summary>
        /// 窗口加载事件
        /// </summary>
        private void BatchProcessEditorWindow_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                _viewModel.Initialize();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"初始化批处理编辑器时发生错误: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 处理返回请求
        /// </summary>
        private void OnBackRequested()
        {
            DialogResult = false;
            Close();
        }

        /// <summary>
        /// 处理取消请求
        /// </summary>
        private void OnCancelRequested()
        {
            DialogResult = false;
            Close();
        }

        /// <summary>
        /// 处理开始处理请求
        /// </summary>
        private void OnProcessingStarted()
        {
            // 将来在这里实现批处理执行逻辑
            MessageBox.Show("批处理功能即将推出！", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        /// <summary>
        /// 工具箱项目鼠标按下事件
        /// </summary>
        private void ToolboxItem_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement element && element.DataContext is CodeBlock templateBlock)
            {
                // 创建新的代码块实例
                var newBlock = CodeBlockFactory.CreateCodeBlock(templateBlock.Type);

                // 开始拖拽操作
                DragDrop.DoDragDrop(element, newBlock, DragDropEffects.Copy);
                e.Handled = true;
            }
        }

        /// <summary>
        /// 编辑画布拖拽悬停事件
        /// </summary>
        private void EditorCanvas_DragOver(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(typeof(CodeBlock)))
            {
                e.Effects = DragDropEffects.Copy;
            }
            else
            {
                e.Effects = DragDropEffects.None;
            }
            e.Handled = true;
        }

        /// <summary>
        /// 编辑画布放置事件
        /// </summary>
        private void EditorCanvas_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(typeof(CodeBlock)))
            {
                var codeBlock = e.Data.GetData(typeof(CodeBlock)) as CodeBlock;
                if (codeBlock != null)
                {
                    // 获取放置位置
                    var dropPosition = e.GetPosition(EditorCanvas);
                    codeBlock.Position = dropPosition;

                    // 添加到编辑器
                    _viewModel.EditorBlocks.Add(codeBlock);
                }
            }
            e.Handled = true;
        }

        /// <summary>
        /// 窗口关闭时清理资源
        /// </summary>
        protected override void OnClosed(EventArgs e)
        {
            if (_viewModel != null)
            {
                _viewModel.BackRequested -= OnBackRequested;
                _viewModel.CancelRequested -= OnCancelRequested;
                _viewModel.ProcessingStarted -= OnProcessingStarted;
            }
            base.OnClosed(e);
        }
    }
}
