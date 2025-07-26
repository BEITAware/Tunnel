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

                // 创建所有类型的扫描任务
                var scanTasks = new List<Task<(List<ResourceObject> Resources, int Count, string TypeName, long ElapsedMs)>>();
                
                foreach (var typeDefinition in typeDefinitions)
                {
                    // 为每个类型创建一个扫描任务
                    var scanTask = Task.Run(async () => 
                    {
                        if (cancellationToken.IsCancellationRequested)
                        {
                            System.Diagnostics.Debug.WriteLine($"[UnifiedResourceScanner] {typeDefinition.DisplayName} 扫描被取消");
                            return (new List<ResourceObject>(), 0, typeDefinition.DisplayName, 0L);
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
                            
                            typeStopwatch.Stop();
                            
                            System.Diagnostics.Debug.WriteLine($"[UnifiedResourceScanner] {typeDefinition.DisplayName} 扫描完成: {resources.Count} 个资源, 耗时: {typeStopwatch.ElapsedMilliseconds}ms");
                            Console.WriteLine($"[UnifiedResourceScanner] {typeDefinition.DisplayName} 扫描完成: {resources.Count} 个资源, 耗时: {typeStopwatch.ElapsedMilliseconds}ms");
                            
                            return (resources, resources.Count, typeDefinition.DisplayName, typeStopwatch.ElapsedMilliseconds);
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"[UnifiedResourceScanner] 扫描 {typeDefinition.DisplayName} 失败: {ex.Message}");
                            Console.WriteLine($"[UnifiedResourceScanner] 扫描 {typeDefinition.DisplayName} 失败: {ex.Message}");
                            return (new List<ResourceObject>(), 0, typeDefinition.DisplayName, 0L);
                        }
                    }, cancellationToken);
                    
                    scanTasks.Add(scanTask);
                }
                
                // 等待所有扫描任务完成
                while (scanTasks.Count > 0)
                {
                    // 使用Task.WhenAny来处理已完成的任务，而不是一次性等待所有任务
                    var completedTask = await Task.WhenAny(scanTasks);
                    scanTasks.Remove(completedTask);
                    
                    var result = await completedTask;
                    
                    // 合并结果
                    allResources.AddRange(result.Resources);
                    scannedFileCount += result.Count;
                    
                    if (result.Count > 0)
                    {
                        System.Diagnostics.Debug.WriteLine($"[UnifiedResourceScanner] 合并 {result.TypeName} 扫描结果: {result.Count} 个资源");
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
