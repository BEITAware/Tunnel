using System.Collections.ObjectModel;
using Tunnel_Next.Models;

namespace Tunnel_Next.Services
{
    /// <summary>
    /// 连接管理器 - 处理节点连接的创建、验证和管理
    /// </summary>
    public class ConnectionManager
    {
        private readonly ObservableCollection<Node> _nodes;
        private readonly ObservableCollection<NodeConnection> _connections;
        private int _nextConnectionId = 1;

        /// <summary>
        /// 连接状态变化事件（创建或删除连接时触发）
        /// </summary>
        public event Action? ConnectionChanged;

        /// <summary>
        /// 连接错误检测事件（当检测到类型不匹配的连接时触发）
        /// </summary>
        public event Action<string>? ConnectionErrorDetected;

        /// <summary>
        /// 灵活端口添加事件
        /// </summary>
        public event Action<Node, NodePort>? FlexiblePortAdded;

        public ConnectionManager(ObservableCollection<Node> nodes, ObservableCollection<NodeConnection> connections)
        {
            _nodes = nodes;
            _connections = connections;
        }

        /// <summary>
        /// 创建连接
        /// </summary>
        public NodeConnection? CreateConnection(Node outputNode, string outputPortName, Node inputNode, string inputPortName)
        {

            // 验证连接有效性
            if (!ValidateConnection(outputNode, outputPortName, inputNode, inputPortName, out var errorMessage))
            {
                return null;
            }

            // 移除重复连接检查 - 允许用户创建重复连接
            // 原代码：检查是否已存在相同连接
            // var existingConnection = _connections.FirstOrDefault(c =>
            //     c.OutputNode?.Id == outputNode.Id &&
            //     c.OutputPortName == outputPortName &&
            //     c.InputNode?.Id == inputNode.Id &&
            //     c.InputPortName == inputPortName);
            //
            // if (existingConnection != null)
            // {
            //     Console.WriteLine("[警告] 连接已存在，返回现有连接");
            //     return existingConnection;
            // }

            // 强制断开输入端口的现有连接（输入端口只能有一个连接）
            // 这是关键修复：确保输入端口只能有一个连接，符合Python原型行为
            var oldConnection = DisconnectInputPort(inputNode, inputPortName);
            if (oldConnection != null)
            {
            }

            // 创建新连接
            var connection = new NodeConnection
            {
                Id = GetNextConnectionId(),
                OutputNode = outputNode,
                OutputPortName = outputPortName,
                InputNode = inputNode,
                InputPortName = inputPortName
            };

            // 验证连接
            if (!connection.ValidateConnection())
            {
                return null;
            }

            // 更新端口连接状态
            var outputPort = connection.GetOutputPort();
            var inputPort = connection.GetInputPort();

            if (outputPort != null && inputPort != null)
            {
                outputPort.AddConnection(inputPort);
                inputPort.AddConnection(outputPort);

                // 检查并添加灵活端口
                CheckAndAddFlexiblePorts(outputNode, outputPortName, NodePortType.Output);
                CheckAndAddFlexiblePorts(inputNode, inputPortName, NodePortType.Input);
            }

            // 添加到连接集合
            _connections.Add(connection);


            // 触发连接变化事件
            ConnectionChanged?.Invoke();

            return connection;
        }

        /// <summary>
        /// 删除连接
        /// </summary>
        public bool RemoveConnection(NodeConnection connection)
        {
            if (!_connections.Contains(connection))
                return false;

            // 更新端口连接状态
            var outputPort = connection.GetOutputPort();
            var inputPort = connection.GetInputPort();

            if (outputPort != null && inputPort != null)
            {
                outputPort.RemoveConnection(inputPort);
                inputPort.RemoveConnection(outputPort);
            }

            // 从连接集合中移除
            _connections.Remove(connection);


            // 触发连接变化事件
            ConnectionChanged?.Invoke();

            return true;
        }

        /// <summary>
        /// 断开输入端口的所有连接
        /// </summary>
        /// <returns>被断开的第一个连接，如果没有连接则返回null</returns>
        public NodeConnection? DisconnectInputPort(Node inputNode, string inputPortName)
        {
            var connectionsToRemove = _connections
                .Where(c => c.InputNode?.Id == inputNode.Id && c.InputPortName == inputPortName)
                .ToList();


            NodeConnection? firstRemovedConnection = null;
            foreach (var connection in connectionsToRemove)
            {
                firstRemovedConnection ??= connection;
                RemoveConnection(connection);
            }

            return firstRemovedConnection;
        }

        /// <summary>
        /// 断开输出端口的所有连接
        /// </summary>
        public void DisconnectOutputPort(Node outputNode, string outputPortName)
        {
            var connectionsToRemove = _connections
                .Where(c => c.OutputNode?.Id == outputNode.Id && c.OutputPortName == outputPortName)
                .ToList();

            foreach (var connection in connectionsToRemove)
            {
                RemoveConnection(connection);
            }
        }

        /// <summary>
        /// 验证连接是否有效 - 已移除所有验证逻辑，由用户负责连接有效性
        /// </summary>
        private bool ValidateConnection(Node outputNode, string outputPortName, Node inputNode, string inputPortName, out string errorMessage)
        {
            errorMessage = string.Empty;

            // 移除所有连接验证逻辑，让用户自己为连接有效性负责
            // 原有的验证包括：
            // - 节点空值检查
            // - 不能连接到自己
            // - 端口存在性检查
            // - 循环检测
            // 现在直接返回true，允许任意连接

            return true;
        }

        /// <summary>
        /// 获取下一个连接ID
        /// </summary>
        private int GetNextConnectionId()
        {
            return _nextConnectionId++;
        }

        /// <summary>
        /// 检查数据类型是否兼容（已废弃，请使用 PortTypeDefinitions.AreTypesCompatible）
        /// </summary>
        [Obsolete("请使用 PortTypeDefinitions.AreTypesCompatible 方法")]
        private static bool AreTypesCompatible(NodePortDataType outputType, NodePortDataType inputType)
        {
            return PortTypeDefinitions.AreTypesCompatible(outputType, inputType);
        }

        /// <summary>
        /// 检查是否会形成循环
        /// </summary>
        private bool WouldFormCycle(Node outputNode, Node inputNode)
        {
            var visited = new HashSet<int>();
            return HasPathToNode(inputNode, outputNode, visited);
        }

        /// <summary>
        /// 检查从起始节点是否有路径到达目标节点
        /// </summary>
        private bool HasPathToNode(Node startNode, Node targetNode, HashSet<int> visited)
        {
            if (startNode.Id == targetNode.Id)
                return true;

            if (visited.Contains(startNode.Id))
                return false;

            visited.Add(startNode.Id);

            // 检查所有从当前节点输出的连接
            var outputConnections = _connections.Where(c => c.OutputNode?.Id == startNode.Id);
            foreach (var connection in outputConnections)
            {
                if (connection.InputNode != null && HasPathToNode(connection.InputNode, targetNode, visited))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// 检查并添加灵活端口
        /// </summary>
        private void CheckAndAddFlexiblePorts(Node node, string portName, NodePortType portType)
        {
            var port = portType == NodePortType.Input ? node.GetInputPort(portName) : node.GetOutputPort(portName);
            if (port == null || !port.IsFlexible)
            {
                return;
            }


            // 检查是否需要添加新的灵活端口
            if (node.ShouldAddFlexiblePort(portType, port.DataType))
            {
                try
                {
                    var newPort = node.AddFlexiblePort(portType, port.DataType);

                    // 触发端口添加事件，通知UI更新
                    FlexiblePortAdded?.Invoke(node, newPort);
                }
                catch (Exception ex)
                {
                }
            }
            else
            {
            }
        }

        /// <summary>
        /// 自动连接两个节点的所有匹配端口
        /// </summary>
        public int AutoConnectNodes(Node outputNode, Node inputNode)
        {
            var connectionsCreated = 0;
            var outputPorts = outputNode.OutputPorts.ToList();
            var inputPorts = inputNode.InputPorts.ToList();

            var maxConnections = Math.Min(outputPorts.Count, inputPorts.Count);

            for (int i = 0; i < maxConnections; i++)
            {
                var outputPort = outputPorts[i];
                var inputPort = inputPorts[i];

                // 移除类型检查 - 允许任意类型自动连接
                var connection = CreateConnection(outputNode, outputPort.Name, inputNode, inputPort.Name);
                if (connection != null)
                {
                    connectionsCreated++;
                }
            }

            return connectionsCreated;
        }

        /// <summary>
        /// 更新所有连接的几何位置
        /// </summary>
        public void UpdateAllConnectionGeometry()
        {
            foreach (var connection in _connections)
            {
                connection.UpdateGeometry();
            }
        }

        /// <summary>
        /// 获取节点的所有输入连接
        /// </summary>
        public IEnumerable<NodeConnection> GetInputConnections(Node node)
        {
            return _connections.Where(c => c.InputNode?.Id == node.Id);
        }

        /// <summary>
        /// 获取节点的所有输出连接
        /// </summary>
        public IEnumerable<NodeConnection> GetOutputConnections(Node node)
        {
            return _connections.Where(c => c.OutputNode?.Id == node.Id);
        }

        /// <summary>
        /// 检查连接的类型兼容性并触发错误事件
        /// </summary>
        private void CheckConnectionTypeCompatibility(NodePort? outputPort, NodePort? inputPort)
        {
            if (outputPort == null || inputPort == null)
                return;

            // 检查类型是否兼容
            if (!PortTypeDefinitions.AreTypesCompatible(outputPort.DataType, inputPort.DataType))
            {
                var errorMessage = $"类型不匹配: {outputPort.DataType} -> {inputPort.DataType}";

                // 触发连接错误事件
                ConnectionErrorDetected?.Invoke(errorMessage);
            }
            else
            {
                // 类型匹配，清除错误状态
                ConnectionErrorDetected?.Invoke(string.Empty);
            }
        }

        /// <summary>
        /// 检查所有现有连接的类型兼容性
        /// </summary>
        public void ValidateAllConnections()
        {
            var errorMessages = new List<string>();

            foreach (var connection in _connections)
            {
                var outputPort = connection.GetOutputPort();
                var inputPort = connection.GetInputPort();

                if (outputPort != null && inputPort != null)
                {
                    if (!PortTypeDefinitions.AreTypesCompatible(outputPort.DataType, inputPort.DataType))
                    {
                        var errorMessage = $"类型不匹配: {connection.OutputNode?.Title}.{connection.OutputPortName}({outputPort.DataType}) -> {connection.InputNode?.Title}.{connection.InputPortName}({inputPort.DataType})";
                        errorMessages.Add(errorMessage);
                    }
                }
            }

            if (errorMessages.Count > 0)
            {
                var combinedMessage = $"发现 {errorMessages.Count} 个类型不匹配的连接";
                ConnectionErrorDetected?.Invoke(combinedMessage);
            }
            else
            {
                ConnectionErrorDetected?.Invoke(string.Empty);
            }
        }
    }
}
