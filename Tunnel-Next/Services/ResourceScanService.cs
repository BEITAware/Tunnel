using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using Tunnel_Next.Models;
using Tunnel_Next.Services.Scripting;

namespace Tunnel_Next.Services
{
    /// <summary>
    /// 资源扫描服务
    /// </summary>
    public class ResourceScanService
    {
        private readonly WorkFolderService _workFolderService;
        private readonly ThumbnailService _thumbnailService;
        private readonly UnifiedResourceScanner _unifiedScanner;
        private readonly RevivalScriptManager? _scriptManager;

        /// <summary>
        /// 支持的节点图文件扩展名
        /// </summary>
        private static readonly string[] NodeGraphExtensions = { ".nodegraph" };

        /// <summary>
        /// 支持的脚本文件扩展名
        /// </summary>
        private static readonly string[] ScriptExtensions = { ".cs", ".sn" };

        /// <summary>
        /// 根据文件路径自动识别资源类型
        /// </summary>
        private ResourceItemType GetResourceTypeFromPath(string filePath)
        {
            var extension = Path.GetExtension(filePath);
            var directory = Path.GetDirectoryName(filePath) ?? string.Empty;

            // 首先尝试使用ResourceTypeRegistry的扩展名映射
            var typeFromExtension = ResourceTypeRegistry.GetTypeByExtension(extension);

            // 特殊处理：模板是在Templates文件夹中的.nodegraph文件
            if (typeFromExtension == ResourceItemType.NodeGraph &&
                directory.Contains("Templates", StringComparison.OrdinalIgnoreCase))
            {
                return ResourceItemType.Template;
            }

            return typeFromExtension;
        }

        public ResourceScanService(WorkFolderService workFolderService, ThumbnailService thumbnailService, RevivalScriptManager? scriptManager = null, IServiceProvider? serviceProvider = null)
        {
            _workFolderService = workFolderService ?? throw new ArgumentNullException(nameof(workFolderService));
            _thumbnailService = thumbnailService ?? throw new ArgumentNullException(nameof(thumbnailService));
            _scriptManager = scriptManager;
            _unifiedScanner = new UnifiedResourceScanner(_workFolderService, serviceProvider);
        }

        /// <summary>
        /// 扫描所有资源
        /// </summary>
        public async Task<List<ResourceObject>> ScanAllResourcesAsync()
        {
            try
            {
                // 使用统一扫描引擎扫描所有资源
                var scanResult = await _unifiedScanner.ScanAllResourcesAsync();

                if (scanResult.Success)
                {
                    System.Diagnostics.Debug.WriteLine($"[ResourceScanService] 统一扫描完成，共找到 {scanResult.Resources.Count} 个资源，耗时: {scanResult.ElapsedMilliseconds}ms");
                    Console.WriteLine($"[ResourceScanService] 统一扫描完成，共找到 {scanResult.Resources.Count} 个资源，耗时: {scanResult.ElapsedMilliseconds}ms");
                    return scanResult.Resources;
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"[ResourceScanService] 统一扫描失败: {scanResult.ErrorMessage}");
                    Console.WriteLine($"[ResourceScanService] 统一扫描失败: {scanResult.ErrorMessage}");
                    return new List<ResourceObject>();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ResourceScanService] 扫描资源失败: {ex.Message}");
                Console.WriteLine($"[ResourceScanService] 扫描资源失败: {ex.Message}");
                return new List<ResourceObject>();
            }
        }

        /// <summary>
        /// 扫描图像文件（只从工作文件夹根目录）
        /// </summary>
        public async Task<List<ResourceObject>> ScanImagesAsync()
        {
            var resources = new List<ResourceObject>();

            try
            {
                var workFolder = _workFolderService.WorkFolder;
                if (!Directory.Exists(workFolder))
                {
                    System.Diagnostics.Debug.WriteLine($"[ResourceScanService] 工作文件夹不存在: {workFolder}");
                    return resources;
                }

                // 获取图像类型定义
                var imageTypeDefinition = ResourceTypeRegistry.GetTypeDefinition(ResourceItemType.Image);
                if (imageTypeDefinition == null)
                {
                    System.Diagnostics.Debug.WriteLine("[ResourceScanService] 图像类型定义未找到");
                    return resources;
                }

                System.Diagnostics.Debug.WriteLine($"[ResourceScanService] 开始扫描图像文件，工作文件夹: {workFolder}");
                Console.WriteLine($"[ResourceScanService] 开始扫描图像文件，工作文件夹: {workFolder}");

                // 只扫描工作文件夹根目录，不递归扫描子目录
                foreach (var extension in imageTypeDefinition.SupportedExtensions)
                {
                    var pattern = $"*{extension}";
                    var imageFiles = Directory.GetFiles(workFolder, pattern, SearchOption.TopDirectoryOnly);

                    foreach (var filePath in imageFiles)
                    {
                        try
                        {
                            var fileInfo = new FileInfo(filePath);
                            var resource = ResourceObject.FromFileInfo(fileInfo, ResourceItemType.Image);

                            // 设置缩略图路径
                            resource.ThumbnailPath = imageTypeDefinition.DefaultIconPath;
                            if (imageTypeDefinition.SupportsThumbnail)
                            {
                                // 尝试生成缩略图
                                resource.Thumbnail = await _thumbnailService.GenerateImageThumbnailAsync(filePath);
                            }

                            // 设置描述
                            resource.Description = imageTypeDefinition.Description;

                            // 添加图像特定的元数据
                            resource.Metadata["Category"] = "Image";
                            resource.Metadata["FileExtension"] = Path.GetExtension(filePath).ToLowerInvariant();

                            resources.Add(resource);

                            System.Diagnostics.Debug.WriteLine($"[ResourceScanService] 扫描到图像: {resource.Name}");
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"[ResourceScanService] 处理图像文件失败 {filePath}: {ex.Message}");
                        }
                    }
                }

                System.Diagnostics.Debug.WriteLine($"[ResourceScanService] 图像扫描完成，共找到 {resources.Count} 个图像文件");
                Console.WriteLine($"[ResourceScanService] 图像扫描完成，共找到 {resources.Count} 个图像文件");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ResourceScanService] 扫描图像失败: {ex.Message}");
            }

            return resources;
        }

        /// <summary>
        /// 扫描注册的资源类型（除了已经专门处理的类型）
        /// 注意：此方法已被UnifiedResourceScanner替代，保留以备兼容性使用
        /// </summary>
        [Obsolete("使用UnifiedResourceScanner替代")]
        private async Task<List<ResourceObject>> ScanRegisteredResourceTypesAsync()
        {
            // 使用统一扫描引擎扫描素材类型
            var scanResult = await _unifiedScanner.ScanResourceTypeAsync(ResourceItemType.Material);
            return scanResult.Success ? scanResult.Resources : new List<ResourceObject>();
        }

        /// <summary>
        /// 扫描节点图文件
        /// 注意：此方法已被UnifiedResourceScanner替代，保留以备兼容性使用
        /// </summary>
        [Obsolete("使用UnifiedResourceScanner替代")]
        public async Task<List<ResourceObject>> ScanNodeGraphsAsync()
        {
            var scanResult = await _unifiedScanner.ScanResourceTypeAsync(ResourceItemType.NodeGraph);
            return scanResult.Success ? scanResult.Resources : new List<ResourceObject>();
        }

        /// <summary>
        /// 扫描模板文件
        /// 注意：此方法已被UnifiedResourceScanner替代，保留以备兼容性使用
        /// </summary>
        [Obsolete("使用UnifiedResourceScanner替代")]
        public async Task<List<ResourceObject>> ScanTemplatesAsync()
        {
            var scanResult = await _unifiedScanner.ScanResourceTypeAsync(ResourceItemType.Template);
            return scanResult.Success ? scanResult.Resources : new List<ResourceObject>();
        }

        /// <summary>
        /// 扫描脚本文件
        /// 注意：此方法已被UnifiedResourceScanner替代，保留以备兼容性使用
        /// </summary>
        [Obsolete("使用UnifiedResourceScanner替代")]
        public async Task<List<ResourceObject>> ScanScriptsAsync()
        {
            var scanResult = await _unifiedScanner.ScanResourceTypeAsync(ResourceItemType.Script);
            return scanResult.Success ? scanResult.Resources : new List<ResourceObject>();
        }

        /// <summary>
        /// 获取默认缩略图路径
        /// </summary>
        private string GetDefaultThumbnailPath(ResourceItemType resourceType)
        {
            return ResourceTypeRegistry.GetDefaultIconPath(resourceType);
        }

        /// <summary>
        /// 加载缩略图
        /// </summary>
        private async Task<BitmapSource?> LoadThumbnailAsync(string thumbnailPath)
        {
            try
            {
                if (!File.Exists(thumbnailPath)) return null;

                return await Task.Run(() =>
                {
                    // 使用FileStream读取文件到内存，然后立即关闭文件句柄
                    byte[] imageBytes;
                    using (var fileStream = new FileStream(thumbnailPath, FileMode.Open, FileAccess.Read, FileShare.Read))
                    {
                        imageBytes = new byte[fileStream.Length];
                        fileStream.Read(imageBytes, 0, imageBytes.Length);
                    }

                    // 从内存中的字节数组创建BitmapImage，确保MemoryStream被正确释放
                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    using (var memoryStream = new MemoryStream(imageBytes))
                    {
                        bitmap.StreamSource = memoryStream;
                        bitmap.DecodePixelWidth = 64; // 缩略图大小
                        bitmap.DecodePixelHeight = 64;
                        bitmap.CacheOption = BitmapCacheOption.OnLoad;
                        bitmap.EndInit();
                    }
                    bitmap.Freeze();
                    return bitmap;
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ResourceScanService] 加载缩略图失败 {thumbnailPath}: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 加载默认缩略图
        /// </summary>
        private async Task<BitmapSource?> LoadDefaultThumbnailAsync(ResourceItemType resourceType)
        {
            // 这里可以加载软件资源文件夹中的默认图标
            // 暂时返回null，后续可以添加默认图标
            await Task.CompletedTask;
            return null;
        }

        /// <summary>
        /// 从文件内容中提取脚本信息
        /// </summary>
        private async Task ExtractScriptInfoFromFileAsync(ResourceObject resource, string filePath)
        {
            try
            {
                var extension = Path.GetExtension(filePath).ToLowerInvariant();

                if (extension == ".cs")
                {
                    // 提取C#脚本信息
                    await ExtractCSharpScriptInfoAsync(resource, filePath);
                }
                else if (extension == ".sn")
                {
                    // 提取SymbolNode脚本信息
                    await ExtractSymbolNodeScriptInfoAsync(resource, filePath);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ResourceScanService] 提取脚本信息失败 {filePath}: {ex.Message}");
            }
        }

        /// <summary>
        /// 提取C#脚本信息
        /// </summary>
        private async Task ExtractCSharpScriptInfoAsync(ResourceObject resource, string filePath)
        {
            try
            {
                var content = await File.ReadAllTextAsync(filePath);
                var lines = content.Split('\n', StringSplitOptions.RemoveEmptyEntries);

                // 查找类名和命名空间
                string? className = null;
                string? namespaceName = null;
                string? description = null;
                string? category = null;

                foreach (var line in lines)
                {
                    var trimmedLine = line.Trim();

                    // 查找命名空间
                    if (trimmedLine.StartsWith("namespace "))
                    {
                        namespaceName = trimmedLine.Substring(10).Trim().TrimEnd('{').Trim();
                    }

                    // 查找类名
                    if (trimmedLine.Contains("class ") && trimmedLine.Contains("RevivalScriptBase"))
                    {
                        var classIndex = trimmedLine.IndexOf("class ");
                        var colonIndex = trimmedLine.IndexOf(":");
                        if (classIndex >= 0 && colonIndex > classIndex)
                        {
                            className = trimmedLine.Substring(classIndex + 6, colonIndex - classIndex - 6).Trim();
                        }
                    }

                    // 查找ScriptInfo特性
                    if (trimmedLine.Contains("[ScriptInfo("))
                    {
                        // 简单的特性解析
                        if (trimmedLine.Contains("Description"))
                        {
                            var descMatch = System.Text.RegularExpressions.Regex.Match(trimmedLine, @"Description\s*=\s*""([^""]+)""");
                            if (descMatch.Success)
                            {
                                description = descMatch.Groups[1].Value;
                            }
                        }

                        if (trimmedLine.Contains("Category"))
                        {
                            var catMatch = System.Text.RegularExpressions.Regex.Match(trimmedLine, @"Category\s*=\s*""([^""]+)""");
                            if (catMatch.Success)
                            {
                                category = catMatch.Groups[1].Value;
                            }
                        }
                    }
                }

                // 更新资源信息
                if (!string.IsNullOrEmpty(className))
                {
                    resource.Metadata["ClassName"] = className;
                }

                if (!string.IsNullOrEmpty(namespaceName))
                {
                    resource.Metadata["Namespace"] = namespaceName;

                    // 从命名空间推断分类
                    if (string.IsNullOrEmpty(category))
                    {
                        var namespaceParts = namespaceName.Split('.');
                        if (namespaceParts.Length > 1)
                        {
                            category = namespaceParts[namespaceParts.Length - 1];
                        }
                    }
                }

                if (!string.IsNullOrEmpty(description))
                {
                    resource.Description = description;
                }

                if (!string.IsNullOrEmpty(category))
                {
                    resource.Metadata["Category"] = category;
                    if (!string.IsNullOrEmpty(description))
                    {
                        resource.Description = $"{category} - {description}";
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ResourceScanService] 提取C#脚本信息失败 {filePath}: {ex.Message}");
            }
        }

        /// <summary>
        /// 提取SymbolNode脚本信息
        /// </summary>
        private async Task ExtractSymbolNodeScriptInfoAsync(ResourceObject resource, string filePath)
        {
            try
            {
                var content = await File.ReadAllTextAsync(filePath);

                // SymbolNode文件通常是JSON格式，可以尝试解析
                // 这里做简单的文本解析，查找常见的元数据字段

                if (content.Contains("\"name\""))
                {
                    var nameMatch = System.Text.RegularExpressions.Regex.Match(content, @"""name""\s*:\s*""([^""]+)""");
                    if (nameMatch.Success)
                    {
                        resource.Metadata["SymbolNodeName"] = nameMatch.Groups[1].Value;
                    }
                }

                if (content.Contains("\"description\""))
                {
                    var descMatch = System.Text.RegularExpressions.Regex.Match(content, @"""description""\s*:\s*""([^""]+)""");
                    if (descMatch.Success)
                    {
                        resource.Description = descMatch.Groups[1].Value;
                    }
                }

                if (content.Contains("\"category\""))
                {
                    var catMatch = System.Text.RegularExpressions.Regex.Match(content, @"""category""\s*:\s*""([^""]+)""");
                    if (catMatch.Success)
                    {
                        resource.Metadata["Category"] = catMatch.Groups[1].Value;
                    }
                }

                resource.Metadata["IsSymbolNode"] = true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ResourceScanService] 提取SymbolNode脚本信息失败 {filePath}: {ex.Message}");
            }
        }
    }
}
