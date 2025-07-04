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
using System.Linq;
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
        private bool _isNodeStatusVisible = false;
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

                    // 如果节点状态显示开启，实时更新处理标记
                    if (IsNodeStatusVisible)
                    {
                        UpdateProcessingMarksFromSelectedNode();
                    }
                }
            };

            // 监听连接错误事件
            _nodeEditor.ConnectionErrorDetected += OnConnectionErrorDetected;

            // 监听连接变化事件
            _nodeEditor.ConnectionsChanged += OnConnectionsChanged;

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

        /// <summary>
        /// 节点状态是否可见
        /// </summary>
        public bool IsNodeStatusVisible
        {
            get => _isNodeStatusVisible;
            set
            {
                if (SetProperty(ref _isNodeStatusVisible, value))
                {
                    // 当状态改变时，更新所有节点的显示状态
                    UpdateNodeStatusVisibility();
                }
            }
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
        public ICommand? ArrangeNodesDenseCommand { get; private set; }
        public ICommand? ArrangeNodesVerticalCommand { get; private set; }
        public ICommand? ArrangeNodesBalancedCommand { get; private set; }
        public ICommand? ShowNodeStatusCommand { get; private set; }

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
            ArrangeNodesDenseCommand = new RelayCommand(ExecuteArrangeNodesDense);
            ArrangeNodesVerticalCommand = new RelayCommand(ExecuteArrangeNodesVertical);
            ArrangeNodesBalancedCommand = new RelayCommand(ExecuteArrangeNodesBalanced);
            ShowNodeStatusCommand = new RelayCommand(ExecuteShowNodeStatus);
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
                TaskStatus = "准备打开模板向导...";

                // 打开模板选择窗口
                var templateDialog = new Tunnel_Next.Windows.NewNodeGraphWindow
                {
                    Owner = System.Windows.Application.Current?.MainWindow
                };

                var result = templateDialog.ShowDialog();
                if (result != true)
                {
                    TaskStatus = "取消创建新节点图";
                    return;
                }

                // 解析模板路径与新名称
                if (templateDialog.Tag is not Tuple<string, string> tpl)
                {
                    TaskStatus = "未选择有效模板";
                    return;
                }

                var templatePath = tpl.Item1;
                var newName = tpl.Item2;

                if (string.IsNullOrEmpty(templatePath) || !File.Exists(templatePath))
                {
                    TaskStatus = "未选择有效模板";
                    return;
                }

                // 步骤1: 反序列化模板
                var revivalManager = _revivalScriptManager ?? new RevivalScriptManager(_workFolderService.UserScriptsFolder, _workFolderService.UserResourcesFolder);
                var deserializer = new NodeGraphDeserializer(revivalManager);
                var json = await File.ReadAllTextAsync(templatePath);
                var nodeGraph = deserializer.DeserializeNodeGraph(json);

                // 步骤2: 设置名称并保存到项目（FileService 会自动创建项目文件夹）
                nodeGraph.Name = newName;

                var projectsDir = _workFolderService.NodeGraphsFolder;
                Directory.CreateDirectory(projectsDir);
                var initialPath = Path.Combine(projectsDir, newName + ".nodegraph");

                var saveSuccess = await _fileService.SaveNodeGraphAsync(nodeGraph, initialPath);
                if (!saveSuccess)
                {
                    TaskStatus = "保存新节点图失败";
                    return;
                }

                // 步骤3: 打开文档
                if (DocumentManager == null)
                {
                    TaskStatus = "无法获取文档管理器";
                    return;
                }

                TaskStatus = "正在打开节点图...";
                var document = await DocumentManager.LoadNodeGraphDocumentAsync(nodeGraph.FilePath);

                // 步骤4: 刷新胶片预览
                await UpdateFilmPreviewAsync();

                TaskStatus = $"已创建并打开节点图: {document.Title}";
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
                    Filter = "节点图文件 (*.nodegraph)|*.nodegraph|所有文件 (*.*)|*.*",
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

                    // 让 FilmPreview 稍后刷新并生成缩略图
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
            var filePaths = _fileService.SelectMultipleImageFiles("选择要导入的图像");
            if (filePaths != null && filePaths.Length > 0)
            {
                try
                {
                    var importedCount = 0;
                    var startX = 50.0;
                    var startY = 50.0;
                    var nodeSpacing = 200.0; // 节点之间的间距

                    foreach (var filePath in filePaths)
                    {
                        // 复制图像到工作文件夹
                        var workFolderPath = await _workFolderService.CopyImageToWorkFolderAsync(filePath);

                        // 如果是第一个图像，设置为当前图像路径
                        if (importedCount == 0)
                        {
                            CurrentImagePath = workFolderPath;
                        }

                        // 计算节点位置，避免重叠
                        var nodeX = startX + (importedCount % 4) * nodeSpacing; // 每行最多4个节点
                        var nodeY = startY + (importedCount / 4) * nodeSpacing; // 超过4个节点时换行

                        // 设置节点位置
                        _nodeEditor.SetPendingNodePosition(new System.Windows.Point(nodeX, nodeY));

                        // 创建图像输入节点
                        _nodeEditor.AddSpecificNodeCommand?.Execute("图像输入");

                        // 设置文件路径参数
                        var lastNode = _nodeEditor.Nodes.LastOrDefault();
                        if (lastNode != null)
                        {
                            bool parameterSet = false;

                            // 方法1：通过ViewModel设置参数（最佳方法）
                            if (lastNode.ViewModel is Services.Scripting.IScriptViewModel scriptViewModel)
                            {
                                try
                                {
                                    // 使用反射设置ViewModel的ImagePath属性
                                    var viewModelType = scriptViewModel.GetType();
                                    var imagePathProperty = viewModelType.GetProperty("ImagePath");
                                    if (imagePathProperty != null && imagePathProperty.CanWrite)
                                    {
                                        imagePathProperty.SetValue(scriptViewModel, workFolderPath);
                                        parameterSet = true;
                                        TaskStatus = $"已通过ViewModel设置 {lastNode.Title} 的ImagePath = {Path.GetFileName(workFolderPath)}";
                                    }
                                }
                                catch (Exception ex)
                                {
                                    TaskStatus = $"通过ViewModel设置参数失败: {ex.Message}";
                                }
                            }

                            // 方法2：直接通过脚本实例设置参数
                            if (!parameterSet && lastNode.Tag is Services.Scripting.IRevivalScript scriptInstance)
                            {
                                try
                                {
                                    // 使用反射直接设置脚本实例的ImagePath属性
                                    var scriptType = scriptInstance.GetType();
                                    var imagePathProperty = scriptType.GetProperty("ImagePath");
                                    if (imagePathProperty != null && imagePathProperty.CanWrite)
                                    {
                                        imagePathProperty.SetValue(scriptInstance, workFolderPath);

                                        // 触发参数变化事件，确保UI和处理流程同步
                                        if (scriptInstance is Services.Scripting.RevivalScriptBase revivalScript)
                                        {
                                            revivalScript.OnParameterChanged("ImagePath", workFolderPath);
                                        }

                                        parameterSet = true;
                                        TaskStatus = $"已通过脚本实例设置 {lastNode.Title} 的ImagePath = {Path.GetFileName(workFolderPath)}";
                                    }
                                }
                                catch (Exception ex)
                                {
                                    TaskStatus = $"通过脚本实例设置参数失败: {ex.Message}";
                                }
                            }

                            // 方法3：如果前面的方法都失败，尝试通过NodeParameter设置
                            if (!parameterSet)
                            {
                                var filePathParam = lastNode.Parameters.FirstOrDefault(p =>
                                    p.Name == "ImagePath" ||
                                    p.Name == "FilePath" ||
                                    p.Name == "Path" ||
                                    p.Name == "文件路径" ||
                                    p.Name.ToLower().Contains("path"));

                                if (filePathParam != null)
                                {
                                    filePathParam.Value = workFolderPath;
                                    parameterSet = true;
                                    TaskStatus = $"已通过NodeParameter设置 {lastNode.Title} 的参数 {filePathParam.Name} = {Path.GetFileName(workFolderPath)}";
                                }
                            }

                            // 如果都失败了，显示调试信息
                            if (!parameterSet)
                            {
                                var paramNames = string.Join(", ", lastNode.Parameters.Select(p => p.Name));
                                var scriptType = lastNode.Tag?.GetType().Name ?? "Unknown";
                                TaskStatus = $"警告：节点 {lastNode.Title} (脚本类型: {scriptType}) 参数设置失败。可用参数: {paramNames}";
                            }
                        }
                        else
                        {
                            TaskStatus = "警告：未能获取最后创建的节点";
                        }

                        importedCount++;
                    }

                    TaskStatus = $"已导入 {importedCount} 张图像";

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
            try
            {
                if (_nodeEditor?.Nodes == null || !_nodeEditor.Nodes.Any())
                {
                    TaskStatus = "没有节点需要整理";
                    return;
                }

                TaskStatus = "正在整理节点...";

                // 执行节点整理算法
                ArrangeNodesAlgorithm();

                TaskStatus = $"已整理 {_nodeEditor.Nodes.Count} 个节点";
            }
            catch (Exception ex)
            {
                TaskStatus = $"整理节点失败: {ex.Message}";
            }
        }

        /// <summary>
        /// 节点整理算法 - 基于拓扑排序的自动布局
        /// </summary>
        private void ArrangeNodesAlgorithm()
        {
            if (_nodeEditor?.Nodes == null || _nodeEditor.Connections == null)
                return;

            var nodes = _nodeEditor.Nodes.ToList();
            var connections = _nodeEditor.Connections.ToList();

            // 1. 更新所有节点的高度以适应端口数量
            foreach (var node in nodes)
            {
                node.UpdateNodeHeight();
            }

            // 2. 使用拓扑排序确定节点层级
            var layers = PerformTopologicalLayering(nodes, connections);

            // 3. 布局参数
            const double layerSpacing = 200; // 层间距
            const double nodeSpacing = 20;   // 节点间距
            const double startX = 50;        // 起始X位置
            const double startY = 50;        // 起始Y位置

            // 4. 为每一层布局节点
            double currentX = startX;

            for (int layerIndex = 0; layerIndex < layers.Count; layerIndex++)
            {
                var layer = layers[layerIndex];
                if (!layer.Any()) continue;

                // 计算当前层的总高度
                double totalHeight = layer.Sum(n => n.Height) + (layer.Count - 1) * nodeSpacing;

                // 计算起始Y位置（居中对齐）
                double currentY = startY;

                // 按节点高度排序，较高的节点放在中间
                var sortedLayer = layer.OrderBy(n => n.Height).ToList();

                // 布局当前层的节点
                foreach (var node in sortedLayer)
                {
                    node.X = currentX;
                    node.Y = currentY;

                    currentY += node.Height + nodeSpacing;
                }

                // 移动到下一层
                currentX += GetMaxNodeWidth(layer) + layerSpacing;
            }

            // 5. 通知节点图已修改 - 通过设置节点属性来触发PropertyChanged事件
            // 这会间接触发NodeEditorViewModel内部的NodeGraphModified事件
            if (nodes.Any())
            {
                // 通过重新设置第一个节点的X属性来触发PropertyChanged事件
                var firstNode = nodes.First();
                var firstNodeX = firstNode.X;
                firstNode.X = firstNodeX; // 这会触发PropertyChanged事件
            }
        }

        /// <summary>
        /// 执行拓扑分层，返回按层级组织的节点列表
        /// </summary>
        private List<List<Node>> PerformTopologicalLayering(List<Node> nodes, List<NodeConnection> connections)
        {
            var layers = new List<List<Node>>();
            var remainingNodes = new HashSet<Node>(nodes);
            var processedNodes = new HashSet<Node>();

            while (remainingNodes.Any())
            {
                var currentLayer = new List<Node>();

                // 找到当前层的节点（没有未处理的输入依赖）
                var candidateNodes = remainingNodes.Where(node =>
                {
                    var inputConnections = connections.Where(c => c.InputNode?.Id == node.Id);
                    return inputConnections.All(c => c.OutputNode == null || processedNodes.Contains(c.OutputNode));
                }).ToList();

                // 如果没有候选节点，说明存在循环依赖，选择剩余节点中的第一个
                if (!candidateNodes.Any() && remainingNodes.Any())
                {
                    candidateNodes.Add(remainingNodes.First());
                }

                // 将候选节点添加到当前层
                foreach (var node in candidateNodes)
                {
                    currentLayer.Add(node);
                    remainingNodes.Remove(node);
                    processedNodes.Add(node);
                }

                if (currentLayer.Any())
                {
                    layers.Add(currentLayer);
                }
            }

            return layers;
        }

        /// <summary>
        /// 获取节点列表中的最大宽度
        /// </summary>
        private double GetMaxNodeWidth(List<Node> nodes)
        {
            return nodes.Any() ? nodes.Max(n => n.Width) : 120; // 默认宽度120
        }

        /// <summary>
        /// 执行密集节点整理
        /// </summary>
        private void ExecuteArrangeNodesDense()
        {
            try
            {
                if (_nodeEditor?.Nodes == null || !_nodeEditor.Nodes.Any())
                {
                    TaskStatus = "没有节点需要整理";
                    return;
                }

                TaskStatus = "正在执行密集整理...";

                // 执行密集节点整理算法
                ArrangeNodesDenseAlgorithm();

                TaskStatus = $"已密集整理 {_nodeEditor.Nodes.Count} 个节点";
            }
            catch (Exception ex)
            {
                TaskStatus = $"密集整理失败: {ex.Message}";
            }
        }

        /// <summary>
        /// 密集节点整理算法 - 极致紧凑的布局
        /// </summary>
        private void ArrangeNodesDenseAlgorithm()
        {
            if (_nodeEditor?.Nodes == null || _nodeEditor.Connections == null)
                return;

            var nodes = _nodeEditor.Nodes.ToList();
            var connections = _nodeEditor.Connections.ToList();

            // 1. 更新所有节点的高度以适应端口数量
            foreach (var node in nodes)
            {
                node.UpdateNodeHeight();
            }

            // 2. 使用拓扑排序确定节点层级
            var layers = PerformTopologicalLayering(nodes, connections);

            // 3. 密集布局参数 - 极致紧凑
            const double layerSpacing = 80;   // 层间距极度减少到80
            const double nodeSpacing = 5;    // 节点间距极度减少到5
            const double startX = 10;        // 起始X位置极度减少
            const double startY = 10;        // 起始Y位置极度减少

            // 4. 为每一层布局节点 - 使用极致紧凑算法
            double currentX = startX;

            for (int layerIndex = 0; layerIndex < layers.Count; layerIndex++)
            {
                var layer = layers[layerIndex];
                if (!layer.Any()) continue;

                // 按连接数量和高度排序，优化垂直空间利用
                var sortedLayer = layer.OrderBy(n => GetNodeConnectionCount(n, connections))
                                      .ThenBy(n => n.Height).ToList();

                // 使用极致紧凑的垂直打包
                var packedPositions = PackNodesUltraCompact(sortedLayer, nodeSpacing, startY);

                // 设置节点位置
                for (int i = 0; i < sortedLayer.Count; i++)
                {
                    var node = sortedLayer[i];
                    node.X = currentX;
                    node.Y = packedPositions[i];
                }

                // 移动到下一层，使用极致紧凑的宽度计算
                // 进一步减少层间距，几乎贴合
                var layerWidth = GetUltraCompactWidth(layer);
                currentX += layerWidth + Math.Min(layerSpacing, layerWidth * 0.3); // 层间距不超过节点宽度的30%
            }

            // 后处理：微调重叠节点
            ResolveNodeOverlaps(nodes, nodeSpacing);

            // 通知节点图已修改
            if (nodes.Any())
            {
                var firstNode = nodes.First();
                var firstNodeX = firstNode.X;
                firstNode.X = firstNodeX; // 触发PropertyChanged事件
            }
        }

        /// <summary>
        /// 获取节点的连接数量（输入+输出）
        /// </summary>
        private int GetNodeConnectionCount(Node node, List<NodeConnection> connections)
        {
            var inputCount = connections.Count(c => c.InputNode?.Id == node.Id);
            var outputCount = connections.Count(c => c.OutputNode?.Id == node.Id);
            return inputCount + outputCount;
        }

        /// <summary>
        /// 极致紧凑的垂直打包节点，返回每个节点的Y坐标
        /// </summary>
        private List<double> PackNodesUltraCompact(List<Node> nodes, double spacing, double startY)
        {
            var positions = new List<double>();
            double currentY = startY;

            foreach (var node in nodes)
            {
                positions.Add(currentY);
                // 使用更小的间距，几乎贴合
                currentY += node.Height + Math.Max(spacing, 2); // 最小间距2像素
            }

            return positions;
        }

        /// <summary>
        /// 获取节点层的极致紧凑宽度
        /// </summary>
        private double GetUltraCompactWidth(List<Node> nodes)
        {
            if (!nodes.Any()) return 80; // 最小宽度80

            // 使用节点的实际宽度，但进一步压缩
            var maxWidth = nodes.Max(n => n.Width);

            // 对于密集布局，我们可以使用更紧凑的宽度
            // 假设节点的实际内容宽度比显示宽度小一些
            return Math.Max(maxWidth * 0.85, 80); // 压缩到85%，最小80
        }

        /// <summary>
        /// 解决节点重叠问题 - 极致紧凑版本
        /// </summary>
        private void ResolveNodeOverlaps(List<Node> nodes, double minSpacing)
        {
            // 按Y坐标排序
            var sortedNodes = nodes.OrderBy(n => n.Y).ToList();

            for (int i = 1; i < sortedNodes.Count; i++)
            {
                var currentNode = sortedNodes[i];
                var previousNode = sortedNodes[i - 1];

                // 检查是否在同一列（X坐标相近），使用更小的阈值
                if (Math.Abs(currentNode.X - previousNode.X) < 30)
                {
                    // 使用极小的间距，几乎贴合
                    var ultraMinSpacing = Math.Max(minSpacing, 1);
                    var requiredY = previousNode.Y + previousNode.Height + ultraMinSpacing;
                    if (currentNode.Y < requiredY)
                    {
                        currentNode.Y = requiredY;
                    }
                }
            }
        }

        /// <summary>
        /// 执行显示节点状态（开关模式）
        /// </summary>
        private void ExecuteShowNodeStatus()
        {
            try
            {
                if (_nodeEditor?.Nodes == null || !_nodeEditor.Nodes.Any())
                {
                    TaskStatus = "没有节点可显示状态";
                    return;
                }

                // 切换显示状态
                IsNodeStatusVisible = !IsNodeStatusVisible;

                if (IsNodeStatusVisible)
                {
                    var selectedNode = _nodeEditor.SelectedNode;
                    if (selectedNode != null)
                    {
                        var nodesToProcess = _nodeEditor.Nodes.Count(n => n.ToBeProcessed);
                        TaskStatus = $"节点状态已开启 - 实时跟踪从 '{selectedNode.Title}' 开始的处理链 (绿色: {nodesToProcess}个待处理, 灰色: {_nodeEditor.Nodes.Count - nodesToProcess}个无需处理)";
                    }
                    else
                    {
                        var nodesToProcess = _nodeEditor.Nodes.Count(n => n.ToBeProcessed);
                        TaskStatus = $"节点状态已开启 - 实时显示处理状态 (绿色: {nodesToProcess}个待处理, 灰色: {_nodeEditor.Nodes.Count - nodesToProcess}个无需处理)";
                    }
                }
                else
                {
                    TaskStatus = "节点状态已关闭";
                }
            }
            catch (Exception ex)
            {
                TaskStatus = $"切换节点状态失败: {ex.Message}";
            }
        }

        /// <summary>
        /// 更新节点状态可见性
        /// </summary>
        private void UpdateNodeStatusVisibility()
        {
            if (_nodeEditor?.Nodes == null) return;

            foreach (var node in _nodeEditor.Nodes)
            {
                node.ShowStatusIndicator = IsNodeStatusVisible;
            }

            // 如果状态显示开启，同时更新处理标记
            if (IsNodeStatusVisible)
            {
                UpdateProcessingMarksFromSelectedNode();
            }
        }

        /// <summary>
        /// 根据选中节点更新处理标记
        /// </summary>
        private void UpdateProcessingMarksFromSelectedNode()
        {
            if (_nodeEditor?.Nodes == null || !_nodeEditor.Nodes.Any()) return;

            try
            {
                // 确保在UI线程中执行
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    var selectedNode = _nodeEditor.SelectedNode;
                    if (selectedNode != null)
                    {
                        // 创建当前节点图
                        var nodeGraph = _nodeEditor.CreateNodeGraph();

                        // 清除所有节点的处理标记
                        nodeGraph.ClearAllProcessingFlags();

                        // 标记选中节点及其下游节点需要处理
                        selectedNode.MarkDownstreamForProcessing(nodeGraph);

                        // 强制触发UI更新 - 直接设置属性会自动触发PropertyChanged
                        // 不需要手动调用OnPropertyChanged，因为ToBeProcessed属性的setter已经处理了
                    }
                });
            }
            catch (Exception)
            {
                // 静默处理异常，不中断程序
            }
        }

        /// <summary>
        /// 连接变化事件处理
        /// </summary>
        private void OnConnectionsChanged()
        {
            try
            {
                // 如果节点状态显示开启，实时更新处理标记
                if (IsNodeStatusVisible)
                {
                    // 延迟一点执行，确保连接变化已完成
                    System.Windows.Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                    {
                        UpdateProcessingMarksFromSelectedNode();
                    }), System.Windows.Threading.DispatcherPriority.Background);
                }
            }
            catch (Exception)
            {
                // 静默处理异常
            }
        }

        /// <summary>
        /// 执行垂直整理
        /// </summary>
        private void ExecuteArrangeNodesVertical()
        {
            try
            {
                if (_nodeEditor?.Nodes == null || !_nodeEditor.Nodes.Any())
                {
                    TaskStatus = "没有节点需要整理";
                    return;
                }

                TaskStatus = "正在执行垂直整理...";

                // 执行垂直节点整理算法
                ArrangeNodesVerticalAlgorithm();

                TaskStatus = $"已垂直整理 {_nodeEditor.Nodes.Count} 个节点";
            }
            catch (Exception ex)
            {
                TaskStatus = $"垂直整理失败: {ex.Message}";
            }
        }

        /// <summary>
        /// 垂直节点整理算法 - 基于拓扑排序的垂直布局
        /// </summary>
        private void ArrangeNodesVerticalAlgorithm()
        {
            if (_nodeEditor?.Nodes == null || _nodeEditor.Connections == null)
                return;

            var nodes = _nodeEditor.Nodes.ToList();
            var connections = _nodeEditor.Connections.ToList();

            // 1. 更新所有节点的高度以适应端口数量
            foreach (var node in nodes)
            {
                node.UpdateNodeHeight();
            }

            // 2. 使用拓扑排序确定节点层级（列）
            var layers = PerformTopologicalLayering(nodes, connections);

            // 3. 布局参数（垂直方向为主）
            const double rowSpacing = 200;   // 行间距（层间距）
            const double nodeSpacing = 20;   // 节点间水平间距
            const double startX = 50;        // 起始X位置
            const double startY = 50;        // 起始Y位置

            // 4. 为每一层布局节点（按行）
            double currentY = startY;

            for (int layerIndex = 0; layerIndex < layers.Count; layerIndex++)
            {
                var layer = layers[layerIndex];
                if (!layer.Any()) continue;

                // 按连接数量和宽度排序，优化水平空间利用
                var sortedLayer = layer.OrderBy(n => GetNodeConnectionCount(n, connections))
                                       .ThenBy(n => n.Width).ToList();

                // 计算当前行的X位置
                double currentX = startX;

                // 布局当前层的节点
                foreach (var node in sortedLayer)
                {
                    node.X = currentX;
                    node.Y = currentY;

                    currentX += node.Width + nodeSpacing;
                }

                // 移动到下一行，使用当前层中最高节点的高度
                currentY += GetMaxNodeHeight(layer) + rowSpacing;
            }

            // 5. 通知节点图已修改
            if (nodes.Any())
            {
                var firstNode = nodes.First();
                var firstNodeX = firstNode.X;
                firstNode.X = firstNodeX; // 触发PropertyChanged
            }
        }

        /// <summary>
        /// 获取节点列表中的最大高度
        /// </summary>
        private double GetMaxNodeHeight(List<Node> nodes)
        {
            return nodes.Any() ? nodes.Max(n => n.Height) : 80; // 默认高度80
        }

        /// <summary>
        /// 执行近似正方形网格整理
        /// </summary>
        private void ExecuteArrangeNodesBalanced()
        {
            try
            {
                if (_nodeEditor?.Nodes == null || !_nodeEditor.Nodes.Any())
                {
                    TaskStatus = "没有节点需要整理";
                    return;
                }

                TaskStatus = "正在执行网格整理...";

                // 执行网格布局算法
                ArrangeNodesBalancedAlgorithm();

                TaskStatus = $"已网格整理 {_nodeEditor.Nodes.Count} 个节点";
            }
            catch (Exception ex)
            {
                TaskStatus = $"网格整理失败: {ex.Message}";
            }
        }

        /// <summary>
        /// 近似正方形网格节点整理算法
        /// 1. 使用拓扑排序获取节点顺序，保证依赖方向大致从左到右、从上到下
        /// 2. 根据节点数量计算接近正方形的行列数（列 = ceil(sqrt(N)))
        /// 3. 第一遍扫描确定各列最大宽度、各行最大高度
        /// 4. 第二遍设置节点坐标，实现紧凑的网格布局
        /// </summary>
        private void ArrangeNodesBalancedAlgorithm()
        {
            if (_nodeEditor?.Nodes == null || _nodeEditor.Connections == null)
                return;

            var nodes = _nodeEditor.Nodes.ToList();
            var connections = _nodeEditor.Connections.ToList();

            // 1. 更新节点高度以适应端口数量
            foreach (var n in nodes) n.UpdateNodeHeight();

            // 2. 基于依赖关系生成层（列）
            var layers = PerformTopologicalLayering(nodes, connections);

            // 3. 对层进行交叉最小化排序
            OptimizeLayerOrdering(layers, connections, 3);

            // 4. 布局参数
            const double layerSpacing = 180;  // 列间距
            const double nodeSpacing = 40;    // 同列节点垂直间距
            const double startX = 50;
            const double startY = 50;

            // 5. 为每一列布局节点
            double currentX = startX;
            foreach (var layer in layers)
            {
                if (!layer.Any()) { currentX += layerSpacing; continue; }

                // 计算当前列中节点的起始Y
                double currentY = startY;

                foreach (var node in layer)
                {
                    node.X = currentX;
                    node.Y = currentY;
                    currentY += node.Height + nodeSpacing;
                }

                currentX += GetMaxNodeWidth(layer) + layerSpacing;
            }

            // 通知修改
            if (nodes.Any())
            {
                var first = nodes.First();
                first.X = first.X;
            }
        }

        /// <summary>
        /// 使用Barycenter启发式优化层节点顺序，减少连接交叉
        /// </summary>
        private void OptimizeLayerOrdering(List<List<Node>> layers, List<NodeConnection> connections, int iterations)
        {
            // 创建快捷索引字典
            var nodeToLayer = new Dictionary<int, int>();
            for (int i = 0; i < layers.Count; i++)
            {
                foreach (var n in layers[i]) nodeToLayer[n.Id] = i;
            }

            // 邻接字典
            Dictionary<int, List<Node>> predecessors = new();
            Dictionary<int, List<Node>> successors = new();

            foreach (var c in connections)
            {
                if (c.OutputNode == null || c.InputNode == null) continue;

                if (!successors.TryGetValue(c.OutputNode.Id, out var listS))
                {
                    listS = new List<Node>(); successors[c.OutputNode.Id] = listS;
                }
                listS.Add(c.InputNode);

                if (!predecessors.TryGetValue(c.InputNode.Id, out var listP))
                {
                    listP = new List<Node>(); predecessors[c.InputNode.Id] = listP;
                }
                listP.Add(c.OutputNode);
            }

            for (int iter = 0; iter < iterations; iter++)
            {
                // Downward pass (top→bottom layers)
                for (int i = 1; i < layers.Count; i++)
                {
                    SortLayerByBarycenter(layers[i], predecessors, layers[i - 1]);
                }

                // Upward pass (bottom→top layers)
                for (int i = layers.Count - 2; i >= 0; i--)
                {
                    SortLayerByBarycenter(layers[i], successors, layers[i + 1]);
                }
            }
        }

        /// <summary>
        /// 对单层节点按其与邻接层的连接重心排序
        /// </summary>
        private void SortLayerByBarycenter(List<Node> layer, Dictionary<int, List<Node>> neighborMap, List<Node> referenceLayer)
        {
            // 建立参考层中节点索引
            var refIndex = new Dictionary<int, int>();
            for (int i = 0; i < referenceLayer.Count; i++) refIndex[referenceLayer[i].Id] = i;

            layer.Sort((a, b) =>
            {
                double aCenter = ComputeBarycenter(a, neighborMap, refIndex);
                double bCenter = ComputeBarycenter(b, neighborMap, refIndex);
                return aCenter.CompareTo(bCenter);
            });
        }

        private double ComputeBarycenter(Node node, Dictionary<int, List<Node>> neighborMap, Dictionary<int, int> refIndex)
        {
            if (!neighborMap.TryGetValue(node.Id, out var neighbors) || neighbors.Count == 0)
                return double.MaxValue; // keep at end if no neighbors

            double sum = 0;
            int count = 0;
            foreach (var n in neighbors)
            {
                if (refIndex.TryGetValue(n.Id, out var idx))
                {
                    sum += idx;
                    count++;
                }
            }
            return count > 0 ? sum / count : double.MaxValue;
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

                    // 如果节点状态显示开启，实时更新处理标记
                    if (IsNodeStatusVisible)
                    {
                        UpdateProcessingMarksFromSelectedNode();
                    }
                }
            };

            // 监听连接错误事件
            nodeEditor.ConnectionErrorDetected += OnConnectionErrorDetected;

            // 监听连接变化事件
            nodeEditor.ConnectionsChanged += OnConnectionsChanged;

            // 更新节点数量
            NodeCount = nodeEditor.Nodes.Count;

        }

        #endregion
    }
}
