using System;
using Tunnel_Next.Models;

namespace Tunnel_Next.Controls
{
    /// <summary>
    /// 文档Tab变更事件参数
    /// </summary>
    public class DocumentChangedEventArgs : EventArgs
    {
        /// <summary>
        /// 旧文档ID
        /// </summary>
        public string? OldDocumentId { get; }

        /// <summary>
        /// 新文档ID
        /// </summary>
        public string? NewDocumentId { get; }

        /// <summary>
        /// 新文档对象
        /// </summary>
        public IDocumentContent? NewDocument { get; }

        /// <summary>
        /// 构造函数
        /// </summary>
        public DocumentChangedEventArgs(string? oldDocumentId, string? newDocumentId, IDocumentContent? newDocument)
        {
            OldDocumentId = oldDocumentId;
            NewDocumentId = newDocumentId;
            NewDocument = newDocument;
        }
    }

    /// <summary>
    /// 文档关闭事件参数
    /// </summary>
    public class DocumentClosedEventArgs : EventArgs
    {
        /// <summary>
        /// 文档ID
        /// </summary>
        public string DocumentId { get; }

        /// <summary>
        /// 构造函数
        /// </summary>
        public DocumentClosedEventArgs(string documentId)
        {
            if (string.IsNullOrEmpty(documentId))
                throw new ArgumentException("文档ID不能为空", nameof(documentId));

            DocumentId = documentId;
        }
    }
} 