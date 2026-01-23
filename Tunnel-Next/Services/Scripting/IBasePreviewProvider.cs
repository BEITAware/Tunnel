using System.Windows;
using Tunnel_Next.Services.UI;

namespace Tunnel_Next.Services.Scripting
{
    /// <summary>
    /// 提供脚本基底预览能力的接口。实现此接口的脚本可以替代程序默认的预览行为，
    /// 成为所有预览器的最底层，取代Tunnel的默认预览。
    /// </summary>
    public interface IBasePreviewProvider
    {
        /// <summary>
        /// 脚本是否希望提供基底预览功能。
        /// </summary>
        /// <returns>是否愿意提供基底预览。</returns>
        bool WantsToProvideBasePreview();

        /// <summary>
        /// 创建基底预览控件。此控件将替代程序默认的预览控件，
        /// 成为所有其他预览控件的基础层。
        /// </summary>
        /// <param name="context">脚本上下文。</param>
        /// <returns>用于作为基底预览的控件。</returns>
        FrameworkElement? CreateBasePreviewControl(IScriptContext context);

        /// <summary>
        /// 当基底预览控件被释放时回调，用于资源清理等。
        /// </summary>
        void OnBasePreviewReleased();

        /// <summary>
        /// 获取基底预览的优先级。数值越高优先级越高。
        /// 当多个脚本都想提供基底预览时，优先级最高的会被选中。
        /// </summary>
        /// <returns>优先级数值。</returns>
        int GetBasePreviewPriority();

        /// <summary>
        /// 基底预览控件的显示名称，用于调试和日志。
        /// </summary>
        /// <returns>显示名称。</returns>
        string GetBasePreviewDisplayName();
    }
}
