namespace Tunnel_Next.Models
{
    /// <summary>
    /// 资源项目类型枚举
    /// 所有资源类型的统一定义
    /// </summary>
    public enum ResourceItemType
    {
        /// <summary>
        /// 文件夹
        /// </summary>
        Folder,

        /// <summary>
        /// 图像文件
        /// </summary>
        Image,

        /// <summary>
        /// 模板文件
        /// </summary>
        Template,

        /// <summary>
        /// 节点图文件
        /// </summary>
        NodeGraph,

        /// <summary>
        /// 脚本文件
        /// </summary>
        Script,

        /// <summary>
        /// 素材文件
        /// </summary>
        Material,

        /// <summary>
        /// 其他文件
        /// </summary>
        Other
    }
}
