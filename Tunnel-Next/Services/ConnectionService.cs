using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
using Tunnel_Next.Models;
using Tunnel_Next.Utils;

namespace Tunnel_Next.Services
{
    /// <summary>
    /// 连接服务 - 负责异步处理连接操作，彻底分离UI和业务逻辑
    /// </summary>
    public class ConnectionService
    {
        private readonly ConnectionManager _connectionManager;
        private readonly DispatcherTimer _batchUpdateTimer;
        private readonly Queue<ConnectionOperation> _pendingOperations = new();
        private readonly object _operationLock = new object();
        private bool _isBatchProcessing = false;

        // 事件
        public event Action<NodeConnection>? ConnectionCreated;
        public event Action<NodeConnection>? ConnectionRemoved;
        public event Action<string>? ConnectionError;
        public event Action? BatchUpdateCompleted;

        public ConnectionService(ConnectionManager connectionManager)
        {
            _connectionManager = connectionManager ?? throw new ArgumentNullException(nameof(connectionManager));
            
            // 初始化批量更新定时器
            _batchUpdateTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(50) // 50ms批量处理
            };
            _batchUpdateTimer.Tick += OnBatchUpdateTimer;
        }

        /// <summary>
        /// 异步创建连接
        /// </summary>
        public async Task<bool> CreateConnectionAsync(Node outputNode, string outputPortName, Node inputNode, string inputPortName)
        {
            using (PerformanceMonitor.CreateTimer("ConnectionService.CreateConnection"))
            {
                try
                {
                    // 快速验证（在调用线程进行）
                    if (outputNode == null || inputNode == null || 
                        string.IsNullOrEmpty(outputPortName) || string.IsNullOrEmpty(inputPortName))
                    {
                        ConnectionError?.Invoke("连接参数无效");
                        return false;
                    }

                    // 将操作加入队列进行批量处理
                    var operation = new ConnectionOperation
                    {
                        Type = ConnectionOperationType.Create,
                        OutputNode = outputNode,
                        OutputPortName = outputPortName,
                        InputNode = inputNode,
                        InputPortName = inputPortName,
                        CompletionSource = new TaskCompletionSource<bool>()
                    };

                    lock (_operationLock)
                    {
                        _pendingOperations.Enqueue(operation);
                        if (!_batchUpdateTimer.IsEnabled)
                        {
                            _batchUpdateTimer.Start();
                        }
                    }

                    return await operation.CompletionSource.Task;
                }
                catch (Exception ex)
                {
                    ConnectionError?.Invoke($"创建连接异常: {ex.Message}");
                    return false;
                }
            }
        }

        /// <summary>
        /// 异步移除连接
        /// </summary>
        public async Task<bool> RemoveConnectionAsync(NodeConnection connection)
        {
            using (PerformanceMonitor.CreateTimer("ConnectionService.RemoveConnection"))
            {
                try
                {
                    if (connection == null)
                    {
                        ConnectionError?.Invoke("连接对象为空");
                        return false;
                    }

                    var operation = new ConnectionOperation
                    {
                        Type = ConnectionOperationType.Remove,
                        Connection = connection,
                        CompletionSource = new TaskCompletionSource<bool>()
                    };

                    lock (_operationLock)
                    {
                        _pendingOperations.Enqueue(operation);
                        if (!_batchUpdateTimer.IsEnabled)
                        {
                            _batchUpdateTimer.Start();
                        }
                    }

                    return await operation.CompletionSource.Task;
                }
                catch (Exception ex)
                {
                    ConnectionError?.Invoke($"移除连接异常: {ex.Message}");
                    return false;
                }
            }
        }

        /// <summary>
        /// 批量更新定时器事件
        /// </summary>
        private async void OnBatchUpdateTimer(object? sender, EventArgs e)
        {
            if (_isBatchProcessing)
                return;

            _isBatchProcessing = true;
            _batchUpdateTimer.Stop();

            try
            {
                await ProcessPendingOperations();
            }
            finally
            {
                _isBatchProcessing = false;
                
                // 检查是否还有待处理的操作
                lock (_operationLock)
                {
                    if (_pendingOperations.Count > 0)
                    {
                        _batchUpdateTimer.Start();
                    }
                }
            }
        }

        /// <summary>
        /// 处理待处理的操作
        /// </summary>
        private async Task ProcessPendingOperations()
        {
            var operations = new List<ConnectionOperation>();
            
            lock (_operationLock)
            {
                while (_pendingOperations.Count > 0)
                {
                    operations.Add(_pendingOperations.Dequeue());
                }
            }

            if (operations.Count == 0)
                return;

            // 在后台线程处理操作
            await Task.Run(() =>
            {
                foreach (var operation in operations)
                {
                    try
                    {
                        bool success = false;
                        
                        switch (operation.Type)
                        {
                            case ConnectionOperationType.Create:
                                success = ProcessCreateConnection(operation);
                                break;
                                
                            case ConnectionOperationType.Remove:
                                success = ProcessRemoveConnection(operation);
                                break;
                        }
                        
                        operation.CompletionSource.SetResult(success);
                    }
                    catch (Exception ex)
                    {
                        operation.CompletionSource.SetException(ex);
                    }
                }
            });

            // 通知批量更新完成
            BatchUpdateCompleted?.Invoke();
        }

        /// <summary>
        /// 处理创建连接操作
        /// </summary>
        private bool ProcessCreateConnection(ConnectionOperation operation)
        {
            var connection = _connectionManager.CreateConnection(
                operation.OutputNode!,
                operation.OutputPortName!,
                operation.InputNode!,
                operation.InputPortName!);

            if (connection != null)
            {
                // 在UI线程触发事件
                Dispatcher.CurrentDispatcher.BeginInvoke(() =>
                {
                    ConnectionCreated?.Invoke(connection);
                });
                return true;
            }

            return false;
        }

        /// <summary>
        /// 处理移除连接操作
        /// </summary>
        private bool ProcessRemoveConnection(ConnectionOperation operation)
        {
            var success = _connectionManager.RemoveConnection(operation.Connection!);
            
            if (success)
            {
                // 在UI线程触发事件
                Dispatcher.CurrentDispatcher.BeginInvoke(() =>
                {
                    ConnectionRemoved?.Invoke(operation.Connection!);
                });
            }

            return success;
        }

        /// <summary>
        /// 获取待处理操作数量
        /// </summary>
        public int GetPendingOperationCount()
        {
            lock (_operationLock)
            {
                return _pendingOperations.Count;
            }
        }

        /// <summary>
        /// 清除所有待处理操作
        /// </summary>
        public void ClearPendingOperations()
        {
            lock (_operationLock)
            {
                while (_pendingOperations.Count > 0)
                {
                    var operation = _pendingOperations.Dequeue();
                    operation.CompletionSource.SetCanceled();
                }
            }
            
            _batchUpdateTimer.Stop();
        }
    }

    /// <summary>
    /// 连接操作类型
    /// </summary>
    public enum ConnectionOperationType
    {
        Create,
        Remove
    }

    /// <summary>
    /// 连接操作
    /// </summary>
    public class ConnectionOperation
    {
        public ConnectionOperationType Type { get; set; }
        public Node? OutputNode { get; set; }
        public string? OutputPortName { get; set; }
        public Node? InputNode { get; set; }
        public string? InputPortName { get; set; }
        public NodeConnection? Connection { get; set; }
        public TaskCompletionSource<bool> CompletionSource { get; set; } = new();
    }
}
