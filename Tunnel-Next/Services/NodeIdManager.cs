using System;
using System.Threading;

namespace Tunnel_Next.Services
{
    /// <summary>
    /// 节点ID管理器 - 提供全局唯一且持久的节点ID
    /// </summary>
    public class NodeIdManager
    {
        private static readonly Lazy<NodeIdManager> _instance = new(() => new NodeIdManager());
        public static NodeIdManager Instance => _instance.Value;

        private long _nextNodeId = 1;
        private readonly object _lockObject = new();

        private NodeIdManager()
        {
            // 使用时间戳作为起始ID，确保重启后ID不重复
            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            _nextNodeId = timestamp * 1000; // 乘以1000为后续ID留出空间
        }

        /// <summary>
        /// 生成新的节点ID
        /// </summary>
        /// <returns>唯一的节点ID</returns>
        public int GenerateNodeId()
        {
            lock (_lockObject)
            {
                var id = (int)Interlocked.Increment(ref _nextNodeId);
                return id;
            }
        }

        /// <summary>
        /// 确保ID不冲突（用于加载时验证）
        /// </summary>
        /// <param name="existingId">现有ID</param>
        public void EnsureIdNotConflict(int existingId)
        {
            lock (_lockObject)
            {
                if (existingId >= _nextNodeId)
                {
                    _nextNodeId = existingId + 1;
                }
            }
        }

        /// <summary>
        /// 重置ID计数器（仅用于测试）
        /// </summary>
        public void ResetForTesting()
        {
            lock (_lockObject)
            {
                _nextNodeId = 1;
            }
        }
    }
}
