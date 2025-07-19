using System;
using System.Collections.Generic;
using System.Linq;

namespace Tunnel_Next.Models
{


    /// <summary>
    /// 资源类型定义
    /// </summary>
    public class ResourceTypeDefinition
    {
        /// <summary>
        /// 资源类型
        /// </summary>
        public ResourceItemType Type { get; set; }

        /// <summary>
        /// 显示名称
        /// </summary>
        public string DisplayName { get; set; } = string.Empty;

        /// <summary>
        /// 描述
        /// </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// 默认图标路径
        /// </summary>
        public string DefaultIconPath { get; set; } = string.Empty;

        /// <summary>
        /// 支持的文件扩展名
        /// </summary>
        public List<string> SupportedExtensions { get; set; } = new();

        /// <summary>
        /// 是否支持缩略图
        /// </summary>
        public bool SupportsThumbnail { get; set; } = false;

        /// <summary>
        /// 是否支持多文件关联
        /// </summary>
        public bool SupportsMultipleFiles { get; set; } = false;

        /// <summary>
        /// 是否支持文件夹关联
        /// </summary>
        public bool SupportsFolderAssociation { get; set; } = false;

        /// <summary>
        /// 扫描优先级（数值越小优先级越高）
        /// </summary>
        public int ScanPriority { get; set; } = 100;

        /// <summary>
        /// 自定义属性
        /// </summary>
        public Dictionary<string, object> CustomProperties { get; set; } = new();

        /// <summary>
        /// 资源扫描委托
        /// </summary>
        public ResourceScanDelegate? ScanDelegate { get; set; }
    }

    /// <summary>
    /// 资源类型注册表 - 可扩展的资源类型管理系统
    /// </summary>
    public static class ResourceTypeRegistry
    {
        private static readonly Dictionary<ResourceItemType, ResourceTypeDefinition> _typeDefinitions = new();
        private static readonly Dictionary<string, ResourceItemType> _extensionMapping = new();
        private static bool _initialized = false;

        /// <summary>
        /// 初始化默认资源类型
        /// </summary>
        static ResourceTypeRegistry()
        {
            InitializeDefaultTypes();
        }

        /// <summary>
        /// 注册资源类型
        /// </summary>
        public static void RegisterType(ResourceTypeDefinition definition)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));

            _typeDefinitions[definition.Type] = definition;

            // 更新扩展名映射
            foreach (var extension in definition.SupportedExtensions)
            {
                var normalizedExt = extension.ToLowerInvariant();
                if (!normalizedExt.StartsWith("."))
                    normalizedExt = "." + normalizedExt;
                
                _extensionMapping[normalizedExt] = definition.Type;
            }
        }

        /// <summary>
        /// 获取资源类型定义
        /// </summary>
        public static ResourceTypeDefinition? GetTypeDefinition(ResourceItemType type)
        {
            return _typeDefinitions.TryGetValue(type, out var definition) ? definition : null;
        }

        /// <summary>
        /// 根据文件扩展名获取资源类型
        /// </summary>
        public static ResourceItemType GetTypeByExtension(string extension)
        {
            if (string.IsNullOrEmpty(extension)) return ResourceItemType.Other;

            var normalizedExt = extension.ToLowerInvariant();
            if (!normalizedExt.StartsWith("."))
                normalizedExt = "." + normalizedExt;

            return _extensionMapping.TryGetValue(normalizedExt, out var type) ? type : ResourceItemType.Other;
        }

        /// <summary>
        /// 获取所有已注册的资源类型
        /// </summary>
        public static IEnumerable<ResourceTypeDefinition> GetAllTypes()
        {
            return _typeDefinitions.Values.OrderBy(t => t.ScanPriority);
        }

        /// <summary>
        /// 获取资源类型的显示名称
        /// </summary>
        public static string GetDisplayName(ResourceItemType type)
        {
            var definition = GetTypeDefinition(type);
            return definition?.DisplayName ?? type.ToString();
        }

        /// <summary>
        /// 获取资源类型的默认图标路径
        /// </summary>
        public static string GetDefaultIconPath(ResourceItemType type)
        {
            var definition = GetTypeDefinition(type);
            return definition?.DefaultIconPath ?? "../Resources/Script.png";
        }

        /// <summary>
        /// 检查资源类型是否支持缩略图
        /// </summary>
        public static bool SupportsThumbnail(ResourceItemType type)
        {
            var definition = GetTypeDefinition(type);
            return definition?.SupportsThumbnail ?? false;
        }

        /// <summary>
        /// 检查资源类型是否支持多文件关联
        /// </summary>
        public static bool SupportsMultipleFiles(ResourceItemType type)
        {
            var definition = GetTypeDefinition(type);
            return definition?.SupportsMultipleFiles ?? false;
        }

        /// <summary>
        /// 检查资源类型是否支持文件夹关联
        /// </summary>
        public static bool SupportsFolderAssociation(ResourceItemType type)
        {
            var definition = GetTypeDefinition(type);
            return definition?.SupportsFolderAssociation ?? false;
        }

        /// <summary>
        /// 初始化默认资源类型
        /// </summary>
        private static void InitializeDefaultTypes()
        {
            if (_initialized) return;

            // 节点图
            RegisterType(new ResourceTypeDefinition
            {
                Type = ResourceItemType.NodeGraph,
                DisplayName = "节点图",
                Description = "节点图文件",
                DefaultIconPath = "../Resources/Nodegraph.png",
                SupportedExtensions = new List<string> { ".nodegraph" },
                SupportsThumbnail = false,
                SupportsMultipleFiles = true,
                SupportsFolderAssociation = false,
                ScanPriority = 10,
                ScanDelegate = BuiltinResourceScanners.ScanNodeGraphsAsync
            });

            // 模板
            RegisterType(new ResourceTypeDefinition
            {
                Type = ResourceItemType.Template,
                DisplayName = "模板",
                Description = "节点图模板",
                DefaultIconPath = "../Resources/Template.png",
                SupportedExtensions = new List<string> { ".nodegraph" }, // 模板也是节点图文件
                SupportsThumbnail = true,
                SupportsMultipleFiles = true,
                SupportsFolderAssociation = true,
                ScanPriority = 5, // 比普通节点图优先级更高
                ScanDelegate = BuiltinResourceScanners.ScanTemplatesAsync
            });

            // 脚本
            RegisterType(new ResourceTypeDefinition
            {
                Type = ResourceItemType.Script,
                DisplayName = "脚本",
                Description = "脚本文件",
                DefaultIconPath = "../Resources/Script.png",
                SupportedExtensions = new List<string> { ".cs", ".sn" },
                SupportsThumbnail = false,
                SupportsMultipleFiles = true,
                SupportsFolderAssociation = true,
                ScanPriority = 20,
                ScanDelegate = BuiltinResourceScanners.ScanScriptsAsync
            });

            // 图像
            RegisterType(new ResourceTypeDefinition
            {
                Type = ResourceItemType.Image,
                DisplayName = "图像",
                Description = "图像文件",
                DefaultIconPath = "../Resources/RawImage.png",
                SupportedExtensions = new List<string> { ".png", ".jpg", ".jpeg", ".bmp", ".tiff", ".tif", ".exr", ".hdr" },
                SupportsThumbnail = true,
                SupportsMultipleFiles = false,
                SupportsFolderAssociation = false,
                ScanPriority = 30,
                ScanDelegate = BuiltinResourceScanners.ScanImagesAsync
            });



            // 素材
            RegisterType(new ResourceTypeDefinition
            {
                Type = ResourceItemType.Material,
                DisplayName = "素材",
                Description = "素材文件",
                DefaultIconPath = "../Resources/RawImage.png",
                SupportedExtensions = new List<string> { ".cube", ".lut", ".3dl", ".csp" },
                SupportsThumbnail = false,
                SupportsMultipleFiles = false,
                SupportsFolderAssociation = false,
                ScanPriority = 40,
                ScanDelegate = BuiltinResourceScanners.ScanMaterialsAsync
            });

            // 文件夹
            RegisterType(new ResourceTypeDefinition
            {
                Type = ResourceItemType.Folder,
                DisplayName = "文件夹",
                Description = "文件夹",
                DefaultIconPath = "../Resources/WorkFolder.png",
                SupportedExtensions = new List<string>(),
                SupportsThumbnail = false,
                SupportsMultipleFiles = false,
                SupportsFolderAssociation = false,
                ScanPriority = 1000, // 最低优先级
                ScanDelegate = BuiltinResourceScanners.ScanFoldersAsync
            });

            // 其他
            RegisterType(new ResourceTypeDefinition
            {
                Type = ResourceItemType.Other,
                DisplayName = "其他",
                Description = "其他文件",
                DefaultIconPath = "../Resources/Script.png",
                SupportedExtensions = new List<string>(),
                SupportsThumbnail = false,
                SupportsMultipleFiles = false,
                SupportsFolderAssociation = false,
                ScanPriority = 9999, // 最低优先级
                ScanDelegate = BuiltinResourceScanners.ScanOthersAsync
            });

            _initialized = true;
        }
    }
}
