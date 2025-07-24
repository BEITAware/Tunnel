using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Input;
using Tunnel_Next.Models;
using Tunnel_Next.Services.Scripting;
using Tunnel_Next.UtilityTools.BatchProcessor.ViewModels;

namespace Tunnel_Next.UtilityTools.BatchProcessor.Views
{
    /// <summary>
    /// 批量处理器窗口
    /// </summary>
    public partial class BatchProcessWindow : Window
    {
        private readonly BatchProcessViewModel _viewModel;

        public BatchProcessWindow(RevivalScriptManager? revivalScriptManager)
        {
            InitializeComponent();
            
            // 创建并设置视图模型
            _viewModel = new BatchProcessViewModel(revivalScriptManager);
            DataContext = _viewModel;
            
            // 订阅视图模型事件
            _viewModel.CancelRequested += OnCancelRequested;
            _viewModel.ContinueRequested += OnContinueRequested;
            
            // 注册窗口加载事件
            Loaded += BatchProcessWindow_Loaded;
        }

        /// <summary>
        /// 窗口加载事件
        /// </summary>
        private async void BatchProcessWindow_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                await _viewModel.InitializeAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"初始化批量处理器时发生错误: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
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
        /// 处理继续请求
        /// </summary>
        private void OnContinueRequested(IEnumerable<BatchProcessNodeGraphItem> selectedItems)
        {
            try
            {
                // 创建并打开批处理编辑器窗口
                var editorWindow = new BatchProcessEditorWindow(selectedItems);
                editorWindow.Owner = this;

                // 显示编辑器窗口
                var result = editorWindow.ShowDialog();

                // 如果编辑器窗口成功完成，关闭当前窗口
                if (result == true)
                {
                    DialogResult = true;
                    Close();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"打开批处理编辑器时发生错误: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 节点图点击事件处理
        /// </summary>
        private void NodeGraph_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement element && element.Tag is BatchProcessNodeGraphItem item)
            {
                _viewModel.NodeGraphClickCommand?.Execute(item);
                e.Handled = true;
            }
        }

        /// <summary>
        /// 窗口关闭时清理资源
        /// </summary>
        protected override void OnClosed(EventArgs e)
        {
            if (_viewModel != null)
            {
                _viewModel.CancelRequested -= OnCancelRequested;
                _viewModel.ContinueRequested -= OnContinueRequested;
            }
            base.OnClosed(e);
        }
    }
}
