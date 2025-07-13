using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;

namespace InstallationWizard.Pages
{
    public partial class LicensePage : UserControl
    {
        public static bool IsLicenseAccepted { get; private set; } = false;

        public LicensePage()
        {
            InitializeComponent();
            LoadLicenseText();
        }

        private void LoadLicenseText()
        {
            try
            {
                string licensePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "App", "Licenses", "LICENSE");
                if (File.Exists(licensePath))
                {
                    string licenseText = File.ReadAllText(licensePath);
                    LicenseTextBlock.Text = licenseText;
                }
                else
                {
                    LicenseTextBlock.Text = "许可协议文件未找到。";
                }
            }
            catch (Exception ex)
            {
                LicenseTextBlock.Text = $"加载许可协议时出错：{ex.Message}";
            }
        }

        private void AcceptLicenseCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            IsLicenseAccepted = true;
            UpdateParentWindow();
        }

        private void AcceptLicenseCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            IsLicenseAccepted = false;
            UpdateParentWindow();
        }

        private void UpdateParentWindow()
        {
            if (Window.GetWindow(this) is MainWindow mainWindow)
            {
                // 可以在这里更新主窗口的下一步按钮状态
                // 暂时不实现，因为用户可以选择不接受然后取消安装
            }
        }
    }
}
