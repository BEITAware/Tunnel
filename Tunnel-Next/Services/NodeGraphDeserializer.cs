using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Tunnel_Next.Models;
using Tunnel_Next.Services.Scripting;

namespace Tunnel_Next.Services
{
    /// <summary>
    /// 节点图反序列化器 - 负责从JSON恢复节点图
    /// </summary>
    public class NodeGraphDeserializer
    {
        private readonly TunnelExtensionScriptManager _scriptManager;
        private readonly NodeIdManager _nodeIdManager;

        public NodeGraphDeserializer(TunnelExtensionScriptManager scriptManager)
        {
            _scriptManager = scriptManager ?? throw new ArgumentNullException(nameof(scriptManager));
            _nodeIdManager = NodeIdManager.Instance;
        }

        /// <summary>
        /// 反序列化节点图
        /// </summary>
        /// <param name="json">JSON字符串</param>
        /// <returns>反序列化的节点图</returns>
        public NodeGraph DeserializeNodeGraph(string json)
        {
            try
            {

                var data = JsonConvert.DeserializeObject<JObject>(json);
                if (data == null)
                    throw new InvalidOperationException("JSON数据为空");

                // 检查版本
                var version = data["version"]?.ToString() ?? "1.0";

                // 创建节点图
                var nodeGraph = new NodeGraph
                {
                    Name = data["name"]?.ToString() ?? "未命名节点图",
                    FilePath = data["filePath"]?.ToString() ?? string.Empty,
                    LastModified = ParseDateTimeSafe(data["lastModified"])
                };

                // 恢复视口信息
                if (data["viewport"] is JObject viewport)
                {
                    nodeGraph.ViewportX = viewport["x"]?.ToObject<double>() ?? 0;
                    nodeGraph.ViewportY = viewport["y"]?.ToObject<double>() ?? 0;
                    nodeGraph.ZoomLevel = viewport["zoomLevel"]?.ToObject<double>() ?? 1.0;
                }

                // 恢复元数据
                if (data["metadata"] is JObject metadata)
                {
                    nodeGraph.Metadata = metadata.ToObject<Dictionary<string, object>>() ?? new Dictionary<string, object>();
                }

                // 反序列化节点
                if (data["nodes"] is JArray nodesArray)
                {
                    DeserializeNodes(nodesArray, nodeGraph.Nodes);
                }

                // 反序列化连接
                if (data["connections"] is JArray connectionsArray)
                {
                    DeserializeConnections(connectionsArray, nodeGraph.Connections, nodeGraph.Nodes);
                }

                return nodeGraph;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"节点图反序列化失败: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// 反序列化节点集合
        /// </summary>
        private void DeserializeNodes(JArray nodesArray, ObservableCollection<Node> nodes)
        {
            foreach (var nodeToken in nodesArray)
            {
                try
                {
                    if (nodeToken is not JObject nodeData)
                        continue;

                    // 创建节点
                    var node = new Node
                    {
                        Id = nodeData["id"]?.ToObject<int>() ?? _nodeIdManager.GenerateNodeId(),
                        Title = nodeData["title"]?.ToString() ?? "未知节点",
                        Category = nodeData["category"]?.ToString() ?? "通用",
                        Description = nodeData["description"]?.ToString() ?? "",
                        Color = FixColorString(nodeData["color"]?.ToString()),
                        ScriptPath = nodeData["scriptPath"]?.ToString() ?? ""
                    };

                    // 确保ID不冲突
                    _nodeIdManager.EnsureIdNotConflict(node.Id);

                    // 恢复位置和大小
                    if (nodeData["position"] is JObject position)
                    {
                        node.X = position["x"]?.ToObject<double>() ?? 0;
                        node.Y = position["y"]?.ToObject<double>() ?? 0;
                    }

                    if (nodeData["size"] is JObject size)
                    {
                        node.Width = size["width"]?.ToObject<double>() ?? 120;
                        node.Height = size["height"]?.ToObject<double>() ?? 80;
                    }

                    // 恢复端口
                    DeserializeNodePorts(nodeData, node);

                    // 恢复脚本实例和参数
                    RestoreScriptInstance(nodeData, node);

                    nodes.Add(node);
                }
                catch (Exception ex)
                {
                    // 继续处理其他节点
                }
            }
        }

        /// <summary>
        /// 反序列化节点端口
        /// </summary>
        private void DeserializeNodePorts(JObject nodeData, Node node)
        {
            // Reset flexible port type lists
            node.FlexibleInputTypes.Clear();
            node.FlexibleOutputTypes.Clear();

            // 恢复输入端口
            if (nodeData["inputPorts"] is JArray inputPorts)
            {
                foreach (var portToken in inputPorts)
                {
                    if (portToken is JObject portData)
                    {
                        var port = new NodePort
                        {
                            Name = portData["name"]?.ToString() ?? "未知端口",
                            DataType = Enum.TryParse<NodePortDataType>(portData["dataType"]?.ToString(), out var dataType) ? dataType : NodePortDataType.Any,
                            Description = portData["description"]?.ToString() ?? "",
                            IsFlexible = portData["isFlexible"]?.ToObject<bool>() ?? false,
                            IsInput = true
                        };
                        node.InputPorts.Add(port);
                        if (port.IsFlexible)
                            node.FlexibleInputTypes.Add(port.DataType);
                    }
                }
            }

            // 恢复输出端口
            if (nodeData["outputPorts"] is JArray outputPorts)
            {
                foreach (var portToken in outputPorts)
                {
                    if (portToken is JObject portData)
                    {
                        var port = new NodePort
                        {
                            Name = portData["name"]?.ToString() ?? "未知端口",
                            DataType = Enum.TryParse<NodePortDataType>(portData["dataType"]?.ToString(), out var dataType) ? dataType : NodePortDataType.Any,
                            Description = portData["description"]?.ToString() ?? "",
                            IsFlexible = portData["isFlexible"]?.ToObject<bool>() ?? false,
                            IsInput = false
                        };
                        node.OutputPorts.Add(port);
                        if (port.IsFlexible)
                            node.FlexibleOutputTypes.Add(port.DataType);
                    }
                }
            }
        }

        /// <summary>
        /// 恢复脚本实例
        /// </summary>
        private void RestoreScriptInstance(JObject nodeData, Node node)
        {
            try
            {
                var scriptPath = node.ScriptPath;
                if (string.IsNullOrEmpty(scriptPath))
                    return;


                // 尝试多种方式查找脚本
                ITunnelExtensionScript? scriptInstance = null;

                // 方法1: 直接使用原始路径
                scriptInstance = _scriptManager.CreateTunnelExtensionScriptInstance(scriptPath);
                if (scriptInstance != null)
                {
                }
                else
                {

                    // 方法2: 使用文件名
                    var fileName = Path.GetFileName(scriptPath);
                    scriptInstance = _scriptManager.CreateTunnelExtensionScriptInstance(fileName);
                    if (scriptInstance != null)
                    {
                    }
                    else
                    {

                        // 方法3: 通过脚本名称查找
                        var availableScripts = _scriptManager.GetAvailableTunnelExtensionScripts();
                        var matchingScript = availableScripts.Values.FirstOrDefault(s => s.Name == node.Title);
                        if (matchingScript != null)
                        {
                            var relativePath = Path.GetRelativePath(_scriptManager.UserScriptsFolder, matchingScript.FilePath);
                            scriptInstance = _scriptManager.CreateTunnelExtensionScriptInstance(relativePath);
                            if (scriptInstance != null)
                            {
                                // 更新节点的脚本路径为正确的路径
                                node.ScriptPath = matchingScript.FilePath;
                            }
                        }
                    }
                }

                if (scriptInstance == null)
                {
                    return;
                }


                // 恢复脚本参数
                if (nodeData["scriptParameters"] is JObject scriptParameters)
                {
                    var parameterDict = scriptParameters.ToObject<Dictionary<string, object>>() ?? new Dictionary<string, object>();

                    foreach (var param in parameterDict)
                    {
                    }

                    scriptInstance.DeserializeParameters(parameterDict);
                }

                // 重建节点参数（从脚本实例获取当前参数值）
                RebuildNodeParameters(node, scriptInstance);

                // 创建ViewModel
                var viewModel = scriptInstance.CreateViewModel();
                viewModel.NodeId = node.Id;

                // 设置节点的Tag和ViewModel
                node.Tag = scriptInstance;
                node.ViewModel = viewModel;

            }
            catch (Exception ex)
            {
            }
        }

        /// <summary>
        /// 重建节点参数（从脚本实例获取当前参数值）
        /// </summary>
        private void RebuildNodeParameters(Node node, ITunnelExtensionScript scriptInstance)
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

        /// <summary>
        /// 反序列化连接集合
        /// </summary>
        private void DeserializeConnections(JArray connectionsArray, ObservableCollection<NodeConnection> connections, ObservableCollection<Node> nodes)
        {
            // 创建节点ID到节点的映射
            var nodeMap = nodes.ToDictionary(n => n.Id, n => n);

            foreach (var connectionToken in connectionsArray)
            {
                try
                {
                    if (connectionToken is not JObject connectionData)
                        continue;

                    var outputNodeId = connectionData["outputNodeId"]?.ToObject<int>() ?? 0;
                    var outputPortName = connectionData["outputPortName"]?.ToString() ?? "";
                    var inputNodeId = connectionData["inputNodeId"]?.ToObject<int>() ?? 0;
                    var inputPortName = connectionData["inputPortName"]?.ToString() ?? "";

                    // 查找节点
                    if (!nodeMap.TryGetValue(outputNodeId, out var outputNode))
                    {
                        continue;
                    }

                    if (!nodeMap.TryGetValue(inputNodeId, out var inputNode))
                    {
                        continue;
                    }

                    // 查找端口
                    var outputPort = outputNode.OutputPorts.FirstOrDefault(p => p.Name == outputPortName);
                    var inputPort = inputNode.InputPorts.FirstOrDefault(p => p.Name == inputPortName);

                    if (outputPort == null)
                    {
                        continue;
                    }

                    if (inputPort == null)
                    {
                        continue;
                    }

                    // 创建连接
                    var connection = new NodeConnection
                    {
                        OutputNode = outputNode,
                        OutputPortName = outputPortName,
                        InputNode = inputNode,
                        InputPortName = inputPortName
                    };

                    // 建立端口连接关系
                    outputPort.AddConnection(inputPort);
                    inputPort.AddConnection(outputPort);

                    connections.Add(connection);
                }
                catch (Exception ex)
                {
                    // 继续处理其他连接
                }
            }
        }

        /// <summary>
        /// 安全解析日期，若格式无效则返回 DateTime.Now
        /// </summary>
        private static DateTime ParseDateTimeSafe(Newtonsoft.Json.Linq.JToken? token)
        {
            if (token == null) return DateTime.Now;

            var str = token.ToString();
            if (DateTime.TryParse(str, out var dt))
            {
                return dt;
            }
            return DateTime.Now;
        }

        /// <summary>
        /// 如果颜色字符串无法被 ColorConverter 解析，则返回默认颜色 #4A90E2
        /// </summary>
        private static string FixColorString(string? colorStr)
        {
            if (string.IsNullOrWhiteSpace(colorStr)) return "#4A90E2";

            try
            {
                var _ = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(colorStr!);
                return colorStr!;
            }
            catch
            {
                return "#4A90E2";
            }
        }
    }
}
