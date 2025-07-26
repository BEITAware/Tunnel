using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
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

            var uniqueName = GenerateUniqueName(templateName);
            GraphNameTextBox.Text = uniqueName;
        }

        /// <summary>
        /// 生成唯一的节点图名称，避免与现有文件和已打开文档重复
        /// </summary>
        /// <param name="baseName">基础名称</param>
        /// <returns>唯一的名称</returns>
        private string GenerateUniqueName(string baseName)
        {
            var projectsDir = GetProjectsDirectory();
            if (!Directory.Exists(projectsDir))
            {
                try { Directory.CreateDirectory(projectsDir); } catch { }
            }

            int index = 1;
            string candidate;
            do
            {
                candidate = $"{baseName}({index})";
                if (!IsNameConflicted(candidate, projectsDir))
                    break;
                index++;
            } while (index < 10000);

            return candidate;
        }

        /// <summary>
        /// 检查名称是否与现有文件或已打开文档冲突
        /// </summary>
        /// <param name="name">要检查的名称</param>
        /// <param name="projectsDir">项目目录</param>
        /// <returns>是否存在冲突</returns>
        private bool IsNameConflicted(string name, string projectsDir)
        {
            // 检查文件系统中的冲突
            var candidatePath = Path.Combine(projectsDir, name + ".nodegraph");
            if (File.Exists(candidatePath))
                return true;

            // 检查项目文件夹是否存在
            var projectFolder = Path.Combine(projectsDir, name);
            if (Directory.Exists(projectFolder))
            {
                var projectNodeGraphPath = Path.Combine(projectFolder, name + ".nodegraph");
                if (File.Exists(projectNodeGraphPath))
                    return true;
            }

            // 检查已打开文档的冲突
            try
            {
                var mainWindow = System.Windows.Application.Current?.MainWindow;
                if (mainWindow != null)
                {
                    // 通过反射获取MainWindow的DocumentManager
                    var documentManagerProperty = mainWindow.GetType().GetProperty("DocumentManager");
                    if (documentManagerProperty != null)
                    {
                        var documentManager = documentManagerProperty.GetValue(mainWindow);
                        if (documentManager != null)
                        {
                            var documentsProperty = documentManager.GetType().GetProperty("Documents");
                            if (documentsProperty != null)
                            {
                                var documents = documentsProperty.GetValue(documentManager) as System.Collections.IEnumerable;
                                if (documents != null)
                                {
                                    foreach (var doc in documents)
                                    {
                                        var titleProperty = doc.GetType().GetProperty("Title");
                                        if (titleProperty != null)
                                        {
                                            var title = titleProperty.GetValue(doc) as string;
                                            if (string.Equals(title, name, StringComparison.OrdinalIgnoreCase))
                                                return true;
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch
            {
                // 如果反射失败，忽略已打开文档的检查
            }

            return false;
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
            CreateNodeGraph();
        }

        private void GraphNameTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                CreateNodeGraph();
                e.Handled = true;
            }
        }

        private void CreateNodeGraph()
        {
#if DEBUG
            Debug.WriteLine($"[DEBUG] CreateNodeGraph invoked");
            Debug.WriteLine($"[DEBUG] _lastTemplate null? {_lastTemplate == null}");
            Debug.WriteLine($"[DEBUG] SelectedTemplate null? {SelectedTemplate == null}");
            Debug.WriteLine($"[DEBUG] Templates count: {Templates.Count}");
#endif
            if (SelectedTemplate == null)
            {
                MessageBox.Show(this, "请先选择一个模板。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
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

            // 获取并验证名称
            var inputName = GraphNameTextBox.Text?.Trim();
            string finalName;

            if (string.IsNullOrWhiteSpace(inputName))
            {
                // 用户未填写，使用模板名称生成唯一名称
                finalName = GenerateUniqueName(SanitizeFileName(_lastTemplate.DisplayName));
            }
            else
            {
                // 用户填写了名称，检查是否需要调整以避免冲突
                var sanitizedName = SanitizeFileName(inputName);
                var projectsDir = GetProjectsDirectory();

                if (IsNameConflicted(sanitizedName, projectsDir))
                {
                    // 存在冲突，生成唯一名称
                    finalName = GenerateUniqueName(sanitizedName);

                    // 提示用户名称已调整
                    var result = MessageBox.Show(this,
                        $"名称 \"{sanitizedName}\" 已存在，将使用 \"{finalName}\" 代替。\n\n是否继续创建？",
                        "名称冲突",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Question);

                    if (result != MessageBoxResult.Yes)
                    {
                        // 用户选择不继续，更新输入框并返回
                        GraphNameTextBox.Text = finalName;
                        GraphNameTextBox.Focus();
                        GraphNameTextBox.SelectAll();
                        return;
                    }
                }
                else
                {
                    finalName = sanitizedName;
                }
            }

            // 使用所选模板创建新节点图
#if DEBUG
            Debug.WriteLine($"[DEBUG] Using template path: {_lastTemplate.NodeGraphPath}, name: {finalName}");
#endif
            this.Tag = new Tuple<string, string>(_lastTemplate.NodeGraphPath, finalName);
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