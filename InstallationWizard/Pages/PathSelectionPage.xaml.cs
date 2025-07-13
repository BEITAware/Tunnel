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
                InstallPath = Path.Combine(programFiles, "Tunnel");
                InstallPathTextBox.Text = InstallPath;

                // 默认工作文件夹路径
                string documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                WorkFolderPath = Path.Combine(documentsPath, "TNX");
                WorkFolderTextBox.Text = WorkFolderPath;

                // 默认脚本文件夹路径
                ScriptsFolderPath = Path.Combine(documentsPath, "TNX", "Scripts");
                ScriptsFolderTextBox.Text = ScriptsFolderPath;
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
                    
                    InstallPath = selectedPath;
                    InstallPathTextBox.Text = InstallPath;
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
                    WorkFolderPath = dialog.SelectedPath;
                    WorkFolderTextBox.Text = WorkFolderPath;
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
                    ScriptsFolderPath = dialog.SelectedPath;
                    ScriptsFolderTextBox.Text = ScriptsFolderPath;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"选择脚本文件夹路径时出错：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
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
