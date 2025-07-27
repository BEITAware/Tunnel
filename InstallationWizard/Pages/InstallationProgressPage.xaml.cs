using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;

namespace InstallationWizard.Pages
{
    public partial class InstallationProgressPage : UserControl
    {
        private int _totalSteps = 5;
        private int _currentStep = 0;
        private bool _installationStarted = false;

        public InstallationProgressPage()
        {
            InitializeComponent();
        }

        public async void StartInstallation()
        {
            if (_installationStarted) return;
            _installationStarted = true;

            try
            {
                await PerformInstallation();
            }
            catch (Exception ex)
            {
                if (Window.GetWindow(this) is MainWindow mainWindow)
                {
                    mainWindow.ShowErrorPage(ex);
                }
            }
        }

        private async Task PerformInstallation()
        {
            // 步骤1: 复制应用程序文件
            await UpdateProgress(0, "正在复制应用程序文件...");
            await CopyApplicationFiles();
            _currentStep++;
            await UpdateProgress(20, "应用程序文件复制完成");

            // 步骤2: 复制脚本文件
            await UpdateProgress(20, "正在复制脚本文件...");
            await CopyScriptFiles();
            _currentStep++;
            await UpdateProgress(40, "脚本文件复制完成");

            // 步骤3: 创建工作目录结构
            await UpdateProgress(40, "正在创建工作目录结构...");
            await CreateWorkDirectoryStructure();
            _currentStep++;
            await UpdateProgress(60, "工作目录结构创建完成");

            // 步骤4: 更新配置文件
            await UpdateProgress(60, "正在配置工作文件夹...");
            await UpdateWorkFoldersConfig();
            _currentStep++;
            await UpdateProgress(80, "配置文件更新完成");

            // 步骤5: 注册开始菜单
            await UpdateProgress(80, "正在注册开始菜单...");
            await RegisterStartMenuShortcut();
            _currentStep++;
            await UpdateProgress(100, "安装完成！");

            // 等待一下让用户看到完成状态
            await Task.Delay(1000);

            // 跳转到完成页面
            if (Window.GetWindow(this) is MainWindow mainWindow)
            {
                mainWindow.GoToCompletionPage();
            }
        }

        private async Task CopyApplicationFiles()
        {
            await Task.Run(() =>
            {
                string sourceDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "App");
                string targetDir = PathSelectionPage.InstallPath;

                LogMessage($"源目录: {sourceDir}");
                LogMessage($"目标目录: {targetDir}");

                if (!Directory.Exists(sourceDir))
                {
                    throw new DirectoryNotFoundException($"源目录不存在: {sourceDir}");
                }

                // 创建目标目录
                Directory.CreateDirectory(targetDir);
                LogMessage("已创建目标目录");

                // 复制所有文件和子目录
                CopyDirectory(sourceDir, targetDir);
                LogMessage("文件复制完成");
            });
        }

        private void CopyDirectory(string sourceDir, string targetDir)
        {
            DirectoryInfo source = new DirectoryInfo(sourceDir);
            DirectoryInfo target = new DirectoryInfo(targetDir);

            if (!target.Exists)
            {
                target.Create();
            }

            // 复制文件
            foreach (FileInfo file in source.GetFiles())
            {
                string targetFilePath = Path.Combine(target.FullName, file.Name);
                file.CopyTo(targetFilePath, true);
                LogMessage($"已复制: {file.Name}");
            }

            // 递归复制子目录
            foreach (DirectoryInfo subDir in source.GetDirectories())
            {
                string targetSubDir = Path.Combine(target.FullName, subDir.Name);
                CopyDirectory(subDir.FullName, targetSubDir);
            }
        }

        private async Task CopyScriptFiles()
        {
            await Task.Run(() =>
            {
                string sourceDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Scripts");
                string targetDir = PathSelectionPage.ScriptsFolderPath;

                LogMessage($"脚本源目录: {sourceDir}");
                LogMessage($"脚本目标目录: {targetDir}");

                if (!Directory.Exists(sourceDir))
                {
                    LogMessage("脚本源目录不存在，跳过脚本复制");
                    return;
                }

                // 创建目标目录
                Directory.CreateDirectory(targetDir);
                LogMessage("已创建脚本目标目录");

                // 复制所有脚本文件和子目录
                CopyDirectory(sourceDir, targetDir);
                LogMessage("脚本文件复制完成");
            });
        }

        private async Task CreateWorkDirectoryStructure()
        {
            await Task.Run(() =>
            {
                string workFolder = PathSelectionPage.WorkFolderPath;
                LogMessage($"工作文件夹: {workFolder}");

                // 创建主要目录结构
                var directories = new[]
                {
                    workFolder,
                    Path.Combine(workFolder, "Projects"),
                    Path.Combine(workFolder, "Projects", "temp"),
                    Path.Combine(workFolder, "Resources"),
                    Path.Combine(workFolder, "Resources", "Templates"),
                    PathSelectionPage.ScriptsFolderPath,
                    Path.Combine(PathSelectionPage.ScriptsFolderPath, "TunnelExtensionResources"),
                    Path.Combine(PathSelectionPage.ScriptsFolderPath, "compiled")
                };

                foreach (var dir in directories)
                {
                    Directory.CreateDirectory(dir);
                    LogMessage($"已创建目录: {Path.GetFileName(dir)}");
                }

                // 复制 Resources\Templates 文件
                CopyTemplateResources(workFolder);

                // 创建初始化标记文件
                var initMarkerFile = Path.Combine(PathSelectionPage.ScriptsFolderPath, ".initialized");
                File.WriteAllText(initMarkerFile, DateTime.Now.ToString());
                LogMessage("已创建初始化标记文件");
            });
        }

        private void CopyTemplateResources(string workFolder)
        {
            try
            {
                string sourceTemplatesDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "Templates");
                string targetTemplatesDir = Path.Combine(workFolder, "Resources", "Templates");

                LogMessage($"模板源目录: {sourceTemplatesDir}");
                LogMessage($"模板目标目录: {targetTemplatesDir}");

                if (Directory.Exists(sourceTemplatesDir))
                {
                    // 确保目标目录存在
                    Directory.CreateDirectory(targetTemplatesDir);

                    // 复制所有模板文件
                    CopyDirectory(sourceTemplatesDir, targetTemplatesDir);
                    LogMessage("模板文件复制完成");
                }
                else
                {
                    LogMessage("模板源目录不存在，跳过模板复制");
                }
            }
            catch (Exception ex)
            {
                LogMessage($"复制模板文件时出错: {ex.Message}");
                // 不抛出异常，允许安装继续
            }
        }

        private async Task RegisterStartMenuShortcut()
        {
            await Task.Run(() =>
            {
                try
                {
                    string startMenuPath = Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu);
                    string programsPath = Path.Combine(startMenuPath, "Programs");
                    string tunnelFolderPath = Path.Combine(programsPath, "Tunnel");

                    // 创建Tunnel文件夹
                    Directory.CreateDirectory(tunnelFolderPath);
                    LogMessage("已创建开始菜单文件夹");

                    // 创建快捷方式
                    string shortcutPath = Path.Combine(tunnelFolderPath, "Tunnel.lnk");
                    string targetPath = Path.Combine(PathSelectionPage.InstallPath, "Tunnel-Next.exe");

                    CreateShortcut(shortcutPath, targetPath, PathSelectionPage.InstallPath, "Tunnel - 专业图像处理和色彩管理工具");
                    LogMessage("已创建开始菜单快捷方式");

                    // 创建卸载快捷方式
                    string uninstallShortcutPath = Path.Combine(tunnelFolderPath, "卸载 Tunnel.lnk");
                    string uninstallTargetPath = Path.Combine(PathSelectionPage.InstallPath, "Uninstall.exe");

                    // 注意：这里我们假设将来会有卸载程序，现在先创建占位符
                    if (File.Exists(uninstallTargetPath))
                    {
                        CreateShortcut(uninstallShortcutPath, uninstallTargetPath, PathSelectionPage.InstallPath, "卸载 Tunnel");
                        LogMessage("已创建卸载快捷方式");
                    }
                }
                catch (Exception ex)
                {
                    LogMessage($"注册开始菜单时出错: {ex.Message}");
                    // 不抛出异常，允许安装继续
                }
            });
        }

        private void CreateShortcut(string shortcutPath, string targetPath, string workingDirectory, string description)
        {
            try
            {
                // 使用COM对象创建快捷方式
                Type shellType = Type.GetTypeFromProgID("WScript.Shell");
                dynamic shell = Activator.CreateInstance(shellType);
                var shortcut = shell.CreateShortcut(shortcutPath);

                shortcut.TargetPath = targetPath;
                shortcut.WorkingDirectory = workingDirectory;
                shortcut.Description = description;
                shortcut.Save();

                LogMessage($"已创建快捷方式: {Path.GetFileName(shortcutPath)}");
            }
            catch (Exception ex)
            {
                LogMessage($"创建快捷方式失败: {ex.Message}");
            }
        }

        private async Task UpdateWorkFoldersConfig()
        {
            await Task.Run(() =>
            {
                string configPath = Path.Combine(PathSelectionPage.InstallPath, "workfolders.ini");
                LogMessage($"配置文件路径: {configPath}");

                if (!File.Exists(configPath))
                {
                    throw new FileNotFoundException($"配置文件不存在: {configPath}");
                }

                // 读取配置文件
                string[] lines = File.ReadAllLines(configPath);
                LogMessage("已读取配置文件");

                // 更新配置
                for (int i = 0; i < lines.Length; i++)
                {
                    if (lines[i].StartsWith("WorkFolder="))
                    {
                        lines[i] = $"WorkFolder={PathSelectionPage.WorkFolderPath}";
                        LogMessage($"已更新工作文件夹路径: {PathSelectionPage.WorkFolderPath}");
                    }
                    else if (lines[i].StartsWith("ScriptsFolder="))
                    {
                        lines[i] = $"ScriptsFolder={PathSelectionPage.ScriptsFolderPath}";
                        LogMessage($"已更新脚本文件夹路径: {PathSelectionPage.ScriptsFolderPath}");
                    }
                }

                // 写回配置文件
                File.WriteAllLines(configPath, lines);
                LogMessage("配置文件更新完成");

                // 创建工作文件夹和脚本文件夹
                Directory.CreateDirectory(PathSelectionPage.WorkFolderPath);
                Directory.CreateDirectory(PathSelectionPage.ScriptsFolderPath);
                LogMessage("已创建工作文件夹和脚本文件夹");
            });
        }

        private async Task UpdateProgress(int percentage, string operation)
        {
            await Dispatcher.InvokeAsync(() =>
            {
                CurrentOperationTextBlock.Text = operation;
                ProgressPercentageTextBlock.Text = $"{percentage}%";
                CompletedStepsTextBlock.Text = _currentStep.ToString();

                // 动画更新进度条
                double targetWidth = (percentage / 100.0) * 398; // 398 = 400 - 2 (border)
                var animation = new DoubleAnimation
                {
                    To = targetWidth,
                    Duration = TimeSpan.FromMilliseconds(300),
                    EasingFunction = new QuadraticEase()
                };
                ProgressBarFill.BeginAnimation(FrameworkElement.WidthProperty, animation);
            });
        }

        private void LogMessage(string message)
        {
            Dispatcher.InvokeAsync(() =>
            {
                string timestamp = DateTime.Now.ToString("HH:mm:ss");
                string logEntry = $"[{timestamp}] {message}\n";
                DetailLogTextBlock.Text += logEntry;

                // 自动滚动到底部
                if (DetailLogTextBlock.Parent is ScrollViewer scrollViewer)
                {
                    scrollViewer.ScrollToEnd();
                }
            });
        }
    }
}
