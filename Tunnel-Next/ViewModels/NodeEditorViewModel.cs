using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Input;
using Tunnel_Next.Models;
using Tunnel_Next.Services;
using Tunnel_Next.Services.ImageProcessing;
using Tunnel_Next.Services.Scripting;
using System.Reflection;
using System.Linq;
using System.Threading.Tasks;

namespace Tunnel_Next.ViewModels
{
    /// <summary>
    /// 节点编辑器ViewModel
    /// </summary>
    public class NodeEditorViewModel : ViewModelBase
    {
        private Node? _selectedNode;
        private NodeConnection? _pendingConnection;
        private string _connectionStartPort = string.Empty;
        private bool _isConnectionStartOutput;
        private readonly IImageProcessingService _processingService;
        private readonly ConnectionManager _connectionManager;
        // 移除了传统的NodeTypeInfo，现在只使用Revival Scripts
        private RevivalScriptManager? _revivalScriptManager;
        private readonly FileService? _fileService;
        private bool _isProcessing = false;

        // 存储待添加节点的位置，用于区分Tab栏添加和右键添加
        private Point? _pendingNodePosition = null;

        public NodeEditorViewModel(RevivalScriptManager? revivalScriptManager = null, FileService? fileService = null)
        {
            _revivalScriptManager = revivalScriptManager;
            _fileService = fileService;

            // 设置RevivalNodeFactory的管理器引用
            if (_revivalScriptManager != null)
            {
                RevivalNodeFactory.SetRevivalScriptManager(_revivalScriptManager);
            }
            Nodes = new ObservableCollection<Node>();
            Connections = new ObservableCollection<NodeConnection>();
            _connectionManager = new ConnectionManager(Nodes, Connections);
            _connectionManager.ConnectionChanged += () => NodeGraphModified?.Invoke();
            _connectionManager.ConnectionErrorDetected += OnConnectionErrorDetected;

            // 创建MVVM解耦的处理服务
            if (_revivalScriptManager != null)
            {
                var imageProcessor = new ImageProcessor(_revivalScriptManager);
                _processingService = new ProcessingCoordinator(imageProcessor);
            }
            else
            {
                // 创建一个临时的RevivalScriptManager用于初始化
                var tempManager = CreateTemporaryRevivalScriptManager();
                var imageProcessor = new ImageProcessor(tempManager);
                _processingService = new ProcessingCoordinator(imageProcessor);
            }

            InitializeCommands();

            // 监听处理服务事件（MVVM解耦）
            _processingService.ProcessingStateChanged += OnProcessingStateChanged;
            _processingService.ProcessingCompleted += OnProcessingCompleted;
            _processingService.StatusChanged += OnStatusChanged;
        }

        /// <summary>
        /// 创建临时的RevivalScriptManager用于初始化
        /// </summary>
        private static RevivalScriptManager CreateTemporaryRevivalScriptManager()
        {
            var tempScriptsFolder = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "TempScripts");
            var tempResourcesFolder = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "TempResources");

            // 确保临时文件夹存在
            System.IO.Directory.CreateDirectory(tempScriptsFolder);
            System.IO.Directory.CreateDirectory(tempResourcesFolder);

            return new RevivalScriptManager(tempScriptsFolder, tempResourcesFolder);
        }

        /// <summary>
        /// 获取图像处理服务实例
        /// </summary>
        /// <returns>图像处理服务</returns>
        public IImageProcessingService GetProcessingService()
        {
            return _processingService;
        }

        /// <summary>
        /// 是否正在处理
        /// </summary>
        public bool IsProcessing
        {
            get => _isProcessing;
            private set => SetProperty(ref _isProcessing, value);
        }

        #region Properties

        /// <summary>
        /// 节点集合
        /// </summary>
        public ObservableCollection<Node> Nodes { get; }

        /// <summary>
        /// 连接集合
        /// </summary>
        public ObservableCollection<NodeConnection> Connections { get; }

        /// <summary>
        /// 当前选中的节点
        /// </summary>
        public Node? SelectedNode
        {
            get => _selectedNode;
            set
            {
                if (_selectedNode != null)
                    _selectedNode.IsSelected = false;

                SetProperty(ref _selectedNode, value);

                if (_selectedNode != null)
                    _selectedNode.IsSelected = true;
            }
        }

        /// <summary>
        /// 可用的节点类型
        /// </summary>
        // 移除了AvailableNodeTypes属性，现在只使用Revival Scripts

        /// <summary>
        /// 图像处理服务
        /// </summary>
        public IImageProcessingService ProcessingService => _processingService;

        /// <summary>
        /// 连接管理器
        /// </summary>
        public ConnectionManager ConnectionManager => _connectionManager;

        /// <summary>
        /// 是否有待连接状态
        /// </summary>
        public bool HasPendingConnection => _pendingConnection != null;

        /// <summary>
        /// 当前待连接对象（只读）
        /// </summary>
        public NodeConnection? PendingConnection => _pendingConnection;

        #endregion

        #region Commands

        public ICommand? AddNodeCommand { get; private set; }
        public ICommand? DeleteNodeCommand { get; private set; }
        public ICommand? ClearAllCommand { get; private set; }
        public ICommand? ProcessNodeGraphCommand { get; private set; }
        public ICommand? AddSpecificNodeCommand { get; private set; }
        public ICommand? AutoConnectNodesCommand { get; private set; }

        #endregion

        #region Private Methods

        private void InitializeCommands()
        {
            AddNodeCommand = new RelayCommand<Point?>(ExecuteAddNode);
            DeleteNodeCommand = new RelayCommand(ExecuteDeleteNode, CanDeleteNode);
            ClearAllCommand = new RelayCommand(ExecuteClearAll);
            ProcessNodeGraphCommand = new RelayCommand(ExecuteProcessNodeGraph);
            AddSpecificNodeCommand = new RelayCommand<string>(ExecuteAddSpecificNode);
            AutoConnectNodesCommand = new RelayCommand<object[]>(ExecuteAutoConnectNodes);
        }

        /// <summary>
        /// 同步参数到脚本 ViewModel
        /// </summary>
        private void SyncParameterToScriptViewModel(Node node, NodeParameter parameter)
        {
            try
            {
                // 获取脚本的 ViewModel
                IScriptViewModel? scriptViewModel = null;
                if (node.ViewModel is IScriptViewModel vm)
                {
                    scriptViewModel = vm;
                }
                else if (node.Tag is IScriptViewModel vmFromTag)
                {
                    scriptViewModel = vmFromTag;
                }

                if (scriptViewModel != null)
                {
                    // 使用反射设置 ViewModel 的属性
                    var viewModelType = scriptViewModel.GetType();
                    var property = viewModelType.GetProperty(parameter.Name);

                    if (property != null && property.CanWrite)
                    {
                        // 转换参数值类型
                        var convertedValue = ConvertParameterValue(parameter.Value, property.PropertyType);
                        property.SetValue(scriptViewModel, convertedValue);
                    }
                }
            }
            catch (Exception)
            {
                // 静默处理同步错误
            }
        }

        /// <summary>
        /// 转换参数值类型
        /// </summary>
        private object? ConvertParameterValue(object? value, Type targetType)
        {
            if (value == null) return null;
            if (targetType.IsAssignableFrom(value.GetType())) return value;

            try
            {
                if (targetType.IsGenericType && targetType.GetGenericTypeDefinition() == typeof(Nullable<>))
                {
                    targetType = Nullable.GetUnderlyingType(targetType) ?? targetType;
                }

                return Convert.ChangeType(value, targetType);
            }
            catch
            {
                return value;
            }
        }

        #endregion

        #region Command Implementations

        private void ExecuteAddNode(Point? position)
        {
            // 现在只使用Revival Scripts，通过RevivalNodeMenuService添加节点
        }

        private void ExecuteAddSpecificNode(string? nodeTypeName)
        {
            if (string.IsNullOrEmpty(nodeTypeName))
            {
                return;
            }

            // 现在通过RevivalScriptManager查找并创建节点
            if (_revivalScriptManager != null)
            {
                var scripts = _revivalScriptManager.GetAvailableRevivalScripts();
                var scriptInfo = scripts.Values.FirstOrDefault(s => s.Name == nodeTypeName);

                if (scriptInfo != null)
                {
                    // 使用存储的位置，如果没有则使用默认位置
                    var position = _pendingNodePosition ?? new Point(100 + Nodes.Count * 150, 100);

                    // 清除存储的位置，避免影响下次添加
                    _pendingNodePosition = null;

                    var node = RevivalNodeFactory.CreateRevivalNode(scriptInfo, position.X, position.Y);

                    // 监听老的节点参数变化 PropertyChanged (This is for individual NodeParameter objects)
                    foreach (var parameter in node.Parameters)
                    {
                        parameter.PropertyChanged += Parameter_PropertyChanged;
                    }

                    Nodes.Add(node);

                    // Subscribe to the script's external parameter change event from RevivalScriptBase
                    IRevivalScript? scriptInstanceForEvent = null;
                    if (node.ViewModel is IScriptViewModel vm && vm.Script != null)
                    {
                        scriptInstanceForEvent = vm.Script;
                    }
                    else if (node.Tag is IRevivalScript directScript)
                    {
                        scriptInstanceForEvent = directScript;
                    }
                    else if (node.Tag is IScriptViewModel vmFromTag && vmFromTag.Script != null)
                    {
                        scriptInstanceForEvent = vmFromTag.Script;
                    }

                    if (scriptInstanceForEvent is RevivalScriptBase rsb)
                    {
                        rsb.ParameterExternallyChanged += HandleScriptParameterExternallyChanged;
                    }

                    // 通知节点图已修改
                    NodeGraphModified?.Invoke();

                    // 添加节点后立即触发预览更新，特别是对于预览节点
                    PreviewUpdateRequested?.Invoke();
                }
            }
        }

        private async void Parameter_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(NodeParameter.Value))
            {
                var parameter = sender as NodeParameter;

                // 通知节点图已修改
                NodeGraphModified?.Invoke();

                // 找到参数所属的节点，并同步参数到脚本 ViewModel
                var changedNode = Nodes.FirstOrDefault(n => n.Parameters.Contains(parameter));
                if (changedNode != null && parameter != null)
                {
                    // 同步参数到脚本的 ViewModel，这将触发 ViewModel 的 PropertyChanged 事件
                    // 进而触发 RevivalScriptBase.OnParameterChanged 方法
                    SyncParameterToScriptViewModel(changedNode, parameter);

                    // 参数变化后标记节点及其下游节点需要处理，然后触发选择性处理
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            var nodeGraph = CreateNodeGraph();

                            // 标记该节点及其下游节点需要处理
                            nodeGraph.MarkNodeAndDownstreamForProcessing(changedNode.Id);

                            var result = await _processingService.ProcessChangedNodesAsync(nodeGraph, new[] { changedNode });

                            if (result.Success)
                            {
                                // 在UI线程触发预览更新
                                await Application.Current.Dispatcher.InvokeAsync(() =>
                                {
                                    PreviewUpdateRequested?.Invoke();
                                });
                            }
                        }
                        catch (Exception)
                        {
                            // 静默处理异常
                        }
                    });
                }
            }
        }

        // 旧的节点处理方法已移除，等待重构

        /// <summary>
        /// 执行节点图处理
        /// </summary>
        private async void ExecuteProcessNodeGraph()
        {
            try
            {
                // 创建当前节点图
                var nodeGraph = CreateNodeGraph();

                // 使用ProcessingCoordinator处理节点图
                var result = await _processingService.ProcessNodeGraphAsync(nodeGraph);
            }
            catch (Exception)
            {
                // 静默处理异常
            }
        }

        /// <summary>
        /// 立即处理节点图（同步方法）
        /// </summary>
        public async Task ProcessNodeGraphImmediately()
        {
            try
            {
                // 创建当前节点图
                var nodeGraph = CreateNodeGraph();

                // 使用ProcessingCoordinator处理节点图
                var result = await _processingService.ProcessNodeGraphAsync(nodeGraph);

                if (result.Success)
                {
                    // 触发预览更新
                    PreviewUpdateRequested?.Invoke();
                }
            }
            catch (Exception)
            {
                // 静默处理异常
            }
        }

        // 旧的立即处理方法已移除，等待重构

        private void ExecuteDeleteNode()
        {
            if (SelectedNode != null)
            {
                var nodeToDelete = SelectedNode;

                // 先清除选择，避免在删除过程中触发其他事件
                SelectedNode = null;

                // 找到所有相关连接
                var connectionsToRemove = Connections
                    .Where(c => c.InputNode?.Id == nodeToDelete.Id || c.OutputNode?.Id == nodeToDelete.Id)
                    .ToList();

                // 删除连接 - 使用ToList()避免在迭代时修改集合
                foreach (var connection in connectionsToRemove)
                {
                    // 使用ConnectionManager删除连接以确保端口状态正确更新
                    _connectionManager.RemoveConnection(connection);
                }

                // 从UI连接集合中移除所有相关连接
                foreach (var connection in connectionsToRemove)
                {
                    if (Connections.Contains(connection))
                    {
                        Connections.Remove(connection);
                    }
                }

                // 取消老的节点参数变化监听 (NodeParameter.PropertyChanged)
                foreach (var parameter in nodeToDelete.Parameters)
                {
                    parameter.PropertyChanged -= Parameter_PropertyChanged;
                }

                // Unsubscribe from the script's external parameter change event from RevivalScriptBase
                IRevivalScript? scriptInstanceForEvent = null;
                if (nodeToDelete.ViewModel is IScriptViewModel vm && vm.Script != null)
                {
                    scriptInstanceForEvent = vm.Script;
                }
                else if (nodeToDelete.Tag is IRevivalScript directScript)
                {
                    scriptInstanceForEvent = directScript;
                }
                else if (nodeToDelete.Tag is IScriptViewModel vmFromTag && vmFromTag.Script != null)
                {
                    scriptInstanceForEvent = vmFromTag.Script;
                }

                if (scriptInstanceForEvent is RevivalScriptBase rsb)
                {
                    rsb.ParameterExternallyChanged -= HandleScriptParameterExternallyChanged;
                }

                // 在删除节点前，处理选择性处理逻辑
                var nodeGraph = CreateNodeGraph();
                nodeGraph.HandleNodeDeletion(nodeToDelete.Id);

                // 删除节点
                Nodes.Remove(nodeToDelete);

                // 通知节点图已修改
                NodeGraphModified?.Invoke();

                // 删除节点后触发选择性处理（如果有受影响的下游节点）
                _ = Task.Run(async () =>
                {
                    try
                    {
                        var updatedNodeGraph = CreateNodeGraph();
                        var nodesToProcess = updatedNodeGraph.GetNodesToProcess();

                        if (nodesToProcess.Any())
                        {
                            var result = await _processingService.ProcessChangedNodesAsync(updatedNodeGraph, nodesToProcess.ToArray());

                            if (result.Success)
                            {
                                // 在UI线程触发预览更新
                                await Application.Current.Dispatcher.InvokeAsync(() =>
                                {
                                    PreviewUpdateRequested?.Invoke();
                                });
                            }
                        }
                        else
                        {
                            // 没有节点需要处理，直接更新预览
                            await Application.Current.Dispatcher.InvokeAsync(() =>
                            {
                                PreviewUpdateRequested?.Invoke();
                            });
                        }
                    }
                    catch (Exception)
                    {
                        // 静默处理异常，但仍然触发预览更新
                        await Application.Current.Dispatcher.InvokeAsync(() =>
                        {
                            PreviewUpdateRequested?.Invoke();
                        });
                    }
                });
            }
        }

        private bool CanDeleteNode()
        {
            return SelectedNode != null;
        }

        private void ExecuteClearAll()
        {
            ClearNodeGraph();
        }

        /// <summary>
        /// 完全清除当前节点图（参考Python版本的clear_node_graph方法）
        /// </summary>
        private void ClearNodeGraph()
        {
            // 1. 清除连接
            Connections.Clear();

            // 2. 清除节点（包括取消事件订阅）
            var nodesToRemove = Nodes.ToList(); // Create a copy for safe iteration if modifying Nodes collection directly
            foreach (var node in nodesToRemove)
            {
                // 取消老的节点参数变化监听 (NodeParameter.PropertyChanged)
                foreach (var parameter in node.Parameters)
                {
                    parameter.PropertyChanged -= Parameter_PropertyChanged;
                }

                // Unsubscribe from the script's external parameter change event from RevivalScriptBase
                IRevivalScript? scriptInstanceForEvent = null;
                if (node.ViewModel is IScriptViewModel vm && vm.Script != null)
                {
                    scriptInstanceForEvent = vm.Script;
                }
                else if (node.Tag is IRevivalScript directScript)
                {
                    scriptInstanceForEvent = directScript;
                }
                else if (node.Tag is IScriptViewModel vmFromTag && vmFromTag.Script != null)
                {
                    scriptInstanceForEvent = vmFromTag.Script;
                }

                if (scriptInstanceForEvent is RevivalScriptBase rsb)
                {
                    rsb.ParameterExternallyChanged -= HandleScriptParameterExternallyChanged;
                }

                // If node has a ViewModel that implements IDisposable, dispose it (optional, depends on your ViewModel design)
                if (node.ViewModel is IDisposable disposableViewModel)
                {
                    disposableViewModel.Dispose();
                }
                else if (node.Tag is IDisposable disposableTag)
                {
                    disposableTag.Dispose();
                }
            }

            // 3. 从主集合中移除所有节点
            Nodes.Clear();

            // 4. 重置连接状态
            ResetConnectionState();
            
            // 5. 通知UI清理
            ClearUIRequested?.Invoke();
        }

        private void ExecuteAutoConnectNodes(object[]? nodeArray)
        {
            if (nodeArray == null || nodeArray.Length != 2)
                return;

            if (nodeArray[0] is Node outputNode && nodeArray[1] is Node inputNode)
            {
                var connectionsCreated = _connectionManager.AutoConnectNodes(outputNode, inputNode);

                if (connectionsCreated > 0)
                {
                    // 自动连接创建后触发增量处理 - 处理连接发出方的下游节点
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            var nodeGraph = CreateNodeGraph();

                            // 标记输出节点及其下游节点需要处理
                            outputNode.MarkDownstreamForProcessing(nodeGraph);

                            var result = await _processingService.ProcessChangedNodesAsync(nodeGraph, new[] { outputNode });

                            if (result.Success)
                            {
                                // 在UI线程触发预览更新
                                await Application.Current.Dispatcher.InvokeAsync(() =>
                                {
                                    PreviewUpdateRequested?.Invoke();
                                });
                            }
                        }
                        catch (Exception)
                        {
                            // 静默处理异常
                        }
                    });
                }
            }
        }

        #endregion

        #region MVVM Event Handlers

        /// <summary>
        /// 处理状态变化事件处理（MVVM解耦）
        /// </summary>
        private void OnProcessingStateChanged(bool isProcessing)
        {
            IsProcessing = isProcessing;
            ProcessingStateChanged?.Invoke(isProcessing);
        }

        /// <summary>
        /// 处理完成事件处理（MVVM解耦）
        /// </summary>
        private void OnProcessingCompleted(ProcessingResult result)
        {
            try
            {
                if (result.Success)
                {
                    PreviewUpdateRequested?.Invoke();
                }
            }
            catch (Exception)
            {
                // 静默处理异常
            }
        }

        /// <summary>
        /// 状态变化事件处理（MVVM解耦）
        /// </summary>
        private void OnStatusChanged(string status)
        {
            // 可以在这里更新UI状态栏等
        }

        /// <summary>
        /// 连接错误检测事件处理
        /// </summary>
        private void OnConnectionErrorDetected(string errorMessage)
        {
            try
            {
                // 在UI线程上触发连接错误事件
                _ = System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    ConnectionErrorDetected?.Invoke(errorMessage);
                });
            }
            catch (Exception ex)
            {
            }
        }

        /// <summary>
        /// 预览更新请求事件
        /// </summary>
        public event Action? PreviewUpdateRequested;

        /// <summary>
        /// 处理状态变化事件
        /// </summary>
        public event Action<bool>? ProcessingStateChanged;

        /// <summary>
        /// 节点图修改状态变化事件
        /// </summary>
        public event Action? NodeGraphModified;

        /// <summary>
        /// 清除UI控件请求事件
        /// </summary>
        public event Action? ClearUIRequested;

        /// <summary>
        /// 连接错误检测事件
        /// </summary>
        public event Action<string>? ConnectionErrorDetected;

        /// <summary>
        /// 连接变化事件
        /// </summary>
        public event Action? ConnectionsChanged;

        #endregion

        #region Event Handlers for Script Parameter Changes

        private async void HandleScriptParameterExternallyChanged(object? sender, ScriptParameterChangedEventArgs e)
        {
            if (sender is IRevivalScript changedScript)
            {
                // Find the node associated with this script instance.
                // Nodes can store the script instance or its ViewModel in the Tag property,
                // or the ViewModel in the ViewModel property.
                Node? associatedNode = Nodes.FirstOrDefault(n =>
                    (n.Tag == changedScript) ||
                    (n.Tag is IScriptViewModel vmTag && vmTag.Script == changedScript) ||
                    (n.ViewModel is IScriptViewModel vmProp && vmProp.Script == changedScript)
                );

                if (associatedNode != null)
                {
                    // The RevivalScriptBase.OnParameterChanged method already calls the script's OnParameterChangedAsync,
                    // so the script's internal state (like ImageInputScript.ImagePath) should be up-to-date.

                    // Trigger graph re-processing with selective processing.
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            var nodeGraph = CreateNodeGraph();

                            // 标记该节点及其下游节点需要处理
                            nodeGraph.MarkNodeAndDownstreamForProcessing(associatedNode.Id);

                            var result = await _processingService.ProcessChangedNodesAsync(nodeGraph, [associatedNode]);

                            if (result.Success)
                            {
                                // 在UI线程触发预览更新
                                await Application.Current.Dispatcher.InvokeAsync(() =>
                                {
                                    PreviewUpdateRequested?.Invoke();
                                });
                            }
                        }
                        catch (Exception)
                        {
                            // 静默处理异常
                        }
                    });
                }
            }
        }

        #endregion

        #region Node Position Management

        /// <summary>
        /// 设置下一个要添加的节点位置（用于右键菜单添加节点）
        /// </summary>
        /// <param name="position">节点位置</param>
        public void SetPendingNodePosition(Point position)
        {
            _pendingNodePosition = position;
        }

        /// <summary>
        /// 设置下一个要添加的节点位置为视图中央（用于Tab栏添加节点）
        /// </summary>
        /// <param name="viewportSize">视口大小</param>
        public void SetPendingNodePositionToCenter(Size viewportSize)
        {
            // 计算视图中央位置
            var centerX = viewportSize.Width / 2;
            var centerY = viewportSize.Height / 2;
            _pendingNodePosition = new Point(centerX, centerY);
        }

        /// <summary>
        /// 清除待添加节点的位置
        /// </summary>
        public void ClearPendingNodePosition()
        {
            _pendingNodePosition = null;
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// 处理节点选择
        /// </summary>
        public void SelectNode(Node node)
        {
            SelectedNode = node;
        }

        /// <summary>
        /// 处理端口点击
        /// </summary>
        public void HandlePortClick(Node node, string portName, bool isOutput)
        {
            if (_pendingConnection == null)
            {
                // 开始新连接 - 只能从输出端口开始
                if (isOutput)
                {
                    _pendingConnection = new NodeConnection
                    {
                        Id = GetNextConnectionId(),
                        OutputNode = node,
                        OutputPortName = portName
                    };
                    _connectionStartPort = portName;
                    _isConnectionStartOutput = true;

                    // 通知UI更新连接状态
                    OnPropertyChanged(nameof(Connections));
                    OnPropertyChanged(nameof(HasPendingConnection));
                }
            }
            else
            {
                // 完成连接 - 只能连接到输入端口
                if (!isOutput && _isConnectionStartOutput && _pendingConnection.OutputNode != null)
                {
                    // 使用ConnectionManager创建连接
                    var connection = _connectionManager.CreateConnection(
                        _pendingConnection.OutputNode,
                        _pendingConnection.OutputPortName,
                        node,
                        portName);

                    if (connection != null)
                    {
                        // 触发连接变化事件
                        ConnectionsChanged?.Invoke();

                        // 连接创建后触发增量处理 - 处理连接发出方的下游节点
                        _ = Task.Run(async () =>
                        {
                            try
                            {
                                var nodeGraph = CreateNodeGraph();

                                // 处理连接变化，标记连接发出方的下游节点
                                nodeGraph.HandleConnectionChange(connection);

                                // 处理连接发出方（输出节点）
                                var outputNode = connection.OutputNode;
                                if (outputNode != null)
                                {
                                    var result = await _processingService.ProcessChangedNodesAsync(nodeGraph, new[] { outputNode });

                                    if (result.Success)
                                    {
                                        // 在UI线程触发预览更新
                                        await Application.Current.Dispatcher.InvokeAsync(() =>
                                        {
                                            PreviewUpdateRequested?.Invoke();
                                        });
                                    }
                                }
                            }
                            catch (Exception)
                            {
                                // 静默处理异常
                            }
                        });
                    }
                }
                else if (isOutput)
                {
                    // 重新开始连接
                    _pendingConnection = new NodeConnection
                    {
                        Id = GetNextConnectionId(),
                        OutputNode = node,
                        OutputPortName = portName
                    };
                    _connectionStartPort = portName;
                    _isConnectionStartOutput = true;

                    return; // 不重置状态
                }

                // 重置待连接状态
                ResetConnectionState();
            }
        }

        /// <summary>
        /// 重置连接状态
        /// </summary>
        private void ResetConnectionState()
        {
            _pendingConnection = null;
            _connectionStartPort = string.Empty;
            _isConnectionStartOutput = false;

            // 清除所有节点的高亮状态
            foreach (var node in Nodes)
            {
                node.IsHighlighted = false;
            }

            // 通知UI更新
            OnPropertyChanged(nameof(Connections));
            OnPropertyChanged(nameof(HasPendingConnection));
        }

        /// <summary>
        /// 断开端口的所有连接
        /// </summary>
        public void DisconnectPort(Node node, string portName, bool isOutput)
        {
            if (node == null || string.IsNullOrEmpty(portName))
                return;

            // 找到所有相关连接
            var connectionsToRemove = Connections
                .Where(c => isOutput
                    ? (c.OutputNode?.Id == node.Id && c.OutputPortName == portName)
                    : (c.InputNode?.Id == node.Id && c.InputPortName == portName))
                .ToList();

            // 删除连接
            foreach (var connection in connectionsToRemove)
            {
                // 使用ConnectionManager删除连接以确保端口状态正确更新
                _connectionManager.RemoveConnection(connection);

                // 从UI连接集合中移除
                if (Connections.Contains(connection))
                {
                    Connections.Remove(connection);
                }
            }

            // 如果有连接被删除，触发处理 - 处理连接发出方的下游节点
            if (connectionsToRemove.Count > 0)
            {
                // 触发连接变化事件
                ConnectionsChanged?.Invoke();

                _ = Task.Run(async () =>
                {
                    try
                    {
                        var nodeGraph = CreateNodeGraph();

                        // 收集所有受影响的输出节点
                        var affectedOutputNodes = new HashSet<Node>();

                        foreach (var connection in connectionsToRemove)
                        {
                            if (connection.OutputNode != null)
                            {
                                // 标记连接发出方的下游节点需要处理
                                connection.OutputNode.MarkDownstreamForProcessing(nodeGraph);
                                affectedOutputNodes.Add(connection.OutputNode);
                            }
                        }

                        if (affectedOutputNodes.Count > 0)
                        {
                            var result = await _processingService.ProcessChangedNodesAsync(nodeGraph, affectedOutputNodes.ToArray());

                            if (result.Success)
                            {
                                // 在UI线程触发预览更新
                                await Application.Current.Dispatcher.InvokeAsync(() =>
                                {
                                    PreviewUpdateRequested?.Invoke();
                                });
                            }
                        }
                    }
                    catch (Exception)
                    {
                        // 静默处理异常
                    }
                });
            }
        }

        /// <summary>
        /// 获取待连接的起点位置
        /// </summary>
        public Point? GetPendingConnectionStartPoint()
        {
            if (_pendingConnection == null)
                return null;

            // 这里不能直接访问UI控件，相关逻辑应在控件端实现
            // 返回null，让控件端通过节点和端口名称去查找实际位置
            return null;
        }

        /// <summary>
        /// 开始端口拖拽
        /// </summary>
        public void StartPortDrag(Node node, string portName, bool isOutput)
        {
            if (isOutput)
            {
                // 只有输出端口可以开始拖拽
                _pendingConnection = new NodeConnection
                {
                    Id = GetNextConnectionId(),
                    OutputNode = node,
                    OutputPortName = portName
                };
                _connectionStartPort = portName;
                _isConnectionStartOutput = true;

                // 通知UI更新连接状态
                OnPropertyChanged(nameof(Connections));
                OnPropertyChanged(nameof(HasPendingConnection));
            }
        }

        /// <summary>
        /// 完成端口拖拽
        /// </summary>
        public void CompletePortDrag(Node targetNode, string targetPortName, bool targetIsOutput)
        {
            if (_pendingConnection == null)
            {
                return;
            }

            if (targetIsOutput)
            {
                ResetConnectionState();
                return;
            }

            // 设置连接的输入端
            _pendingConnection.InputNode = targetNode;
            _pendingConnection.InputPortName = targetPortName;

            // 使用ConnectionManager创建连接（确保输入端口单连接限制）
            var connection = _connectionManager.CreateConnection(
                _pendingConnection.OutputNode,
                _pendingConnection.OutputPortName,
                _pendingConnection.InputNode,
                _pendingConnection.InputPortName);

            if (connection != null)
            {
                // 触发连接变化事件
                ConnectionsChanged?.Invoke();

                // 拖拽连接创建后触发选择性处理 - 处理连接发出方的下游节点
                _ = Task.Run(async () =>
                {
                    try
                    {
                        var nodeGraph = CreateNodeGraph();

                        // 处理连接变化，标记连接发出方的下游节点
                        nodeGraph.HandleConnectionChange(connection);

                        // 处理连接发出方（输出节点）
                        var outputNode = connection.OutputNode;
                        if (outputNode != null)
                        {
                            var result = await _processingService.ProcessChangedNodesAsync(nodeGraph, [outputNode]);

                            if (result.Success)
                            {
                                // 在UI线程触发预览更新
                                await Application.Current.Dispatcher.InvokeAsync(() =>
                                {
                                    PreviewUpdateRequested?.Invoke();
                                });
                            }
                        }
                    }
                    catch (Exception)
                    {
                        // 静默处理异常
                    }
                });
            }

            // 重置连接状态
            ResetConnectionState();
        }

        /// <summary>
        /// 创建节点图（从当前编辑器状态）
        /// </summary>
        /// <returns>当前状态的节点图</returns>
        public NodeGraph CreateNodeGraph()
        {

            var nodeGraph = new NodeGraph
            {
                Name = "当前节点图",
                FilePath = string.Empty,
                IsModified = true,
                LastModified = DateTime.Now,
                ViewportX = 0,
                ViewportY = 0,
                ZoomLevel = 1.0
            };


            // 复制节点
            foreach (var node in Nodes)
            {

                nodeGraph.Nodes.Add(node);
            }

            // 复制连接
            foreach (var connection in Connections)
            {
                nodeGraph.Connections.Add(connection);
            }

            return nodeGraph;
        }

        /// <summary>
        /// 加载节点图
        /// </summary>
        public async Task LoadNodeGraphAsync(NodeGraph nodeGraph)
        {
            try
            {
                // 通知UI层清理节点控件，等待清理完成
                var clearUITask = new TaskCompletionSource<bool>();
                
                // 注册一次性事件处理器，等待UI清理完成
                EventHandler? uiClearedHandler = null;
                uiClearedHandler = (s, e) => {
                    clearUITask.TrySetResult(true);
                    // 解除事件订阅
                    if (UIClearedEvent != null && uiClearedHandler != null)
                        UIClearedEvent -= uiClearedHandler;
                };
                
                // 订阅UI清理完成事件
                if (UIClearedEvent != null)
                    UIClearedEvent += uiClearedHandler;
                
                // 触发UI清理请求
                ClearUIRequested?.Invoke();
                
                // 等待UI清理完成，设置超时
                var timeoutTask = Task.Delay(3000); // 3秒超时
                var completedTask = await Task.WhenAny(clearUITask.Task, timeoutTask);
                
                // 如果是超时任务完成，记录日志
                if (completedTask == timeoutTask)
                {
                    System.Diagnostics.Debug.WriteLine("等待UI清理超时，继续加载节点图");
                    // 解除事件订阅
                    if (UIClearedEvent != null && uiClearedHandler != null)
                        UIClearedEvent -= uiClearedHandler;
                }
                
                // 完全清除当前节点图（模型层面的清理）
                ClearNodeGraph();

                // 加载节点
                foreach (var node in nodeGraph.Nodes)
                {
                    // 监听节点参数变化
                    foreach (var parameter in node.Parameters)
                    {
                        parameter.PropertyChanged += Parameter_PropertyChanged;
                    }

                    // 更新节点高度以适应端口
                    node.UpdateNodeHeight();

                    Nodes.Add(node);

                    // 【修复】绑定脚本事件 - 这是加载节点图时缺失的关键步骤！
                    // 与新建节点时的逻辑保持一致（参考ExecuteAddSpecificNode方法）
                    IRevivalScript? scriptInstanceForEvent = null;
                    if (node.ViewModel is IScriptViewModel vm && vm.Script != null)
                    {
                        scriptInstanceForEvent = vm.Script;
                    }
                    else if (node.Tag is IRevivalScript directScript)
                    {
                        scriptInstanceForEvent = directScript;
                    }
                    else if (node.Tag is IScriptViewModel vmFromTag && vmFromTag.Script != null)
                    {
                        scriptInstanceForEvent = vmFromTag.Script;
                    }

                    if (scriptInstanceForEvent is RevivalScriptBase rsb)
                    {
                        rsb.ParameterExternallyChanged += HandleScriptParameterExternallyChanged;
                    }
                }

                // 加载连接
                foreach (var connection in nodeGraph.Connections)
                {
                    // 验证连接有效性
                    if (connection.ValidateConnection())
                    {
                        Connections.Add(connection);
                    }
                }

                // 重置选择状态
                SelectedNode = null;

                // 触发节点图修改事件以更新UI
                NodeGraphModified?.Invoke();

                // 触发预览更新（这是关键！加载节点图后需要刷新预览）
                PreviewUpdateRequested?.Invoke();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"加载节点图失败: {ex.Message}");
                throw;
            }
        }
        
        /// <summary>
        /// UI清理完成事件
        /// </summary>
        public event EventHandler? UIClearedEvent;
        
        /// <summary>
        /// 触发UI清理完成事件
        /// </summary>
        public void NotifyUIClearedEvent()
        {
            UIClearedEvent?.Invoke(this, EventArgs.Empty);
        }

        // 旧的EnsureAllNodesParametersRebuilt方法已移除，等待重构

        /// <summary>
        /// 刷新可用的节点类型（现在只支持Revival Scripts）
        /// </summary>
        public void RefreshAvailableNodeTypes()
        {
            // 现在只使用Revival Scripts，不需要刷新传统节点类型
        }

        /// <summary>
        /// 更新Revival Script管理器
        /// </summary>
        /// <param name="revivalScriptManager">新的Revival Script管理器</param>
        public void UpdateRevivalScriptManager(RevivalScriptManager revivalScriptManager)
        {
            _revivalScriptManager = revivalScriptManager;

            // 同时更新RevivalNodeFactory的管理器引用
            RevivalNodeFactory.SetRevivalScriptManager(revivalScriptManager);

        }

        /// <summary>
        /// 取消待连接状态
        /// </summary>
        public void CancelPendingConnection()
        {
            ResetConnectionState();
        }



        #endregion

        #region Private Helper Methods

        private int GetNextConnectionId()
        {
            return Connections.Count > 0 ? Connections.Max(c => c.Id) + 1 : 1;
        }

        private bool IsValidConnection(NodeConnection connection)
        {

            // 移除所有连接验证逻辑，让用户自己为连接有效性负责
            // 原有的验证包括：
            // - 不能连接到自己
            // - 输出端口存在性检查
            // - 输入端口存在性检查
            // - 重复连接检查
            // 现在直接返回true，允许任意连接

            return true;
        }

        private void UpdatePortConnections(NodeConnection connection)
        {
            // 更新输出端口
            var outputPort = connection.OutputNode.OutputPorts
                .FirstOrDefault(p => p.Name == connection.OutputPortName);

            // 更新输入端口
            var inputPort = connection.InputNode.InputPorts
                .FirstOrDefault(p => p.Name == connection.InputPortName);

            if (outputPort != null && inputPort != null)
            {
                inputPort.AddConnection(outputPort);
                outputPort.AddConnection(inputPort);
            }
        }

        // 旧的RebuildRevivalScriptNode方法已移除，等待重构

        // 旧的RebuildNodePorts方法已移除，等待重构

        // 旧的重建节点参数方法已移除，等待重构

        // 旧的GetDefaultValue方法已移除，等待重构

        // 旧的GetPortDataType方法已移除，等待重构

        #endregion

        #region IDisposable

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            _processingService?.Dispose();
        }

        #endregion
    }
}
