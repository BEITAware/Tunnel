using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;

namespace InstallationWizard.Pages
{
    public partial class PathSelectionPage : UserControl
    {
        public static string InstallPath { get; private set; } = "";
        public static string WorkFolderPath { get; private set; } = "";
        public static string ScriptsFolderPath { get; private set; } = "";

        public PathSelectionPage()
        {
            InitializeComponent();
            InitializeDefaultPaths();
        }

        private void InitializeDefaultPaths()
        {
            try
            {
                // 默认安装路径
                string programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
                InstallPathTextBox.Text = Path.Combine(programFiles, "Tunnel");

                // 默认工作文件夹路径
                string documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                WorkFolderTextBox.Text = Path.Combine(documentsPath, "TNX");

                // 默认脚本文件夹路径
                ScriptsFolderTextBox.Text = Path.Combine(documentsPath, "TNX", "Scripts");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"初始化默认路径时出错：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BrowseInstallPathButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var dialog = new System.Windows.Forms.FolderBrowserDialog
                {
                    Description = "选择安装路径",
                    SelectedPath = InstallPathTextBox.Text
                };

                if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    string selectedPath = dialog.SelectedPath;
                    
                    // 如果选择的是根目录，自动添加 \Tunnel\
                    if (IsRootDirectory(selectedPath))
                    {
                        selectedPath = Path.Combine(selectedPath, "Tunnel");
                    }
                    
                    InstallPathTextBox.Text = selectedPath;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"选择安装路径时出错：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BrowseWorkFolderButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var dialog = new System.Windows.Forms.FolderBrowserDialog
                {
                    Description = "选择工作文件夹路径",
                    SelectedPath = WorkFolderTextBox.Text
                };

                if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    WorkFolderTextBox.Text = dialog.SelectedPath;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"选择工作文件夹路径时出错：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BrowseScriptsFolderButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var dialog = new System.Windows.Forms.FolderBrowserDialog
                {
                    Description = "选择脚本文件夹路径",
                    SelectedPath = ScriptsFolderTextBox.Text
                };

                if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    ScriptsFolderTextBox.Text = dialog.SelectedPath;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"选择脚本文件夹路径时出错：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        internal bool TryConfirmPaths()
        {
            if (!TryReadFolder(InstallPathTextBox, "安装路径", out var install) ||
                !TryReadFolder(WorkFolderTextBox, "工作文件夹", out var work) ||
                !TryReadFolder(ScriptsFolderTextBox, "脚本文件夹", out var scripts))
                return false;

            InstallPath = IsRootDirectory(install) ? Path.Combine(install, "Tunnel") : install;
            WorkFolderPath = work;
            ScriptsFolderPath = scripts;
            return true;
        }

        private static bool TryReadFolder(TextBox input, string label, out string folder)
        {
            folder = input.Text.Trim();
            try
            {
                if (Path.IsPathFullyQualified(folder) && folder.IndexOfAny(Path.GetInvalidPathChars()) < 0)
                {
                    folder = Path.GetFullPath(folder);
                    return true;
                }
            }
            catch (Exception ex) when (ex is ArgumentException or IOException or NotSupportedException)
            {
                // 在当前输入框提示修正路径。
            }

            MessageBox.Show($"请为{label}输入有效的绝对路径。", "路径无效", MessageBoxButton.OK, MessageBoxImage.Warning);
            input.Focus();
            input.SelectAll();
            return false;
        }

        private bool IsRootDirectory(string path)
        {
            try
            {
                return Path.GetPathRoot(path) == path;
            }
            catch
            {
                return false;
            }
        }
    }
}
