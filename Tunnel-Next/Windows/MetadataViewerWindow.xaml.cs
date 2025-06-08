using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using Tunnel_Next.Models;
using Tunnel_Next.Services.ImageProcessing;

namespace Tunnel_Next.Windows
{
    /// <summary>
    /// 元数据查看器窗口
    /// </summary>
    public partial class MetadataViewerWindow : Window
    {
        private readonly Dictionary<string, object> _metadata;
        private readonly Node _node;
        private readonly MetadataManager _metadataManager;

        public MetadataViewerWindow(Node node, Dictionary<string, object> metadata)
        {
            InitializeComponent();
            _node = node;
            _metadata = metadata ?? new Dictionary<string, object>();
            _metadataManager = new MetadataManager();

            InitializeWindow();
            PopulateMetadataTree();
            PopulateDetailsPanel();
        }

        /// <summary>
        /// 初始化窗口
        /// </summary>
        private void InitializeWindow()
        {
            TitleTextBlock.Text = $"元数据查看 - {_node.Title}";
            SubtitleTextBlock.Text = $"节点: {_node.Title} (ID: {_node.Id})";

            // 设置窗口图标（如果需要）
            // Icon = ...
        }

        /// <summary>
        /// 填充元数据树形视图
        /// </summary>
        private void PopulateMetadataTree()
        {
            var rootItems = new ObservableCollection<MetadataTreeItem>();

            foreach (var kvp in _metadata)
            {
                var item = CreateTreeItem(kvp.Key, kvp.Value);
                rootItems.Add(item);
            }

            MetadataTreeView.ItemsSource = rootItems;

            // 展开第一级节点
            foreach (var item in rootItems)
            {
                var container = MetadataTreeView.ItemContainerGenerator.ContainerFromItem(item) as TreeViewItem;
                if (container != null)
                {
                    container.IsExpanded = true;
                }
            }
        }

        /// <summary>
        /// 创建树形项目
        /// </summary>
        private MetadataTreeItem CreateTreeItem(string key, object? value)
        {
            var item = new MetadataTreeItem { Key = key };

            if (value is Dictionary<string, object> dict)
            {
                item.Value = $"({dict.Count} 项)";
                item.Type = "字典";
                item.Children = new ObservableCollection<MetadataTreeItem>();

                foreach (var kvp in dict)
                {
                    item.Children.Add(CreateTreeItem(kvp.Key, kvp.Value));
                }
            }
            else if (value is List<object> list)
            {
                item.Value = $"({list.Count} 项)";
                item.Type = "列表";
                item.Children = new ObservableCollection<MetadataTreeItem>();

                for (int i = 0; i < list.Count; i++)
                {
                    item.Children.Add(CreateTreeItem($"[{i}]", list[i]));
                }
            }
            else if (value is System.Text.Json.JsonElement jsonElement)
            {
                // 处理JsonElement类型
                switch (jsonElement.ValueKind)
                {
                    case JsonValueKind.Object:
                        var objDict = JsonSerializer.Deserialize<Dictionary<string, object>>(jsonElement.GetRawText());
                        return CreateTreeItem(key, objDict);
                    case JsonValueKind.Array:
                        var arrayList = JsonSerializer.Deserialize<List<object>>(jsonElement.GetRawText());
                        return CreateTreeItem(key, arrayList);
                    default:
                        item.Value = jsonElement.ToString();
                        item.Type = jsonElement.ValueKind.ToString();
                        break;
                }
            }
            else
            {
                item.Value = value?.ToString() ?? "null";
                item.Type = value?.GetType().Name ?? "null";
            }

            return item;
        }

        /// <summary>
        /// 填充详细信息面板
        /// </summary>
        private void PopulateDetailsPanel()
        {
            PopulateBasicInfo();
            PopulateNodePath();
            PopulateProcessingHistory();
            PopulateCustomMetadata();
        }

        /// <summary>
        /// 填充基本信息
        /// </summary>
        private void PopulateBasicInfo()
        {
            BasicInfoGrid.Children.Clear();
            BasicInfoGrid.RowDefinitions.Clear();

            var basicInfo = new Dictionary<string, string>
            {
                ["节点ID"] = _node.Id.ToString(),
                ["节点标题"] = _node.Title,
                ["脚本路径"] = _node.ScriptPath ?? "无",
                ["创建时间"] = _metadata.ContainsKey("创建时间") ? _metadata["创建时间"]?.ToString() ?? "未知" : "未知",
                ["元数据键数量"] = _metadata.Count.ToString()
            };

            int row = 0;
            foreach (var kvp in basicInfo)
            {
                BasicInfoGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

                var label = new TextBlock
                {
                    Text = kvp.Key + ":",
                    Style = (Style)FindResource("InfoLabelStyle")
                };
                Grid.SetRow(label, row);
                Grid.SetColumn(label, 0);
                BasicInfoGrid.Children.Add(label);

                var value = new TextBlock
                {
                    Text = kvp.Value,
                    Style = (Style)FindResource("InfoValueStyle")
                };
                Grid.SetRow(value, row);
                Grid.SetColumn(value, 1);
                BasicInfoGrid.Children.Add(value);

                row++;
            }
        }

        /// <summary>
        /// 填充节点路径
        /// </summary>
        private void PopulateNodePath()
        {
            NodePathPanel.Children.Clear();

            if (_metadata.TryGetValue("节点路径", out var nodePathObj) && nodePathObj is List<object> nodePath)
            {
                if (nodePath.Count == 0)
                {
                    var noPathText = new TextBlock
                    {
                        Text = "无节点路径记录",
                        Style = (Style)FindResource("InfoValueStyle")
                    };
                    NodePathPanel.Children.Add(noPathText);
                    return;
                }

                for (int i = 0; i < nodePath.Count; i++)
                {
                    var pathItem = nodePath[i];
                    var pathText = $"{i + 1}. ";

                    if (pathItem is Dictionary<string, object> pathDict)
                    {
                        var nodeTitle = pathDict.ContainsKey("节点标题") ? pathDict["节点标题"]?.ToString() : "未知";
                        var nodeId = pathDict.ContainsKey("节点ID") ? pathDict["节点ID"]?.ToString() : "N/A";
                        pathText += $"{nodeTitle} (ID: {nodeId})";
                    }
                    else
                    {
                        pathText += pathItem?.ToString() ?? "未知";
                    }

                    var pathBlock = new TextBlock
                    {
                        Text = pathText,
                        Style = (Style)FindResource("InfoValueStyle"),
                        Margin = new Thickness(0, 2, 0, 0)
                    };
                    NodePathPanel.Children.Add(pathBlock);
                }
            }
            else
            {
                var noPathText = new TextBlock
                {
                    Text = "无节点路径记录",
                    Style = (Style)FindResource("InfoValueStyle")
                };
                NodePathPanel.Children.Add(noPathText);
            }
        }

        /// <summary>
        /// 填充处理历史
        /// </summary>
        private void PopulateProcessingHistory()
        {
            ProcessingHistoryPanel.Children.Clear();

            if (_metadata.TryGetValue("处理历史", out var historyObj) && historyObj is List<object> history)
            {
                if (history.Count == 0)
                {
                    var noHistoryText = new TextBlock
                    {
                        Text = "无处理历史记录",
                        Style = (Style)FindResource("InfoValueStyle")
                    };
                    ProcessingHistoryPanel.Children.Add(noHistoryText);
                    return;
                }

                for (int i = 0; i < history.Count; i++)
                {
                    var historyItem = history[i];
                    var historyText = $"{i + 1}. ";

                    if (historyItem is Dictionary<string, object> historyDict)
                    {
                        var operation = historyDict.ContainsKey("操作") ? historyDict["操作"]?.ToString() : "未知操作";
                        historyText += operation;

                        if (historyDict.ContainsKey("详情") && historyDict["详情"] is Dictionary<string, object> details)
                        {
                            var scriptType = details.ContainsKey("脚本类型") ? details["脚本类型"]?.ToString() : null;
                            if (!string.IsNullOrEmpty(scriptType))
                            {
                                historyText += $" ({scriptType})";
                            }
                        }
                    }
                    else
                    {
                        historyText += historyItem?.ToString() ?? "未知";
                    }

                    var historyBlock = new TextBlock
                    {
                        Text = historyText,
                        Style = (Style)FindResource("InfoValueStyle"),
                        Margin = new Thickness(0, 2, 0, 0)
                    };
                    ProcessingHistoryPanel.Children.Add(historyBlock);
                }
            }
            else
            {
                var noHistoryText = new TextBlock
                {
                    Text = "无处理历史记录",
                    Style = (Style)FindResource("InfoValueStyle")
                };
                ProcessingHistoryPanel.Children.Add(noHistoryText);
            }
        }

        /// <summary>
        /// 填充自定义元数据
        /// </summary>
        private void PopulateCustomMetadata()
        {
            CustomMetadataPanel.Children.Clear();

            var systemKeys = new HashSet<string> { "创建时间", "节点路径", "处理历史", "节点ID", "节点标题", "脚本路径" };
            var customMetadata = _metadata.Where(kvp => !systemKeys.Contains(kvp.Key)).ToList();

            if (customMetadata.Count == 0)
            {
                var noCustomText = new TextBlock
                {
                    Text = "无自定义元数据",
                    Style = (Style)FindResource("InfoValueStyle")
                };
                CustomMetadataPanel.Children.Add(noCustomText);
                return;
            }

            foreach (var kvp in customMetadata)
            {
                var keyText = new TextBlock
                {
                    Text = kvp.Key + ":",
                    Style = (Style)FindResource("InfoLabelStyle")
                };
                CustomMetadataPanel.Children.Add(keyText);

                var valueText = kvp.Value switch
                {
                    Dictionary<string, object> dict => $"字典 (长度: {dict.Count})",
                    List<object> list => $"列表 (长度: {list.Count})",
                    _ => kvp.Value?.ToString() ?? "null"
                };

                var valueBlock = new TextBlock
                {
                    Text = valueText,
                    Style = (Style)FindResource("InfoValueStyle"),
                    Margin = new Thickness(20, 0, 0, 10)
                };
                CustomMetadataPanel.Children.Add(valueBlock);
            }
        }

        /// <summary>
        /// 复制到剪贴板按钮点击事件
        /// </summary>
        private void CopyButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var json = _metadataManager.SerializeMetadata(_metadata);
                Clipboard.SetText(json);
                MessageBox.Show("元数据已复制到剪贴板", "复制成功", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"复制失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 导出到文件按钮点击事件
        /// </summary>
        private void ExportButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var saveDialog = new SaveFileDialog
                {
                    Title = "导出元数据",
                    Filter = "JSON文件 (*.json)|*.json|文本文件 (*.txt)|*.txt|所有文件 (*.*)|*.*",
                    FileName = $"metadata_{_node.Title.Replace("/", "_").Replace("\\", "_")}_ID{_node.Id}.json"
                };

                if (saveDialog.ShowDialog() == true)
                {
                    var json = _metadataManager.SerializeMetadata(_metadata);
                    File.WriteAllText(saveDialog.FileName, json);
                    MessageBox.Show($"元数据已导出到:\n{saveDialog.FileName}", "导出成功", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"导出失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 关闭按钮点击事件
        /// </summary>
        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }

    /// <summary>
    /// 元数据树形项目
    /// </summary>
    public class MetadataTreeItem
    {
        public string Key { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public ObservableCollection<MetadataTreeItem>? Children { get; set; }
    }
}
