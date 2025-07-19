using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media.Imaging;

namespace Tunnel_Next.Models
{
    /// <summary>
    /// 胶片预览项目
    /// </summary>
    public class FilmPreviewItem : INotifyPropertyChanged
    {
        private string _name = string.Empty;
        private string _filePath = string.Empty;
        private DateTime _createdTime = DateTime.Now;
        private DateTime _lastModified = DateTime.Now;
        private BitmapSource? _thumbnail;
        private bool _isSelected;

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
            set => SetProperty(ref _thumbnail, value);
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
    }

    /// <summary>
    /// 资源库项目
    /// </summary>
    public class ResourceLibraryItem : INotifyPropertyChanged
    {
        private string _name = string.Empty;
        private string _filePath = string.Empty;
        private ResourceItemType _itemType = ResourceItemType.Image;
        private BitmapSource? _thumbnail;
        private bool _isExpanded;
        private bool _isSelected;

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
            set => SetProperty(ref _thumbnail, value);
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
    }


}
