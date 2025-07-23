using Microsoft.Win32;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using Tunnel_Next.Controls;
using Tunnel_Next.Models;
using Tunnel_Next.Services;
using Tunnel_Next.Services.Scripting;
using Tunnel_Next.ViewModels;

namespace Tunnel_Next.Windows
{
    /// <summary>
    /// 批量处理器窗口
    /// </summary>
    public partial class BatchProcessWindow : Window
    {
        private readonly ObservableCollection<BatchProcessNodeGraphItem> _nodeGraphItems = new();
        private readonly ObservableCollection<BatchProcessNodeGraphItem> _selectedItems = new();
        private readonly WorkFolderService _workFolderService;
        private readonly ThumbnailService _thumbnailService;
        private readonly FileService _fileService;
        private readonly NodeEditorViewModel? _nodeEditorViewModel;
        private NodePreviewControl? _previewControl;
        private readonly RevivalScriptManager? _revivalScriptManager;
        // 跟踪第一个被选择的节点图项目
        private BatchProcessNodeGraphItem? _firstSelectedItem;
        
        public BatchProcessWindow(RevivalScriptManager? revivalScriptManager)
        {
            InitializeComponent();
            
            // 保存RevivalScriptManager引用
            _revivalScriptManager = revivalScriptManager;
            
            // 初始化服务
            _workFolderService = new WorkFolderService();
            _thumbnailService = new ThumbnailService(_workFolderService);
            _fileService = new FileService(_workFolderService, _revivalScriptManager);
            
            // 创建一个轻量级的NodeEditorViewModel用于预览
            _nodeEditorViewModel = new NodeEditorViewModel(_revivalScriptManager);
            
            // 设置数据源
            NodeGraphItemsControl.ItemsSource = _nodeGraphItems;
            SelectedItemsControl.ItemsSource = _selectedItems;
            
            // 注册窗口加载事件
            Loaded += BatchProcessWindow_Loaded;
        }

        /// <summary>
        /// 窗口加载事件
        /// </summary>
        private async void BatchProcessWindow_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                // 初始化工作文件夹
                await _workFolderService.InitializeAsync();
                
                // 加载节点图列表
                await LoadNodeGraphsAsync();
                
                // 初始化预览控件
                InitializePreviewControl();
                
                // 重置第一个选中项
                _firstSelectedItem = null;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"初始化批量处理器时发生错误: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 加载节点图列表
        /// </summary>
        private async System.Threading.Tasks.Task LoadNodeGraphsAsync()
        {
            try
            {
                // 清空现有项目
                _nodeGraphItems.Clear();
                _selectedItems.Clear();
                
                // 获取所有节点图文件
                var nodeGraphFiles = _workFolderService.GetNodeGraphFiles();
                
                // 为每个节点图文件创建一个项目
                foreach (string filePath in nodeGraphFiles)
                {
                    try
                    {
                        string fileName = System.IO.Path.GetFileNameWithoutExtension(filePath);
                        FileInfo fileInfo = new FileInfo(filePath);
                        
                        BatchProcessNodeGraphItem item = new BatchProcessNodeGraphItem
                        {
                            Name = fileName,
                            FilePath = filePath,
                            CreationTime = fileInfo.CreationTime,
                            LastModified = fileInfo.LastWriteTime,
                            IsSelected = false
                        };
                        
                        // 加载缩略图
                        string thumbnailPath = _thumbnailService.GetNodeGraphThumbnailPath(filePath);
                        if (File.Exists(thumbnailPath))
                        {
                            var thumbnail = await LoadThumbnailAsync(thumbnailPath);
                            if (thumbnail != null)
                            {
                                item.Thumbnail = thumbnail;
                            }
                            else
                            {
                                // 使用默认缩略图
                                try
                                {
                                    item.Thumbnail = new BitmapImage(new Uri("pack://application:,,,/Resources/Nodegraph.png"));
                                }
                                catch
                                {
                                    // 如果默认图标也加载失败，不设置缩略图
                                    System.Diagnostics.Debug.WriteLine("无法加载默认缩略图");
                                }
                            }
                        }
                        else
                        {
                            try
                            {
                                // 生成默认缩略图
                                await _thumbnailService.GenerateNodeGraphThumbnailAsync(filePath);
                                var thumbnail = await LoadThumbnailAsync(thumbnailPath);
                                if (thumbnail != null)
                                {
                                    item.Thumbnail = thumbnail;
                                }
                                else
                                {
                                    // 使用默认缩略图
                                    try
                                    {
                                        item.Thumbnail = new BitmapImage(new Uri("pack://application:,,,/Resources/Nodegraph.png"));
                                    }
                                    catch
                                    {
                                        // 如果默认图标也加载失败，不设置缩略图
                                        System.Diagnostics.Debug.WriteLine("无法加载默认缩略图");
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                System.Diagnostics.Debug.WriteLine($"无法生成缩略图 {filePath}: {ex.Message}");
                                // 使用默认缩略图
                                try
                                {
                                    item.Thumbnail = new BitmapImage(new Uri("pack://application:,,,/Resources/Nodegraph.png"));
                                }
                                catch
                                {
                                    // 如果默认图标也加载失败，不设置缩略图
                                    System.Diagnostics.Debug.WriteLine("无法加载默认缩略图");
                                }
                            }
                        }
                        
                        // 添加到列表
                        _nodeGraphItems.Add(item);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"加载节点图时出错 {filePath}: {ex.Message}");
                        // 继续处理下一个文件
                        continue;
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"加载节点图时发生错误: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 初始化预览控件
        /// </summary>
        private void InitializePreviewControl()
        {
            if (_previewControl != null)
                return;
                
            _previewControl = new NodePreviewControl
            {
                DataContext = _nodeEditorViewModel,
                IsReadOnly = true // 设置为只读模式
            };
            
            // 不立即添加到视图，等选择节点图后添加
        }

        /// <summary>
        /// 加载缩略图
        /// </summary>
        private System.Threading.Tasks.Task<BitmapImage> LoadThumbnailAsync(string path)
        {
            return System.Threading.Tasks.Task.Run(() =>
            {
                try
                {
                    BitmapImage image = new BitmapImage();
                    
                    // 在后台线程中加载图像，然后冻结它以便可以在UI线程中使用
                    using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read))
                    {
                        image.BeginInit();
                        image.CacheOption = BitmapCacheOption.OnLoad;
                        image.StreamSource = stream;
                        image.EndInit();
                        image.Freeze(); // 冻结图像，使其可以跨线程使用
                    }
                    
                    return image;
                }
                catch (Exception ex)
                {
                    // 记录错误并返回一个空白图像
                    System.Diagnostics.Debug.WriteLine($"无法加载缩略图 {path}: {ex.Message}");
                    return null;
                }
            });
        }

        /// <summary>
        /// 更新选中项列表
        /// </summary>
        private void UpdateSelectedItems(BatchProcessNodeGraphItem item)
        {
            // 完全重建选中项列表，确保与当前选择状态同步
            _selectedItems.Clear();
            
            // 确保首先添加第一个选择的项目（如果存在）
            if (_firstSelectedItem != null && _firstSelectedItem.IsSelected)
            {
                _selectedItems.Add(_firstSelectedItem);
            }
            
            // 添加其余选择的项目
            foreach (var nodeItem in _nodeGraphItems.Where(n => n.IsSelected && n != _firstSelectedItem))
            {
                _selectedItems.Add(nodeItem);
            }
            
            // 如果没有选中项，清除第一个选中项引用
            if (_selectedItems.Count == 0)
            {
                _firstSelectedItem = null;
            }
            // 如果有选中项但第一个选中项为null，设置第一个
            else if (_firstSelectedItem == null && _selectedItems.Count > 0)
            {
                _firstSelectedItem = _selectedItems[0];
            }
        }

        /// <summary>
        /// 显示预览
        /// </summary>
        private async void ShowPreview(BatchProcessNodeGraphItem item)
        {
            try
            {
                // 显示加载中的提示
                NoSelectionText.Text = "正在加载预览...";
                NoSelectionText.Visibility = Visibility.Visible;
                
                // 更新预览标题
                PreviewTitleText.Text = $"- {item.Name}";
                
                // 检查必要对象
                if (_previewControl == null)
                {
                    InitializePreviewControl();
                }

                if (_previewControl == null || _nodeEditorViewModel == null)
                {
                    NoSelectionText.Text = "预览控件初始化失败";
                    return;
                }
                
                try
                {
                    // 读取文件内容
                    string json = await File.ReadAllTextAsync(item.FilePath);
                    
                    // 创建反序列化器
                    var deserializer = new NodeGraphDeserializer(_revivalScriptManager);
                    
                    // 反序列化节点图
                    var nodeGraph = deserializer.DeserializeNodeGraph(json);
                    
                    // 加载到视图模型
                    await _nodeEditorViewModel.LoadNodeGraphAsync(nodeGraph);
                    
                    // 添加到容器，但先清空
                    PreviewContainer.Children.Clear();
                    PreviewContainer.Children.Add(_previewControl);
                    
                    // 隐藏提示文本
                    NoSelectionText.Visibility = Visibility.Collapsed;
                }
                catch (Exception ex)
                {
                    NoSelectionText.Text = $"无法加载节点图: {ex.Message}";
                    NoSelectionText.Visibility = Visibility.Visible;
                    System.Diagnostics.Debug.WriteLine($"加载节点图预览失败: {ex.Message}");
                }
            }
            catch (Exception ex)
            {
                NoSelectionText.Text = $"加载预览时发生错误: {ex.Message}";
                NoSelectionText.Visibility = Visibility.Visible;
                System.Diagnostics.Debug.WriteLine($"严重错误: {ex.Message}");
            }
        }

        /// <summary>
        /// 隐藏预览
        /// </summary>
        private void HidePreview()
        {
            // 如果存在预览控件，从容器中移除
            if (_previewControl != null)
            {
                PreviewContainer.Children.Clear();
            }
            
            // 清除预览标题
            PreviewTitleText.Text = "";
            
            // 显示提示文本
            NoSelectionText.Visibility = Visibility.Visible;
        }

        /// <summary>
        /// 刷新按钮点击事件
        /// </summary>
        private async void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            await LoadNodeGraphsAsync();
        }

        /// <summary>
        /// 节点图点击事件
        /// </summary>
        private void NodeGraph_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            try
            {
                if (sender is Border border && border.Tag is BatchProcessNodeGraphItem item)
                {
                    bool isControlPressed = Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl);
                    bool isShiftPressed = Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift);
                    
                    if (isControlPressed)
                    {
                        // Ctrl+单击：只切换预览，不改变选中状态
                        ShowPreview(item);
                    }
                    else if (isShiftPressed && _firstSelectedItem != null)
                    {
                        // Shift+单击：进行连续多选
                        // 找到第一个选中项和当前项在列表中的索引
                        int firstIndex = _nodeGraphItems.IndexOf(_firstSelectedItem);
                        int currentIndex = _nodeGraphItems.IndexOf(item);
                        
                        // 确定选择范围的起始和结束索引
                        int startIdx = Math.Min(firstIndex, currentIndex);
                        int endIdx = Math.Max(firstIndex, currentIndex);
                        
                        // 选中范围内的所有项目
                        for (int i = 0; i < _nodeGraphItems.Count; i++)
                        {
                            // 范围内的设为选中，范围外的不变
                            if (i >= startIdx && i <= endIdx)
                            {
                                _nodeGraphItems[i].IsSelected = true;
                            }
                        }
                        
                        // 更新选中项列表
                        UpdateSelectedItems(item);
                        
                        // 显示当前点击项的预览
                        ShowPreview(item);
                    }
                    else
                    {
                        // 普通单击：切换当前项的选中状态
                        item.IsSelected = !item.IsSelected;
                        
                        // 如果是选中操作，设置为第一个选择项
                        if (item.IsSelected)
                        {
                            _firstSelectedItem = item;
                        }
                        // 如果是取消选择且它是第一个选择项，需要重新找第一个
                        else if (item == _firstSelectedItem)
                        {
                            _firstSelectedItem = _nodeGraphItems.FirstOrDefault(i => i.IsSelected);
                        }
                        
                        // 更新选中项列表
                        UpdateSelectedItems(item);
                        
                        // 显示预览（无论是否选中，都显示当前项的预览）
                        ShowPreview(item);
                    }
                    
                    // 标记事件已处理，防止冒泡
                    e.Handled = true;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"节点图点击处理错误: {ex.Message}");
            }
        }

        /// <summary>
        /// 取消按钮点击事件
        /// </summary>
        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        /// <summary>
        /// 继续按钮点击事件
        /// </summary>
        private void ContinueButton_Click(object sender, RoutedEventArgs e)
        {
            // 检查是否有选中的节点图
            if (_selectedItems.Count == 0)
            {
                MessageBox.Show("请至少选择一个节点图进行处理", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            
            // 创建结果窗口
            var resultWindow = new BatchProcessResultWindow(_selectedItems);
            resultWindow.Owner = this;
            
            // 显示结果窗口
            resultWindow.ShowDialog();
            
            // 关闭当前窗口
            DialogResult = true;
            Close();
        }
    }
} 