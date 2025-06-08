using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;

namespace Tunnel_Next.Services.ImageProcessing
{
    /// <summary>
    /// 元数据清洗选项
    /// </summary>
    public class MetadataCleanOptions
    {
        /// <summary>
        /// 是否移除空值
        /// </summary>
        public bool RemoveNullValues { get; set; } = true;

        /// <summary>
        /// 是否清理节点路径
        /// </summary>
        public bool CleanNodePath { get; set; } = true;

        /// <summary>
        /// 是否清理处理历史
        /// </summary>
        public bool CleanProcessingHistory { get; set; } = true;

        /// <summary>
        /// 最大历史记录数
        /// </summary>
        public int MaxHistoryRecords { get; set; } = 50;

        /// <summary>
        /// 是否移除过期时间戳
        /// </summary>
        public bool RemoveExpiredTimestamps { get; set; } = false;

        /// <summary>
        /// 时间戳过期小时数
        /// </summary>
        public int TimestampExpiryHours { get; set; } = 24;

        /// <summary>
        /// 是否合并重复键
        /// </summary>
        public bool MergeDuplicateKeys { get; set; } = true;
    }

    /// <summary>
    /// 元数据验证结果
    /// </summary>
    public class MetadataValidationResult
    {
        /// <summary>
        /// 是否有效
        /// </summary>
        public bool IsValid { get; set; }

        /// <summary>
        /// 错误列表
        /// </summary>
        public List<string> Errors { get; set; } = new List<string>();

        /// <summary>
        /// 警告列表
        /// </summary>
        public List<string> Warnings { get; set; } = new List<string>();
    }

    /// <summary>
    /// 元数据管理器 - 负责处理节点图中的元数据流
    /// 基于Python版本的MetadataManager实现
    /// </summary>
    public class MetadataManager
    {
        private readonly object _lock = new object();

        /// <summary>
        /// 创建新的元数据字典（仅包含用户提供的数据）
        /// </summary>
        /// <param name="additionalData">额外的元数据</param>
        /// <returns>新的元数据字典</returns>
        public Dictionary<string, object> CreateMetadata(Dictionary<string, object>? additionalData = null)
        {
            lock (_lock)
            {
                var metadata = new Dictionary<string, object>();

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
        /// 清洗元数据 - 移除无效、过期或冗余的元数据
        /// </summary>
        /// <param name="metadata">要清洗的元数据</param>
        /// <param name="options">清洗选项</param>
        /// <returns>清洗后的元数据</returns>
        public Dictionary<string, object> CleanMetadata(Dictionary<string, object>? metadata, MetadataCleanOptions? options = null)
        {
            if (metadata == null)
                return new Dictionary<string, object>();

            options ??= new MetadataCleanOptions();
            var cleaned = CopyMetadata(metadata);

            lock (_lock)
            {
                // 1. 移除空值和无效值
                if (options.RemoveNullValues)
                {
                    var keysToRemove = cleaned.Where(kvp => kvp.Value == null).Select(kvp => kvp.Key).ToList();
                    foreach (var key in keysToRemove)
                    {
                        cleaned.Remove(key);
                    }
                }

                // 2. 清理节点路径
                if (options.CleanNodePath && cleaned.ContainsKey("节点路径"))
                {
                    cleaned["节点路径"] = CleanNodePath(cleaned["节点路径"]);
                }

                // 3. 清理处理历史
                if (options.CleanProcessingHistory && cleaned.ContainsKey("处理历史"))
                {
                    cleaned["处理历史"] = CleanProcessingHistory(cleaned["处理历史"], options.MaxHistoryRecords);
                }

                // 4. 移除过期的时间戳
                if (options.RemoveExpiredTimestamps)
                {
                    RemoveExpiredTimestamps(cleaned, options.TimestampExpiryHours);
                }

                // 5. 合并重复的键值对
                if (options.MergeDuplicateKeys)
                {
                    MergeDuplicateKeys(cleaned);
                }

                return cleaned;
            }
        }

        /// <summary>
        /// 验证元数据完整性
        /// </summary>
        /// <param name="metadata">要验证的元数据</param>
        /// <returns>验证结果</returns>
        public MetadataValidationResult ValidateMetadata(Dictionary<string, object>? metadata)
        {
            var result = new MetadataValidationResult { IsValid = true };

            if (metadata == null)
            {
                result.IsValid = false;
                result.Errors.Add("元数据为空");
                return result;
            }

            // 检查必需的基础字段
            var requiredFields = new[] { "创建时间", "节点路径", "处理历史" };
            foreach (var field in requiredFields)
            {
                if (!metadata.ContainsKey(field))
                {
                    result.Warnings.Add($"缺少推荐字段: {field}");
                }
            }

            // 验证节点路径格式
            if (metadata.ContainsKey("节点路径"))
            {
                if (!(metadata["节点路径"] is List<Dictionary<string, object>>))
                {
                    result.Errors.Add("节点路径格式无效");
                    result.IsValid = false;
                }
            }

            // 验证处理历史格式
            if (metadata.ContainsKey("处理历史"))
            {
                if (!(metadata["处理历史"] is List<Dictionary<string, object>>))
                {
                    result.Errors.Add("处理历史格式无效");
                    result.IsValid = false;
                }
            }

            return result;
        }

        /// <summary>
        /// 清理节点路径
        /// </summary>
        private object CleanNodePath(object nodePath)
        {
            if (!(nodePath is List<Dictionary<string, object>> pathList))
                return new List<Dictionary<string, object>>();

            // 移除重复的节点
            var seen = new HashSet<string>();
            var cleaned = new List<Dictionary<string, object>>();

            foreach (var node in pathList)
            {
                if (node.ContainsKey("节点ID") && node.ContainsKey("节点标题"))
                {
                    var nodeKey = $"{node["节点ID"]}_{node["节点标题"]}";
                    if (!seen.Contains(nodeKey))
                    {
                        seen.Add(nodeKey);
                        cleaned.Add(node);
                    }
                }
            }

            return cleaned;
        }

        /// <summary>
        /// 清理处理历史
        /// </summary>
        private object CleanProcessingHistory(object processingHistory, int maxRecords)
        {
            if (!(processingHistory is List<Dictionary<string, object>> historyList))
                return new List<Dictionary<string, object>>();

            // 保留最新的记录
            if (historyList.Count > maxRecords)
            {
                return historyList.Skip(historyList.Count - maxRecords).ToList();
            }

            return historyList;
        }

        /// <summary>
        /// 移除过期的时间戳
        /// </summary>
        private void RemoveExpiredTimestamps(Dictionary<string, object> metadata, int expiryHours)
        {
            var cutoffTime = DateTime.Now.AddHours(-expiryHours);
            var keysToRemove = new List<string>();

            foreach (var kvp in metadata)
            {
                if (kvp.Key.Contains("时间") && kvp.Value is string timeStr)
                {
                    if (DateTime.TryParse(timeStr, out var timestamp) && timestamp < cutoffTime)
                    {
                        keysToRemove.Add(kvp.Key);
                    }
                }
            }

            foreach (var key in keysToRemove)
            {
                metadata.Remove(key);
            }
        }

        /// <summary>
        /// 合并重复的键值对
        /// </summary>
        private void MergeDuplicateKeys(Dictionary<string, object> metadata)
        {
            // 查找相似的键并合并
            var groups = metadata.GroupBy(kvp => kvp.Key.ToLowerInvariant()).Where(g => g.Count() > 1);

            foreach (var group in groups)
            {
                var firstKey = group.First().Key;
                var values = group.Select(kvp => kvp.Value).ToList();

                // 移除重复的键
                foreach (var kvp in group.Skip(1))
                {
                    metadata.Remove(kvp.Key);
                }

                // 合并值（如果是列表类型）
                if (values.All(v => v is List<object>))
                {
                    var mergedList = new List<object>();
                    foreach (var list in values.Cast<List<object>>())
                    {
                        mergedList.AddRange(list);
                    }
                    metadata[firstKey] = mergedList;
                }
            }
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
