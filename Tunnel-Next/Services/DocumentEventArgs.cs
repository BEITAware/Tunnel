using System;
using Tunnel_Next.Models;

namespace Tunnel_Next.Services
{
    /// <summary>
    /// 文档事件参数
    /// </summary>
    public class DocumentEventArgs : EventArgs
    {
        /// <summary>
        /// 文档对象
        /// </summary>
        public IDocumentContent Document { get; }

        /// <summary>
        /// 文档ID
        /// </summary>
        public string DocumentId { get; }

        /// <summary>
        /// 构造函数
        /// </summary>
        public DocumentEventArgs(IDocumentContent document)
        {
            Document = document ?? throw new ArgumentNullException(nameof(document));
            DocumentId = document.Id;
        }

        /// <summary>
        /// 构造函数 - 只提供文档ID的情况
        /// </summary>
        public DocumentEventArgs(string documentId)
        {
            if (string.IsNullOrEmpty(documentId))
                throw new ArgumentException("文档ID不能为空", nameof(documentId));

            Document = null!;
            DocumentId = documentId;
        }
    }

    /// <summary>
    /// 文档变更事件参数
    /// </summary>
    public class DocumentChangedEventArgs : EventArgs
    {
        /// <summary>
        /// 旧文档对象
        /// </summary>
        public IDocumentContent? OldDocument { get; }

        /// <summary>
        /// 新文档对象
        /// </summary>
        public IDocumentContent? NewDocument { get; }

        /// <summary>
        /// 旧文档ID
        /// </summary>
        public string? OldDocumentId { get; }

        /// <summary>
        /// 新文档ID
        /// </summary>
        public string? NewDocumentId { get; }

        /// <summary>
        /// 构造函数
        /// </summary>
        public DocumentChangedEventArgs(IDocumentContent? oldDocument, IDocumentContent? newDocument)
        {
            OldDocument = oldDocument;
            NewDocument = newDocument;
            OldDocumentId = oldDocument?.Id;
            NewDocumentId = newDocument?.Id;
        }

        /// <summary>
        /// 构造函数 - 只提供文档ID的情况
        /// </summary>
        public DocumentChangedEventArgs(string? oldDocumentId, string? newDocumentId)
        {
            OldDocument = null;
            NewDocument = null;
            OldDocumentId = oldDocumentId;
            NewDocumentId = newDocumentId;
        }
    }

    /// <summary>
    /// 保存文档事件参数
    /// </summary>
    public class SaveDocumentEventArgs : EventArgs
    {
        /// <summary>
        /// 文档ID
        /// </summary>
        public string DocumentId { get; }

        /// <summary>
        /// 是否另存为
        /// </summary>
        public bool SaveAs { get; }

        /// <summary>
        /// 构造函数
        /// </summary>
        public SaveDocumentEventArgs(string documentId, bool saveAs = false)
        {
            if (string.IsNullOrEmpty(documentId))
                throw new ArgumentException("文档ID不能为空", nameof(documentId));

            DocumentId = documentId;
            SaveAs = saveAs;
        }
    }
} 