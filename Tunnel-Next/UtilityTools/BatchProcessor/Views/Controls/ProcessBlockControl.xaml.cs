using System.Windows;
using System.Windows.Controls;
using Tunnel_Next.UtilityTools.BatchProcessor.Models;

namespace Tunnel_Next.UtilityTools.BatchProcessor.Views.Controls
{
    /// <summary>
    /// ProcessBlockControl.xaml 的交互逻辑
    /// </summary>
    public partial class ProcessBlockControl : UserControl
    {
        public ProcessBlockControl()
        {
            InitializeComponent();
        }

        /// <summary>
        /// 插槽拖拽悬停事件
        /// </summary>
        private void Slot_DragOver(object sender, DragEventArgs e)
        {
            if (sender is FrameworkElement element && element.DataContext is CodeBlockSlot slot)
            {
                if (e.Data.GetDataPresent(typeof(CodeBlock)))
                {
                    var draggedBlock = e.Data.GetData(typeof(CodeBlock)) as CodeBlock;
                    if (draggedBlock != null && slot.CanAccept(draggedBlock))
                    {
                        e.Effects = DragDropEffects.Move;
                        slot.IsHighlighted = true;
                    }
                    else
                    {
                        e.Effects = DragDropEffects.None;
                    }
                }
                else
                {
                    e.Effects = DragDropEffects.None;
                }
            }
            e.Handled = true;
        }

        /// <summary>
        /// 插槽放置事件
        /// </summary>
        private void Slot_Drop(object sender, DragEventArgs e)
        {
            if (sender is FrameworkElement element && element.DataContext is CodeBlockSlot slot)
            {
                if (e.Data.GetDataPresent(typeof(CodeBlock)))
                {
                    var draggedBlock = e.Data.GetData(typeof(CodeBlock)) as CodeBlock;
                    if (draggedBlock != null && slot.CanAccept(draggedBlock))
                    {
                        // 如果插槽已有内容，先移除
                        if (slot.Content != null)
                        {
                            slot.Content.Parent = null;
                        }

                        // 设置新内容
                        slot.Content = draggedBlock;
                        draggedBlock.Parent = DataContext as CodeBlock;
                    }
                }
                slot.IsHighlighted = false;
            }
            e.Handled = true;
        }
    }
}
