using System;
using System.Collections.Generic;
using System.Threading;
using System.Windows;
using System.Windows.Threading;
using Tunnel_Next.Models;
using Tunnel_Next.Utils;

namespace Tunnel_Next.Services.UI
{
    /// <summary>
    /// UI状态管理器 - 负责管理UI状态变化，避免频繁的UI更新
    /// </summary>
    public class UIStateManager
    {
        private readonly DispatcherTimer _updateTimer;
        private readonly Dictionary<string, object> _pendingUpdates = new();
        private readonly object _updateLock = new object();
        private readonly SynchronizationContext _uiContext;

        // UI状态
        private bool _hasConnectionPreview = false;
        private Point _connectionPreviewEnd = new Point();
        private Node? _selectedNode = null;
        private HashSet<Node> _highlightedNodes = new();

        // 事件
        public event Action<Node?>? SelectedNodeChanged;
        public event Action<IEnumerable<Node>>? HighlightedNodesChanged;
        public event Action<bool, Point>? ConnectionPreviewChanged;
        public event Action? UIUpdateRequested;

        public UIStateManager()
        {
            _uiContext = SynchronizationContext.Current ?? throw new InvalidOperationException("必须在UI线程创建UIStateManager");
            
            _updateTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(16) // ~60 FPS
            };
            _updateTimer.Tick += OnUpdateTimer;
        }

        #region 公共属性

        public Node? SelectedNode
        {
            get => _selectedNode;
            private set
            {
                if (_selectedNode != value)
                {
                    _selectedNode = value;
                    SelectedNodeChanged?.Invoke(value);
                }
            }
        }

        public bool HasConnectionPreview => _hasConnectionPreview;

        public Point ConnectionPreviewEnd => _connectionPreviewEnd;

        public IEnumerable<Node> HighlightedNodes => _highlightedNodes;

        #endregion

        #region 状态更新方法

        /// <summary>
        /// 请求选择节点
        /// </summary>
        public void RequestSelectNode(Node? node)
        {
            RequestUpdate("SelectedNode", node);
        }

        /// <summary>
        /// 请求高亮节点
        /// </summary>
        public void RequestHighlightNode(Node node, bool highlight = true)
        {
            RequestUpdate($"Highlight_{node.Id}", new HighlightRequest { Node = node, Highlight = highlight });
        }

        /// <summary>
        /// 请求清除所有高亮
        /// </summary>
        public void RequestClearAllHighlights()
        {
            RequestUpdate("ClearHighlights", true);
        }

        /// <summary>
        /// 请求更新连接预览
        /// </summary>
        public void RequestUpdateConnectionPreview(bool hasPreview, Point endPoint = default)
        {
            RequestUpdate("ConnectionPreview", new ConnectionPreviewRequest 
            { 
                HasPreview = hasPreview, 
                EndPoint = endPoint 
            });
        }

        /// <summary>
        /// 请求UI更新
        /// </summary>
        private void RequestUpdate(string key, object value)
        {
            lock (_updateLock)
            {
                _pendingUpdates[key] = value;
                
                if (!_updateTimer.IsEnabled)
                {
                    _updateTimer.Start();
                }
            }
        }

        #endregion

        #region 批量更新处理

        /// <summary>
        /// 更新定时器事件
        /// </summary>
        private void OnUpdateTimer(object? sender, EventArgs e)
        {
            _updateTimer.Stop();
            
            Dictionary<string, object> updates;
            lock (_updateLock)
            {
                if (_pendingUpdates.Count == 0)
                    return;
                    
                updates = new Dictionary<string, object>(_pendingUpdates);
                _pendingUpdates.Clear();
            }

            ProcessUpdates(updates);
        }

        /// <summary>
        /// 处理批量更新
        /// </summary>
        private void ProcessUpdates(Dictionary<string, object> updates)
        {
            using (PerformanceMonitor.CreateTimer("UIStateManager.ProcessUpdates"))
            {
                bool hasChanges = false;

                foreach (var kvp in updates)
                {
                    switch (kvp.Key)
                    {
                        case "SelectedNode":
                            if (ProcessSelectedNodeUpdate(kvp.Value as Node))
                                hasChanges = true;
                            break;

                        case "ClearHighlights":
                            if (ProcessClearHighlights())
                                hasChanges = true;
                            break;

                        case "ConnectionPreview":
                            if (ProcessConnectionPreviewUpdate(kvp.Value as ConnectionPreviewRequest))
                                hasChanges = true;
                            break;

                        default:
                            if (kvp.Key.StartsWith("Highlight_"))
                            {
                                if (ProcessHighlightUpdate(kvp.Value as HighlightRequest))
                                    hasChanges = true;
                            }
                            break;
                    }
                }

                if (hasChanges)
                {
                    UIUpdateRequested?.Invoke();
                }
            }
        }

        /// <summary>
        /// 处理选中节点更新
        /// </summary>
        private bool ProcessSelectedNodeUpdate(Node? node)
        {
            if (_selectedNode != node)
            {
                SelectedNode = node;
                return true;
            }
            return false;
        }

        /// <summary>
        /// 处理高亮更新
        /// </summary>
        private bool ProcessHighlightUpdate(HighlightRequest? request)
        {
            if (request?.Node == null)
                return false;

            bool changed = false;
            
            if (request.Highlight)
            {
                if (_highlightedNodes.Add(request.Node))
                    changed = true;
            }
            else
            {
                if (_highlightedNodes.Remove(request.Node))
                    changed = true;
            }

            if (changed)
            {
                HighlightedNodesChanged?.Invoke(_highlightedNodes);
            }

            return changed;
        }

        /// <summary>
        /// 处理清除高亮
        /// </summary>
        private bool ProcessClearHighlights()
        {
            if (_highlightedNodes.Count > 0)
            {
                _highlightedNodes.Clear();
                HighlightedNodesChanged?.Invoke(_highlightedNodes);
                return true;
            }
            return false;
        }

        /// <summary>
        /// 处理连接预览更新
        /// </summary>
        private bool ProcessConnectionPreviewUpdate(ConnectionPreviewRequest? request)
        {
            if (request == null)
                return false;

            bool changed = false;
            
            if (_hasConnectionPreview != request.HasPreview)
            {
                _hasConnectionPreview = request.HasPreview;
                changed = true;
            }

            if (_connectionPreviewEnd != request.EndPoint)
            {
                _connectionPreviewEnd = request.EndPoint;
                changed = true;
            }

            if (changed)
            {
                ConnectionPreviewChanged?.Invoke(_hasConnectionPreview, _connectionPreviewEnd);
            }

            return changed;
        }

        #endregion

        #region 辅助类

        private class HighlightRequest
        {
            public Node? Node { get; set; }
            public bool Highlight { get; set; }
        }

        private class ConnectionPreviewRequest
        {
            public bool HasPreview { get; set; }
            public Point EndPoint { get; set; }
        }

        #endregion

        #region 资源清理

        public void Dispose()
        {
            _updateTimer?.Stop();
            
            lock (_updateLock)
            {
                _pendingUpdates.Clear();
            }
            
            _highlightedNodes.Clear();
        }

        #endregion
    }
}
