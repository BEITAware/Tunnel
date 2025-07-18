using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Tunnel_Next.Models;

namespace Tunnel_Next.Services
{
    /// <summary>
    /// 资源文件监控服务
    /// </summary>
    public class ResourceWatcherService : IDisposable
    {
        private readonly WorkFolderService _workFolderService;
        private readonly ResourceCatalogService _catalogService;
        private readonly ResourceScanService _scanService;
        private readonly List<FileSystemWatcher> _watchers = new();
        private bool _disposed = false;

        /// <summary>
        /// 资源变化事件
        /// </summary>
        public event Action<string, WatcherChangeTypes>? ResourceChanged;

        public ResourceWatcherService(WorkFolderService workFolderService, ResourceCatalogService catalogService, ResourceScanService scanService)
        {
            _workFolderService = workFolderService ?? throw new ArgumentNullException(nameof(workFolderService));
            _catalogService = catalogService ?? throw new ArgumentNullException(nameof(catalogService));
            _scanService = scanService ?? throw new ArgumentNullException(nameof(scanService));
        }

        /// <summary>
        /// 启动文件监控
        /// </summary>
        public void StartWatching()
        {
            if (_disposed) return;

            try
            {
                // 监控Projects文件夹（节点图文件）
                var projectsFolder = Path.Combine(_workFolderService.WorkFolder, "Projects");
                if (Directory.Exists(projectsFolder))
                {
                    var projectsWatcher = CreateWatcher(projectsFolder, "*.nodegraph", true);
                    _watchers.Add(projectsWatcher);
                }

                // 监控Templates文件夹（模板文件）
                var templatesFolder = Path.Combine(_workFolderService.WorkFolder, "Resources", "Templates");
                if (Directory.Exists(templatesFolder))
                {
                    var templatesWatcher = CreateWatcher(templatesFolder, "*.nodegraph", true);
                    _watchers.Add(templatesWatcher);
                }

                // 监控Scripts文件夹（脚本文件）
                var scriptsFolder = _workFolderService.UserScriptsFolder;
                if (Directory.Exists(scriptsFolder))
                {
                    var scriptsWatcher = CreateWatcher(scriptsFolder, "*.cs", true);
                    _watchers.Add(scriptsWatcher);
                    
                    var symbolNodeWatcher = CreateWatcher(scriptsFolder, "*.sn", true);
                    _watchers.Add(symbolNodeWatcher);
                }

                System.Diagnostics.Debug.WriteLine($"[ResourceWatcherService] 已启动 {_watchers.Count} 个文件监控器");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ResourceWatcherService] 启动文件监控失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 停止文件监控
        /// </summary>
        public void StopWatching()
        {
            try
            {
                foreach (var watcher in _watchers)
                {
                    watcher.EnableRaisingEvents = false;
                    watcher.Dispose();
                }
                _watchers.Clear();

                System.Diagnostics.Debug.WriteLine("[ResourceWatcherService] 已停止所有文件监控器");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ResourceWatcherService] 停止文件监控失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 创建文件监控器
        /// </summary>
        private FileSystemWatcher CreateWatcher(string path, string filter, bool includeSubdirectories)
        {
            var watcher = new FileSystemWatcher(path, filter)
            {
                IncludeSubdirectories = includeSubdirectories,
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.CreationTime
            };

            watcher.Created += OnFileChanged;
            watcher.Changed += OnFileChanged;
            watcher.Deleted += OnFileChanged;
            watcher.Renamed += OnFileRenamed;

            watcher.EnableRaisingEvents = true;

            return watcher;
        }

        /// <summary>
        /// 文件变化事件处理
        /// </summary>
        private async void OnFileChanged(object sender, FileSystemEventArgs e)
        {
            try
            {
                // 延迟处理，避免频繁触发
                await Task.Delay(500);

                System.Diagnostics.Debug.WriteLine($"[ResourceWatcherService] 文件变化: {e.ChangeType} - {e.FullPath}");

                // 触发资源变化事件
                ResourceChanged?.Invoke(e.FullPath, e.ChangeType);

                // 根据变化类型处理
                switch (e.ChangeType)
                {
                    case WatcherChangeTypes.Created:
                        await HandleFileCreated(e.FullPath);
                        break;
                    case WatcherChangeTypes.Changed:
                        await HandleFileChanged(e.FullPath);
                        break;
                    case WatcherChangeTypes.Deleted:
                        await HandleFileDeleted(e.FullPath);
                        break;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ResourceWatcherService] 处理文件变化失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 文件重命名事件处理
        /// </summary>
        private async void OnFileRenamed(object sender, RenamedEventArgs e)
        {
            try
            {
                await Task.Delay(500);

                System.Diagnostics.Debug.WriteLine($"[ResourceWatcherService] 文件重命名: {e.OldFullPath} -> {e.FullPath}");

                // 移除旧资源
                await _catalogService.RemoveResourceAsync(e.OldFullPath);

                // 添加新资源
                await HandleFileCreated(e.FullPath);

                ResourceChanged?.Invoke(e.FullPath, WatcherChangeTypes.Renamed);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ResourceWatcherService] 处理文件重命名失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 处理文件创建
        /// </summary>
        private async Task HandleFileCreated(string filePath)
        {
            try
            {
                var resourceType = GetResourceTypeFromPath(filePath);
                if (resourceType == ResourceItemType.Other) return;

                var fileInfo = new FileInfo(filePath);
                var resource = ResourceObject.FromFileInfo(fileInfo, resourceType);

                // 设置特定类型的属性
                await SetResourceSpecificProperties(resource);

                await _catalogService.AddResourceAsync(resource);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ResourceWatcherService] 处理文件创建失败 {filePath}: {ex.Message}");
            }
        }

        /// <summary>
        /// 处理文件修改
        /// </summary>
        private async Task HandleFileChanged(string filePath)
        {
            try
            {
                var resource = _catalogService.GetResourceByPath(filePath);
                if (resource != null)
                {
                    resource.UpdateFromFileInfo();
                    await _catalogService.AddResourceAsync(resource); // 更新现有资源
                }
                else
                {
                    // 如果资源不存在，作为新文件处理
                    await HandleFileCreated(filePath);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ResourceWatcherService] 处理文件修改失败 {filePath}: {ex.Message}");
            }
        }

        /// <summary>
        /// 处理文件删除
        /// </summary>
        private async Task HandleFileDeleted(string filePath)
        {
            try
            {
                await _catalogService.RemoveResourceAsync(filePath);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ResourceWatcherService] 处理文件删除失败 {filePath}: {ex.Message}");
            }
        }

        /// <summary>
        /// 根据文件路径获取资源类型
        /// </summary>
        private ResourceItemType GetResourceTypeFromPath(string filePath)
        {
            var extension = Path.GetExtension(filePath).ToLowerInvariant();
            var directory = Path.GetDirectoryName(filePath) ?? string.Empty;

            if (extension == ".nodegraph")
            {
                if (directory.Contains("Templates"))
                    return ResourceItemType.Template;
                else
                    return ResourceItemType.NodeGraph;
            }
            else if (extension == ".cs" || extension == ".sn")
            {
                return ResourceItemType.Script;
            }

            return ResourceItemType.Other;
        }

        /// <summary>
        /// 设置资源特定属性
        /// </summary>
        private async Task SetResourceSpecificProperties(ResourceObject resource)
        {
            switch (resource.ResourceType)
            {
                case ResourceItemType.Template:
                    var templateFolder = Path.GetDirectoryName(resource.FilePath);
                    if (!string.IsNullOrEmpty(templateFolder))
                    {
                        resource.Metadata["TemplateFolder"] = templateFolder;
                        
                        var thumbnailPath = Path.Combine(templateFolder, "thumbnail.png");
                        if (File.Exists(thumbnailPath))
                        {
                            resource.ThumbnailPath = thumbnailPath;
                        }
                    }
                    break;

                case ResourceItemType.Script:
                    resource.Metadata["Category"] = "Script";
                    resource.Metadata["ScriptType"] = Path.GetExtension(resource.FilePath).ToLowerInvariant() == ".cs" ? "CSharp" : "SymbolNode";
                    break;
            }

            await Task.CompletedTask;
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                StopWatching();
                _disposed = true;
            }
        }
    }
}
