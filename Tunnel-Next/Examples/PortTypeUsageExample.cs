using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Media;
using Tunnel_Next.Models;

namespace Tunnel_Next.Examples
{
    /// <summary>
    /// 端口类型定义系统使用示例
    /// </summary>
    public static class PortTypeUsageExample
    {
        /// <summary>
        /// 演示如何使用统一的端口类型定义系统
        /// </summary>
        public static void DemonstrateUsage()
        {

            // 1. 基本类型信息获取
            DemonstrateBasicUsage();

            // 2. 颜色获取
            DemonstrateColorUsage();

            // 3. 类型转换
            DemonstrateTypeConversion();

            // 4. 兼容性检查
            DemonstrateCompatibilityCheck();

            // 5. 分类浏览
            DemonstrateCategoryBrowsing();

            // 6. 扩展新类型（概念演示）
            DemonstrateExtensionConcept();

        }

        /// <summary>
        /// 演示基本使用方法
        /// </summary>
        private static void DemonstrateBasicUsage()
        {

            // 获取类型信息
            var f32bmpInfo = PortTypeDefinitions.GetTypeInfo(NodePortDataType.F32bmp);

            // 检查废弃状态
            if (PortTypeDefinitions.IsTypeDeprecated(NodePortDataType.Tif16))
            {
                var message = PortTypeDefinitions.GetDeprecationMessage(NodePortDataType.Tif16);
            }

        }

        /// <summary>
        /// 演示颜色使用
        /// </summary>
        private static void DemonstrateColorUsage()
        {

            // 获取十六进制颜色（用于字符串显示、CSS等）
            var hexColor = PortTypeDefinitions.GetPortColor(NodePortDataType.F32bmp);

            // 获取WPF颜色（用于WPF界面）
            var wpfColor = PortTypeDefinitions.GetPortWpfColor(NodePortDataType.F32bmp);

            // 在实际使用中的示例
            var imageTypes = new[] { NodePortDataType.F32bmp, NodePortDataType.Img, NodePortDataType.Tif16 };
            foreach (var type in imageTypes)
            {
                var color = PortTypeDefinitions.GetPortColor(type);
                var info = PortTypeDefinitions.GetTypeInfo(type);
            }

        }

        /// <summary>
        /// 演示类型转换
        /// </summary>
        private static void DemonstrateTypeConversion()
        {

            // 从脚本或配置文件中读取的字符串类型
            var stringTypes = new[] { "f32bmp", "image", "kernel", "text", "channelr", "unknown" };

            foreach (var stringType in stringTypes)
            {
                var dataType = PortTypeDefinitions.GetPortDataType(stringType);
                var info = PortTypeDefinitions.GetTypeInfo(dataType);
            }

        }

        /// <summary>
        /// 演示兼容性检查
        /// </summary>
        private static void DemonstrateCompatibilityCheck()
        {

            // 模拟连接检查
            var connectionTests = new[]
            {
                (NodePortDataType.F32bmp, NodePortDataType.Img, "图像输出连接到图像输入"),
                (NodePortDataType.Kernel, NodePortDataType.F32bmp, "卷积核连接到图像"),
                (NodePortDataType.Number, NodePortDataType.Constant, "数值连接到常量"),
                (NodePortDataType.Any, NodePortDataType.String, "任意类型连接到字符串")
            };

            foreach (var (outputType, inputType, description) in connectionTests)
            {
                var compatible = PortTypeDefinitions.AreTypesCompatible(outputType, inputType);
                var status = compatible ? "[兼容]" : "[不兼容]";
            }

        }

        /// <summary>
        /// 演示分类浏览
        /// </summary>
        private static void DemonstrateCategoryBrowsing()
        {

            var categories = PortTypeDefinitions.GetAllCategories();
            foreach (var category in categories)
            {
                var typesInCategory = PortTypeDefinitions.GetTypesByCategory(category);
                foreach (var typeInfo in typesInCategory)
                {
                    var status = typeInfo.IsDeprecated ? " (已废弃)" : "";
                }
            }

        }

        /// <summary>
        /// 演示扩展概念（如何添加新类型）
        /// </summary>
        private static void DemonstrateExtensionConcept()
        {

        }

        /// <summary>
        /// 模拟在节点编辑器中的实际使用
        /// </summary>
        public static void SimulateNodeEditorUsage()
        {

            // 模拟创建节点端口
            var ports = new List<(string name, NodePortDataType type, NodePortType portType)>
            {
                ("图像输入", NodePortDataType.F32bmp, NodePortType.Input),
                ("卷积核", NodePortDataType.Kernel, NodePortType.Input),
                ("处理结果", NodePortDataType.F32bmp, NodePortType.Output),
                ("调试信息", NodePortDataType.String, NodePortType.Output)
            };

            foreach (var (name, type, portType) in ports)
            {
                var info = PortTypeDefinitions.GetTypeInfo(type);
                var color = PortTypeDefinitions.GetPortColor(type);
                var direction = portType == NodePortType.Input ? "输入" : "输出";


                if (info.IsDeprecated)
                {
                }
            }

            // 模拟连接检查
            var outputPort = ports.First(p => p.portType == NodePortType.Output && p.type == NodePortDataType.F32bmp);
            var inputPorts = ports.Where(p => p.portType == NodePortType.Input).ToList();

            foreach (var inputPort in inputPorts)
            {
                var compatible = PortTypeDefinitions.AreTypesCompatible(outputPort.type, inputPort.type);
                var status = compatible ? "[兼容]" : "[不兼容]";
            }

        }
    }
}
