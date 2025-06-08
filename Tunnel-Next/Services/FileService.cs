using Newtonsoft.Json;
using Tunnel_Next.Models;
using Microsoft.Win32;
using System.IO;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using OpenCvSharp;
using System.Collections.ObjectModel;
using Tunnel_Next.Services.Scripting;

namespace Tunnel_Next.Services
{
    /// <summary>
    /// 文件操作服务
    /// </summary>
    public class FileService
    {
        private const string NodeGraphFilter = "TunnelNX 节点图文件 (*.tnx)|*.tnx|所有文件 (*.*)|*.*";
        private const string ImageFilter = "图像文件 (*.jpg;*.jpeg;*.png;*.bmp;*.tiff)|*.jpg;*.jpeg;*.png;*.bmp;*.tiff|所有文件 (*.*)|*.*";

        private readonly WorkFolderService? _workFolderService;
        private NodeGraphSerializer? _serializer;
        private NodeGraphDeserializer? _deserializer;

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="workFolderService">工作文件夹服务（可选）</param>
        /// <param name="scriptManager">Revival Script管理器（可选）</param>
        public FileService(WorkFolderService? workFolderService = null, RevivalScriptManager? scriptManager = null)
        {
            _workFolderService = workFolderService;

            if (scriptManager != null)
            {
                _serializer = new NodeGraphSerializer(scriptManager);
                _deserializer = new NodeGraphDeserializer(scriptManager);
            }
        }

        /// <summary>
        /// 更新Revival Script管理器
        /// </summary>
        /// <param name="scriptManager">新的Revival Script管理器</param>
        public void UpdateScriptManager(RevivalScriptManager scriptManager)
        {
            if (scriptManager != null)
            {
                _serializer = new NodeGraphSerializer(scriptManager);
                _deserializer = new NodeGraphDeserializer(scriptManager);
            }
        }

        /// <summary>
        /// 保存节点图到文件
        /// </summary>
        /// <param name="nodeGraph">要保存的节点图</param>
        /// <param name="filePath">文件路径，如果为空则显示保存对话框</param>
        /// <returns>保存是否成功</returns>
        public async Task<bool> SaveNodeGraphAsync(NodeGraph nodeGraph, string? filePath = null)
        {
            try
            {

                if (_serializer == null)
                {
                    throw new InvalidOperationException("节点图序列化器未初始化，请确保传入了RevivalScriptManager");
                }


                // 确定保存路径
                if (string.IsNullOrEmpty(filePath))
                {
                    var initialDirectory = GetSafeInitialDirectory();

                    var saveDialog = new SaveFileDialog
                    {
                        Filter = NodeGraphFilter,
                        DefaultExt = "tnx",
                        AddExtension = true,
                        Title = "保存节点图",
                        InitialDirectory = initialDirectory,
                        FileName = nodeGraph.Name
                    };

                    if (saveDialog.ShowDialog() != true)
                    {
                        return false;
                    }

                    filePath = saveDialog.FileName;
                }

                // 确保目录存在
                var directory = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(directory))
                {
                    EnsureDirectoryExists(directory);
                }

                // 序列化节点图
                var json = _serializer.SerializeNodeGraph(nodeGraph);

                // 写入文件
                await File.WriteAllTextAsync(filePath, json);

                // 更新节点图信息
                nodeGraph.FilePath = filePath;
                nodeGraph.IsModified = false;
                nodeGraph.LastModified = DateTime.Now;

                return true;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"保存节点图失败: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// 从文件加载节点图
        /// </summary>
        /// <param name="filePath">文件路径，如果为空则显示打开对话框</param>
        /// <returns>加载的节点图，如果失败则返回null</returns>
        public async Task<NodeGraph?> LoadNodeGraphAsync(string? filePath = null)
        {
            try
            {
                if (_deserializer == null)
                    throw new InvalidOperationException("节点图反序列化器未初始化，请确保传入了RevivalScriptManager");

                // 确定加载路径
                if (string.IsNullOrEmpty(filePath))
                {
                    var openDialog = new OpenFileDialog
                    {
                        Filter = NodeGraphFilter,
                        Title = "打开节点图",
                        CheckFileExists = true,
                        InitialDirectory = GetSafeInitialDirectory()
                    };

                    if (openDialog.ShowDialog() != true)
                        return null;

                    filePath = openDialog.FileName;
                }

                if (!File.Exists(filePath))
                    throw new FileNotFoundException($"文件不存在: {filePath}");


                // 读取文件
                var json = await File.ReadAllTextAsync(filePath);

                // 反序列化节点图
                var nodeGraph = _deserializer.DeserializeNodeGraph(json);

                // 更新节点图信息
                nodeGraph.FilePath = filePath;
                nodeGraph.IsModified = false;
                nodeGraph.Name = Path.GetFileNameWithoutExtension(filePath);

                return nodeGraph;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"加载节点图失败: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// 选择图像文件
        /// </summary>
        /// <param name="title">对话框标题</param>
        /// <returns>选择的文件路径，如果取消则返回null</returns>
        public string? SelectImageFile(string title = "选择图像文件")
        {
            var openDialog = new OpenFileDialog
            {
                Filter = ImageFilter,
                Title = title,
                CheckFileExists = true
            };

            return openDialog.ShowDialog() == true ? openDialog.FileName : null;
        }

        /// <summary>
        /// 选择图像保存路径
        /// </summary>
        /// <param name="title">对话框标题</param>
        /// <param name="defaultFileName">默认文件名</param>
        /// <returns>选择的保存路径，如果取消则返回null</returns>
        public string? SelectImageSavePath(string title = "保存图像", string defaultFileName = "output.png")
        {
            var saveDialog = new SaveFileDialog
            {
                Filter = ImageFilter,
                Title = title,
                DefaultExt = "png",
                AddExtension = true,
                FileName = defaultFileName
            };

            return saveDialog.ShowDialog() == true ? saveDialog.FileName : null;
        }

        /// <summary>
        /// 导出节点图为图像
        /// </summary>
        /// <param name="nodeGraph">要导出的节点图</param>
        /// <param name="outputPath">输出路径</param>
        /// <returns>导出是否成功</returns>
        public async Task<bool> ExportNodeGraphAsImageAsync(NodeGraph nodeGraph, string? outputPath = null)
        {
            try
            {
                if (string.IsNullOrEmpty(outputPath))
                {
                    outputPath = SelectImageSavePath("导出节点图", $"{nodeGraph.Name}_export.png");
                    if (string.IsNullOrEmpty(outputPath))
                        return false;
                }

                // TODO: 实现节点图的图像导出功能
                // 这里需要渲染整个节点图为图像
                throw new NotImplementedException("节点图图像导出功能尚未实现");
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"导出节点图失败: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// 创建新的节点图
        /// </summary>
        /// <param name="name">节点图名称</param>
        /// <returns>新的节点图</returns>
        public NodeGraph CreateNewNodeGraph(string name = "新节点图")
        {
            return new NodeGraph
            {
                Name = name,
                FilePath = string.Empty,
                IsModified = false,
                LastModified = DateTime.Now,
                ZoomLevel = 1.0,
                ViewportX = 0,
                ViewportY = 0
            };
        }

        /// <summary>
        /// 检查文件是否存在
        /// </summary>
        /// <param name="filePath">文件路径</param>
        /// <returns>文件是否存在</returns>
        public bool FileExists(string filePath)
        {
            return !string.IsNullOrEmpty(filePath) && File.Exists(filePath);
        }

        /// <summary>
        /// 获取文件的最后修改时间
        /// </summary>
        /// <param name="filePath">文件路径</param>
        /// <returns>最后修改时间</returns>
        public DateTime GetLastWriteTime(string filePath)
        {
            return File.Exists(filePath) ? File.GetLastWriteTime(filePath) : DateTime.MinValue;
        }

        // 旧的连接恢复方法已移除，等待重构

        // 旧的节点图验证方法已移除，等待重构

        // 旧的序列化清理方法已移除，等待重构

        // 旧的反序列化方法已移除，等待重构

        // 旧的清理格式反序列化方法已移除，等待重构

        // 旧的ID生成和节点创建方法已移除，等待重构

        // 旧的端口重建方法已移除，等待重构

        // 旧的连接创建方法已移除，等待重构

        /// <summary>
        /// 获取默认的节点图文件夹路径
        /// </summary>
        /// <returns>节点图文件夹路径</returns>
        public string GetDefaultNodeGraphsFolder()
        {
            return _workFolderService?.NodeGraphsFolder ?? GetFallbackNodeGraphsFolder();
        }

        /// <summary>
        /// 获取安全的初始目录路径
        /// </summary>
        /// <returns>安全的初始目录路径</returns>
        private string GetSafeInitialDirectory()
        {
            try
            {
                var nodeGraphsFolder = _workFolderService?.NodeGraphsFolder;
                if (!string.IsNullOrEmpty(nodeGraphsFolder) && Directory.Exists(nodeGraphsFolder))
                {
                    return nodeGraphsFolder;
                }
            }
            catch (Exception ex)
            {
            }

            return GetFallbackNodeGraphsFolder();
        }

        /// <summary>
        /// 获取回退的节点图文件夹路径
        /// </summary>
        /// <returns>回退的节点图文件夹路径</returns>
        private string GetFallbackNodeGraphsFolder()
        {
            try
            {
                // 优先使用用户文档文件夹
                var documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                if (!string.IsNullOrEmpty(documentsPath) && Directory.Exists(documentsPath))
                {
                    return Path.Combine(documentsPath, "Tunnel");
                }
            }
            catch (Exception ex)
            {
            }

            try
            {
                // 回退到用户配置文件夹
                var userProfilePath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                if (!string.IsNullOrEmpty(userProfilePath) && Directory.Exists(userProfilePath))
                {
                    return Path.Combine(userProfilePath, "Documents", "Tunnel");
                }
            }
            catch (Exception ex)
            {
            }

            // 最终回退到当前目录
            return Environment.CurrentDirectory;
        }

        /// <summary>
        /// 确保目录存在，如果不存在则创建
        /// </summary>
        /// <param name="directoryPath">目录路径</param>
        private void EnsureDirectoryExists(string directoryPath)
        {
            try
            {
                if (!Directory.Exists(directoryPath))
                {
                    Directory.CreateDirectory(directoryPath);
                }
            }
            catch (Exception ex)
            {

                // 尝试使用临时文件夹作为备用
                try
                {
                    var tempPath = Path.Combine(Path.GetTempPath(), "Tunnel");
                    if (!Directory.Exists(tempPath))
                    {
                        Directory.CreateDirectory(tempPath);
                    }
                }
                catch (Exception tempEx)
                {
                    throw new InvalidOperationException($"无法创建目录: {ex.Message}", ex);
                }
            }
        }

        // 旧的公共序列化方法和强制更新方法已移除，等待重构

        // 旧的节点数据更新和连接验证方法已移除，等待重构

        // 移除 UpdateNodeGraphForParameterChanges 方法
        // 不再需要通过 FileService 处理参数变化
        // 改为使用单一链路：RevivalScriptBase.ParameterExternallyChanged → NodeEditorViewModel.HandleScriptParameterExternallyChanged → ProcessNodeGraphImmediately
    }
}
