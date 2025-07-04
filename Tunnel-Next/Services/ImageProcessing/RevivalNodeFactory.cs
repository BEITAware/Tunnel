using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Tunnel_Next.Models;
using Tunnel_Next.Services.Scripting;
using Tunnel_Next.Services.ImageProcessing;
using Tunnel_Next.Services;

namespace Tunnel_Next.Services.ImageProcessing
{
    /// <summary>
    /// Revival Scripts节点工厂 - 基于Revival Scripts系统创建节点
    /// </summary>
    public static class RevivalNodeFactory
    {
        private static RevivalScriptManager? _revivalScriptManager;
        private static Random _random = new Random(); // 用于随机样式

        /// <summary>
        /// 获取所有可用的Revival Scripts节点类型
        /// </summary>
        public static List<RevivalNodeMenuItem> GetAvailableRevivalNodeTypes(RevivalScriptManager? revivalScriptManager = null)
        {
            var nodeTypes = new List<RevivalNodeMenuItem>();

            if (revivalScriptManager != null)
            {
                // 保存管理器引用以供后续使用
                _revivalScriptManager = revivalScriptManager;

                // 从Revival Scripts管理器获取节点类型
                var scripts = revivalScriptManager.GetAvailableRevivalScripts();
                foreach (var kvp in scripts)
                {
                    var scriptInfo = kvp.Value;
                    nodeTypes.Add(new RevivalNodeMenuItem
                    {
                        Name = scriptInfo.Name,
                        Category = scriptInfo.Category,
                        Description = scriptInfo.Description,
                        Color = scriptInfo.Color,
                        ScriptInfo = scriptInfo
                    });
                }
            }

            // 如果没有脚本，显示警告
            if (nodeTypes.Count == 0)
            {
            }
            else
            {
            }

            return nodeTypes;
        }

        /// <summary>
        /// 从Revival Script创建节点（公共方法）
        /// </summary>
        public static Node CreateRevivalNode(RevivalScriptInfo scriptInfo, double x, double y)
        {
            if (scriptInfo.IsSymbolNode)
            {
                return CreateSymbolNode(scriptInfo, x, y);
            }

            return CreateRevivalNodeFromScript(scriptInfo, x, y);
        }

        /// <summary>
        /// 设置RevivalScriptManager（确保正确初始化）
        /// </summary>
        public static void SetRevivalScriptManager(RevivalScriptManager manager)
        {
            _revivalScriptManager = manager;
        }

        /// <summary>
        /// 从Revival Script创建节点
        /// </summary>
        private static Node CreateRevivalNodeFromScript(RevivalScriptInfo scriptInfo, double x, double y)
        {
            try
            {
                // 创建节点
                var node = new Node
                {
                    Id = NodeIdManager.Instance.GenerateNodeId(),
                    Title = scriptInfo.Name,
                    X = x,
                    Y = y,
                    ScriptPath = scriptInfo.FilePath,
                    Category = scriptInfo.Category,
                    Description = scriptInfo.Description,
                    Color = scriptInfo.Color
                };

                // 随机分配V1或V2样式标记
                node.StyleType = _random.Next(2) == 0 ? "V1" : "V2";

                // 创建脚本实例
                var scriptInstance = CreateRevivalScriptInstance(scriptInfo);

                if (scriptInstance != null)
                {
                    // 将脚本实例绑定到节点
                    node.Tag = scriptInstance;

                    // 设置端口和参数
                    SetupRevivalScriptPorts(node, scriptInstance);
                    SetupRevivalScriptParameters(node, scriptInstance);
                }
                else
                {
                }

                return node;
            }
            catch (Exception ex)
            {
                throw;
            }
        }

        /// <summary>
        /// 创建Revival Script实例
        /// </summary>
        private static IRevivalScript? CreateRevivalScriptInstance(RevivalScriptInfo scriptInfo)
        {
            try
            {
                if (_revivalScriptManager == null)
                {
                    return null;
                }

                // 计算相对路径
                var relativePath = Path.GetRelativePath(_revivalScriptManager.UserScriptsFolder, scriptInfo.FilePath);

                // 使用RevivalScriptManager创建实例
                var instance = _revivalScriptManager.CreateRevivalScriptInstance(relativePath);

                if (instance != null)
                {
                }
                else
                {
                }

                return instance;
            }
            catch (Exception ex)
            {
                return null;
            }
        }

        /// <summary>
        /// 设置Revival Script端口
        /// </summary>
        private static void SetupRevivalScriptPorts(Node node, IRevivalScript scriptInstance)
        {
            try
            {

                // 清除现有端口和灵活端口配置
                node.InputPorts.Clear();
                node.OutputPorts.Clear();
                node.FlexibleInputTypes.Clear();
                node.FlexibleOutputTypes.Clear();

                // 从脚本获取端口定义
                var inputPorts = scriptInstance.GetInputPorts();
                var outputPorts = scriptInstance.GetOutputPorts();

                // 添加输入端口
                foreach (var kvp in inputPorts)
                {
                    var portName = kvp.Key;
                    var portDef = kvp.Value;
                    var dataType = GetNodePortDataType(portDef.DataType);

                    var port = new NodePort
                    {
                        Name = portName,
                        Type = NodePortType.Input,
                        DataType = dataType,
                        Description = portDef.Description,
                        IsInput = true,
                        IsFlexible = portDef.IsFlexible
                    };
                    node.InputPorts.Add(port);

                    // 如果是灵活端口，添加到灵活端口类型列表
                    if (portDef.IsFlexible && !node.FlexibleInputTypes.Contains(dataType))
                    {
                        node.FlexibleInputTypes.Add(dataType);
                    }

                }

                // 添加输出端口
                foreach (var kvp in outputPorts)
                {
                    var portName = kvp.Key;
                    var portDef = kvp.Value;
                    var dataType = GetNodePortDataType(portDef.DataType);

                    var port = new NodePort
                    {
                        Name = portName,
                        Type = NodePortType.Output,
                        DataType = dataType,
                        Description = portDef.Description,
                        IsInput = false,
                        IsFlexible = portDef.IsFlexible
                    };
                    node.OutputPorts.Add(port);

                    // 如果是灵活端口，添加到灵活端口类型列表
                    if (portDef.IsFlexible && !node.FlexibleOutputTypes.Contains(dataType))
                    {
                        node.FlexibleOutputTypes.Add(dataType);
                    }

                }

            }
            catch (Exception ex)
            {
            }
        }

        /// <summary>
        /// 设置Revival Script参数
        /// </summary>
        private static void SetupRevivalScriptParameters(Node node, IRevivalScript scriptInstance)
        {
            try
            {

                // 清除现有参数
                node.Parameters.Clear();

                // 从脚本获取参数
                var scriptParameters = scriptInstance.SerializeParameters();

                // 添加参数到节点
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
        /// 将字符串端口类型转换为NodePortDataType枚举
        /// </summary>
        private static NodePortDataType GetNodePortDataType(string dataType)
        {
            try
            {
                // 尝试直接解析枚举
                if (Enum.TryParse<NodePortDataType>(dataType, true, out var result))
                {
                    return result;
                }

                // 处理一些常见的别名
                return dataType.ToLowerInvariant() switch
                {
                    "f32bmp" or "image" or "bitmap" => NodePortDataType.F32bmp,
                    "img" => NodePortDataType.Img,
                    "tif16" => NodePortDataType.Tif16,
                    "tif8" => NodePortDataType.Tif8,
                    "string" or "text" => NodePortDataType.String,
                    "number" or "float" or "double" => NodePortDataType.Number,
                    "bool" or "boolean" => NodePortDataType.Boolean,
                    "vector" => NodePortDataType.Vector,
                    "color" => NodePortDataType.Color,
                    "kernel" => NodePortDataType.Kernel,
                    "constant" => NodePortDataType.Constant,
                    "mask" => NodePortDataType.Mask,
                    "spectrumf" => NodePortDataType.Spectrumf,
                    "array" => NodePortDataType.Array,
                    "object" => NodePortDataType.Object,
                    "channelr" => NodePortDataType.ChannelR,
                    "channelg" => NodePortDataType.ChannelG,
                    "channelb" => NodePortDataType.ChannelB,
                    "channela" => NodePortDataType.ChannelA,
                    "any" => NodePortDataType.Any,
                    _ => NodePortDataType.Any // 默认为Any类型
                };
            }
            catch (Exception ex)
            {
                return NodePortDataType.Any;
            }
        }

        /// <summary>
        /// 从符号节点信息创建占位节点
        /// </summary>
        private static Node CreateSymbolNode(RevivalScriptInfo scriptInfo, double x, double y)
        {
            var node = new Node
            {
                Id = NodeIdManager.Instance.GenerateNodeId(),
                Title = scriptInfo.Name,
                X = x,
                Y = y,
                ScriptPath = scriptInfo.FilePath,
                Category = scriptInfo.Category,
                Description = scriptInfo.Description,
                Color = scriptInfo.Color,
                ToBeProcessed = false, // 占位，无需处理
                StyleType = "Symbol"
            };

            // 设置端口
            foreach (var kvp in scriptInfo.InputPorts)
            {
                var port = new NodePort
                {
                    Name = kvp.Key,
                    Type = NodePortType.Input,
                    DataType = GetNodePortDataType(kvp.Value.DataType),
                    Description = kvp.Value.Description,
                    IsInput = true,
                    IsFlexible = kvp.Value.IsFlexible
                };
                node.InputPorts.Add(port);

                if (kvp.Value.IsFlexible && !node.FlexibleInputTypes.Contains(port.DataType))
                {
                    node.FlexibleInputTypes.Add(port.DataType);
                }
            }

            foreach (var kvp in scriptInfo.OutputPorts)
            {
                var port = new NodePort
                {
                    Name = kvp.Key,
                    Type = NodePortType.Output,
                    DataType = GetNodePortDataType(kvp.Value.DataType),
                    Description = kvp.Value.Description,
                    IsInput = false,
                    IsFlexible = kvp.Value.IsFlexible
                };
                node.OutputPorts.Add(port);

                if (kvp.Value.IsFlexible && !node.FlexibleOutputTypes.Contains(port.DataType))
                {
                    node.FlexibleOutputTypes.Add(port.DataType);
                }
            }

            // 更新节点尺寸
            node.UpdateNodeHeight();

            return node;
        }

        // 移除了CreateRevivalScriptInstance方法，现在完全使用ScriptInstanceManager

        // 旧的随机ID生成方法已移除，等待重构
    }
}
