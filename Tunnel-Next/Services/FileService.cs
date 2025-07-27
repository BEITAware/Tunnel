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
        private const string NodeGraphFilter = "TunnelNX 节点图文件 (*.nodegraph)|*.nodegraph|所有文件 (*.*)|*.*";
        private const string ImageFilter = "图像文件 (*.jpg;*.jpeg;*.png;*.bmp;*.tiff)|*.jpg;*.jpeg;*.png;*.bmp;*.tiff|所有文件 (*.*)|*.*";

        private readonly WorkFolderService? _workFolderService;
        private NodeGraphSerializer? _serializer;
        private NodeGraphDeserializer? _deserializer;

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="workFolderService">工作文件夹服务（可选）</param>
        /// <param name="scriptManager">TunnelExtension Script管理器（可选）</param>
        public FileService(WorkFolderService? workFolderService = null, TunnelExtensionScriptManager? scriptManager = null)
        {
            _workFolderService = workFolderService;

            if (scriptManager != null)
            {
                _serializer = new NodeGraphSerializer(scriptManager);
                _deserializer = new NodeGraphDeserializer(scriptManager);
            }
        }

        /// <summary>
        /// 更新TunnelExtension Script管理器
        /// </summary>
        /// <param name="scriptManager">新的TunnelExtension Script管理器</param>
        public void UpdateScriptManager(TunnelExtensionScriptManager scriptManager)
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
                    throw new InvalidOperationException("节点图序列化器未初始化，请确保传入了TunnelExtensionScriptManager");
                }

                // 确定保存路径
                if (string.IsNullOrEmpty(filePath))
                {
                    var initialDirectory = GetSafeInitialDirectory();

                    var saveDialog = new SaveFileDialog
                    {
                        Filter = NodeGraphFilter,
                        DefaultExt = "nodegraph",
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

                // -------- 项目文件夹自动创建逻辑 --------
                var projectsRoot = _workFolderService?.NodeGraphsFolder;
                if (!string.IsNullOrEmpty(projectsRoot) && !string.IsNullOrEmpty(filePath))
                {
                    var parentDir = Path.GetDirectoryName(filePath) ?? string.Empty;

                    // 当选择路径位于 Projects 根目录，或位于其他非项目目录，但文件名未包含目录分隔符
                    if (string.Equals(Path.GetFullPath(parentDir), Path.GetFullPath(projectsRoot), StringComparison.OrdinalIgnoreCase))
                    {
                        var projectName = Path.GetFileNameWithoutExtension(filePath);
                        var projectFolder = Path.Combine(projectsRoot, projectName);
                        if (!Directory.Exists(projectFolder))
                        {
                            Directory.CreateDirectory(projectFolder);
                        }

                        // 更新文件路径到项目文件夹内
                        filePath = Path.Combine(projectFolder, $"{projectName}.nodegraph");

                        // 创建项目元数据文件
                        var metadataPath = Path.Combine(projectFolder, $"{projectName}.TNXProject");
                        if (!File.Exists(metadataPath))
                        {
                            File.WriteAllText(metadataPath, "{}");
                        }

                        // 创建版本目录
                        var versionsDir = Path.Combine(projectFolder, "versions");
                        if (!Directory.Exists(versionsDir))
                        {
                            Directory.CreateDirectory(versionsDir);
                        }
                    }
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
                    throw new InvalidOperationException("节点图反序列化器未初始化，请确保传入了TunnelExtensionScriptManager");

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
        /// 选择多个图像文件
        /// </summary>
        /// <param name="title">对话框标题</param>
        /// <returns>选择的文件路径数组，如果取消则返回空数组</returns>
        public string[] SelectMultipleImageFiles(string title = "选择图像文件")
        {
            var openDialog = new OpenFileDialog
            {
                Filter = ImageFilter,
                Title = title,
                CheckFileExists = true,
                Multiselect = true
            };

            return openDialog.ShowDialog() == true ? openDialog.FileNames : new string[0];
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

            // 安全地设置Owner并显示对话框
            var mainWindow = System.Windows.Application.Current?.MainWindow;
            bool? result = mainWindow != null ? saveDialog.ShowDialog(mainWindow) : saveDialog.ShowDialog();
            return result == true ? saveDialog.FileName : null;
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
        /// 获取回退的节点图文件夹路径（使用配置系统）
        /// </summary>
        /// <returns>回退的节点图文件夹路径</returns>
        private string GetFallbackNodeGraphsFolder()
        {
            try
            {
                var config = new WorkFolderConfig();
                return Path.Combine(config.WorkFolder, "Projects");
            }
            catch
            {
                // 如果配置系统失败，使用硬编码的回退路径
                var documentsPath = GetSafeDocumentsPath();
                return Path.Combine(documentsPath, "TNX", "Projects");
            }
        }

        /// <summary>
        /// 获取安全的用户文档路径，包含回退机制
        /// </summary>
        /// <returns>安全的用户文档路径</returns>
        private string GetSafeDocumentsPath()
        {
            try
            {
                var documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

                // 验证路径是否有效
                if (!string.IsNullOrWhiteSpace(documentsPath) && Directory.Exists(documentsPath))
                {
                    return documentsPath;
                }
            }
            catch (Exception ex)
            {
                // 记录错误但继续使用回退方案
                System.Diagnostics.Debug.WriteLine($"[FileService] 获取用户文档路径失败: {ex.Message}");
            }

            // 回退方案：使用用户配置文件路径
            try
            {
                var userProfilePath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                if (!string.IsNullOrWhiteSpace(userProfilePath) && Directory.Exists(userProfilePath))
                {
                    var documentsInProfile = Path.Combine(userProfilePath, "Documents");
                    if (Directory.Exists(documentsInProfile))
                    {
                        return documentsInProfile;
                    }
                    return userProfilePath;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[FileService] 获取用户配置文件路径失败: {ex.Message}");
            }

            // 最终回退方案：使用应用程序数据路径
            try
            {
                var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                if (!string.IsNullOrWhiteSpace(appDataPath))
                {
                    return appDataPath;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[FileService] 获取应用程序数据路径失败: {ex.Message}");
            }

            // 绝对最终回退：使用临时目录
            var tempPath = Path.GetTempPath();
            System.Diagnostics.Debug.WriteLine($"[FileService] 使用临时目录作为最终回退: {tempPath}");
            return tempPath;
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
        // 改为使用单一链路：TunnelExtensionScriptBase.ParameterExternallyChanged → NodeEditorViewModel.HandleScriptParameterExternallyChanged → ProcessNodeGraphImmediately
    }
}
