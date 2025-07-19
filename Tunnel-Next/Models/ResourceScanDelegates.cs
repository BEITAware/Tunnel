using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Tunnel_Next.Models
{
    /// <summary>
    /// 扫描上下文，包含扫描所需的所有信息
    /// </summary>
    public class ResourceScanContext
    {
        /// <summary>
        /// 工作文件夹路径
        /// </summary>
        public string WorkFolder { get; set; } = string.Empty;

        /// <summary>
        /// 资源类型定义
        /// </summary>
        public ResourceTypeDefinition TypeDefinition { get; set; } = null!;

        /// <summary>
        /// 服务提供者（用于获取依赖服务）
        /// </summary>
        public IServiceProvider? Services { get; set; }

        /// <summary>
        /// 取消令牌
        /// </summary>
        public System.Threading.CancellationToken CancellationToken { get; set; } = default;

        /// <summary>
        /// 扩展属性字典
        /// </summary>
        public Dictionary<string, object> Properties { get; set; } = new();
    }

    /// <summary>
    /// 资源扫描委托定义
    /// </summary>
    /// <param name="context">扫描上下文</param>
    /// <returns>扫描到的资源对象列表</returns>
    public delegate Task<List<ResourceObject>> ResourceScanDelegate(ResourceScanContext context);

    /// <summary>
    /// 扫描结果
    /// </summary>
    public class ResourceScanResult
    {
        /// <summary>
        /// 扫描到的资源列表
        /// </summary>
        public List<ResourceObject> Resources { get; set; } = new();

        /// <summary>
        /// 是否成功
        /// </summary>
        public bool Success { get; set; } = true;

        /// <summary>
        /// 错误信息
        /// </summary>
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// 扫描耗时（毫秒）
        /// </summary>
        public long ElapsedMilliseconds { get; set; }

        /// <summary>
        /// 扫描的文件数量
        /// </summary>
        public int ScannedFileCount { get; set; }
    }
}
