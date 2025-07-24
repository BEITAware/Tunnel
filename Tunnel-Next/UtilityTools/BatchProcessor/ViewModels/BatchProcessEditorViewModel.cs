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
        private readonly DragDropService _dragDropService;

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
        /// 工具箱中的代码块模板
        /// </summary>
        public ObservableCollection<CodeBlock> ToolboxBlocks { get; } = new();

        /// <summary>
        /// 编辑器中的代码块
        /// </summary>
        public ObservableCollection<CodeBlock> EditorBlocks { get; } = new();

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
        /// 从工具箱创建代码块命令
        /// </summary>
        public ICommand CreateFromToolboxCommand { get; }

        #endregion

        #region 构造函数

        public BatchProcessEditorViewModel(IEnumerable<BatchProcessNodeGraphItem> selectedNodeGraphs)
        {
            _selectedNodeGraphs = selectedNodeGraphs ?? throw new ArgumentNullException(nameof(selectedNodeGraphs));

            // 初始化拖拽服务
            _dragDropService = new DragDropService();
            _dragDropService.DragStarted += OnDragStarted;
            _dragDropService.DragCompleted += OnDragCompleted;
            _dragDropService.DragCancelled += OnDragCancelled;

            // 初始化命令
            BackCommand = new RelayCommand(ExecuteBack);
            CancelCommand = new RelayCommand(ExecuteCancel);
            StartProcessingCommand = new RelayCommand(ExecuteStartProcessing, CanExecuteStartProcessing);
            AddCodeBlockCommand = new RelayCommand<string>(ExecuteAddCodeBlock);
            RemoveCodeBlockCommand = new RelayCommand<object>(ExecuteRemoveCodeBlock);
            CreateFromToolboxCommand = new RelayCommand<CodeBlock>(ExecuteCreateFromToolbox);
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

            // 将来在这里初始化代码块工具箱和编辑器
            InitializeCodeBlockToolbox();
            InitializeWorkflowEditor();
        }

        #endregion

        #region 私有方法

        /// <summary>
        /// 初始化代码块工具箱
        /// </summary>
        private void InitializeCodeBlockToolbox()
        {
            // 添加可用的代码块类型
            ToolboxBlocks.Add(CodeBlockFactory.CreateCodeBlock(CodeBlockType.NodeGraphSequence));
            ToolboxBlocks.Add(CodeBlockFactory.CreateCodeBlock(CodeBlockType.FileSequence));
            ToolboxBlocks.Add(CodeBlockFactory.CreateCodeBlock(CodeBlockType.NumberSequence));
            ToolboxBlocks.Add(CodeBlockFactory.CreateCodeBlock(CodeBlockType.NodeScript));
            ToolboxBlocks.Add(CodeBlockFactory.CreateCodeBlock(CodeBlockType.ProcessBlock));
            ToolboxBlocks.Add(CodeBlockFactory.CreateCodeBlock(CodeBlockType.LoopBlock));
            ToolboxBlocks.Add(CodeBlockFactory.CreateCodeBlock(CodeBlockType.CollectionLiteral));
            ToolboxBlocks.Add(CodeBlockFactory.CreateCodeBlock(CodeBlockType.MultiFunctionLiteral));
        }

        /// <summary>
        /// 初始化工作流编辑器
        /// </summary>
        private void InitializeWorkflowEditor()
        {
            // 将来在这里初始化拖拽编辑器
            // 设置画布、网格、连接线等
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
        /// 执行删除代码块命令
        /// </summary>
        private void ExecuteRemoveCodeBlock(object? codeBlock)
        {
            if (codeBlock is CodeBlock block)
            {
                EditorBlocks.Remove(block);
            }
        }

        /// <summary>
        /// 执行从工具箱创建代码块命令
        /// </summary>
        private void ExecuteCreateFromToolbox(CodeBlock? templateBlock)
        {
            if (templateBlock == null)
                return;

            // 创建新的代码块实例
            var newBlock = CodeBlockFactory.CreateCodeBlock(templateBlock.Type);
            newBlock.Position = new Point(100, 100); // 默认位置

            EditorBlocks.Add(newBlock);
        }

        /// <summary>
        /// 拖拽开始事件处理
        /// </summary>
        private void OnDragStarted(CodeBlock codeBlock)
        {
            // 可以在这里添加拖拽开始时的逻辑
        }

        /// <summary>
        /// 拖拽完成事件处理
        /// </summary>
        private void OnDragCompleted(CodeBlock codeBlock, Point dropPoint)
        {
            // 更新代码块位置
            codeBlock.Position = dropPoint;

            // 如果是从工具箱拖拽的新代码块，添加到编辑器
            if (!EditorBlocks.Contains(codeBlock))
            {
                EditorBlocks.Add(codeBlock);
            }
        }

        /// <summary>
        /// 拖拽取消事件处理
        /// </summary>
        private void OnDragCancelled(CodeBlock codeBlock)
        {
            // 如果是从工具箱拖拽的新代码块，不添加到编辑器
            // 如果是编辑器中的代码块，恢复原位置
        }

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
