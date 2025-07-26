using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Tunnel_Next.Models
{
    /// <summary>
    /// 资源操作结果
    /// </summary>
    public class ResourceOperationResult
    {
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
        public string? OutputPath { get; set; }
        public Dictionary<string, object> AdditionalData { get; set; } = new();
    }

    /// <summary>
    /// 导出委托 - 将资源打包成Zip文件
    /// </summary>
    /// <param name="resource">要导出的资源对象</param>
    /// <param name="outputPath">输出Zip文件路径</param>
    /// <returns>操作结果</returns>
    public delegate Task<ResourceOperationResult> ExportDelegate(ResourceObject resource, string outputPath);

    /// <summary>
    /// 导入委托 - 从Zip文件导入资源
    /// </summary>
    /// <param name="zipPath">Zip文件路径</param>
    /// <param name="targetDirectory">目标目录</param>
    /// <returns>操作结果，包含导入的资源对象</returns>
    public delegate Task<ResourceOperationResult> ImportDelegate(string zipPath, string targetDirectory);

    /// <summary>
    /// 删除委托 - 删除资源及其关联文件
    /// </summary>
    /// <param name="resource">要删除的资源对象</param>
    /// <returns>操作结果</returns>
    public delegate Task<ResourceOperationResult> DeleteDelegate(ResourceObject resource);

    /// <summary>
    /// 重命名委托 - 重命名资源及其关联文件
    /// </summary>
    /// <param name="resource">要重命名的资源对象</param>
    /// <param name="newName">新名称（不包含扩展名）</param>
    /// <returns>操作结果</returns>
    public delegate Task<ResourceOperationResult> RenameDelegate(ResourceObject resource, string newName);

    /// <summary>
    /// 通用资源操作委托
    /// </summary>
    /// <param name="resource">资源对象</param>
    /// <param name="parameters">操作参数</param>
    /// <returns>操作结果</returns>
    public delegate Task<ResourceOperationResult> ResourceOperationDelegate(ResourceObject resource, Dictionary<string, object>? parameters = null);



    /// <summary>
    /// 资源委托集
    /// </summary>
    public class ResourceDelegateSet
    {
        private readonly Dictionary<string, Delegate> _delegates = new();

        /// <summary>
        /// 导出委托
        /// </summary>
        public ExportDelegate? ExportDelegate
        {
            get => GetDelegate<ExportDelegate>("Export");
            set => SetDelegate("Export", value);
        }

        /// <summary>
        /// 导入委托
        /// </summary>
        public ImportDelegate? ImportDelegate
        {
            get => GetDelegate<ImportDelegate>("Import");
            set => SetDelegate("Import", value);
        }

        /// <summary>
        /// 删除委托
        /// </summary>
        public DeleteDelegate? DeleteDelegate
        {
            get => GetDelegate<DeleteDelegate>("Delete");
            set => SetDelegate("Delete", value);
        }

        /// <summary>
        /// 重命名委托
        /// </summary>
        public RenameDelegate? RenameDelegate
        {
            get => GetDelegate<RenameDelegate>("Rename");
            set => SetDelegate("Rename", value);
        }



        /// <summary>
        /// 设置委托
        /// </summary>
        public void SetDelegate(string name, Delegate? delegateInstance)
        {
            if (delegateInstance == null)
            {
                _delegates.Remove(name);
            }
            else
            {
                _delegates[name] = delegateInstance;
            }
        }

        /// <summary>
        /// 获取委托
        /// </summary>
        public T? GetDelegate<T>(string name) where T : Delegate
        {
            return _delegates.TryGetValue(name, out var del) ? del as T : null;
        }

        /// <summary>
        /// 检查是否包含指定委托
        /// </summary>
        public bool HasDelegate(string name)
        {
            return _delegates.ContainsKey(name);
        }

        /// <summary>
        /// 获取所有委托名称
        /// </summary>
        public IEnumerable<string> GetDelegateNames()
        {
            return _delegates.Keys;
        }

        /// <summary>
        /// 检查是否包含所有基本委托
        /// </summary>
        public bool HasAllBasicDelegates()
        {
            return HasDelegate("Export") && HasDelegate("Import") &&
                   HasDelegate("Delete") && HasDelegate("Rename");
        }
    }

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

        /// <summary>
        /// 资源操作委托集
        /// </summary>
        public ResourceDelegateSet DelegateSet { get; set; } = new();
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

            // 如果委托集为空，设置默认委托
            if (!definition.DelegateSet.HasAllBasicDelegates())
            {
                Services.DefaultResourceDelegates.SetupDefaultDelegates(definition);
            }

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
            var nodeGraphDefinition = new ResourceTypeDefinition
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
            };
            RegisterType(nodeGraphDefinition);

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

            // 静态节点
            RegisterType(new ResourceTypeDefinition
            {
                Type = ResourceItemType.StaticNode,
                DisplayName = "静态节点",
                Description = "静态节点文件",
                DefaultIconPath = "../Resources/FlexiblePort.png",
                SupportedExtensions = new List<string> { ".tsn" },
                SupportsThumbnail = true,
                SupportsMultipleFiles = false,
                SupportsFolderAssociation = false,
                ScanPriority = 25,
                ScanDelegate = BuiltinResourceScanners.ScanStaticNodesAsync
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
