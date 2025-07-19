using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;
using System.Windows.Media.Imaging;

namespace Tunnel_Next.Models
{
    /// <summary>
    /// 统一的资源对象模型
    /// </summary>
    public class ResourceObject : INotifyPropertyChanged
    {
        private string _id = Guid.NewGuid().ToString();
        private string _name = string.Empty;
        private string _filePath = string.Empty;
        private ResourceItemType _resourceType = ResourceItemType.Other;
        private DateTime _createdTime = DateTime.Now;
        private DateTime _modifiedTime = DateTime.Now;
        private string _thumbnailPath = string.Empty;
        private long _fileSize = 0;
        private string _description = string.Empty;
        private Dictionary<string, object> _metadata = new();
        private BitmapSource? _thumbnail;
        private List<string> _associatedFiles = new();
        private List<string> _associatedFolders = new();

        /// <summary>
        /// 资源唯一标识符
        /// </summary>
        public string Id
        {
            get => _id;
            set => SetProperty(ref _id, value);
        }

        /// <summary>
        /// 资源名称
        /// </summary>
        public string Name
        {
            get => _name;
            set => SetProperty(ref _name, value);
        }

        /// <summary>
        /// 文件路径
        /// </summary>
        public string FilePath
        {
            get => _filePath;
            set => SetProperty(ref _filePath, value);
        }

        /// <summary>
        /// 资源类型
        /// </summary>
        public ResourceItemType ResourceType
        {
            get => _resourceType;
            set => SetProperty(ref _resourceType, value);
        }

        /// <summary>
        /// 创建时间
        /// </summary>
        public DateTime CreatedTime
        {
            get => _createdTime;
            set => SetProperty(ref _createdTime, value);
        }

        /// <summary>
        /// 修改时间
        /// </summary>
        public DateTime ModifiedTime
        {
            get => _modifiedTime;
            set => SetProperty(ref _modifiedTime, value);
        }

        /// <summary>
        /// 缩略图路径
        /// </summary>
        public string ThumbnailPath
        {
            get => _thumbnailPath;
            set => SetProperty(ref _thumbnailPath, value);
        }

        /// <summary>
        /// 文件大小（字节）
        /// </summary>
        public long FileSize
        {
            get => _fileSize;
            set => SetProperty(ref _fileSize, value);
        }

        /// <summary>
        /// 资源描述
        /// </summary>
        public string Description
        {
            get => _description;
            set => SetProperty(ref _description, value);
        }

        /// <summary>
        /// 元数据字典
        /// </summary>
        public Dictionary<string, object> Metadata
        {
            get => _metadata;
            set => SetProperty(ref _metadata, value);
        }

        /// <summary>
        /// 缩略图（不序列化）
        /// </summary>
        [JsonIgnore]
        public BitmapSource? Thumbnail
        {
            get => _thumbnail;
            set => SetProperty(ref _thumbnail, value);
        }

        /// <summary>
        /// 关联的文件路径列表（包含所有相关文件）
        /// </summary>
        public List<string> AssociatedFiles
        {
            get => _associatedFiles;
            set => SetProperty(ref _associatedFiles, value);
        }

        /// <summary>
        /// 关联的文件夹路径列表（如模板文件夹、脚本资源文件夹等）
        /// </summary>
        public List<string> AssociatedFolders
        {
            get => _associatedFolders;
            set => SetProperty(ref _associatedFolders, value);
        }

        /// <summary>
        /// 获取格式化的文件大小
        /// </summary>
        [JsonIgnore]
        public string FormattedFileSize
        {
            get
            {
                if (FileSize < 1024) return $"{FileSize} B";
                if (FileSize < 1024 * 1024) return $"{FileSize / 1024.0:F1} KB";
                if (FileSize < 1024 * 1024 * 1024) return $"{FileSize / (1024.0 * 1024.0):F1} MB";
                return $"{FileSize / (1024.0 * 1024.0 * 1024.0):F1} GB";
            }
        }

        /// <summary>
        /// 获取资源类型的显示名称
        /// </summary>
        [JsonIgnore]
        public string ResourceTypeDisplayName => ResourceTypeRegistry.GetDisplayName(ResourceType);

        /// <summary>
        /// 获取资源类型的图标路径
        /// </summary>
        [JsonIgnore]
        public string ResourceTypeIconPath => ResourceTypeRegistry.GetDefaultIconPath(ResourceType);

        /// <summary>
        /// 获取实际显示的图标路径（优先使用缩略图）
        /// </summary>
        [JsonIgnore]
        public string DisplayIconPath
        {
            get
            {
                // 如果资源类型支持缩略图且有缩略图文件，则使用缩略图
                if (ResourceTypeRegistry.SupportsThumbnail(ResourceType) &&
                    !string.IsNullOrEmpty(ThumbnailPath) &&
                    File.Exists(ThumbnailPath))
                {
                    return ThumbnailPath;
                }

                // 对于Cube3DLut类型，使用专用图标
                if (Name.EndsWith(".cube", StringComparison.OrdinalIgnoreCase) ||
                    Description.Contains("Cube3DLut", StringComparison.OrdinalIgnoreCase))
                {
                    return "../Resources/CubeLut.png";
                }

                // 其他情况使用类型默认图标
                return ResourceTypeIconPath;
            }
        }

        /// <summary>
        /// 检查主文件是否存在
        /// </summary>
        [JsonIgnore]
        public bool FileExists => !string.IsNullOrEmpty(FilePath) && File.Exists(FilePath);

        /// <summary>
        /// 获取文件扩展名
        /// </summary>
        [JsonIgnore]
        public string FileExtension => Path.GetExtension(FilePath).ToLowerInvariant();

        /// <summary>
        /// 获取所有文件路径（主文件 + 关联文件）
        /// </summary>
        [JsonIgnore]
        public IEnumerable<string> AllFiles
        {
            get
            {
                if (!string.IsNullOrEmpty(FilePath))
                    yield return FilePath;

                foreach (var file in AssociatedFiles)
                {
                    if (!string.IsNullOrEmpty(file))
                        yield return file;
                }
            }
        }

        /// <summary>
        /// 检查所有关联文件是否存在
        /// </summary>
        [JsonIgnore]
        public bool AllFilesExist => AllFiles.All(File.Exists);

        /// <summary>
        /// 检查所有关联文件夹是否存在
        /// </summary>
        [JsonIgnore]
        public bool AllFoldersExist => AssociatedFolders.All(Directory.Exists);

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        /// <summary>
        /// 从文件信息创建资源对象
        /// </summary>
        public static ResourceObject FromFileInfo(FileInfo fileInfo, ResourceItemType resourceType)
        {
            return new ResourceObject
            {
                Name = Path.GetFileNameWithoutExtension(fileInfo.Name),
                FilePath = fileInfo.FullName,
                ResourceType = resourceType,
                CreatedTime = fileInfo.CreationTime,
                ModifiedTime = fileInfo.LastWriteTime,
                FileSize = fileInfo.Length
            };
        }

        /// <summary>
        /// 更新文件信息
        /// </summary>
        public void UpdateFromFileInfo()
        {
            if (!FileExists) return;

            var fileInfo = new FileInfo(FilePath);
            ModifiedTime = fileInfo.LastWriteTime;
            FileSize = fileInfo.Length;
        }

        /// <summary>
        /// 添加关联文件
        /// </summary>
        public void AddAssociatedFile(string filePath)
        {
            if (!string.IsNullOrEmpty(filePath) && !AssociatedFiles.Contains(filePath))
            {
                AssociatedFiles.Add(filePath);
            }
        }

        /// <summary>
        /// 添加关联文件夹
        /// </summary>
        public void AddAssociatedFolder(string folderPath)
        {
            if (!string.IsNullOrEmpty(folderPath) && !AssociatedFolders.Contains(folderPath))
            {
                AssociatedFolders.Add(folderPath);
            }
        }

        /// <summary>
        /// 移除关联文件
        /// </summary>
        public bool RemoveAssociatedFile(string filePath)
        {
            return AssociatedFiles.Remove(filePath);
        }

        /// <summary>
        /// 移除关联文件夹
        /// </summary>
        public bool RemoveAssociatedFolder(string folderPath)
        {
            return AssociatedFolders.Remove(folderPath);
        }

        /// <summary>
        /// 清理不存在的关联文件和文件夹
        /// </summary>
        public void CleanupMissingAssociations()
        {
            AssociatedFiles.RemoveAll(file => !File.Exists(file));
            AssociatedFolders.RemoveAll(folder => !Directory.Exists(folder));
        }
    }

    /// <summary>
    /// 资源目录
    /// </summary>
    public class ResourceCatalog
    {
        /// <summary>
        /// 目录版本
        /// </summary>
        public string Version { get; set; } = "1.0";

        /// <summary>
        /// 最后更新时间
        /// </summary>
        public DateTime LastUpdated { get; set; } = DateTime.Now;

        /// <summary>
        /// 资源对象列表
        /// </summary>
        public List<ResourceObject> Resources { get; set; } = new();

        /// <summary>
        /// 目录统计信息
        /// </summary>
        public Dictionary<ResourceItemType, int> Statistics { get; set; } = new();

        /// <summary>
        /// 更新统计信息
        /// </summary>
        public void UpdateStatistics()
        {
            Statistics.Clear();
            foreach (var resource in Resources)
            {
                if (Statistics.ContainsKey(resource.ResourceType))
                    Statistics[resource.ResourceType]++;
                else
                    Statistics[resource.ResourceType] = 1;
            }
        }
    }
}
