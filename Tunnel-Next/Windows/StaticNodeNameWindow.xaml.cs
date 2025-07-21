using System;
using System.Windows;

namespace Tunnel_Next.Windows
{
    /// <summary>
    /// StaticNodeNameWindow.xaml 的交互逻辑
    /// </summary>
    public partial class StaticNodeNameWindow : Window
    {
        /// <summary>
        /// 用户输入的节点名称
        /// </summary>
        public string NodeName { get; private set; }

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="defaultName">默认显示的节点名称</param>
        public StaticNodeNameWindow(string defaultName)
        {
            InitializeComponent();
            
            // 设置默认名称
            NodeNameTextBox.Text = defaultName;
            
            // 默认选择全部文字以便用户直接替换
            NodeNameTextBox.Focus();
            NodeNameTextBox.SelectAll();
        }

        /// <summary>
        /// 保存按钮点击处理
        /// </summary>
        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            // 获取用户输入的名称
            string name = NodeNameTextBox.Text?.Trim() ?? string.Empty;
            
            // 验证名称不能为空
            if (string.IsNullOrWhiteSpace(name))
            {
                MessageBox.Show("请输入一个有效的名称。", "输入错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                NodeNameTextBox.Focus();
                return;
            }
            
            // 设置结果并关闭窗口
            NodeName = name;
            DialogResult = true;
            Close();
        }
    }
} 