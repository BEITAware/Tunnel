using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Tunnel_Next.Models;

namespace Tunnel_Next.Services
{
    /// <summary>
    /// 资源目录管理服务
    /// </summary>
    public class ResourceCatalogService
    {
        private const string CatalogFileName = "Catalog.json";
        private readonly WorkFolderService _workFolderService;
        private readonly string _catalogFilePath;
        private ResourceCatalog _catalog;

        /// <summary>
        /// 资源目录变化事件
        /// </summary>
        public event Action<ResourceCatalog>? CatalogChanged;

        /// <summary>
        /// 当前资源目录
        /// </summary>
        public ResourceCatalog Catalog => _catalog;

        public ResourceCatalogService(WorkFolderService workFolderService)
        {
            _workFolderService = workFolderService ?? throw new ArgumentNullException(nameof(workFolderService));
            
            // 确保Resources文件夹存在
            var resourcesFolder = Path.Combine(_workFolderService.WorkFolder, "Resources");
            Directory.CreateDirectory(resourcesFolder);
            
            _catalogFilePath = Path.Combine(resourcesFolder, CatalogFileName);
            _catalog = new ResourceCatalog();
        }

        /// <summary>
        /// 加载资源目录
        /// 注意：当前资源扫描模式下暂时不使用磁盘持久化，此方法保留以备将来使用
        /// </summary>
        public async Task<bool> LoadCatalogAsync()
        {
            try
            {
                if (!File.Exists(_catalogFilePath))
                {
                    // 如果目录文件不存在，创建新的空目录
                    _catalog = new ResourceCatalog();
                    await SaveCatalogAsync();
                    return true;
                }

                var json = await File.ReadAllTextAsync(_catalogFilePath);
                var options = new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    WriteIndented = true
                };

                var catalog = JsonSerializer.Deserialize<ResourceCatalog>(json, options);
                if (catalog != null)
                {
                    _catalog = catalog;
                    _catalog.UpdateStatistics();
                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ResourceCatalogService] 加载目录失败: {ex.Message}");
                // 如果加载失败，创建新的空目录
                _catalog = new ResourceCatalog();
                return false;
            }
        }

        /// <summary>
        /// 保存资源目录
        /// 注意：当前资源扫描模式下暂时不使用磁盘持久化，此方法保留以备将来使用
        /// </summary>
        public async Task<bool> SaveCatalogAsync()
        {
            try
            {
                _catalog.LastUpdated = DateTime.Now;
                _catalog.UpdateStatistics();

                var options = new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    WriteIndented = true
                };

                var json = JsonSerializer.Serialize(_catalog, options);
                await File.WriteAllTextAsync(_catalogFilePath, json);

                CatalogChanged?.Invoke(_catalog);
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ResourceCatalogService] 保存目录失败: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 添加资源对象
        /// 注意：当前资源扫描模式下暂时不使用磁盘持久化，此方法保留以备将来使用
        /// </summary>
        public async Task<bool> AddResourceAsync(ResourceObject resource)
        {
            try
            {
                // 检查是否已存在相同路径的资源
                var existing = _catalog.Resources.FirstOrDefault(r => r.FilePath.Equals(resource.FilePath, StringComparison.OrdinalIgnoreCase));
                if (existing != null)
                {
                    // 更新现有资源
                    existing.Name = resource.Name;
                    existing.ModifiedTime = resource.ModifiedTime;
                    existing.FileSize = resource.FileSize;
                    existing.ThumbnailPath = resource.ThumbnailPath;
                    existing.Description = resource.Description;
                    existing.Metadata = resource.Metadata;
                }
                else
                {
                    // 添加新资源
                    _catalog.Resources.Add(resource);
                }

                return await SaveCatalogAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ResourceCatalogService] 添加资源失败: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 批量添加资源对象
        /// 注意：当前资源扫描模式下暂时不使用磁盘持久化，此方法保留以备将来使用
        /// </summary>
        public async Task<bool> AddResourcesAsync(IEnumerable<ResourceObject> resources)
        {
            try
            {
                foreach (var resource in resources)
                {
                    var existing = _catalog.Resources.FirstOrDefault(r => r.FilePath.Equals(resource.FilePath, StringComparison.OrdinalIgnoreCase));
                    if (existing != null)
                    {
                        // 更新现有资源
                        existing.Name = resource.Name;
                        existing.ModifiedTime = resource.ModifiedTime;
                        existing.FileSize = resource.FileSize;
                        existing.ThumbnailPath = resource.ThumbnailPath;
                        existing.Description = resource.Description;
                        existing.Metadata = resource.Metadata;
                    }
                    else
                    {
                        // 添加新资源
                        _catalog.Resources.Add(resource);
                    }
                }

                return await SaveCatalogAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ResourceCatalogService] 批量添加资源失败: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 移除资源对象
        /// </summary>
        public async Task<bool> RemoveResourceAsync(string filePath)
        {
            try
            {
                var resource = _catalog.Resources.FirstOrDefault(r => r.FilePath.Equals(filePath, StringComparison.OrdinalIgnoreCase));
                if (resource != null)
                {
                    _catalog.Resources.Remove(resource);
                    return await SaveCatalogAsync();
                }
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ResourceCatalogService] 移除资源失败: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 清理无效资源（文件不存在的资源）
        /// 注意：当前资源扫描模式下暂时不使用磁盘持久化，此方法保留以备将来使用
        /// </summary>
        public async Task<int> CleanupInvalidResourcesAsync()
        {
            try
            {
                var invalidResources = _catalog.Resources.Where(r => !r.FileExists).ToList();
                foreach (var resource in invalidResources)
                {
                    _catalog.Resources.Remove(resource);
                }

                if (invalidResources.Count > 0)
                {
                    await SaveCatalogAsync();
                }

                return invalidResources.Count;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ResourceCatalogService] 清理无效资源失败: {ex.Message}");
                return 0;
            }
        }

        /// <summary>
        /// 根据类型获取资源
        /// </summary>
        public List<ResourceObject> GetResourcesByType(ResourceItemType resourceType)
        {
            return _catalog.Resources.Where(r => r.ResourceType == resourceType).ToList();
        }

        /// <summary>
        /// 根据路径获取资源
        /// </summary>
        public ResourceObject? GetResourceByPath(string filePath)
        {
            return _catalog.Resources.FirstOrDefault(r => r.FilePath.Equals(filePath, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// 搜索资源
        /// </summary>
        public List<ResourceObject> SearchResources(string searchTerm)
        {
            if (string.IsNullOrWhiteSpace(searchTerm))
                return _catalog.Resources.ToList();

            var term = searchTerm.ToLowerInvariant();
            return _catalog.Resources.Where(r => 
                r.Name.ToLowerInvariant().Contains(term) ||
                r.Description.ToLowerInvariant().Contains(term) ||
                Path.GetFileName(r.FilePath).ToLowerInvariant().Contains(term)
            ).ToList();
        }

        /// <summary>
        /// 获取资源统计信息
        /// </summary>
        public Dictionary<ResourceItemType, int> GetStatistics()
        {
            _catalog.UpdateStatistics();
            return new Dictionary<ResourceItemType, int>(_catalog.Statistics);
        }
    }
}
