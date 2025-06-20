using System;
using System.Windows;
using System.Windows.Controls;

namespace Tunnel_Next.Services
{
    /// <summary>
    /// 节点菜单服务接口 - 统一传统脚本和Revival Scripts的菜单接口
    /// </summary>
    public interface INodeMenuService : IDisposable
    {
        /// <summary>
        /// 创建节点添加菜单
        /// </summary>
        /// <param name="onNodeSelected">节点选择回调</param>
        /// <returns>上下文菜单</returns>
        ContextMenu CreateNodeAddMenu(Action<string> onNodeSelected);

        /// <summary>
        /// 显示节点添加菜单
        /// </summary>
        /// <param name="targetElement">目标元素</param>
        /// <param name="onNodeSelected">节点选择回调</param>
        void ShowNodeAddMenu(FrameworkElement targetElement, Action<string> onNodeSelected);
    }
}
