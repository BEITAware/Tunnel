using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Tunnel_Next.Models;
using Tunnel_Next.Services.ImageProcessing;
using Tunnel_Next.Services.Scripting;

namespace Tunnel_Next.Services
{
    /// <summary>
    /// 节点图解释器服务 - 加载并执行节点图，获取"返回"节点的输入值
    /// </summary>
    public class NodeGraphInterpreterService
    {
        private readonly FileService _fileService;
        private readonly RevivalScriptManager _revivalScriptManager;

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="fileService">文件服务</param>
        /// <param name="revivalScriptManager">Revival脚本管理器</param>
        /// <param name="workFolderService">工作文件夹服务</param>
        public NodeGraphInterpreterService(
            FileService fileService,
            RevivalScriptManager revivalScriptManager,
            WorkFolderService workFolderService)
        {
            _fileService = fileService ?? throw new ArgumentNullException(nameof(fileService));
            _revivalScriptManager = revivalScriptManager ?? throw new ArgumentNullException(nameof(revivalScriptManager));
        }

        /// <summary>
        /// 解释执行节点图
        /// </summary>
        /// <param name="nodeGraphPath">节点图文件的绝对路径</param>
        /// <returns>返回节点的输入值，如果没有找到返回节点则返回null</returns>
        public async Task<Dictionary<string, object>?> InterpretNodeGraphAsync(string nodeGraphPath)
        {
            if (string.IsNullOrEmpty(nodeGraphPath))
                throw new ArgumentException("节点图路径不能为空", nameof(nodeGraphPath));

            if (!File.Exists(nodeGraphPath))
                throw new FileNotFoundException($"节点图文件不存在: {nodeGraphPath}");

            try
            {
                // 1. 加载节点图
                var nodeGraph = await _fileService.LoadNodeGraphAsync(nodeGraphPath);
                if (nodeGraph == null)
                    throw new InvalidOperationException($"无法加载节点图: {nodeGraphPath}");

                // 2. 查找返回节点
                var returnNode = FindReturnNode(nodeGraph);
                if (returnNode == null)
                    return null; // 没有找到返回节点

                // 3. 使用ImageProcessor执行节点图
                var imageProcessor = new ImageProcessor(_revivalScriptManager);
                var success = await imageProcessor.ProcessNodeGraphAsync(nodeGraph, null);
                if (!success)
                    throw new InvalidOperationException("节点图执行失败");

                // 4. 获取返回节点的输入值
                var returnInputs = GetReturnNodeInputs(returnNode, nodeGraph, imageProcessor);

                return returnInputs;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"节点图解释执行失败: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// 查找名为"返回"的节点
        /// </summary>
        /// <param name="nodeGraph">节点图</param>
        /// <returns>返回节点，如果没有找到则返回null</returns>
        private Node? FindReturnNode(NodeGraph nodeGraph)
        {
            return nodeGraph.Nodes.FirstOrDefault(node =>
                string.Equals(node.Title, "返回", StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// 获取返回节点的输入值
        /// </summary>
        /// <param name="returnNode">返回节点</param>
        /// <param name="nodeGraph">节点图</param>
        /// <param name="imageProcessor">图像处理器</param>
        /// <returns>返回节点的输入值字典</returns>
        private Dictionary<string, object> GetReturnNodeInputs(Node returnNode, NodeGraph nodeGraph, ImageProcessor imageProcessor)
        {
            var inputs = new Dictionary<string, object>();

            // 遍历返回节点的所有输入端口
            foreach (var inputPort in returnNode.InputPorts)
            {
                // 查找连接到此输入端口的连接
                var connection = nodeGraph.Connections.FirstOrDefault(conn =>
                    conn.InputNode?.Id == returnNode.Id &&
                    string.Equals(conn.InputPortName, inputPort.Name, StringComparison.OrdinalIgnoreCase));

                if (connection?.OutputNode != null)
                {
                    // 获取输出节点的处理结果
                    var outputNodeResults = imageProcessor.GetNodeOutput(connection.OutputNode.Id);
                    if (outputNodeResults != null &&
                        outputNodeResults.TryGetValue(connection.OutputPortName, out var outputValue))
                    {
                        inputs[inputPort.Name] = outputValue;
                    }
                }
            }

            return inputs;
        }
    }
}
