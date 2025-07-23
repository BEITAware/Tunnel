using System;
using System.Windows.Media.Imaging;
using System.ComponentModel;

namespace Tunnel_Next.Models
{
    /// <summary>
    /// 批量处理节点图项目模型类
    /// </summary>
    public class BatchProcessNodeGraphItem : INotifyPropertyChanged
    {
        private string _name;
        private string _filePath;
        private DateTime _creationTime;
        private DateTime _lastModified;
        private bool _isSelected;
        private BitmapImage _thumbnail;
        private double _processingProgress;
        private string _processingStatus;
        private bool _isProcessingComplete;
        private string _outputFilePath;

        /// <summary>
        /// 节点图名称
        /// </summary>
        public string Name
        {
            get => _name;
            set
            {
                if (_name != value)
                {
                    _name = value;
                    OnPropertyChanged(nameof(Name));
                }
            }
        }

        /// <summary>
        /// 节点图文件路径
        /// </summary>
        public string FilePath
        {
            get => _filePath;
            set
            {
                if (_filePath != value)
                {
                    _filePath = value;
                    OnPropertyChanged(nameof(FilePath));
                }
            }
        }

        /// <summary>
        /// 创建时间
        /// </summary>
        public DateTime CreationTime
        {
            get => _creationTime;
            set
            {
                if (_creationTime != value)
                {
                    _creationTime = value;
                    OnPropertyChanged(nameof(CreationTime));
                }
            }
        }

        /// <summary>
        /// 最后修改时间
        /// </summary>
        public DateTime LastModified
        {
            get => _lastModified;
            set
            {
                if (_lastModified != value)
                {
                    _lastModified = value;
                    OnPropertyChanged(nameof(LastModified));
                }
            }
        }

        /// <summary>
        /// 是否选中
        /// </summary>
        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected != value)
                {
                    _isSelected = value;
                    OnPropertyChanged(nameof(IsSelected));
                }
            }
        }

        /// <summary>
        /// 缩略图
        /// </summary>
        public BitmapImage Thumbnail
        {
            get => _thumbnail;
            set
            {
                if (_thumbnail != value)
                {
                    _thumbnail = value;
                    OnPropertyChanged(nameof(Thumbnail));
                }
            }
        }

        /// <summary>
        /// 处理进度 (0-100)
        /// </summary>
        public double ProcessingProgress
        {
            get => _processingProgress;
            set
            {
                if (_processingProgress != value)
                {
                    _processingProgress = value;
                    OnPropertyChanged(nameof(ProcessingProgress));
                }
            }
        }

        /// <summary>
        /// 处理状态
        /// </summary>
        public string ProcessingStatus
        {
            get => _processingStatus;
            set
            {
                if (_processingStatus != value)
                {
                    _processingStatus = value;
                    OnPropertyChanged(nameof(ProcessingStatus));
                }
            }
        }

        /// <summary>
        /// 处理是否完成
        /// </summary>
        public bool IsProcessingComplete
        {
            get => _isProcessingComplete;
            set
            {
                if (_isProcessingComplete != value)
                {
                    _isProcessingComplete = value;
                    OnPropertyChanged(nameof(IsProcessingComplete));
                }
            }
        }

        /// <summary>
        /// 输出文件路径
        /// </summary>
        public string OutputFilePath
        {
            get => _outputFilePath;
            set
            {
                if (_outputFilePath != value)
                {
                    _outputFilePath = value;
                    OnPropertyChanged(nameof(OutputFilePath));
                }
            }
        }

        /// <summary>
        /// 属性变更事件
        /// </summary>
        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// 触发属性变更事件
        /// </summary>
        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
} 