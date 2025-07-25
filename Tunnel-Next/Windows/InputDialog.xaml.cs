using System.Windows;
using System.Windows.Input;

namespace Tunnel_Next.Windows
{
    /// <summary>
    /// 输入对话框
    /// </summary>
    public partial class InputDialog : Window
    {
        /// <summary>
        /// 输入的文本
        /// </summary>
        public string InputText { get; private set; } = string.Empty;

        public InputDialog(string title, string prompt, string defaultValue = "")
        {
            InitializeComponent();
            
            Title = title;
            TitleTextBlock.Text = title;
            PromptTextBlock.Text = prompt;
            InputTextBox.Text = defaultValue;
            InputText = defaultValue;
            
            // 选中默认文本
            InputTextBox.SelectAll();
            InputTextBox.Focus();
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            InputText = InputTextBox.Text;
            DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void InputTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                OkButton_Click(sender, e);
            }
            else if (e.Key == Key.Escape)
            {
                CancelButton_Click(sender, e);
            }
        }
    }
}
