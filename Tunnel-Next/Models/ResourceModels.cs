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
        public string ResourceTypeDisplayName
        {
            get
            {
                return ResourceType switch
                {
                    ResourceItemType.NodeGraph => "节点图",
                    ResourceItemType.Template => "模板",
                    ResourceItemType.Script => "脚本",
                    ResourceItemType.Image => "图像",
                    ResourceItemType.Preset => "预设",
                    ResourceItemType.Material => "素材",
                    ResourceItemType.Folder => "文件夹",
                    _ => "其他"
                };
            }
        }

        /// <summary>
        /// 获取资源类型的图标路径
        /// </summary>
        [JsonIgnore]
        public string ResourceTypeIconPath
        {
            get
            {
                return ResourceType switch
                {
                    ResourceItemType.NodeGraph => "../Resources/Nodegraph.png",
                    ResourceItemType.Template => "../Resources/Template.png",
                    ResourceItemType.Script => "../Resources/Script.png",
                    ResourceItemType.Image => "../Resources/RawImage.png",
                    ResourceItemType.Preset => "../Resources/Script.png", // 使用脚本图标作为预设
                    ResourceItemType.Material => "../Resources/RawImage.png", // 使用图像图标作为素材
                    ResourceItemType.Folder => "../Resources/WorkFolder.png",
                    _ => "../Resources/Script.png" // 默认使用脚本图标
                };
            }
        }

        /// <summary>
        /// 获取实际显示的图标路径（优先使用缩略图）
        /// </summary>
        [JsonIgnore]
        public string DisplayIconPath
        {
            get
            {
                // 对于模板，如果有缩略图则使用缩略图，否则使用默认图标
                if (ResourceType == ResourceItemType.Template && !string.IsNullOrEmpty(ThumbnailPath) && File.Exists(ThumbnailPath))
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
        /// 检查文件是否存在
        /// </summary>
        [JsonIgnore]
        public bool FileExists => !string.IsNullOrEmpty(FilePath) && File.Exists(FilePath);

        /// <summary>
        /// 获取文件扩展名
        /// </summary>
        [JsonIgnore]
        public string FileExtension => Path.GetExtension(FilePath).ToLowerInvariant();

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
