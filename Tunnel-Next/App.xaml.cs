using System;
using System.Configuration;
using System.Data;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using Tunnel_Next.Windows;

namespace Tunnel_Next
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        [DllImport("kernel32.dll")]
        private static extern bool AllocConsole();

        protected override void OnStartup(StartupEventArgs e)
        {
#if DEBUG
            // 仅在调试模式下分配控制台窗口用于调试输出
            AllocConsole();
#endif

            base.OnStartup(e);
        }
        
        private void Application_Startup(object sender, StartupEventArgs e)
        {
                        // 显示启动窗口
            var splashWindow = new SplashWindow();
            splashWindow.Show();

            // 加载主窗口（不自动显示）
            MainWindow mainWindow = new MainWindow();

            // 重要：设置为应用程序的主窗口，这样Application.Current.MainWindow就不会为null
            this.MainWindow = mainWindow;
            
            // 设置关闭启动窗口的标志和方法
            bool mainWindowShown = false;
            
            // 创建显示主窗口的方法（确保只执行一次）
            Action showMainWindow = () => 
            {
                if (!mainWindowShown)
                {
                    Debug.WriteLine("显示主窗口 - 首次调用");
                    mainWindowShown = true;
                    if (splashWindow != null && splashWindow.IsLoaded)
                    {
                        Debug.WriteLine("关闭启动窗口");
                        splashWindow.Close();
                    }
                    Debug.WriteLine("显示主窗口");
                    mainWindow.Show();
                }
                else
                {
                    Debug.WriteLine("显示主窗口 - 重复调用，已忽略");
                }
            };
            
            // 创建一个进度报告处理器，更新启动窗口状态
            var progressHandler = new Progress<string>(status =>
            {
                splashWindow.UpdateStatus(status);
            });
            
            // 监听主窗口的脚本管理器初始化完成事件
            if (mainWindow.GetRevivalScriptManager() != null)
            {
                var scriptManager = mainWindow.GetRevivalScriptManager();
                
                // 设置脚本编译状态订阅
                Debug.WriteLine("设置脚本编译完成事件处理程序");
                
                scriptManager.ScriptsCompilationCompleted += (s, args) =>
                {
                    Debug.WriteLine("脚本编译完成事件触发");
                    // 脚本编译完成后，在UI线程关闭启动窗口并显示主窗口
                    Dispatcher.Invoke(() =>
                    {
                        Debug.WriteLine("脚本编译完成 - 在UI线程更新状态");
                        splashWindow.UpdateStatus("脚本加载完成，正在启动应用程序...");
                        // 稍微延迟一下，让用户看到完成消息
                        var finalTimer = new System.Windows.Threading.DispatcherTimer();
                        finalTimer.Interval = TimeSpan.FromMilliseconds(500);
                        finalTimer.Tick += (st, at) =>
                        {
                            Debug.WriteLine("延迟计时器触发 - 准备显示主窗口");
                            finalTimer.Stop();
                            showMainWindow(); // 使用统一的方法显示主窗口
                        };
                        Debug.WriteLine("启动延迟计时器");
                        finalTimer.Start();
                    });
                };
                
                // 检查脚本管理器是否已经初始化完成
                if (scriptManager.IsInitialized)
                {
                    Debug.WriteLine("脚本已经初始化完成，直接显示主窗口");
                    splashWindow.UpdateStatus("脚本已加载完成，正在启动应用程序...");
                    
                    // 稍微延迟一下，让用户看到完成消息
                    var immediateTimer = new System.Windows.Threading.DispatcherTimer();
                    immediateTimer.Interval = TimeSpan.FromMilliseconds(500);
                    immediateTimer.Tick += (st, at) =>
                    {
                        immediateTimer.Stop();
                        showMainWindow(); // 使用统一的方法显示主窗口
                    };
                    immediateTimer.Start();
                }
            }
            else
            {
                // 如果脚本管理器还没准备好，设置一个较长的超时定时器，确保不会卡死
                var checkTimer = new System.Windows.Threading.DispatcherTimer();
                checkTimer.Interval = TimeSpan.FromMilliseconds(500);
                int checkCount = 0;
                bool forceShowMainWindow = false;
                
                // 添加一个确保启动的定时器（10秒后强制显示主窗口）
                var forceStartTimer = new System.Windows.Threading.DispatcherTimer();
                forceStartTimer.Interval = TimeSpan.FromSeconds(10);
                forceStartTimer.Tick += (s, args) => 
                {
                    forceStartTimer.Stop();
                    Debug.WriteLine("强制超时 - 准备显示主窗口");
                    splashWindow.UpdateStatus("准备启动应用程序...");
                    forceShowMainWindow = true;
                    
                    // 延迟一下再显示
                    var delayTimer = new System.Windows.Threading.DispatcherTimer();
                    delayTimer.Interval = TimeSpan.FromMilliseconds(500);
                    delayTimer.Tick += (st, at) =>
                    {
                        delayTimer.Stop();
                        showMainWindow();
                    };
                    delayTimer.Start();
                };
                forceStartTimer.Start();
                
                // 常规检查定时器
                checkTimer.Tick += (s, args) =>
                {
                    // 如果已经强制显示了，停止检查
                    if (forceShowMainWindow)
                    {
                        checkTimer.Stop();
                        return;
                    }
                
                    checkCount++;
                    if (checkCount % 4 == 0) // 每2秒更新一次状态
                    {
                        splashWindow.UpdateStatus($"正在初始化脚本系统... ({checkCount/2}秒)");
                    }
                    
                    var scriptManager = mainWindow.GetRevivalScriptManager();
                    if (scriptManager != null)
                    {
                        checkTimer.Stop();
                        forceStartTimer.Stop(); // 停止强制启动定时器
                        splashWindow.UpdateStatus("正在编译脚本...");
                        
                        Debug.WriteLine("延迟初始化 - 设置脚本编译完成事件处理程序");
                        
                        // 注册脚本编译完成事件
                        scriptManager.ScriptsCompilationCompleted += (sm, smArgs) =>
                        {
                            Debug.WriteLine("延迟初始化 - 脚本编译完成事件触发");
                            // 脚本编译完成后，在UI线程关闭启动窗口并显示主窗口
                            Dispatcher.Invoke(() =>
                            {
                                Debug.WriteLine("延迟初始化 - 脚本编译完成 - 在UI线程更新状态");
                                splashWindow.UpdateStatus("脚本加载完成，正在启动应用程序...");
                                // 稍微延迟一下，让用户看到完成消息
                                var finalTimer = new System.Windows.Threading.DispatcherTimer();
                                finalTimer.Interval = TimeSpan.FromMilliseconds(500);
                                finalTimer.Tick += (st, at) =>
                                {
                                    Debug.WriteLine("延迟初始化 - 延迟计时器触发 - 准备显示主窗口");
                                    finalTimer.Stop();
                                    showMainWindow(); // 使用统一的方法显示主窗口
                                };
                                Debug.WriteLine("延迟初始化 - 启动延迟计时器");
                                finalTimer.Start();
                            });
                        };
                        
                        // 检查脚本管理器是否已经初始化完成
                        if (scriptManager.IsInitialized)
                        {
                            Debug.WriteLine("脚本已经初始化完成，直接显示主窗口");
                            splashWindow.UpdateStatus("脚本已加载完成，正在启动应用程序...");
                            
                            // 稍微延迟一下，让用户看到完成消息
                            var immediateTimer = new System.Windows.Threading.DispatcherTimer();
                            immediateTimer.Interval = TimeSpan.FromMilliseconds(500);
                            immediateTimer.Tick += (st, at) =>
                            {
                                immediateTimer.Stop();
                                showMainWindow(); // 使用统一的方法显示主窗口
                            };
                            immediateTimer.Start();
                        }
                    }
                };
                checkTimer.Start();
                splashWindow.UpdateStatus("正在初始化脚本系统...");
            }
            
            // 如果用户点击启动窗口，可以提前显示主窗口
            splashWindow.MouseDown += (s, args) =>
            {
                Debug.WriteLine("用户点击启动窗口 - 准备显示主窗口");
                splashWindow.UpdateStatus("正在启动应用程序...");
                
                // 延迟一小段时间让用户看到状态更新
                var clickTimer = new System.Windows.Threading.DispatcherTimer();
                clickTimer.Interval = TimeSpan.FromMilliseconds(200);
                clickTimer.Tick += (st, at) =>
                {
                    clickTimer.Stop();
                    Debug.WriteLine("用户点击触发 - 显示主窗口");
                    showMainWindow();
                };
                clickTimer.Start();
            };
        }
    }

}
