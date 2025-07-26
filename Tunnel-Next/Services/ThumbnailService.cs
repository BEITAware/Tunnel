using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Effects;
using OpenCvSharp;
using Tunnel_Next.Models;
using Tunnel_Next.Controls;

namespace Tunnel_Next.Services
{
    /// <summary>
    /// 缩略图生成服务
    /// </summary>
    public class ThumbnailService
    {
        private const int ThumbnailSize = 128;
        private readonly WorkFolderService _workFolderService;
        private static readonly Random _random = new Random();
        private static readonly string[] AuroraBackgrounds = { "Aurora2.png", "Aurora3.png" };

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="workFolderService">工作文件夹服务</param>
        public ThumbnailService(WorkFolderService workFolderService)
        {
            _workFolderService = workFolderService ?? throw new ArgumentNullException(nameof(workFolderService));
        }
        
        /// <summary>
        /// 整理所有缩略图，将基于映射关系的最新缩略图重命名为标准格式，并清理无用缩略图
        /// 这个方法应该在应用程序启动时调用，避免后续出现文件句柄冲突
        /// </summary>
        public void OrganizeThumbnails()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"[ThumbnailService] 开始整理缩略图...");
                
                // 获取所有工作目录
                var nodegraphsFolder = _workFolderService.NodeGraphsFolder;
                if (string.IsNullOrEmpty(nodegraphsFolder) || !Directory.Exists(nodegraphsFolder))
                    return;
                    
                // 递归扫描所有目录
                ProcessDirectory(nodegraphsFolder);
                
                // 完成后清理所有无关的PNG文件
                CleanupOrphanedPngFiles(nodegraphsFolder);
                
                System.Diagnostics.Debug.WriteLine($"[ThumbnailService] 缩略图整理完成");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ThumbnailService] 整理缩略图出现异常: {ex.Message}");
            }
        }
        
        /// <summary>
        /// 递归处理目录中的节点图和缩略图
        /// </summary>
        /// <param name="directory">要处理的目录</param>
        private void ProcessDirectory(string directory)
        {
            try
            {
                // 处理当前目录中的所有节点图文件
                var nodeGraphFiles = Directory.GetFiles(directory, "*.nodegraph");
                foreach (var nodeGraphFile in nodeGraphFiles)
                {
                    OrganizeNodeGraphThumbnails(nodeGraphFile);
                }
                
                // 递归处理子目录
                var subdirectories = Directory.GetDirectories(directory);
                foreach (var subdirectory in subdirectories)
                {
                    ProcessDirectory(subdirectory);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ThumbnailService] 处理目录 {directory} 时出错: {ex.Message}");
            }
        }
        
        /// <summary>
        /// 整理指定节点图的缩略图
        /// </summary>
        /// <param name="nodeGraphPath">节点图文件路径</param>
        private void OrganizeNodeGraphThumbnails(string nodeGraphPath)
        {
            try
            {
                var nodeGraphName = Path.GetFileNameWithoutExtension(nodeGraphPath);
                var projectFolder = Path.GetDirectoryName(nodeGraphPath) ?? _workFolderService.NodeGraphsFolder;
                var standardThumbnailPath = Path.Combine(projectFolder, $"{nodeGraphName}.png");
                var mapFilePath = Path.Combine(projectFolder, $"{nodeGraphName}.thumbnailmap");
                
                string validThumbnailPath = null;
                
                // 检查是否存在映射文件
                if (File.Exists(mapFilePath))
                {
                    try
                    {
                        // 读取映射文件确定最新缩略图
                        string latestThumbnailName = File.ReadAllText(mapFilePath).Trim();
                        if (!string.IsNullOrEmpty(latestThumbnailName))
                        {
                            validThumbnailPath = Path.Combine(projectFolder, latestThumbnailName);
                            if (!File.Exists(validThumbnailPath))
                            {
                                validThumbnailPath = null;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[ThumbnailService] 读取映射文件失败: {ex.Message}");
                    }
                }
                
                // 如果映射文件不存在或无效，则尝试查找格式为 {nodegraphname}.*.png 的缩略图
                if (validThumbnailPath == null)
                {
                    try
                    {
                        string pattern = $"{nodeGraphName}.*.png";
                        var thumbnailFiles = Directory.GetFiles(projectFolder, pattern);
                        if (thumbnailFiles.Length > 0)
                        {
                            // 按修改时间排序，选择最新的
                            validThumbnailPath = thumbnailFiles
                                .OrderByDescending(f => new FileInfo(f).LastWriteTime)
                                .FirstOrDefault();
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[ThumbnailService] 搜索缩略图失败: {ex.Message}");
                    }
                }
                
                // 如果找到了有效的缩略图，将其重命名为标准格式
                if (validThumbnailPath != null && File.Exists(validThumbnailPath))
                {
                    // 如果标准路径已存在且不是有效缩略图，先删除
                    if (File.Exists(standardThumbnailPath) && !string.Equals(standardThumbnailPath, validThumbnailPath, StringComparison.OrdinalIgnoreCase))
                    {
                        try
                        {
                            File.Delete(standardThumbnailPath);
                            System.Diagnostics.Debug.WriteLine($"[ThumbnailService] 删除旧缩略图: {standardThumbnailPath}");
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"[ThumbnailService] 删除旧缩略图失败: {ex.Message}");
                            return; // 如果无法删除，则退出
                        }
                    }
                    
                    // 如果有效缩略图不是标准名称，则重命名
                    if (!string.Equals(standardThumbnailPath, validThumbnailPath, StringComparison.OrdinalIgnoreCase))
                    {
                        try
                        {
                            // 复制而不是移动，以避免可能的文件锁问题
                            File.Copy(validThumbnailPath, standardThumbnailPath, true);
                            System.Diagnostics.Debug.WriteLine($"[ThumbnailService] 将缩略图 {validThumbnailPath} 重命名为标准格式: {standardThumbnailPath}");
                            
                            // 删除原始的非标准格式缩略图
                            try
                            {
                                File.Delete(validThumbnailPath);
                            }
                            catch (Exception ex)
                            {
                                System.Diagnostics.Debug.WriteLine($"[ThumbnailService] 删除原始非标准缩略图失败: {ex.Message}");
                            }
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"[ThumbnailService] 重命名缩略图失败: {ex.Message}");
                        }
                    }
                    
                    // 清理其他无用的缩略图
                    try
                    {
                        string pattern = $"{nodeGraphName}.*.png";
                        var thumbnailFiles = Directory.GetFiles(projectFolder, pattern);
                        foreach (var thumbnailFile in thumbnailFiles)
                        {
                            if (thumbnailFile != validThumbnailPath && thumbnailFile != standardThumbnailPath)
                            {
                                try
                                {
                                    File.Delete(thumbnailFile);
                                    System.Diagnostics.Debug.WriteLine($"[ThumbnailService] 删除无用缩略图: {thumbnailFile}");
                                }
                                catch (Exception ex)
                                {
                                    System.Diagnostics.Debug.WriteLine($"[ThumbnailService] 删除无用缩略图失败: {ex.Message}");
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[ThumbnailService] 清理无用缩略图失败: {ex.Message}");
                    }
                    
                    // 删除映射文件，因为现在已经统一为标准格式
                    try
                    {
                        if (File.Exists(mapFilePath))
                        {
                            File.Delete(mapFilePath);
                            System.Diagnostics.Debug.WriteLine($"[ThumbnailService] 删除映射文件: {mapFilePath}");
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[ThumbnailService] 删除映射文件失败: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ThumbnailService] 整理缩略图 {nodeGraphPath} 时出错: {ex.Message}");
            }
        }

        /// <summary>
        /// 为节点图生成缩略图（从预览控件获取图像）
        /// </summary>
        /// <param name="nodeGraphPath">节点图文件路径</param>
        /// <param name="previewControl">预览控件</param>
        /// <param name="clearCacheCallback">清除缓存的回调函数</param>
        /// <returns>缩略图文件路径</returns>
        public async Task<string?> GenerateNodeGraphThumbnailFromPreviewAsync(string nodeGraphPath, ImagePreviewControl? previewControl = null, Action<string>? clearCacheCallback = null)
        {
            try
            {
                if (string.IsNullOrEmpty(nodeGraphPath) || !File.Exists(nodeGraphPath))
                    return null;

                var nodeGraphName = Path.GetFileNameWithoutExtension(nodeGraphPath);
                var projectFolder = Path.GetDirectoryName(nodeGraphPath) ?? _workFolderService.NodeGraphsFolder;
                var thumbnailPath = Path.Combine(projectFolder, $"{nodeGraphName}.png");

                BitmapSource? thumbnail = null;

                // 尝试从预览控件获取图像
                Mat? previewImage = null;
                if (previewControl != null)
                {
                    // 在UI线程上安全地获取渲染后的图像，而不是ImageSource
                    thumbnail = await Application.Current.Dispatcher.Invoke(async () => {
                        // 检查PreviewImage是否有内容
                        if (previewControl.PreviewImageControl != null && 
                            previewControl.PreviewImageControl.Source != null && 
                            previewControl.PreviewImageControl.ActualWidth > 0 && 
                            previewControl.PreviewImageControl.ActualHeight > 0)
                        {
                            try
                            {
                                // 创建RenderTargetBitmap以捕获实际渲染的图像
                                var rtb = new RenderTargetBitmap(
                                    (int)previewControl.PreviewImageControl.ActualWidth,
                                    (int)previewControl.PreviewImageControl.ActualHeight,
                                    96, 96, PixelFormats.Pbgra32);

                                // 渲染控件内容
                                rtb.Render(previewControl.PreviewImageControl);
                                rtb.Freeze();
                                
                                // 调整大小
                                return await Task.Run(() => CreateThumbnailFromBitmapSource(rtb));
                            }
                            catch (Exception ex)
                            {
                                System.Diagnostics.Debug.WriteLine($"[ThumbnailService] 捕获预览图像失败: {ex.Message}");
                            }
                        }
                        
                        // 如果从UI渲染获取失败，尝试从ImageSource获取
                        var source = previewControl.ImageSource;
                        if (source != null && !source.Empty())
                        {
                            var cloned = source.Clone();
                            var result = await CreateThumbnailFromMatAsync(cloned);
                            cloned.Dispose();
                            return result;
                        }
                        return null;
                    });
                }

                // 如果无法从UI获取，但有原始Mat，则使用它
                if (thumbnail == null && previewControl?.ImageSource != null && !previewControl.ImageSource.Empty())
                {
                    // 从原始ImageSource克隆Mat对象以确保线程安全
                    previewImage = previewControl.ImageSource.Clone();
                    
                    if (previewImage != null && !previewImage.Empty())
                    {
                        // 从预览图像生成缩略图
                        thumbnail = await CreateThumbnailFromMatAsync(previewImage);
                        
                        // 释放克隆的图像资源
                        previewImage.Dispose();
                    }
                }

                // 如果仍无法获取有效图像，创建默认缩略图
                if (thumbnail == null)
                {
                    thumbnail = CreateEnhancedDefaultThumbnail(nodeGraphName);
                }

                if (thumbnail != null)
                {
                    // 在保存前清除缓存以释放文件句柄
                    clearCacheCallback?.Invoke(thumbnailPath);

                    // 使用临时文件机制保存缩略图
                    string actualPath = await SaveThumbnailAsync(thumbnail, thumbnailPath);
                    return actualPath;
                }

                return null;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ThumbnailService] 生成缩略图异常: {ex.Message}");
                return null;
            }
        }
        
        /// <summary>
        /// 从BitmapSource创建缩略图
        /// </summary>
        /// <param name="source">源BitmapSource</param>
        /// <returns>调整大小后的缩略图BitmapSource</returns>
        private BitmapSource CreateThumbnailFromBitmapSource(BitmapSource source)
        {
            try
            {
                if (source == null) return null;
                
                // 计算缩放比例，保持宽高比
                double scale = Math.Min((double)ThumbnailSize / source.PixelWidth, (double)ThumbnailSize / source.PixelHeight);
                int newWidth = (int)(source.PixelWidth * scale);
                int newHeight = (int)(source.PixelHeight * scale);
                
                // 创建转换组
                TransformGroup transformGroup = new TransformGroup();
                transformGroup.Children.Add(new ScaleTransform(scale, scale));
                
                // 创建TransformedBitmap进行缩放
                var transformedBitmap = new TransformedBitmap(source, transformGroup);
                
                // 创建渲染目标绘制缩放后的图像
                var drawingVisual = new DrawingVisual();
                                 using (DrawingContext drawingContext = drawingVisual.RenderOpen())
                 {
                     drawingContext.DrawImage(transformedBitmap, new System.Windows.Rect(0, 0, newWidth, newHeight));
                 }
                
                // 将图像渲染到RenderTargetBitmap
                var renderTargetBitmap = new RenderTargetBitmap(
                    newWidth, newHeight, 96, 96, PixelFormats.Pbgra32);
                renderTargetBitmap.Render(drawingVisual);
                renderTargetBitmap.Freeze();
                
                return renderTargetBitmap;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ThumbnailService] 创建BitmapSource缩略图失败: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 为节点图生成缩略图
        /// </summary>
        /// <param name="nodeGraphPath">节点图文件路径</param>
        /// <param name="previewImage">预览图像（Mat格式）</param>
        /// <param name="clearCacheCallback">清除缓存的回调函数</param>
        /// <returns>缩略图文件路径</returns>
        public async Task<string?> GenerateNodeGraphThumbnailAsync(string nodeGraphPath, Mat? previewImage = null, Action<string>? clearCacheCallback = null)
        {
            try
            {
                if (string.IsNullOrEmpty(nodeGraphPath) || !File.Exists(nodeGraphPath))
                    return null;

                var nodeGraphName = Path.GetFileNameWithoutExtension(nodeGraphPath);
                // 缩略图直接保存在项目文件夹中
                var projectFolder = Path.GetDirectoryName(nodeGraphPath) ?? _workFolderService.NodeGraphsFolder;
                var thumbnailPath = Path.Combine(projectFolder, $"{nodeGraphName}.png");

                BitmapSource? thumbnail = null;
                
                // 使用安全副本
                Mat? safePreviewImage = null;
                if (previewImage != null && !previewImage.Empty())
                {
                    try
                    {
                        // 克隆Mat对象以确保线程安全
                        safePreviewImage = previewImage.Clone();
                        
                        // 从预览图像生成缩略图
                        thumbnail = await CreateThumbnailFromMatAsync(safePreviewImage);
                        
                        // 释放克隆的图像资源
                        safePreviewImage.Dispose();
                    }
                    catch
                    {
                        // 如果克隆失败，使用原始图像
                        thumbnail = await CreateThumbnailFromMatAsync(previewImage);
                    }
                }
                else
                {
                    // 创建改良的默认缩略图
                    thumbnail = CreateEnhancedDefaultThumbnail(nodeGraphName);
                }

                if (thumbnail != null)
                {
                    // 在保存前清除缓存以释放文件句柄
                    clearCacheCallback?.Invoke(thumbnailPath);

                    // 使用临时文件机制保存缩略图
                    string actualPath = await SaveThumbnailAsync(thumbnail, thumbnailPath);
                    return actualPath;
                }

                return null;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ThumbnailService] 生成缩略图异常: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 为图片文件生成缩略图
        /// </summary>
        /// <param name="imagePath">图片文件路径</param>
        /// <returns>缩略图BitmapSource</returns>
        public async Task<BitmapSource?> GenerateImageThumbnailAsync(string imagePath)
        {
            try
            {
                if (string.IsNullOrEmpty(imagePath) || !File.Exists(imagePath))
                    return null;

                // 使用OpenCV加载图像以支持更多格式
                using var mat = Cv2.ImRead(imagePath, ImreadModes.Color);
                if (mat.Empty())
                    return null;

                return await CreateThumbnailFromMatAsync(mat);
            }
            catch (Exception ex)
            {
                return null;
            }
        }

        /// <summary>
        /// 从Mat创建缩略图
        /// </summary>
        /// <param name="mat">源图像</param>
        /// <returns>缩略图BitmapSource</returns>
        private async Task<BitmapSource?> CreateThumbnailFromMatAsync(Mat mat)
        {
            return await Task.Run(() =>
            {
                try
                {
                    if (mat.Empty())
                        return null;

                    // 计算缩放比例，保持宽高比
                    double scale = Math.Min((double)ThumbnailSize / mat.Width, (double)ThumbnailSize / mat.Height);
                    int newWidth = (int)(mat.Width * scale);
                    int newHeight = (int)(mat.Height * scale);

                    // 缩放图像
                    using var resized = new Mat();
                    Cv2.Resize(mat, resized, new OpenCvSharp.Size(newWidth, newHeight), 0, 0, InterpolationFlags.Area);

                    // 转换为BitmapSource
                    return Application.Current.Dispatcher.Invoke(() =>
                    {
                        // 将Mat转换为字节数组
                        var bytes = resized.ToBytes(".png");

                        // 从字节数组创建BitmapImage，确保MemoryStream被正确释放
                        var bitmap = new BitmapImage();
                        bitmap.BeginInit();
                        using (var memoryStream = new MemoryStream(bytes))
                        {
                            bitmap.StreamSource = memoryStream;
                            bitmap.CacheOption = BitmapCacheOption.OnLoad;
                            bitmap.EndInit();
                        }
                        bitmap.Freeze(); // 使其可在其他线程使用

                        return bitmap;
                    });
                }
                catch (Exception ex)
                {
                    return null;
                }
            });
        }

        /// <summary>
        /// 创建改良的默认缩略图（使用Aurora背景和Segoe UI字体）
        /// </summary>
        /// <param name="text">显示的文本</param>
        /// <returns>改良的默认缩略图</returns>
        private BitmapSource CreateEnhancedDefaultThumbnail(string text)
        {
            return Application.Current.Dispatcher.Invoke(() =>
            {
                var visual = new DrawingVisual();
                using (var context = visual.RenderOpen())
                {
                    // 随机选择Aurora背景
                    var selectedAurora = AuroraBackgrounds[_random.Next(AuroraBackgrounds.Length)];
                    var auroraPath = $"pack://application:,,,/Resources/{selectedAurora}";

                    try
                    {
                        // 加载Aurora背景图片
                        var auroraImage = new BitmapImage(new Uri(auroraPath));
                        var imageBrush = new ImageBrush(auroraImage)
                        {
                            Stretch = Stretch.UniformToFill,
                            TileMode = TileMode.None
                        };

                        // 绘制Aurora背景
                        context.DrawRectangle(imageBrush, null, new System.Windows.Rect(0, 0, ThumbnailSize, ThumbnailSize));
                    }
                    catch
                    {
                        // 如果Aurora图片加载失败，使用渐变背景作为后备
                        var gradientBrush = new LinearGradientBrush();
                        gradientBrush.StartPoint = new System.Windows.Point(0, 0);
                        gradientBrush.EndPoint = new System.Windows.Point(1, 1);
                        gradientBrush.GradientStops.Add(new GradientStop(Color.FromRgb(64, 128, 255), 0.0));
                        gradientBrush.GradientStops.Add(new GradientStop(Color.FromRgb(128, 64, 255), 1.0));

                        context.DrawRectangle(gradientBrush, null, new System.Windows.Rect(0, 0, ThumbnailSize, ThumbnailSize));
                    }

                    // 添加半透明遮罩以提高文字可读性
                    var overlayBrush = new SolidColorBrush(Color.FromArgb(120, 0, 0, 0));
                    context.DrawRectangle(overlayBrush, null, new System.Windows.Rect(0, 0, ThumbnailSize, ThumbnailSize));

                    // 使用Segoe UI字体绘制文本
                    var displayText = text.Length > 8 ? text.Substring(0, 8) + "..." : text;
                    var formattedText = new FormattedText(
                        displayText,
                        System.Globalization.CultureInfo.CurrentCulture,
                        FlowDirection.LeftToRight,
                        new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal),
                        14,
                        Brushes.White,
                        VisualTreeHelper.GetDpi(visual).PixelsPerDip);

                    // 添加文字阴影效果
                    var shadowText = new FormattedText(
                        displayText,
                        System.Globalization.CultureInfo.CurrentCulture,
                        FlowDirection.LeftToRight,
                        new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal),
                        14,
                        new SolidColorBrush(Color.FromArgb(180, 0, 0, 0)),
                        VisualTreeHelper.GetDpi(visual).PixelsPerDip);

                    var textX = (ThumbnailSize - formattedText.Width) / 2;
                    var textY = (ThumbnailSize - formattedText.Height) / 2;

                    // 绘制阴影（稍微偏移）
                    context.DrawText(shadowText, new System.Windows.Point(textX + 1, textY + 1));

                    // 绘制主文本
                    context.DrawText(formattedText, new System.Windows.Point(textX, textY));

                    // 添加倒影效果
                    var reflectionY = textY + formattedText.Height + 2;
                    if (reflectionY + formattedText.Height * 0.5 < ThumbnailSize)
                    {
                        var reflectionBrush = new SolidColorBrush(Color.FromArgb(60, 255, 255, 255));
                        var reflectionText = new FormattedText(
                            displayText,
                            System.Globalization.CultureInfo.CurrentCulture,
                            FlowDirection.LeftToRight,
                            new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal),
                            14,
                            reflectionBrush,
                            VisualTreeHelper.GetDpi(visual).PixelsPerDip);

                        // 应用垂直翻转变换
                        context.PushTransform(new ScaleTransform(1, -1, textX + formattedText.Width / 2, reflectionY + formattedText.Height / 2));
                        context.DrawText(reflectionText, new System.Windows.Point(textX, reflectionY));
                        context.Pop();
                    }
                }

                var renderTargetBitmap = new RenderTargetBitmap(
                    ThumbnailSize, ThumbnailSize, 96, 96, PixelFormats.Pbgra32);
                renderTargetBitmap.Render(visual);
                renderTargetBitmap.Freeze();

                return renderTargetBitmap;
            });
        }

        /// <summary>
        /// 创建默认缩略图（保留原有方法作为后备）
        /// </summary>
        /// <param name="text">显示的文本</param>
        /// <returns>默认缩略图</returns>
        private BitmapSource CreateDefaultThumbnail(string text)
        {
            return Application.Current.Dispatcher.Invoke(() =>
            {
                var visual = new DrawingVisual();
                using (var context = visual.RenderOpen())
                {
                    // 绘制背景
                    context.DrawRectangle(Brushes.LightGray, null, new System.Windows.Rect(0, 0, ThumbnailSize, ThumbnailSize));

                    // 绘制文本
                    var formattedText = new FormattedText(
                        text.Length > 10 ? text.Substring(0, 10) + "..." : text,
                        System.Globalization.CultureInfo.CurrentCulture,
                        FlowDirection.LeftToRight,
                        new Typeface("Arial"),
                        12,
                        Brushes.Black,
                        VisualTreeHelper.GetDpi(visual).PixelsPerDip);

                    var textX = (ThumbnailSize - formattedText.Width) / 2;
                    var textY = (ThumbnailSize - formattedText.Height) / 2;
                    context.DrawText(formattedText, new System.Windows.Point(textX, textY));
                }

                var renderTargetBitmap = new RenderTargetBitmap(
                    ThumbnailSize, ThumbnailSize, 96, 96, PixelFormats.Pbgra32);
                renderTargetBitmap.Render(visual);
                renderTargetBitmap.Freeze();

                return renderTargetBitmap;
            });
        }

        /// <summary>
        /// 保存缩略图到文件，使用临时文件和映射机制
        /// </summary>
        /// <param name="thumbnail">缩略图</param>
        /// <param name="filePath">保存路径</param>
        /// <returns>实际保存的文件路径</returns>
        private async Task<string> SaveThumbnailAsync(BitmapSource thumbnail, string filePath)
        {
            System.Diagnostics.Debug.WriteLine($"[ThumbnailService] 开始保存缩略图: {filePath}");
            
            return await Task.Run(() =>
            {
                try
                {
                    // 确保目录存在
                    var directory = Path.GetDirectoryName(filePath);
                    if (string.IsNullOrEmpty(directory))
                    {
                        directory = Path.GetTempPath();
                    }
                    Directory.CreateDirectory(directory);
                    
                    // 先冻结BitmapSource以确保可以在后台线程安全使用
                    if (!thumbnail.IsFrozen)
                    {
                        thumbnail = thumbnail.Clone();
                        thumbnail.Freeze();
                    }

                    // 生成唯一文件名 - 添加时间戳和随机数，确保不会冲突
                    string timestamp = DateTime.Now.ToString("yyyyMMddHHmmss");
                    string random = new Random().Next(10000, 99999).ToString();
                    string fileNameWithoutExt = Path.GetFileNameWithoutExtension(filePath);
                    string uniqueFilePath = Path.Combine(
                        directory,
                        $"{fileNameWithoutExt}.{timestamp}.{random}.png");

                    System.Diagnostics.Debug.WriteLine($"[ThumbnailService] 使用临时文件保存: {uniqueFilePath}");

                    // 保存为PNG
                    var encoder = new PngBitmapEncoder();
                    encoder.Frames.Add(BitmapFrame.Create(thumbnail));
                    
                    using (var stream = new FileStream(uniqueFilePath, FileMode.Create, FileAccess.Write, FileShare.None))
                    {
                        encoder.Save(stream);
                        stream.Flush(); // 确保所有数据写入磁盘
                    }
                    
                    System.Diagnostics.Debug.WriteLine($"[ThumbnailService] 缩略图临时文件保存成功: {uniqueFilePath}");
                    
                    // 保存成功后，创建一个映射文件，用于记录最新的缩略图路径
                    string mapFilePath = Path.Combine(directory, $"{fileNameWithoutExt}.thumbnailmap");
                    File.WriteAllText(mapFilePath, Path.GetFileName(uniqueFilePath));
                    System.Diagnostics.Debug.WriteLine($"[ThumbnailService] 创建映射文件: {mapFilePath}");
                    
                    // 尝试清理旧的缩略图文件（异步，不阻塞主流程）
                    Task.Run(() => 
                    {
                        try 
                        {
                            // 等待一段时间，确保其他操作已完成
                            System.Threading.Thread.Sleep(1000);
                            
                            string pattern = $"{fileNameWithoutExt}.*.png";
                            var files = Directory.GetFiles(directory, pattern)
                                .Where(f => f != uniqueFilePath && !f.EndsWith($"{fileNameWithoutExt}.png", StringComparison.OrdinalIgnoreCase))
                                .ToList();
                            
                            foreach (var oldFile in files)
                            {
                                try
                                {
                                    if (File.Exists(oldFile))
                                    {
                                        File.Delete(oldFile);
                                        System.Diagnostics.Debug.WriteLine($"[ThumbnailService] 删除旧缩略图: {oldFile}");
                                    }
                                }
                                catch
                                {
                                    // 忽略删除旧文件时的错误
                                }
                            }
                        }
                        catch
                        {
                            // 忽略清理过程中的错误
                        }
                    });
                    
                    return uniqueFilePath;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[ThumbnailService] 保存缩略图失败: {ex.Message}");
                    return filePath; // 失败时返回原始路径
                }
            });
        }

        /// <summary>
        /// 带重试机制的文件删除
        /// </summary>
        /// <param name="filePath">文件路径</param>
        private void TryDeleteFileWithRetry(string filePath)
        {
            const int maxRetries = 5;
            const int delayMs = 100;

            for (int i = 0; i < maxRetries; i++)
            {
                try
                {
                    File.Delete(filePath);
                    return; // 删除成功，退出
                }
                catch (IOException) when (i < maxRetries - 1)
                {
                    // 文件被占用，等待后重试
                    System.Threading.Thread.Sleep(delayMs);
                }
                catch (UnauthorizedAccessException) when (i < maxRetries - 1)
                {
                    // 权限问题，等待后重试
                    System.Threading.Thread.Sleep(delayMs);
                }
            }

            // 如果所有重试都失败，记录错误但不抛出异常
            System.Diagnostics.Debug.WriteLine($"[ThumbnailService] 无法删除旧缩略图文件: {filePath}");
        }

        /// <summary>
        /// 加载缩略图
        /// </summary>
        /// <param name="thumbnailPath">缩略图路径</param>
        /// <returns>缩略图BitmapSource</returns>
        public BitmapSource? LoadThumbnail(string thumbnailPath)
        {
            try
            {
                if (string.IsNullOrEmpty(thumbnailPath) || !File.Exists(thumbnailPath))
                    return null;

                // 使用FileStream读取文件到内存，然后立即关闭文件句柄
                byte[] imageBytes;
                using (var fileStream = new FileStream(thumbnailPath, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    imageBytes = new byte[fileStream.Length];
                    fileStream.Read(imageBytes, 0, imageBytes.Length);
                }

                // 从内存中的字节数组创建BitmapImage，确保MemoryStream也被正确释放
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                using (var memoryStream = new System.IO.MemoryStream(imageBytes))
                {
                    bitmap.StreamSource = memoryStream;
                    bitmap.CacheOption = BitmapCacheOption.OnLoad; // 确保图像数据完全加载到内存
                    bitmap.EndInit();
                }
                bitmap.Freeze(); // 冻结以便跨线程使用

                return bitmap;
            }
            catch (Exception ex)
            {
                return null;
            }
        }

        /// <summary>
        /// 清理所有孤立的PNG文件 (没有对应节点图的缩略图文件)
        /// </summary>
        /// <param name="directory">要清理的目录</param>
        private void CleanupOrphanedPngFiles(string directory)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"[ThumbnailService] 开始清理目录中的孤立PNG文件: {directory}");
                
                // 获取目录中所有节点图文件
                var nodeGraphFiles = Directory.GetFiles(directory, "*.nodegraph");
                var nodeGraphBasenames = nodeGraphFiles.Select(path => Path.GetFileNameWithoutExtension(path)).ToHashSet();
                
                                    // 获取目录中所有PNG文件
                    var pngFiles = Directory.GetFiles(directory, "*.png");
                    // 获取所有缩略图映射文件
                    var mapFiles = Directory.GetFiles(directory, "*.thumbnailmap");
                    int removedCount = 0;
                
                // 检查每个PNG文件
                foreach (var pngFile in pngFiles)
                {
                    string filename = Path.GetFileName(pngFile);
                    bool shouldKeep = false;
                    
                    // 检查标准格式的缩略图 (nodegraphname.png)
                    string pngBaseName = Path.GetFileNameWithoutExtension(pngFile);
                    if (nodeGraphBasenames.Contains(pngBaseName))
                    {
                        shouldKeep = true;
                    }
                    
                    // 检查临时格式的缩略图 (nodegraphname.timestamp.random.png)
                    var parts = pngBaseName.Split('.');
                    if (parts.Length >= 3)
                    {
                        string possibleNodeGraphName = parts[0];
                        if (nodeGraphBasenames.Contains(possibleNodeGraphName))
                        {
                            shouldKeep = true;
                        }
                    }
                    
                    // 删除无关的PNG文件
                    if (!shouldKeep)
                    {
                        try
                        {
                            File.Delete(pngFile);
                            removedCount++;
                            System.Diagnostics.Debug.WriteLine($"[ThumbnailService] 已删除孤立PNG文件: {filename}");
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"[ThumbnailService] 删除PNG文件 {filename} 失败: {ex.Message}");
                        }
                    }
                }
                
                                    // 清理孤立的映射文件
                    foreach (var mapFile in mapFiles)
                    {
                        string mapBaseName = Path.GetFileNameWithoutExtension(mapFile).Replace(".thumbnailmap", "");
                        if (!nodeGraphBasenames.Contains(mapBaseName))
                        {
                            try
                            {
                                File.Delete(mapFile);
                                removedCount++;
                                System.Diagnostics.Debug.WriteLine($"[ThumbnailService] 已删除孤立映射文件: {Path.GetFileName(mapFile)}");
                            }
                            catch (Exception ex)
                            {
                                System.Diagnostics.Debug.WriteLine($"[ThumbnailService] 删除映射文件失败: {ex.Message}");
                            }
                        }
                    }
                    
                    System.Diagnostics.Debug.WriteLine($"[ThumbnailService] 已清理 {removedCount} 个孤立文件");
                
                // 递归处理子目录
                foreach (var subDir in Directory.GetDirectories(directory))
                {
                    CleanupOrphanedPngFiles(subDir);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ThumbnailService] 清理孤立PNG文件时出错: {ex.Message}");
            }
        }

        /// <summary>
        /// 获取节点图缩略图路径
        /// </summary>
        /// <param name="nodeGraphPath">节点图路径</param>
        /// <returns>缩略图路径</returns>
        public string GetNodeGraphThumbnailPath(string nodeGraphPath)
        {
            var nodeGraphName = Path.GetFileNameWithoutExtension(nodeGraphPath);
            var projectFolder = Path.GetDirectoryName(nodeGraphPath) ?? _workFolderService.NodeGraphsFolder;
            
            // 尝试查找映射文件来获取最新缩略图
            string mapFilePath = Path.Combine(projectFolder, $"{nodeGraphName}.thumbnailmap");
            
            try
            {
                if (File.Exists(mapFilePath))
                {
                    // 读取映射文件获取最新缩略图文件名
                    string latestThumbnailName = File.ReadAllText(mapFilePath).Trim();
                    if (!string.IsNullOrEmpty(latestThumbnailName))
                    {
                        string fullPath = Path.Combine(projectFolder, latestThumbnailName);
                        if (File.Exists(fullPath))
                        {
                            System.Diagnostics.Debug.WriteLine($"[ThumbnailService] 从映射文件获取缩略图: {fullPath}");
                            return fullPath;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ThumbnailService] 读取映射文件失败: {ex.Message}");
            }
            
            // 如果没有找到映射文件或最新缩略图，尝试查找所有匹配的缩略图并返回最新的
            try
            {
                string pattern = $"{nodeGraphName}.*.png";
                var files = Directory.GetFiles(projectFolder, pattern);
                if (files.Length > 0)
                {
                    // 按修改时间排序，返回最新的
                    var latestFile = files.OrderByDescending(f => new FileInfo(f).LastWriteTime).FirstOrDefault();
                    if (latestFile != null)
                    {
                        System.Diagnostics.Debug.WriteLine($"[ThumbnailService] 通过搜索找到最新缩略图: {latestFile}");
                        return latestFile;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ThumbnailService] 搜索缩略图失败: {ex.Message}");
            }
            
            // 回退到默认路径
            string defaultPath = Path.Combine(projectFolder, $"{nodeGraphName}.png");
            return defaultPath;
        }

        /// <summary>
        /// 检查缩略图是否存在
        /// </summary>
        /// <param name="nodeGraphPath">节点图路径</param>
        /// <returns>缩略图是否存在</returns>
        public bool ThumbnailExists(string nodeGraphPath)
        {
            var thumbnailPath = GetNodeGraphThumbnailPath(nodeGraphPath);
            return File.Exists(thumbnailPath);
        }

        /// <summary>
        /// 删除缩略图
        /// </summary>
        /// <param name="nodeGraphPath">节点图路径</param>
        public void DeleteThumbnail(string nodeGraphPath)
        {
            try
            {
                var thumbnailPath = GetNodeGraphThumbnailPath(nodeGraphPath);
                if (File.Exists(thumbnailPath))
                {
                    File.Delete(thumbnailPath);
                }
            }
            catch (Exception ex)
            {
            }
        }
    }
}
