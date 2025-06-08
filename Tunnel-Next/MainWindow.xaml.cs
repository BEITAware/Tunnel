using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Tunnel_Next.ViewModels;
using Tunnel_Next.Models;
using Tunnel_Next.Services.Scripting;
using Tunnel_Next.Services;

namespace Tunnel_Next
{
    /// <summary>
    /// Tunnel主窗口
    /// </summary>
    public partial class MainWindow : Window
    {
        private MainViewModel _viewModel;
        private RevivalScriptManager? _revivalScriptManager;
        private INodeMenuService? _nodeMenuService;
        private DocumentFactory _documentFactory;
        private readonly DocumentManagerService _documentManager;
        private NodeEditorViewModel? _currentBoundNodeEditor;

        // 公开DocumentManager供其他组件访问
        public DocumentManagerService DocumentManager => _documentManager;

        /// <summary>
        /// 获取RevivalScriptManager实例
        /// </summary>
        public RevivalScriptManager? GetRevivalScriptManager()
        {
            return _revivalScriptManager;
        }

        public MainWindow()
        {
            InitializeComponent();

            // 注意：不再初始化传统脚本管理器，只使用Revival Scripts
            // Revival Scripts管理器将在InitializeScriptSystem中初始化

            // 初始化ViewModel（暂时传入null，稍后会设置Revival Scripts管理器）
            _viewModel = new MainViewModel(null, false); // 传入false表示延迟初始化
            DataContext = _viewModel;

            // 初始化文档工厂（暂时传入null，稍后会在InitializeScriptSystem中更新）
            _documentFactory = new DocumentFactory(_viewModel.FileService, null);

            // 初始化文档管理器
            _documentManager = new DocumentManagerService(_documentFactory);
            _viewModel.DocumentManager = _documentManager;

            // 绑定文档管理器事件
            _documentManager.NewDocumentRequested += DocumentManager_NewDocumentRequested;
            _documentManager.OpenDocumentRequested += DocumentManager_OpenDocumentRequested;
            _documentManager.SaveDocumentRequested += DocumentManager_SaveDocumentRequested;
            _documentManager.ActiveDocumentChanged += DocumentManager_ActiveDocumentChanged;

            // 在窗口加载完成后设置ImageProcessor服务
            Loaded += MainWindow_Loaded;

            // 初始化UI（基础UI，不涉及节点图）
            InitializeBasicUI();

            // 绑定事件
            BindEvents();

            // 异步初始化脚本系统（这是关键修改）
            _ = InitializeScriptSystemAsync();

        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // 设置NodeEditorControl的ImageProcessor服务
            // 不再直接设置ImageProcessor，使用MVVM解耦架构

            // 设置NodeEditorControl的NodeMenuService服务（如果已初始化）
            if (_nodeMenuService != null)
            {
                NodeEditorControl.NodeMenuService = _nodeMenuService;
            }
        }

        private async Task InitializeScriptSystemAsync()
        {
            try
            {
                _viewModel.TaskStatus = "正在初始化工作文件夹...";

                // 初始化工作文件夹和Revival Scripts管理器
                var workFolderService = new WorkFolderService();
                await workFolderService.InitializeAsync();
                _revivalScriptManager = new RevivalScriptManager(
                    workFolderService.UserScriptsFolder,
                    workFolderService.UserResourcesFolder);

                _viewModel.TaskStatus = "正在扫描Revival Scripts...";

                // 扫描Revival Scripts
                _revivalScriptManager.ScanRevivalScripts();

                _viewModel.TaskStatus = "正在创建脚本资源文件夹...";

                // 确保所有脚本的资源文件夹存在
                _revivalScriptManager.EnsureAllScriptResourceFolders();

                _viewModel.TaskStatus = "正在编译Revival Scripts...";

                // 编译Revival Scripts
                await Task.Run(() => _revivalScriptManager.CompileRevivalScripts());

                // 在UI线程上完成初始化
                await Dispatcher.InvokeAsync(async () =>
                {
                    await CompleteInitializationOnUIThread();
                });

                _viewModel.TaskStatus = "Revival Scripts系统初始化完成";
            }
            catch (Exception ex)
            {
                await Dispatcher.InvokeAsync(() =>
                {
                    _viewModel.TaskStatus = $"Revival Scripts系统初始化失败: {ex.Message}";
                    MessageBox.Show($"Revival Scripts系统初始化失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                });
            }
        }

        private async Task CompleteInitializationOnUIThread()
        {
            try
            {

                // 初始化节点菜单服务（使用Revival Scripts管理器）
                _nodeMenuService = new RevivalNodeMenuService(_revivalScriptManager);

                // 设置NodeEditorControl的NodeMenuService服务
                if (NodeEditorControl != null)
                {
                    NodeEditorControl.NodeMenuService = _nodeMenuService;
                }

                // 更新现有ViewModel的RevivalScriptManager
                _viewModel.FileService.UpdateScriptManager(_revivalScriptManager);
                _viewModel.NodeEditor.UpdateRevivalScriptManager(_revivalScriptManager);

                // 更新DocumentFactory以使用RevivalScriptManager
                _documentFactory = new DocumentFactory(_viewModel.FileService, _revivalScriptManager);
                _documentManager.UpdateDocumentFactory(_documentFactory);

                // 现在初始化集合（此时RevivalScriptManager已经可用）
                await _viewModel.InitializeCollectionsAsync();

                // 完整初始化UI（包括节点图相关的UI）
                await InitializeCompleteUI();

            }
            catch (Exception ex)
            {
                throw;
            }
        }

        private void InitializeBasicUI()
        {
            // 设置初始状态（不涉及节点图的基础UI）
            TaskLabel.Text = _viewModel.TaskStatus;
            ZoomLabel.Text = _viewModel.ZoomLevelText;
            NodeCountLabel.Text = _viewModel.NodeCountText;

        }

        private async Task InitializeCompleteUI()
        {
            // 设置完整状态（包括节点图相关的UI）
            TaskLabel.Text = _viewModel.TaskStatus;
            ZoomLabel.Text = _viewModel.ZoomLevelText;
            NodeCountLabel.Text = _viewModel.NodeCountText;

            // 初始化参数编辑器的DataContext（使用默认的NodeEditor）
            ParameterEditorControl.DataContext = _viewModel.NodeEditor;

            // 重新绑定事件（因为ViewModel已经重新创建）
            BindEvents();

        }

        private void InitializeUI()
        {
            // 兼容性方法，调用完整UI初始化
            _ = InitializeCompleteUI();
        }

        private void BindEvents()
        {
            // 绑定ViewModel属性变化事件
            _viewModel.PropertyChanged += (s, e) =>
            {
                switch (e.PropertyName)
                {
                    case nameof(_viewModel.TaskStatus):
                        TaskLabel.Text = _viewModel.TaskStatus;
                        break;
                    case nameof(_viewModel.ZoomLevelText):
                        ZoomLabel.Text = _viewModel.ZoomLevelText;
                        break;
                    case nameof(_viewModel.NodeCountText):
                        NodeCountLabel.Text = _viewModel.NodeCountText;
                        break;
                }
            };

            // 绑定文档管理器事件
            _documentManager.NewDocumentRequested += DocumentManager_NewDocumentRequested;
            _documentManager.OpenDocumentRequested += DocumentManager_OpenDocumentRequested;
            _documentManager.SaveDocumentRequested += DocumentManager_SaveDocumentRequested;
            _documentManager.ActiveDocumentChanged += DocumentManager_ActiveDocumentChanged;

            // 绑定当前节点编辑器事件
            RebindNodeEditorEvents(_viewModel.NodeEditor);

            // 绑定缩放事件
            _viewModel.ZoomFitRequested += () => ZoomFit();
            _viewModel.ZoomInRequested += () => ZoomIn();
            _viewModel.ZoomOutRequested += () => ZoomOut();

            // 绑定节点菜单事件
            _viewModel.ShowNodeMenuRequested += OnShowNodeMenuRequested;
        }

        private void ZoomFit()
        {
            var activeDocument = _documentManager.ActiveDocument as NodeGraphDocument;
            activeDocument?.ImagePreview?.ZoomFit();
        }

        private void ZoomIn()
        {
            var activeDocument = _documentManager.ActiveDocument as NodeGraphDocument;
            activeDocument?.ImagePreview?.ZoomIn();
        }

        private void ZoomOut()
        {
            var activeDocument = _documentManager.ActiveDocument as NodeGraphDocument;
            activeDocument?.ImagePreview?.ZoomOut();
        }

        /// <summary>
        /// 显示节点菜单请求处理
        /// </summary>
        private void OnShowNodeMenuRequested(object? sender)
        {
            // 暂时注释掉，因为需要重新实现
            // _nodeMenuService.ShowNodeAddMenu(AddNodeButton, OnNodeSelected);
        }

        #region DocumentManager事件处理

        private void DocumentManager_NewDocumentRequested(object? sender, EventArgs e)
        {
            try
            {
                _viewModel.TaskStatus = "正在创建新文档...";

                // 创建新节点图文档
                var document = _documentManager.CreateNodeGraphDocument();

                _viewModel.TaskStatus = $"创建新文档成功: {document.Title}";
            }
            catch (Exception ex)
            {
                _viewModel.TaskStatus = $"创建新文档失败: {ex.Message}";
            }
        }

        private async void DocumentManager_OpenDocumentRequested(object? sender, EventArgs e)
        {
            try
            {
                // 使用文件对话框选择节点图文件
                var openDialog = new Microsoft.Win32.OpenFileDialog
                {
                    Filter = "节点图文件 (*.json)|*.json|所有文件 (*.*)|*.*",
                    Title = "打开节点图",
                    CheckFileExists = true,
                    InitialDirectory = _viewModel.FileService?.GetDefaultNodeGraphsFolder() ?? Environment.CurrentDirectory
                };

                if (openDialog.ShowDialog() == true)
                {
                    _viewModel.TaskStatus = $"正在打开文档: {openDialog.FileName}";

                    var document = await _documentManager.LoadNodeGraphDocumentAsync(openDialog.FileName);

                    _viewModel.TaskStatus = $"打开文档成功: {document.Title}";
                }
            }
            catch (Exception ex)
            {
                _viewModel.TaskStatus = $"打开文档失败: {ex.Message}";
            }
        }

        private void DocumentManager_SaveDocumentRequested(object? sender, SaveDocumentEventArgs e)
        {
            try
            {
                var document = _documentManager.GetDocument(e.DocumentId);
                if (document == null)
                {
                    _viewModel.TaskStatus = "保存失败: 未找到文档";
                    return;
                }

                string? filePath = null;
                if (e.SaveAs || string.IsNullOrEmpty(document.FilePath))
                {
                    var saveDialog = new Microsoft.Win32.SaveFileDialog
                    {
                        Filter = "节点图文件 (*.json)|*.json|所有文件 (*.*)|*.*",
                        Title = "保存节点图",
                        FileName = document.Title
                    };

                    if (saveDialog.ShowDialog() == true)
                    {
                        filePath = saveDialog.FileName;
                    }
                    else
                    {
                        // 用户取消操作
                        return;
                    }
                }

                _viewModel.TaskStatus = $"正在保存文档: {document.Title}";

                if (_documentManager.SaveDocument(e.DocumentId, filePath))
                {
                    _viewModel.TaskStatus = $"保存文档成功: {document.Title}";
                }
                else
                {
                    _viewModel.TaskStatus = $"保存文档失败: {document.Title}";
                }
            }
            catch (Exception ex)
            {
                _viewModel.TaskStatus = $"保存文档失败: {ex.Message}";
            }
        }

        private void DocumentManager_ActiveDocumentChanged(object? sender, DocumentChangedEventArgs e)
        {
            try
            {
                if (e.NewDocument is NodeGraphDocument nodeGraphDoc)
                {
                    // 更新UI状态
                    _viewModel.TaskStatus = $"当前文档: {nodeGraphDoc.Title}";

                    // 更新MainViewModel的当前节点图和节点编辑器
                    _viewModel.CurrentNodeGraph = nodeGraphDoc.NodeGraph;
                    _viewModel.SetActiveNodeEditor(nodeGraphDoc.NodeEditor);

                    // 绑定到节点编辑器控件
                    NodeEditorControl.DataContext = nodeGraphDoc.NodeEditor;

                    // 绑定到参数编辑器控件
                    ParameterEditorControl.DataContext = nodeGraphDoc.NodeEditor;

                    // 更新节点编辑器相关事件
                    RebindNodeEditorEvents(nodeGraphDoc.NodeEditor);

                    // 关键修复：调用文档的OnActivated方法来触发节点图处理
                    nodeGraphDoc.OnActivated();
                }
                else if (e.NewDocument == null)
                {
                    // 没有活动文档，清除当前状态
                    _viewModel.TaskStatus = "无活动文档";

                    // 重新绑定到默认的NodeEditor
                    _viewModel.SetActiveNodeEditor(_viewModel.NodeEditor);
                    NodeEditorControl.DataContext = _viewModel.NodeEditor;
                    ParameterEditorControl.DataContext = _viewModel.NodeEditor;

                    // 恢复默认事件绑定
                    RebindNodeEditorEvents(_viewModel.NodeEditor);
                }
            }
            catch (Exception ex)
            {
                _viewModel.TaskStatus = $"切换活动文档失败: {ex.Message}";
            }
        }

        #endregion

        #region 工具方法

        /// <summary>
        /// 重新绑定节点编辑器事件
        /// </summary>
        private void RebindNodeEditorEvents(NodeEditorViewModel nodeEditor)
        {
            if (nodeEditor == null)
                return;

            // 清除现有事件绑定
            if (_currentBoundNodeEditor != null)
            {
                _currentBoundNodeEditor.PreviewUpdateRequested -= UpdateImagePreview;
                _currentBoundNodeEditor.ProcessingStateChanged -= OnProcessingStateChanged;
                _currentBoundNodeEditor.ClearUIRequested -= OnClearUIRequested;
            }

            // 重新绑定节点编辑器事件
            if (nodeEditor != null)
            {
                nodeEditor.PreviewUpdateRequested += UpdateImagePreview;
                nodeEditor.ProcessingStateChanged += OnProcessingStateChanged;
                nodeEditor.ClearUIRequested += OnClearUIRequested;
            }

            // 更新当前绑定的NodeEditor引用
            _currentBoundNodeEditor = nodeEditor;
        }

        /// <summary>
        /// 处理状态变化事件
        /// </summary>
        private void OnProcessingStateChanged(bool isProcessing)
        {
            Controls.ImagePreviewControl.SetProcessingState(isProcessing);
        }

        /// <summary>
        /// 清除UI控件事件
        /// </summary>
        private void OnClearUIRequested()
        {
            NodeEditorControl.ClearAllNodeControls();
        }

        #endregion

        #region Event Handlers

        private void UpdateImagePreview()
        {
            try
            {

                // 获取当前活动的文档
                var activeDocument = _documentManager.GetNodeGraphDocument(_documentManager.ActiveDocumentId);
                if (activeDocument == null)
                {
                    return;
                }


                if (activeDocument.ImagePreview == null)
                {
                    // 强制初始化UI控件
                    var contentControl = activeDocument.GetContentControl();

                    if (activeDocument.ImagePreview == null)
                    {
                        return;
                    }
                }

                var nodeEditor = activeDocument.NodeEditor;
                if (nodeEditor == null)
                {
                    activeDocument.ImagePreview.ClearImage(); // 清除预览以防显示旧内容
                    return;
                }


                // 设置预览控制为非处理状态（避免界面卡顿）
                Controls.ImagePreviewControl.SetProcessingState(false);

                // 列出所有节点及其输出的调试信息
                foreach (var node in nodeEditor.Nodes)
                {
                    foreach (var output in node.ProcessedOutputs)
                    {
                        string outputValueTypeName = output.Value?.GetType().Name ?? "null";
                        if (output.Value is OpenCvSharp.Mat mat)
                        {
                            // 安全检查: 访问Mat属性前确保其有效
                            if (mat != null && !mat.IsDisposed && !mat.Empty())
                            {
                                try
                                {
                                }
                                catch (Exception ex)
                                {
                                }
                            }
                            else
                            {
                            }
                        }
                    }
                }

                // 只显示预览节点的输出
                var previewNodes = nodeEditor.Nodes.Where(n => n.Title == "预览节点").ToList();
                Node? previewNodeToUse = null;

                if (previewNodes.Any())
                {
                    // 优先查找有有效输入连接且已处理的预览节点
                    previewNodeToUse = previewNodes.FirstOrDefault(pn =>
                                            pn.IsProcessed &&
                                            pn.InputPorts.Any(ip => nodeEditor.Connections.Any(c => c.InputNode == pn && c.InputPortName == ip.Name && c.OutputNode != null))
                                        );
                    if (previewNodeToUse != null)
                    {
                    }
                    else
                    {
                        // 否则，使用第一个已处理的预览节点
                        previewNodeToUse = previewNodes.FirstOrDefault(pn => pn.IsProcessed);
                        if (previewNodeToUse != null)
                        {
                        }
                        else
                        {
                             // 最后，使用第一个预览节点（无论是否处理）
                            previewNodeToUse = previewNodes.FirstOrDefault();
                            if (previewNodeToUse != null)
                            {
                            }
                        }
                    }
                }

                if (previewNodeToUse != null)
                {
                    if (previewNodeToUse.ProcessedOutputs.TryGetValue("f32bmp", out var previewOutputObject))
                    {
                        if (previewOutputObject is OpenCvSharp.Mat previewMat)
                        {
                            // 安全检查: 使用Mat对象前确保其有效
                            if (previewMat != null && !previewMat.IsDisposed && !previewMat.Empty())
                            {
                                try
                                {
                                    activeDocument.ImagePreview.ImageSource = previewMat; // 设置图像源
                                    return; // 成功设置图像，方法结束
                                }
                                catch (Exception ex)
                                {
                                    activeDocument.ImagePreview.ClearImage(); // 出错时清除图像
                                }
                            }
                            else
                            {
                                // 不清除图像，保持上一次的预览
                            }
                        }
                        else
                        {
                             // 不清除预览，保持上一次的图像
                        }
                    }
                    else
                    {
                        // 不清除预览，保持上一次的图像
                    }
                }
                else
                {
                }

                // 如果没有有效的预览节点或其输出，不清空图像预览（保持上一次的图像）
                // 不再调用 ClearImage()，让用户看到上一次的预览
            }
            catch (Exception ex)
            {

                // 尝试清空图像预览以避免显示损坏的内容
                try
                {
                    var activeDoc = _documentManager.GetNodeGraphDocument(_documentManager.ActiveDocumentId);
                    activeDoc?.ImagePreview?.ClearImage();
                }
                catch (Exception clearEx)
                {
                }
            }
        }

        #region 胶片预览事件

        private async void FilmPreviewControl_FilmItemSelected(object sender, FilmPreviewItem item)
        {
            try
            {
                _viewModel.TaskStatus = $"选择胶片: {item.Name}";
            }
            catch (Exception ex)
            {
                _viewModel.TaskStatus = $"选择胶片失败: {ex.Message}";
            }
        }

        private async void FilmPreviewControl_FilmItemDoubleClicked(object sender, FilmPreviewItem item)
        {
            try
            {
                // 1. 先保存当前活动文档（如果有修改且有文件路径）
                var currentDocumentId = _documentManager.ActiveDocumentId;
                if (currentDocumentId != null)
                {
                    var currentDocument = _documentManager.GetNodeGraphDocument(currentDocumentId);
                    if (currentDocument != null && currentDocument.IsModified && !string.IsNullOrEmpty(currentDocument.FilePath))
                    {
                        try
                        {
                            var saveSuccess = currentDocument.Save();
                            if (saveSuccess)
                            {
                            }
                            else
                            {
                            }
                        }
                        catch (Exception saveEx)
                        {
                        }
                    }
                }

                // 2. 打开新的节点图文档
                _viewModel.TaskStatus = $"正在打开文档: {item.FilePath}";
                var document = await _documentManager.LoadNodeGraphDocumentAsync(item.FilePath);
                _viewModel.TaskStatus = $"已打开节点图文档: {document.Title}";
            }
            catch (Exception ex)
            {
                _viewModel.TaskStatus = $"打开节点图失败: {ex.Message}";
            }
        }

        private async void FilmPreviewControl_RefreshRequested(object sender, EventArgs e)
        {
            try
            {
                await _viewModel.UpdateFilmPreviewAsync();
                _viewModel.TaskStatus = "胶片预览刷新";
            }
            catch (Exception ex)
            {
                _viewModel.TaskStatus = $"刷新胶片预览失败: {ex.Message}";
            }
        }

        #endregion

        #region 资源库事件

        private void ResourceLibraryControl_ResourceItemSelected(object sender, ResourceLibraryItem item)
        {
            try
            {
                _viewModel.TaskStatus = $"选择资源: {item.Name}";
            }
            catch (Exception ex)
            {
                _viewModel.TaskStatus = $"选择资源失败: {ex.Message}";
            }
        }

        private async void ResourceLibraryControl_ResourceItemDoubleClicked(object sender, ResourceLibraryItem item)
        {
            try
            {
                if (item.ItemType == ResourceItemType.Image)
                {
                    // 将图片资源添加到节点图
                    _viewModel.CurrentImagePath = item.FilePath;
                    _viewModel.TaskStatus = $"已选择图片: {item.Name}";

                    // 将图片资源添加到节点
                    var position = new System.Windows.Point(50, 50);
                    _viewModel.NodeEditor.SetPendingNodePosition(position);
                    _viewModel.NodeEditor.AddSpecificNodeCommand?.Execute("图像输入");

                    // 将文件路径参数添加到节点
                    var lastNode = _viewModel.NodeEditor.Nodes.LastOrDefault();
                    if (lastNode != null)
                    {
                        bool parameterSet = false;

                        // 方法1：通过ViewModel设置参数（最佳方法）
                        if (lastNode.ViewModel is Tunnel_Next.Services.Scripting.IScriptViewModel scriptViewModel)
                        {
                            try
                            {
                                // 使用反射设置ViewModel的ImagePath属性
                                var viewModelType = scriptViewModel.GetType();
                                var imagePathProperty = viewModelType.GetProperty("ImagePath");
                                if (imagePathProperty != null && imagePathProperty.CanWrite)
                                {
                                    imagePathProperty.SetValue(scriptViewModel, item.FilePath);
                                    parameterSet = true;
                                    _viewModel.TaskStatus = $"已通过ViewModel设置 {lastNode.Title} 的ImagePath = {item.Name}";
                                }
                            }
                            catch (Exception ex)
                            {
                                _viewModel.TaskStatus = $"通过ViewModel设置参数失败: {ex.Message}";
                            }
                        }

                        // 方法2：直接通过脚本实例设置参数
                        if (!parameterSet && lastNode.Tag is Tunnel_Next.Services.Scripting.IRevivalScript scriptInstance)
                        {
                            try
                            {
                                // 使用反射直接设置脚本实例的ImagePath属性
                                var scriptType = scriptInstance.GetType();
                                var imagePathProperty = scriptType.GetProperty("ImagePath");
                                if (imagePathProperty != null && imagePathProperty.CanWrite)
                                {
                                    imagePathProperty.SetValue(scriptInstance, item.FilePath);

                                    // 触发参数变化事件，确保UI和处理流程同步
                                    if (scriptInstance is Tunnel_Next.Services.Scripting.RevivalScriptBase revivalScript)
                                    {
                                        revivalScript.OnParameterChanged("ImagePath", item.FilePath);
                                    }

                                    parameterSet = true;
                                    _viewModel.TaskStatus = $"已通过脚本实例设置 {lastNode.Title} 的ImagePath = {item.Name}";
                                }
                            }
                            catch (Exception ex)
                            {
                                _viewModel.TaskStatus = $"通过脚本实例设置参数失败: {ex.Message}";
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
                                filePathParam.Value = item.FilePath;
                                parameterSet = true;
                                _viewModel.TaskStatus = $"已通过NodeParameter设置 {lastNode.Title} 的参数 {filePathParam.Name} = {item.Name}";
                            }
                        }

                        // 如果都失败了，显示调试信息
                        if (!parameterSet)
                        {
                            var paramNames = string.Join(", ", lastNode.Parameters.Select(p => p.Name));
                            var scriptType = lastNode.Tag?.GetType().Name ?? "Unknown";
                            _viewModel.TaskStatus = $"警告：节点 {lastNode.Title} (脚本类型: {scriptType}) 参数设置失败。可用参数: {paramNames}";
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _viewModel.TaskStatus = $"使用资源失败: {ex.Message}";
            }
        }

        private async void ResourceLibraryControl_RefreshRequested(object sender, EventArgs e)
        {
            try
            {
                await _viewModel.UpdateResourceLibraryAsync();
                ResourceLibraryControl?.UpdateResourceItems();
                _viewModel.TaskStatus = "资源库刷新";
            }
            catch (Exception ex)
            {
                _viewModel.TaskStatus = $"刷新资源库失败: {ex.Message}";
            }
        }

        private void ResourceLibraryControl_ImportRequested(object sender, EventArgs e)
        {
            try
            {
                // 导入图片资源
                _viewModel.ImportImageCommand?.Execute(null);
            }
            catch (Exception ex)
            {
                _viewModel.TaskStatus = $"导入资源失败: {ex.Message}";
            }
        }

        #endregion

        private void OnNodeSelected(string nodeTypeName)
        {
            // 设置节点位置为节点编辑器视图中央（Tab栏添加）
            if (NodeEditorControl != null)
            {
                var viewportSize = new Size(NodeEditorControl.ActualWidth, NodeEditorControl.ActualHeight);
                _viewModel.NodeEditor.SetPendingNodePositionToCenter(viewportSize);
            }

            // 选择选中的节点
            _viewModel.NodeEditor.AddSpecificNodeCommand?.Execute(nodeTypeName);
            _viewModel.TaskStatus = $"添加了 {nodeTypeName} 节点";
        }

        private void AddNodeButton_Click(object sender, RoutedEventArgs e)
        {
            // 显示添加节点菜单
            if (sender is Button button && _nodeMenuService != null)
            {
                // Tab栏添加节点，使用视图中央位置
                _nodeMenuService.ShowNodeAddMenu(button, OnNodeSelected);
            }
        }

        private void NodeEditorAddNodeButton_Click(object sender, RoutedEventArgs e)
        {
            // 节点编辑器右上角添加节点按钮点击事件
            if (sender is Button button && _nodeMenuService != null)
            {
                // Tab栏添加节点，使用视图中央位置
                _nodeMenuService.ShowNodeAddMenu(button, OnNodeSelected);
            }
        }

        #endregion

        #region Window Events

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);

            // 删除键处理
            if (e.Key == Key.Delete && _viewModel.SelectedNode != null)
            {
                _viewModel.DeleteCommand?.Execute(null);
                e.Handled = true;
            }
            else if (Keyboard.Modifiers == ModifierKeys.Control)
            {
                switch (e.Key)
                {
                    case Key.N:
                        _viewModel.NewNodeGraphCommand?.Execute(null);
                        e.Handled = true;
                        break;
                    case Key.O:
                        _viewModel.OpenNodeGraphCommand?.Execute(null);
                        e.Handled = true;
                        break;
                    case Key.S:
                        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
                            _viewModel.SaveAsNodeGraphCommand?.Execute(null);
                        else
                            _viewModel.SaveNodeGraphCommand?.Execute(null);
                        e.Handled = true;
                        break;
                    case Key.Z:
                        _viewModel.UndoCommand?.Execute(null);
                        e.Handled = true;
                        break;
                    case Key.Y:
                        _viewModel.RedoCommand?.Execute(null);
                        e.Handled = true;
                        break;
                    case Key.C:
                        _viewModel.CopyCommand?.Execute(null);
                        e.Handled = true;
                        break;
                    case Key.V:
                        _viewModel.PasteCommand?.Execute(null);
                        e.Handled = true;
                        break;
                    case Key.A:
                        _viewModel.AddNodeCommand?.Execute(null);
                        e.Handled = true;
                        break;
                    case Key.L:
                        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
                            _viewModel.ArrangeNodesDenseCommand?.Execute(null);
                        else
                            _viewModel.ArrangeNodesCommand?.Execute(null);
                        e.Handled = true;
                        break;
                    case Key.D0:
                        _viewModel.ZoomFitCommand?.Execute(null);
                        e.Handled = true;
                        break;
                    case Key.OemPlus:
                        _viewModel.ZoomInCommand?.Execute(null);
                        e.Handled = true;
                        break;
                    case Key.OemMinus:
                        _viewModel.ZoomOutCommand?.Execute(null);
                        e.Handled = true;
                        break;
                }
            }
            else if (e.Key == Key.F5)
            {
                // F5 处理节点图
                _viewModel.NodeEditor.ProcessNodeGraphCommand?.Execute(null);
                e.Handled = true;
            }
        }

        private bool _isClosing = false;

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            if (!_isClosing)
            {
                // 第一次关闭请求，取消关闭并开始异步保存
                e.Cancel = true;
                _ = HandleApplicationClosingAsync();
            }
            else
            {
                // 异步保存完成后的真正关闭
                base.OnClosing(e);
            }
        }

        private async Task HandleApplicationClosingAsync()
        {
            try
            {

                // 列出所有文档的状态
                foreach (var doc in _documentManager.Documents)
                {
                }

                // 使用与保存按钮相同的逻辑保存所有文档
                bool allSaved = true;

                // 1. 保存活动的多文档Tab文档（如果有）
                if (_documentManager.ActiveDocumentId != null)
                {
                    var activeDocument = _documentManager.GetNodeGraphDocument(_documentManager.ActiveDocumentId);
                    if (activeDocument != null)
                    {
                        try
                        {
                            var saveSuccess = activeDocument.Save();
                            if (saveSuccess)
                            {
                            }
                            else
                            {
                                allSaved = false;
                            }
                        }
                        catch (Exception saveEx)
                        {
                            allSaved = false;
                        }
                    }
                }

                // 2. 保存其他非活动的多文档Tab文档
                foreach (var doc in _documentManager.Documents)
                {
                    if (doc is NodeGraphDocument nodeGraphDoc && doc.Id != _documentManager.ActiveDocumentId)
                    {
                        try
                        {
                            var saveSuccess = nodeGraphDoc.Save();
                            if (saveSuccess)
                            {
                            }
                            else
                            {
                                allSaved = false;
                            }
                        }
                        catch (Exception saveEx)
                        {
                            allSaved = false;
                        }
                    }
                }

                // 3. 保存默认的NodeEditor（非多文档系统的节点图）- 使用与保存按钮相同的逻辑
                if (_documentManager.ActiveDocumentId == null)
                {
                    try
                    {
                        // 使用新的保存逻辑
                        var activeDocument = _viewModel.DocumentManager?.ActiveDocumentId != null
                            ? _viewModel.DocumentManager.GetNodeGraphDocument(_viewModel.DocumentManager.ActiveDocumentId)
                            : null;
                        var success = activeDocument != null ? await activeDocument.SaveAsync() : false;

                        if (success)
                        {
                        }
                        else
                        {
                            allSaved = false;
                        }
                    }
                    catch (Exception ex)
                    {
                        allSaved = false;
                    }
                }


                if (allSaved)
                {
                }
                else
                {
                }


                // 保存完成，设置标志并真正关闭窗口
                _isClosing = true;

                // 在UI线程上关闭窗口
                await Dispatcher.InvokeAsync(() =>
                {
                    Close();
                });
            }
            catch (Exception ex)
            {

                // 即使出现异常也要关闭应用程序
                _isClosing = true;
                await Dispatcher.InvokeAsync(() =>
                {
                    Close();
                });
            }
        }

        #endregion



        #region 多文档Tab事件处理

        /// <summary>
        /// 文档Tab变化事件处理
        /// </summary>
        private async void DocumentTabControl_DocumentChanged(object sender, Controls.DocumentChangedEventArgs e)
        {
            try
            {

                // 1. 处理旧文档的自动保存（参考Python版本的切换流程）
                if (e.OldDocumentId != null)
                {
                    var oldDocument = _documentManager.GetNodeGraphDocument(e.OldDocumentId);
                    if (oldDocument != null && oldDocument.IsModified && !string.IsNullOrEmpty(oldDocument.FilePath))
                    {
                        try
                        {
                            var saveSuccess = oldDocument.Save();
                            if (saveSuccess)
                            {
                            }
                            else
                            {
                            }
                        }
                        catch (Exception saveEx)
                        {
                        }
                    }

                    // 失活旧文档
                    oldDocument?.OnDeactivated();
                }

                // 2. 处理新文档的激活
                if (e.NewDocument is NodeGraphDocument nodeGraphDoc)
                {

                    // 更新MainViewModel的当前节点图
                    _viewModel.CurrentNodeGraph = nodeGraphDoc.NodeGraph;

                    // 更新MainViewModel的NodeEditor以确保Ribbon按钮指向当前活动的节点编辑器
                    _viewModel.SetActiveNodeEditor(nodeGraphDoc.NodeEditor);

                    // 重新绑定事件到新的NodeEditor
                    RebindNodeEditorEvents(nodeGraphDoc.NodeEditor);

                    // 更新节点编辑器的DataContext（这会触发UI重新绑定到新文档的数据）
                    // 注意：设置DataContext会自动触发OnDataContextChanged，会添加现有节点的UI控件
                    NodeEditorControl.DataContext = nodeGraphDoc.NodeEditor;

                    // 更新参数面板的DataContext
                    ParameterEditorControl.DataContext = nodeGraphDoc.NodeEditor;

                    // 等待UI更新完成后再进行其他操作
                    await Application.Current.Dispatcher.InvokeAsync(() => { }, System.Windows.Threading.DispatcherPriority.Loaded);

                    // 再次等待，确保所有异步节点重建完成
                    await Task.Delay(100);

                    // 激活新文档（这会更新图像预览和确保参数UI重建）
                    nodeGraphDoc.OnActivated();

                    // 更新文档管理器的活动文档
                    _documentManager.SetActiveDocument(e.NewDocumentId);

                    _viewModel.TaskStatus = $"已切换到文档: {nodeGraphDoc.Title}";

                }
                else if (e.NewDocumentId == null)
                {
                    // 没有活动文档，清除当前状态

                    // 先清除UI上的所有节点控件
                    try
                    {
                        NodeEditorControl.ClearAllNodeControls();
                    }
                    catch (Exception clearEx)
                    {
                    }

                    // 重新绑定到MainViewModel的NodeEditor
                    RebindNodeEditorEvents(_viewModel.NodeEditor);

                    // 清除节点编辑器和参数面板
                    NodeEditorControl.DataContext = _viewModel.NodeEditor;
                    ParameterEditorControl.DataContext = _viewModel.NodeEditor;

                    // 清除MainViewModel的节点图数据
                    try
                    {
                        _viewModel.NodeEditor.ClearAllCommand?.Execute(null);

                        // 创建新的空节点图
                        var emptyNodeGraph = _viewModel.FileService.CreateNewNodeGraph();
                        _viewModel.CurrentNodeGraph = emptyNodeGraph;

                    }
                    catch (Exception clearEx)
                    {
                    }

                    // 更新文档管理器
                    _documentManager.SetActiveDocument(null);

                    _viewModel.TaskStatus = "无活动文档";
                }
            }
            catch (Exception ex)
            {
                _viewModel.TaskStatus = $"文档切换失败: {ex.Message}";
            }
        }



        /// <summary>
        /// 新建文档请求事件处理
        /// </summary>
        private void DocumentTabControl_NewDocumentRequested(object sender, EventArgs e)
        {
            try
            {
                // 1. 先保存当前活动文档（如果有修改且有文件路径）
                var currentDocumentId = _documentManager.ActiveDocumentId;
                if (currentDocumentId != null)
                {
                    var currentDocument = _documentManager.GetNodeGraphDocument(currentDocumentId);
                    if (currentDocument != null)
                    {
                        // 只对已有文件路径且已修改的文档进行自动保存
                        if (currentDocument.IsModified && !string.IsNullOrEmpty(currentDocument.FilePath))
                        {
                            try
                            {
                                var saveSuccess = currentDocument.Save();
                                if (saveSuccess)
                                {
                                }
                                else
                                {
                                }
                            }
                            catch (Exception saveEx)
                            {
                            }
                        }
                        else if (currentDocument.IsModified)
                        {
                        }
                    }
                }

                // 2. 创建新节点图文档
                var document = _documentManager.CreateNodeGraphDocument();
                _viewModel.TaskStatus = $"已创建新节点图文档: {document.Title}";
            }
            catch (Exception ex)
            {
                _viewModel.TaskStatus = $"新建文档失败: {ex.Message}";
            }
        }

        #endregion



    }
}
