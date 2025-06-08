using System.Collections.ObjectModel;
using System.Windows.Input;
using Tunnel_Next.Models;
using Tunnel_Next.Services;
using Tunnel_Next.Services.ImageProcessing;
using Tunnel_Next.Services.Scripting;
using System.Threading.Tasks;
using System.IO;
using System;
using System.Collections.Generic;
using System.Windows.Media;

namespace Tunnel_Next.ViewModels
{
    /// <summary>
    /// 主窗口ViewModel
    /// </summary>
    public class MainViewModel : ViewModelBase
    {
        private string _taskStatus = "就绪";
        private double _zoomLevel = 1.0;
        private int _nodeCount = 0;
        private NodeGraph _currentNodeGraph = new();
        private Node? _selectedNode;
        private string _currentImagePath = string.Empty;
        private NodeEditorViewModel _nodeEditor;
        private readonly WorkFolderService _workFolderService = new();
        private readonly FileService _fileService;
        private readonly ThumbnailService _thumbnailService;
        private readonly RevivalScriptManager? _revivalScriptManager;
        private DocumentManagerService? _documentManager;

        public MainViewModel(RevivalScriptManager? revivalScriptManager = null, bool initializeImmediately = true)
        {
            _revivalScriptManager = revivalScriptManager;

            // 初始化服务（传入RevivalScriptManager）
            _fileService = new FileService(_workFolderService, _revivalScriptManager);
            _thumbnailService = new ThumbnailService(_workFolderService);
            _nodeEditor = new NodeEditorViewModel(_revivalScriptManager);

            InitializeCommands();

            // 根据参数决定是否立即初始化集合
            if (initializeImmediately)
            {
                InitializeCollections();
            }

            // 监听节点编辑器的变化
            _nodeEditor.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(_nodeEditor.SelectedNode))
                {
                    SelectedNode = _nodeEditor.SelectedNode;
                }
            };

            // 监听连接错误事件
            _nodeEditor.ConnectionErrorDetected += OnConnectionErrorDetected;

            // 异步初始化工作文件夹
            _ = InitializeAsync();
        }

        #region Properties

        /// <summary>
        /// 任务状态文本
        /// </summary>
        public string TaskStatus
        {
            get => _taskStatus;
            set => SetProperty(ref _taskStatus, value);
        }

        /// <summary>
        /// 缩放级别
        /// </summary>
        public double ZoomLevel
        {
            get => _zoomLevel;
            set
            {
                if (SetProperty(ref _zoomLevel, value))
                {
                    OnPropertyChanged(nameof(ZoomLevelText));
                }
            }
        }

        /// <summary>
        /// 缩放级别文本显示
        /// </summary>
        public string ZoomLevelText => $"缩放: {ZoomLevel:P0}";

        /// <summary>
        /// 节点数量
        /// </summary>
        public int NodeCount
        {
            get => _nodeCount;
            set
            {
                if (SetProperty(ref _nodeCount, value))
                {
                    OnPropertyChanged(nameof(NodeCountText));
                }
            }
        }

        /// <summary>
        /// 节点数量文本显示
        /// </summary>
        public string NodeCountText => $"节点: {NodeCount}";

        /// <summary>
        /// 当前节点图
        /// </summary>
        public NodeGraph CurrentNodeGraph
        {
            get => _currentNodeGraph;
            set => SetProperty(ref _currentNodeGraph, value);
        }

        /// <summary>
        /// 当前选中的节点
        /// </summary>
        public Node? SelectedNode
        {
            get => _selectedNode;
            set => SetProperty(ref _selectedNode, value);
        }

        /// <summary>
        /// 当前图像路径
        /// </summary>
        public string CurrentImagePath
        {
            get => _currentImagePath;
            set => SetProperty(ref _currentImagePath, value);
        }

        /// <summary>
        /// 胶片预览项集合
        /// </summary>
        public ObservableCollection<FilmPreviewItem> FilmPreviewItems { get; private set; } = new();

        /// <summary>
        /// 资源库项集合
        /// </summary>
        public ObservableCollection<ResourceLibraryItem> ResourceLibraryItems { get; private set; } = new();

        /// <summary>
        /// 节点编辑器ViewModel
        /// </summary>
        public NodeEditorViewModel NodeEditor => _nodeEditor;

        /// <summary>
        /// 工作文件夹路径
        /// </summary>
        public string WorkFolderPath => _workFolderService.WorkFolder;

        /// <summary>
        /// 文件服务
        /// </summary>
        public FileService FileService => _fileService;

        /// <summary>
        /// 文档管理器
        /// </summary>
        public DocumentManagerService? DocumentManager
        {
            get => _documentManager;
            set => SetProperty(ref _documentManager, value);
        }

        #endregion

        #region Commands

        public ICommand? NewNodeGraphCommand { get; private set; }
        public ICommand? OpenNodeGraphCommand { get; private set; }
        public ICommand? SaveNodeGraphCommand { get; private set; }
        public ICommand? SaveAsNodeGraphCommand { get; private set; }
        public ICommand? ImportImageCommand { get; private set; }
        public ICommand? ExportImageCommand { get; private set; }
        public ICommand? ExitCommand { get; private set; }

        public ICommand? UndoCommand { get; private set; }
        public ICommand? RedoCommand { get; private set; }
        public ICommand? CopyCommand { get; private set; }
        public ICommand? PasteCommand { get; private set; }
        public ICommand? DeleteCommand { get; private set; }

        public ICommand? ZoomFitCommand { get; private set; }
        public ICommand? ZoomInCommand { get; private set; }
        public ICommand? ZoomOutCommand { get; private set; }

        public ICommand? AddNodeCommand { get; private set; }
        public ICommand? DeleteNodeCommand { get; private set; }
        public ICommand? ArrangeNodesCommand { get; private set; }

        #endregion

        #region Private Methods

        private void InitializeCommands()
        {
            // 文件命令
            NewNodeGraphCommand = new RelayCommand(ExecuteNewNodeGraph);
            OpenNodeGraphCommand = new RelayCommand(ExecuteOpenNodeGraph);
            SaveNodeGraphCommand = new RelayCommand(ExecuteSaveNodeGraph); // 移除CanSaveNodeGraph检查
            SaveAsNodeGraphCommand = new RelayCommand(ExecuteSaveAsNodeGraph);
            ImportImageCommand = new RelayCommand(ExecuteImportImage);
            ExportImageCommand = new RelayCommand(ExecuteExportImage);
            ExitCommand = new RelayCommand(ExecuteExit);

            // 编辑命令
            UndoCommand = new RelayCommand(ExecuteUndo, CanUndo);
            RedoCommand = new RelayCommand(ExecuteRedo, CanRedo);
            CopyCommand = new RelayCommand(ExecuteCopy, CanCopy);
            PasteCommand = new RelayCommand(ExecutePaste, CanPaste);
            DeleteCommand = new RelayCommand(ExecuteDelete, CanDelete);

            // 视图命令
            ZoomFitCommand = new RelayCommand(ExecuteZoomFit);
            ZoomInCommand = new RelayCommand(ExecuteZoomIn);
            ZoomOutCommand = new RelayCommand(ExecuteZoomOut);

            // 节点命令
            AddNodeCommand = new RelayCommand(ExecuteAddNode);
            DeleteNodeCommand = new RelayCommand(ExecuteDeleteNode, CanDeleteNode);
            ArrangeNodesCommand = new RelayCommand(ExecuteArrangeNodes);
        }

        private async void InitializeCollections()
        {
            await InitializeCollectionsAsync();
        }

        public async Task InitializeCollectionsAsync()
        {
            try
            {

                // 初始化当前节点图
                CurrentNodeGraph = _fileService.CreateNewNodeGraph();

                // 使用新的加载逻辑
                await _nodeEditor.LoadNodeGraphAsync(CurrentNodeGraph);

                // 监听节点图变化
                CurrentNodeGraph.Nodes.CollectionChanged += (s, e) =>
                {
                    NodeCount = CurrentNodeGraph.Nodes.Count;
                };

                // 监听节点编辑器的节点变化
                _nodeEditor.Nodes.CollectionChanged += (s, e) =>
                {
                    NodeCount = _nodeEditor.Nodes.Count;
                };

                // 监听节点图修改状态变化
                _nodeEditor.NodeGraphModified += () =>
                {
                    CurrentNodeGraph.IsModified = true;
                    CurrentNodeGraph.LastModified = DateTime.Now;
                };

            }
            catch (Exception ex)
            {
                throw;
            }
        }

        #endregion

        #region Command Implementations

        private async void ExecuteNewNodeGraph()
        {
            try
            {
                TaskStatus = "正在创建新的节点图...";

                // 检查当前节点图是否需要自动保存
                if (CurrentNodeGraph != null && CurrentNodeGraph.IsModified && !string.IsNullOrEmpty(CurrentNodeGraph.FilePath))
                {
                    try
                    {

                        // 使用FileService保存当前节点图
                        var saveSuccess = await _fileService.SaveNodeGraphAsync(CurrentNodeGraph);

                        if (saveSuccess)
                        {
                        }
                        else
                        {
                        }
                    }
                    catch (Exception ex)
                    {
                        // 询问用户是否继续
                        var result = System.Windows.MessageBox.Show(
                            $"自动保存当前节点图失败：{ex.Message}\n\n是否继续创建新节点图？",
                            "自动保存失败",
                            System.Windows.MessageBoxButton.YesNo,
                            System.Windows.MessageBoxImage.Warning);

                        if (result == System.Windows.MessageBoxResult.No)
                        {
                            TaskStatus = "取消创建新节点图";
                            return;
                        }
                    }
                }

                // 弹出对话框询问节点图名称
                var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                var defaultName = $"新节点图_{timestamp}";

                var dialog = new Microsoft.Win32.SaveFileDialog
                {
                    Title = "创建新节点图",
                    Filter = "节点图文件 (*.tnx)|*.tnx",
                    DefaultExt = "tnx",
                    FileName = defaultName,
                    InitialDirectory = _workFolderService.NodeGraphsFolder
                };

                if (dialog.ShowDialog() == true)
                {
                    var filePath = dialog.FileName;
                    var fileName = Path.GetFileNameWithoutExtension(filePath);


                    // 创建新节点图
                    CurrentNodeGraph = _fileService.CreateNewNodeGraph(fileName);
                    CurrentNodeGraph.FilePath = filePath;
                    CurrentNodeGraph.IsModified = true; // 标记为已修改，需要保存


                    // 加载到编辑器
                    await _nodeEditor.LoadNodeGraphAsync(CurrentNodeGraph);

                    // 使用FileService保存新节点图
                    var success = await _fileService.SaveNodeGraphAsync(CurrentNodeGraph, filePath);

                    if (success)
                    {

                        // 生成缩略图
                        await GenerateThumbnailForCurrentNodeGraph();

                        // 更新胶片预览
                        await UpdateFilmPreviewAsync();

                        // 使用文档管理器打开新创建的节点图
                        if (DocumentManager != null)
                        {
                            TaskStatus = $"正在打开新创建的节点图: {fileName}";
                            var document = await DocumentManager.LoadNodeGraphDocumentAsync(filePath);
                            TaskStatus = $"已创建并打开新节点图: {fileName}";
                        }
                        else
                        {
                            TaskStatus = $"已创建新节点图: {fileName}";
                        }
                    }
                    else
                    {
                        TaskStatus = "保存新节点图失败";
                    }
                }
                else
                {
                    TaskStatus = "取消创建新节点图";
                }
            }
            catch (Exception ex)
            {
                TaskStatus = $"创建节点图失败: {ex.Message}";
            }
        }

        private async void ExecuteOpenNodeGraph()
        {
            try
            {
                var openDialog = new Microsoft.Win32.OpenFileDialog
                {
                    Filter = "节点图文件 (*.json)|*.json|所有文件 (*.*)|*.*",
                    Title = "打开节点图"
                };

                if (openDialog.ShowDialog() == true)
                {
                    // 获取文档管理器
                    if (DocumentManager == null)
                    {
                        TaskStatus = "无法获取文档管理器";
                        return;
                    }

                    TaskStatus = $"正在打开文档: {openDialog.FileName}";
                    var document = await DocumentManager.LoadNodeGraphDocumentAsync(openDialog.FileName);
                    TaskStatus = $"已打开节点图: {document.Title}";
                }
            }
            catch (Exception ex)
            {
                TaskStatus = $"打开节点图失败: {ex.Message}";
            }
        }

        private async void ExecuteSaveNodeGraph()
        {
            try
            {

                // 检查是否有活动的多文档Tab文档

                if (_documentManager != null && _documentManager.ActiveDocumentId != null)
                {
                    var activeDocument = _documentManager.GetNodeGraphDocument(_documentManager.ActiveDocumentId);

                    if (activeDocument != null)
                    {
                        TaskStatus = $"正在保存文档: {activeDocument.Title}";

                        // 在后台线程执行保存操作，避免UI卡死
                        var saveSuccess = await Task.Run(() => {
                            var result = activeDocument.Save();
                            return result;
                        });

                        if (saveSuccess)
                        {
                            TaskStatus = $"已保存文档: {activeDocument.Title}";
                        }
                        else
                        {
                            TaskStatus = $"保存文档失败: {activeDocument.Title}";
                        }
                        return;
                    }
                }


                // 获取当前节点图
                TaskStatus = "正在创建节点图数据...";
                var nodeGraph = _nodeEditor.CreateNodeGraph();

                // 使用FileService保存节点图
                TaskStatus = "正在保存节点图...";
                var success = await _fileService.SaveNodeGraphAsync(nodeGraph);

                if (success)
                {
                    CurrentNodeGraph = nodeGraph;

                    // 生成缩略图
                    await GenerateThumbnailForCurrentNodeGraph();

                    // 更新胶片预览
                    await UpdateFilmPreviewAsync();

                    TaskStatus = $"已保存节点图: {nodeGraph.Name}";
                }
                else
                {
                    TaskStatus = $"保存节点图失败: {nodeGraph.Name}";
                }
            }
            catch (Exception ex)
            {
                TaskStatus = $"保存节点图失败: {ex.Message}";
                if (ex.InnerException != null)
                {
                }
            }
        }



        private async void ExecuteSaveAsNodeGraph()
        {
            try
            {
                // 获取当前节点图
                var nodeGraph = _nodeEditor.CreateNodeGraph();

                // 使用FileService另存为节点图
                var success = await _fileService.SaveNodeGraphAsync(nodeGraph);

                if (success)
                {
                    CurrentNodeGraph = nodeGraph;
                    TaskStatus = $"已另存为节点图: {nodeGraph.Name}";
                }
                else
                {
                    TaskStatus = "另存为节点图失败";
                }
            }
            catch (Exception ex)
            {
                TaskStatus = $"另存为节点图失败: {ex.Message}";
            }
        }

        private async void ExecuteImportImage()
        {
            var filePath = _fileService.SelectImageFile("选择要导入的图像");
            if (!string.IsNullOrEmpty(filePath))
            {
                try
                {
                    // 复制图像到工作文件夹
                    var workFolderPath = await _workFolderService.CopyImageToWorkFolderAsync(filePath);
                    CurrentImagePath = workFolderPath;
                    TaskStatus = $"已导入图像: {Path.GetFileName(workFolderPath)}";

                    // 创建图像输入节点
                    var position = new System.Windows.Point(50, 50);
                    _nodeEditor.AddSpecificNodeCommand?.Execute("图像输入");

                    // 设置文件路径参数
                    var lastNode = _nodeEditor.Nodes.LastOrDefault();
                    if (lastNode != null)
                    {
                        var filePathParam = lastNode.Parameters.FirstOrDefault(p => p.Name == "FilePath");
                        if (filePathParam != null)
                        {
                            filePathParam.Value = workFolderPath;
                        }
                    }

                    // 更新资源库
                    await UpdateResourceLibraryAsync();
                }
                catch (Exception ex)
                {
                    TaskStatus = $"导入图像失败: {ex.Message}";
                }
            }
        }

        private void ExecuteExportImage()
        {
            var savePath = _fileService.SelectImageSavePath("导出图像");
            if (!string.IsNullOrEmpty(savePath))
            {
                TaskStatus = $"图像将导出到: {System.IO.Path.GetFileName(savePath)}";
                // TODO: 实现实际的图像导出逻辑
            }
        }

        private void ExecuteExit()
        {
            // 应用程序退出逻辑
            System.Windows.Application.Current.Shutdown();
        }

        private void ExecuteUndo()
        {
            TaskStatus = "撤销功能待实现";
        }

        private bool CanUndo()
        {
            return false; // 待实现
        }

        private void ExecuteRedo()
        {
            TaskStatus = "重做功能待实现";
        }

        private bool CanRedo()
        {
            return false; // 待实现
        }

        private void ExecuteCopy()
        {
            TaskStatus = "复制功能待实现";
        }

        private bool CanCopy()
        {
            return SelectedNode != null;
        }

        private void ExecutePaste()
        {
            TaskStatus = "粘贴功能待实现";
        }

        private bool CanPaste()
        {
            return false; // 待实现
        }

        private void ExecuteDelete()
        {
            if (SelectedNode != null)
            {
                _nodeEditor.DeleteNodeCommand?.Execute(null);
                TaskStatus = "删除了选中的节点";
            }
        }

        private bool CanDelete()
        {
            return SelectedNode != null;
        }

        private void ExecuteZoomFit()
        {
            // 通知主窗口执行缩放适应
            ZoomFitRequested?.Invoke();
            TaskStatus = "缩放适应";
        }

        private void ExecuteZoomIn()
        {
            // 通知主窗口执行放大
            ZoomInRequested?.Invoke();
            TaskStatus = "放大视图";
        }

        private void ExecuteZoomOut()
        {
            // 通知主窗口执行缩小
            ZoomOutRequested?.Invoke();
            TaskStatus = "缩小视图";
        }

        // 事件用于通知主窗口执行缩放操作
        public event Action? ZoomFitRequested;
        public event Action? ZoomInRequested;
        public event Action? ZoomOutRequested;

        // 添加节点菜单请求事件
        public event Action<object>? ShowNodeMenuRequested;

        private void ExecuteAddNode()
        {
            // 触发显示节点菜单事件
            ShowNodeMenuRequested?.Invoke(null);
        }

        private void ExecuteDeleteNode()
        {
            ExecuteDelete();
        }

        private bool CanDeleteNode()
        {
            return CanDelete();
        }

        private void ExecuteArrangeNodes()
        {
            TaskStatus = "整理节点功能待实现";
        }

        #endregion

        #region Initialization

        /// <summary>
        /// 异步初始化
        /// </summary>
        private async Task InitializeAsync()
        {
            try
            {
                // 初始化工作文件夹
                await _workFolderService.InitializeAsync();

                // 更新胶片预览
                await UpdateFilmPreviewAsync();

                // 更新资源库
                await UpdateResourceLibraryAsync();

                TaskStatus = $"工作文件夹已初始化: {_workFolderService.WorkFolder}";
            }
            catch (Exception ex)
            {
                TaskStatus = $"初始化失败: {ex.Message}";
            }
        }

        /// <summary>
        /// 更新胶片预览
        /// </summary>
        public async Task UpdateFilmPreviewAsync()
        {
            try
            {
                FilmPreviewItems.Clear();

                var nodeGraphFiles = _workFolderService.GetNodeGraphFiles();

                foreach (var filePath in nodeGraphFiles)
                {
                    var fileName = Path.GetFileNameWithoutExtension(filePath);
                    var fileInfo = new FileInfo(filePath);

                    var item = new FilmPreviewItem
                    {
                        Name = fileName,
                        FilePath = filePath,
                        CreatedTime = fileInfo.CreationTime,
                        LastModified = fileInfo.LastWriteTime
                    };

                    // 加载缩略图
                    var thumbnailPath = _thumbnailService.GetNodeGraphThumbnailPath(filePath);
                    if (File.Exists(thumbnailPath))
                    {
                        item.Thumbnail = _thumbnailService.LoadThumbnail(thumbnailPath);
                    }
                    else
                    {
                        // 生成默认缩略图
                        await _thumbnailService.GenerateNodeGraphThumbnailAsync(filePath);
                        item.Thumbnail = _thumbnailService.LoadThumbnail(thumbnailPath);
                    }

                    FilmPreviewItems.Add(item);
                }

            }
            catch (Exception ex)
            {
            }
        }

        /// <summary>
        /// 更新资源库
        /// </summary>
        public async Task UpdateResourceLibraryAsync()
        {
            try
            {
                ResourceLibraryItems.Clear();

                var imageFiles = _workFolderService.GetImageFiles();

                foreach (var filePath in imageFiles)
                {
                    var fileName = Path.GetFileName(filePath);
                    var fileInfo = new FileInfo(filePath);

                    var item = new ResourceLibraryItem
                    {
                        Name = fileName,
                        FilePath = filePath,
                        ItemType = ResourceItemType.Image,
                        FileSize = fileInfo.Length,
                        LastModified = fileInfo.LastWriteTime
                    };

                    // 生成图片缩略图
                    item.Thumbnail = await _thumbnailService.GenerateImageThumbnailAsync(filePath);

                    ResourceLibraryItems.Add(item);
                }

            }
            catch (Exception ex)
            {
            }
        }

        /// <summary>
        /// 为当前节点图生成缩略图
        /// </summary>
        private async Task GenerateThumbnailForCurrentNodeGraph()
        {
            try
            {
                if (!string.IsNullOrEmpty(CurrentNodeGraph.FilePath))
                {
                    // 尝试获取预览节点的输出作为缩略图
                    var previewNode = _nodeEditor.Nodes.FirstOrDefault(n => n.Title == "预览节点");
                    OpenCvSharp.Mat? previewMat = null;

                    if (previewNode?.ProcessedOutputs.TryGetValue("f32bmp", out var output) == true &&
                        output is OpenCvSharp.Mat mat)
                    {
                        previewMat = mat;
                    }

                    await _thumbnailService.GenerateNodeGraphThumbnailAsync(CurrentNodeGraph.FilePath, previewMat);
                }
            }
            catch (Exception ex)
            {
            }
        }

        /// <summary>
        /// 连接错误检测事件处理
        /// </summary>
        private void OnConnectionErrorDetected(string errorMessage)
        {
            if (string.IsNullOrEmpty(errorMessage))
            {
                // 清除错误状态，显示就绪信息
                TaskStatus = "就绪";
            }
            else
            {
                // 显示连接错误信息
                TaskStatus = errorMessage;
            }
        }

        /// <summary>
        /// 设置当前活动节点编辑器
        /// </summary>
        /// <param name="nodeEditor">节点编辑器</param>
        public void SetActiveNodeEditor(NodeEditorViewModel nodeEditor)
        {
            if (nodeEditor == null)
                return;

            // 更新引用
            _nodeEditor = nodeEditor;

            // 监听节点编辑器的变化
            nodeEditor.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(nodeEditor.SelectedNode))
                {
                    SelectedNode = nodeEditor.SelectedNode;
                }
            };

            // 监听连接错误事件
            nodeEditor.ConnectionErrorDetected += OnConnectionErrorDetected;

            // 更新节点数量
            NodeCount = nodeEditor.Nodes.Count;

        }

        #endregion
    }
}
