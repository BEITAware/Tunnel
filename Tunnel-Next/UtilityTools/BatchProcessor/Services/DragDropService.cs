using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Tunnel_Next.UtilityTools.BatchProcessor.Models;

namespace Tunnel_Next.UtilityTools.BatchProcessor.Services
{
    /// <summary>
    /// 拖拽服务
    /// </summary>
    public class DragDropService
    {
        private bool _isDragging;
        private Point _startPoint;
        private CodeBlock? _draggedBlock;
        private FrameworkElement? _draggedElement;

        /// <summary>
        /// 拖拽开始事件
        /// </summary>
        public event Action<CodeBlock>? DragStarted;

        /// <summary>
        /// 拖拽结束事件
        /// </summary>
        public event Action<CodeBlock, Point>? DragCompleted;

        /// <summary>
        /// 拖拽取消事件
        /// </summary>
        public event Action<CodeBlock>? DragCancelled;

        /// <summary>
        /// 开始拖拽
        /// </summary>
        public void StartDrag(FrameworkElement element, CodeBlock codeBlock, Point startPoint)
        {
            if (_isDragging) return;

            _isDragging = true;
            _startPoint = startPoint;
            _draggedBlock = codeBlock;
            _draggedElement = element;

            // 设置拖拽状态
            codeBlock.IsDragging = true;

            // 捕获鼠标
            element.CaptureMouse();

            // 注册事件
            element.MouseMove += OnMouseMove;
            element.MouseUp += OnMouseUp;
            element.LostMouseCapture += OnLostMouseCapture;

            DragStarted?.Invoke(codeBlock);
        }

        /// <summary>
        /// 鼠标移动事件
        /// </summary>
        private void OnMouseMove(object sender, MouseEventArgs e)
        {
            if (!_isDragging || _draggedBlock == null || _draggedElement == null)
                return;

            var currentPoint = e.GetPosition(_draggedElement.Parent as IInputElement);
            var offset = currentPoint - _startPoint;

            // 更新代码块位置
            _draggedBlock.Position = new Point(
                _draggedBlock.Position.X + offset.X,
                _draggedBlock.Position.Y + offset.Y
            );

            _startPoint = currentPoint;
        }

        /// <summary>
        /// 鼠标释放事件
        /// </summary>
        private void OnMouseUp(object sender, MouseButtonEventArgs e)
        {
            if (!_isDragging || _draggedBlock == null || _draggedElement == null)
                return;

            var endPoint = e.GetPosition(_draggedElement.Parent as IInputElement);
            CompleteDrag(endPoint);
        }

        /// <summary>
        /// 失去鼠标捕获事件
        /// </summary>
        private void OnLostMouseCapture(object sender, MouseEventArgs e)
        {
            if (!_isDragging || _draggedBlock == null)
                return;

            CancelDrag();
        }

        /// <summary>
        /// 完成拖拽
        /// </summary>
        private void CompleteDrag(Point endPoint)
        {
            if (_draggedBlock == null || _draggedElement == null)
                return;

            // 清理事件
            CleanupDrag();

            // 设置拖拽状态
            _draggedBlock.IsDragging = false;

            DragCompleted?.Invoke(_draggedBlock, endPoint);

            ResetDragState();
        }

        /// <summary>
        /// 取消拖拽
        /// </summary>
        private void CancelDrag()
        {
            if (_draggedBlock == null)
                return;

            // 清理事件
            CleanupDrag();

            // 设置拖拽状态
            _draggedBlock.IsDragging = false;

            DragCancelled?.Invoke(_draggedBlock);

            ResetDragState();
        }

        /// <summary>
        /// 清理拖拽事件
        /// </summary>
        private void CleanupDrag()
        {
            if (_draggedElement != null)
            {
                _draggedElement.MouseMove -= OnMouseMove;
                _draggedElement.MouseUp -= OnMouseUp;
                _draggedElement.LostMouseCapture -= OnLostMouseCapture;
                _draggedElement.ReleaseMouseCapture();
            }
        }

        /// <summary>
        /// 重置拖拽状态
        /// </summary>
        private void ResetDragState()
        {
            _isDragging = false;
            _draggedBlock = null;
            _draggedElement = null;
            _startPoint = new Point();
        }

        /// <summary>
        /// 检查是否可以放置到指定位置
        /// </summary>
        public bool CanDropAt(CodeBlock draggedBlock, Point dropPoint, FrameworkElement container)
        {
            // 简单的边界检查
            return dropPoint.X >= 0 && dropPoint.Y >= 0 &&
                   dropPoint.X <= container.ActualWidth &&
                   dropPoint.Y <= container.ActualHeight;
        }

        /// <summary>
        /// 查找拖拽目标插槽
        /// </summary>
        public CodeBlockSlot? FindDropTarget(Point dropPoint, FrameworkElement container)
        {
            // 使用命中测试查找目标插槽
            var hitTest = VisualTreeHelper.HitTest(container, dropPoint);
            
            if (hitTest?.VisualHit != null)
            {
                // 向上遍历可视化树查找插槽
                var element = hitTest.VisualHit as FrameworkElement;
                while (element != null)
                {
                    if (element.DataContext is CodeBlockSlot slot)
                        return slot;
                    
                    element = VisualTreeHelper.GetParent(element) as FrameworkElement;
                }
            }

            return null;
        }
    }

    /// <summary>
    /// 拖拽行为附加属性
    /// </summary>
    public static class DragBehavior
    {
        public static readonly DependencyProperty IsDragEnabledProperty =
            DependencyProperty.RegisterAttached(
                "IsDragEnabled",
                typeof(bool),
                typeof(DragBehavior),
                new PropertyMetadata(false, OnIsDragEnabledChanged));

        public static bool GetIsDragEnabled(DependencyObject obj)
        {
            return (bool)obj.GetValue(IsDragEnabledProperty);
        }

        public static void SetIsDragEnabled(DependencyObject obj, bool value)
        {
            obj.SetValue(IsDragEnabledProperty, value);
        }

        private static void OnIsDragEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is FrameworkElement element)
            {
                if ((bool)e.NewValue)
                {
                    element.MouseLeftButtonDown += OnMouseLeftButtonDown;
                }
                else
                {
                    element.MouseLeftButtonDown -= OnMouseLeftButtonDown;
                }
            }
        }

        private static void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement element && element.DataContext is CodeBlock codeBlock)
            {
                var dragService = new DragDropService();
                var startPoint = e.GetPosition(element);
                dragService.StartDrag(element, codeBlock, startPoint);
                e.Handled = true;
            }
        }
    }
}
