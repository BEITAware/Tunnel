using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Tunnel_Next.UtilityTools.BatchProcessor.Models;

namespace Tunnel_Next.UtilityTools.BatchProcessor.Services
{
    /// <summary>
    /// 批处理执行结果
    /// </summary>
    public class BatchExecutionResult
    {
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
        public TimeSpan Duration { get; set; }
        public int ProcessedBlockCount { get; set; }
        public Dictionary<string, object> FinalMetadata { get; set; } = new();
        public List<string> ProcessingLog { get; set; } = new();
    }

    /// <summary>
    /// 批处理执行器 - 负责执行积木块序列并处理元数据流
    /// </summary>
    public class BatchExecutor
    {
        private readonly BatchMetadataManager _metadataManager;
        private readonly object _lock = new object();

        public BatchExecutor()
        {
            _metadataManager = new BatchMetadataManager();
        }

        /// <summary>
        /// 执行积木块序列
        /// </summary>
        /// <param name="blocks">积木块序列</param>
        /// <param name="initialMetadata">初始元数据</param>
        /// <returns>执行结果</returns>
        public async Task<BatchExecutionResult> ExecuteAsync(
            IEnumerable<CodeBlockBase> blocks, 
            Dictionary<string, object>? initialMetadata = null)
        {
            var result = new BatchExecutionResult();
            var startTime = DateTime.Now;

            try
            {
                var blockList = blocks.ToList();
                result.ProcessedBlockCount = blockList.Count;

                // 初始化元数据
                var currentMetadata = initialMetadata ?? _metadataManager.CreateMetadata();
                result.ProcessingLog.Add($"开始执行 {blockList.Count} 个积木块");

                // 按顺序执行每个积木块
                for (int i = 0; i < blockList.Count; i++)
                {
                    var block = blockList[i];
                    result.ProcessingLog.Add($"执行积木块 {i + 1}: {block.DisplayName} ({block.BlockType})");

                    try
                    {
                        // 处理积木块的元数据流
                        currentMetadata = await ProcessBlockMetadataAsync(block, currentMetadata);
                        
                        // 验证积木块设定
                        if (!block.ValidateSettings())
                        {
                            result.ProcessingLog.Add($"警告: 积木块 {block.DisplayName} 设定无效");
                        }

                        result.ProcessingLog.Add($"完成积木块 {i + 1}: {block.DisplayName}");
                    }
                    catch (Exception ex)
                    {
                        result.ProcessingLog.Add($"错误: 积木块 {block.DisplayName} 执行失败: {ex.Message}");
                        result.Success = false;
                        result.ErrorMessage = $"积木块 {block.DisplayName} 执行失败: {ex.Message}";
                        result.Duration = DateTime.Now - startTime;
                        return result;
                    }
                }

                // 清理最终元数据
                currentMetadata = _metadataManager.CleanMetadata(currentMetadata);
                result.FinalMetadata = currentMetadata;
                result.Success = true;
                result.ProcessingLog.Add("批处理执行完成");
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.ErrorMessage = ex.Message;
                result.ProcessingLog.Add($"批处理执行失败: {ex.Message}");
            }

            result.Duration = DateTime.Now - startTime;
            return result;
        }

        /// <summary>
        /// 处理单个积木块的元数据流
        /// </summary>
        /// <param name="block">积木块</param>
        /// <param name="upstreamMetadata">上游元数据</param>
        /// <returns>处理后的元数据</returns>
        private async Task<Dictionary<string, object>> ProcessBlockMetadataAsync(
            CodeBlockBase block, 
            Dictionary<string, object> upstreamMetadata)
        {
            return await Task.Run(() =>
            {
                lock (_lock)
                {
                    // 使用积木块的元数据处理方法
                    var processedMetadata = block.ProcessMetadata(upstreamMetadata);
                    
                    // 添加执行记录
                    processedMetadata = _metadataManager.AddProcessingRecord(
                        processedMetadata, 
                        "积木块执行", 
                        block.BlockType.ToString(),
                        new Dictionary<string, object>
                        {
                            ["积木块ID"] = block.Id.ToString(),
                            ["积木块名称"] = block.DisplayName,
                            ["执行时间"] = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
                        });

                    return processedMetadata;
                }
            });
        }

        /// <summary>
        /// 验证积木块序列
        /// </summary>
        /// <param name="blocks">积木块序列</param>
        /// <returns>验证结果</returns>
        public BatchMetadataValidationResult ValidateBlockSequence(IEnumerable<CodeBlockBase> blocks)
        {
            var result = new BatchMetadataValidationResult { IsValid = true };

            try
            {
                var blockList = blocks.ToList();

                if (blockList.Count == 0)
                {
                    result.Warnings.Add("积木块序列为空");
                    return result;
                }

                // 验证每个积木块
                for (int i = 0; i < blockList.Count; i++)
                {
                    var block = blockList[i];

                    if (!block.ValidateSettings())
                    {
                        result.Warnings.Add($"积木块 {i + 1} ({block.DisplayName}) 设定无效");
                    }

                    // 验证积木块的元数据
                    var metadataValidation = _metadataManager.ValidateMetadata(block.Metadata);
                    if (!metadataValidation.IsValid)
                    {
                        result.Errors.AddRange(metadataValidation.Errors.Select(e => $"积木块 {i + 1}: {e}"));
                        result.IsValid = false;
                    }

                    result.Warnings.AddRange(metadataValidation.Warnings.Select(w => $"积木块 {i + 1}: {w}"));
                }
            }
            catch (Exception ex)
            {
                result.IsValid = false;
                result.Errors.Add($"验证过程中发生错误: {ex.Message}");
            }

            return result;
        }

        /// <summary>
        /// 获取积木块序列的元数据摘要
        /// </summary>
        /// <param name="blocks">积木块序列</param>
        /// <returns>元数据摘要</returns>
        public string GetSequenceMetadataSummary(IEnumerable<CodeBlockBase> blocks)
        {
            try
            {
                var blockList = blocks.ToList();
                if (blockList.Count == 0)
                    return "无积木块";

                var summary = new List<string>
                {
                    $"积木块数量: {blockList.Count}"
                };

                var blockTypes = blockList.GroupBy(b => b.BlockType)
                    .Select(g => $"{g.Key}: {g.Count()}")
                    .ToList();

                if (blockTypes.Count > 0)
                {
                    summary.Add($"类型分布: {string.Join(", ", blockTypes)}");
                }

                var validBlocks = blockList.Count(b => b.ValidateSettings());
                summary.Add($"有效积木块: {validBlocks}/{blockList.Count}");

                return string.Join(", ", summary);
            }
            catch (Exception ex)
            {
                return $"获取摘要失败: {ex.Message}";
            }
        }

        /// <summary>
        /// 模拟执行（不实际执行，只处理元数据流）
        /// </summary>
        /// <param name="blocks">积木块序列</param>
        /// <param name="initialMetadata">初始元数据</param>
        /// <returns>模拟执行结果</returns>
        public async Task<BatchExecutionResult> SimulateExecutionAsync(
            IEnumerable<CodeBlockBase> blocks, 
            Dictionary<string, object>? initialMetadata = null)
        {
            var result = new BatchExecutionResult();
            var startTime = DateTime.Now;

            try
            {
                var blockList = blocks.ToList();
                result.ProcessedBlockCount = blockList.Count;

                // 初始化元数据
                var currentMetadata = initialMetadata ?? _metadataManager.CreateMetadata();
                result.ProcessingLog.Add($"开始模拟执行 {blockList.Count} 个积木块");

                // 模拟执行每个积木块
                for (int i = 0; i < blockList.Count; i++)
                {
                    var block = blockList[i];
                    result.ProcessingLog.Add($"模拟积木块 {i + 1}: {block.DisplayName} ({block.BlockType})");

                    // 只处理元数据流，不执行实际操作
                    currentMetadata = block.ProcessMetadata(currentMetadata);
                    
                    // 添加模拟执行记录
                    currentMetadata = _metadataManager.AddProcessingRecord(
                        currentMetadata, 
                        "模拟执行", 
                        block.BlockType.ToString(),
                        new Dictionary<string, object>
                        {
                            ["积木块名称"] = block.DisplayName,
                            ["模拟时间"] = DateTime.Now.ToString("HH:mm:ss")
                        });

                    result.ProcessingLog.Add($"完成模拟积木块 {i + 1}: {block.DisplayName}");
                }

                result.FinalMetadata = currentMetadata;
                result.Success = true;
                result.ProcessingLog.Add("模拟执行完成");
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.ErrorMessage = ex.Message;
                result.ProcessingLog.Add($"模拟执行失败: {ex.Message}");
            }

            result.Duration = DateTime.Now - startTime;
            return result;
        }
    }
}
