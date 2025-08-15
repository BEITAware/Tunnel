using System;
using System.Collections.Generic;
using System.Linq;
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

        // 基底预览相关字段
        private object? _basePreviewOwner;
        private FrameworkElement? _basePreview;
        private readonly List<Services.Scripting.IBasePreviewProvider> _registeredBaseProviders = new();

        private PreviewManager() { }

        /// <summary>
        /// 初始化预览宿主。必须在应用启动后且可视控件创建完成后调用一次。
        /// </summary>
        public void Initialize(ContentControl host, FrameworkElement defaultPreview)
        {
            _host = host ?? throw new ArgumentNullException(nameof(host));
            _defaultPreview = defaultPreview ?? throw new ArgumentNullException(nameof(defaultPreview));

            // 检查是否有基底预览可用，如果没有则使用默认预览
            if (_basePreview == null)
            {
                _host.Content = _defaultPreview;
            }
            else
            {
                _host.Content = _basePreview;
            }
        }

        /// <summary>
        /// 注册基底预览提供者。
        /// </summary>
        /// <param name="provider">基底预览提供者。</param>
        /// <param name="context">脚本上下文。</param>
        /// <returns>是否成功注册并激活。</returns>
        public bool RegisterBasePreviewProvider(Services.Scripting.IBasePreviewProvider provider, Services.Scripting.IScriptContext context)
        {
            if (provider == null || !provider.WantsToProvideBasePreview())
                return false;

            // 添加到注册列表
            if (!_registeredBaseProviders.Contains(provider))
            {
                _registeredBaseProviders.Add(provider);
            }

            // 选择优先级最高的提供者
            var bestProvider = _registeredBaseProviders
                .OrderByDescending(p => p.GetBasePreviewPriority())
                .FirstOrDefault();

            if (bestProvider != null && bestProvider == provider)
            {
                // 如果当前提供者是最高优先级的，激活它
                return ActivateBasePreview(provider, context);
            }

            return false;
        }

        /// <summary>
        /// 取消注册基底预览提供者。
        /// </summary>
        /// <param name="provider">要取消注册的提供者。</param>
        public void UnregisterBasePreviewProvider(Services.Scripting.IBasePreviewProvider provider)
        {
            if (provider == null)
                return;

            _registeredBaseProviders.Remove(provider);

            // 如果当前激活的基底预览就是这个提供者，需要切换到其他提供者或默认预览
            if (_basePreviewOwner == provider)
            {
                DeactivateCurrentBasePreview();

                // 尝试激活下一个优先级最高的提供者
                var nextBestProvider = _registeredBaseProviders
                    .OrderByDescending(p => p.GetBasePreviewPriority())
                    .FirstOrDefault();

                if (nextBestProvider != null)
                {
                    var ctx = new Services.Scripting.ScriptContext("", "", "", () => null, _ => { }, _ => new Dictionary<string, object>(), (a, b, c) => { });
                    ActivateBasePreview(nextBestProvider, ctx);
                }
                else
                {
                    // 没有其他基底预览提供者，恢复到默认预览
                    RestoreToDefault();
                }
            }
        }

        /// <summary>
        /// 激活基底预览。
        /// </summary>
        /// <param name="provider">基底预览提供者。</param>
        /// <param name="context">脚本上下文。</param>
        /// <returns>是否成功激活。</returns>
        private bool ActivateBasePreview(Services.Scripting.IBasePreviewProvider provider, Services.Scripting.IScriptContext context)
        {
            try
            {
                var basePreviewControl = provider.CreateBasePreviewControl(context);
                if (basePreviewControl == null)
                    return false;

                // 停用当前的基底预览（如果有）
                DeactivateCurrentBasePreview();

                // 激活新的基底预览
                _basePreviewOwner = provider;
                _basePreview = basePreviewControl;

                // 如果当前没有其他预览控件接管，显示基底预览
                if (_currentPreview == null && _host != null)
                {
                    _host.Content = _basePreview;
                }

                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 停用当前的基底预览。
        /// </summary>
        private void DeactivateCurrentBasePreview()
        {
            if (_basePreviewOwner is Services.Scripting.IBasePreviewProvider currentProvider)
            {
                try
                {
                    currentProvider.OnBasePreviewReleased();
                }
                catch
                {
                    // 忽略清理异常
                }
            }

            _basePreviewOwner = null;
            _basePreview = null;
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
            if (_host != null)
            {
                // 优先使用基底预览，如果没有则使用默认预览
                if (_basePreview != null)
                {
                    _host.Content = _basePreview;
                }
                else if (_defaultPreview != null)
                {
                    _host.Content = _defaultPreview;
                }
            }
        }

        /// <summary>
        /// 恢复到真正的默认预览（忽略基底预览）。
        /// </summary>
        private void RestoreToDefault()
        {
            if (_host != null && _defaultPreview != null)
            {
                _host.Content = _defaultPreview;
            }
        }

        /// <summary>
        /// 获取当前激活的基底预览提供者信息。
        /// </summary>
        /// <returns>基底预览提供者信息，如果没有则返回null。</returns>
        public string? GetCurrentBasePreviewInfo()
        {
            if (_basePreviewOwner is Services.Scripting.IBasePreviewProvider provider)
            {
                return $"{provider.GetBasePreviewDisplayName()} (优先级: {provider.GetBasePreviewPriority()})";
            }
            return null;
        }

        /// <summary>
        /// 获取所有注册的基底预览提供者信息。
        /// </summary>
        /// <returns>提供者信息列表。</returns>
        public List<string> GetRegisteredBasePreviewProviders()
        {
            return _registeredBaseProviders
                .Select(p => $"{p.GetBasePreviewDisplayName()} (优先级: {p.GetBasePreviewPriority()})")
                .ToList();
        }

        /// <summary>
        /// 清除所有基底预览提供者。
        /// </summary>
        public void ClearAllBasePreviewProviders()
        {
            // 停用当前的基底预览
            DeactivateCurrentBasePreview();

            // 清除所有注册的提供者
            _registeredBaseProviders.Clear();

            // 恢复到默认预览
            RestoreToDefault();
        }

        /// <summary>
        /// 获取基底预览提供者的数量。
        /// </summary>
        /// <returns>提供者数量。</returns>
        public int GetBasePreviewProvidersCount()
        {
            return _registeredBaseProviders.Count;
        }
    }
} 