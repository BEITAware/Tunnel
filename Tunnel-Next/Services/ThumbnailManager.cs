using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace Tunnel_Next.Services
{
    /// <summary>
    /// 缩略图管理器 - 负责缩略图的生命周期管理和文件句柄释放
    /// </summary>
    public class ThumbnailManager : IDisposable
    {
        private readonly ThumbnailService _thumbnailService;
        private readonly ConcurrentDictionary<string, WeakReference<BitmapSource>> _thumbnailCache;
        private bool _disposed = false;

        /// <summary>
        /// 缩略图更新事件
        /// </summary>
        public event Action<string, BitmapSource?>? ThumbnailUpdated;

        public ThumbnailManager(ThumbnailService thumbnailService)
        {
            _thumbnailService = thumbnailService ?? throw new ArgumentNullException(nameof(thumbnailService));
            _thumbnailCache = new ConcurrentDictionary<string, WeakReference<BitmapSource>>();
        }

        /// <summary>
        /// 获取缩略图，优先从缓存获取
        /// </summary>
        /// <param name="thumbnailPath">缩略图路径</param>
        /// <returns>缩略图BitmapSource</returns>
        public BitmapSource? GetThumbnail(string thumbnailPath)
        {
            if (string.IsNullOrEmpty(thumbnailPath) || _disposed)
                return null;

            // 尝试从缓存获取
            if (_thumbnailCache.TryGetValue(thumbnailPath, out var weakRef) && 
                weakRef.TryGetTarget(out var cachedThumbnail))
            {
                return cachedThumbnail;
            }

            // 从文件加载
            var thumbnail = _thumbnailService.LoadThumbnail(thumbnailPath);
            if (thumbnail != null)
            {
                // 添加到缓存
                _thumbnailCache[thumbnailPath] = new WeakReference<BitmapSource>(thumbnail);
            }

            return thumbnail;
        }

        /// <summary>
        /// 从预览控件异步生成并缓存缩略图
        /// </summary>
        /// <param name="nodeGraphPath">节点图路径</param>
        /// <param name="previewControl">预览控件</param>
        /// <returns>生成的缩略图路径</returns>
        public async Task<string?> GenerateAndCacheThumbnailFromPreviewAsync(string nodeGraphPath, Controls.ImagePreviewControl? previewControl = null)
        {
            if (string.IsNullOrEmpty(nodeGraphPath) || _disposed)
                return null;

            try
            {
                var thumbnailPath = await _thumbnailService.GenerateNodeGraphThumbnailFromPreviewAsync(
                    nodeGraphPath,
                    previewControl,
                    ForceClearThumbnailCache);

                if (!string.IsNullOrEmpty(thumbnailPath))
                {
                    // 加载新缩略图到缓存
                    var thumbnail = _thumbnailService.LoadThumbnail(thumbnailPath);
                    if (thumbnail != null)
                    {
                        _thumbnailCache[thumbnailPath] = new WeakReference<BitmapSource>(thumbnail);

                        // 通知缩略图更新
                        ThumbnailUpdated?.Invoke(thumbnailPath, thumbnail);
                    }
                }
                return thumbnailPath;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ThumbnailManager] 生成缩略图失败: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 异步生成并缓存缩略图
        /// </summary>
        /// <param name="nodeGraphPath">节点图路径</param>
        /// <param name="previewImage">预览图像</param>
        /// <returns>生成的缩略图路径</returns>
        public async Task<string?> GenerateAndCacheThumbnailAsync(string nodeGraphPath, OpenCvSharp.Mat? previewImage = null)
        {
            if (string.IsNullOrEmpty(nodeGraphPath) || _disposed)
                return null;

            try
            {
                var thumbnailPath = await _thumbnailService.GenerateNodeGraphThumbnailAsync(
                    nodeGraphPath,
                    previewImage,
                    ForceClearThumbnailCache);

                if (!string.IsNullOrEmpty(thumbnailPath))
                {
                    // 加载新缩略图到缓存
                    var thumbnail = _thumbnailService.LoadThumbnail(thumbnailPath);
                    if (thumbnail != null)
                    {
                        _thumbnailCache[thumbnailPath] = new WeakReference<BitmapSource>(thumbnail);

                        // 通知缩略图更新
                        ThumbnailUpdated?.Invoke(thumbnailPath, thumbnail);
                    }
                }
                return thumbnailPath;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ThumbnailManager] 生成缩略图失败: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 使缩略图缓存失效
        /// </summary>
        /// <param name="thumbnailPath">缩略图路径</param>
        public void InvalidateThumbnail(string thumbnailPath)
        {
            if (string.IsNullOrEmpty(thumbnailPath))
                return;

            _thumbnailCache.TryRemove(thumbnailPath, out _);
        }

        /// <summary>
        /// 清除缩略图缓存
        /// </summary>
        /// <param name="thumbnailPath">缩略图路径</param>
        private void ForceClearThumbnailCache(string thumbnailPath)
        {
            if (string.IsNullOrEmpty(thumbnailPath))
                return;

            // 清除指定缩略图的缓存
            InvalidateThumbnail(thumbnailPath);
            
            // 执行一次简单的垃圾回收
            GC.Collect(0);
        }

        /// <summary>
        /// 删除缩略图文件并清除缓存
        /// </summary>
        /// <param name="nodeGraphPath">节点图路径</param>
        public void DeleteThumbnail(string nodeGraphPath)
        {
            if (string.IsNullOrEmpty(nodeGraphPath))
                return;

            var thumbnailPath = _thumbnailService.GetNodeGraphThumbnailPath(nodeGraphPath);
            
            // 清除缓存
            InvalidateThumbnail(thumbnailPath);
            
            // 删除文件
            _thumbnailService.DeleteThumbnail(nodeGraphPath);
            
            // 通知缩略图删除
            ThumbnailUpdated?.Invoke(thumbnailPath, null);
        }

        /// <summary>
        /// 清理过期的缓存项
        /// </summary>
        public void CleanupCache()
        {
            var keysToRemove = new List<string>();
            
            foreach (var kvp in _thumbnailCache)
            {
                if (!kvp.Value.TryGetTarget(out _))
                {
                    keysToRemove.Add(kvp.Key);
                }
            }
            
            foreach (var key in keysToRemove)
            {
                _thumbnailCache.TryRemove(key, out _);
            }
            
            // 执行一次垃圾回收，释放未使用的资源
            GC.Collect(0, GCCollectionMode.Optimized);
        }
        
        /// <summary>
        /// 执行完整的缩略图清理和整理，包括文件系统和内存缓存
        /// </summary>
        public void PerformFullCleanup()
        {
            try
            {
                // 清空内存缓存
                _thumbnailCache.Clear();
                
                // 调用整理服务清理文件系统
                _thumbnailService.OrganizeThumbnails();
                
                // 通知完成
                ThumbnailUpdated?.Invoke("", null);
                
                // 强制垃圾回收
                GC.Collect();
                GC.WaitForPendingFinalizers();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ThumbnailManager] 执行完整清理失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 获取节点图缩略图路径
        /// </summary>
        /// <param name="nodeGraphPath">节点图路径</param>
        /// <returns>缩略图路径</returns>
        public string GetNodeGraphThumbnailPath(string nodeGraphPath)
        {
            return _thumbnailService.GetNodeGraphThumbnailPath(nodeGraphPath);
        }

        /// <summary>
        /// 检查缩略图是否存在
        /// </summary>
        /// <param name="nodeGraphPath">节点图路径</param>
        /// <returns>缩略图是否存在</returns>
        public bool ThumbnailExists(string nodeGraphPath)
        {
            return _thumbnailService.ThumbnailExists(nodeGraphPath);
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// 释放资源的具体实现
        /// </summary>
        /// <param name="disposing">是否正在释放托管资源</param>
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed && disposing)
            {
                // 清除所有缓存
                _thumbnailCache.Clear();
                _disposed = true;
            }
        }

        /// <summary>
        /// 析构函数
        /// </summary>
        ~ThumbnailManager()
        {
            Dispose(false);
        }
    }
}
