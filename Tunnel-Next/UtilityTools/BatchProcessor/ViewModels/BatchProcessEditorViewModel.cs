using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using Tunnel_Next.Models;
using Tunnel_Next.ViewModels;
using Tunnel_Next.UtilityTools.BatchProcessor.Models;
using Tunnel_Next.UtilityTools.BatchProcessor.Services;

namespace Tunnel_Next.UtilityTools.BatchProcessor.ViewModels
{
    /// <summary>
    /// 批处理编辑器窗口的视图模型
    /// </summary>
    public class BatchProcessEditorViewModel : INotifyPropertyChanged
    {
        #region 私有字段

        private readonly IEnumerable<BatchProcessNodeGraphItem> _selectedNodeGraphs;
        private int _selectedNodeGraphsCount;

        #endregion

        #region 公共属性

        /// <summary>
        /// 选中的节点图数量
        /// </summary>
        public int SelectedNodeGraphsCount
        {
            get => _selectedNodeGraphsCount;
            set
            {
                if (_selectedNodeGraphsCount != value)
                {
                    _selectedNodeGraphsCount = value;
                    OnPropertyChanged(nameof(SelectedNodeGraphsCount));
                }
            }
        }

        /// <summary>
        /// 可用的积木块列表（新架构）
        /// </summary>
        public ObservableCollection<CodeBlockBase> AvailableBlocks { get; } = new();

        /// <summary>
        /// 编辑器中的积木块（新架构）
        /// </summary>
        public ObservableCollection<CodeBlockBase> EditorBlocks { get; } = new();

        /// <summary>
        /// 当前选中的积木块
        /// </summary>
        public CodeBlockBase? SelectedBlock { get; set; }

        #endregion

        #region 命令

        /// <summary>
        /// 返回上一步命令
        /// </summary>
        public ICommand BackCommand { get; }

        /// <summary>
        /// 取消命令
        /// </summary>
        public ICommand CancelCommand { get; }

        /// <summary>
        /// 开始处理命令
        /// </summary>
        public ICommand StartProcessingCommand { get; }

        /// <summary>
        /// 添加代码块命令（将来实现）
        /// </summary>
        public ICommand AddCodeBlockCommand { get; }

        /// <summary>
        /// 删除代码块命令
        /// </summary>
        public ICommand RemoveCodeBlockCommand { get; }

        /// <summary>
        /// 选择积木块命令
        /// </summary>
        public ICommand SelectBlockCommand { get; }

        #endregion

        #region 构造函数

        public BatchProcessEditorViewModel(IEnumerable<BatchProcessNodeGraphItem> selectedNodeGraphs)
        {
            _selectedNodeGraphs = selectedNodeGraphs ?? throw new ArgumentNullException(nameof(selectedNodeGraphs));

            // 旧的拖拽服务已移除

            // 初始化命令
            BackCommand = new RelayCommand(ExecuteBack);
            CancelCommand = new RelayCommand(ExecuteCancel);
            StartProcessingCommand = new RelayCommand(ExecuteStartProcessing, CanExecuteStartProcessing);
            AddCodeBlockCommand = new RelayCommand<string>(ExecuteAddCodeBlock);
            RemoveCodeBlockCommand = new RelayCommand<object>(ExecuteRemoveCodeBlock);
            SelectBlockCommand = new RelayCommand<CodeBlockBase>(ExecuteSelectBlock);
        }

        #endregion

        #region 公共方法

        /// <summary>
        /// 初始化
        /// </summary>
        public void Initialize()
        {
            // 设置选中的节点图数量
            SelectedNodeGraphsCount = _selectedNodeGraphs.Count();

            // 初始化新的积木块系统
            InitializeAvailableBlocks();
        }

        #endregion

        #region 私有方法

        /// <summary>
        /// 初始化可用积木块列表
        /// </summary>
        private void InitializeAvailableBlocks()
        {
            // 添加可用的积木块类型（使用新架构）
            AvailableBlocks.Add(new NodeGraphSequenceBlock());
            AvailableBlocks.Add(new FileSequenceBlock());

            // 将来添加更多积木块类型
        }

        /// <summary>
        /// 执行返回上一步命令
        /// </summary>
        private void ExecuteBack()
        {
            BackRequested?.Invoke();
        }

        /// <summary>
        /// 执行取消命令
        /// </summary>
        private void ExecuteCancel()
        {
            CancelRequested?.Invoke();
        }

        /// <summary>
        /// 执行开始处理命令
        /// </summary>
        private void ExecuteStartProcessing()
        {
            ProcessingStarted?.Invoke();
        }

        /// <summary>
        /// 判断是否可以执行开始处理命令
        /// </summary>
        private bool CanExecuteStartProcessing()
        {
            // 将来在这里检查工作流是否有效
            // 目前总是返回true
            return true;
        }

        /// <summary>
        /// 执行添加代码块命令
        /// </summary>
        private void ExecuteAddCodeBlock(string? blockType)
        {
            if (string.IsNullOrEmpty(blockType))
                return;

            // 将来在这里实现代码块添加逻辑
        }

        /// <summary>
        /// 执行删除积木块命令
        /// </summary>
        private void ExecuteRemoveCodeBlock(object? codeBlock)
        {
            if (codeBlock is CodeBlockBase block)
            {
                EditorBlocks.Remove(block);
                if (SelectedBlock == block)
                {
                    SelectedBlock = null;
                    OnPropertyChanged(nameof(SelectedBlock));
                }
            }
        }

        /// <summary>
        /// 执行选择积木块命令
        /// </summary>
        private void ExecuteSelectBlock(CodeBlockBase? block)
        {
            // 清除所有积木块的选中状态
            foreach (var availableBlock in AvailableBlocks)
            {
                availableBlock.IsSelected = false;
            }
            foreach (var editorBlock in EditorBlocks)
            {
                editorBlock.IsSelected = false;
            }

            // 设置新选中的积木块
            if (block != null)
            {
                block.IsSelected = true;
            }

            SelectedBlock = block;
            OnPropertyChanged(nameof(SelectedBlock));
        }

        // 旧的拖拽事件处理方法已移除，将来实现新的积木块交互逻辑

        #endregion

        #region 事件

        public event PropertyChangedEventHandler? PropertyChanged;
        public event Action? BackRequested;
        public event Action? CancelRequested;
        public event Action? ProcessingStarted;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion
    }
}
