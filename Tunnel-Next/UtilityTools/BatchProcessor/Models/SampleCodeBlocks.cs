using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Tunnel_Next.UtilityTools.BatchProcessor.Models
{
    /// <summary>
    /// 示例积木块：节点图序列
    /// </summary>
    public class NodeGraphSequenceBlock : CodeBlockBase
    {
        private string _nodeGraphPath = string.Empty;
        private int _repeatCount = 1;
        private bool _processInParallel = false;

        public override CodeBlockType BlockType => CodeBlockType.NodeGraphSequence;

        public string NodeGraphPath
        {
            get => _nodeGraphPath;
            set
            {
                if (_nodeGraphPath != value)
                {
                    var oldValue = _nodeGraphPath;
                    _nodeGraphPath = value;
                    OnPropertyChanged(nameof(NodeGraphPath));
                    OnSettingsChanged(nameof(NodeGraphPath), oldValue, value);
                }
            }
        }

        public int RepeatCount
        {
            get => _repeatCount;
            set
            {
                if (_repeatCount != value)
                {
                    var oldValue = _repeatCount;
                    _repeatCount = value;
                    OnPropertyChanged(nameof(RepeatCount));
                    OnSettingsChanged(nameof(RepeatCount), oldValue, value);
                }
            }
        }

        public bool ProcessInParallel
        {
            get => _processInParallel;
            set
            {
                if (_processInParallel != value)
                {
                    var oldValue = _processInParallel;
                    _processInParallel = value;
                    OnPropertyChanged(nameof(ProcessInParallel));
                    OnSettingsChanged(nameof(ProcessInParallel), oldValue, value);
                }
            }
        }

        public NodeGraphSequenceBlock()
        {
            DisplayName = "节点图序列";
            Description = "执行指定的节点图文件";
            InitializeMetadata();
        }

        public override FrameworkElement CreateSettingsControl()
        {
            var panel = new StackPanel();

            // 节点图路径设定
            var pathLabel = new TextBlock { Text = "节点图路径:", Margin = new Thickness(0, 5, 0, 2) };
            panel.Children.Add(pathLabel);

            var pathTextBox = new TextBox 
            { 
                Text = NodeGraphPath,
                Margin = new Thickness(0, 0, 0, 5)
            };
            pathTextBox.TextChanged += (s, e) => NodeGraphPath = pathTextBox.Text;
            panel.Children.Add(pathTextBox);

            // 重复次数设定
            var countLabel = new TextBlock { Text = "重复次数:", Margin = new Thickness(0, 5, 0, 2) };
            panel.Children.Add(countLabel);

            var countTextBox = new TextBox 
            { 
                Text = RepeatCount.ToString(),
                Margin = new Thickness(0, 0, 0, 5)
            };
            countTextBox.TextChanged += (s, e) => 
            {
                if (int.TryParse(countTextBox.Text, out var count))
                    RepeatCount = count;
            };
            panel.Children.Add(countTextBox);

            // 并行处理设定
            var parallelCheckBox = new CheckBox 
            { 
                Content = "并行处理",
                IsChecked = ProcessInParallel,
                Margin = new Thickness(0, 5, 0, 5)
            };
            parallelCheckBox.Checked += (s, e) => ProcessInParallel = true;
            parallelCheckBox.Unchecked += (s, e) => ProcessInParallel = false;
            panel.Children.Add(parallelCheckBox);

            return panel;
        }

        public override Dictionary<string, object> SerializeSettings()
        {
            return new Dictionary<string, object>
            {
                [nameof(NodeGraphPath)] = NodeGraphPath,
                [nameof(RepeatCount)] = RepeatCount,
                [nameof(ProcessInParallel)] = ProcessInParallel
            };
        }

        public override void DeserializeSettings(Dictionary<string, object> data)
        {
            if (data.TryGetValue(nameof(NodeGraphPath), out var pathValue) && pathValue is string path)
                NodeGraphPath = path;

            if (data.TryGetValue(nameof(RepeatCount), out var countValue) && countValue is int count)
                RepeatCount = count;

            if (data.TryGetValue(nameof(ProcessInParallel), out var parallelValue) && parallelValue is bool parallel)
                ProcessInParallel = parallel;
        }

        public override bool ValidateSettings()
        {
            return !string.IsNullOrEmpty(NodeGraphPath) && RepeatCount > 0;
        }

        public override string GetSettingsSummary()
        {
            var summary = $"路径: {(string.IsNullOrEmpty(NodeGraphPath) ? "未设定" : System.IO.Path.GetFileName(NodeGraphPath))}";
            if (RepeatCount > 1)
                summary += $", 重复: {RepeatCount}次";
            if (ProcessInParallel)
                summary += ", 并行处理";
            return summary;
        }

        public override string GetTypeIdentifier()
        {
            return "NodeGraphSequence";
        }

        #region 元数据方法

        public override Dictionary<string, object> InjectMetadata(Dictionary<string, object> currentMetadata)
        {
            // 覆盖注入节点图相关信息
            if (!string.IsNullOrEmpty(NodeGraphPath))
            {
                currentMetadata["节点图路径"] = NodeGraphPath;
                currentMetadata["节点图名称"] = System.IO.Path.GetFileNameWithoutExtension(NodeGraphPath);
                currentMetadata["节点图存在"] = System.IO.File.Exists(NodeGraphPath);
            }

            // 覆盖注入处理配置
            currentMetadata["重复次数"] = RepeatCount;
            currentMetadata["并行处理"] = ProcessInParallel;
            currentMetadata["积木块类型"] = "节点图序列";

            return currentMetadata;
        }

        public override void ExtractMetadata(Dictionary<string, object> upstreamMetadata)
        {
            // 从上游提取有用的信息并存储到当前积木块的元数据中
            if (upstreamMetadata.ContainsKey("处理历史"))
            {
                var history = upstreamMetadata["处理历史"] as List<Dictionary<string, object>>;
                InjectMetadataValue("上游处理步骤数", history?.Count ?? 0);
            }

            // 提取文件相关信息
            if (upstreamMetadata.ContainsKey("文件夹路径"))
            {
                InjectMetadataValue("上游文件夹", upstreamMetadata["文件夹路径"]);
            }

            // 提取其他有用信息
            if (upstreamMetadata.ContainsKey("创建时间"))
            {
                InjectMetadataValue("上游创建时间", upstreamMetadata["创建时间"]);
            }
        }

        public override Dictionary<string, object> GenerateMetadata(Dictionary<string, object> currentMetadata)
        {
            // 生成积木块特定的元数据（覆盖现有值）
            currentMetadata["积木块状态"] = ValidateSettings() ? "有效" : "无效";
            currentMetadata["配置摘要"] = GetSettingsSummary();
            currentMetadata["预计执行时间"] = RepeatCount * (ProcessInParallel ? 0.5 : 1.0); // 模拟计算
            currentMetadata["最后验证时间"] = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

            return currentMetadata;
        }

        public override Dictionary<string, object> ProcessMetadata(Dictionary<string, object> upstreamMetadata)
        {
            // 使用默认的元数据处理流程
            return DefaultProcessMetadata(upstreamMetadata);
        }

        #endregion
    }

    /// <summary>
    /// 示例积木块：文件序列
    /// </summary>
    public class FileSequenceBlock : CodeBlockBase
    {
        private string _folderPath = string.Empty;
        private string _filePattern = "*.*";
        private bool _includeSubfolders = false;

        public override CodeBlockType BlockType => CodeBlockType.FileSequence;

        public string FolderPath
        {
            get => _folderPath;
            set
            {
                if (_folderPath != value)
                {
                    var oldValue = _folderPath;
                    _folderPath = value;
                    OnPropertyChanged(nameof(FolderPath));
                    OnSettingsChanged(nameof(FolderPath), oldValue, value);
                }
            }
        }

        public string FilePattern
        {
            get => _filePattern;
            set
            {
                if (_filePattern != value)
                {
                    var oldValue = _filePattern;
                    _filePattern = value;
                    OnPropertyChanged(nameof(FilePattern));
                    OnSettingsChanged(nameof(FilePattern), oldValue, value);
                }
            }
        }

        public bool IncludeSubfolders
        {
            get => _includeSubfolders;
            set
            {
                if (_includeSubfolders != value)
                {
                    var oldValue = _includeSubfolders;
                    _includeSubfolders = value;
                    OnPropertyChanged(nameof(IncludeSubfolders));
                    OnSettingsChanged(nameof(IncludeSubfolders), oldValue, value);
                }
            }
        }

        public FileSequenceBlock()
        {
            DisplayName = "文件序列";
            Description = "遍历指定文件夹中的文件";
            InitializeMetadata();
        }

        public override FrameworkElement CreateSettingsControl()
        {
            var panel = new StackPanel();

            // 文件夹路径设定
            var folderLabel = new TextBlock { Text = "文件夹路径:", Margin = new Thickness(0, 5, 0, 2) };
            panel.Children.Add(folderLabel);

            var folderTextBox = new TextBox 
            { 
                Text = FolderPath,
                Margin = new Thickness(0, 0, 0, 5)
            };
            folderTextBox.TextChanged += (s, e) => FolderPath = folderTextBox.Text;
            panel.Children.Add(folderTextBox);

            // 文件模式设定
            var patternLabel = new TextBlock { Text = "文件模式:", Margin = new Thickness(0, 5, 0, 2) };
            panel.Children.Add(patternLabel);

            var patternTextBox = new TextBox 
            { 
                Text = FilePattern,
                Margin = new Thickness(0, 0, 0, 5)
            };
            patternTextBox.TextChanged += (s, e) => FilePattern = patternTextBox.Text;
            panel.Children.Add(patternTextBox);

            // 包含子文件夹设定
            var subfolderCheckBox = new CheckBox 
            { 
                Content = "包含子文件夹",
                IsChecked = IncludeSubfolders,
                Margin = new Thickness(0, 5, 0, 5)
            };
            subfolderCheckBox.Checked += (s, e) => IncludeSubfolders = true;
            subfolderCheckBox.Unchecked += (s, e) => IncludeSubfolders = false;
            panel.Children.Add(subfolderCheckBox);

            return panel;
        }

        public override Dictionary<string, object> SerializeSettings()
        {
            return new Dictionary<string, object>
            {
                [nameof(FolderPath)] = FolderPath,
                [nameof(FilePattern)] = FilePattern,
                [nameof(IncludeSubfolders)] = IncludeSubfolders
            };
        }

        public override void DeserializeSettings(Dictionary<string, object> data)
        {
            if (data.TryGetValue(nameof(FolderPath), out var folderValue) && folderValue is string folder)
                FolderPath = folder;

            if (data.TryGetValue(nameof(FilePattern), out var patternValue) && patternValue is string pattern)
                FilePattern = pattern;

            if (data.TryGetValue(nameof(IncludeSubfolders), out var subfolderValue) && subfolderValue is bool subfolder)
                IncludeSubfolders = subfolder;
        }

        public override bool ValidateSettings()
        {
            return !string.IsNullOrEmpty(FolderPath) && !string.IsNullOrEmpty(FilePattern);
        }

        public override string GetSettingsSummary()
        {
            var summary = $"文件夹: {(string.IsNullOrEmpty(FolderPath) ? "未设定" : System.IO.Path.GetFileName(FolderPath))}";
            if (!string.IsNullOrEmpty(FilePattern) && FilePattern != "*.*")
                summary += $", 模式: {FilePattern}";
            if (IncludeSubfolders)
                summary += ", 含子文件夹";
            return summary;
        }

        public override string GetTypeIdentifier()
        {
            return "FileSequence";
        }

        #region 元数据方法

        public override Dictionary<string, object> InjectMetadata(Dictionary<string, object> currentMetadata)
        {
            // 覆盖注入文件夹相关信息
            if (!string.IsNullOrEmpty(FolderPath))
            {
                currentMetadata["文件夹路径"] = FolderPath;
                currentMetadata["文件夹名称"] = System.IO.Path.GetFileName(FolderPath);
                currentMetadata["文件夹存在"] = System.IO.Directory.Exists(FolderPath);

                // 如果文件夹存在，获取文件数量
                if (System.IO.Directory.Exists(FolderPath))
                {
                    try
                    {
                        var searchOption = IncludeSubfolders ? System.IO.SearchOption.AllDirectories : System.IO.SearchOption.TopDirectoryOnly;
                        var fileCount = System.IO.Directory.GetFiles(FolderPath, FilePattern, searchOption).Length;
                        currentMetadata["文件数量"] = fileCount;
                    }
                    catch
                    {
                        currentMetadata["文件数量"] = "无法获取";
                    }
                }
            }

            // 覆盖注入处理配置
            currentMetadata["文件模式"] = FilePattern;
            currentMetadata["包含子文件夹"] = IncludeSubfolders;
            currentMetadata["积木块类型"] = "文件序列";

            return currentMetadata;
        }

        public override void ExtractMetadata(Dictionary<string, object> upstreamMetadata)
        {
            // 从上游提取有用的信息并存储到当前积木块的元数据中
            if (upstreamMetadata.ContainsKey("处理历史"))
            {
                var history = upstreamMetadata["处理历史"] as List<Dictionary<string, object>>;
                InjectMetadataValue("上游处理步骤数", history?.Count ?? 0);
            }

            // 提取节点图相关信息
            if (upstreamMetadata.ContainsKey("节点图路径"))
            {
                InjectMetadataValue("上游节点图", upstreamMetadata["节点图路径"]);
            }

            // 提取其他有用信息
            if (upstreamMetadata.ContainsKey("重复次数"))
            {
                InjectMetadataValue("上游重复次数", upstreamMetadata["重复次数"]);
            }
        }

        public override Dictionary<string, object> GenerateMetadata(Dictionary<string, object> currentMetadata)
        {
            // 生成积木块特定的元数据（覆盖现有值）
            currentMetadata["积木块状态"] = ValidateSettings() ? "有效" : "无效";
            currentMetadata["配置摘要"] = GetSettingsSummary();
            currentMetadata["扫描深度"] = IncludeSubfolders ? "递归" : "单层";
            currentMetadata["最后验证时间"] = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

            return currentMetadata;
        }

        public override Dictionary<string, object> ProcessMetadata(Dictionary<string, object> upstreamMetadata)
        {
            // 使用默认的元数据处理流程
            return DefaultProcessMetadata(upstreamMetadata);
        }

        #endregion
    }
}
