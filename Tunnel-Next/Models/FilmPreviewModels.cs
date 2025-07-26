using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media.Imaging;

namespace Tunnel_Next.Models
{
    /// <summary>
    /// 胶片预览项目
    /// </summary>
    public class FilmPreviewItem : INotifyPropertyChanged, IDisposable
    {
        private string _name = string.Empty;
        private string _filePath = string.Empty;
        private DateTime _createdTime = DateTime.Now;
        private DateTime _lastModified = DateTime.Now;
        private BitmapSource? _thumbnail;
        private bool _isSelected;
        private string _thumbnailPath = string.Empty;
        private bool _disposed = false;

        /// <summary>
        /// 显示名称
        /// </summary>
        public string Name
        {
            get => _name;
            set => SetProperty(ref _name, value);
        }

        /// <summary>
        /// 节点图文件路径
        /// </summary>
        public string FilePath
        {
            get => _filePath;
            set => SetProperty(ref _filePath, value);
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
        /// 最后修改时间
        /// </summary>
        public DateTime LastModified
        {
            get => _lastModified;
            set => SetProperty(ref _lastModified, value);
        }

        /// <summary>
        /// 缩略图
        /// </summary>
        public BitmapSource? Thumbnail
        {
            get => _thumbnail;
            set
            {
                if (_thumbnail != value)
                {
                    // 释放旧的缩略图资源（如果不是冻结的系统资源）
                    if (_thumbnail != null && _thumbnail.CanFreeze)
                    {
                        // BitmapSource通常是冻结的，不需要手动释放
                        // 但我们清除引用以帮助GC
                    }
                    SetProperty(ref _thumbnail, value);
                }
            }
        }

        /// <summary>
        /// 是否被选中
        /// </summary>
        public bool IsSelected
        {
            get => _isSelected;
            set => SetProperty(ref _isSelected, value);
        }

        /// <summary>
        /// 缩略图文件路径
        /// </summary>
        public string ThumbnailPath
        {
            get => _thumbnailPath;
            set => SetProperty(ref _thumbnailPath, value);
        }

        /// <summary>
        /// 缩略图是否存在
        /// </summary>
        public bool ThumbnailExists => !string.IsNullOrEmpty(ThumbnailPath) && System.IO.File.Exists(ThumbnailPath);

        /// <summary>
        /// 工具提示文本
        /// </summary>
        public string ToolTip => $"节点图: {Name}\n路径: {FilePath}\n修改时间: {LastModified:yyyy-MM-dd HH:mm:ss}";

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// 释放资源的具体实现
        /// </summary>
        /// <param name="disposing">是否正在释放托管资源</param>
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed && disposing)
            {
                // 清除缩略图引用
                _thumbnail = null;
                _disposed = true;
            }
        }

        /// <summary>
        /// 析构函数
        /// </summary>
        ~FilmPreviewItem()
        {
            Dispose(false);
        }
    }

    /// <summary>
    /// 资源库项目
    /// </summary>
    public class ResourceLibraryItem : INotifyPropertyChanged, IDisposable
    {
        private string _name = string.Empty;
        private string _filePath = string.Empty;
        private ResourceItemType _itemType = ResourceItemType.Image;
        private BitmapSource? _thumbnail;
        private bool _isExpanded;
        private bool _isSelected;
        private bool _disposed = false;

        /// <summary>
        /// 显示名称
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
        public ResourceItemType ItemType
        {
            get => _itemType;
            set => SetProperty(ref _itemType, value);
        }

        /// <summary>
        /// 缩略图
        /// </summary>
        public BitmapSource? Thumbnail
        {
            get => _thumbnail;
            set
            {
                if (_thumbnail != value)
                {
                    // 释放旧的缩略图资源（如果不是冻结的系统资源）
                    if (_thumbnail != null && _thumbnail.CanFreeze)
                    {
                        // BitmapSource通常是冻结的，不需要手动释放
                        // 但我们清除引用以帮助GC
                    }
                    SetProperty(ref _thumbnail, value);
                }
            }
        }

        /// <summary>
        /// 是否展开（用于文件夹）
        /// </summary>
        public bool IsExpanded
        {
            get => _isExpanded;
            set => SetProperty(ref _isExpanded, value);
        }

        /// <summary>
        /// 是否被选中
        /// </summary>
        public bool IsSelected
        {
            get => _isSelected;
            set => SetProperty(ref _isSelected, value);
        }

        /// <summary>
        /// 文件大小（字节）
        /// </summary>
        public long FileSize { get; set; }

        /// <summary>
        /// 文件修改时间
        /// </summary>
        public DateTime LastModified { get; set; }

        /// <summary>
        /// 工具提示文本
        /// </summary>
        public string ToolTip
        {
            get
            {
                if (ItemType == ResourceItemType.Folder)
                    return $"文件夹: {Name}";

                var sizeText = FileSize > 0 ? $"\n大小: {FormatFileSize(FileSize)}" : "";
                return $"{ItemType}: {Name}\n路径: {FilePath}{sizeText}\n修改时间: {LastModified:yyyy-MM-dd HH:mm:ss}";
            }
        }

        private static string FormatFileSize(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB" };
            double len = bytes;
            int order = 0;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len = len / 1024;
            }
            return $"{len:0.##} {sizes[order]}";
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// 释放资源的具体实现
        /// </summary>
        /// <param name="disposing">是否正在释放托管资源</param>
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed && disposing)
            {
                // 清除缩略图引用
                _thumbnail = null;
                _disposed = true;
            }
        }

        /// <summary>
        /// 析构函数
        /// </summary>
        ~ResourceLibraryItem()
        {
            Dispose(false);
        }
    }


}
