using System;
using System.Windows;
using System.Windows.Controls;

namespace Tunnel_Next.Services.UI
{
    /// <summary>
    /// 预览触发源。
    /// </summary>
    public enum PreviewTrigger
    {
        /// <summary>
        /// 由于节点被选中而触发。
        /// </summary>
        NodeSelected,

        /// <summary>
        /// 由于脚本参数窗口被打开而触发。
        /// </summary>
        ParameterWindow
    }

    /// <summary>
    /// 统一管理预览控件接管与恢复逻辑的管理器（基础定义）。
    /// </summary>
    public sealed class PreviewManager
    {
        private static readonly Lazy<PreviewManager> _instance = new(() => new PreviewManager());
        public static PreviewManager Instance => _instance.Value;

        private object? _currentOwner;
        private PreviewTrigger _currentTrigger;
        private ContentControl? _host;
        private FrameworkElement? _defaultPreview;
        private FrameworkElement? _currentPreview;
        // 历史栈，用于回退
        private readonly Stack<(object owner, FrameworkElement view, PreviewTrigger trigger)> _history = new();

        private PreviewManager() { }

        /// <summary>
        /// 初始化预览宿主。必须在应用启动后且可视控件创建完成后调用一次。
        /// </summary>
        public void Initialize(ContentControl host, FrameworkElement defaultPreview)
        {
            _host = host ?? throw new ArgumentNullException(nameof(host));
            _defaultPreview = defaultPreview ?? throw new ArgumentNullException(nameof(defaultPreview));
            _host.Content = _defaultPreview;
        }

        /// <summary>
        /// 请求接管预览。
        /// </summary>
        /// <returns>成功与否。</returns>
        public bool RequestTakeover(object owner, FrameworkElement previewControl, PreviewTrigger trigger)
        {
            if (owner == null || previewControl == null || _host == null)
                return false;

            // 如果有旧拥有者，将其压栈
            if (_currentOwner != null && _currentPreview != null)
            {
                _history.Push((_currentOwner, _currentPreview, _currentTrigger));
            }

            // 通知旧拥有者
            if (_currentOwner is Tunnel_Next.Services.Scripting.IScriptPreviewProvider oldProvider)
            {
                try { oldProvider.OnPreviewReleased(); } catch { }
            }

            // 直接覆盖，后来的永远优先。
            _currentOwner = owner;
            _currentTrigger = trigger;
            _currentPreview = previewControl;
            _host.Content = previewControl;
            return true;
        }

        /// <summary>
        /// 指定拥有者释放预览。如果不是当前拥有者，则忽略。
        /// </summary>
        public void Release(object owner)
        {
            if (_currentOwner != owner)
                return;

            // 如果有旧拥有者，将其压栈
            if (_currentOwner != null && _currentPreview != null)
            {
                _history.Push((_currentOwner, _currentPreview, _currentTrigger));
            }

            // 通知拥有者
            if (owner is Tunnel_Next.Services.Scripting.IScriptPreviewProvider provider)
            {
                try { provider.OnPreviewReleased(); } catch { }
            }

            _currentOwner = null;
            _currentPreview = null;
            RestoreDefault();

            // 如果历史栈还有，恢复上一个
            if (_history.Count > 0)
            {
                var (prevOwner, prevView, prevTrigger) = _history.Pop();
                _currentOwner = prevOwner;
                _currentPreview = prevView;
                _currentTrigger = prevTrigger;
                _host!.Content = prevView;
            }
        }

        /// <summary>
        /// 强制恢复为默认预览。
        /// </summary>
        public void ForceReleaseAll()
        {
            if (_currentOwner is Tunnel_Next.Services.Scripting.IScriptPreviewProvider provider)
            {
                try { provider.OnPreviewReleased(); } catch { }
            }

            _currentOwner = null;
            _currentPreview = null;
            _history.Clear();
            RestoreDefault();
        }

        private void RestoreDefault()
        {
            if (_host != null && _defaultPreview != null)
            {
                _host.Content = _defaultPreview;
            }
        }
    }
} 