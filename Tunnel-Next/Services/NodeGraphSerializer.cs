using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Tunnel_Next.Models;
using Tunnel_Next.Services.Scripting;

namespace Tunnel_Next.Services
{
    /// <summary>
    /// 节点图序列化器 - 负责节点图的序列化和反序列化
    /// </summary>
    public class NodeGraphSerializer
    {
        private readonly TunnelExtensionScriptManager _scriptManager;

        public NodeGraphSerializer(TunnelExtensionScriptManager scriptManager)
        {
            _scriptManager = scriptManager ?? throw new ArgumentNullException(nameof(scriptManager));
        }

        /// <summary>
        /// 序列化节点图
        /// </summary>
        /// <param name="nodeGraph">要序列化的节点图</param>
        /// <returns>序列化后的JSON字符串</returns>
        public string SerializeNodeGraph(NodeGraph nodeGraph)
        {
            try
            {

                var serializedData = new
                {
                    version = "2.0", // 新版本格式
                    name = nodeGraph.Name,
                    filePath = nodeGraph.FilePath,
                    lastModified = nodeGraph.LastModified,
                    viewport = new
                    {
                        x = nodeGraph.ViewportX,
                        y = nodeGraph.ViewportY,
                        zoomLevel = nodeGraph.ZoomLevel
                    },
                    metadata = nodeGraph.Metadata,
                    nodes = SerializeNodes(nodeGraph.Nodes),
                    connections = SerializeConnections(nodeGraph.Connections)
                };

                var json = JsonConvert.SerializeObject(serializedData, Formatting.Indented, new JsonSerializerSettings
                {
                    ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
                    NullValueHandling = NullValueHandling.Ignore
                });

                return json;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"节点图序列化失败: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// 序列化节点集合
        /// </summary>
        private List<object> SerializeNodes(IEnumerable<Node> nodes)
        {
            var serializedNodes = new List<object>();

            foreach (var node in nodes)
            {
                try
                {

                    // 序列化基本节点信息
                    var nodeData = new Dictionary<string, object>
                    {
                        ["id"] = node.Id,
                        ["title"] = node.Title,
                        ["category"] = node.Category,
                        ["description"] = node.Description,
                        ["color"] = node.Color,
                        ["position"] = new { x = node.X, y = node.Y },
                        ["size"] = new { width = node.Width, height = node.Height },
                        ["scriptPath"] = node.ScriptPath
                    };


                    // 序列化端口信息
                    nodeData["inputPorts"] = node.InputPorts.Select(p => new
                    {
                        name = p.Name,
                        dataType = p.DataType.ToString(),
                        description = p.Description,
                        isFlexible = p.IsFlexible
                    }).ToList();

                    nodeData["outputPorts"] = node.OutputPorts.Select(p => new
                    {
                        name = p.Name,
                        dataType = p.DataType.ToString(),
                        description = p.Description,
                        isFlexible = p.IsFlexible
                    }).ToList();


                    // 序列化脚本参数（如果有TunnelExtension Script实例）
                    if (node.Tag is ITunnelExtensionScript scriptInstance)
                    {
                        try
                        {

                            // 使用超时机制防止序列化卡死
                            var serializationTask = Task.Run(() => {
                                var result = scriptInstance.SerializeParameters();
                                return result;
                            });
                            var timeoutTask = Task.Delay(TimeSpan.FromSeconds(5)); // 5秒超时

                            var completedTask = Task.WaitAny(serializationTask, timeoutTask);

                            if (completedTask == 0 && serializationTask.IsCompletedSuccessfully)
                            {
                                var scriptParameters = serializationTask.Result;
                                nodeData["scriptParameters"] = scriptParameters;
                                foreach (var param in scriptParameters)
                                {
                                }
                            }
                            else
                            {
                                nodeData["scriptParameters"] = new Dictionary<string, object>();
                            }
                        }
                        catch (Exception ex)
                        {
                            nodeData["scriptParameters"] = new Dictionary<string, object>();
                        }
                    }
                    else
                    {
                        nodeData["scriptParameters"] = new Dictionary<string, object>();
                    }

                    serializedNodes.Add(nodeData);
                }
                catch (Exception ex)
                {
                    // 继续处理其他节点
                }
            }

            return serializedNodes;
        }

        /// <summary>
        /// 序列化连接集合
        /// </summary>
        private List<object> SerializeConnections(IEnumerable<NodeConnection> connections)
        {
            return connections.Select(conn => (object)new
            {
                outputNodeId = conn.OutputNode?.Id ?? 0,
                outputPortName = conn.OutputPortName,
                inputNodeId = conn.InputNode?.Id ?? 0,
                inputPortName = conn.InputPortName
            }).ToList();
        }
    }
}
