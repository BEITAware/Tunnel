using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Tunnel_Next.Controls;
using Tunnel_Next.Services;
using Tunnel_Next.ViewModels;

namespace Tunnel_Next.Models
{
    /// <summary>
    /// 节点图文档 - 实现多文档Tab中的节点图类型文档
    /// </summary>
    public class NodeGraphDocument : IDocumentContent
    {
        #region 私有字段

        private readonly NodeGraph _nodeGraph;
        private readonly NodeEditorViewModel _nodeEditor;
        private readonly FileService _fileService;
        private Grid? _contentGrid;
        // 预览宿主容器
        private System.Windows.Controls.ContentControl? _previewHost;
        private ImagePreviewControl? _imagePreview;
        private bool _disposed = false;
        private string _id = Guid.NewGuid().ToString(); // 文档ID使用GUID
        private bool _canClose = true;
        private string _title;
        private bool _isModified;

        #endregion

        #region 事件

        /// <summary>
        /// 文档标题变化事件
        /// </summary>
        public event EventHandler<EventArgs>? TitleChanged;

        /// <summary>
        /// 文档修改状态变化事件
        /// </summary>
        public event EventHandler<EventArgs>? ModifiedChanged;

        /// <summary>
        /// 文档请求关闭事件
        /// </summary>
        public event EventHandler<EventArgs>? CloseRequested;

        #endregion

        #region 构造函数

        public NodeGraphDocument(NodeGraph nodeGraph, NodeEditorViewModel nodeEditor, FileService fileService)
        {
            _nodeGraph = nodeGraph ?? throw new ArgumentNullException(nameof(nodeGraph));
            _nodeEditor = nodeEditor ?? throw new ArgumentNullException(nameof(nodeEditor));
            _fileService = fileService ?? throw new ArgumentNullException(nameof(fileService));
            _title = nodeGraph.Name;
            _isModified = nodeGraph.IsModified;

            // 监听节点图修改状态
            _nodeEditor.NodeGraphModified += OnNodeGraphModified;

            // 监听预览更新请求
            _nodeEditor.PreviewUpdateRequested += UpdateImagePreview;
        }

        #endregion

        #region IDocumentContent 实现

        /// <summary>
        /// 文档类型
        /// </summary>
        public DocumentType DocumentType => DocumentType.NodeGraph;

        /// <summary>
        /// 文档唯一标识符
        /// </summary>
        public string Id => _id;

        /// <summary>
        /// 文档标题
        /// </summary>
        public string Title
        {
            get => _title;
            set
            {
                if (_title != value)
                {
                    _title = value;
                    _nodeGraph.Name = value;
                    TitleChanged?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        /// <summary>
        /// 文档是否已修改
        /// </summary>
        public bool IsModified
        {
            get => _isModified;
            set
            {
                if (_isModified != value)
                {
                    _isModified = value;
                    _nodeGraph.IsModified = value;
                    ModifiedChanged?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        /// <summary>
        /// 文档文件路径
        /// </summary>
        public string? FilePath
        {
            get => _nodeGraph.FilePath;
            set => _nodeGraph.FilePath = value;
        }

        /// <summary>
        /// 文档是否可关闭
        /// </summary>
        public bool CanClose
        {
            get => _canClose;
            set => _canClose = value;
        }

        /// <summary>
        /// 获取用于显示的UI控件
        /// </summary>
        public FrameworkElement GetContentControl()
        {
            if (_contentGrid == null)
            {
                CreateContentControl();
            }
            return _contentGrid!;
        }

        /// <summary>
        /// 保存文档
        /// </summary>
        public async Task<bool> SaveAsync(string? filePath = null)
        {

            try
            {
                // 获取当前节点图数据
                var currentNodeGraph = _nodeEditor.CreateNodeGraph();

                currentNodeGraph.Name = _nodeGraph.Name;
                currentNodeGraph.FilePath = _nodeGraph.FilePath;
                currentNodeGraph.IsModified = _nodeGraph.IsModified;
                currentNodeGraph.LastModified = _nodeGraph.LastModified;
                currentNodeGraph.ViewportX = _nodeGraph.ViewportX;
                currentNodeGraph.ViewportY = _nodeGraph.ViewportY;
                currentNodeGraph.ZoomLevel = _nodeGraph.ZoomLevel;
                currentNodeGraph.Metadata = _nodeGraph.Metadata;


                // 确定保存路径：优先使用传入的filePath，然后使用现有的FilePath
                var targetFilePath = filePath ?? FilePath;

                // 使用FileService保存节点图
                var success = await _fileService.SaveNodeGraphAsync(currentNodeGraph, targetFilePath);

                if (success)
                {
                    // 更新文档状态
                    FilePath = currentNodeGraph.FilePath;
                    IsModified = false;
                    _nodeGraph.FilePath = currentNodeGraph.FilePath;
                    _nodeGraph.IsModified = false;
                    _nodeGraph.LastModified = currentNodeGraph.LastModified;

                    return true;
                }
                else
                {
                    return false;
                }
            }
            catch (Exception ex)
            {
                if (ex.InnerException != null)
                {
                }
                MessageBox.Show($"保存过程中发生异常: {ex.Message}", "保存错误", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }

        /// <summary>
        /// 同步保存文档（为了向后兼容）
        /// </summary>
        public bool Save(string? filePath = null)
        {
            try
            {
                // 避免死锁，使用Task.Run在后台线程执行异步操作
                return Task.Run(async () => await SaveAsync(filePath)).GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                return false;
            }
        }

        /// <summary>
        /// 检查文档是否可以关闭
        /// </summary>
        public bool CheckCanClose()
        {
            // 如果文档未修改，可以直接关闭
            if (!IsModified)
            {
                return true;
            }

            // 文档已修改，询问用户是否保存
            var result = MessageBox.Show(
                $"文档 '{Title}' 已修改，是否保存？\n\n点击\"是\"保存并关闭\n点击\"否\"不保存直接关闭\n点击\"取消\"取消关闭操作",
                "保存确认",
                MessageBoxButton.YesNoCancel,
                MessageBoxImage.Question);

            switch (result)
            {
                case MessageBoxResult.Yes:
                    // 用户选择保存
                    try
                    {
                        bool saveSuccess = Save();
                        if (saveSuccess)
                        {
                            return true;
                        }
                        else
                        {
                            // 保存失败，询问是否强制关闭
                            var forceCloseResult = MessageBox.Show(
                                $"保存文档 '{Title}' 失败。\n\n是否强制关闭文档？（未保存的更改将丢失）",
                                "保存失败",
                                MessageBoxButton.YesNo,
                                MessageBoxImage.Warning);
                            return forceCloseResult == MessageBoxResult.Yes;
                        }
                    }
                    catch (Exception ex)
                    {
                        // 保存异常，询问是否强制关闭
                        var forceCloseResult = MessageBox.Show(
                            $"保存文档 '{Title}' 时发生异常：{ex.Message}\n\n是否强制关闭文档？（未保存的更改将丢失）",
                            "保存异常",
                            MessageBoxButton.YesNo,
                            MessageBoxImage.Error);
                        return forceCloseResult == MessageBoxResult.Yes;
                    }

                case MessageBoxResult.No:
                    // 用户选择不保存，直接关闭
                    return true;

                case MessageBoxResult.Cancel:
                default:
                    // 用户取消关闭操作
                    return false;
            }
        }

        /// <summary>
        /// 激活文档时调用
        /// </summary>
        public void OnActivated()
        {

            // 确保所有节点的参数UI都已正确重建
            _ = EnsureParametersRebuiltOnActivation();

            // 触发节点图处理以更新图像预览
            try
            {
                // 文档激活时触发完整的节点图处理
                _ = Task.Run(async () =>
                {
                    try
                    {
                        var nodeGraph = _nodeEditor.CreateNodeGraph();

                        // 如果有节点，标记所有节点需要处理并立即处理节点图
                        if (nodeGraph.Nodes.Count > 0)
                        {
                            // 文档激活时标记所有节点需要处理（首次完整处理）
                            nodeGraph.MarkAllNodesForProcessing();
                            await _nodeEditor.ProcessNodeGraphImmediately();
                        }
                        else
                        {
                            // 在UI线程触发预览更新
                            await Application.Current.Dispatcher.InvokeAsync(() =>
                            {
                                UpdateImagePreview();
                            });
                        }
                    }
                    catch (Exception ex)
                    {
                    }
                });
            }
            catch (Exception ex)
            {
            }

            // 更新图像预览
            UpdateImagePreview();
        }

        /// <summary>
        /// 文档激活时确保参数UI重建
        /// </summary>
        private async Task EnsureParametersRebuiltOnActivation()
        {
            try
            {

                // 等待一小段时间，确保UI绑定完成
                await Task.Delay(50);

                // 触发参数UI重建检查
                foreach (var node in _nodeEditor.Nodes)
                {
                    // 检查是否需要重建参数
                    if (node.Tag is Services.Scripting.IRevivalScript scriptInstance && node.Parameters.Count == 0)
                    {
                        RebuildNodeParameters(node, scriptInstance);
                    }
                }

            }
            catch (Exception ex)
            {
            }
        }

        /// <summary>
        /// 失活文档时调用
        /// </summary>
        public void OnDeactivated()
        {
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            if (!_disposed)
            {
                // 取消事件监听
                _nodeEditor.NodeGraphModified -= OnNodeGraphModified;
                _nodeEditor.PreviewUpdateRequested -= UpdateImagePreview;

                // 清理UI控件
                _imagePreview = null;
                _contentGrid = null;

                _disposed = true;
            }
        }

        #endregion

        #region 公共属性

        /// <summary>
        /// 获取节点图对象
        /// </summary>
        public NodeGraph NodeGraph => _nodeGraph;

        /// <summary>
        /// 获取节点编辑器视图模型
        /// </summary>
        public NodeEditorViewModel NodeEditor => _nodeEditor;

        /// <summary>
        /// 获取图像预览控件
        /// </summary>
        public ImagePreviewControl? ImagePreview => _imagePreview;

        #endregion

        #region 私有方法

        /// <summary>
        /// 获取默认的节点图文件夹路径（Windows最佳实践）
        /// </summary>
        /// <returns>默认节点图文件夹路径</returns>
        private string GetDefaultNodeGraphsFolder()
        {
            try
            {
                // 优先使用用户文档文件夹
                var documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                if (!string.IsNullOrEmpty(documentsPath) && Directory.Exists(documentsPath))
                {
                    return Path.Combine(documentsPath, "Tunnel", "nodegraphs");
                }
            }
            catch (Exception ex)
            {
            }

            try
            {
                // 回退到用户配置文件夹
                var userProfilePath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                if (!string.IsNullOrEmpty(userProfilePath) && Directory.Exists(userProfilePath))
                {
                    return Path.Combine(userProfilePath, "Documents", "Tunnel", "nodegraphs");
                }
            }
            catch (Exception ex)
            {
            }

            // 最后回退到应用程序数据文件夹
            try
            {
                var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                if (!string.IsNullOrEmpty(appDataPath))
                {
                    return Path.Combine(appDataPath, "Tunnel", "nodegraphs");
                }
            }
            catch (Exception ex)
            {
            }

            // 最终回退到当前目录
            return Path.Combine(Environment.CurrentDirectory, "Tunnel", "nodegraphs");
        }

        /// <summary>
        /// 确保目录存在，如果不存在则创建
        /// </summary>
        /// <param name="directoryPath">目录路径</param>
        private void EnsureDirectoryExists(string directoryPath)
        {
            try
            {
                if (!Directory.Exists(directoryPath))
                {
                    Directory.CreateDirectory(directoryPath);
                }
            }
            catch (Exception ex)
            {

                // 尝试使用临时文件夹作为备用
                try
                {
                    var tempPath = Path.Combine(Path.GetTempPath(), "Tunnel", "nodegraphs");
                    if (!Directory.Exists(tempPath))
                    {
                        Directory.CreateDirectory(tempPath);
                    }
                }
                catch (Exception tempEx)
                {
                    throw new InvalidOperationException($"无法创建节点图文件夹: {ex.Message}", ex);
                }
            }
        }

        private void CreateContentControl()
        {
            _contentGrid = new Grid();

            // 创建预览宿主
            _previewHost = new System.Windows.Controls.ContentControl();

            // 创建默认图像预览控件
            _imagePreview = new ImagePreviewControl();

            // 默认显示图像预览
            _previewHost.Content = _imagePreview;
            _contentGrid.Children.Add(_previewHost);

            // 初始化全局 PreviewManager（如果尚未初始化）
            try
            {
                Tunnel_Next.Services.UI.PreviewManager.Instance.Initialize(_previewHost, _imagePreview);
            }
            catch
            {
                // 已初始化或发生异常时忽略
            }
        }

        private void OnNodeGraphModified()
        {
            IsModified = true;
            _nodeGraph.LastModified = DateTime.Now;
        }

        /// <summary>
        /// 重建节点参数（从脚本实例获取当前参数值）
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
                    var parameter = new NodeParameter
                    {
                        Name = kvp.Key,
                        Value = kvp.Value,
                        Type = kvp.Value?.GetType().Name ?? "object"
                    };
                    node.Parameters.Add(parameter);
                }

            }
            catch (Exception ex)
            {
            }
        }

        private void UpdateImagePreview()
        {
            try
            {
                // 设置预览控件为非处理状态（避免界面卡顿）
                Controls.ImagePreviewControl.SetProcessingState(false);

                // 只显示预览节点的输出
                // 优先查找有输入连接的预览节点，如果没有则使用第一个预览节点
                var previewNodes = _nodeEditor.Nodes.Where(n => n.Title == "预览节点").ToList();
                var previewNode = previewNodes.FirstOrDefault(n => n.InputPorts.Any(p => p.Value != null))
                                ?? previewNodes.FirstOrDefault();

                if (previewNode != null && previewNode.ProcessedOutputs.TryGetValue("f32bmp", out var previewOutput))
                {
                    if (previewOutput is OpenCvSharp.Mat previewMat)
                    {
                        if (_imagePreview != null)
                        {
                            _imagePreview.ImageSource = previewMat;
                            return;
                        }
                    }
                }

                // 如果没有预览节点或预览节点没有输出，清空图像预览
                _imagePreview?.ClearImage();
            }
            catch (Exception ex)
            {
            }
        }

        #endregion
    }
}