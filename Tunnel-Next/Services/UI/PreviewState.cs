using System;

namespace Tunnel_Next.Services.UI
{
    /// <summary>
    /// 全局预览状态服务，由标准 ImagePreviewControl 更新，用于脚本查询缩放与滚动信息。
    /// </summary>
    public static class PreviewState
    {
        public static double Zoom { get; internal set; } = 1.0;
        public static double ScrollOffsetX { get; internal set; } = 0.0;
        public static double ScrollOffsetY { get; internal set; } = 0.0;
    }
} 