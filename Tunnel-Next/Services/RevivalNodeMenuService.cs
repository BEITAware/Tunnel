using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Tunnel_Next.Services.Scripting;

namespace Tunnel_Next.Services
{
    /// <summary>
    /// Revival Scripts节点菜单服务 - 基于Revival Scripts系统的节点菜单
    /// </summary>
    public class RevivalNodeMenuService : INodeMenuService
    {
        private readonly RevivalScriptManager _revivalScriptManager;

        public RevivalNodeMenuService(RevivalScriptManager revivalScriptManager)
        {
            _revivalScriptManager = revivalScriptManager ?? throw new ArgumentNullException(nameof(revivalScriptManager));
        }

        /// <summary>
        /// 创建节点添加菜单
        /// </summary>
        public ContextMenu CreateNodeAddMenu(Action<string> onNodeSelected)
        {
            var menu = new ContextMenu();
            var menuItems = BuildRevivalMenuStructure();

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
                    Header = "没有可用的Revival Scripts",
                    IsEnabled = false
                };
                menu.Items.Add(noScriptsItem);
            }

            return menu;
        }

        /// <summary>
        /// 构建Revival Scripts菜单结构
        /// </summary>
        private List<RevivalNodeMenuItem> BuildRevivalMenuStructure()
        {
            var rootItems = new List<RevivalNodeMenuItem>();

            // 从Revival Scripts管理器获取脚本信息
            var scripts = _revivalScriptManager.GetAvailableRevivalScripts();

            // 按脚本文件路径构建文件夹结构
            foreach (var kvp in scripts)
            {
                var relativePath = kvp.Key;
                var scriptInfo = kvp.Value;

                AddRevivalScriptToMenuStructure(rootItems, relativePath, scriptInfo);
            }

            return rootItems;
        }

        /// <summary>
        /// 将Revival Script添加到菜单结构中
        /// </summary>
        private void AddRevivalScriptToMenuStructure(List<RevivalNodeMenuItem> rootItems, string relativePath, RevivalScriptInfo scriptInfo)
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
                    existingFolder = new RevivalNodeMenuItem
                    {
                        Name = folderName,
                        IsFolder = true,
                        Children = new List<RevivalNodeMenuItem>()
                    };
                    currentLevel.Add(existingFolder);
                }

                currentLevel = existingFolder.Children;
            }

            // 添加Revival Script节点
            var scriptItem = new RevivalNodeMenuItem
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
        private MenuItem CreateMenuItem(RevivalNodeMenuItem item, Action<string> onNodeSelected)
        {
            var menuItem = new MenuItem
            {
                Header = item.Name
            };

            if (item.IsFolder)
            {
                // 文件夹菜单项
                menuItem.FontWeight = FontWeights.Bold;

                foreach (var child in item.Children.OrderBy(c => c.IsFolder ? 0 : 1).ThenBy(c => c.Name))
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
    }

    /// <summary>
    /// Revival Scripts节点菜单项
    /// </summary>
    public class RevivalNodeMenuItem
    {
        public string Name { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Color { get; set; } = string.Empty;
        public string ScriptPath { get; set; } = string.Empty;
        public bool IsFolder { get; set; }
        public List<RevivalNodeMenuItem> Children { get; set; } = new();
        public RevivalScriptInfo? ScriptInfo { get; set; }
    }
}
