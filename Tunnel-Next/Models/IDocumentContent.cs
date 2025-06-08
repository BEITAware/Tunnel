using System;
using System.Windows;

namespace Tunnel_Next.Models
{
    /// <summary>
    /// 文档内容接口 - 为多文档Tab系统提供可扩展的文档类型支持
    /// </summary>
    public interface IDocumentContent : IDisposable
    {
        /// <summary>
        /// 文档类型
        /// </summary>
        DocumentType DocumentType { get; }

        /// <summary>
        /// 文档ID - 唯一标识符
        /// </summary>
        string Id { get; }

        /// <summary>
        /// 文档标题
        /// </summary>
        string Title { get; set; }

        /// <summary>
        /// 文档是否已修改
        /// </summary>
        bool IsModified { get; set; }

        /// <summary>
        /// 文档文件路径（如果有）
        /// </summary>
        string? FilePath { get; set; }

        /// <summary>
        /// 文档是否可关闭
        /// </summary>
        bool CanClose { get; set; }

        /// <summary>
        /// 文档标题变化事件
        /// </summary>
        event EventHandler<EventArgs>? TitleChanged;

        /// <summary>
        /// 文档修改状态变化事件
        /// </summary>
        event EventHandler<EventArgs>? ModifiedChanged;

        /// <summary>
        /// 文档请求关闭事件
        /// </summary>
        event EventHandler<EventArgs>? CloseRequested;

        /// <summary>
        /// 获取用于显示的UI控件
        /// </summary>
        /// <returns>UI控件</returns>
        FrameworkElement GetContentControl();

        /// <summary>
        /// 保存文档
        /// </summary>
        /// <param name="filePath">保存路径，为null时显示保存对话框</param>
        /// <returns>保存是否成功</returns>
        Task<bool> SaveAsync(string? filePath = null);

        /// <summary>
        /// 同步保存文档（为了向后兼容）
        /// </summary>
        /// <param name="filePath">保存路径，为null时显示保存对话框</param>
        /// <returns>保存是否成功</returns>
        bool Save(string? filePath = null);

        /// <summary>
        /// 检查文档是否可以关闭
        /// </summary>
        /// <returns>true表示可以关闭，false表示需要用户确认</returns>
        bool CheckCanClose();

        /// <summary>
        /// 激活文档时调用
        /// </summary>
        void OnActivated();

        /// <summary>
        /// 失活文档时调用
        /// </summary>
        void OnDeactivated();
    }

    /// <summary>
    /// 文档类型枚举
    /// </summary>
    public enum DocumentType
    {
        /// <summary>
        /// 未知类型
        /// </summary>
        Unknown = 0,

        /// <summary>
        /// 节点图文档
        /// </summary>
        NodeGraph = 1,

        /// <summary>
        /// 图像文档
        /// </summary>
        Image = 2,

        /// <summary>
        /// 预设文档
        /// </summary>
        Preset = 3
    }
}
