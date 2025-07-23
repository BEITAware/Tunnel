using System;
using System.Threading;
using System.Threading.Tasks;
using Tunnel_Next.Models;

namespace Tunnel_Next.Services.ImageProcessing
{
    /// <summary>
    /// 图像处理服务接口 - MVVM解耦的核心接口
    /// </summary>
    public interface IImageProcessingService : IDisposable
    {
        event Action<bool>? ProcessingStateChanged;
        event Action<ProcessingResult>? ProcessingCompleted;
        event Action<string>? StatusChanged;

        Task<ProcessingResult> ProcessNodeGraphAsync(NodeGraph nodeGraph, ProcessorEnvironment environment, CancellationToken cancellationToken = default);
        Task<ProcessingResult> ProcessChangedNodesAsync(NodeGraph nodeGraph, Node[] changedNodes, ProcessorEnvironment environment, CancellationToken cancellationToken = default);
        void CancelProcessing();
        bool IsProcessing { get; }
    }

    /// <summary>
    /// 处理结果
    /// </summary>
    public class ProcessingResult
    {
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
        public TimeSpan Duration { get; set; }
        public int ProcessedNodeCount { get; set; }
        public NodeGraph? ProcessedNodeGraph { get; set; }
    }

    /// <summary>
    /// 处理协调器 - 解耦ViewModel和ImageProcessor的MVVM实现
    /// </summary>
    public class ProcessingCoordinator : IImageProcessingService, IDisposable
    {
        private readonly ImageProcessor _imageProcessor;
        private readonly SynchronizationContext _uiContext;
        private readonly CancellationTokenSource _cancellationTokenSource;
        private volatile bool _disposed = false;
        private volatile bool _isProcessing = false;

        // 事件
        public event Action<bool>? ProcessingStateChanged;
        public event Action<ProcessingResult>? ProcessingCompleted;
        public event Action<string>? StatusChanged;

        public bool IsProcessing => _isProcessing;

        public ProcessingCoordinator(ImageProcessor imageProcessor)
        {
            _imageProcessor = imageProcessor ?? throw new ArgumentNullException(nameof(imageProcessor));
            _uiContext = SynchronizationContext.Current ?? new SynchronizationContext();
            _cancellationTokenSource = new CancellationTokenSource();

            // 绑定ImageProcessor事件，但不直接调用UI
            _imageProcessor.OnNodeGraphProcessed += OnNodeGraphProcessed;

        }

        /// <summary>
        /// 处理节点图（MVVM模式，完全异步）
        /// </summary>
        public async Task<ProcessingResult> ProcessNodeGraphAsync(NodeGraph nodeGraph, ProcessorEnvironment environment, CancellationToken cancellationToken = default)
        {
            if (_disposed)
                return new ProcessingResult { Success = false, ErrorMessage = "服务已释放" };

            if (_isProcessing)
                return new ProcessingResult { Success = false, ErrorMessage = "正在处理中，请稍后重试" };

            var startTime = DateTime.Now;
            var result = new ProcessingResult();

            try
            {
                _isProcessing = true;

                // 通知UI开始处理（异步，不阻塞）
                NotifyUIAsync(() => ProcessingStateChanged?.Invoke(true));
                NotifyUIAsync(() => StatusChanged?.Invoke("开始处理节点图"));

                // 在后台线程执行图像处理，完全不涉及UI
                var success = await _imageProcessor.ProcessNodeGraphAsync(nodeGraph, environment);

                result.Success = success;
                result.ProcessedNodeCount = nodeGraph.Nodes.Count;
                result.ProcessedNodeGraph = nodeGraph;
                result.Duration = DateTime.Now - startTime;

                // 通知UI处理完成
                NotifyUIAsync(() => StatusChanged?.Invoke(success ? "处理完成" : "处理失败"));

                return result;
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.ErrorMessage = ex.Message;
                result.Duration = DateTime.Now - startTime;

                NotifyUIAsync(() => StatusChanged?.Invoke($"处理错误: {ex.Message}"));

                return result;
            }
            finally
            {
                _isProcessing = false;
                NotifyUIAsync(() => ProcessingStateChanged?.Invoke(false));
                NotifyUIAsync(() => ProcessingCompleted?.Invoke(result));
            }
        }

        /// <summary>
        /// 处理变化的节点（增量处理）
        /// </summary>
        public async Task<ProcessingResult> ProcessChangedNodesAsync(NodeGraph nodeGraph, Node[] changedNodes, ProcessorEnvironment environment, CancellationToken cancellationToken = default)
        {
            if (_disposed)
                return new ProcessingResult { Success = false, ErrorMessage = "服务已释放" };

            if (_isProcessing)
                return new ProcessingResult { Success = false, ErrorMessage = "正在处理中，请稍后重试" };

            var startTime = DateTime.Now;
            var result = new ProcessingResult();

            try
            {
                _isProcessing = true;

                NotifyUIAsync(() => ProcessingStateChanged?.Invoke(true));
                NotifyUIAsync(() => StatusChanged?.Invoke($"增量处理 {changedNodes.Length} 个节点"));

                var success = await _imageProcessor.ProcessChangedNodesAsync(nodeGraph, changedNodes, environment);

                result.Success = success;
                result.ProcessedNodeCount = changedNodes.Length;
                result.ProcessedNodeGraph = nodeGraph;
                result.Duration = DateTime.Now - startTime;

                NotifyUIAsync(() => StatusChanged?.Invoke(success ? "增量处理完成" : "增量处理失败"));

                return result;
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.ErrorMessage = ex.Message;
                result.Duration = DateTime.Now - startTime;

                NotifyUIAsync(() => StatusChanged?.Invoke($"增量处理错误: {ex.Message}"));

                return result;
            }
            finally
            {
                _isProcessing = false;
                NotifyUIAsync(() => ProcessingStateChanged?.Invoke(false));
                NotifyUIAsync(() => ProcessingCompleted?.Invoke(result));
            }
        }

        /// <summary>
        /// 取消当前处理
        /// </summary>
        public void CancelProcessing()
        {
            if (_isProcessing)
            {
                _cancellationTokenSource.Cancel();
                NotifyUIAsync(() => StatusChanged?.Invoke("处理已取消"));
            }
        }

        /// <summary>
        /// 异步通知UI（不阻塞当前线程）
        /// </summary>
        private void NotifyUIAsync(Action action)
        {
            if (_disposed) return;

            _uiContext.Post(_ =>
            {
                try
                {
                    action();
                }
                catch (Exception ex)
                {
                }
            }, null);
        }

        /// <summary>
        /// ImageProcessor处理完成回调
        /// </summary>
        private void OnNodeGraphProcessed(NodeGraph nodeGraph)
        {
            // 不直接触发UI更新，而是通过ProcessingCompleted事件
        }

        public void Dispose()
        {
            if (_disposed) return;

            _disposed = true;

            _cancellationTokenSource?.Cancel();
            _cancellationTokenSource?.Dispose();

            GC.SuppressFinalize(this);
        }
    }

    /// <summary>
    /// 处理请求类型
    /// </summary>
    public enum ProcessingRequestType
    {
        ParameterChange,    // 参数变化
        NodeAdded,         // 节点添加
        NodeDeleted,       // 节点删除
        ConnectionChanged, // 连接变化
        DocumentSwitch,    // 文档切换
        ManualRefresh      // 手动刷新
    }
}
