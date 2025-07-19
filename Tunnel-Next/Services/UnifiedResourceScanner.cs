using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Tunnel_Next.Models;

namespace Tunnel_Next.Services
{
    /// <summary>
    /// 统一的资源扫描引擎
    /// 使用委托模式扫描所有注册的资源类型
    /// </summary>
    public class UnifiedResourceScanner
    {
        private readonly WorkFolderService _workFolderService;
        private readonly IServiceProvider? _serviceProvider;

        /// <summary>
        /// 构造函数
        /// </summary>
        public UnifiedResourceScanner(WorkFolderService workFolderService, IServiceProvider? serviceProvider = null)
        {
            _workFolderService = workFolderService ?? throw new ArgumentNullException(nameof(workFolderService));
            _serviceProvider = serviceProvider;
        }

        /// <summary>
        /// 扫描所有资源
        /// </summary>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>扫描结果</returns>
        public async Task<ResourceScanResult> ScanAllResourcesAsync(CancellationToken cancellationToken = default)
        {
            var stopwatch = Stopwatch.StartNew();
            var allResources = new List<ResourceObject>();
            var scannedFileCount = 0;

            try
            {
                System.Diagnostics.Debug.WriteLine("[UnifiedResourceScanner] 开始扫描所有资源类型");
                Console.WriteLine("[UnifiedResourceScanner] 开始扫描所有资源类型");

                // 获取所有已注册的资源类型，按优先级排序
                var typeDefinitions = ResourceTypeRegistry.GetAllTypes()
                    .Where(t => t.ScanDelegate != null) // 只扫描有委托的类型
                    .OrderBy(t => t.ScanPriority)
                    .ToList();

                System.Diagnostics.Debug.WriteLine($"[UnifiedResourceScanner] 找到 {typeDefinitions.Count} 个可扫描的资源类型");

                foreach (var typeDefinition in typeDefinitions)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        System.Diagnostics.Debug.WriteLine("[UnifiedResourceScanner] 扫描被取消");
                        break;
                    }

                    try
                    {
                        var typeStopwatch = Stopwatch.StartNew();
                        
                        System.Diagnostics.Debug.WriteLine($"[UnifiedResourceScanner] 开始扫描 {typeDefinition.DisplayName} (优先级: {typeDefinition.ScanPriority})");

                        // 创建扫描上下文
                        var context = new ResourceScanContext
                        {
                            WorkFolder = _workFolderService.WorkFolder,
                            TypeDefinition = typeDefinition,
                            Services = _serviceProvider,
                            CancellationToken = cancellationToken
                        };

                        // 调用扫描委托
                        var resources = await typeDefinition.ScanDelegate!(context);
                        
                        allResources.AddRange(resources);
                        scannedFileCount += resources.Count;

                        typeStopwatch.Stop();
                        
                        System.Diagnostics.Debug.WriteLine($"[UnifiedResourceScanner] {typeDefinition.DisplayName} 扫描完成: {resources.Count} 个资源, 耗时: {typeStopwatch.ElapsedMilliseconds}ms");
                        Console.WriteLine($"[UnifiedResourceScanner] {typeDefinition.DisplayName} 扫描完成: {resources.Count} 个资源, 耗时: {typeStopwatch.ElapsedMilliseconds}ms");
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[UnifiedResourceScanner] 扫描 {typeDefinition.DisplayName} 失败: {ex.Message}");
                        Console.WriteLine($"[UnifiedResourceScanner] 扫描 {typeDefinition.DisplayName} 失败: {ex.Message}");
                        // 继续扫描其他类型，不因单个类型失败而中断
                    }
                }

                stopwatch.Stop();

                System.Diagnostics.Debug.WriteLine($"[UnifiedResourceScanner] 扫描完成，共找到 {allResources.Count} 个资源，总耗时: {stopwatch.ElapsedMilliseconds}ms");
                Console.WriteLine($"[UnifiedResourceScanner] 扫描完成，共找到 {allResources.Count} 个资源，总耗时: {stopwatch.ElapsedMilliseconds}ms");

                return new ResourceScanResult
                {
                    Resources = allResources,
                    Success = true,
                    ElapsedMilliseconds = stopwatch.ElapsedMilliseconds,
                    ScannedFileCount = scannedFileCount
                };
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                
                System.Diagnostics.Debug.WriteLine($"[UnifiedResourceScanner] 扫描失败: {ex.Message}");
                Console.WriteLine($"[UnifiedResourceScanner] 扫描失败: {ex.Message}");

                return new ResourceScanResult
                {
                    Resources = allResources,
                    Success = false,
                    ErrorMessage = ex.Message,
                    ElapsedMilliseconds = stopwatch.ElapsedMilliseconds,
                    ScannedFileCount = scannedFileCount
                };
            }
        }

        /// <summary>
        /// 扫描特定类型的资源
        /// </summary>
        /// <param name="resourceType">资源类型</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>扫描结果</returns>
        public async Task<ResourceScanResult> ScanResourceTypeAsync(ResourceItemType resourceType, CancellationToken cancellationToken = default)
        {
            var stopwatch = Stopwatch.StartNew();

            try
            {
                var typeDefinition = ResourceTypeRegistry.GetTypeDefinition(resourceType);
                if (typeDefinition?.ScanDelegate == null)
                {
                    return new ResourceScanResult
                    {
                        Success = false,
                        ErrorMessage = $"资源类型 {resourceType} 没有注册扫描委托"
                    };
                }

                System.Diagnostics.Debug.WriteLine($"[UnifiedResourceScanner] 开始扫描单个资源类型: {typeDefinition.DisplayName}");

                // 创建扫描上下文
                var context = new ResourceScanContext
                {
                    WorkFolder = _workFolderService.WorkFolder,
                    TypeDefinition = typeDefinition,
                    Services = _serviceProvider,
                    CancellationToken = cancellationToken
                };

                // 调用扫描委托
                var resources = await typeDefinition.ScanDelegate(context);

                stopwatch.Stop();

                System.Diagnostics.Debug.WriteLine($"[UnifiedResourceScanner] {typeDefinition.DisplayName} 单独扫描完成: {resources.Count} 个资源，耗时: {stopwatch.ElapsedMilliseconds}ms");

                return new ResourceScanResult
                {
                    Resources = resources,
                    Success = true,
                    ElapsedMilliseconds = stopwatch.ElapsedMilliseconds,
                    ScannedFileCount = resources.Count
                };
            }
            catch (Exception ex)
            {
                stopwatch.Stop();

                System.Diagnostics.Debug.WriteLine($"[UnifiedResourceScanner] 扫描资源类型 {resourceType} 失败: {ex.Message}");

                return new ResourceScanResult
                {
                    Success = false,
                    ErrorMessage = ex.Message,
                    ElapsedMilliseconds = stopwatch.ElapsedMilliseconds
                };
            }
        }

        /// <summary>
        /// 获取所有可扫描的资源类型
        /// </summary>
        /// <returns>可扫描的资源类型列表</returns>
        public List<ResourceItemType> GetScannableResourceTypes()
        {
            return ResourceTypeRegistry.GetAllTypes()
                .Where(t => t.ScanDelegate != null)
                .OrderBy(t => t.ScanPriority)
                .Select(t => t.Type)
                .ToList();
        }

        /// <summary>
        /// 检查资源类型是否可扫描
        /// </summary>
        /// <param name="resourceType">资源类型</param>
        /// <returns>是否可扫描</returns>
        public bool IsResourceTypeScannable(ResourceItemType resourceType)
        {
            var typeDefinition = ResourceTypeRegistry.GetTypeDefinition(resourceType);
            return typeDefinition?.ScanDelegate != null;
        }
    }
}
