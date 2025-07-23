using System.Collections.Generic;
using System.Windows;
using Tunnel_Next.Models;

namespace Tunnel_Next.Windows
{
    /// <summary>
    /// BatchProcessResultWindow.xaml 的交互逻辑
    /// </summary>
    public partial class BatchProcessResultWindow : Window
    {
        public BatchProcessResultWindow(IEnumerable<BatchProcessNodeGraphItem> selectedItems)
        {
            InitializeComponent();
            
            // 设置选中的节点图列表
            SelectedNodesListBox.ItemsSource = selectedItems;
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
} 