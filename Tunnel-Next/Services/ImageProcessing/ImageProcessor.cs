using OpenCvSharp;
using System.Collections.Concurrent;
using Tunnel_Next.Models;
using Tunnel_Next.Services.Scripting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace Tunnel_Next.Services.ImageProcessing
{
    /// <summary>
    /// Revival Scripts图像处理核心服务
    /// 专为Revival Scripts节点系统设计，完全移除传统脚本支持
    /// </summary>
    public class ImageProcessor : IDisposable
    {
        private readonly ConcurrentDictionary<int, Dictionary<string, object>> _nodeOutputs = new();
        private readonly object _processingLock = new();
        private bool _disposed = false;
        private volatile bool _isProcessing = false;
        private readonly RevivalScriptManager _revivalScriptManager;
        private readonly IScriptContext _scriptContext;

        public ImageProcessor(RevivalScriptManager revivalScriptManager, IScriptContext? scriptContext = null)
        {
            _revivalScriptManager = revivalScriptManager ?? throw new ArgumentNullException(nameof(revivalScriptManager));
            _scriptContext = scriptContext ?? CreateDefaultContext();

        }

        /// <summary>
        /// 节点图处理完成事件
        /// </summary>
        public event Action<NodeGraph>? OnNodeGraphProcessed;

        /// <summary>
        /// 处理Revival Scripts节点图
        /// </summary>
        public async Task<bool> ProcessNodeGraphAsync(NodeGraph nodeGraph)
        {
            return await ProcessNodeGraphAsync(nodeGraph, null);
        }

        /// <summary>
        /// 增量处理指定的节点及其下游节点
        /// </summary>
        /// <param name="nodeGraph">节点图</param>
        /// <param name="changedNodes">发生变化的节点列表</param>
        public async Task<bool> ProcessChangedNodesAsync(NodeGraph nodeGraph, IEnumerable<Node> changedNodes)
        {
            if (changedNodes == null || !changedNodes.Any())
                return true;

            var changedNodeIds = new HashSet<int>(changedNodes.Select(n => n.Id));
            return await ProcessNodeGraphAsync(nodeGraph, changedNodeIds);
        }

        /// <summary>
        /// 处理Revival Scripts节点图（支持选择性处理）
        /// </summary>
        /// <param name="nodeGraph">要处理的节点图</param>
        /// <param name="changedNodeIds">发生变化的节点ID集合，如果为null则处理所有标记为ToBeProcessed的节点</param>
        public async Task<bool> ProcessNodeGraphAsync(NodeGraph nodeGraph, HashSet<int>? changedNodeIds)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(ImageProcessor));

            if (nodeGraph == null || !nodeGraph.Nodes.Any())
                return false;

            if (_isProcessing)
            {
                return false;
            }

            return await Task.Run(async () =>
            {
                try
                {
                    _isProcessing = true;
                    var startTime = DateTime.Now;

                    // 如果是首次处理（changedNodeIds为null），标记所有节点需要处理
                    if (changedNodeIds == null)
                    {
                        nodeGraph.MarkAllNodesForProcessing();
                        ClearOutputs(); // 清除所有缓存
                    }

                    // 获取需要处理的节点
                    var nodesToProcess = nodeGraph.GetNodesToProcess();
                    var processingMode = changedNodeIds == null ?
                        $"完全处理({nodesToProcess.Count}个节点)" :
                        $"选择性处理({nodesToProcess.Count}个待处理节点)";

                    // 调试信息：记录哪些节点被标记为需要处理
                    if (changedNodeIds != null && nodesToProcess.Any())
                    {
                        var markedNodeTitles = string.Join(", ", nodesToProcess.Select(n => n.Title));
                    }

                    if (!nodesToProcess.Any())
                    {
                        return true;
                    }

                    // 移除UI耦合 - 不再直接操作UI控件

                    // 使用选择性拓扑排序算法
                    var sortedNodes = SelectiveTopologicalSort(nodeGraph, nodesToProcess);
                    if (sortedNodes == null || !sortedNodes.Any())
                    {
                        return false;
                    }

                    var processingTime = DateTime.Now;

                    // 记录每个节点的处理时间
                    var nodeProcessingTimes = new Dictionary<string, double>();
                    var processedCount = 0;

                    // 按顺序处理每个Revival Script节点
                    foreach (var node in sortedNodes)
                    {
                        var nodeStartTime = DateTime.Now;

                        try
                        {
                            await Task.Run(() =>
                            {
                                lock (_processingLock)
                                {
                                    ProcessRevivalScriptNode(node, nodeGraph);
                                }
                            });

                            // 更新节点状态
                            node.IsProcessed = true;
                            node.ToBeProcessed = false; // 清除处理标记
                            processedCount++;

                            var nodeEndTime = DateTime.Now;
                            var nodeTime = (nodeEndTime - nodeStartTime).TotalMilliseconds;
                            nodeProcessingTimes[node.Title] = nodeTime;

                        }
                        catch (Exception nodeEx)
                        {
                            node.HasError = true;
                            node.ErrorMessage = nodeEx.Message;

                            // 继续处理其他节点，不中断整个流程
                            continue;
                        }
                    }

                    var endTime = DateTime.Now;
                    var totalTime = (endTime - startTime).TotalMilliseconds;


                    // 输出详细的节点处理时间
                    if (nodeProcessingTimes.Any())
                    {
                        foreach (var kvp in nodeProcessingTimes.OrderByDescending(x => x.Value))
                        {
                        }
                    }

                    // 移除UI耦合 - 只触发事件，不直接操作UI
                    OnNodeGraphProcessed?.Invoke(nodeGraph);

                    return processedCount > 0; // 只要有节点成功处理就返回true
                }
                catch (Exception ex)
                {

                    // 移除UI耦合 - 异常情况下只记录日志

                    return false;
                }
                finally
                {
                    _isProcessing = false;
                }
            });
        }

        /// <summary>
        /// 处理单个Revival Script节点
        /// </summary>
        private void ProcessRevivalScriptNode(Node node, NodeGraph nodeGraph)
        {
            try
            {

                // 清除节点错误状态，预先假设本次会成功
                node.HasError = false;
                node.ErrorMessage = string.Empty;

                // 准备输入数据
                var inputs = PrepareNodeInputs(node, nodeGraph);

                // 执行Revival Script
                var outputs = ExecuteRevivalScript(node, inputs, nodeGraph); // This can return null if script fails

                if (outputs != null)
                {
                    // 脚本执行成功（可能返回空字典，表示无输出，但不是错误）
                    _nodeOutputs[node.Id] = outputs; // Cache the output

                    // 更新节点的ProcessedOutputs
                    node.ProcessedOutputs.Clear(); // Clear previous outputs first
                    foreach (var output in outputs)
                    {
                        node.ProcessedOutputs[output.Key] = output.Value;
                    }

                    if (outputs.Count > 0)
                    {
                        foreach (var output in outputs)
                        {
                            // The problematic logging is here. Let's make it safer.
                            string outputValueTypeName = output.Value?.GetType().Name ?? "null";
                            if (output.Value is Mat mat)
                            {
                                if (!mat.IsDisposed && !mat.Empty())
                                {
                                    try
                                    {
                                    }
                                    catch (Exception ex) // Catch potential access violation here too as a last resort
                                    {
                                        node.HasError = true;
                                        node.ErrorMessage = $"访问输出Mat属性失败: {ex.Message}";
                                    }
                                }
                                else
                                {
                                    if (mat.IsDisposed) node.ErrorMessage = $"输出Mat '{output.Key}' 已被释放。";
                                }
                            }
                        }
                    }
                }
                else
                {
                    // 脚本执行失败 (ExecuteRevivalScript returned null due to an exception)
                    node.HasError = true; // Error state should have been set by ExecuteRevivalScript's catch block
                    // Ensure ErrorMessage is set if ExecuteRevivalScript didn't set it on the node directly
                    if (string.IsNullOrEmpty(node.ErrorMessage)) node.ErrorMessage = "脚本执行失败";

                    _nodeOutputs.TryRemove(node.Id, out _); // Remove from cache
                    node.ProcessedOutputs.Clear(); // CRITICAL: Clear stale outputs from the node object
                }
            }
            catch (Exception ex) // Catch any other unexpected error during this processing step
            {
                node.HasError = true;
                node.ErrorMessage = $"节点处理失败: {ex.Message}";
                _nodeOutputs.TryRemove(node.Id, out _);
                node.ProcessedOutputs.Clear();
            }
        }

        /// <summary>
        /// 执行Revival Script
        /// </summary>
        private Dictionary<string, object>? ExecuteRevivalScript(Node node, Dictionary<string, object> inputs, NodeGraph nodeGraph)
        {
            try
            {
                // 使用鲁棒的实例获取机制
                var scriptInstance = ScriptInstanceManager.Instance.GetRobustScriptInstance(node);
                if (scriptInstance == null)
                {
                    return null;
                }

                // 确保参数同步
                ScriptInstanceManager.Instance.EnsureParameterSynchronization(scriptInstance, node);

                // 1. 收集和合并上游元数据
                var upstreamMetadata = CollectUpstreamMetadata(node, nodeGraph);

                // 2. 让脚本提取所需的元数据
                scriptInstance.ExtractMetadata(upstreamMetadata);

                // 3. 执行脚本核心处理逻辑
                var outputs = scriptInstance.Process(inputs, _scriptContext);

                if (outputs != null)
                {
                    // 4. 处理元数据注入和传递
                    outputs = ProcessMetadataForOutputs(scriptInstance, node, outputs, upstreamMetadata);

                    // 5. 调试输出
                    foreach (var output in outputs)
                    {
                        if (output.Value is Mat mat)
                        {
                            // Mat输出调试信息
                        }
                        else
                        {
                            // 其他类型输出调试信息
                        }
                    }
                    return outputs;
                }
                else
                {
                    // 即使没有输出，也要处理元数据
                    var emptyOutputs = new Dictionary<string, object>();
                    return ProcessMetadataForOutputs(scriptInstance, node, emptyOutputs, upstreamMetadata);
                }
            }
            catch (Exception ex)
            {
                node.HasError = true;
                node.ErrorMessage = $"脚本执行失败: {ex.Message}";

                // 详细的异常调试信息
                System.Diagnostics.Debug.WriteLine($"[脚本执行异常] 节点 {node.Title}({node.Id}) 执行失败:");
                System.Diagnostics.Debug.WriteLine($"  异常类型: {ex.GetType().Name}");
                System.Diagnostics.Debug.WriteLine($"  异常消息: {ex.Message}");
                if (ex.InnerException != null)
                {
                    System.Diagnostics.Debug.WriteLine($"  内部异常: {ex.InnerException.Message}");
                }
                System.Diagnostics.Debug.WriteLine($"  堆栈跟踪: {ex.StackTrace}");

                return null;
            }
        }

        /// <summary>
        /// 设置Revival Script参数
        /// </summary>
        private static void SetRevivalScriptParameters(IRevivalScript scriptInstance, Node node)
        {
            // 对于Revival Script，我们不需要从node.Parameters设置参数
            // 因为参数已经通过UI控件直接设置到脚本实例中了
            // 这里只是记录当前的参数值用于调试

            var scriptType = scriptInstance.GetType();
            var properties = scriptType.GetProperties()
                .Where(p => p.GetCustomAttribute<Services.Scripting.ScriptParameterAttribute>() != null);

            foreach (var property in properties)
            {
                try
                {
                    var currentValue = property.GetValue(scriptInstance);
                }
                catch (Exception ex)
                {
                }
            }
        }

        /// <summary>
        /// 转换参数值类型
        /// </summary>
        private static object? ConvertParameterValue(object? value, Type targetType)
        {
            if (value == null)
                return null;

            if (targetType.IsAssignableFrom(value.GetType()))
                return value;

            try
            {
                // 处理可空类型
                if (targetType.IsGenericType && targetType.GetGenericTypeDefinition() == typeof(Nullable<>))
                {
                    targetType = Nullable.GetUnderlyingType(targetType) ?? targetType;
                }

                // 字符串转换
                if (value is string stringValue)
                {
                    if (targetType == typeof(bool))
                        return bool.Parse(stringValue);
                    if (targetType == typeof(int))
                        return int.Parse(stringValue);
                    if (targetType == typeof(double))
                        return double.Parse(stringValue);
                    if (targetType == typeof(float))
                        return float.Parse(stringValue);
                }

                return Convert.ChangeType(value, targetType);
            }
            catch
            {
                return value; // 如果转换失败，返回原值
            }
        }

        /// <summary>
        /// 收集上游元数据
        /// </summary>
        private Dictionary<string, object> CollectUpstreamMetadata(Node node, NodeGraph nodeGraph)
        {
            var metadataManager = new MetadataManager();
            var allMetadata = new List<Dictionary<string, object>>();

            // 遍历节点的输入端口，直接从上游节点的输出中获取元数据
            foreach (var inputPort in node.InputPorts)
            {
                // 查找连接到此输入端口的输出端口
                var connection = nodeGraph.Connections.FirstOrDefault(c =>
                    c.InputNode?.Id == node.Id && c.InputPortName == inputPort.Name);

                if (connection?.OutputNode != null)
                {
                    var outputNode = connection.OutputNode;
                    var outputPortName = connection.OutputPortName;

                    // 从输出节点获取完整的输出数据（包含元数据）
                    if (_nodeOutputs.TryGetValue(outputNode.Id, out var outputData))
                    {
                        // 检查是否有元数据
                        if (outputData.TryGetValue("_metadata", out var metadata) &&
                            metadata is Dictionary<string, object> metadataDict)
                        {
                            allMetadata.Add(metadataDict);
                        }
                    }
                }
            }

            // 合并所有上游元数据
            return metadataManager.MergeMetadata(allMetadata.ToArray());
        }

        /// <summary>
        /// 处理输出的元数据注入和传递
        /// </summary>
        private Dictionary<string, object> ProcessMetadataForOutputs(
            IRevivalScript scriptInstance,
            Node node,
            Dictionary<string, object> outputs,
            Dictionary<string, object> upstreamMetadata)
        {
            var metadataManager = new MetadataManager();

            // 1. 清洗上游元数据
            var cleanOptions = new MetadataCleanOptions
            {
                RemoveNullValues = true,
                CleanNodePath = false,  // 不再自动管理节点路径
                CleanProcessingHistory = false,  // 不再自动管理处理历史
                MaxHistoryRecords = 50,
                MergeDuplicateKeys = true
            };
            var cleanedUpstreamMetadata = metadataManager.CleanMetadata(upstreamMetadata, cleanOptions);

            // 2. 从清洗后的上游元数据开始
            var currentMetadata = metadataManager.CopyMetadata(cleanedUpstreamMetadata);

            // 3. 让脚本注入自定义元数据（不覆盖已有键）
            currentMetadata = scriptInstance.InjectMetadata(currentMetadata);

            // 4. 让脚本生成/覆盖特定元数据
            currentMetadata = scriptInstance.GenerateMetadata(currentMetadata);

            // 5. 最终清洗元数据
            currentMetadata = metadataManager.CleanMetadata(currentMetadata, cleanOptions);

            // 6. 将元数据附加到所有输出
            var processedOutputs = new Dictionary<string, object>(outputs);
            processedOutputs["_metadata"] = currentMetadata;

            return processedOutputs;
        }

        /// <summary>
        /// 准备节点输入数据
        /// </summary>
        private Dictionary<string, object> PrepareNodeInputs(Node node, NodeGraph nodeGraph)
        {
            var inputs = new Dictionary<string, object>();

            // 遍历节点的输入端口
            foreach (var inputPort in node.InputPorts)
            {
                // 查找连接到此输入端口的输出端口
                var connection = nodeGraph.Connections.FirstOrDefault(c =>
                    c.InputNode?.Id == node.Id && c.InputPortName == inputPort.Name);

                if (connection?.OutputNode != null)
                {
                    var outputNode = connection.OutputNode;
                    var outputPortName = connection.OutputPortName;

                    // 从输出节点获取数据
                    if (_nodeOutputs.TryGetValue(outputNode.Id, out var outputData) &&
                        outputData.TryGetValue(outputPortName, out var portData))
                    {
                        inputs[inputPort.Name] = portData;
                        if (portData is Mat mat)
                        {
                            // Mat输入调试信息
                        }
                        else
                        {
                            // 其他类型输入调试信息
                        }
                    }
                    else
                    {
                        // 输入数据不可用的调试信息
                    }
                }
            }

            return inputs;
        }

        /// <summary>
        /// 获取节点输出数据
        /// </summary>
        public Dictionary<string, object>? GetNodeOutput(int nodeId)
        {
            return _nodeOutputs.TryGetValue(nodeId, out var output) ? output : null;
        }

        /// <summary>
        /// 获取节点元数据（为了兼容性保留）
        /// </summary>
        public Dictionary<string, object>? GetNodeMetadata(int nodeId)
        {
            // 在新的Revival Scripts系统中，元数据包含在输出数据中
            if (_nodeOutputs.TryGetValue(nodeId, out var output))
            {
                // 尝试从输出中获取元数据
                if (output.TryGetValue("_metadata", out var metadata) && metadata is Dictionary<string, object> metadataDict)
                {
                    return metadataDict;
                }
            }

            // 如果没有找到元数据，返回null
            return null;
        }



        /// <summary>
        /// 简单直接的拓扑排序算法，完全模仿Python原型的精髓，去掉循环依赖检测
        /// </summary>
        private static List<Node>? TopologicalSort(NodeGraph nodeGraph, HashSet<int>? changedNodeIds = null)
        {
            if (nodeGraph == null || !nodeGraph.Nodes.Any())
                return new List<Node>();


            var result = new List<Node>();
            var processed = new HashSet<int>();
            var inDegree = new Dictionary<int, int>();

            // 1. 计算每个节点的入度
            foreach (var node in nodeGraph.Nodes)
            {
                inDegree[node.Id] = 0;
            }

            foreach (var connection in nodeGraph.Connections)
            {
                if (connection.InputNode != null)
                {
                    inDegree[connection.InputNode.Id]++;
                }
            }

            foreach (var kvp in inDegree)
            {
                var node = nodeGraph.Nodes.FirstOrDefault(n => n.Id == kvp.Key);
            }

            // 2. 找到入度为0的节点（起始节点）
            var currentLayer = nodeGraph.Nodes.Where(n => inDegree[n.Id] == 0).ToList();

            if (currentLayer.Count == 0 && nodeGraph.Nodes.Count > 0)
            {
                // 如果没有入度为0的节点但有节点存在，选择第一个节点作为起点
                currentLayer.Add(nodeGraph.Nodes.First());
            }


            // 3. 逐层处理节点，直到所有节点都被处理
            var maxIterations = nodeGraph.Nodes.Count + 10; // 给一些余量
            var iterations = 0;

            while (currentLayer.Count > 0 && iterations < maxIterations)
            {
                iterations++;

                // 将当前层的节点添加到结果中
                result.AddRange(currentLayer);
                foreach (var node in currentLayer)
                {
                    processed.Add(node.Id);
                }

                // 找到下一层的节点
                var nextLayer = new List<Node>();
                foreach (var node in currentLayer)
                {
                    // 找到当前节点的所有输出连接
                    var outputConnections = nodeGraph.Connections.Where(c => c.OutputNode?.Id == node.Id);

                    foreach (var connection in outputConnections)
                    {
                        if (connection.InputNode != null)
                        {
                            var inputNode = connection.InputNode;
                            inDegree[inputNode.Id]--;

                            // 如果输入节点的入度变为0且未处理过，加入下一层
                            if (inDegree[inputNode.Id] == 0 && !processed.Contains(inputNode.Id))
                            {
                                if (!nextLayer.Contains(inputNode))
                                {
                                    nextLayer.Add(inputNode);
                                }
                            }
                        }
                    }
                }

                // 如果没有找到新节点但还有未处理的节点，选择一个未处理的节点
                if (nextLayer.Count == 0 && processed.Count < nodeGraph.Nodes.Count)
                {
                    var unprocessedNode = nodeGraph.Nodes.FirstOrDefault(n => !processed.Contains(n.Id));
                    if (unprocessedNode != null)
                    {
                        nextLayer.Add(unprocessedNode);
                    }
                }

                currentLayer = nextLayer;
            }

            // 简单检查：确保处理了所有节点
            var unprocessedNodes = nodeGraph.Nodes.Where(n => !processed.Contains(n.Id)).ToList();
            if (unprocessedNodes.Any())
            {
                foreach (var node in unprocessedNodes)
                {
                    result.Add(node);
                }
            }

            return result;
        }

        /// <summary>
        /// 选择性拓扑排序算法 - 处理标记为ToBeProcessed的节点及其必要的上游依赖
        /// </summary>
        private static List<Node>? SelectiveTopologicalSort(NodeGraph nodeGraph, List<Node> nodesToProcess)
        {
            if (nodeGraph == null || !nodeGraph.Nodes.Any() || !nodesToProcess.Any())
                return new List<Node>();


            var result = new List<Node>();
            var processed = new HashSet<int>();
            var inDegree = new Dictionary<int, int>();
            var nodesToProcessSet = new HashSet<int>(nodesToProcess.Select(n => n.Id));

            // 1. 收集所有需要处理的节点（包括上游依赖）
            var allNodesToProcess = new HashSet<Node>(nodesToProcess);
            foreach (var node in nodesToProcess)
            {
                CollectUpstreamNodes(node, nodeGraph, allNodesToProcess);
            }


            // 2. 计算每个节点的入度（考虑所有节点）
            foreach (var node in nodeGraph.Nodes)
            {
                inDegree[node.Id] = 0;
            }

            foreach (var connection in nodeGraph.Connections)
            {
                if (connection.InputNode != null)
                {
                    inDegree[connection.InputNode.Id]++;
                }
            }

            // 3. 找到入度为0的节点作为起始点（从所有需要处理的节点中找）
            var currentLayer = allNodesToProcess.Where(n => inDegree[n.Id] == 0).ToList();

            if (currentLayer.Count == 0 && allNodesToProcess.Any())
            {
                // 如果没有入度为0的节点，选择第一个节点（可能存在循环依赖）
                currentLayer.Add(allNodesToProcess.First());
            }



            // 4. 逐层处理节点
            var maxIterations = nodeGraph.Nodes.Count + 10;
            var iterations = 0;

            while (currentLayer.Count > 0 && iterations < maxIterations)
            {
                iterations++;

                // 将当前层的所有节点添加到结果中（包括上游依赖节点）
                result.AddRange(currentLayer);

                foreach (var node in currentLayer)
                {
                    processed.Add(node.Id);
                    var nodeType = nodesToProcessSet.Contains(node.Id) ? "目标节点" : "依赖节点";
                }

                // 找到下一层的节点
                var nextLayer = new List<Node>();
                foreach (var node in currentLayer)
                {
                    var outputConnections = nodeGraph.Connections.Where(c => c.OutputNode?.Id == node.Id);

                    foreach (var connection in outputConnections)
                    {
                        if (connection.InputNode != null)
                        {
                            var inputNode = connection.InputNode;
                            inDegree[inputNode.Id]--;

                            // 只有在需要处理的节点集合中的节点才加入下一层
                            if (inDegree[inputNode.Id] == 0 && !processed.Contains(inputNode.Id) &&
                                allNodesToProcess.Contains(inputNode))
                            {
                                if (!nextLayer.Contains(inputNode))
                                {
                                    nextLayer.Add(inputNode);
                                }
                            }
                        }
                    }
                }

                currentLayer = nextLayer;
            }

            return result;
        }

        /// <summary>
        /// 收集节点的所有上游依赖节点
        /// </summary>
        private static void CollectUpstreamNodes(Node node, NodeGraph nodeGraph, HashSet<Node> upstreamNodes, HashSet<int>? visited = null)
        {
            if (visited == null)
                visited = new HashSet<int>();

            if (visited.Contains(node.Id))
                return;

            visited.Add(node.Id);

            // 查找所有输入到此节点的连接
            var inputConnections = nodeGraph.Connections.Where(c => c.InputNode?.Id == node.Id);

            foreach (var connection in inputConnections)
            {
                if (connection.OutputNode != null)
                {
                    upstreamNodes.Add(connection.OutputNode);
                    // 递归收集上游节点
                    CollectUpstreamNodes(connection.OutputNode, nodeGraph, upstreamNodes, visited);
                }
            }
        }

        /// <summary>
        /// 创建默认脚本上下文
        /// </summary>
        private static IScriptContext CreateDefaultContext()
        {
            var workFolder = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            var tempFolder = System.IO.Path.GetTempPath();
            var scriptsFolder = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Scripts");

            return new ScriptContext(
                workFolder,
                tempFolder,
                scriptsFolder,
                () => new NodeGraph(), // 默认空节点图
                _ => { }, // 空的处理节点图方法
                _ => new Dictionary<string, object>(), // 空的获取节点输入方法
                (_, _, _) => { } // 空的更新节点参数方法
            );
        }

        /// <summary>
        /// 清除所有输出缓存
        /// </summary>
        public void ClearOutputs()
        {
            foreach (var output in _nodeOutputs.Values)
            {
                // 释放Mat资源
                foreach (var item in output.Values)
                {
                    if (item is Mat mat)
                    {
                        mat?.Dispose();
                    }
                }
            }
            _nodeOutputs.Clear();
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            if (!_disposed)
            {
                ClearOutputs();
                _disposed = true;
            }
        }
    }
}
