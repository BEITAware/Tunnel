using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using OpenCvSharp;

namespace Tunnel_Next.Services.ImageProcessing
{
    /// <summary>
    /// 先进的资源管理对象池 - 支持生命周期管理、自动清理和性能优化
    /// </summary>
    public class ImageProcessingObjectPool : IDisposable
    {
        // 原有的池
        private readonly ConcurrentDictionary<MatKey, AdvancedResourcePool<Mat>> _matPools = new();
        private readonly ConcurrentQueue<Dictionary<string, object>> _metadataPool = new();
        private readonly ConcurrentQueue<List<Mat>> _matListPool = new();
        private readonly ConcurrentQueue<ConcurrentDictionary<int, Dictionary<string, object>>> _nodeOutputsPool = new();

        // 池大小配置
        private const int DefaultMatPoolSize = 10;
        private const int MaxMatPoolSize = 100;
        private const int DefaultMetadataPoolSize = 20;
        private const int MaxMetadataPoolSize = 200;

        // 生命周期管理配置
        private static readonly TimeSpan ResourceMaxIdleTime = TimeSpan.FromMinutes(5); // 资源最大空闲时间
        private static readonly TimeSpan MaintenanceInterval = TimeSpan.FromMinutes(2);  // 维护间隔

        // 统计信息
        private long _totalMatAllocations = 0;
        private long _totalMatReuses = 0;
        private long _totalMetadataAllocations = 0;
        private long _totalMetadataReuses = 0;

        // 维护任务
        private readonly Timer _maintenanceTimer;
        private readonly object _maintenanceLock = new object();
        private bool _disposed = false;

        public ImageProcessingObjectPool()
        {
            // 启动定期维护任务
            _maintenanceTimer = new Timer(PerformMaintenance, null, MaintenanceInterval, MaintenanceInterval);
        }

        #region Mat对象池管理

        /// <summary>
        /// 从对象池获取Mat对象 - 使用先进的生命周期管理
        /// </summary>
        public Mat GetMat(Size size, MatType matType)
        {
            var key = new MatKey(size, matType);

            var pool = _matPools.GetOrAdd(key, _ => new AdvancedResourcePool<Mat>(
                MaxMatPoolSize,
                mat => mat != null && !mat.IsDisposed && !mat.Empty(),
                mat => { try { mat?.Dispose(); } catch { } }
            ));

            if (pool.TryGet(out var mat))
            {
                Interlocked.Increment(ref _totalMatReuses);

                // 重置Mat内容但保持尺寸和类型
                try
                {
                    mat.SetTo(0);
                    return mat;
                }
                catch
                {
                    // 如果重置失败，创建新的Mat
                }
            }

            // 池中没有可用对象或重置失败，创建新的
            Interlocked.Increment(ref _totalMatAllocations);
            return new Mat(size, matType);
        }

        /// <summary>
        /// 从对象池获取Mat对象（基于现有Mat的尺寸和类型）
        /// </summary>
        public Mat GetMat(Mat template)
        {
            if (template == null || template.IsDisposed)
                throw new ArgumentException("Template Mat cannot be null or disposed");

            return GetMat(new Size(template.Width, template.Height), template.Type());
        }

        /// <summary>
        /// 将Mat对象返回到对象池 - 使用先进的生命周期管理
        /// </summary>
        public void ReturnMat(Mat mat)
        {
            if (mat == null || mat.IsDisposed)
                return;

            var key = new MatKey(new Size(mat.Width, mat.Height), mat.Type());
            var pool = _matPools.GetOrAdd(key, _ => new AdvancedResourcePool<Mat>(
                MaxMatPoolSize,
                m => m != null && !m.IsDisposed && !m.Empty(),
                m => { try { m?.Dispose(); } catch { } }
            ));

            pool.Return(mat);
        }

        /// <summary>
        /// 批量回收Mat对象
        /// </summary>
        public void ReturnMats(IEnumerable<Mat> mats)
        {
            if (mats == null) return;

            foreach (var mat in mats)
            {
                ReturnMat(mat);
            }
        }

        #endregion

        #region 元数据字典池管理

        /// <summary>
        /// 从对象池获取元数据字典
        /// </summary>
        public Dictionary<string, object> GetMetadataDictionary()
        {
            if (_metadataPool.TryDequeue(out var dict))
            {
                Interlocked.Increment(ref _totalMetadataReuses);
                dict.Clear();
                return dict;
            }
            
            Interlocked.Increment(ref _totalMetadataAllocations);
            return new Dictionary<string, object>();
        }

        /// <summary>
        /// 将元数据字典返回到对象池
        /// </summary>
        public void ReturnMetadataDictionary(Dictionary<string, object> dict)
        {
            if (dict == null) return;
            
            if (_metadataPool.Count < MaxMetadataPoolSize)
            {
                dict.Clear();
                _metadataPool.Enqueue(dict);
            }
            // 如果池已满，让GC回收
        }

        #endregion



        #region 其他对象池管理

        /// <summary>
        /// 从对象池获取Mat列表
        /// </summary>
        public List<Mat> GetMatList()
        {
            if (_matListPool.TryDequeue(out var list))
            {
                list.Clear();
                return list;
            }
            return new List<Mat>();
        }

        /// <summary>
        /// 将Mat列表返回到对象池
        /// </summary>
        public void ReturnMatList(List<Mat> list)
        {
            if (list == null) return;
            
            if (_matListPool.Count < DefaultMetadataPoolSize)
            {
                list.Clear();
                _matListPool.Enqueue(list);
            }
        }

        /// <summary>
        /// 从对象池获取节点输出字典
        /// </summary>
        public ConcurrentDictionary<int, Dictionary<string, object>> GetNodeOutputsDictionary()
        {
            if (_nodeOutputsPool.TryDequeue(out var dict))
            {
                dict.Clear();
                return dict;
            }
            return new ConcurrentDictionary<int, Dictionary<string, object>>();
        }

        /// <summary>
        /// 将节点输出字典返回到对象池
        /// </summary>
        public void ReturnNodeOutputsDictionary(ConcurrentDictionary<int, Dictionary<string, object>> dict)
        {
            if (dict == null) return;
            
            if (_nodeOutputsPool.Count < DefaultMetadataPoolSize)
            {
                dict.Clear();
                _nodeOutputsPool.Enqueue(dict);
            }
        }

        #endregion

        #region 统计和监控

        /// <summary>
        /// 获取池统计信息
        /// </summary>
        public ObjectPoolStats GetStats()
        {
            var totalMatPoolSize = _matPools.Values.Sum(pool => pool.Count);

            return new ObjectPoolStats
            {
                TotalMatAllocations = Interlocked.Read(ref _totalMatAllocations),
                TotalMatReuses = Interlocked.Read(ref _totalMatReuses),
                TotalMetadataAllocations = Interlocked.Read(ref _totalMetadataAllocations),
                TotalMetadataReuses = Interlocked.Read(ref _totalMetadataReuses),
                MatPoolSize = totalMatPoolSize,
                MetadataPoolSize = _metadataPool.Count,
                MatListPoolSize = _matListPool.Count,
                NodeOutputsPoolSize = _nodeOutputsPool.Count,
                MatPoolCount = _matPools.Count
            };
        }

        /// <summary>
        /// 定期维护 - 清理过期资源
        /// </summary>
        private void PerformMaintenance(object state)
        {
            if (_disposed) return;

            lock (_maintenanceLock)
            {
                try
                {
                    var totalCleaned = 0;

                    // 清理Mat池中的过期资源
                    var matPoolsToRemove = new List<MatKey>();
                    foreach (var kvp in _matPools.ToList())
                    {
                        var cleaned = kvp.Value.CleanExpired(ResourceMaxIdleTime);
                        totalCleaned += cleaned;

                        // 如果池为空，移除该池
                        if (kvp.Value.Count == 0)
                        {
                            matPoolsToRemove.Add(kvp.Key);
                        }
                    }

                    foreach (var key in matPoolsToRemove)
                    {
                        _matPools.TryRemove(key, out _);
                    }



#if DEBUG
                    if (totalCleaned > 0)
                    {
                        System.Diagnostics.Debug.WriteLine($"[ObjectPool] 维护完成，清理了 {totalCleaned} 个过期资源");
                    }
#endif
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[ObjectPool] 维护过程中发生异常: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// 手动触发维护
        /// </summary>
        public void TriggerMaintenance()
        {
            PerformMaintenance(null);
        }

        /// <summary>
        /// 清理所有池，释放资源
        /// </summary>
        public void ClearAllPools()
        {
            lock (_maintenanceLock)
            {
                // 清理Mat池
                foreach (var kvp in _matPools)
                {
                    kvp.Value.Clear();
                }
                _matPools.Clear();

                // 清理其他池
                while (_metadataPool.TryDequeue(out _)) { }
                while (_matListPool.TryDequeue(out _)) { }
                while (_nodeOutputsPool.TryDequeue(out _)) { }
            }
        }

        #endregion

        #region IDisposable实现

        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;

                // 停止维护定时器
                _maintenanceTimer?.Dispose();

                // 清理所有池
                ClearAllPools();

                GC.SuppressFinalize(this);
            }
        }

        #endregion

        #region 内部类

        /// <summary>
        /// 带生命周期管理的资源包装器
        /// </summary>
        private class ResourceWrapper<T> where T : class
        {
            public T Resource { get; }
            public DateTime LastUsed { get; set; }
            public DateTime CreatedAt { get; }
            public bool IsDisposed { get; set; }

            public ResourceWrapper(T resource)
            {
                Resource = resource;
                LastUsed = DateTime.UtcNow;
                CreatedAt = DateTime.UtcNow;
                IsDisposed = false;
            }

            public bool IsExpired(TimeSpan maxIdleTime)
            {
                return DateTime.UtcNow - LastUsed > maxIdleTime;
            }

            public void UpdateLastUsed()
            {
                LastUsed = DateTime.UtcNow;
            }
        }

        /// <summary>
        /// 先进的资源池，支持生命周期管理
        /// </summary>
        private class AdvancedResourcePool<T> where T : class
        {
            private readonly ConcurrentQueue<ResourceWrapper<T>> _pool = new();
            private readonly Func<T, bool> _isValidResource;
            private readonly Action<T> _disposeResource;
            private readonly int _maxSize;

            public AdvancedResourcePool(int maxSize, Func<T, bool> isValidResource, Action<T> disposeResource)
            {
                _maxSize = maxSize;
                _isValidResource = isValidResource;
                _disposeResource = disposeResource;
            }

            public bool TryGet(out T resource)
            {
                resource = null;

                while (_pool.TryDequeue(out var wrapper))
                {
                    if (!wrapper.IsDisposed && _isValidResource(wrapper.Resource))
                    {
                        wrapper.UpdateLastUsed();
                        resource = wrapper.Resource;
                        return true;
                    }
                    else
                    {
                        // 清理无效资源
                        try { _disposeResource(wrapper.Resource); } catch { }
                        wrapper.IsDisposed = true;
                    }
                }

                return false;
            }

            public void Return(T resource)
            {
                if (resource == null || !_isValidResource(resource))
                    return;

                if (_pool.Count < _maxSize)
                {
                    _pool.Enqueue(new ResourceWrapper<T>(resource));
                }
                else
                {
                    // 池已满，直接释放
                    try { _disposeResource(resource); } catch { }
                }
            }

            public int CleanExpired(TimeSpan maxIdleTime)
            {
                var cleanedCount = 0;
                var validWrappers = new List<ResourceWrapper<T>>();

                // 取出所有资源进行检查
                while (_pool.TryDequeue(out var wrapper))
                {
                    if (!wrapper.IsDisposed && _isValidResource(wrapper.Resource) && !wrapper.IsExpired(maxIdleTime))
                    {
                        validWrappers.Add(wrapper);
                    }
                    else
                    {
                        // 清理过期或无效资源
                        try { _disposeResource(wrapper.Resource); } catch { }
                        wrapper.IsDisposed = true;
                        cleanedCount++;
                    }
                }

                // 将有效资源放回池中
                foreach (var validWrapper in validWrappers)
                {
                    _pool.Enqueue(validWrapper);
                }

                return cleanedCount;
            }

            public void Clear()
            {
                while (_pool.TryDequeue(out var wrapper))
                {
                    try { _disposeResource(wrapper.Resource); } catch { }
                    wrapper.IsDisposed = true;
                }
            }

            public int Count => _pool.Count;
        }

        /// <summary>
        /// Mat对象的键，用于池分类
        /// </summary>
        private readonly struct MatKey : IEquatable<MatKey>
        {
            public readonly Size Size;
            public readonly MatType MatType;

            public MatKey(Size size, MatType matType)
            {
                Size = size;
                MatType = matType;
            }

            public bool Equals(MatKey other)
            {
                return Size.Equals(other.Size) && MatType.Equals(other.MatType);
            }

            public override bool Equals(object obj)
            {
                return obj is MatKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                return HashCode.Combine(Size, MatType);
            }
        }



        /// <summary>
        /// 对象池统计信息
        /// </summary>
        public class ObjectPoolStats
        {
            public long TotalMatAllocations { get; set; }
            public long TotalMatReuses { get; set; }
            public long TotalMetadataAllocations { get; set; }
            public long TotalMetadataReuses { get; set; }
            public int MatPoolSize { get; set; }
            public int MetadataPoolSize { get; set; }
            public int MatListPoolSize { get; set; }
            public int NodeOutputsPoolSize { get; set; }
            public int MatPoolCount { get; set; }

            public double MatReuseRate => TotalMatAllocations > 0 ? (double)TotalMatReuses / (TotalMatAllocations + TotalMatReuses) : 0;
            public double MetadataReuseRate => TotalMetadataAllocations > 0 ? (double)TotalMetadataReuses / (TotalMetadataAllocations + TotalMetadataReuses) : 0;

            public long TotalResourcesInPools => MatPoolSize + MetadataPoolSize + MatListPoolSize + NodeOutputsPoolSize;
            public int TotalPoolTypes => MatPoolCount + (MetadataPoolSize > 0 ? 1 : 0) + (MatListPoolSize > 0 ? 1 : 0) + (NodeOutputsPoolSize > 0 ? 1 : 0);

            public override string ToString()
            {
                return $"ObjectPool Stats: Mat({MatPoolSize} pooled, {MatReuseRate:P1} reuse), " +
                       $"Metadata({MetadataPoolSize} pooled, {MetadataReuseRate:P1} reuse), " +
                       $"Total Pools: {TotalPoolTypes}, Total Resources: {TotalResourcesInPools}";
            }
        }
        #endregion
    }
} 