using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using Tunnel_Next.Controls;
using Tunnel_Next.Models;
using Tunnel_Next.Services;
using Tunnel_Next.Services.Scripting;
using Tunnel_Next.ViewModels;

namespace Tunnel_Next.UtilityTools.BatchProcessor.ViewModels
{
    /// <summary>
    /// 批量处理器窗口的视图模型
    /// </summary>
    public class BatchProcessViewModel : INotifyPropertyChanged
    {
        #region 私有字段

        private readonly WorkFolderService _workFolderService;
        private readonly ThumbnailService _thumbnailService;
        private readonly FileService _fileService;
        private readonly NodeEditorViewModel? _nodeEditorViewModel;
        private readonly RevivalScriptManager? _revivalScriptManager;
        private NodePreviewControl? _previewControl;
        private BatchProcessNodeGraphItem? _firstSelectedItem;
        private string _previewTitle = "";
        private bool _isNoSelectionTextVisible = true;
        private int _selectedItemsCount = 0;

        #endregion

        #region 公共属性

        /// <summary>
        /// 所有可用的节点图项目
        /// </summary>
        public ObservableCollection<BatchProcessNodeGraphItem> NodeGraphItems { get; } = new();

        /// <summary>
        /// 已选择的节点图项目
        /// </summary>
        public ObservableCollection<BatchProcessNodeGraphItem> SelectedItems { get; } = new();

        /// <summary>
        /// 预览标题
        /// </summary>
        public string PreviewTitle
        {
            get => _previewTitle;
            set
            {
                if (_previewTitle != value)
                {
                    _previewTitle = value;
                    OnPropertyChanged(nameof(PreviewTitle));
                }
            }
        }

        /// <summary>
        /// 是否显示无选择提示文本
        /// </summary>
        public bool IsNoSelectionTextVisible
        {
            get => _isNoSelectionTextVisible;
            set
            {
                if (_isNoSelectionTextVisible != value)
                {
                    _isNoSelectionTextVisible = value;
                    OnPropertyChanged(nameof(IsNoSelectionTextVisible));
                }
            }
        }

        /// <summary>
        /// 已选择项目数量
        /// </summary>
        public int SelectedItemsCount
        {
            get => _selectedItemsCount;
            set
            {
                if (_selectedItemsCount != value)
                {
                    _selectedItemsCount = value;
                    OnPropertyChanged(nameof(SelectedItemsCount));
                }
            }
        }

        /// <summary>
        /// 预览控件
        /// </summary>
        public NodePreviewControl? PreviewControl
        {
            get => _previewControl;
            set
            {
                if (_previewControl != value)
                {
                    _previewControl = value;
                    OnPropertyChanged(nameof(PreviewControl));
                }
            }
        }

        #endregion

        #region 命令

        /// <summary>
        /// 刷新命令
        /// </summary>
        public ICommand RefreshCommand { get; }

        /// <summary>
        /// 取消命令
        /// </summary>
        public ICommand CancelCommand { get; }

        /// <summary>
        /// 继续命令
        /// </summary>
        public ICommand ContinueCommand { get; }

        /// <summary>
        /// 节点图点击命令
        /// </summary>
        public ICommand NodeGraphClickCommand { get; }

        #endregion

        #region 构造函数

        public BatchProcessViewModel(RevivalScriptManager? revivalScriptManager)
        {
            _revivalScriptManager = revivalScriptManager;

            // 初始化服务
            _workFolderService = new WorkFolderService();
            _thumbnailService = new ThumbnailService(_workFolderService);
            _fileService = new FileService(_workFolderService, _revivalScriptManager);

            // 创建一个轻量级的NodeEditorViewModel用于预览
            _nodeEditorViewModel = new NodeEditorViewModel(_revivalScriptManager);

            // 初始化命令
            RefreshCommand = new RelayCommand(async () => await LoadNodeGraphsAsync());
            CancelCommand = new RelayCommand(ExecuteCancel);
            ContinueCommand = new RelayCommand(ExecuteContinue, CanExecuteContinue);
            NodeGraphClickCommand = new RelayCommand<BatchProcessNodeGraphItem>(ExecuteNodeGraphClick);

            // 初始化预览控件
            InitializePreviewControl();
        }

        #endregion

        #region 公共方法

        /// <summary>
        /// 初始化异步操作
        /// </summary>
        public async Task InitializeAsync()
        {
            try
            {
                // 初始化工作文件夹
                await _workFolderService.InitializeAsync();

                // 加载节点图列表
                await LoadNodeGraphsAsync();

                // 重置第一个选中项
                _firstSelectedItem = null;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"初始化批量处理器时发生错误: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #endregion

        #region 私有方法

        /// <summary>
        /// 初始化预览控件
        /// </summary>
        private void InitializePreviewControl()
        {
            try
            {
                if (_nodeEditorViewModel != null)
                {
                    _previewControl = new NodePreviewControl();
                    _previewControl.DataContext = _nodeEditorViewModel;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"初始化预览控件失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 加载节点图列表
        /// </summary>
        private async Task LoadNodeGraphsAsync()
        {
            try
            {
                // 清空现有项目
                NodeGraphItems.Clear();
                SelectedItems.Clear();
                UpdateSelectedItemsCount();

                // 获取所有节点图文件
                var nodeGraphFiles = _workFolderService.GetNodeGraphFiles();

                foreach (var filePath in nodeGraphFiles)
                {
                    try
                    {
                        var fileInfo = new FileInfo(filePath);
                        var item = new BatchProcessNodeGraphItem
                        {
                            Name = Path.GetFileNameWithoutExtension(fileInfo.Name),
                            FilePath = fileInfo.FullName,
                            CreationTime = fileInfo.CreationTime,
                            LastModified = fileInfo.LastWriteTime
                        };

                        // 生成缩略图
                        var thumbnailPath = _thumbnailService.GetNodeGraphThumbnailPath(filePath);
                        if (File.Exists(thumbnailPath))
                        {
                            var uri = new Uri(thumbnailPath);
                            item.Thumbnail = new BitmapImage(uri);
                        }
                        else
                        {
                            // 如果缩略图不存在，尝试生成一个
                            await _thumbnailService.GenerateNodeGraphThumbnailAsync(filePath);
                            if (File.Exists(thumbnailPath))
                            {
                                var uri = new Uri(thumbnailPath);
                                item.Thumbnail = new BitmapImage(uri);
                            }
                        }

                        NodeGraphItems.Add(item);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"加载节点图文件失败 {Path.GetFileName(filePath)}: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"加载节点图列表时发生错误: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 更新已选择项目数量
        /// </summary>
        private void UpdateSelectedItemsCount()
        {
            SelectedItemsCount = SelectedItems.Count;
        }

        /// <summary>
        /// 执行取消命令
        /// </summary>
        private void ExecuteCancel()
        {
            // 通过事件通知窗口关闭
            CancelRequested?.Invoke();
        }

        /// <summary>
        /// 执行继续命令
        /// </summary>
        private void ExecuteContinue()
        {
            // 检查是否有选中的节点图
            if (SelectedItems.Count == 0)
            {
                MessageBox.Show("请至少选择一个节点图进行处理", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // 打开批处理编辑器窗口
            ContinueRequested?.Invoke(SelectedItems);
        }

        /// <summary>
        /// 判断是否可以执行继续命令
        /// </summary>
        private bool CanExecuteContinue()
        {
            return SelectedItems.Count > 0;
        }

        /// <summary>
        /// 执行节点图点击命令
        /// </summary>
        private void ExecuteNodeGraphClick(BatchProcessNodeGraphItem? item)
        {
            if (item == null) return;

            try
            {
                bool isCtrlPressed = (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control;

                if (isCtrlPressed)
                {
                    // Ctrl+点击：多选模式
                    item.IsSelected = !item.IsSelected;

                    // 如果是选中操作且没有第一个选择项，设置为第一个选择项
                    if (item.IsSelected && _firstSelectedItem == null)
                    {
                        _firstSelectedItem = item;
                    }
                    // 如果是取消选择且它是第一个选择项，需要重新找第一个
                    else if (!item.IsSelected && item == _firstSelectedItem)
                    {
                        _firstSelectedItem = NodeGraphItems.FirstOrDefault(i => i.IsSelected);
                    }

                    // 更新选中项列表
                    UpdateSelectedItems(item);

                    // 显示预览（无论是否选中，都显示当前项的预览）
                    ShowPreview(item);
                }
                else
                {
                    // 普通单击：切换当前项的选中状态
                    item.IsSelected = !item.IsSelected;

                    // 如果是选中操作，设置为第一个选择项
                    if (item.IsSelected)
                    {
                        _firstSelectedItem = item;
                    }
                    // 如果是取消选择且它是第一个选择项，需要重新找第一个
                    else if (item == _firstSelectedItem)
                    {
                        _firstSelectedItem = NodeGraphItems.FirstOrDefault(i => i.IsSelected);
                    }

                    // 更新选中项列表
                    UpdateSelectedItems(item);

                    // 显示预览（无论是否选中，都显示当前项的预览）
                    ShowPreview(item);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"节点图点击处理错误: {ex.Message}");
            }
        }

        /// <summary>
        /// 更新选中项列表
        /// </summary>
        private void UpdateSelectedItems(BatchProcessNodeGraphItem item)
        {
            if (item.IsSelected)
            {
                if (!SelectedItems.Contains(item))
                {
                    SelectedItems.Add(item);
                }
            }
            else
            {
                SelectedItems.Remove(item);
            }

            UpdateSelectedItemsCount();
        }

        /// <summary>
        /// 显示预览
        /// </summary>
        private async void ShowPreview(BatchProcessNodeGraphItem item)
        {
            try
            {
                if (_nodeEditorViewModel == null || _previewControl == null)
                {
                    HidePreview();
                    return;
                }

                // 设置预览标题
                PreviewTitle = $"- {item.Name}";

                try
                {
                    // 读取文件内容
                    string json = await File.ReadAllTextAsync(item.FilePath);

                    // 创建反序列化器
                    var deserializer = new NodeGraphDeserializer(_revivalScriptManager);

                    // 反序列化节点图
                    var nodeGraph = deserializer.DeserializeNodeGraph(json);

                    // 加载到视图模型
                    await _nodeEditorViewModel.LoadNodeGraphAsync(nodeGraph);

                    // 隐藏提示文本
                    IsNoSelectionTextVisible = false;
                }
                catch (Exception ex)
                {
                    IsNoSelectionTextVisible = true;
                    System.Diagnostics.Debug.WriteLine($"加载节点图预览失败: {ex.Message}");
                }
            }
            catch (Exception ex)
            {
                IsNoSelectionTextVisible = true;
                System.Diagnostics.Debug.WriteLine($"严重错误: {ex.Message}");
            }
        }

        /// <summary>
        /// 隐藏预览
        /// </summary>
        private void HidePreview()
        {
            // 清除预览标题
            PreviewTitle = "";

            // 显示提示文本
            IsNoSelectionTextVisible = true;
        }

        #endregion

        #region 事件

        public event PropertyChangedEventHandler? PropertyChanged;
        public event Action? CancelRequested;
        public event Action<IEnumerable<BatchProcessNodeGraphItem>>? ContinueRequested;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion
    }
}
