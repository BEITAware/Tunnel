using System.Windows;
using Tunnel_Next.Services.UI;

namespace Tunnel_Next.Services.Scripting
{
    /// <summary>
    /// 提供脚本自定义预览能力的可选接口。脚本实现此接口即可在合适时机接管主预览区域。
    /// </summary>
    public interface IScriptPreviewProvider
    {
        /// <summary>
        /// 脚本是否希望在指定触发场景下接管预览。
        /// </summary>
        /// <param name="trigger">触发源。</param>
        /// <returns>是否愿意接管。</returns>
        bool WantsPreview(PreviewTrigger trigger);

        /// <summary>
        /// 创建脚本自己的预览控件。
        /// </summary>
        /// <param name="trigger">触发源。</param>
        /// <param name="context">脚本上下文。</param>
        /// <returns>用于显示的控件。</returns>
        FrameworkElement? CreatePreviewControl(PreviewTrigger trigger, IScriptContext context);

        /// <summary>
        /// 当预览控件被释放/切换时回调，用于资源清理等。
        /// </summary>
        void OnPreviewReleased();
    }
} 