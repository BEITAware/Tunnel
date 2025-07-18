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
        private readonly RevivalScriptManager? _scriptManager;

        /// <summary>
        /// 支持的节点图文件扩展名
        /// </summary>
        private static readonly string[] NodeGraphExtensions = { ".nodegraph" };

        /// <summary>
        /// 支持的脚本文件扩展名
        /// </summary>
        private static readonly string[] ScriptExtensions = { ".cs", ".sn" };

        public ResourceScanService(WorkFolderService workFolderService, ThumbnailService thumbnailService, RevivalScriptManager? scriptManager = null)
        {
            _workFolderService = workFolderService ?? throw new ArgumentNullException(nameof(workFolderService));
            _thumbnailService = thumbnailService ?? throw new ArgumentNullException(nameof(thumbnailService));
            _scriptManager = scriptManager;
        }

        /// <summary>
        /// 扫描所有资源
        /// </summary>
        public async Task<List<ResourceObject>> ScanAllResourcesAsync()
        {
            var resources = new List<ResourceObject>();

            try
            {
                // 扫描节点图
                var nodeGraphs = await ScanNodeGraphsAsync();
                resources.AddRange(nodeGraphs);

                // 扫描模板
                var templates = await ScanTemplatesAsync();
                resources.AddRange(templates);

                // 扫描脚本
                var scripts = await ScanScriptsAsync();
                resources.AddRange(scripts);

                System.Diagnostics.Debug.WriteLine($"[ResourceScanService] 扫描完成，共找到 {resources.Count} 个资源");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ResourceScanService] 扫描资源失败: {ex.Message}");
            }

            return resources;
        }

        /// <summary>
        /// 扫描节点图文件
        /// </summary>
        public async Task<List<ResourceObject>> ScanNodeGraphsAsync()
        {
            var resources = new List<ResourceObject>();

            try
            {
                var projectsFolder = Path.Combine(_workFolderService.WorkFolder, "Projects");
                if (!Directory.Exists(projectsFolder))
                {
                    Directory.CreateDirectory(projectsFolder);
                    return resources;
                }

                var nodeGraphFiles = Directory.GetFiles(projectsFolder, "*.nodegraph", SearchOption.AllDirectories);
                
                foreach (var filePath in nodeGraphFiles)
                {
                    try
                    {
                        var fileInfo = new FileInfo(filePath);
                        var resource = ResourceObject.FromFileInfo(fileInfo, ResourceItemType.NodeGraph);
                        
                        // 设置缩略图路径（使用通用的节点图图标）
                        resource.ThumbnailPath = GetDefaultThumbnailPath(ResourceItemType.NodeGraph);
                        
                        // 尝试加载缩略图
                        resource.Thumbnail = await LoadDefaultThumbnailAsync(ResourceItemType.NodeGraph);

                        // 添加节点图特定的元数据
                        resource.Metadata["Category"] = "NodeGraph";
                        resource.Description = "节点图文件";

                        resources.Add(resource);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[ResourceScanService] 处理节点图文件失败 {filePath}: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ResourceScanService] 扫描节点图失败: {ex.Message}");
            }

            return resources;
        }

        /// <summary>
        /// 扫描模板文件
        /// </summary>
        public async Task<List<ResourceObject>> ScanTemplatesAsync()
        {
            var resources = new List<ResourceObject>();

            try
            {
                var templatesFolder = Path.Combine(_workFolderService.WorkFolder, "Resources", "Templates");
                if (!Directory.Exists(templatesFolder))
                {
                    Directory.CreateDirectory(templatesFolder);
                    return resources;
                }

                var templateDirs = Directory.GetDirectories(templatesFolder);
                
                foreach (var templateDir in templateDirs)
                {
                    try
                    {
                        var templateName = Path.GetFileName(templateDir);
                        var nodeGraphFile = Directory.GetFiles(templateDir, "*.nodegraph").FirstOrDefault();
                        
                        if (nodeGraphFile == null) continue; // 必须包含节点图文件

                        var fileInfo = new FileInfo(nodeGraphFile);
                        var resource = ResourceObject.FromFileInfo(fileInfo, ResourceItemType.Template);
                        resource.Name = templateName; // 使用文件夹名作为模板名

                        // 查找缩略图
                        var thumbnailPath = Path.Combine(templateDir, "thumbnail.png");
                        if (File.Exists(thumbnailPath))
                        {
                            resource.ThumbnailPath = thumbnailPath;
                            resource.Thumbnail = await LoadThumbnailAsync(thumbnailPath);
                        }
                        else
                        {
                            // 使用默认模板图标
                            resource.ThumbnailPath = GetDefaultThumbnailPath(ResourceItemType.Template);
                            resource.Thumbnail = await LoadDefaultThumbnailAsync(ResourceItemType.Template);
                        }

                        // 添加模板特定的元数据
                        resource.Metadata["Category"] = "Template";
                        resource.Metadata["TemplateFolder"] = templateDir;
                        resource.Description = "节点图模板";

                        resources.Add(resource);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[ResourceScanService] 处理模板失败 {templateDir}: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ResourceScanService] 扫描模板失败: {ex.Message}");
            }

            return resources;
        }

        /// <summary>
        /// 扫描脚本文件
        /// </summary>
        public async Task<List<ResourceObject>> ScanScriptsAsync()
        {
            var resources = new List<ResourceObject>();

            try
            {
                if (_scriptManager == null)
                {
                    System.Diagnostics.Debug.WriteLine("[ResourceScanService] 脚本管理器未初始化，跳过脚本扫描");
                    Console.WriteLine("[ResourceScanService] 脚本管理器未初始化，跳过脚本扫描");
                    return resources;
                }

                var scriptsFolder = _workFolderService.UserScriptsFolder;
                System.Diagnostics.Debug.WriteLine($"[ResourceScanService] 脚本文件夹路径: {scriptsFolder}");
                Console.WriteLine($"[ResourceScanService] 脚本文件夹路径: {scriptsFolder}");

                if (!Directory.Exists(scriptsFolder))
                {
                    System.Diagnostics.Debug.WriteLine($"[ResourceScanService] 脚本文件夹不存在: {scriptsFolder}");
                    Console.WriteLine($"[ResourceScanService] 脚本文件夹不存在: {scriptsFolder}");
                    return resources;
                }

                // 获取所有脚本文件
                var scriptFiles = new List<string>();
                foreach (var extension in ScriptExtensions)
                {
                    var files = Directory.GetFiles(scriptsFolder, $"*{extension}", SearchOption.AllDirectories);
                    System.Diagnostics.Debug.WriteLine($"[ResourceScanService] 找到 {files.Length} 个 {extension} 文件");
                    Console.WriteLine($"[ResourceScanService] 找到 {files.Length} 个 {extension} 文件");
                    scriptFiles.AddRange(files);
                }

                System.Diagnostics.Debug.WriteLine($"[ResourceScanService] 总共找到 {scriptFiles.Count} 个脚本文件");
                Console.WriteLine($"[ResourceScanService] 总共找到 {scriptFiles.Count} 个脚本文件");

                foreach (var filePath in scriptFiles)
                {
                    try
                    {
                        var fileInfo = new FileInfo(filePath);
                        var resource = ResourceObject.FromFileInfo(fileInfo, ResourceItemType.Script);
                        
                        // 设置缩略图路径（使用通用的脚本图标）
                        resource.ThumbnailPath = GetDefaultThumbnailPath(ResourceItemType.Script);
                        resource.Thumbnail = await LoadDefaultThumbnailAsync(ResourceItemType.Script);

                        // 添加脚本特定的元数据
                        resource.Metadata["Category"] = "Script";
                        resource.Metadata["ScriptType"] = Path.GetExtension(filePath).ToLowerInvariant() == ".cs" ? "CSharp" : "SymbolNode";
                        
                        // 尝试从脚本管理器获取更多信息
                        var relativePath = Path.GetRelativePath(scriptsFolder, filePath);
                        var availableScripts = _scriptManager.GetAvailableRevivalScripts();
                        if (availableScripts.TryGetValue(relativePath, out var scriptInfo))
                        {
                            resource.Description = scriptInfo.Description ?? "脚本文件";
                            resource.Metadata["ScriptName"] = scriptInfo.Name;
                            resource.Metadata["Category"] = scriptInfo.Category ?? "Script";
                            resource.Metadata["IsCompiled"] = scriptInfo.IsCompiled;
                            resource.Metadata["IsSymbolNode"] = scriptInfo.IsSymbolNode;

                            // 如果脚本有分类，使用分类作为描述的一部分
                            if (!string.IsNullOrEmpty(scriptInfo.Category))
                            {
                                resource.Description = $"{scriptInfo.Category} - {resource.Description}";
                            }

                            // 添加编译状态信息
                            if (!scriptInfo.IsSymbolNode)
                            {
                                resource.Metadata["CompilationStatus"] = scriptInfo.IsCompiled ? "已编译" : "未编译";
                            }
                        }
                        else
                        {
                            resource.Description = "脚本文件";
                            // 尝试从文件内容中提取基本信息
                            await ExtractScriptInfoFromFileAsync(resource, filePath);
                        }

                        resources.Add(resource);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[ResourceScanService] 处理脚本文件失败 {filePath}: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ResourceScanService] 扫描脚本失败: {ex.Message}");
            }

            return resources;
        }

        /// <summary>
        /// 获取默认缩略图路径
        /// </summary>
        private string GetDefaultThumbnailPath(ResourceItemType resourceType)
        {
            // 这里应该返回软件资源文件夹中的默认图标路径
            // 暂时返回空字符串，后续可以添加默认图标
            return string.Empty;
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
                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.UriSource = new Uri(thumbnailPath);
                    bitmap.DecodePixelWidth = 64; // 缩略图大小
                    bitmap.DecodePixelHeight = 64;
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.EndInit();
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
