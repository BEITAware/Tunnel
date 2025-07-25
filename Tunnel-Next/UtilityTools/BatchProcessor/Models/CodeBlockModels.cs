using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Tunnel_Next.UtilityTools.BatchProcessor.Models
{
    /// <summary>
    /// 代码块类型枚举
    /// </summary>
    public enum CodeBlockType
    {
        // 填充类型
        CollectionLiteral,      // [] 集合字面量
        MultiFunctionLiteral,   // <> 多功能字面量
        
        // 积木类型
        NodeGraphSequence,      // [节点图序列]
        FileSequence,          // [文件序列]
        NumberSequence,        // [序号列]
        NodeScript,            // <节点脚本>
        ProcessBlock,          // >处理<><
        LoopBlock              // 对于每个[]中的<>{}
    }

    /// <summary>
    /// 插槽类型枚举
    /// </summary>
    public enum SlotType
    {
        Collection,     // [] 集合插槽
        MultiFunction,  // <> 多功能插槽
        Logic          // {} 逻辑插槽
    }

    /// <summary>
    /// 代码块插槽
    /// </summary>
    public class CodeBlockSlot : INotifyPropertyChanged
    {
        private CodeBlock? _content;
        private bool _isHighlighted;

        /// <summary>
        /// 插槽类型
        /// </summary>
        public SlotType Type { get; set; }

        /// <summary>
        /// 插槽名称
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// 插槽内容
        /// </summary>
        public CodeBlock? Content
        {
            get => _content;
            set
            {
                if (_content != value)
                {
                    _content = value;
                    OnPropertyChanged(nameof(Content));
                    OnPropertyChanged(nameof(IsEmpty));
                }
            }
        }

        /// <summary>
        /// 是否为空
        /// </summary>
        public bool IsEmpty => _content == null;

        /// <summary>
        /// 是否高亮显示（拖拽时）
        /// </summary>
        public bool IsHighlighted
        {
            get => _isHighlighted;
            set
            {
                if (_isHighlighted != value)
                {
                    _isHighlighted = value;
                    OnPropertyChanged(nameof(IsHighlighted));
                }
            }
        }

        /// <summary>
        /// 检查是否可以接受指定的代码块
        /// </summary>
        public bool CanAccept(CodeBlock codeBlock)
        {
            return Type switch
            {
                SlotType.Collection => codeBlock.Type == CodeBlockType.CollectionLiteral ||
                                     codeBlock.Type == CodeBlockType.NodeGraphSequence ||
                                     codeBlock.Type == CodeBlockType.FileSequence ||
                                     codeBlock.Type == CodeBlockType.NumberSequence,
                SlotType.MultiFunction => codeBlock.Type == CodeBlockType.MultiFunctionLiteral ||
                                        codeBlock.Type == CodeBlockType.NodeScript,
                SlotType.Logic => true, // 逻辑插槽可以接受任何代码块
                _ => false
            };
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    /// <summary>
    /// 代码块基类
    /// </summary>
    public class CodeBlock : INotifyPropertyChanged
    {
        private Point _position;
        private bool _isSelected;
        private bool _isDragging;

        /// <summary>
        /// 唯一标识符
        /// </summary>
        public Guid Id { get; set; } = Guid.NewGuid();

        /// <summary>
        /// 代码块类型
        /// </summary>
        public CodeBlockType Type { get; set; }

        /// <summary>
        /// 显示文本
        /// </summary>
        public string DisplayText { get; set; } = string.Empty;

        /// <summary>
        /// 代码块颜色
        /// </summary>
        public Brush Color { get; set; } = Brushes.Gray;

        /// <summary>
        /// 位置
        /// </summary>
        public Point Position
        {
            get => _position;
            set
            {
                if (_position != value)
                {
                    _position = value;
                    OnPropertyChanged(nameof(Position));
                }
            }
        }

        /// <summary>
        /// 是否被选中
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
        /// 是否正在拖拽
        /// </summary>
        public bool IsDragging
        {
            get => _isDragging;
            set
            {
                if (_isDragging != value)
                {
                    _isDragging = value;
                    OnPropertyChanged(nameof(IsDragging));
                }
            }
        }

        /// <summary>
        /// 插槽集合
        /// </summary>
        public ObservableCollection<CodeBlockSlot> Slots { get; } = new();

        /// <summary>
        /// 父代码块
        /// </summary>
        public CodeBlock? Parent { get; set; }

        /// <summary>
        /// 是否可以被嵌入到其他代码块中
        /// </summary>
        public bool CanBeEmbedded => Type switch
        {
            CodeBlockType.CollectionLiteral => true,
            CodeBlockType.MultiFunctionLiteral => true,
            CodeBlockType.NodeGraphSequence => true,
            CodeBlockType.FileSequence => true,
            CodeBlockType.NumberSequence => true,
            CodeBlockType.NodeScript => true,
            _ => false
        };

        /// <summary>
        /// 是否是容器类型（可以包含其他代码块）
        /// </summary>
        public bool IsContainer => Slots.Count > 0;

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    /// <summary>
    /// 代码块工厂
    /// </summary>
    public static class CodeBlockFactory
    {
        /// <summary>
        /// 创建代码块
        /// </summary>
        public static CodeBlock CreateCodeBlock(CodeBlockType type)
        {
            var block = new CodeBlock { Type = type };

            switch (type)
            {
                case CodeBlockType.CollectionLiteral:
                    block.DisplayText = "集合";
                    block.Color = new SolidColorBrush(Color.FromRgb(52, 152, 219)); // 蓝色
                    break;

                case CodeBlockType.MultiFunctionLiteral:
                    block.DisplayText = "多功能";
                    block.Color = new SolidColorBrush(Color.FromRgb(155, 89, 182)); // 紫色
                    break;

                case CodeBlockType.NodeGraphSequence:
                    block.DisplayText = "节点图序列";
                    block.Color = new SolidColorBrush(Color.FromRgb(52, 152, 219)); // 蓝色
                    break;

                case CodeBlockType.FileSequence:
                    block.DisplayText = "文件序列";
                    block.Color = new SolidColorBrush(Color.FromRgb(52, 152, 219)); // 蓝色
                    break;

                case CodeBlockType.NumberSequence:
                    block.DisplayText = "序号列";
                    block.Color = new SolidColorBrush(Color.FromRgb(52, 152, 219)); // 蓝色
                    break;

                case CodeBlockType.NodeScript:
                    block.DisplayText = "节点脚本";
                    block.Color = new SolidColorBrush(Color.FromRgb(155, 89, 182)); // 紫色
                    break;

                case CodeBlockType.ProcessBlock:
                    block.DisplayText = "处理";
                    block.Color = new SolidColorBrush(Color.FromRgb(46, 204, 113)); // 绿色
                    // 添加一个多功能插槽
                    block.Slots.Add(new CodeBlockSlot { Type = SlotType.MultiFunction, Name = "节点图" });
                    break;

                case CodeBlockType.LoopBlock:
                    block.DisplayText = "对于每个";
                    block.Color = new SolidColorBrush(Color.FromRgb(230, 126, 34)); // 橙色
                    // 添加集合插槽和多功能插槽
                    block.Slots.Add(new CodeBlockSlot { Type = SlotType.Collection, Name = "集合" });
                    block.Slots.Add(new CodeBlockSlot { Type = SlotType.MultiFunction, Name = "项目" });
                    block.Slots.Add(new CodeBlockSlot { Type = SlotType.Logic, Name = "执行" });
                    break;
            }

            return block;
        }
    }

    /// <summary>
    /// 插槽模板选择器
    /// </summary>
    public class SlotTemplateSelector : DataTemplateSelector
    {
        public DataTemplate? CollectionTemplate { get; set; }
        public DataTemplate? MultiFunctionTemplate { get; set; }
        public DataTemplate? LogicTemplate { get; set; }

        public override DataTemplate? SelectTemplate(object item, DependencyObject container)
        {
            if (item is CodeBlockSlot slot)
            {
                return slot.Type switch
                {
                    SlotType.Collection => CollectionTemplate,
                    SlotType.MultiFunction => MultiFunctionTemplate,
                    SlotType.Logic => LogicTemplate,
                    _ => null
                };
            }
            return null;
        }
    }

    /// <summary>
    /// 代码块模板选择器
    /// </summary>
    public class CodeBlockTemplateSelector : DataTemplateSelector
    {
        public DataTemplate? DefaultTemplate { get; set; }
        public DataTemplate? ProcessTemplate { get; set; }
        public DataTemplate? LoopTemplate { get; set; }

        public override DataTemplate? SelectTemplate(object item, DependencyObject container)
        {
            if (item is CodeBlock block)
            {
                return block.Type switch
                {
                    CodeBlockType.ProcessBlock => ProcessTemplate,
                    CodeBlockType.LoopBlock => LoopTemplate,
                    _ => DefaultTemplate
                };
            }
            return DefaultTemplate;
        }
    }

    #region 新的积木块架构

    /// <summary>
    /// 积木块设定面板接口
    /// </summary>
    public interface ICodeBlockSettings
    {
        /// <summary>
        /// 创建设定面板控件
        /// </summary>
        FrameworkElement CreateSettingsControl();

        /// <summary>
        /// 序列化设定数据
        /// </summary>
        Dictionary<string, object> SerializeSettings();

        /// <summary>
        /// 反序列化设定数据
        /// </summary>
        void DeserializeSettings(Dictionary<string, object> data);

        /// <summary>
        /// 验证设定是否有效
        /// </summary>
        bool ValidateSettings();

        /// <summary>
        /// 获取设定摘要文本
        /// </summary>
        string GetSettingsSummary();

        /// <summary>
        /// 设定变化事件
        /// </summary>
        event EventHandler<SettingsChangedEventArgs>? SettingsChanged;
    }

    /// <summary>
    /// 设定变化事件参数
    /// </summary>
    public class SettingsChangedEventArgs : EventArgs
    {
        public string PropertyName { get; set; } = string.Empty;
        public object? OldValue { get; set; }
        public object? NewValue { get; set; }
    }

    /// <summary>
    /// 可序列化的积木块接口
    /// </summary>
    public interface ISerializableCodeBlock
    {
        /// <summary>
        /// 序列化积木块数据
        /// </summary>
        Dictionary<string, object> Serialize();

        /// <summary>
        /// 反序列化积木块数据
        /// </summary>
        void Deserialize(Dictionary<string, object> data);

        /// <summary>
        /// 获取积木块类型标识
        /// </summary>
        string GetTypeIdentifier();

        /// <summary>
        /// 获取积木块版本
        /// </summary>
        string GetVersion();
    }

    /// <summary>
    /// 积木块元数据接口
    /// </summary>
    public interface ICodeBlockMetadata
    {
        /// <summary>
        /// 注入元数据到下游（覆盖注入）
        /// </summary>
        Dictionary<string, object> InjectMetadata(Dictionary<string, object> currentMetadata);

        /// <summary>
        /// 从上游提取所需元数据
        /// </summary>
        void ExtractMetadata(Dictionary<string, object> upstreamMetadata);

        /// <summary>
        /// 生成积木块特定的元数据
        /// </summary>
        Dictionary<string, object> GenerateMetadata(Dictionary<string, object> currentMetadata);

        /// <summary>
        /// 处理元数据流（完整的元数据处理流程）
        /// </summary>
        Dictionary<string, object> ProcessMetadata(Dictionary<string, object> upstreamMetadata);
    }

    /// <summary>
    /// 新的积木块基类
    /// </summary>
    public abstract class CodeBlockBase : INotifyPropertyChanged, ICodeBlockSettings, ISerializableCodeBlock, ICodeBlockMetadata
    {
        private Point _position;
        private bool _isSelected;
        private bool _isDragging;
        private string _displayName = string.Empty;
        private string _description = string.Empty;
        private Dictionary<string, object> _metadata = new();

        #region 基础属性

        /// <summary>
        /// 唯一标识符
        /// </summary>
        public Guid Id { get; set; } = Guid.NewGuid();

        /// <summary>
        /// 积木块类型
        /// </summary>
        public abstract CodeBlockType BlockType { get; }

        /// <summary>
        /// 显示名称
        /// </summary>
        public string DisplayName
        {
            get => _displayName;
            set
            {
                if (_displayName != value)
                {
                    _displayName = value;
                    OnPropertyChanged(nameof(DisplayName));
                }
            }
        }

        /// <summary>
        /// 描述
        /// </summary>
        public string Description
        {
            get => _description;
            set
            {
                if (_description != value)
                {
                    _description = value;
                    OnPropertyChanged(nameof(Description));
                }
            }
        }

        /// <summary>
        /// 位置
        /// </summary>
        public Point Position
        {
            get => _position;
            set
            {
                if (_position != value)
                {
                    _position = value;
                    OnPropertyChanged(nameof(Position));
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
        /// 是否正在拖拽
        /// </summary>
        public bool IsDragging
        {
            get => _isDragging;
            set
            {
                if (_isDragging != value)
                {
                    _isDragging = value;
                    OnPropertyChanged(nameof(IsDragging));
                }
            }
        }

        /// <summary>
        /// 积木块元数据
        /// </summary>
        public Dictionary<string, object> Metadata
        {
            get => _metadata;
            set
            {
                if (_metadata != value)
                {
                    _metadata = value ?? new Dictionary<string, object>();
                    OnPropertyChanged(nameof(Metadata));
                }
            }
        }

        #endregion

        #region 抽象方法

        /// <summary>
        /// 创建设定面板控件
        /// </summary>
        public abstract FrameworkElement CreateSettingsControl();

        /// <summary>
        /// 序列化设定数据
        /// </summary>
        public abstract Dictionary<string, object> SerializeSettings();

        /// <summary>
        /// 反序列化设定数据
        /// </summary>
        public abstract void DeserializeSettings(Dictionary<string, object> data);

        /// <summary>
        /// 验证设定是否有效
        /// </summary>
        public abstract bool ValidateSettings();

        /// <summary>
        /// 获取设定摘要文本
        /// </summary>
        public abstract string GetSettingsSummary();

        /// <summary>
        /// 获取积木块类型标识
        /// </summary>
        public abstract string GetTypeIdentifier();

        #endregion

        #region 元数据抽象方法

        /// <summary>
        /// 注入元数据到下游（覆盖注入）
        /// </summary>
        public abstract Dictionary<string, object> InjectMetadata(Dictionary<string, object> currentMetadata);

        /// <summary>
        /// 从上游提取所需元数据
        /// </summary>
        public abstract void ExtractMetadata(Dictionary<string, object> upstreamMetadata);

        /// <summary>
        /// 生成积木块特定的元数据
        /// </summary>
        public abstract Dictionary<string, object> GenerateMetadata(Dictionary<string, object> currentMetadata);

        /// <summary>
        /// 处理元数据流（完整的元数据处理流程）
        /// </summary>
        public abstract Dictionary<string, object> ProcessMetadata(Dictionary<string, object> upstreamMetadata);

        #endregion

        #region 虚方法

        /// <summary>
        /// 获取积木块版本
        /// </summary>
        public virtual string GetVersion() => "1.0";

        /// <summary>
        /// 初始化积木块元数据
        /// </summary>
        protected virtual void InitializeMetadata()
        {
            var metadataManager = new Services.BatchMetadataManager();
            Metadata = metadataManager.CreateMetadata(new Dictionary<string, object>
            {
                ["积木块类型"] = BlockType.ToString(),
                ["积木块名称"] = DisplayName,
                ["积木块ID"] = Id.ToString()
            });
        }

        /// <summary>
        /// 更新元数据中的处理记录
        /// </summary>
        protected virtual void UpdateProcessingRecord(string operation, Dictionary<string, object>? details = null)
        {
            var metadataManager = new Services.BatchMetadataManager();
            Metadata = metadataManager.AddProcessingRecord(Metadata, operation, BlockType.ToString(), details);
        }

        /// <summary>
        /// 注入元数据值（覆盖注入）
        /// </summary>
        protected virtual void InjectMetadataValue(string key, object? value)
        {
            var metadataManager = new Services.BatchMetadataManager();
            Metadata = metadataManager.SetMetadataValue(Metadata, key, value);
        }

        /// <summary>
        /// 获取元数据值
        /// </summary>
        protected virtual T? GetMetadataValue<T>(string key, T? defaultValue = default)
        {
            var metadataManager = new Services.BatchMetadataManager();
            return metadataManager.GetMetadataValue(Metadata, key, defaultValue);
        }

        /// <summary>
        /// 移除元数据键
        /// </summary>
        protected virtual void RemoveMetadataKeys(params string[] keys)
        {
            var metadataManager = new Services.BatchMetadataManager();
            Metadata = metadataManager.RemoveMetadata(Metadata, keys);
        }

        /// <summary>
        /// 合并上游元数据到当前元数据
        /// </summary>
        protected virtual void MergeUpstreamMetadata(Dictionary<string, object> upstreamMetadata)
        {
            var metadataManager = new Services.BatchMetadataManager();
            Metadata = metadataManager.MergeMetadata(Metadata, upstreamMetadata);
        }

        /// <summary>
        /// 默认的元数据处理流程实现
        /// </summary>
        protected virtual Dictionary<string, object> DefaultProcessMetadata(Dictionary<string, object> upstreamMetadata)
        {
            var metadataManager = new Services.BatchMetadataManager();

            // 1. 从上游提取元数据
            ExtractMetadata(upstreamMetadata);

            // 2. 合并上游元数据到当前元数据
            var currentMetadata = metadataManager.MergeMetadata(Metadata, upstreamMetadata);

            // 3. 注入积木块特定的元数据
            currentMetadata = InjectMetadata(currentMetadata);

            // 4. 生成积木块特定的元数据
            currentMetadata = GenerateMetadata(currentMetadata);

            // 5. 更新当前积木块的元数据
            Metadata = currentMetadata;

            // 6. 添加处理记录
            UpdateProcessingRecord("元数据处理", new Dictionary<string, object>
            {
                ["积木块名称"] = DisplayName,
                ["处理时间"] = DateTime.Now.ToString("HH:mm:ss")
            });

            return Metadata;
        }

        #endregion

        #region 序列化

        /// <summary>
        /// 序列化积木块数据
        /// </summary>
        public virtual Dictionary<string, object> Serialize()
        {
            var data = new Dictionary<string, object>
            {
                ["Id"] = Id.ToString(),
                ["BlockType"] = BlockType.ToString(),
                ["TypeIdentifier"] = GetTypeIdentifier(),
                ["Version"] = GetVersion(),
                ["DisplayName"] = DisplayName,
                ["Description"] = Description,
                ["Position"] = new Dictionary<string, object>
                {
                    ["X"] = Position.X,
                    ["Y"] = Position.Y
                },
                ["Settings"] = SerializeSettings(),
                ["Metadata"] = Metadata
            };

            return data;
        }

        /// <summary>
        /// 反序列化积木块数据
        /// </summary>
        public virtual void Deserialize(Dictionary<string, object> data)
        {
            if (data.TryGetValue("Id", out var idValue) && idValue is string idStr && Guid.TryParse(idStr, out var id))
                Id = id;

            if (data.TryGetValue("DisplayName", out var displayNameValue) && displayNameValue is string displayName)
                DisplayName = displayName;

            if (data.TryGetValue("Description", out var descriptionValue) && descriptionValue is string description)
                Description = description;

            if (data.TryGetValue("Position", out var positionValue) && positionValue is Dictionary<string, object> positionData)
            {
                var x = positionData.TryGetValue("X", out var xValue) && xValue is double xDouble ? xDouble : 0;
                var y = positionData.TryGetValue("Y", out var yValue) && yValue is double yDouble ? yDouble : 0;
                Position = new Point(x, y);
            }

            if (data.TryGetValue("Settings", out var settingsValue) && settingsValue is Dictionary<string, object> settingsData)
                DeserializeSettings(settingsData);

            if (data.TryGetValue("Metadata", out var metadataValue) && metadataValue is Dictionary<string, object> metadataData)
                Metadata = metadataData;
            else
                InitializeMetadata(); // 如果没有元数据，初始化默认元数据
        }

        #endregion

        #region 事件

        /// <summary>
        /// 设定变化事件
        /// </summary>
        public event EventHandler<SettingsChangedEventArgs>? SettingsChanged;

        /// <summary>
        /// 属性变化事件
        /// </summary>
        public event PropertyChangedEventHandler? PropertyChanged;

        /// <summary>
        /// 触发设定变化事件
        /// </summary>
        protected virtual void OnSettingsChanged(string propertyName, object? oldValue, object? newValue)
        {
            SettingsChanged?.Invoke(this, new SettingsChangedEventArgs
            {
                PropertyName = propertyName,
                OldValue = oldValue,
                NewValue = newValue
            });
        }

        /// <summary>
        /// 触发属性变化事件
        /// </summary>
        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion
    }

    #endregion
}
