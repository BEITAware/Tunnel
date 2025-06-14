using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Tunnel_Next.Models
{
    /// <summary>
    /// 节点端口类型枚举
    /// </summary>
    public enum NodePortType
    {
        Input,
        Output
    }

    /// <summary>
    /// 节点端口模型
    /// </summary>
    public class NodePort : INotifyPropertyChanged
    {
        private string _name = string.Empty;
        private NodePortType _type;
        private NodePortDataType _dataType;
        private object? _value;
        private List<NodePort> _connectedPorts = new();
        private string _description = string.Empty;
        private double _x;
        private double _y;
        private bool _isFlexible;
        private bool _isInput;

        public string Name
        {
            get => _name;
            set { _name = value; OnPropertyChanged(); }
        }

        public NodePortType Type
        {
            get => _type;
            set { _type = value; OnPropertyChanged(); }
        }

        public NodePortDataType DataType
        {
            get => _dataType;
            set { _dataType = value; OnPropertyChanged(); }
        }

        public object? Value
        {
            get => _value;
            set { _value = value; OnPropertyChanged(); }
        }

        /// <summary>
        /// 连接的端口列表（支持一对多连接）
        /// </summary>
        public List<NodePort> ConnectedPorts
        {
            get => _connectedPorts;
            set { _connectedPorts = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsConnected)); }
        }

        /// <summary>
        /// 是否已连接
        /// </summary>
        public bool IsConnected => _connectedPorts.Count > 0;

        public string Description
        {
            get => _description;
            set { _description = value; OnPropertyChanged(); }
        }

        /// <summary>
        /// 是否为灵活端口（可动态添加）
        /// </summary>
        public bool IsFlexible
        {
            get => _isFlexible;
            set { _isFlexible = value; OnPropertyChanged(); }
        }

        /// <summary>
        /// 是否为输入端口（用于Revival Scripts）
        /// </summary>
        public bool IsInput
        {
            get => _isInput;
            set { _isInput = value; OnPropertyChanged(); }
        }

        // UI相关属性
        public double X
        {
            get => _x;
            set { _x = value; OnPropertyChanged(); }
        }

        public double Y
        {
            get => _y;
            set { _y = value; OnPropertyChanged(); }
        }

        /// <summary>
        /// 获取端口颜色
        /// </summary>
        public string Color => PortTypeDefinitions.GetPortColor(DataType);

        /// <summary>
        /// 添加连接
        /// </summary>
        public void AddConnection(NodePort port)
        {
            if (!_connectedPorts.Contains(port))
            {
                _connectedPorts.Add(port);
                OnPropertyChanged(nameof(ConnectedPorts));
                OnPropertyChanged(nameof(IsConnected));
            }
        }

        /// <summary>
        /// 移除连接
        /// </summary>
        public void RemoveConnection(NodePort port)
        {
            if (_connectedPorts.Remove(port))
            {
                OnPropertyChanged(nameof(ConnectedPorts));
                OnPropertyChanged(nameof(IsConnected));
            }
        }

        /// <summary>
        /// 清除所有连接
        /// </summary>
        public void ClearConnections()
        {
            _connectedPorts.Clear();
            OnPropertyChanged(nameof(ConnectedPorts));
            OnPropertyChanged(nameof(IsConnected));
        }

        /// <summary>
        /// 获取端口颜色映射（已废弃，请使用 PortTypeDefinitions.GetPortColor）
        /// </summary>
        [Obsolete("请使用 PortTypeDefinitions.GetPortColor 方法")]
        public static string GetPortColor(NodePortDataType dataType)
        {
            return PortTypeDefinitions.GetPortColor(dataType);
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    /// <summary>
    /// 节点参数模型
    /// </summary>
    public class NodeParameter : INotifyPropertyChanged
    {
        private object? _value;

        public string Name { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;

        public object? Value
        {
            get => _value;
            set
            {
                if (_value != value)
                {
                    _value = value;
                    OnPropertyChanged();
                }
            }
        }

        public object? DefaultValue { get; set; }
        public Type ParameterType { get; set; } = typeof(object);
        public string Type { get; set; } = "object";

        /// <summary>
        /// 原始脚本参数类型（用于UI控件选择）
        /// </summary>
        public Services.Scripting.ParameterType? OriginalParameterType { get; set; }

        public string Description { get; set; } = string.Empty;
        public bool IsVisible { get; set; } = true;
        public bool IsReadOnly { get; set; } = false;

        // 数值参数的范围限制
        public double? MinValue { get; set; }
        public double? MaxValue { get; set; }
        public double? Step { get; set; }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    /// <summary>
    /// 节点连接模型
    /// </summary>
    public class NodeConnection : INotifyPropertyChanged
    {
        private int _id;
        private Node? _outputNode;
        private string _outputPortName = string.Empty;
        private Node? _inputNode;
        private string _inputPortName = string.Empty;
        private string _color = "#FFFFFF";
        private double _thickness = 2.0;
        private bool _isValid = true;
        private bool _isHighlighted;

        public int Id
        {
            get => _id;
            set { _id = value; OnPropertyChanged(); }
        }

        public Node? OutputNode
        {
            get => _outputNode;
            set { _outputNode = value; OnPropertyChanged(); UpdateConnectionColor(); }
        }

        public string OutputPortName
        {
            get => _outputPortName;
            set { _outputPortName = value; OnPropertyChanged(); UpdateConnectionColor(); }
        }

        public Node? InputNode
        {
            get => _inputNode;
            set { _inputNode = value; OnPropertyChanged(); }
        }

        public string InputPortName
        {
            get => _inputPortName;
            set { _inputPortName = value; OnPropertyChanged(); }
        }

        // 连接线的视觉属性
        public string Color
        {
            get => _color;
            set { _color = value; OnPropertyChanged(); }
        }

        public double Thickness
        {
            get => _thickness;
            set { _thickness = value; OnPropertyChanged(); }
        }

        /// <summary>
        /// 连接是否有效
        /// </summary>
        public bool IsValid
        {
            get => _isValid;
            set { _isValid = value; OnPropertyChanged(); }
        }

        /// <summary>
        /// 连接是否高亮显示
        /// </summary>
        public bool IsHighlighted
        {
            get => _isHighlighted;
            set { _isHighlighted = value; OnPropertyChanged(); }
        }

        // 连接线的几何属性（用于绘制）
        public double StartX { get; set; }
        public double StartY { get; set; }
        public double EndX { get; set; }
        public double EndY { get; set; }

        /// <summary>
        /// 获取输出端口
        /// </summary>
        public NodePort? GetOutputPort()
        {
            return OutputNode?.GetOutputPort(OutputPortName);
        }

        /// <summary>
        /// 获取输入端口
        /// </summary>
        public NodePort? GetInputPort()
        {
            return InputNode?.GetInputPort(InputPortName);
        }

        /// <summary>
        /// 验证连接是否有效 - 已移除所有验证逻辑，由用户负责连接有效性
        /// </summary>
        public bool ValidateConnection()
        {
            // 移除所有连接验证逻辑，让用户自己为连接有效性负责
            // 原有的验证包括：
            // - 节点空值检查
            // - 不能连接到自己
            // - 端口存在性检查
            // - 类型兼容性检查
            // 现在直接返回true，允许任意连接

            IsValid = true;
            return true;
        }

        /// <summary>
        /// 检查两种数据类型是否兼容
        /// </summary>
        private static bool AreTypesCompatible(NodePortDataType outputType, NodePortDataType inputType)
        {
            // Any类型可以连接任何类型
            if (outputType == NodePortDataType.Any || inputType == NodePortDataType.Any)
                return true;

            // 相同类型可以连接
            if (outputType == inputType)
                return true;

            // 图像类型之间的兼容性
            var imageTypes = new[] { NodePortDataType.F32bmp, NodePortDataType.Img, NodePortDataType.Tif16, NodePortDataType.Tif8 };
            if (imageTypes.Contains(outputType) && imageTypes.Contains(inputType))
                return true;

            // 通道类型之间的兼容性
            var channelTypes = new[] { NodePortDataType.ChannelR, NodePortDataType.ChannelG, NodePortDataType.ChannelB, NodePortDataType.ChannelA };
            if (channelTypes.Contains(outputType) && channelTypes.Contains(inputType))
                return true;

            return false;
        }

        /// <summary>
        /// 更新连接线颜色（基于输出端口类型）
        /// </summary>
        private void UpdateConnectionColor()
        {
            var outputPort = GetOutputPort();
            if (outputPort != null)
            {
                Color = outputPort.Color;
            }
        }

        /// <summary>
        /// 更新连接线几何位置
        /// </summary>
        public void UpdateGeometry()
        {
            var outputPort = GetOutputPort();
            var inputPort = GetInputPort();

            if (outputPort != null && OutputNode != null)
            {
                StartX = OutputNode.X + OutputNode.Width - 5; // 输出端口在右侧
                StartY = OutputNode.Y + 20 + OutputNode.OutputPorts.IndexOf(outputPort) * 20 + 8;
            }

            if (inputPort != null && InputNode != null)
            {
                EndX = InputNode.X + 5; // 输入端口在左侧
                EndY = InputNode.Y + 20 + InputNode.InputPorts.IndexOf(inputPort) * 20 + 8;
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    /// <summary>
    /// 节点基础模型
    /// </summary>
    public class Node : INotifyPropertyChanged
    {
        private int _id;
        private string _title = string.Empty;
        private string _scriptPath = string.Empty;
        private string _category = string.Empty;
        private string _description = string.Empty;
        private string _color = "#4A90E2";
        private double _x;
        private double _y;
        private double _width = 120;
        private double _height = 80;
        private bool _isProcessed;
        private bool _hasError;
        private string _errorMessage = string.Empty;
        private bool _isSelected;
        private bool _isHighlighted;
        private object? _tag;
        private object? _viewModel;
        private bool _toBeProcessed = true; // 默认新节点需要处理
        private bool _showStatusIndicator = false; // 是否显示状态指示器
        private string _styleType = "V1"; // 节点样式类型：V1或V2

        public int Id
        {
            get => _id;
            set { _id = value; OnPropertyChanged(); }
        }

        public string Title
        {
            get => _title;
            set { _title = value; OnPropertyChanged(); }
        }

        public string ScriptPath
        {
            get => _scriptPath;
            set { _scriptPath = value; OnPropertyChanged(); }
        }

        public string Category
        {
            get => _category;
            set { _category = value; OnPropertyChanged(); }
        }

        public string Description
        {
            get => _description;
            set { _description = value; OnPropertyChanged(); }
        }

        public string Color
        {
            get => _color;
            set { _color = value; OnPropertyChanged(); }
        }

        // 位置和尺寸
        public double X
        {
            get => _x;
            set { _x = value; OnPropertyChanged(); }
        }

        public double Y
        {
            get => _y;
            set { _y = value; OnPropertyChanged(); }
        }

        public double Width
        {
            get => _width;
            set { _width = value; OnPropertyChanged(); }
        }

        public double Height
        {
            get => _height;
            set { _height = value; OnPropertyChanged(); }
        }

        // 端口和参数
        public ObservableCollection<NodePort> InputPorts { get; set; } = new();
        public ObservableCollection<NodePort> OutputPorts { get; set; } = new();
        public ObservableCollection<NodeParameter> Parameters { get; set; } = new();

        // 灵活端口配置
        public List<NodePortDataType> FlexibleInputTypes { get; set; } = new();
        public List<NodePortDataType> FlexibleOutputTypes { get; set; } = new();

        // 处理状态
        public bool IsProcessed
        {
            get => _isProcessed;
            set { _isProcessed = value; OnPropertyChanged(); }
        }

        public bool HasError
        {
            get => _hasError;
            set { _hasError = value; OnPropertyChanged(); }
        }

        public string ErrorMessage
        {
            get => _errorMessage;
            set { _errorMessage = value; OnPropertyChanged(); }
        }

        public Dictionary<string, object> ProcessedOutputs { get; set; } = new();

        // UI状态
        public bool IsSelected
        {
            get => _isSelected;
            set { _isSelected = value; OnPropertyChanged(); }
        }

        public bool IsHighlighted
        {
            get => _isHighlighted;
            set { _isHighlighted = value; OnPropertyChanged(); }
        }

        /// <summary>
        /// 节点标签，用于存储额外的数据（如Revival Script实例或ViewModel）
        /// </summary>
        public object? Tag
        {
            get => _tag;
            set { _tag = value; OnPropertyChanged(); }
        }

        /// <summary>
        /// 视图模型（用于Revival Scripts）
        /// </summary>
        public object? ViewModel
        {
            get => _viewModel;
            set { _viewModel = value; OnPropertyChanged(); }
        }

        /// <summary>
        /// 标记节点是否需要处理 - 用于选择性处理功能
        /// </summary>
        public bool ToBeProcessed
        {
            get => _toBeProcessed;
            set { _toBeProcessed = value; OnPropertyChanged(); }
        }

        /// <summary>
        /// 是否显示状态指示器
        /// </summary>
        public bool ShowStatusIndicator
        {
            get => _showStatusIndicator;
            set { _showStatusIndicator = value; OnPropertyChanged(); }
        }
        
        /// <summary>
        /// 节点样式类型（V1或V2）
        /// </summary>
        public string StyleType
        {
            get => _styleType;
            set { _styleType = value; OnPropertyChanged(); }
        }

        /// <summary>
        /// 用户数据字典，用于存储节点的扩展数据
        /// </summary>
        public Dictionary<string, object> UserData { get; set; } = new();

        /// <summary>
        /// 节点元数据字典
        /// </summary>
        public Dictionary<string, object> Metadata { get; set; } = new();

        /// <summary>
        /// 添加灵活端口
        /// </summary>
        public NodePort AddFlexiblePort(NodePortType portType, NodePortDataType dataType, string? name = null)
        {
            var portCollection = portType == NodePortType.Input ? InputPorts : OutputPorts;
            var flexibleTypes = portType == NodePortType.Input ? FlexibleInputTypes : FlexibleOutputTypes;

            if (!flexibleTypes.Contains(dataType))
            {
                throw new InvalidOperationException($"节点不支持 {dataType} 类型的灵活{(portType == NodePortType.Input ? "输入" : "输出")}端口");
            }

            var port = new NodePort
            {
                Name = name ?? $"{dataType}_{portCollection.Count}",
                Type = portType,
                DataType = dataType,
                IsFlexible = true
            };

            portCollection.Add(port);

            // 调整节点高度以适应新端口
            UpdateNodeHeight();

            return port;
        }

        /// <summary>
        /// 检查是否需要添加灵活端口
        /// </summary>
        public bool ShouldAddFlexiblePort(NodePortType portType, NodePortDataType dataType)
        {
            var portCollection = portType == NodePortType.Input ? InputPorts : OutputPorts;
            var flexibleTypes = portType == NodePortType.Input ? FlexibleInputTypes : FlexibleOutputTypes;

            if (!flexibleTypes.Contains(dataType))
                return false;

            // 检查是否所有相同类型的端口都已连接
            var portsOfType = portCollection.Where(p => p.DataType == dataType && p.IsFlexible).ToList();
            return portsOfType.All(p => p.IsConnected);
        }

        /// <summary>
        /// 更新节点高度以适应端口数量 - 增加端口间距
        /// </summary>
        public void UpdateNodeHeight()
        {
            var maxPorts = Math.Max(InputPorts.Count, OutputPorts.Count);
            var baseHeight = 85; // 基础高度
            var portSpacing = 24; // 每个端口占用的高度（包括间距）

            // 计算所需高度：基础高度 + 端口数量 * 端口间距
            Height = Math.Max(baseHeight, baseHeight + Math.Max(0, maxPorts - 1) * portSpacing);

        }

        /// <summary>
        /// 获取指定名称的输入端口
        /// </summary>
        public NodePort? GetInputPort(string name)
        {
            return InputPorts.FirstOrDefault(p => p.Name == name);
        }

        /// <summary>
        /// 获取指定名称的输出端口
        /// </summary>
        public NodePort? GetOutputPort(string name)
        {
            return OutputPorts.FirstOrDefault(p => p.Name == name);
        }

        /// <summary>
        /// 标记此节点及其所有下游节点需要处理
        /// </summary>
        /// <param name="nodeGraph">节点图，用于查找连接关系</param>
        /// <param name="visited">已访问的节点集合，防止循环依赖</param>
        public void MarkDownstreamForProcessing(NodeGraph nodeGraph, HashSet<int>? visited = null)
        {
            if (visited == null)
                visited = new HashSet<int>();

            // 防止循环依赖
            if (visited.Contains(Id))
                return;

            visited.Add(Id);

            // 标记当前节点需要处理
            ToBeProcessed = true;

            // 查找所有从此节点输出的连接
            var outputConnections = nodeGraph.Connections.Where(c => c.OutputNode?.Id == Id);
            var downstreamCount = outputConnections.Count();

            if (downstreamCount > 0)
            {
            }

            foreach (var connection in outputConnections)
            {
                if (connection.InputNode != null)
                {
                    // 递归标记下游节点
                    connection.InputNode.MarkDownstreamForProcessing(nodeGraph, visited);
                }
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    /// <summary>
    /// 节点图模型
    /// </summary>
    public class NodeGraph
    {
        public string Name { get; set; } = "新节点图";
        public string FilePath { get; set; } = string.Empty;
        public bool IsModified { get; set; }
        public DateTime LastModified { get; set; } = DateTime.Now;

        public ObservableCollection<Node> Nodes { get; set; } = new();
        public ObservableCollection<NodeConnection> Connections { get; set; } = new();

        // 视图状态
        public double ViewportX { get; set; }
        public double ViewportY { get; set; }
        public double ZoomLevel { get; set; } = 1.0;

        // 元数据
        public Dictionary<string, object> Metadata { get; set; } = new();

        /// <summary>
        /// 标记所有节点需要处理（用于首次加载）
        /// </summary>
        public void MarkAllNodesForProcessing()
        {
            foreach (var node in Nodes)
            {
                node.ToBeProcessed = true;
            }
        }

        /// <summary>
        /// 清除所有节点的处理标记
        /// </summary>
        public void ClearAllProcessingFlags()
        {
            foreach (var node in Nodes)
            {
                node.ToBeProcessed = false;
            }
        }

        /// <summary>
        /// 获取需要处理的节点列表
        /// </summary>
        public List<Node> GetNodesToProcess()
        {
            return Nodes.Where(n => n.ToBeProcessed).ToList();
        }

        /// <summary>
        /// 当节点参数更新时，标记该节点及其下游节点需要处理
        /// </summary>
        /// <param name="nodeId">参数发生变化的节点ID</param>
        public void MarkNodeAndDownstreamForProcessing(int nodeId)
        {
            var node = Nodes.FirstOrDefault(n => n.Id == nodeId);
            if (node != null)
            {
                node.MarkDownstreamForProcessing(this);
            }
        }

        /// <summary>
        /// 当节点被删除时，标记受影响的下游节点需要处理
        /// </summary>
        /// <param name="deletedNodeId">被删除的节点ID</param>
        public void HandleNodeDeletion(int deletedNodeId)
        {
            // 查找所有从被删除节点输出的连接
            var outputConnections = Connections.Where(c => c.OutputNode?.Id == deletedNodeId).ToList();

            // 标记所有下游节点需要处理
            foreach (var connection in outputConnections)
            {
                if (connection.InputNode != null)
                {
                    connection.InputNode.MarkDownstreamForProcessing(this);
                }
            }
        }

        /// <summary>
        /// 当连接发生变化时，标记受影响的节点需要处理
        /// </summary>
        /// <param name="connection">发生变化的连接</param>
        public void HandleConnectionChange(NodeConnection connection)
        {
            if (connection.OutputNode != null)
            {
                // 标记连接发出方（输出节点）及其所有下游节点需要处理
                connection.OutputNode.MarkDownstreamForProcessing(this);
            }
        }
    }

}
