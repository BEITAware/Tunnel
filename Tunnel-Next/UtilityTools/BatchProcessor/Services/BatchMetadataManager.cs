using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace Tunnel_Next.UtilityTools.BatchProcessor.Services
{
    /// <summary>
    /// 批处理元数据清洗选项
    /// </summary>
    public class BatchMetadataCleanOptions
    {
        /// <summary>
        /// 是否移除空值
        /// </summary>
        public bool RemoveNullValues { get; set; } = true;

        /// <summary>
        /// 是否清理处理历史
        /// </summary>
        public bool CleanProcessingHistory { get; set; } = true;

        /// <summary>
        /// 最大历史记录数
        /// </summary>
        public int MaxHistoryRecords { get; set; } = 20;

        /// <summary>
        /// 是否合并重复键
        /// </summary>
        public bool MergeDuplicateKeys { get; set; } = true;
    }

    /// <summary>
    /// 批处理元数据验证结果
    /// </summary>
    public class BatchMetadataValidationResult
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
    /// 批处理元数据管理器 - 简化版本的元数据管理
    /// </summary>
    public class BatchMetadataManager
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
                    ["创建时间"] = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
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
        /// 合并多个元数据字典
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
                var json = JsonSerializer.Serialize(metadata);
                var copied = JsonSerializer.Deserialize<Dictionary<string, object>>(json);
                return copied ?? new Dictionary<string, object>();
            }
            catch (Exception)
            {
                return new Dictionary<string, object>();
            }
        }

        /// <summary>
        /// 向元数据中添加处理记录
        /// </summary>
        /// <param name="metadata">元数据字典</param>
        /// <param name="operation">操作名称</param>
        /// <param name="blockType">积木块类型</param>
        /// <param name="details">操作详情</param>
        /// <returns>更新后的元数据</returns>
        public Dictionary<string, object> AddProcessingRecord(
            Dictionary<string, object>? metadata, 
            string operation, 
            string blockType,
            Dictionary<string, object>? details = null)
        {
            if (metadata == null)
                metadata = new Dictionary<string, object>();

            if (!metadata.ContainsKey("处理历史"))
                metadata["处理历史"] = new List<Dictionary<string, object>>();

            var processingHistory = metadata["处理历史"] as List<Dictionary<string, object>> ?? new List<Dictionary<string, object>>();

            var record = new Dictionary<string, object>
            {
                ["操作"] = operation,
                ["积木块类型"] = blockType,
                ["时间"] = DateTime.Now.ToString("HH:mm:ss")
            };

            if (details != null)
            {
                var simplifiedDetails = new Dictionary<string, object>();

                foreach (var kvp in details)
                {
                    // 只保存简单类型的详情
                    if (kvp.Value is string || kvp.Value is int || kvp.Value is double || kvp.Value is bool)
                    {
                        simplifiedDetails[kvp.Key] = kvp.Value;
                    }
                }

                if (simplifiedDetails.Count > 0)
                    record["详情"] = simplifiedDetails;
            }

            processingHistory.Add(record);

            // 限制历史记录数量
            if (processingHistory.Count > 50)
            {
                processingHistory.RemoveAt(0);
            }

            metadata["处理历史"] = processingHistory;
            return metadata;
        }

        /// <summary>
        /// 清洗元数据
        /// </summary>
        /// <param name="metadata">要清洗的元数据</param>
        /// <param name="options">清洗选项</param>
        /// <returns>清洗后的元数据</returns>
        public Dictionary<string, object> CleanMetadata(Dictionary<string, object>? metadata, BatchMetadataCleanOptions? options = null)
        {
            if (metadata == null)
                return new Dictionary<string, object>();

            options ??= new BatchMetadataCleanOptions();
            var cleaned = CopyMetadata(metadata);

            lock (_lock)
            {
                // 移除空值
                if (options.RemoveNullValues)
                {
                    var keysToRemove = cleaned.Where(kvp => kvp.Value == null).Select(kvp => kvp.Key).ToList();
                    foreach (var key in keysToRemove)
                    {
                        cleaned.Remove(key);
                    }
                }

                // 清理处理历史
                if (options.CleanProcessingHistory && cleaned.ContainsKey("处理历史"))
                {
                    cleaned["处理历史"] = CleanProcessingHistory(cleaned["处理历史"], options.MaxHistoryRecords);
                }

                return cleaned;
            }
        }

        /// <summary>
        /// 验证元数据完整性
        /// </summary>
        /// <param name="metadata">要验证的元数据</param>
        /// <returns>验证结果</returns>
        public BatchMetadataValidationResult ValidateMetadata(Dictionary<string, object>? metadata)
        {
            var result = new BatchMetadataValidationResult { IsValid = true };

            if (metadata == null)
            {
                result.IsValid = false;
                result.Errors.Add("元数据为空");
                return result;
            }

            // 检查推荐字段
            var recommendedFields = new[] { "创建时间", "处理历史" };
            foreach (var field in recommendedFields)
            {
                if (!metadata.ContainsKey(field))
                {
                    result.Warnings.Add($"缺少推荐字段: {field}");
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
        /// 注入元数据（覆盖注入）
        /// </summary>
        /// <param name="targetMetadata">目标元数据</param>
        /// <param name="sourceMetadata">源元数据</param>
        /// <param name="keys">要注入的键，如果为null则注入所有</param>
        /// <returns>注入后的元数据</returns>
        public Dictionary<string, object> InjectMetadata(
            Dictionary<string, object>? targetMetadata,
            Dictionary<string, object>? sourceMetadata,
            IEnumerable<string>? keys = null)
        {
            if (targetMetadata == null)
                targetMetadata = new Dictionary<string, object>();

            if (sourceMetadata == null)
                return targetMetadata;

            lock (_lock)
            {
                var keysToInject = keys?.ToList() ?? sourceMetadata.Keys.ToList();

                foreach (var key in keysToInject)
                {
                    if (sourceMetadata.ContainsKey(key))
                    {
                        targetMetadata[key] = sourceMetadata[key]; // 覆盖注入
                    }
                }

                return targetMetadata;
            }
        }

        /// <summary>
        /// 移除元数据键
        /// </summary>
        /// <param name="metadata">元数据字典</param>
        /// <param name="keys">要移除的键</param>
        /// <returns>移除后的元数据</returns>
        public Dictionary<string, object> RemoveMetadata(Dictionary<string, object>? metadata, params string[] keys)
        {
            if (metadata == null)
                return new Dictionary<string, object>();

            lock (_lock)
            {
                var result = CopyMetadata(metadata);

                foreach (var key in keys)
                {
                    result.Remove(key);
                }

                return result;
            }
        }

        /// <summary>
        /// 检查元数据是否包含指定键
        /// </summary>
        /// <param name="metadata">元数据字典</param>
        /// <param name="key">要检查的键</param>
        /// <returns>是否包含</returns>
        public bool ContainsKey(Dictionary<string, object>? metadata, string key)
        {
            return metadata?.ContainsKey(key) ?? false;
        }

        /// <summary>
        /// 获取元数据值
        /// </summary>
        /// <param name="metadata">元数据字典</param>
        /// <param name="key">键</param>
        /// <param name="defaultValue">默认值</param>
        /// <returns>元数据值</returns>
        public T? GetMetadataValue<T>(Dictionary<string, object>? metadata, string key, T? defaultValue = default)
        {
            if (metadata == null || !metadata.ContainsKey(key))
                return defaultValue;

            try
            {
                var value = metadata[key];
                if (value is T typedValue)
                    return typedValue;

                // 尝试转换
                return (T?)Convert.ChangeType(value, typeof(T));
            }
            catch
            {
                return defaultValue;
            }
        }

        /// <summary>
        /// 设置元数据值
        /// </summary>
        /// <param name="metadata">元数据字典</param>
        /// <param name="key">键</param>
        /// <param name="value">值</param>
        /// <returns>更新后的元数据</returns>
        public Dictionary<string, object> SetMetadataValue(Dictionary<string, object>? metadata, string key, object? value)
        {
            if (metadata == null)
                metadata = new Dictionary<string, object>();

            lock (_lock)
            {
                if (value != null)
                {
                    metadata[key] = value;
                }
                else
                {
                    metadata.Remove(key);
                }

                return metadata;
            }
        }

        /// <summary>
        /// 过滤元数据
        /// </summary>
        /// <param name="metadata">元数据字典</param>
        /// <param name="predicate">过滤条件</param>
        /// <returns>过滤后的元数据</returns>
        public Dictionary<string, object> FilterMetadata(
            Dictionary<string, object>? metadata,
            Func<KeyValuePair<string, object>, bool> predicate)
        {
            if (metadata == null)
                return new Dictionary<string, object>();

            lock (_lock)
            {
                return metadata.Where(predicate).ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
            }
        }

        /// <summary>
        /// 获取元数据摘要信息（内部使用）
        /// </summary>
        /// <param name="metadata">元数据字典</param>
        /// <returns>摘要信息</returns>
        internal string GetMetadataSummary(Dictionary<string, object>? metadata)
        {
            if (metadata == null || metadata.Count == 0)
                return "无元数据";

            var summary = new List<string>();

            if (metadata.ContainsKey("创建时间"))
                summary.Add($"创建: {metadata["创建时间"]}");

            if (metadata.ContainsKey("处理历史") && metadata["处理历史"] is List<Dictionary<string, object>> history)
                summary.Add($"处理步骤: {history.Count}");

            var customCount = metadata.Count - 2; // 减去创建时间和处理历史
            if (customCount > 0)
                summary.Add($"自定义字段: {customCount}");

            return string.Join(", ", summary);
        }
    }
}
