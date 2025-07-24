using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
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
}
