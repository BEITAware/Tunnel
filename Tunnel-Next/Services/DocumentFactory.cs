using System;
using System.IO;
using System.Threading.Tasks;
using Tunnel_Next.Models;
using Tunnel_Next.ViewModels;
using Tunnel_Next.Services.Scripting;

namespace Tunnel_Next.Services
{
    /// <summary>
    /// 文档工厂 - 负责创建各种类型的文档
    /// </summary>
    public class DocumentFactory
    {
        private readonly FileService _fileService;
        private readonly RevivalScriptManager? _revivalScriptManager;

        public DocumentFactory(FileService fileService, RevivalScriptManager? revivalScriptManager)
        {
            _fileService = fileService ?? throw new ArgumentNullException(nameof(fileService));
            _revivalScriptManager = revivalScriptManager; // Revival Scripts管理器
        }

        /// <summary>
        /// 创建新的节点图文档
        /// </summary>
        /// <param name="name">文档名称，为null时使用默认名称</param>
        /// <returns>创建的节点图文档</returns>
        public IDocumentContent CreateNodeGraphDocument(string? name = null)
        {
            try
            {
                // 创建新节点图
                var nodeGraph = _fileService.CreateNewNodeGraph();
                nodeGraph.Name = name ?? "新节点图";

                // 创建节点编辑器实例（传入RevivalScriptManager）
                var nodeEditor = new NodeEditorViewModel(_revivalScriptManager);

                // 创建文档实例
                var document = new NodeGraphDocument(nodeGraph, nodeEditor, _fileService);

                return document;
            }
            catch (Exception ex)
            {
                throw new Exception($"创建新节点图文档失败: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// 从文件加载节点图文档
        /// </summary>
        /// <param name="filePath">文件路径</param>
        /// <returns>加载的节点图文档</returns>
        public async Task<IDocumentContent> LoadNodeGraphDocumentAsync(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
                throw new ArgumentException("文件路径不能为空", nameof(filePath));

            if (!File.Exists(filePath))
                throw new FileNotFoundException("找不到节点图文件", filePath);

            try
            {
                // 使用FileService加载节点图
                var loadedNodeGraph = await _fileService.LoadNodeGraphAsync(filePath);
                if (loadedNodeGraph == null)
                    throw new InvalidOperationException("加载节点图失败");

                // 创建节点编辑器实例（传入RevivalScriptManager）
                var nodeEditor = new NodeEditorViewModel(_revivalScriptManager);

                // 加载节点图到编辑器
                await nodeEditor.LoadNodeGraphAsync(loadedNodeGraph);

                // 创建文档
                var document = new NodeGraphDocument(loadedNodeGraph, nodeEditor, _fileService);

                return document;
            }
            catch (Exception ex)
            {
                throw new Exception($"加载节点图文档失败: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// 创建图像文档（预留接口）
        /// </summary>
        /// <param name="imagePath">图像路径</param>
        /// <returns>图像文档</returns>
        public IDocumentContent CreateImageDocument(string imagePath)
        {
            // TODO: 实现图像文档创建
            throw new NotImplementedException("图像文档功能暂未实现");
        }

        /// <summary>
        /// 创建预设文档（预留接口）
        /// </summary>
        /// <param name="presetPath">预设路径</param>
        /// <returns>预设文档</returns>
        public IDocumentContent CreatePresetDocument(string presetPath)
        {
            // TODO: 实现预设文档创建
            throw new NotImplementedException("预设文档功能暂未实现");
        }
    }
}