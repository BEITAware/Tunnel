using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Media;

namespace Tunnel_Next.Models
{
    /// <summary>
    /// 节点端口数据类型枚举
    /// </summary>
    public enum NodePortDataType
    {
        // 图像类型
        F32bmp,     // 32位浮点RGBA格式（主要格式）
        F32Page,    // 32位浮点统计页面图像类型（兼容F32bmp）
        Img,        // 标准图像格式（PNG、JPG等）
        Image,      // 通用图像类型（Revival Scripts兼容）
        Tif16,      // 16位TIFF格式
        Tif8,       // 8位TIFF格式（预留）

        // 数据类型
        Kernel,     // 卷积核数据
        Constant,   // 常量数据
        Mask,       // 遮罩数据
        Spectrumf,  // 频域图像数据
        Array,      // 数组类型（Revival Scripts兼容）
        Object,     // 对象类型（Revival Scripts兼容）

        // 色彩管理类型
        Cube3DLut,          // 3D LUT立方体数据
        OneDLut,            // 1D LUT曲线数据
        ColorTransferModel, // 色彩转换模型

        // 通道类型
        ChannelR,   // 红色通道
        ChannelG,   // 绿色通道
        ChannelB,   // 蓝色通道
        ChannelA,   // Alpha通道

        // 基础类型
        Number,
        String,
        Boolean,
        Color,
        Vector,
        Set,
        F32bmpSet,
        Any
    }

    /// <summary>
    /// 端口类型信息
    /// </summary>
    public class PortTypeInfo
    {
        public NodePortDataType Type { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string HexColor { get; set; } = "#9B9B9B";
        public Color WpfColor { get; set; } = Colors.Gray;
        public string Category { get; set; } = "通用";
        public bool IsDeprecated { get; set; } = false;
        public string? DeprecationMessage { get; set; }
        public List<NodePortDataType> CompatibleTypes { get; set; } = new();
    }

    /// <summary>
    /// 端口类型定义管理器 - 统一管理所有端口类型的定义、颜色和兼容性
    /// </summary>
    public static class PortTypeDefinitions
    {
        private static readonly Dictionary<NodePortDataType, PortTypeInfo> _typeDefinitions;
        private static readonly Dictionary<string, NodePortDataType> _stringToTypeMap;

        static PortTypeDefinitions()
        {
            _typeDefinitions = InitializeTypeDefinitions();
            _stringToTypeMap = InitializeStringToTypeMap();
        }

        /// <summary>
        /// 初始化端口类型定义
        /// </summary>
        private static Dictionary<NodePortDataType, PortTypeInfo> InitializeTypeDefinitions()
        {
            var definitions = new Dictionary<NodePortDataType, PortTypeInfo>();

            // 图像类型
            definitions[NodePortDataType.F32bmp] = new PortTypeInfo
            {
                Type = NodePortDataType.F32bmp,
                Name = "F32BMP",
                Description = "32位浮点RGBA格式图像（主要格式）",
                HexColor = "#F08080",
                WpfColor = Color.FromRgb(0x5B, 0xA0, 0xF2),
                Category = "图像",
                CompatibleTypes = new List<NodePortDataType> { NodePortDataType.F32bmp, NodePortDataType.F32Page, NodePortDataType.Img, NodePortDataType.Tif16, NodePortDataType.Tif8 }
            };

            definitions[NodePortDataType.F32Page] = new PortTypeInfo
            {
                Type = NodePortDataType.F32Page,
                Name = "F32Page",
                Description = "一种兼容F32bmp的统计页面图像类型",
                HexColor = "#87CEEB",
                WpfColor = Color.FromRgb(0x87, 0xCE, 0xEB),
                Category = "图像",
                CompatibleTypes = new List<NodePortDataType> { NodePortDataType.F32bmp, NodePortDataType.F32Page, NodePortDataType.Img, NodePortDataType.Tif16, NodePortDataType.Tif8 }
            };

            definitions[NodePortDataType.Img] = new PortTypeInfo
            {
                Type = NodePortDataType.Img,
                Name = "IMG",
                Description = "标准图像格式（PNG、JPG等）",
                HexColor = "#8A2BE2",
                WpfColor = Color.FromRgb(0x4A, 0x90, 0xE2),
                Category = "图像",
                CompatibleTypes = new List<NodePortDataType> { NodePortDataType.F32bmp, NodePortDataType.F32Page, NodePortDataType.Img, NodePortDataType.Tif16, NodePortDataType.Tif8 }
            };

            definitions[NodePortDataType.Tif16] = new PortTypeInfo
            {
                Type = NodePortDataType.Tif16,
                Name = "TIF16",
                Description = "16位TIFF格式图像",
                HexColor = "#0000FF",
                WpfColor = Color.FromRgb(0x7E, 0xD3, 0x21),
                Category = "图像",
                IsDeprecated = true,
                DeprecationMessage = "已废弃，请勿使用",
                CompatibleTypes = new List<NodePortDataType> { NodePortDataType.F32bmp, NodePortDataType.F32Page, NodePortDataType.Img, NodePortDataType.Tif16, NodePortDataType.Tif8 }
            };

            definitions[NodePortDataType.Tif8] = new PortTypeInfo
            {
                Type = NodePortDataType.Tif8,
                Name = "TIF8",
                Description = "8位TIFF格式图像（预留）",
                HexColor = "#87CEFA",
                WpfColor = Color.FromRgb(0x87, 0xCE, 0xFA),
                Category = "图像",
                CompatibleTypes = new List<NodePortDataType> { NodePortDataType.F32bmp, NodePortDataType.F32Page, NodePortDataType.Img, NodePortDataType.Tif16, NodePortDataType.Tif8 }
            };

            // 数据类型
            definitions[NodePortDataType.Kernel] = new PortTypeInfo
            {
                Type = NodePortDataType.Kernel,
                Name = "Kernel",
                Description = "卷积核数据",
                HexColor = "#FFFF00",
                WpfColor = Color.FromRgb(0xFF, 0x9F, 0x40),
                Category = "数据",
                CompatibleTypes = new List<NodePortDataType> { NodePortDataType.Kernel }
            };

            definitions[NodePortDataType.Constant] = new PortTypeInfo
            {
                Type = NodePortDataType.Constant,
                Name = "Constant",
                Description = "常量数据",
                HexColor = "#228B22",
                WpfColor = Color.FromRgb(0xE9, 0x4B, 0x8C),
                Category = "数据",
                CompatibleTypes = new List<NodePortDataType> { NodePortDataType.Constant, NodePortDataType.Number }
            };

            definitions[NodePortDataType.Mask] = new PortTypeInfo
            {
                Type = NodePortDataType.Mask,
                Name = "Mask",
                Description = "遮罩数据",
                HexColor = "#F0E68C",
                WpfColor = Color.FromRgb(0xF0, 0xE6, 0x8C),
                Category = "数据",
                CompatibleTypes = new List<NodePortDataType> { NodePortDataType.Mask }
            };

            definitions[NodePortDataType.Spectrumf] = new PortTypeInfo
            {
                Type = NodePortDataType.Spectrumf,
                Name = "Spectrumf",
                Description = "频域图像数据",
                HexColor = "#FF6A6A",
                WpfColor = Color.FromRgb(0xFF, 0x6A, 0x6A),
                Category = "数据",
                CompatibleTypes = new List<NodePortDataType> { NodePortDataType.Spectrumf }
            };

            // Revival Scripts兼容类型
            definitions[NodePortDataType.Image] = new PortTypeInfo
            {
                Type = NodePortDataType.Image,
                Name = "Image",
                Description = "通用图像类型（Revival Scripts兼容）",
                HexColor = "#FF6464",
                WpfColor = Color.FromRgb(0xFF, 0x64, 0x64),
                Category = "图像",
                CompatibleTypes = new List<NodePortDataType> { NodePortDataType.Image, NodePortDataType.F32bmp, NodePortDataType.F32Page, NodePortDataType.Img, NodePortDataType.Tif16, NodePortDataType.Tif8 }
            };

            definitions[NodePortDataType.Array] = new PortTypeInfo
            {
                Type = NodePortDataType.Array,
                Name = "Array",
                Description = "数组类型（Revival Scripts兼容）",
                HexColor = "#64FFFF",
                WpfColor = Color.FromRgb(0x64, 0xFF, 0xFF),
                Category = "数据",
                CompatibleTypes = new List<NodePortDataType> { NodePortDataType.Array }
            };

            definitions[NodePortDataType.Object] = new PortTypeInfo
            {
                Type = NodePortDataType.Object,
                Name = "Object",
                Description = "对象类型（Revival Scripts兼容）",
                HexColor = "#C8C8C8",
                WpfColor = Color.FromRgb(0xC8, 0xC8, 0xC8),
                Category = "数据",
                CompatibleTypes = new List<NodePortDataType> { NodePortDataType.Object }
            };

            // 色彩管理类型
            definitions[NodePortDataType.Cube3DLut] = new PortTypeInfo
            {
                Type = NodePortDataType.Cube3DLut,
                Name = "Cube3DLut",
                Description = "3D查找表立方体数据",
                HexColor = "#9932CC",
                WpfColor = Color.FromRgb(0x99, 0x32, 0xCC),
                Category = "色彩管理",
                CompatibleTypes = new List<NodePortDataType> { NodePortDataType.Cube3DLut }
            };

            definitions[NodePortDataType.OneDLut] = new PortTypeInfo
            {
                Type = NodePortDataType.OneDLut,
                Name = "OneDLut",
                Description = "1D查找表曲线数据",
                HexColor = "#FF8C00",
                WpfColor = Color.FromRgb(0xFF, 0x8C, 0x00),
                Category = "色彩管理",
                CompatibleTypes = new List<NodePortDataType> { NodePortDataType.OneDLut }
            };

            definitions[NodePortDataType.ColorTransferModel] = new PortTypeInfo
            {
                Type = NodePortDataType.ColorTransferModel,
                Name = "ColorTransferModel",
                Description = "TunnelUtils色彩转换模型数据",
                HexColor = "#DC143C",
                WpfColor = Color.FromRgb(0xDC, 0x14, 0x3C),
                Category = "色彩管理",
                CompatibleTypes = new List<NodePortDataType> { NodePortDataType.ColorTransferModel }
            };

            // 通道类型
            definitions[NodePortDataType.ChannelR] = new PortTypeInfo
            {
                Type = NodePortDataType.ChannelR,
                Name = "ChannelR",
                Description = "红色通道",
                HexColor = "#FF0000",
                WpfColor = Color.FromRgb(0xFF, 0x00, 0x00),
                Category = "通道",
                CompatibleTypes = new List<NodePortDataType> { NodePortDataType.ChannelR, NodePortDataType.ChannelG, NodePortDataType.ChannelB, NodePortDataType.ChannelA }
            };

            definitions[NodePortDataType.ChannelG] = new PortTypeInfo
            {
                Type = NodePortDataType.ChannelG,
                Name = "ChannelG",
                Description = "绿色通道",
                HexColor = "#00FF00",
                WpfColor = Color.FromRgb(0x00, 0xFF, 0x00),
                Category = "通道",
                CompatibleTypes = new List<NodePortDataType> { NodePortDataType.ChannelR, NodePortDataType.ChannelG, NodePortDataType.ChannelB, NodePortDataType.ChannelA }
            };

            definitions[NodePortDataType.ChannelB] = new PortTypeInfo
            {
                Type = NodePortDataType.ChannelB,
                Name = "ChannelB",
                Description = "蓝色通道",
                HexColor = "#0000FF",
                WpfColor = Color.FromRgb(0x00, 0x00, 0xFF),
                Category = "通道",
                CompatibleTypes = new List<NodePortDataType> { NodePortDataType.ChannelR, NodePortDataType.ChannelG, NodePortDataType.ChannelB, NodePortDataType.ChannelA }
            };

            definitions[NodePortDataType.ChannelA] = new PortTypeInfo
            {
                Type = NodePortDataType.ChannelA,
                Name = "ChannelA",
                Description = "Alpha通道",
                HexColor = "#778899",
                WpfColor = Color.FromRgb(0x77, 0x88, 0x99),
                Category = "通道",
                CompatibleTypes = new List<NodePortDataType> { NodePortDataType.ChannelR, NodePortDataType.ChannelG, NodePortDataType.ChannelB, NodePortDataType.ChannelA }
            };

            // 基础类型
            definitions[NodePortDataType.Number] = new PortTypeInfo
            {
                Type = NodePortDataType.Number,
                Name = "Number",
                Description = "数值类型",
                HexColor = "#FFA500",
                WpfColor = Color.FromRgb(0xF5, 0xA6, 0x23),
                Category = "基础",
                CompatibleTypes = new List<NodePortDataType> { NodePortDataType.Number, NodePortDataType.Constant }
            };

            definitions[NodePortDataType.String] = new PortTypeInfo
            {
                Type = NodePortDataType.String,
                Name = "String",
                Description = "字符串类型",
                HexColor = "#DDA0DD",
                WpfColor = Color.FromRgb(0xBD, 0x7E, 0xDE),
                Category = "基础",
                CompatibleTypes = new List<NodePortDataType> { NodePortDataType.String }
            };

            definitions[NodePortDataType.Boolean] = new PortTypeInfo
            {
                Type = NodePortDataType.Boolean,
                Name = "Boolean",
                Description = "布尔类型",
                HexColor = "#20B2AA",
                WpfColor = Color.FromRgb(0x50, 0xE3, 0xC2),
                Category = "基础",
                CompatibleTypes = new List<NodePortDataType> { NodePortDataType.Boolean }
            };

            definitions[NodePortDataType.Color] = new PortTypeInfo
            {
                Type = NodePortDataType.Color,
                Name = "Color",
                Description = "颜色类型",
                HexColor = "#FF69B4",
                WpfColor = Color.FromRgb(0xFF, 0x69, 0xB4),
                Category = "基础",
                CompatibleTypes = new List<NodePortDataType> { NodePortDataType.Color }
            };

            definitions[NodePortDataType.Vector] = new PortTypeInfo
            {
                Type = NodePortDataType.Vector,
                Name = "Vector",
                Description = "向量类型",
                HexColor = "#8FBC8F",
                WpfColor = Color.FromRgb(0x8F, 0xBC, 0x8F),
                Category = "基础",
                CompatibleTypes = new List<NodePortDataType> { NodePortDataType.Vector }
            };

            definitions[NodePortDataType.Set] = new PortTypeInfo
            {
                Type = NodePortDataType.Set,
                Name = "Set",
                Description = "集合类型",
                HexColor = "#DDA0DD",
                WpfColor = Color.FromRgb(0xDD, 0xA0, 0xDD),
                Category = "基础",
                CompatibleTypes = new List<NodePortDataType> { NodePortDataType.Set, NodePortDataType.F32bmpSet }
            };

            definitions[NodePortDataType.F32bmpSet] = new PortTypeInfo
            {
                Type = NodePortDataType.F32bmpSet,
                Name = "F32bmpSet",
                Description = "F32BMP图像集合类型",
                HexColor = "#D02090",
                WpfColor = Color.FromRgb(0xD0, 0x20, 0x90),
                Category = "基础",
                CompatibleTypes = new List<NodePortDataType> { NodePortDataType.F32bmpSet }
            };

            definitions[NodePortDataType.Any] = new PortTypeInfo
            {
                Type = NodePortDataType.Any,
                Name = "Any",
                Description = "任意类型",
                HexColor = "#9B9B9B",
                WpfColor = Color.FromRgb(0x9B, 0x9B, 0x9B),
                Category = "通用",
                CompatibleTypes = Enum.GetValues<NodePortDataType>().ToList()
            };

            return definitions;
        }

        /// <summary>
        /// 初始化字符串到类型的映射
        /// </summary>
        private static Dictionary<string, NodePortDataType> InitializeStringToTypeMap()
        {
            var map = new Dictionary<string, NodePortDataType>(StringComparer.OrdinalIgnoreCase);

            // 标准映射
            foreach (var kvp in _typeDefinitions)
            {
                var info = kvp.Value;
                map[info.Name.ToLower()] = kvp.Key;
                map[kvp.Key.ToString().ToLower()] = kvp.Key;
            }

            // 别名映射
            map["image"] = NodePortDataType.F32bmp; // 默认映射到F32bmp
            map["img"] = NodePortDataType.Img;
            map["f32page"] = NodePortDataType.F32Page;
            map["page"] = NodePortDataType.F32Page; // 简短别名
            map["text"] = NodePortDataType.String;
            map["channelr"] = NodePortDataType.ChannelR;
            map["channelg"] = NodePortDataType.ChannelG;
            map["channelb"] = NodePortDataType.ChannelB;
            map["channela"] = NodePortDataType.ChannelA;

            // 色彩管理类型别名
            map["3dlut"] = NodePortDataType.Cube3DLut;
            map["cube3dlut"] = NodePortDataType.Cube3DLut;
            map["1dlut"] = NodePortDataType.OneDLut;
            map["onedlut"] = NodePortDataType.OneDLut;
            map["ctm"] = NodePortDataType.ColorTransferModel;
            map["colortransfermodel"] = NodePortDataType.ColorTransferModel;

            return map;
        }

        /// <summary>
        /// 获取端口类型信息
        /// </summary>
        public static PortTypeInfo GetTypeInfo(NodePortDataType type)
        {
            return _typeDefinitions.TryGetValue(type, out var info) ? info : _typeDefinitions[NodePortDataType.Any];
        }

        /// <summary>
        /// 获取端口颜色（十六进制字符串）
        /// </summary>
        public static string GetPortColor(NodePortDataType type)
        {
            return GetTypeInfo(type).HexColor;
        }

        /// <summary>
        /// 获取端口颜色（WPF Color）
        /// </summary>
        public static Color GetPortWpfColor(NodePortDataType type)
        {
            return GetTypeInfo(type).WpfColor;
        }

        /// <summary>
        /// 从字符串获取端口数据类型
        /// </summary>
        public static NodePortDataType GetPortDataType(string typeName)
        {
            if (string.IsNullOrEmpty(typeName))
                return NodePortDataType.Any;

            return _stringToTypeMap.TryGetValue(typeName.ToLower(), out var type) ? type : NodePortDataType.Any;
        }

        /// <summary>
        /// 检查两个类型是否兼容
        /// </summary>
        public static bool AreTypesCompatible(NodePortDataType outputType, NodePortDataType inputType)
        {
            // Any类型可以连接任何类型
            if (outputType == NodePortDataType.Any || inputType == NodePortDataType.Any)
                return true;

            // 相同类型可以连接
            if (outputType == inputType)
                return true;

            // 检查兼容性列表
            var outputInfo = GetTypeInfo(outputType);
            return outputInfo.CompatibleTypes.Contains(inputType);
        }

        /// <summary>
        /// 获取所有端口类型
        /// </summary>
        public static IEnumerable<PortTypeInfo> GetAllTypes()
        {
            return _typeDefinitions.Values;
        }

        /// <summary>
        /// 按分类获取端口类型
        /// </summary>
        public static IEnumerable<PortTypeInfo> GetTypesByCategory(string category)
        {
            return _typeDefinitions.Values.Where(t => t.Category == category);
        }

        /// <summary>
        /// 获取所有分类
        /// </summary>
        public static IEnumerable<string> GetAllCategories()
        {
            return _typeDefinitions.Values.Select(t => t.Category).Distinct();
        }

        /// <summary>
        /// 检查类型是否已废弃
        /// </summary>
        public static bool IsTypeDeprecated(NodePortDataType type)
        {
            return GetTypeInfo(type).IsDeprecated;
        }

        /// <summary>
        /// 获取类型废弃信息
        /// </summary>
        public static string? GetDeprecationMessage(NodePortDataType type)
        {
            return GetTypeInfo(type).DeprecationMessage;
        }
    }
}
