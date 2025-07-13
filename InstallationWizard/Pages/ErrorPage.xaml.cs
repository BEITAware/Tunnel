using System;
using System.Reflection;
using System.Text;
using System.Windows;
using System.Windows.Controls;

namespace InstallationWizard.Pages
{
    public partial class ErrorPage : UserControl
    {
        private readonly Exception _exception;
        private readonly string _fullErrorInfo;

        public ErrorPage(Exception exception)
        {
            InitializeComponent();
            _exception = exception;
            _fullErrorInfo = GenerateFullErrorInfo();
            LoadErrorInformation();
        }

        private void LoadErrorInformation()
        {
            // 基本错误信息
            ErrorMessageTextBlock.Text = _exception.Message;

            // 详细错误信息（包括调用栈）
            var detailedInfo = new StringBuilder();
            detailedInfo.AppendLine($"异常类型: {_exception.GetType().FullName}");
            detailedInfo.AppendLine($"错误消息: {_exception.Message}");
            detailedInfo.AppendLine();
            detailedInfo.AppendLine("调用栈:");
            detailedInfo.AppendLine(_exception.StackTrace ?? "无调用栈信息");

            // 如果有内部异常，也显示出来
            var innerException = _exception.InnerException;
            int level = 1;
            while (innerException != null)
            {
                detailedInfo.AppendLine();
                detailedInfo.AppendLine($"内部异常 {level}:");
                detailedInfo.AppendLine($"类型: {innerException.GetType().FullName}");
                detailedInfo.AppendLine($"消息: {innerException.Message}");
                detailedInfo.AppendLine($"调用栈: {innerException.StackTrace ?? "无调用栈信息"}");
                
                innerException = innerException.InnerException;
                level++;
            }

            DetailedErrorTextBlock.Text = detailedInfo.ToString();

            // 系统信息
            var systemInfo = new StringBuilder();
            systemInfo.AppendLine($"操作系统: {Environment.OSVersion}");
            systemInfo.AppendLine($"CLR 版本: {Environment.Version}");
            systemInfo.AppendLine($"机器名: {Environment.MachineName}");
            systemInfo.AppendLine($"用户名: {Environment.UserName}");
            systemInfo.AppendLine($"工作目录: {Environment.CurrentDirectory}");
            systemInfo.AppendLine($"应用程序域: {AppDomain.CurrentDomain.FriendlyName}");
            systemInfo.AppendLine($"程序集位置: {Assembly.GetExecutingAssembly().Location}");
            systemInfo.AppendLine($"时间: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");

            // 添加内存信息
            try
            {
                systemInfo.AppendLine($"工作集内存: {Environment.WorkingSet / 1024 / 1024} MB");
                systemInfo.AppendLine($"处理器数量: {Environment.ProcessorCount}");
            }
            catch
            {
                // 忽略获取系统信息时的错误
            }

            SystemInfoTextBlock.Text = systemInfo.ToString();
        }

        private string GenerateFullErrorInfo()
        {
            var fullInfo = new StringBuilder();
            fullInfo.AppendLine("=== Tunnel 安装向导错误报告 ===");
            fullInfo.AppendLine($"时间: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            fullInfo.AppendLine();
            
            fullInfo.AppendLine("=== 错误信息 ===");
            fullInfo.AppendLine($"异常类型: {_exception.GetType().FullName}");
            fullInfo.AppendLine($"错误消息: {_exception.Message}");
            fullInfo.AppendLine();
            
            fullInfo.AppendLine("=== 调用栈 ===");
            fullInfo.AppendLine(_exception.StackTrace ?? "无调用栈信息");
            fullInfo.AppendLine();

            // 内部异常
            var innerException = _exception.InnerException;
            int level = 1;
            while (innerException != null)
            {
                fullInfo.AppendLine($"=== 内部异常 {level} ===");
                fullInfo.AppendLine($"类型: {innerException.GetType().FullName}");
                fullInfo.AppendLine($"消息: {innerException.Message}");
                fullInfo.AppendLine($"调用栈: {innerException.StackTrace ?? "无调用栈信息"}");
                fullInfo.AppendLine();
                
                innerException = innerException.InnerException;
                level++;
            }

            fullInfo.AppendLine("=== 系统信息 ===");
            fullInfo.AppendLine($"操作系统: {Environment.OSVersion}");
            fullInfo.AppendLine($"CLR 版本: {Environment.Version}");
            fullInfo.AppendLine($"机器名: {Environment.MachineName}");
            fullInfo.AppendLine($"用户名: {Environment.UserName}");
            fullInfo.AppendLine($"工作目录: {Environment.CurrentDirectory}");
            fullInfo.AppendLine($"应用程序域: {AppDomain.CurrentDomain.FriendlyName}");
            fullInfo.AppendLine($"程序集位置: {Assembly.GetExecutingAssembly().Location}");

            try
            {
                fullInfo.AppendLine($"工作集内存: {Environment.WorkingSet / 1024 / 1024} MB");
                fullInfo.AppendLine($"处理器数量: {Environment.ProcessorCount}");
            }
            catch
            {
                // 忽略获取系统信息时的错误
            }

            return fullInfo.ToString();
        }

        private void CopyErrorButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Clipboard.SetText(_fullErrorInfo);
                MessageBox.Show("错误信息已复制到剪贴板。", "信息", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"复制到剪贴板时出错：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ExitButton_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }
    }
}
