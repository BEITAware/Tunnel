using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using Tunnel_Next.Controls;
using Tunnel_Next.Models;
using Tunnel_Next.Services;
using Tunnel_Next.Services.Scripting;
using Tunnel_Next.ViewModels;
using System.Text.RegularExpressions;
using System.Diagnostics;

namespace Tunnel_Next.Windows
{
    /// <summary>
    /// 新建节点图窗口
    /// </summary>
    public partial class NewNodeGraphWindow : Window
    {
        public ObservableCollection<TemplateItem> Templates { get; } = new();

        private TemplateItem? SelectedTemplate => TemplateListBox.SelectedItem as TemplateItem;

        // 最近一次有效选择的模板（即便 ListBox 失焦或取消选择也保留）
        private TemplateItem? _lastTemplate;

        public NewNodeGraphWindow()
        {
            InitializeComponent();

            LoadTemplates();

            TemplateListBox.ItemsSource = Templates;
            if (Templates.Count > 0)
            {
                TemplateListBox.SelectedIndex = 0;
                _lastTemplate = Templates[0];
            }

            // 生成初始名称
            UpdateDefaultName();
        }

        /// <summary>
        /// 加载模板目录下的所有模板
        /// </summary>
        private void LoadTemplates()
        {
            Templates.Clear();

            try
            {
                var documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                var templatesRoot = Path.Combine(documentsPath, "TNX", "Resources", "Templates");

                if (!Directory.Exists(templatesRoot)) return;

                foreach (var dir in Directory.GetDirectories(templatesRoot))
                {
                    var templateName = Path.GetFileName(dir);
                    var thumbnailPath = Path.Combine(dir, "thumbnail.png");
                    var nodeGraphPath = Directory.GetFiles(dir, "*.nodegraph").FirstOrDefault();
                    if (nodeGraphPath == null) continue; // 必须存在节点图文件

                    BitmapImage? thumbnail = null;
                    if (File.Exists(thumbnailPath))
                    {
                        thumbnail = new BitmapImage(new Uri(thumbnailPath));
                    }

                    Templates.Add(new TemplateItem(templateName, templateName, nodeGraphPath, thumbnail));
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"加载模板时发生错误: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void UpdateDefaultName()
        {
            var templateItem = _lastTemplate ?? SelectedTemplate;
            if (templateItem == null) return;
            var templateName = SanitizeFileName(templateItem.DisplayName);
            var projectsDir = GetProjectsDirectory();
            if (!Directory.Exists(projectsDir))
            {
                try { Directory.CreateDirectory(projectsDir); } catch { }
            }

            int index = 1;
            string candidate;
            do
            {
                candidate = $"{templateName}({index})";
                var candidatePath = Path.Combine(projectsDir, candidate + ".nodegraph");
                if (!File.Exists(candidatePath)) break;
                index++;
            } while (index < 10000);

            GraphNameTextBox.Text = candidate;
        }

        private static string SanitizeFileName(string name)
        {
            var invalid = new string(Path.GetInvalidFileNameChars());
            var regex = new Regex("[" + Regex.Escape(invalid) + "]");
            return regex.Replace(name, "_");
        }

        /// <summary>
        /// 获取项目目录路径
        /// </summary>
        private string GetProjectsDirectory()
        {
            try
            {
                var config = new WorkFolderConfig();
                return Path.Combine(config.WorkFolder, "Projects");
            }
            catch
            {
                // 如果配置系统失败，使用硬编码的回退路径
                var documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                return Path.Combine(documentsPath, "TNX", "Projects");
            }
        }

        private async void TemplateListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
#if DEBUG
            Debug.WriteLine("[DEBUG] SelectionChanged called");
#endif
            if (SelectedTemplate != null)
            {
                _lastTemplate = SelectedTemplate;
#if DEBUG
                Debug.WriteLine($"[DEBUG] _lastTemplate set to {SelectedTemplate.DisplayName}");
#endif
            }
            else
            {
#if DEBUG
                Debug.WriteLine("[DEBUG] SelectedTemplate is null");
#endif
            }

            if (_lastTemplate == null) return;

            // 更新默认名称
            UpdateDefaultName();

            try
            {
                // 反序列化节点图
                var tempScriptsFolder = Path.Combine(Path.GetTempPath(), "TempScripts");
                var tempResourcesFolder = Path.Combine(Path.GetTempPath(), "TempResources");
                Directory.CreateDirectory(tempScriptsFolder);
                Directory.CreateDirectory(tempResourcesFolder);
                var revivalScriptManager = new RevivalScriptManager(tempScriptsFolder, tempResourcesFolder);

                var deserializer = new NodeGraphDeserializer(revivalScriptManager);
                var json = await File.ReadAllTextAsync(_lastTemplate.NodeGraphPath);
                var nodeGraph = deserializer.DeserializeNodeGraph(json);

                var viewModel = new NodeEditorViewModel(revivalScriptManager);
                await viewModel.LoadNodeGraphAsync(nodeGraph);

                PreviewEditor.IsReadOnly = true;
                PreviewEditor.DataContext = viewModel;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"加载模板预览失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CreateButton_Click(object sender, RoutedEventArgs e)
        {
#if DEBUG
            Debug.WriteLine($"[DEBUG] CreateButton_Click invoked");
            Debug.WriteLine($"[DEBUG] _lastTemplate null? {_lastTemplate == null}");
            Debug.WriteLine($"[DEBUG] SelectedTemplate null? {SelectedTemplate == null}");
            Debug.WriteLine($"[DEBUG] Templates count: {Templates.Count}");
#endif
            if (SelectedTemplate == null)
            {
                MessageBox.Show(this, "请先选择一个模板。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var name = string.IsNullOrWhiteSpace(GraphNameTextBox.Text) ? SanitizeFileName(_lastTemplate.DisplayName) : SanitizeFileName(GraphNameTextBox.Text);

            // 如果用户未填写，则重新生成唯一名称
            if (string.IsNullOrWhiteSpace(GraphNameTextBox.Text))
            {
                GraphNameTextBox.Text = name;
            }

            if (_lastTemplate == null)
            {
#if DEBUG
                Debug.WriteLine("[DEBUG] _lastTemplate is null at click. Attempting fallback...");
#endif
                // 尝试回退到列表首项
                _lastTemplate = Templates.FirstOrDefault();
                if (_lastTemplate == null)
                {
                    MessageBox.Show(this, "请先选择一个模板。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
#if DEBUG
                    Debug.WriteLine("[DEBUG] Still null after fallback. Abort.");
#endif
                    return;
                }
            }

            // 使用所选模板创建新节点图
#if DEBUG
            Debug.WriteLine($"[DEBUG] Using template path: {_lastTemplate.NodeGraphPath}, name: {name}");
#endif
            this.Tag = new Tuple<string, string>(_lastTemplate.NodeGraphPath, name);
            DialogResult = true;
            Close();
        }

        public class TemplateItem
        {
            public string DisplayName { get; }
            public string Description { get; }
            public string NodeGraphPath { get; }
            public BitmapImage? Thumbnail { get; }

            public TemplateItem(string displayName, string description, string nodeGraphPath, BitmapImage? thumbnail)
            {
                DisplayName = displayName;
                Description = description;
                NodeGraphPath = nodeGraphPath;
                Thumbnail = thumbnail;
            }
        }
    }
} 