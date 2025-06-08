using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using Tunnel_Next.Models;

namespace Tunnel_Next.Utils
{
    /// <summary>
    /// 视口优化器 - 用于节点图编辑器的视口裁剪和性能优化
    /// </summary>
    public class ViewportOptimizer
    {
        private Rect _currentViewport = Rect.Empty;
        private readonly Dictionary<Node, bool> _nodeVisibilityCache = new();
        private readonly Dictionary<NodeConnection, bool> _connectionVisibilityCache = new();
        private bool _cacheValid = false;

        /// <summary>
        /// 当前视口
        /// </summary>
        public Rect CurrentViewport
        {
            get => _currentViewport;
            set
            {
                if (_currentViewport != value)
                {
                    _currentViewport = value;
                    InvalidateCache();
                }
            }
        }

        /// <summary>
        /// 视口边距（用于预加载即将进入视口的对象）
        /// </summary>
        public double ViewportMargin { get; set; } = 100;

        /// <summary>
        /// 无效化缓存
        /// </summary>
        public void InvalidateCache()
        {
            _cacheValid = false;
            _nodeVisibilityCache.Clear();
            _connectionVisibilityCache.Clear();
        }

        /// <summary>
        /// 检查节点是否在视口内
        /// </summary>
        public bool IsNodeInViewport(Node node)
        {
            if (_currentViewport.IsEmpty)
                return true;

            if (_nodeVisibilityCache.TryGetValue(node, out var cached))
                return cached;

            var nodeRect = new Rect(node.X, node.Y, node.Width, node.Height);
            var expandedViewport = _currentViewport;
            expandedViewport.Inflate(ViewportMargin, ViewportMargin);
            
            var isVisible = expandedViewport.IntersectsWith(nodeRect);
            _nodeVisibilityCache[node] = isVisible;
            
            return isVisible;
        }

        /// <summary>
        /// 检查连接线是否在视口内
        /// </summary>
        public bool IsConnectionInViewport(NodeConnection connection)
        {
            if (_currentViewport.IsEmpty)
                return true;

            if (_connectionVisibilityCache.TryGetValue(connection, out var cached))
                return cached;

            // 如果连接的任一节点在视口内，则认为连接线可能在视口内
            var isVisible = IsNodeInViewport(connection.OutputNode) || IsNodeInViewport(connection.InputNode);
            _connectionVisibilityCache[connection] = isVisible;
            
            return isVisible;
        }

        /// <summary>
        /// 获取视口内的节点列表
        /// </summary>
        public IEnumerable<Node> GetNodesInViewport(IEnumerable<Node> allNodes)
        {
            return allNodes.Where(IsNodeInViewport);
        }

        /// <summary>
        /// 获取视口内的连接线列表
        /// </summary>
        public IEnumerable<NodeConnection> GetConnectionsInViewport(IEnumerable<NodeConnection> allConnections)
        {
            return allConnections.Where(IsConnectionInViewport);
        }

        /// <summary>
        /// 计算节点的边界矩形
        /// </summary>
        public Rect CalculateNodesBounds(IEnumerable<Node> nodes)
        {
            if (!nodes.Any())
                return Rect.Empty;

            var minX = double.MaxValue;
            var minY = double.MaxValue;
            var maxX = double.MinValue;
            var maxY = double.MinValue;

            foreach (var node in nodes)
            {
                minX = Math.Min(minX, node.X);
                minY = Math.Min(minY, node.Y);
                maxX = Math.Max(maxX, node.X + node.Width);
                maxY = Math.Max(maxY, node.Y + node.Height);
            }

            return new Rect(minX, minY, maxX - minX, maxY - minY);
        }

        /// <summary>
        /// 检查是否需要重新计算视口
        /// </summary>
        public bool ShouldUpdateViewport(Rect newViewport)
        {
            if (_currentViewport.IsEmpty)
                return true;

            // 如果视口变化超过一定阈值，则需要更新
            var deltaX = Math.Abs(newViewport.X - _currentViewport.X);
            var deltaY = Math.Abs(newViewport.Y - _currentViewport.Y);
            var deltaWidth = Math.Abs(newViewport.Width - _currentViewport.Width);
            var deltaHeight = Math.Abs(newViewport.Height - _currentViewport.Height);

            var threshold = Math.Min(_currentViewport.Width, _currentViewport.Height) * 0.1; // 10%的变化阈值

            return deltaX > threshold || deltaY > threshold || 
                   deltaWidth > threshold || deltaHeight > threshold;
        }

        /// <summary>
        /// 获取优化统计信息
        /// </summary>
        public ViewportOptimizationStats GetStats(IEnumerable<Node> allNodes, IEnumerable<NodeConnection> allConnections)
        {
            var allNodesList = allNodes.ToList();
            var allConnectionsList = allConnections.ToList();
            
            return new ViewportOptimizationStats
            {
                TotalNodes = allNodesList.Count,
                VisibleNodes = GetNodesInViewport(allNodesList).Count(),
                TotalConnections = allConnectionsList.Count,
                VisibleConnections = GetConnectionsInViewport(allConnectionsList).Count(),
                ViewportArea = _currentViewport.Width * _currentViewport.Height,
                CacheHitRate = CalculateCacheHitRate()
            };
        }

        private double CalculateCacheHitRate()
        {
            var totalCacheEntries = _nodeVisibilityCache.Count + _connectionVisibilityCache.Count;
            return totalCacheEntries > 0 ? 1.0 : 0.0; // 简化实现
        }
    }

    /// <summary>
    /// 视口优化统计信息
    /// </summary>
    public class ViewportOptimizationStats
    {
        public int TotalNodes { get; set; }
        public int VisibleNodes { get; set; }
        public int TotalConnections { get; set; }
        public int VisibleConnections { get; set; }
        public double ViewportArea { get; set; }
        public double CacheHitRate { get; set; }

        public double NodeCullingRatio => TotalNodes > 0 ? (double)(TotalNodes - VisibleNodes) / TotalNodes : 0;
        public double ConnectionCullingRatio => TotalConnections > 0 ? (double)(TotalConnections - VisibleConnections) / TotalConnections : 0;

        public override string ToString()
        {
            return $"节点: {VisibleNodes}/{TotalNodes} ({NodeCullingRatio:P1} 裁剪), " +
                   $"连接: {VisibleConnections}/{TotalConnections} ({ConnectionCullingRatio:P1} 裁剪), " +
                   $"视口: {ViewportArea:F0}px², 缓存命中率: {CacheHitRate:P1}";
        }
    }

    /// <summary>
    /// 渲染层级枚举
    /// </summary>
    public enum RenderLayer
    {
        Background,     // 背景网格
        Connections,    // 连接线
        Nodes,          // 节点
        Selection,      // 选择框
        Preview,        // 预览连接线
        UI              // UI元素
    }

    /// <summary>
    /// 渲染优先级管理器
    /// </summary>
    public static class RenderPriorityManager
    {
        private static readonly Dictionary<RenderLayer, int> LayerPriorities = new()
        {
            { RenderLayer.Background, 0 },
            { RenderLayer.Connections, 1 },
            { RenderLayer.Nodes, 2 },
            { RenderLayer.Selection, 3 },
            { RenderLayer.Preview, 4 },
            { RenderLayer.UI, 5 }
        };

        public static int GetPriority(RenderLayer layer)
        {
            return LayerPriorities.TryGetValue(layer, out var priority) ? priority : 0;
        }

        public static IEnumerable<RenderLayer> GetOrderedLayers()
        {
            return LayerPriorities.OrderBy(kvp => kvp.Value).Select(kvp => kvp.Key);
        }
    }
}
