using System;
using System.Threading;

namespace Tunnel_Next.Services.ImageProcessing
{
    /// <summary>
    /// 对象池管理器 - 提供全局的对象池实例
    /// </summary>
    public static class ObjectPoolManager
    {
        private static readonly Lazy<ImageProcessingObjectPool> _instance = 
            new Lazy<ImageProcessingObjectPool>(() => new ImageProcessingObjectPool(), 
                LazyThreadSafetyMode.ExecutionAndPublication);

        private static volatile bool _isShuttingDown = false;

        /// <summary>
        /// 获取全局对象池实例
        /// </summary>
        public static ImageProcessingObjectPool Instance
        {
            get
            {
                if (_isShuttingDown)
                {
                    throw new ObjectDisposedException(nameof(ObjectPoolManager), "对象池管理器已关闭");
                }
                return _instance.Value;
            }
        }

        /// <summary>
        /// 检查对象池是否已初始化
        /// </summary>
        public static bool IsInitialized => _instance.IsValueCreated && !_isShuttingDown;

        /// <summary>
        /// 安全获取对象池实例（不会抛出异常）
        /// </summary>
        /// <returns>对象池实例，如果不可用则返回null</returns>
        public static ImageProcessingObjectPool? TryGetInstance()
        {
            try
            {
                return _isShuttingDown ? null : _instance.Value;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// 获取对象池统计信息
        /// </summary>
        /// <returns>统计信息，如果对象池不可用则返回null</returns>
        public static ImageProcessingObjectPool.ObjectPoolStats? GetStats()
        {
            try
            {
                return TryGetInstance()?.GetStats();
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// 手动触发维护
        /// </summary>
        public static void TriggerMaintenance()
        {
            try
            {
                TryGetInstance()?.TriggerMaintenance();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"触发维护失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 关闭对象池管理器
        /// </summary>
        public static void Shutdown()
        {
            _isShuttingDown = true;

            try
            {
                if (_instance.IsValueCreated)
                {
                    _instance.Value.Dispose();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"关闭对象池时发生异常: {ex.Message}");
            }
        }

        /// <summary>
        /// 重置对象池管理器（主要用于测试）
        /// </summary>
        internal static void Reset()
        {
            _isShuttingDown = false;
        }

        static ObjectPoolManager()
        {
            // 注册应用程序退出事件
            try
            {
                if (System.Windows.Application.Current != null)
                {
                    System.Windows.Application.Current.Exit += (sender, e) => Shutdown();
                }
            }
            catch
            {
                // 如果不在WPF应用程序上下文中，忽略异常
            }

            // 注册进程退出事件作为备用
            AppDomain.CurrentDomain.ProcessExit += (sender, e) => Shutdown();
        }
    }

    /// <summary>
    /// 对象池扩展方法
    /// </summary>
    public static class ObjectPoolExtensions
    {
        /// <summary>
        /// 安全获取Mat对象
        /// </summary>
        public static OpenCvSharp.Mat? TryGetMat(this ImageProcessingObjectPool? pool, 
            OpenCvSharp.Size size, OpenCvSharp.MatType matType)
        {
            try
            {
                return pool?.GetMat(size, matType);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// 安全返回Mat对象
        /// </summary>
        public static void TryReturnMat(this ImageProcessingObjectPool? pool, OpenCvSharp.Mat? mat)
        {
            try
            {
                if (mat != null)
                {
                    pool?.ReturnMat(mat);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"返回Mat到对象池失败: {ex.Message}");
            }
        }


    }
}
