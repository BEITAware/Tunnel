using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Tunnel_Next.Services.Scripting;

namespace Tunnel_Next.Services
{
    /// <summary>
    /// TunnelExtension Scripts节点菜单服务 - 基于TunnelExtension Scripts系统的节点菜单
    /// </summary>
    public class TunnelExtensionNodeMenuService : INodeMenuService, IDisposable
    {
        private readonly TunnelExtensionScriptManager _TunnelExtensionScriptManager;

        public TunnelExtensionNodeMenuService(TunnelExtensionScriptManager TunnelExtensionScriptManager)
        {
            _TunnelExtensionScriptManager = TunnelExtensionScriptManager ?? throw new ArgumentNullException(nameof(TunnelExtensionScriptManager));
            
            // 订阅脚本编译完成事件，当脚本编译完成时重新排序菜单
            _TunnelExtensionScriptManager.ScriptsCompilationCompleted += OnScriptsCompilationCompleted;
        }
        
        /// <summary>
        /// 脚本编译完成事件处理
        /// </summary>
        private void OnScriptsCompilationCompleted(object? sender, EventArgs e)
        {
            // 脚本编译完成后，菜单结构会在下次创建菜单时自动排序
            // 这里不需要额外处理，因为每次调用CreateNodeAddMenu时都会重新构建并排序菜单
            Console.WriteLine("[TunnelExtension Node Menu] 脚本编译完成，下次打开菜单时将重新排序");
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            // 取消事件订阅，避免内存泄漏
            if (_TunnelExtensionScriptManager != null)
            {
                _TunnelExtensionScriptManager.ScriptsCompilationCompleted -= OnScriptsCompilationCompleted;
            }
        }

        /// <summary>
        /// 创建节点添加菜单
        /// </summary>
        public ContextMenu CreateNodeAddMenu(Action<string> onNodeSelected)
        {
            var menu = new ContextMenu();
            var menuItems = BuildTunnelExtensionMenuStructure();

            foreach (var item in menuItems)
            {
                var menuItem = CreateMenuItem(item, onNodeSelected);
                menu.Items.Add(menuItem);
            }

            // 如果没有脚本，添加提示信息
            if (menuItems.Count == 0)
            {
                var noScriptsItem = new MenuItem
                {
                    Header = "没有可用的TunnelExtension Scripts",
                    IsEnabled = false
                };
                menu.Items.Add(noScriptsItem);
            }

            return menu;
        }

        /// <summary>
        /// 构建TunnelExtension Scripts菜单结构
        /// </summary>
        private List<TunnelExtensionNodeMenuItem> BuildTunnelExtensionMenuStructure()
        {
            var rootItems = new List<TunnelExtensionNodeMenuItem>();

            // 从TunnelExtension Scripts管理器获取脚本信息
            var scripts = _TunnelExtensionScriptManager.GetAvailableTunnelExtensionScripts();

            // 按脚本文件路径构建文件夹结构
            foreach (var kvp in scripts)
            {
                var relativePath = kvp.Key;
                var scriptInfo = kvp.Value;

                AddTunnelExtensionScriptToMenuStructure(rootItems, relativePath, scriptInfo);
            }

            // 对根菜单项进行排序，确保文件夹在上方
            return SortMenuItems(rootItems);
        }

        /// <summary>
        /// 将TunnelExtension Script添加到菜单结构中
        /// </summary>
        private void AddTunnelExtensionScriptToMenuStructure(List<TunnelExtensionNodeMenuItem> rootItems, string relativePath, TunnelExtensionScriptInfo scriptInfo)
        {
            var pathParts = relativePath.Replace('\\', '/').Split('/');
            var currentLevel = rootItems;

            // 遍历路径的每一部分（除了最后的文件名）
            for (int i = 0; i < pathParts.Length - 1; i++)
            {
                var folderName = pathParts[i];
                var existingFolder = currentLevel.FirstOrDefault(item => item.Name == folderName && item.IsFolder);

                if (existingFolder == null)
                {
                    existingFolder = new TunnelExtensionNodeMenuItem
                    {
                        Name = folderName,
                        IsFolder = true,
                        Children = new List<TunnelExtensionNodeMenuItem>()
                    };
                    currentLevel.Add(existingFolder);
                }

                currentLevel = existingFolder.Children;
            }

            // 添加TunnelExtension Script节点
            var scriptItem = new TunnelExtensionNodeMenuItem
            {
                Name = scriptInfo.Name,
                Category = scriptInfo.Category,
                Description = scriptInfo.Description,
                Color = scriptInfo.Color,
                ScriptPath = scriptInfo.FilePath,
                IsFolder = false,
                ScriptInfo = scriptInfo
            };

            currentLevel.Add(scriptItem);
        }

        /// <summary>
        /// 创建菜单项
        /// </summary>
        private MenuItem CreateMenuItem(TunnelExtensionNodeMenuItem item, Action<string> onNodeSelected)
        {
            var menuItem = new MenuItem
            {
                Header = item.Name
            };

            if (item.IsFolder)
            {
                // 文件夹菜单项
                menuItem.FontWeight = FontWeights.Bold;

                // 对子项进行排序：文件夹在前，脚本在后，同类型按名称排序
                var sortedChildren = SortMenuItems(item.Children);
                
                foreach (var child in sortedChildren)
                {
                    var childMenuItem = CreateMenuItem(child, onNodeSelected);
                    menuItem.Items.Add(childMenuItem);
                }
            }
            else
            {
                // 脚本菜单项
                menuItem.Click += (s, e) =>
                {
                    onNodeSelected?.Invoke(item.Name); // 传递脚本名称而不是路径
                };

                // 设置工具提示
                if (!string.IsNullOrEmpty(item.Description))
                {
                    menuItem.ToolTip = item.Description;
                }

                // 符号节点应用专用样式
                if (item.ScriptInfo != null && item.ScriptInfo.IsSymbolNode)
                {
                    menuItem.Style = Application.Current.FindResource("SymbolNodeMenuItemStyle") as Style;
                }
            }

            return menuItem;
        }

        /// <summary>
        /// 显示节点添加菜单
        /// </summary>
        public void ShowNodeAddMenu(FrameworkElement targetElement, Action<string> onNodeSelected)
        {
            var menu = CreateNodeAddMenu(onNodeSelected);
            menu.PlacementTarget = targetElement;
            menu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
            menu.IsOpen = true;
        }

        /// <summary>
        /// 显示节点添加菜单（在指定位置）
        /// </summary>
        public void ShowNodeAddMenuAtPosition(FrameworkElement targetElement, Point position, Action<string> onNodeSelected)
        {
            var menu = CreateNodeAddMenu(onNodeSelected);
            menu.PlacementTarget = targetElement;
            menu.Placement = System.Windows.Controls.Primitives.PlacementMode.MousePoint;
            menu.HorizontalOffset = position.X;
            menu.VerticalOffset = position.Y;
            menu.IsOpen = true;
        }

        /// <summary>
        /// 对菜单项进行排序，文件夹始终在上方
        /// 使用快速排序算法实现高效排序
        /// </summary>
        /// <param name="items">要排序的菜单项列表</param>
        /// <returns>排序后的菜单项列表</returns>
        private List<TunnelExtensionNodeMenuItem> SortMenuItems(List<TunnelExtensionNodeMenuItem> items)
        {
            if (items == null || items.Count <= 1)
                return items;
                
            var result = new List<TunnelExtensionNodeMenuItem>(items);
            QuickSort(result, 0, result.Count - 1);
            return result;
        }
        
        /// <summary>
        /// 快速排序算法实现
        /// </summary>
        private void QuickSort(List<TunnelExtensionNodeMenuItem> items, int left, int right)
        {
            if (left < right)
            {
                int pivotIndex = Partition(items, left, right);
                QuickSort(items, left, pivotIndex - 1);
                QuickSort(items, pivotIndex + 1, right);
            }
        }
        
        /// <summary>
        /// 快速排序分区函数
        /// </summary>
        private int Partition(List<TunnelExtensionNodeMenuItem> items, int left, int right)
        {
            var pivot = items[right];
            int i = left - 1;
            
            for (int j = left; j < right; j++)
            {
                // 比较规则：1. 文件夹在前，非文件夹在后 2. 同类型按名称字母顺序排序
                if (CompareMenuItems(items[j], pivot) <= 0)
                {
                    i++;
                    Swap(items, i, j);
                }
            }
            
            Swap(items, i + 1, right);
            return i + 1;
        }
        
        /// <summary>
        /// 交换列表中两个元素的位置
        /// </summary>
        private void Swap(List<TunnelExtensionNodeMenuItem> items, int i, int j)
        {
            var temp = items[i];
            items[i] = items[j];
            items[j] = temp;
        }
        
        /// <summary>
        /// 比较两个菜单项
        /// </summary>
        /// <returns>
        /// 小于0：item1排在item2前面
        /// 等于0：item1和item2顺序相等
        /// 大于0：item1排在item2后面
        /// </returns>
        private int CompareMenuItems(TunnelExtensionNodeMenuItem item1, TunnelExtensionNodeMenuItem item2)
        {
            // 先比较是否为文件夹（文件夹排在前面）
            if (item1.IsFolder && !item2.IsFolder)
                return -1;
                
            if (!item1.IsFolder && item2.IsFolder)
                return 1;
                
            // 同为文件夹或同为文件，按名称排序（不区分大小写）
            return string.Compare(item1.Name, item2.Name, StringComparison.CurrentCultureIgnoreCase);
        }
    }

    /// <summary>
    /// TunnelExtension Scripts节点菜单项
    /// </summary>
    public class TunnelExtensionNodeMenuItem
    {
        public string Name { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Color { get; set; } = string.Empty;
        public string ScriptPath { get; set; } = string.Empty;
        public bool IsFolder { get; set; }
        public List<TunnelExtensionNodeMenuItem> Children { get; set; } = new();
        public TunnelExtensionScriptInfo? ScriptInfo { get; set; }
    }
}
