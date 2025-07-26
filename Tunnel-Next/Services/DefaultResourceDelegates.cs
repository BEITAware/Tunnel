using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Tunnel_Next.Models;

namespace Tunnel_Next.Services
{
    /// <summary>
    /// 默认资源操作委托实现
    /// </summary>
    public static class DefaultResourceDelegates
    {
        /// <summary>
        /// 默认导出委托实现
        /// </summary>
        public static async Task<ResourceOperationResult> DefaultExportDelegate(ResourceObject resource, string outputPath)
        {
            try
            {
                // 确保输出目录存在
                var outputDir = Path.GetDirectoryName(outputPath);
                if (!string.IsNullOrEmpty(outputDir))
                {
                    Directory.CreateDirectory(outputDir);
                }

                using var archive = ZipFile.Open(outputPath, ZipArchiveMode.Create);

                // 创建元数据
                var metadata = new
                {
                    ResourceType = resource.ResourceType.ToString(),
                    Name = resource.Name,
                    Description = resource.Description,
                    FileSize = resource.FileSize,
                    CreatedTime = resource.CreatedTime,
                    ModifiedTime = resource.ModifiedTime,
                    Metadata = resource.Metadata,
                    AssociatedFiles = resource.AssociatedFiles,
                    AssociatedFolders = resource.AssociatedFolders,
                    ExportTime = DateTime.Now,
                    Version = "1.0"
                };

                // 添加元数据文件
                var metadataEntry = archive.CreateEntry("metadata.json");
                using (var metadataStream = metadataEntry.Open())
                using (var writer = new StreamWriter(metadataStream))
                {
                    var json = JsonSerializer.Serialize(metadata, new JsonSerializerOptions { WriteIndented = true });
                    await writer.WriteAsync(json);
                }

                // 添加主文件
                if (File.Exists(resource.FilePath))
                {
                    var fileName = Path.GetFileName(resource.FilePath);
                    archive.CreateEntryFromFile(resource.FilePath, $"files/{fileName}");
                }

                // 添加关联文件
                foreach (var associatedFile in resource.AssociatedFiles)
                {
                    if (File.Exists(associatedFile))
                    {
                        var fileName = Path.GetFileName(associatedFile);
                        archive.CreateEntryFromFile(associatedFile, $"files/{fileName}");
                    }
                }

                // 添加缩略图（如果存在）
                if (!string.IsNullOrEmpty(resource.ThumbnailPath) && File.Exists(resource.ThumbnailPath))
                {
                    var thumbnailName = Path.GetFileName(resource.ThumbnailPath);
                    archive.CreateEntryFromFile(resource.ThumbnailPath, $"thumbnails/{thumbnailName}");
                }

                return new ResourceOperationResult
                {
                    Success = true,
                    OutputPath = outputPath
                };
            }
            catch (Exception ex)
            {
                return new ResourceOperationResult
                {
                    Success = false,
                    ErrorMessage = ex.Message
                };
            }
        }

        /// <summary>
        /// 默认导入委托实现
        /// </summary>
        public static async Task<ResourceOperationResult> DefaultImportDelegate(string zipPath, string targetDirectory)
        {
            try
            {
                if (!File.Exists(zipPath))
                {
                    return new ResourceOperationResult
                    {
                        Success = false,
                        ErrorMessage = "Zip文件不存在"
                    };
                }

                // 确保目标目录存在
                Directory.CreateDirectory(targetDirectory);

                using var archive = ZipFile.OpenRead(zipPath);

                // 读取元数据
                var metadataEntry = archive.GetEntry("metadata.json");
                if (metadataEntry == null)
                {
                    return new ResourceOperationResult
                    {
                        Success = false,
                        ErrorMessage = "Zip文件中缺少元数据"
                    };
                }

                string metadataJson;
                using (var metadataStream = metadataEntry.Open())
                using (var reader = new StreamReader(metadataStream))
                {
                    metadataJson = await reader.ReadToEndAsync();
                }

                var metadata = JsonSerializer.Deserialize<JsonElement>(metadataJson);

                // 提取所有文件
                foreach (var entry in archive.Entries)
                {
                    if (entry.FullName.StartsWith("files/"))
                    {
                        var fileName = Path.GetFileName(entry.FullName);
                        var targetPath = Path.Combine(targetDirectory, fileName);
                        entry.ExtractToFile(targetPath, overwrite: true);
                    }
                    else if (entry.FullName.StartsWith("thumbnails/"))
                    {
                        var thumbnailDir = Path.Combine(targetDirectory, "thumbnails");
                        Directory.CreateDirectory(thumbnailDir);
                        var fileName = Path.GetFileName(entry.FullName);
                        var targetPath = Path.Combine(thumbnailDir, fileName);
                        entry.ExtractToFile(targetPath, overwrite: true);
                    }
                }

                return new ResourceOperationResult
                {
                    Success = true,
                    OutputPath = targetDirectory,
                    AdditionalData = { ["Metadata"] = metadata }
                };
            }
            catch (Exception ex)
            {
                return new ResourceOperationResult
                {
                    Success = false,
                    ErrorMessage = ex.Message
                };
            }
        }

        /// <summary>
        /// 默认删除委托实现
        /// </summary>
        public static async Task<ResourceOperationResult> DefaultDeleteDelegate(ResourceObject resource)
        {
            try
            {
                var deletedFiles = new List<string>();

                // 删除主文件
                if (File.Exists(resource.FilePath))
                {
                    File.Delete(resource.FilePath);
                    deletedFiles.Add(resource.FilePath);
                }

                // 删除关联文件
                foreach (var associatedFile in resource.AssociatedFiles)
                {
                    if (File.Exists(associatedFile))
                    {
                        File.Delete(associatedFile);
                        deletedFiles.Add(associatedFile);
                    }
                }

                // 删除缩略图
                if (!string.IsNullOrEmpty(resource.ThumbnailPath) && File.Exists(resource.ThumbnailPath))
                {
                    File.Delete(resource.ThumbnailPath);
                    deletedFiles.Add(resource.ThumbnailPath);
                }

                // 删除关联文件夹（如果为空）
                foreach (var folder in resource.AssociatedFolders)
                {
                    if (Directory.Exists(folder) && !Directory.EnumerateFileSystemEntries(folder).Any())
                    {
                        Directory.Delete(folder);
                        deletedFiles.Add(folder);
                    }
                }

                return new ResourceOperationResult
                {
                    Success = true,
                    AdditionalData = { ["DeletedFiles"] = deletedFiles }
                };
            }
            catch (Exception ex)
            {
                return new ResourceOperationResult
                {
                    Success = false,
                    ErrorMessage = ex.Message
                };
            }
        }

        /// <summary>
        /// 默认重命名委托实现
        /// </summary>
        public static async Task<ResourceOperationResult> DefaultRenameDelegate(ResourceObject resource, string newName)
        {
            try
            {
                var renamedFiles = new Dictionary<string, string>();
                var oldName = resource.Name;

                // 重命名主文件
                if (File.Exists(resource.FilePath))
                {
                    var directory = Path.GetDirectoryName(resource.FilePath);
                    var extension = Path.GetExtension(resource.FilePath);
                    var newFilePath = Path.Combine(directory!, newName + extension);
                    
                    File.Move(resource.FilePath, newFilePath);
                    renamedFiles[resource.FilePath] = newFilePath;
                    resource.FilePath = newFilePath;
                }

                // 重命名关联文件
                for (int i = 0; i < resource.AssociatedFiles.Count; i++)
                {
                    var associatedFile = resource.AssociatedFiles[i];
                    if (File.Exists(associatedFile))
                    {
                        var fileName = Path.GetFileName(associatedFile);
                        if (fileName.Contains(oldName))
                        {
                            var directory = Path.GetDirectoryName(associatedFile);
                            var newFileName = fileName.Replace(oldName, newName);
                            var newFilePath = Path.Combine(directory!, newFileName);
                            
                            File.Move(associatedFile, newFilePath);
                            renamedFiles[associatedFile] = newFilePath;
                            resource.AssociatedFiles[i] = newFilePath;
                        }
                    }
                }

                // 重命名缩略图
                if (!string.IsNullOrEmpty(resource.ThumbnailPath) && File.Exists(resource.ThumbnailPath))
                {
                    var thumbnailName = Path.GetFileName(resource.ThumbnailPath);
                    if (thumbnailName.Contains(oldName))
                    {
                        var directory = Path.GetDirectoryName(resource.ThumbnailPath);
                        var newThumbnailName = thumbnailName.Replace(oldName, newName);
                        var newThumbnailPath = Path.Combine(directory!, newThumbnailName);
                        
                        File.Move(resource.ThumbnailPath, newThumbnailPath);
                        renamedFiles[resource.ThumbnailPath] = newThumbnailPath;
                        resource.ThumbnailPath = newThumbnailPath;
                    }
                }

                // 更新资源名称
                resource.Name = newName;

                return new ResourceOperationResult
                {
                    Success = true,
                    AdditionalData = { ["RenamedFiles"] = renamedFiles }
                };
            }
            catch (Exception ex)
            {
                return new ResourceOperationResult
                {
                    Success = false,
                    ErrorMessage = ex.Message
                };
            }
        }

        /// <summary>
        /// 验证资源完整性
        /// </summary>
        public static async Task<ResourceOperationResult> ValidateResourceDelegate(ResourceObject resource, Dictionary<string, object>? parameters = null)
        {
            try
            {
                var missingFiles = new List<string>();

                // 检查主文件
                if (!File.Exists(resource.FilePath))
                {
                    missingFiles.Add(resource.FilePath);
                }

                // 检查关联文件
                foreach (var file in resource.AssociatedFiles)
                {
                    if (!File.Exists(file))
                    {
                        missingFiles.Add(file);
                    }
                }

                // 检查关联文件夹
                foreach (var folder in resource.AssociatedFolders)
                {
                    if (!Directory.Exists(folder))
                    {
                        missingFiles.Add(folder);
                    }
                }

                if (missingFiles.Count > 0)
                {
                    return new ResourceOperationResult
                    {
                        Success = false,
                        ErrorMessage = $"发现 {missingFiles.Count} 个缺失的文件或文件夹",
                        AdditionalData = { ["MissingFiles"] = missingFiles }
                    };
                }

                return new ResourceOperationResult
                {
                    Success = true,
                    AdditionalData = { ["Message"] = "资源完整性验证通过" }
                };
            }
            catch (Exception ex)
            {
                return new ResourceOperationResult
                {
                    Success = false,
                    ErrorMessage = ex.Message
                };
            }
        }

        /// <summary>
        /// 备份资源
        /// </summary>
        public static async Task<ResourceOperationResult> BackupResourceDelegate(ResourceObject resource, Dictionary<string, object>? parameters = null)
        {
            try
            {
                var backupDir = Path.Combine(Path.GetDirectoryName(resource.FilePath)!, "Backup", DateTime.Now.ToString("yyyyMMdd_HHmmss"));
                Directory.CreateDirectory(backupDir);

                var backedUpFiles = new List<string>();

                // 备份主文件
                if (File.Exists(resource.FilePath))
                {
                    var fileName = Path.GetFileName(resource.FilePath);
                    var backupPath = Path.Combine(backupDir, fileName);
                    File.Copy(resource.FilePath, backupPath);
                    backedUpFiles.Add(backupPath);
                }

                // 备份关联文件
                foreach (var file in resource.AssociatedFiles)
                {
                    if (File.Exists(file))
                    {
                        var fileName = Path.GetFileName(file);
                        var backupPath = Path.Combine(backupDir, fileName);
                        File.Copy(file, backupPath);
                        backedUpFiles.Add(backupPath);
                    }
                }

                return new ResourceOperationResult
                {
                    Success = true,
                    OutputPath = backupDir,
                    AdditionalData = { ["BackedUpFiles"] = backedUpFiles }
                };
            }
            catch (Exception ex)
            {
                return new ResourceOperationResult
                {
                    Success = false,
                    ErrorMessage = ex.Message
                };
            }
        }

        /// <summary>
        /// 为资源类型设置默认委托
        /// </summary>
        public static void SetupDefaultDelegates(ResourceTypeDefinition typeDefinition)
        {
            typeDefinition.DelegateSet.ExportDelegate = DefaultExportDelegate;
            typeDefinition.DelegateSet.ImportDelegate = DefaultImportDelegate;
            typeDefinition.DelegateSet.DeleteDelegate = DefaultDeleteDelegate;
            typeDefinition.DelegateSet.RenameDelegate = DefaultRenameDelegate;

            // 添加扩展委托
            typeDefinition.DelegateSet.SetDelegate("Validate", (ResourceOperationDelegate)ValidateResourceDelegate);
            typeDefinition.DelegateSet.SetDelegate("Backup", (ResourceOperationDelegate)BackupResourceDelegate);

            // 为节点图类型设置专门的导出委托
            if (typeDefinition.Type == ResourceItemType.NodeGraph)
            {
                SetupNodeGraphDelegates(typeDefinition);
            }
        }

        /// <summary>
        /// 为节点图资源类型设置专门的委托
        /// </summary>
        public static void SetupNodeGraphDelegates(ResourceTypeDefinition typeDefinition)
        {
            // 这里暂时留空，等待依赖注入系统完善后再实现
            // 节点图导出委托需要NodeGraphExportService，而该服务需要其他依赖
            // 将在应用程序启动时通过依赖注入容器设置

            // 避免编译器警告
            _ = typeDefinition;
        }
    }
}
