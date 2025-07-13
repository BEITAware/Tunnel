using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using InstallationWizard.Pages;

namespace InstallationWizard
{
    public partial class MainWindow : Window
    {
        private List<UserControl> _pages;
        private int _currentPageIndex = 0;

        public MainWindow()
        {
            InitializeComponent();
            InitializePages();
            ShowCurrentPage();
        }

        private void InitializePages()
        {
            _pages = new List<UserControl>
            {
                new WelcomePage(),
                new LicensePage(),
                new PathSelectionPage(),
                new InstallationProgressPage(),
                new CompletionPage()
            };
        }

        private void ShowCurrentPage()
        {
            if (_currentPageIndex >= 0 && _currentPageIndex < _pages.Count)
            {
                PageContentPresenter.Content = _pages[_currentPageIndex];
                UpdateButtonStates();
            }
        }

        private void UpdateButtonStates()
        {
            if (_currentPageIndex == _pages.Count - 1) // 完成页面
            {
                BackButton.Visibility = Visibility.Collapsed;
                NextButton.Visibility = Visibility.Collapsed;
                CancelButton.Content = "退出";
            }
            else if (_currentPageIndex == _pages.Count - 2) // 安装进度页
            {
                BackButton.Visibility = Visibility.Collapsed;
                NextButton.Visibility = Visibility.Collapsed;
                CancelButton.Visibility = Visibility.Collapsed;
            }
            else
            {
                BackButton.Visibility = _currentPageIndex > 0 ? Visibility.Visible : Visibility.Collapsed;
                NextButton.Visibility = Visibility.Visible;
                CancelButton.Visibility = Visibility.Visible;
                CancelButton.Content = "取消";

                if (_currentPageIndex == 1) // 许可协议页面
                {
                    NextButton.Content = "同意";
                }
                else if (_currentPageIndex == 2) // 路径选择页面
                {
                    NextButton.Content = "安装";
                }
                else
                {
                    NextButton.Content = "下一步";
                }
            }
        }

        private void NextButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_currentPageIndex == _pages.Count - 1) // 完成页面
                {
                    // 检查是否需要启动应用程序
                    if (_pages[_currentPageIndex] is CompletionPage completionPage && completionPage.ShouldLaunchApplication)
                    {
                        completionPage.LaunchApplication();
                    }
                    Application.Current.Shutdown();
                    return;
                }

                // 在许可协议页面检查是否接受了协议
                if (_currentPageIndex == 1 && !LicensePage.IsLicenseAccepted) // 许可协议页面
                {
                    MessageBox.Show("您必须接受许可协议才能继续安装。", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (_currentPageIndex < _pages.Count - 1)
                {
                    _currentPageIndex++;
                    ShowCurrentPage();

                    // 如果是安装进度页面，开始安装
                    if (_pages[_currentPageIndex] is InstallationProgressPage progressPage)
                    {
                        progressPage.StartInstallation();
                    }
                }
            }
            catch (Exception ex)
            {
                ShowErrorPage(ex);
            }
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_currentPageIndex > 0)
                {
                    _currentPageIndex--;
                    ShowCurrentPage();
                }
            }
            catch (Exception ex)
            {
                ShowErrorPage(ex);
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show("确定要取消安装吗？", "确认", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                Application.Current.Shutdown();
            }
        }

        public void ShowErrorPage(Exception exception)
        {
            var errorPage = new ErrorPage(exception);
            PageContentPresenter.Content = errorPage;
            BackButton.Visibility = Visibility.Collapsed;
            NextButton.Visibility = Visibility.Collapsed;
            CancelButton.Content = "退出";
        }

        public void GoToCompletionPage()
        {
            _currentPageIndex = _pages.Count - 1;
            ShowCurrentPage();
        }
    }
}
