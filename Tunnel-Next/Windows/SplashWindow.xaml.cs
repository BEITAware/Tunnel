using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;

namespace Tunnel_Next.Windows
{
    /// <summary>
    /// SplashWindow.xaml 的交互逻辑
    /// </summary>
    public partial class SplashWindow : Window
    {
        public SplashWindow()
        {
            InitializeComponent();
            
            // 基本初始化
            Debug.WriteLine("启动窗口创建");
        }
        
        public void ShowAndWait()
        {
            this.ShowDialog();
        }
        
        // 更新启动窗口上显示的状态文本
        public void UpdateStatus(string status)
        {
            try
            {
                // 在UI线程上更新状态文本
                Dispatcher.Invoke(() =>
                {
                    if (StatusText != null)
                    {
                        StatusText.Text = status;
                        Debug.WriteLine($"更新启动窗口状态: {status}");
                    }
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"更新启动窗口状态失败: {ex.Message}");
            }
        }
    }
} 