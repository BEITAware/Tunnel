using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Tunnel_Next.Models;
using Tunnel_Next.ViewModels;
using Tunnel_Next.Controls;

namespace Tunnel_Next.Services
{
    /// <summary>
    /// 文档管理器服务 - 管理多文档Tab系统中的所有文档
    /// </summary>
    public class DocumentManagerService
    {
        #region 私有字段

        private readonly Dictionary<string, IDocumentContent> _documents = new();
        private DocumentFactory _documentFactory;
        private string? _activeDocumentId;

        #endregion

        #region 事件

        /// <summary>
        /// 文档添加时触发
        /// </summary>
        public event EventHandler<DocumentEventArgs>? DocumentAdded;

        /// <summary>
        /// 文档关闭时触发
        /// </summary>
        public event EventHandler<DocumentEventArgs>? DocumentClosed;

        /// <summary>
        /// 活动文档变化时触发
        /// </summary>
        public event EventHandler<DocumentChangedEventArgs>? ActiveDocumentChanged;

        /// <summary>
        /// 请求新建文档时触发
        /// </summary>
        public event EventHandler<EventArgs>? NewDocumentRequested;

        /// <summary>
        /// 请求打开文档时触发
        /// </summary>
        public event EventHandler<EventArgs>? OpenDocumentRequested;

        /// <summary>
        /// 请求保存文档时触发
        /// </summary>
        public event EventHandler<SaveDocumentEventArgs>? SaveDocumentRequested;

        #endregion

        #region 构造函数

        public DocumentManagerService(DocumentFactory documentFactory)
        {
            _documentFactory = documentFactory ?? throw new ArgumentNullException(nameof(documentFactory));
        }

        #endregion

        #region 公共属性

        /// <summary>
        /// 活动文档ID
        /// </summary>
        public string? ActiveDocumentId => _activeDocumentId;

        /// <summary>
        /// 活动文档
        /// </summary>
        public IDocumentContent? ActiveDocument =>
            _activeDocumentId != null && _documents.TryGetValue(_activeDocumentId, out var doc) ? doc : null;

        /// <summary>
        /// 文档数量
        /// </summary>
        public int DocumentCount => _documents.Count;

        /// <summary>
        /// 所有文档
        /// </summary>
        public IEnumerable<IDocumentContent> Documents => _documents.Values;

        /// <summary>
        /// 所有文档ID
        /// </summary>
        public IEnumerable<string> DocumentIds => _documents.Keys;

        #endregion

        #region 文档工厂管理

        /// <summary>
        /// 更新文档工厂
        /// </summary>
        /// <param name="documentFactory">新的文档工厂</param>
        public void UpdateDocumentFactory(DocumentFactory documentFactory)
        {
            _documentFactory = documentFactory ?? throw new ArgumentNullException(nameof(documentFactory));
        }

        #endregion

        #region 节点图文档管理

        /// <summary>
        /// 请求新建文档
        /// </summary>
        public void RequestNewDocument()
        {
            NewDocumentRequested?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// 请求打开文档
        /// </summary>
        public void RequestOpenDocument()
        {
            OpenDocumentRequested?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// 请求保存文档
        /// </summary>
        /// <param name="documentId">文档ID，默认为当前活动文档</param>
        /// <param name="saveAs">是否另存为</param>
        public void RequestSaveDocument(string? documentId = null, bool saveAs = false)
        {
            documentId ??= _activeDocumentId;
            if (documentId != null)
            {
                SaveDocumentRequested?.Invoke(this, new SaveDocumentEventArgs(documentId, saveAs));
            }
        }

        /// <summary>
        /// 创建新的节点图文档
        /// </summary>
        /// <param name="name">文档名称</param>
        /// <returns>创建的文档</returns>
        public IDocumentContent CreateNodeGraphDocument(string? name = null)
        {
            try
            {
                // 创建文档
                var document = _documentFactory.CreateNodeGraphDocument(name);

                // 添加到管理器
                _documents[document.Id] = document;

                // 触发事件
                DocumentAdded?.Invoke(this, new DocumentEventArgs(document));

                // 激活文档
                SetActiveDocument(document.Id);

                return document;
            }
            catch (Exception ex)
            {
                throw;
            }
        }

        /// <summary>
        /// 从文件加载节点图文档
        /// </summary>
        /// <param name="filePath">文件路径</param>
        /// <returns>加载的文档</returns>
        public async Task<IDocumentContent> LoadNodeGraphDocumentAsync(string filePath)
        {
            try
            {
                // 检查是否已经打开
                var existingDoc = _documents.Values.OfType<NodeGraphDocument>()
                    .FirstOrDefault(d => string.Equals(d.FilePath, filePath, StringComparison.OrdinalIgnoreCase));

                if (existingDoc != null)
                {
                    // 已经打开，激活该文档
                    SetActiveDocument(existingDoc.Id);
                    return existingDoc;
                }

                // 加载文档
                var document = await _documentFactory.LoadNodeGraphDocumentAsync(filePath);

                // 添加到管理器
                _documents[document.Id] = document;

                // 触发事件
                DocumentAdded?.Invoke(this, new DocumentEventArgs(document));

                // 激活文档
                SetActiveDocument(document.Id);

                return document;
            }
            catch (Exception ex)
            {
                throw;
            }
        }

        #endregion

        #region 通用文档管理

        /// <summary>
        /// 获取文档
        /// </summary>
        /// <param name="documentId">文档ID</param>
        /// <returns>文档，不存在返回null</returns>
        public IDocumentContent? GetDocument(string documentId)
        {
            return _documents.TryGetValue(documentId, out var document) ? document : null;
        }

        /// <summary>
        /// 获取节点图文档
        /// </summary>
        /// <param name="documentId">文档ID</param>
        /// <returns>节点图文档，不存在或类型不匹配返回null</returns>
        public NodeGraphDocument? GetNodeGraphDocument(string? documentId)
        {
            return documentId != null ? GetDocument(documentId) as NodeGraphDocument : null;
        }

        /// <summary>
        /// 设置活动文档
        /// </summary>
        /// <param name="documentId">文档ID</param>
        /// <returns>是否设置成功</returns>
        public bool SetActiveDocument(string? documentId)
        {
            if (documentId != null && !_documents.ContainsKey(documentId))
                return false;

            var oldDocumentId = _activeDocumentId;
            var oldDocument = oldDocumentId != null ? GetDocument(oldDocumentId) : null;

            // 设置新活动文档
            _activeDocumentId = documentId;
            var newDocument = documentId != null ? GetDocument(documentId) : null;

            // 触发事件（但不在这里调用OnActivated/OnDeactivated，因为这会在UI层处理）
            if (oldDocumentId != documentId)
            {
                ActiveDocumentChanged?.Invoke(this, new DocumentChangedEventArgs(oldDocument, newDocument));
            }

            return true;
        }

        /// <summary>
        /// 关闭文档
        /// </summary>
        /// <param name="documentId">文档ID</param>
        /// <returns>是否关闭成功</returns>
        public bool CloseDocument(string documentId)
        {
            try
            {
                // 获取文档
                var document = GetDocument(documentId);
                if (document == null)
                    return false;

                // 检查文档是否可以关闭
                if (!document.CheckCanClose())
                    return false;

                // 若关闭的是当前活动文档，需要切换到其他文档
                if (documentId == _activeDocumentId)
                {
                    // 找到下一个可激活的文档（优先选择前一个）
                    var docIds = DocumentIds.ToList();
                    var nextDocId = docIds.Count > 1 ? docIds.FirstOrDefault(id => id != documentId) : null;
                    SetActiveDocument(nextDocId);
                }

                // 移除文档
                _documents.Remove(documentId);

                // 触发事件
                DocumentClosed?.Invoke(this, new DocumentEventArgs(documentId));

                // 释放资源
                document.Dispose();

                return true;
            }
            catch (Exception ex)
            {
                return false;
            }
        }

        /// <summary>
        /// 关闭所有文档
        /// </summary>
        /// <returns>是否全部关闭成功</returns>
        public bool CloseAllDocuments()
        {
            bool allClosed = true;
            var docIds = DocumentIds.ToList(); // 创建副本避免迭代中修改集合

            foreach (var docId in docIds)
            {
                if (!CloseDocument(docId))
                {
                    allClosed = false;
                }
            }

            return allClosed;
        }

        /// <summary>
        /// 保存文档
        /// </summary>
        /// <param name="documentId">文档ID</param>
        /// <param name="filePath">文件路径，为null时使用文档的现有路径</param>
        /// <returns>是否保存成功</returns>
        public bool SaveDocument(string documentId, string? filePath = null)
        {
            var document = GetDocument(documentId);
            return document?.Save(filePath) ?? false;
        }

        /// <summary>
        /// 保存所有文档
        /// </summary>
        /// <returns>是否全部保存成功</returns>
        public bool SaveAllDocuments()
        {
            bool allSaved = true;
            var docIds = DocumentIds.ToList(); // 创建副本避免迭代中修改集合

            foreach (var docId in docIds)
            {
                var document = GetDocument(docId);

                if (document == null)
                {
                    continue;
                }


                // 无论是否修改都保存
                try
                {
                    bool saveResult = document.Save();

                    if (!saveResult)
                    {
                        allSaved = false;
                    }
                    else
                    {
                    }
                }
                catch (Exception ex)
                {
                    allSaved = false;
                }
            }

            return allSaved;
        }

        /// <summary>
        /// 保存活动文档
        /// </summary>
        /// <param name="filePath">文件路径，为null时使用文档的现有路径</param>
        /// <returns>是否保存成功</returns>
        public bool SaveActiveDocument(string? filePath = null)
        {
            return _activeDocumentId != null && SaveDocument(_activeDocumentId, filePath);
        }

        #endregion

        #region 图像文档管理（预留接口）

        /// <summary>
        /// 创建图像文档（预留接口）
        /// </summary>
        /// <param name="imagePath">图像路径</param>
        /// <returns>文档ID</returns>
        public string? CreateImageDocument(string imagePath)
        {
            // TODO: 实现图像文档创建
            throw new NotImplementedException("图像文档功能暂未实现");
        }

        /// <summary>
        /// 创建预设文档（预留接口）
        /// </summary>
        /// <param name="presetPath">预设路径</param>
        /// <returns>文档ID</returns>
        public string? CreatePresetDocument(string presetPath)
        {
            // TODO: 实现预设文档创建
            throw new NotImplementedException("预设文档功能暂未实现");
        }

        #endregion
    }
}
