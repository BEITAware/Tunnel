using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;

namespace InstallationWizard.Pages
{
    public partial class CompletionPage : UserControl
    {
        public CompletionPage()
        {
            InitializeComponent();
            LoadInstallationInfo();
        }

        private void LoadInstallationInfo()
        {
            InstallPathTextBlock.Text = PathSelectionPage.InstallPath;
            WorkFolderTextBlock.Text = PathSelectionPage.WorkFolderPath;
            ScriptsFolderTextBlock.Text = PathSelectionPage.ScriptsFolderPath;
        }

        public bool ShouldLaunchApplication => LaunchApplicationCheckBox.IsChecked == true;

        public void LaunchApplication()
        {
            try
            {
                string executablePath = Path.Combine(PathSelectionPage.InstallPath, "Tunnel-Next.exe");
                if (File.Exists(executablePath))
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = executablePath,
                        UseShellExecute = true
                    });
                }
                else
                {
                    MessageBox.Show($"无法找到应用程序文件：{executablePath}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (System.Exception ex)
            {
                MessageBox.Show($"启动应用程序时出错：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
