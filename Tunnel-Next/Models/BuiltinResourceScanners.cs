using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Tunnel_Next.Services;
using Tunnel_Next.Services.Scripting;

namespace Tunnel_Next.Models
{
    /// <summary>
    /// 内建资源类型的扫描委托实现
    /// </summary>
    public static class BuiltinResourceScanners
    {
        /// <summary>
        /// 模板扫描委托
        /// </summary>
        public static async Task<List<ResourceObject>> ScanTemplatesAsync(ResourceScanContext context)
        {
            var resources = new List<ResourceObject>();

            try
            {
                var templatesFolder = Path.Combine(context.WorkFolder, "Resources", "Templates");
                if (!Directory.Exists(templatesFolder))
                {
                    System.Diagnostics.Debug.WriteLine($"[TemplateScan] 模板文件夹不存在: {templatesFolder}");
                    return resources;
                }

                System.Diagnostics.Debug.WriteLine($"[TemplateScan] 开始扫描模板文件夹: {templatesFolder}");

                var templateDirs = Directory.GetDirectories(templatesFolder);

                foreach (var templateDir in templateDirs)
                {
                    try
                    {
                        var templateName = Path.GetFileName(templateDir);
                        var nodeGraphFile = Directory.GetFiles(templateDir, "*.nodegraph").FirstOrDefault();

                        if (nodeGraphFile == null)
                        {
                            System.Diagnostics.Debug.WriteLine($"[TemplateScan] 模板文件夹中没有找到.nodegraph文件: {templateDir}");
                            continue;
                        }

                        var fileInfo = new FileInfo(nodeGraphFile);
                        var resource = ResourceObject.FromFileInfo(fileInfo, ResourceItemType.Template);
                        resource.Name = templateName; // 使用文件夹名作为模板名

                        // 添加模板文件夹作为关联文件夹
                        resource.AddAssociatedFolder(templateDir);

                        // 扫描模板文件夹中的所有文件作为关联文件
                        var templateFiles = Directory.GetFiles(templateDir, "*", SearchOption.AllDirectories);
                        foreach (var templateFile in templateFiles)
                        {
                            if (!templateFile.Equals(nodeGraphFile, StringComparison.OrdinalIgnoreCase))
                            {
                                resource.AddAssociatedFile(templateFile);
                            }
                        }

                        // 查找缩略图
                        var thumbnailPath = Path.Combine(templateDir, "thumbnail.png");
                        if (File.Exists(thumbnailPath))
                        {
                            resource.ThumbnailPath = thumbnailPath;
                            // 缩略图对象将在UI显示时按需加载
                        }
                        else
                        {
                            // 使用默认模板图标
                            resource.ThumbnailPath = ResourceTypeRegistry.GetDefaultIconPath(ResourceItemType.Template);
                        }

                        // 添加模板特定的元数据
                        resource.Metadata["Category"] = "Template";
                        resource.Metadata["TemplateFolder"] = templateDir;
                        resource.Metadata["AssociatedFileCount"] = resource.AssociatedFiles.Count;
                        resource.Description = $"节点图模板 ({resource.AssociatedFiles.Count + 1} 个文件)";

                        System.Diagnostics.Debug.WriteLine($"[TemplateScan] 模板 {resource.Name}: 主文件={nodeGraphFile}, 关联文件={resource.AssociatedFiles.Count}, 关联文件夹={resource.AssociatedFolders.Count}");

                        resources.Add(resource);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[TemplateScan] 处理模板失败 {templateDir}: {ex.Message}");
                    }
                }

                System.Diagnostics.Debug.WriteLine($"[TemplateScan] 模板扫描完成，共找到 {resources.Count} 个模板");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[TemplateScan] 扫描模板失败: {ex.Message}");
            }

            return resources;
        }

        /// <summary>
        /// 节点图扫描委托
        /// </summary>
        public static async Task<List<ResourceObject>> ScanNodeGraphsAsync(ResourceScanContext context)
        {
            var resources = new List<ResourceObject>();

            try
            {
                var projectsFolder = Path.Combine(context.WorkFolder, "Projects");
                if (!Directory.Exists(projectsFolder))
                {
                    Directory.CreateDirectory(projectsFolder);
                    return resources;
                }

                System.Diagnostics.Debug.WriteLine($"[NodeGraphScan] 开始扫描节点图文件夹: {projectsFolder}");

                var nodeGraphFiles = Directory.GetFiles(projectsFolder, "*.nodegraph", SearchOption.AllDirectories);

                foreach (var filePath in nodeGraphFiles)
                {
                    try
                    {
                        // 排除模板文件夹中的节点图文件
                        if (filePath.Contains("Templates", StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }

                        var fileInfo = new FileInfo(filePath);
                        var resource = ResourceObject.FromFileInfo(fileInfo, ResourceItemType.NodeGraph);

                        // 查找同目录下的相关文件（如配置文件、资源文件等）
                        var nodeGraphDir = Path.GetDirectoryName(filePath);
                        if (!string.IsNullOrEmpty(nodeGraphDir))
                        {
                            var nodeGraphName = Path.GetFileNameWithoutExtension(filePath);
                            var relatedFiles = Directory.GetFiles(nodeGraphDir, "*", SearchOption.TopDirectoryOnly)
                                .Where(f => !f.Equals(filePath, StringComparison.OrdinalIgnoreCase))
                                .Where(f => Path.GetFileNameWithoutExtension(f).StartsWith(nodeGraphName, StringComparison.OrdinalIgnoreCase) ||
                                           Path.GetExtension(f).ToLowerInvariant() is ".json" or ".xml" or ".config" or ".txt" or ".md");

                            foreach (var relatedFile in relatedFiles)
                            {
                                resource.AddAssociatedFile(relatedFile);
                            }
                        }

                        // 设置缩略图路径（使用通用的节点图图标）
                        resource.ThumbnailPath = ResourceTypeRegistry.GetDefaultIconPath(ResourceItemType.NodeGraph);

                        // 添加节点图特定的元数据
                        resource.Metadata["Category"] = "NodeGraph";
                        resource.Metadata["AssociatedFileCount"] = resource.AssociatedFiles.Count;

                        if (resource.AssociatedFiles.Count > 0)
                        {
                            resource.Description = $"节点图文件 ({resource.AssociatedFiles.Count + 1} 个文件)";
                        }
                        else
                        {
                            resource.Description = "节点图文件";
                        }

                        System.Diagnostics.Debug.WriteLine($"[NodeGraphScan] 节点图 {resource.Name}: 主文件={filePath}, 关联文件={resource.AssociatedFiles.Count}");

                        resources.Add(resource);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[NodeGraphScan] 处理节点图文件失败 {filePath}: {ex.Message}");
                    }
                }

                System.Diagnostics.Debug.WriteLine($"[NodeGraphScan] 节点图扫描完成，共找到 {resources.Count} 个节点图");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[NodeGraphScan] 扫描节点图失败: {ex.Message}");
            }

            return resources;
        }

        /// <summary>
        /// 脚本扫描委托
        /// </summary>
        public static async Task<List<ResourceObject>> ScanScriptsAsync(ResourceScanContext context)
        {
            var resources = new List<ResourceObject>();

            try
            {
                var scriptsFolder = Path.Combine(context.WorkFolder, "Scripts");
                if (!Directory.Exists(scriptsFolder))
                {
                    Directory.CreateDirectory(scriptsFolder);
                    return resources;
                }

                System.Diagnostics.Debug.WriteLine($"[ScriptScan] 开始扫描脚本文件夹: {scriptsFolder}");

                // 获取脚本管理器（如果可用）
                var scriptManager = context.Services?.GetService(typeof(RevivalScriptManager)) as RevivalScriptManager;
                var availableScripts = scriptManager?.GetAvailableRevivalScripts() ?? new Dictionary<string, RevivalScriptInfo>();

                var scriptFiles = new List<string>();
                scriptFiles.AddRange(Directory.GetFiles(scriptsFolder, "*.cs", SearchOption.AllDirectories));
                scriptFiles.AddRange(Directory.GetFiles(scriptsFolder, "*.sn", SearchOption.AllDirectories));

                foreach (var filePath in scriptFiles)
                {
                    try
                    {
                        var fileInfo = new FileInfo(filePath);
                        var resource = ResourceObject.FromFileInfo(fileInfo, ResourceItemType.Script);

                        // 查找脚本资源文件夹
                        var scriptName = Path.GetFileNameWithoutExtension(filePath);
                        var scriptResourceFolder = Path.Combine(scriptsFolder, "RevivalResources", scriptName);
                        if (Directory.Exists(scriptResourceFolder))
                        {
                            resource.AddAssociatedFolder(scriptResourceFolder);

                            // 扫描脚本资源文件夹中的所有文件作为关联文件
                            var resourceFiles = Directory.GetFiles(scriptResourceFolder, "*", SearchOption.AllDirectories);
                            foreach (var resourceFile in resourceFiles)
                            {
                                resource.AddAssociatedFile(resourceFile);
                            }
                        }

                        // 设置缩略图路径（使用通用的脚本图标）
                        resource.ThumbnailPath = ResourceTypeRegistry.GetDefaultIconPath(ResourceItemType.Script);

                        // 添加脚本特定的元数据
                        resource.Metadata["Category"] = "Script";
                        resource.Metadata["ScriptType"] = Path.GetExtension(filePath).ToLowerInvariant() == ".cs" ? "CSharp" : "SymbolNode";
                        resource.Metadata["AssociatedFileCount"] = resource.AssociatedFiles.Count;
                        resource.Metadata["AssociatedFolderCount"] = resource.AssociatedFolders.Count;

                        // 构建描述信息，包含关联文件数量
                        var baseDescription = "脚本文件";
                        if (resource.AssociatedFiles.Count > 0)
                        {
                            resource.Description = $"{baseDescription} ({resource.AssociatedFiles.Count + 1} 个文件)";
                        }
                        else
                        {
                            resource.Description = baseDescription;
                        }

                        System.Diagnostics.Debug.WriteLine($"[ScriptScan] 脚本 {resource.Name}: 主文件={filePath}, 关联文件={resource.AssociatedFiles.Count}, 关联文件夹={resource.AssociatedFolders.Count}");

                        resources.Add(resource);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[ScriptScan] 处理脚本文件失败 {filePath}: {ex.Message}");
                    }
                }

                System.Diagnostics.Debug.WriteLine($"[ScriptScan] 脚本扫描完成，共找到 {resources.Count} 个脚本");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ScriptScan] 扫描脚本失败: {ex.Message}");
            }

            return resources;
        }

        /// <summary>
        /// 图像扫描委托
        /// </summary>
        public static async Task<List<ResourceObject>> ScanImagesAsync(ResourceScanContext context)
        {
            var resources = new List<ResourceObject>();

            try
            {
                if (!Directory.Exists(context.WorkFolder))
                {
                    System.Diagnostics.Debug.WriteLine($"[ImageScan] 工作文件夹不存在: {context.WorkFolder}");
                    return resources;
                }

                System.Diagnostics.Debug.WriteLine($"[ImageScan] 开始扫描图像文件，工作文件夹: {context.WorkFolder}");

                // 只扫描工作文件夹根目录，不递归扫描子目录
                foreach (var extension in context.TypeDefinition.SupportedExtensions)
                {
                    var pattern = $"*{extension}";
                    var imageFiles = Directory.GetFiles(context.WorkFolder, pattern, SearchOption.TopDirectoryOnly);

                    foreach (var filePath in imageFiles)
                    {
                        try
                        {
                            var fileInfo = new FileInfo(filePath);
                            var resource = ResourceObject.FromFileInfo(fileInfo, ResourceItemType.Image);

                            // 设置缩略图路径
                            resource.ThumbnailPath = ResourceTypeRegistry.GetDefaultIconPath(ResourceItemType.Image);

                            // 设置描述
                            resource.Description = "图像文件";

                            // 添加图像特定的元数据
                            resource.Metadata["Category"] = "Image";
                            resource.Metadata["FileExtension"] = Path.GetExtension(filePath).ToLowerInvariant();

                            resources.Add(resource);

                            System.Diagnostics.Debug.WriteLine($"[ImageScan] 扫描到图像: {resource.Name}");
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"[ImageScan] 处理图像文件失败 {filePath}: {ex.Message}");
                        }
                    }
                }

                System.Diagnostics.Debug.WriteLine($"[ImageScan] 图像扫描完成，共找到 {resources.Count} 个图像文件");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ImageScan] 扫描图像失败: {ex.Message}");
            }

            return resources;
        }

        /// <summary>
        /// 素材扫描委托
        /// </summary>
        public static async Task<List<ResourceObject>> ScanMaterialsAsync(ResourceScanContext context)
        {
            var resources = new List<ResourceObject>();

            try
            {
                if (!Directory.Exists(context.WorkFolder))
                {
                    return resources;
                }

                System.Diagnostics.Debug.WriteLine($"[MaterialScan] 开始扫描素材文件，工作文件夹: {context.WorkFolder}");

                foreach (var extension in context.TypeDefinition.SupportedExtensions)
                {
                    var pattern = $"*{extension}";
                    var files = Directory.GetFiles(context.WorkFolder, pattern, SearchOption.AllDirectories);

                    foreach (var filePath in files)
                    {
                        try
                        {
                            var fileInfo = new FileInfo(filePath);
                            var resource = ResourceObject.FromFileInfo(fileInfo, ResourceItemType.Material);

                            // 设置缩略图路径
                            resource.ThumbnailPath = ResourceTypeRegistry.GetDefaultIconPath(ResourceItemType.Material);

                            // 设置描述
                            resource.Description = "素材文件";

                            // 添加素材特定的元数据
                            resource.Metadata["Category"] = "Material";
                            resource.Metadata["FileExtension"] = Path.GetExtension(filePath).ToLowerInvariant();

                            resources.Add(resource);

                            System.Diagnostics.Debug.WriteLine($"[MaterialScan] 扫描到素材: {resource.Name}");
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"[MaterialScan] 处理素材文件失败 {filePath}: {ex.Message}");
                        }
                    }
                }

                System.Diagnostics.Debug.WriteLine($"[MaterialScan] 素材扫描完成，共找到 {resources.Count} 个素材文件");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MaterialScan] 扫描素材失败: {ex.Message}");
            }

            return resources;
        }

        /// <summary>
        /// 文件夹扫描委托（通常不主动扫描）
        /// </summary>
        public static async Task<List<ResourceObject>> ScanFoldersAsync(ResourceScanContext context)
        {
            // 文件夹类型通常不主动扫描，返回空列表
            return new List<ResourceObject>();
        }

        /// <summary>
        /// 其他文件扫描委托（兜底类型，通常不主动扫描）
        /// </summary>
        public static async Task<List<ResourceObject>> ScanOthersAsync(ResourceScanContext context)
        {
            // 其他类型作为兜底，通常不主动扫描，返回空列表
            return new List<ResourceObject>();
        }

        /// <summary>
        /// 静态节点扫描委托
        /// </summary>
        public static async Task<List<ResourceObject>> ScanStaticNodesAsync(ResourceScanContext context)
        {
            var resources = new List<ResourceObject>();

            try
            {
                var staticNodesFolder = Path.Combine(context.WorkFolder, "Resources", "StaticNodes");
                if (!Directory.Exists(staticNodesFolder))
                {
                    Directory.CreateDirectory(staticNodesFolder);
                    System.Diagnostics.Debug.WriteLine($"[StaticNodeScan] 创建静态节点文件夹: {staticNodesFolder}");
                    return resources;
                }

                System.Diagnostics.Debug.WriteLine($"[StaticNodeScan] 开始扫描静态节点文件夹: {staticNodesFolder}");

                // 获取所有.tsn文件
                var staticNodeFiles = Directory.GetFiles(staticNodesFolder, "*.tsn", SearchOption.TopDirectoryOnly);

                foreach (var filePath in staticNodeFiles)
                {
                    try
                    {
                        var fileInfo = new FileInfo(filePath);
                        var resource = ResourceObject.FromFileInfo(fileInfo, ResourceItemType.StaticNode);

                        // 读取元数据（如果可能）
                        await ExtractStaticNodeMetadataAsync(resource, filePath);

                        // 设置缩略图路径为FlexiblePort图标
                        resource.ThumbnailPath = "../Resources/FlexiblePort.png";

                        // 添加静态节点特定的元数据
                        resource.Metadata["Category"] = "StaticNode";
                        resource.Metadata["FileExtension"] = ".tsn";

                        // 设置描述
                        if (resource.Metadata.TryGetValue("OriginalNodeName", out var origNode) &&
                            resource.Metadata.TryGetValue("OriginalPortName", out var origPort))
                        {
                            resource.Description = $"静态节点: {origNode} - {origPort}";
                        }
                        else
                        {
                            resource.Description = "静态节点文件";
                        }

                        resources.Add(resource);

                        System.Diagnostics.Debug.WriteLine($"[StaticNodeScan] 扫描到静态节点: {resource.Name}");
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[StaticNodeScan] 处理静态节点文件失败 {filePath}: {ex.Message}");
                    }
                }

                System.Diagnostics.Debug.WriteLine($"[StaticNodeScan] 静态节点扫描完成，共找到 {resources.Count} 个静态节点文件");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[StaticNodeScan] 扫描静态节点失败: {ex.Message}");
            }

            return resources;
        }

        /// <summary>
        /// 提取静态节点元数据
        /// </summary>
        private static async Task ExtractStaticNodeMetadataAsync(ResourceObject resource, string filePath)
        {
            try
            {
                using (var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read))
                using (var br = new BinaryReader(fs))
                {
                    // 尝试读取文件标识符
                    int headerLength = br.ReadInt32();
                    byte[] headerBytes = br.ReadBytes(headerLength);
                    string header = System.Text.Encoding.ASCII.GetString(headerBytes);

                    if (header == "TSN1")
                    {
                        // 新版本格式
                        int nodeNameLength = br.ReadInt32();
                        byte[] nodeNameBytes = br.ReadBytes(nodeNameLength);
                        string nodeName = System.Text.Encoding.UTF8.GetString(nodeNameBytes);

                        int portNameLength = br.ReadInt32();
                        byte[] portNameBytes = br.ReadBytes(portNameLength);
                        string portName = System.Text.Encoding.UTF8.GetString(portNameBytes);

                        int dataTypeLength = br.ReadInt32();
                        byte[] dataTypeBytes = br.ReadBytes(dataTypeLength);
                        string dataType = System.Text.Encoding.UTF8.GetString(dataTypeBytes);

                        resource.Metadata["OriginalNodeName"] = nodeName;
                        resource.Metadata["OriginalPortName"] = portName;
                        resource.Metadata["OriginalDataType"] = dataType;
                        resource.Metadata["FileFormat"] = "TSN1";
                    }
                    else
                    {
                        // 旧版本格式
                        fs.Seek(0, SeekOrigin.Begin);
                        
                        byte[] lengthBytes = new byte[4];
                        fs.Read(lengthBytes, 0, 4);
                        int metadataLength = BitConverter.ToInt32(lengthBytes, 0);

                        byte[] metadataBytes = new byte[metadataLength];
                        fs.Read(metadataBytes, 0, metadataLength);
                        string metadata = System.Text.Encoding.UTF8.GetString(metadataBytes);

                        string[] parts = metadata.Split('\n');
                        if (parts.Length >= 3)
                        {
                            resource.Metadata["OriginalNodeName"] = parts[0];
                            resource.Metadata["OriginalPortName"] = parts[1];
                            resource.Metadata["OriginalDataType"] = parts[2];
                            resource.Metadata["FileFormat"] = "Legacy";
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[StaticNodeScan] 读取静态节点元数据失败: {ex.Message}");
                // 设置默认元数据
                resource.Metadata["OriginalNodeName"] = "未知节点";
                resource.Metadata["OriginalPortName"] = "未知端口";
                resource.Metadata["OriginalDataType"] = "未知类型";
                resource.Metadata["FileFormat"] = "未知";
            }
        }
    }
}
