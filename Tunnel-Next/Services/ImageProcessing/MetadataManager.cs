using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;

namespace Tunnel_Next.Services.ImageProcessing
{
    /// <summary>
    /// 元数据管理器 - 负责处理节点图中的元数据流
    /// 基于Python版本的MetadataManager实现
    /// </summary>
    public class MetadataManager
    {
        private readonly object _lock = new object();

        /// <summary>
        /// 创建新的元数据字典
        /// </summary>
        /// <param name="additionalData">额外的元数据</param>
        /// <returns>新的元数据字典</returns>
        public Dictionary<string, object> CreateMetadata(Dictionary<string, object>? additionalData = null)
        {
            lock (_lock)
            {
                var metadata = new Dictionary<string, object>
                {
                    ["创建时间"] = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"),
                    ["节点路径"] = new List<Dictionary<string, object>>(),
                    ["处理历史"] = new List<Dictionary<string, object>>()
                };

                if (additionalData != null)
                {
                    foreach (var kvp in additionalData)
                    {
                        metadata[kvp.Key] = kvp.Value;
                    }
                }

                return metadata;
            }
        }

        /// <summary>
        /// 合并多个元数据字典，后面的会覆盖前面的同名键
        /// </summary>
        /// <param name="metadataDicts">要合并的元数据字典数组</param>
        /// <returns>合并后的元数据字典</returns>
        public Dictionary<string, object> MergeMetadata(params Dictionary<string, object>?[] metadataDicts)
        {
            lock (_lock)
            {
                var merged = new Dictionary<string, object>();

                foreach (var metadata in metadataDicts)
                {
                    if (metadata != null)
                    {
                        foreach (var kvp in metadata)
                        {
                            merged[kvp.Key] = kvp.Value;
                        }
                    }
                }

                return merged;
            }
        }

        /// <summary>
        /// 深拷贝元数据
        /// </summary>
        /// <param name="metadata">要拷贝的元数据</param>
        /// <returns>拷贝后的元数据</returns>
        public Dictionary<string, object> CopyMetadata(Dictionary<string, object>? metadata)
        {
            if (metadata == null)
                return new Dictionary<string, object>();

            try
            {
                // 使用JSON序列化进行深拷贝
                var json = JsonSerializer.Serialize(metadata);
                var copied = JsonSerializer.Deserialize<Dictionary<string, object>>(json);
                return copied ?? new Dictionary<string, object>();
            }
            catch (Exception ex)
            {
                return new Dictionary<string, object>();
            }
        }

        /// <summary>
        /// 向元数据的节点路径中添加节点（优化版本）
        /// </summary>
        /// <param name="metadata">元数据字典</param>
        /// <param name="nodeId">节点ID</param>
        /// <param name="nodeTitle">节点标题</param>
        /// <returns>更新后的元数据</returns>
        public Dictionary<string, object> AddNodeToPath(Dictionary<string, object>? metadata, int nodeId, string nodeTitle)
        {
            if (metadata == null)
                metadata = new Dictionary<string, object>();

            if (!metadata.ContainsKey("节点路径"))
                metadata["节点路径"] = new List<Dictionary<string, object>>();

            var nodePath = metadata["节点路径"] as List<Dictionary<string, object>> ?? new List<Dictionary<string, object>>();

            // 检查节点是否已经在路径中（避免重复）
            var existingNode = nodePath.FirstOrDefault(n =>
                n.ContainsKey("节点ID") && n["节点ID"].ToString() == nodeId.ToString());

            if (existingNode != null)
            {
                // 节点已存在，不重复添加
                return metadata;
            }

            // 添加新节点（只保存ID和标题）
            var nodeInfo = new Dictionary<string, object>
            {
                ["节点ID"] = nodeId,
                ["节点标题"] = nodeTitle
            };

            nodePath.Add(nodeInfo);

            // 限制路径长度，防止无限增长（最多保留50个节点）
            if (nodePath.Count > 50)
            {
                nodePath.RemoveAt(0);
            }

            metadata["节点路径"] = nodePath;
            return metadata;
        }

        /// <summary>
        /// 向元数据中添加处理记录（优化版本）
        /// </summary>
        /// <param name="metadata">元数据字典</param>
        /// <param name="operation">操作名称</param>
        /// <param name="details">操作详情</param>
        /// <returns>更新后的元数据</returns>
        public Dictionary<string, object> AddProcessingRecord(Dictionary<string, object>? metadata, string operation, Dictionary<string, object>? details = null)
        {
            if (metadata == null)
                metadata = new Dictionary<string, object>();

            if (!metadata.ContainsKey("处理历史"))
                metadata["处理历史"] = new List<Dictionary<string, object>>();

            var processingHistory = metadata["处理历史"] as List<Dictionary<string, object>> ?? new List<Dictionary<string, object>>();

            // 简化的记录格式（不包含时间戳）
            var record = new Dictionary<string, object>
            {
                ["操作"] = operation
            };

            // 只保存重要的详情信息
            if (details != null)
            {
                var simplifiedDetails = new Dictionary<string, object>();

                if (details.ContainsKey("script_type"))
                    simplifiedDetails["脚本类型"] = details["script_type"];

                if (details.ContainsKey("output_types") && details["output_types"] != null)
                    simplifiedDetails["输出类型"] = details["output_types"];

                if (simplifiedDetails.Count > 0)
                    record["详情"] = simplifiedDetails;
            }

            processingHistory.Add(record);

            // 限制历史记录数量，防止无限增长（最多保留100条记录）
            if (processingHistory.Count > 100)
            {
                processingHistory.RemoveAt(0);
            }

            metadata["处理历史"] = processingHistory;
            return metadata;
        }

        /// <summary>
        /// 验证元数据格式
        /// </summary>
        /// <param name="metadata">要验证的元数据</param>
        /// <returns>是否有效</returns>
        public bool ValidateMetadata(Dictionary<string, object>? metadata)
        {
            if (metadata == null)
                return false;

            // 检查必要的字段
            var requiredFields = new[] { "创建时间", "节点路径", "处理历史" };

            foreach (var field in requiredFields)
            {
                if (!metadata.ContainsKey(field))
                    return false;
            }

            return true;
        }

        /// <summary>
        /// 序列化元数据为JSON字符串
        /// </summary>
        /// <param name="metadata">要序列化的元数据</param>
        /// <returns>JSON字符串</returns>
        public string SerializeMetadata(Dictionary<string, object>? metadata)
        {
            try
            {
                if (metadata == null)
                    return "{}";

                var options = new JsonSerializerOptions
                {
                    WriteIndented = true,
                    Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                };

                return JsonSerializer.Serialize(metadata, options);
            }
            catch (Exception ex)
            {
                return "{}";
            }
        }

        /// <summary>
        /// 从JSON字符串反序列化元数据
        /// </summary>
        /// <param name="metadataStr">JSON字符串</param>
        /// <returns>元数据字典</returns>
        public Dictionary<string, object> DeserializeMetadata(string metadataStr)
        {
            try
            {
                if (string.IsNullOrEmpty(metadataStr))
                    return new Dictionary<string, object>();

                var metadata = JsonSerializer.Deserialize<Dictionary<string, object>>(metadataStr);
                return metadata ?? new Dictionary<string, object>();
            }
            catch (Exception ex)
            {
                return new Dictionary<string, object>();
            }
        }
    }
}
