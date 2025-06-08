using System.Configuration;
using System.Data;
using System.Runtime.InteropServices;
using System.Windows;

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
            // 分配控制台窗口用于调试输出
            AllocConsole();

            base.OnStartup(e);
        }
    }

}
