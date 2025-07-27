using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Tunnel_Next.Services
{
    /// <summary>
    /// 工作文件夹管理服务
    /// </summary>
    public class WorkFolderService
    {
        private readonly WorkFolderConfig _config;

        private string _workFolder = string.Empty;
        private string _nodeGraphsFolder = string.Empty;
        private string _thumbnailsFolder = string.Empty;
        private string _tempFolder = string.Empty;
        private string _userScriptsFolder = string.Empty;
        private string _userResourcesFolder = string.Empty;
        private string _resourcesFolder = string.Empty;

        /// <summary>
        /// 当前工作文件夹路径
        /// </summary>
        public string WorkFolder => _workFolder;

        /// <summary>
        /// 节点图文件夹路径
        /// </summary>
        public string NodeGraphsFolder => _nodeGraphsFolder;

        /// <summary>
        /// 缩略图文件夹路径
        /// </summary>
        public string ThumbnailsFolder => _thumbnailsFolder;

        /// <summary>
        /// 临时文件夹路径
        /// </summary>
        public string TempFolder => _tempFolder;



        /// <summary>
        /// 用户脚本文件夹路径（从配置文件读取）
        /// </summary>
        public string UserScriptsFolder => _userScriptsFolder;

        /// <summary>
        /// 用户脚本资源文件夹路径（脚本文件夹下的TunnelExtensionResources）
        /// </summary>
        public string UserResourcesFolder => _userResourcesFolder;

        /// <summary>
        /// 工作文件夹下的Resources文件夹路径
        /// </summary>
        public string ResourcesFolder => _resourcesFolder;

        /// <summary>
        /// 支持的图片文件扩展名
        /// </summary>
        public static readonly string[] SupportedImageExtensions = 
        {
            ".jpg", ".jpeg", ".png", ".tif", ".tiff", ".bmp", ".gif", ".webp"
        };

        /// <summary>
        /// 工作文件夹变化事件
        /// </summary>
        public event Action<string>? WorkFolderChanged;

        public WorkFolderService()
        {
            _config = new WorkFolderConfig();
        }

        /// <summary>
        /// 初始化工作文件夹服务
        /// </summary>
        public async Task InitializeAsync()
        {
            LoadFolderPaths();
            await EnsureDirectoryStructureAsync();
            await EnsureScriptsDirectoryStructureAsync();
        }

        /// <summary>
        /// 从配置文件加载文件夹路径
        /// </summary>
        private void LoadFolderPaths()
        {
            try
            {
                _workFolder = _config.WorkFolder;
                _userScriptsFolder = _config.ScriptsFolder;
                _userResourcesFolder = Path.Combine(_userScriptsFolder, "TunnelExtensionResources");

                // 基于工作文件夹设置其他路径
                _nodeGraphsFolder = Path.Combine(_workFolder, "Projects");
                _thumbnailsFolder = _nodeGraphsFolder;
                _tempFolder = Path.Combine(_nodeGraphsFolder, "temp");
                _resourcesFolder = Path.Combine(_workFolder, "Resources");

                // 添加调试输出
                System.Diagnostics.Debug.WriteLine($"[WorkFolderService] 工作文件夹: {_workFolder}");
                System.Diagnostics.Debug.WriteLine($"[WorkFolderService] 脚本文件夹: {_userScriptsFolder}");
                System.Diagnostics.Debug.WriteLine($"[WorkFolderService] 资源文件夹: {_userResourcesFolder}");
                System.Diagnostics.Debug.WriteLine($"[WorkFolderService] 项目文件夹: {_nodeGraphsFolder}");
                Console.WriteLine($"[WorkFolderService] 脚本文件夹路径: {_userScriptsFolder}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[WorkFolderService] 加载文件夹路径失败: {ex.Message}");
                throw;
            }
        }







        /// <summary>
        /// 验证工作文件夹路径是否有效
        /// </summary>
        /// <param name="folderPath">文件夹路径</param>
        /// <returns>是否有效</returns>
        private bool IsValidWorkFolder(string folderPath)
        {
            if (string.IsNullOrWhiteSpace(folderPath))
                return false;

            try
            {
                // 检查路径格式是否有效
                var fullPath = Path.GetFullPath(folderPath);

                // 检查父目录是否存在或可以创建
                var parentDir = Path.GetDirectoryName(fullPath);
                if (!string.IsNullOrEmpty(parentDir))
                {
                    return Directory.Exists(parentDir) || CanCreateDirectory(parentDir);
                }

                return false;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 检查是否可以创建目录
        /// </summary>
        /// <param name="directoryPath">目录路径</param>
        /// <returns>是否可以创建</returns>
        private bool CanCreateDirectory(string directoryPath)
        {
            try
            {
                if (Directory.Exists(directoryPath))
                    return true;

                // 旧的权限测试逻辑已移除，等待重构
                // 简单返回true，假设有权限
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 确保目录结构存在
        /// </summary>
        private async Task EnsureDirectoryStructureAsync()
        {
            var directoriesToCreate = new[]
            {
                (_workFolder, "工作文件夹"),
                (_nodeGraphsFolder, "节点图文件夹"),
                (_thumbnailsFolder, "缩略图文件夹"),
                (_tempFolder, "临时文件夹"),
                (_resourcesFolder, "资源文件夹")
            };

            foreach (var (dirPath, dirName) in directoriesToCreate)
            {
                try
                {
                    if (!Directory.Exists(dirPath))
                    {
                        Directory.CreateDirectory(dirPath);
                    }
                }
                catch (Exception ex)
                {

                    // 对于关键目录，如果创建失败，尝试使用备用路径
                    if (dirName == "工作文件夹" || dirName == "节点图文件夹")
                    {
                        await HandleCriticalDirectoryCreationFailure(dirPath, dirName);
                    }
                }
            }

        }

        /// <summary>
        /// 确保脚本目录结构存在
        /// </summary>
        private async Task EnsureScriptsDirectoryStructureAsync()
        {
            try
            {
                // 创建脚本目录结构
                Directory.CreateDirectory(_userScriptsFolder);
                Directory.CreateDirectory(_userResourcesFolder);

                // 创建初始化标记文件
                var initMarkerFile = Path.Combine(_userScriptsFolder, ".initialized");
                if (!File.Exists(initMarkerFile))
                {
                    // 这里不再创建示例脚本
                    await File.WriteAllTextAsync(initMarkerFile, DateTime.Now.ToString());
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[WorkFolderService] 创建脚本目录结构失败: {ex.Message}");
                // 不抛出异常，允许应用程序继续运行
            }
        }

        /// <summary>
        /// 处理关键目录创建失败的情况（仅使用用户文档文件夹）
        /// </summary>
        /// <param name="failedPath">失败的路径</param>
        /// <param name="dirName">目录名称</param>
        private async Task HandleCriticalDirectoryCreationFailure(string failedPath, string dirName)
        {
            // 不使用任何回退方案，直接抛出异常
            throw new InvalidOperationException($"无法创建{dirName}：{failedPath}。请确保用户文档文件夹可写。");
        }

        /// <summary>
        /// 设置新的工作文件夹
        /// </summary>
        /// <param name="newWorkFolder">新的工作文件夹路径</param>
        public async Task SetWorkFolderAsync(string newWorkFolder)
        {
            if (string.IsNullOrWhiteSpace(newWorkFolder))
                throw new ArgumentException("工作文件夹路径不能为空", nameof(newWorkFolder));

            try
            {
                _config.SetWorkFolder(newWorkFolder);
                LoadFolderPaths();

                await EnsureDirectoryStructureAsync();
                await EnsureScriptsDirectoryStructureAsync();

                WorkFolderChanged?.Invoke(_workFolder);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"设置工作文件夹失败: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// 设置新的脚本文件夹
        /// </summary>
        /// <param name="newScriptsFolder">新的脚本文件夹路径</param>
        public async Task SetScriptsFolderAsync(string newScriptsFolder)
        {
            if (string.IsNullOrWhiteSpace(newScriptsFolder))
                throw new ArgumentException("脚本文件夹路径不能为空", nameof(newScriptsFolder));

            try
            {
                _config.SetScriptsFolder(newScriptsFolder);
                LoadFolderPaths();

                await EnsureScriptsDirectoryStructureAsync();
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"设置脚本文件夹失败: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// 获取工作文件夹中的所有图片文件
        /// </summary>
        /// <returns>图片文件路径列表</returns>
        public List<string> GetImageFiles()
        {
            try
            {
                if (!Directory.Exists(_workFolder))
                    return new List<string>();

                var imageFiles = new List<string>();
                
                foreach (var extension in SupportedImageExtensions)
                {
                    var files = Directory.GetFiles(_workFolder, $"*{extension}", SearchOption.TopDirectoryOnly);
                    imageFiles.AddRange(files);
                }

                return imageFiles.OrderBy(f => Path.GetFileName(f)).ToList();
            }
            catch (Exception ex)
            {
                return new List<string>();
            }
        }

        /// <summary>
        /// 获取节点图文件夹中的所有节点图文件
        /// </summary>
        /// <returns>节点图文件路径列表</returns>
        public List<string> GetNodeGraphFiles()
        {
            try
            {
                if (!Directory.Exists(_nodeGraphsFolder))
                    return new List<string>();

                var nodeGraphFiles = new List<string>();

                // 遍历 Projects 目录下的每个项目文件夹，查找首个 .nodegraph 文件
                foreach (var dir in Directory.GetDirectories(_nodeGraphsFolder))
                {
                    var files = Directory.GetFiles(dir, "*.nodegraph", SearchOption.TopDirectoryOnly)
                        .Where(f => !Path.GetFileName(f).Contains("_auto_"));

                    // 默认每个项目仅有一个节点图，取第一个即可
                    var first = files.FirstOrDefault();
                    if (!string.IsNullOrEmpty(first))
                        nodeGraphFiles.Add(first);
                }

                return nodeGraphFiles
                    .OrderBy(f => File.GetLastWriteTime(f))
                    .Reverse()
                    .ToList();
            }
            catch (Exception)
            {
                return new List<string>();
            }
        }

        /// <summary>
        /// 复制外部图片文件到工作文件夹
        /// </summary>
        /// <param name="sourceFilePath">源文件路径</param>
        /// <returns>目标文件路径</returns>
        public async Task<string> CopyImageToWorkFolderAsync(string sourceFilePath)
        {
            if (string.IsNullOrWhiteSpace(sourceFilePath) || !File.Exists(sourceFilePath))
                throw new ArgumentException("源文件路径无效", nameof(sourceFilePath));

            try
            {
                var fileName = Path.GetFileName(sourceFilePath);
                var destPath = Path.Combine(_workFolder, fileName);

                // 检查文件是否已在工作文件夹中
                if (Path.GetFullPath(sourceFilePath).Equals(Path.GetFullPath(destPath), StringComparison.OrdinalIgnoreCase))
                {
                    return destPath; // 文件已在工作文件夹中
                }

                // 如果目标文件已存在，检查是否相同
                if (File.Exists(destPath))
                {
                    var srcInfo = new FileInfo(sourceFilePath);
                    var dstInfo = new FileInfo(destPath);
                    
                    if (srcInfo.Length == dstInfo.Length && 
                        Math.Abs((srcInfo.LastWriteTime - dstInfo.LastWriteTime).TotalSeconds) < 2)
                    {
                        return destPath;
                    }
                }

                // 复制文件
                File.Copy(sourceFilePath, destPath, true);
                
                return destPath;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"复制图片文件失败: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// 检查文件是否为支持的图片格式
        /// </summary>
        /// <param name="filePath">文件路径</param>
        /// <returns>是否为支持的图片格式</returns>
        public static bool IsSupportedImageFile(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                return false;

            var extension = Path.GetExtension(filePath).ToLowerInvariant();
            return SupportedImageExtensions.Contains(extension);
        }

        /// <summary>
        /// 清理孤立的缩略图文件
        /// </summary>
        public void CleanupOrphanedThumbnails()
        {
            try
            {
                if (!Directory.Exists(_thumbnailsFolder))
                    return;

                var nodeGraphFiles = GetNodeGraphFiles();
                var nodeGraphNames = nodeGraphFiles.Select(f => Path.GetFileNameWithoutExtension(f)).ToHashSet();

                var thumbnailFiles = Directory.GetFiles(_thumbnailsFolder, "*.png");
                
                foreach (var thumbnailFile in thumbnailFiles)
                {
                    var thumbnailName = Path.GetFileNameWithoutExtension(thumbnailFile);
                    if (!nodeGraphNames.Contains(thumbnailName))
                    {
                        File.Delete(thumbnailFile);
                    }
                }
            }
            catch (Exception ex)
            {
            }
        }
    }
}
