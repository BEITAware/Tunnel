using System;
using System.Collections.Generic;
using System.Linq;
using Tunnel_Next.Models;
using Tunnel_Next.Services.Scripting;

namespace Tunnel_Next.Services
{
    /// <summary>
    /// 动态端口管理器 - 处理节点运行时端口变化
    /// </summary>
    public class DynamicPortManager
    {
        private readonly ConnectionManager _connectionManager;

        /// <summary>
        /// 端口变化事件 - 当端口被添加、移除或修改时触发
        /// </summary>
        public event Action<Node, PortChangeInfo>? PortChanged;

        public DynamicPortManager(ConnectionManager connectionManager)
        {
            _connectionManager = connectionManager;
        }

        /// <summary>
        /// 更新节点的端口定义
        /// </summary>
        /// <param name="node">要更新的节点</param>
        /// <param name="script">节点的脚本实例</param>
        /// <returns>端口变化信息</returns>
        public PortUpdateResult UpdateNodePorts(Node node, IRevivalScript script)
        {
            System.Diagnostics.Debug.WriteLine($"[DynamicPortManager] 开始更新节点端口: {node.Title}");

            var result = new PortUpdateResult();

            try
            {
                // 获取新的端口定义
                var newInputPorts = script.GetInputPorts();
                var newOutputPorts = script.GetOutputPorts();

                // 更新输入端口
                var inputChanges = UpdatePorts(node, node.InputPorts, newInputPorts, true);
                result.InputChanges.AddRange(inputChanges);

                // 更新输出端口
                var outputChanges = UpdatePorts(node, node.OutputPorts, newOutputPorts, false);
                result.OutputChanges.AddRange(outputChanges);

                // 处理连接变化
                var disconnectedConnections = HandleConnectionChanges(node, result);
                result.DisconnectedConnections.AddRange(disconnectedConnections);

                result.Success = true;
                System.Diagnostics.Debug.WriteLine($"[DynamicPortManager] 端口更新完成: 输入变化{result.InputChanges.Count}, 输出变化{result.OutputChanges.Count}, 断开连接{result.DisconnectedConnections.Count}");

                // 触发端口变化事件
                foreach (var change in result.InputChanges.Concat(result.OutputChanges))
                {
                    PortChanged?.Invoke(node, change);
                }
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.ErrorMessage = ex.Message;
                System.Diagnostics.Debug.WriteLine($"[DynamicPortManager] 端口更新失败: {ex.Message}");
            }

            return result;
        }

        /// <summary>
        /// 更新端口集合
        /// </summary>
        private List<PortChangeInfo> UpdatePorts(Node node, System.Collections.ObjectModel.ObservableCollection<NodePort> currentPorts, 
            Dictionary<string, PortDefinition> newPortDefinitions, bool isInput)
        {
            var changes = new List<PortChangeInfo>();
            var portType = isInput ? "输入" : "输出";

            System.Diagnostics.Debug.WriteLine($"[DynamicPortManager] 更新{portType}端口: 当前{currentPorts.Count}个, 新定义{newPortDefinitions.Count}个");

            // 创建端口名称到索引的映射
            var currentPortMap = currentPorts.Select((port, index) => new { port, index }).ToDictionary(x => x.port.Name, x => x.index);
            var newPortNames = newPortDefinitions.Keys.ToList();

            // 1. 处理需要保留的端口（按顺序匹配）
            var portsToKeep = new List<(NodePort port, int newIndex)>();
            var usedNewIndices = new HashSet<int>();

            for (int currentIndex = 0; currentIndex < currentPorts.Count; currentIndex++)
            {
                var currentPort = currentPorts[currentIndex];
                
                // 查找在新定义中的位置
                var newIndex = newPortNames.IndexOf(currentPort.Name);
                if (newIndex >= 0 && !usedNewIndices.Contains(newIndex))
                {
                    portsToKeep.Add((currentPort, newIndex));
                    usedNewIndices.Add(newIndex);
                    
                    // 更新端口属性
                    var newDef = newPortDefinitions[currentPort.Name];
                    UpdatePortProperties(currentPort, newDef);
                    
                    changes.Add(new PortChangeInfo
                    {
                        ChangeType = PortChangeType.Modified,
                        PortName = currentPort.Name,
                        IsInput = isInput,
                        OldIndex = currentIndex,
                        NewIndex = newIndex
                    });
                }
            }

            // 2. 移除不再需要的端口
            var portsToRemove = currentPorts.Where(p => !newPortNames.Contains(p.Name)).ToList();
            foreach (var port in portsToRemove)
            {
                var oldIndex = currentPorts.IndexOf(port);
                currentPorts.Remove(port);
                
                changes.Add(new PortChangeInfo
                {
                    ChangeType = PortChangeType.Removed,
                    PortName = port.Name,
                    IsInput = isInput,
                    OldIndex = oldIndex,
                    NewIndex = -1
                });
                
                System.Diagnostics.Debug.WriteLine($"[DynamicPortManager] 移除{portType}端口: {port.Name}");
            }

            // 3. 重新排列现有端口
            currentPorts.Clear();
            var finalPorts = new NodePort[newPortNames.Count];

            // 放置保留的端口到新位置
            foreach (var (port, newIndex) in portsToKeep)
            {
                finalPorts[newIndex] = port;
            }

            // 4. 添加新端口
            for (int i = 0; i < newPortNames.Count; i++)
            {
                if (finalPorts[i] == null)
                {
                    var portName = newPortNames[i];
                    var portDef = newPortDefinitions[portName];
                    
                    var newPort = CreatePortFromDefinition(portName, portDef, isInput);
                    finalPorts[i] = newPort;
                    
                    changes.Add(new PortChangeInfo
                    {
                        ChangeType = PortChangeType.Added,
                        PortName = portName,
                        IsInput = isInput,
                        OldIndex = -1,
                        NewIndex = i
                    });
                    
                    System.Diagnostics.Debug.WriteLine($"[DynamicPortManager] 添加{portType}端口: {portName}");
                }
            }

            // 5. 将最终端口添加到集合
            foreach (var port in finalPorts)
            {
                currentPorts.Add(port);
            }

            return changes;
        }

        /// <summary>
        /// 更新端口属性
        /// </summary>
        private void UpdatePortProperties(NodePort port, PortDefinition definition)
        {
            // 更新端口的数据类型和其他属性
            var newDataType = ConvertToNodePortDataType(definition.DataType);
            if (port.DataType != newDataType)
            {
                port.DataType = newDataType;
                System.Diagnostics.Debug.WriteLine($"[DynamicPortManager] 更新端口 {port.Name} 数据类型: {newDataType}");
            }

            if (port.Description != definition.Description)
            {
                port.Description = definition.Description;
            }
        }

        /// <summary>
        /// 从端口定义创建新端口
        /// </summary>
        private NodePort CreatePortFromDefinition(string name, PortDefinition definition, bool isInput)
        {
            return new NodePort
            {
                Name = name,
                DataType = ConvertToNodePortDataType(definition.DataType),
                Type = isInput ? NodePortType.Input : NodePortType.Output,
                Description = definition.Description,
                IsInput = isInput,
                IsFlexible = definition.IsFlexible
            };
        }

        /// <summary>
        /// 转换数据类型
        /// </summary>
        private NodePortDataType ConvertToNodePortDataType(string dataType)
        {
            return dataType.ToLower() switch
            {
                "f32bmp" => NodePortDataType.F32bmp,
                "mat" => NodePortDataType.F32bmp,
                "string" => NodePortDataType.String,
                "number" => NodePortDataType.Number,
                "bool" => NodePortDataType.Boolean,
                "any" => NodePortDataType.Any,
                _ => NodePortDataType.Any
            };
        }

        /// <summary>
        /// 处理连接变化
        /// </summary>
        private List<NodeConnection> HandleConnectionChanges(Node node, PortUpdateResult result)
        {
            var disconnectedConnections = new List<NodeConnection>();

            // 处理被移除端口的连接
            foreach (var change in result.InputChanges.Concat(result.OutputChanges))
            {
                if (change.ChangeType == PortChangeType.Removed)
                {
                    var connections = change.IsInput 
                        ? _connectionManager.GetInputConnections(node).Where(c => c.InputPortName == change.PortName)
                        : _connectionManager.GetOutputConnections(node).Where(c => c.OutputPortName == change.PortName);

                    foreach (var connection in connections.ToList())
                    {
                        _connectionManager.RemoveConnection(connection);
                        disconnectedConnections.Add(connection);
                        System.Diagnostics.Debug.WriteLine($"[DynamicPortManager] 断开连接: {connection.OutputNode?.Title}:{connection.OutputPortName} -> {connection.InputNode?.Title}:{connection.InputPortName}");
                    }
                }
            }

            return disconnectedConnections;
        }
    }

    /// <summary>
    /// 端口更新结果
    /// </summary>
    public class PortUpdateResult
    {
        public bool Success { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;
        public List<PortChangeInfo> InputChanges { get; set; } = new();
        public List<PortChangeInfo> OutputChanges { get; set; } = new();
        public List<NodeConnection> DisconnectedConnections { get; set; } = new();
    }

    /// <summary>
    /// 端口变化信息
    /// </summary>
    public class PortChangeInfo
    {
        public PortChangeType ChangeType { get; set; }
        public string PortName { get; set; } = string.Empty;
        public bool IsInput { get; set; }
        public int OldIndex { get; set; }
        public int NewIndex { get; set; }
    }

    /// <summary>
    /// 端口变化类型
    /// </summary>
    public enum PortChangeType
    {
        Added,      // 端口被添加
        Removed,    // 端口被移除
        Modified    // 端口被修改（位置或属性变化）
    }
}
