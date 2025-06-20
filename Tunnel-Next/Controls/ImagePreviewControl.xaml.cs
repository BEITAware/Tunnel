using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using OpenCvSharp;
using OpenCvSharp.WpfExtensions;
using Microsoft.Win32;
using System.Collections.Generic;
using System;
using System.Runtime.InteropServices;
using System.Windows.Threading;
using System.Drawing;
using System.Windows.Interop;

namespace Tunnel_Next.Controls
{
    /// <summary>
    /// 高性能图像预览控件 - Revival Scripts系统专用版本
    /// 专为Revival Scripts节点系统设计，支持f32bmp格式图像显示和实时预览
    /// </summary>
    public partial class ImagePreviewControl : UserControl
    {
        public static readonly DependencyProperty ImageSourceProperty =
            DependencyProperty.Register(nameof(ImageSource), typeof(Mat), typeof(ImagePreviewControl),
                new PropertyMetadata(null, OnImageSourceChanged));

        // 核心属性 - Revival Scripts系统
        private Mat? _currentImage;
        private BitmapSource? _originalBitmap;
        private double _zoomFactor = 1.0;
        private const double MIN_ZOOM = 0.001;
        private const double MAX_ZOOM = 20.0;
        private const double ZOOM_STEP = 1.2;

        // 转换缓冲区大小限制
        private const int MAX_BUFFER_SIZE = 100 * 1024 * 1024; // 100MB

        // 交互状态
        private bool _isPanning = false;
        private System.Windows.Point _lastPanPoint;

        // 性能优化标志
        private bool _isUpdating = false;
        private bool _preserveCurrentImage = false; // 标志：是否保持当前图像不被清除

        public Mat? ImageSource
        {
            get => (Mat?)GetValue(ImageSourceProperty);
            set => SetValue(ImageSourceProperty, value);
        }

        public double ZoomFactor
        {
            get => _zoomFactor;
            set
            {
                var newZoom = Math.Max(MIN_ZOOM, Math.Min(MAX_ZOOM, value));
                if (Math.Abs(_zoomFactor - newZoom) > 0.001)
                {
                    _zoomFactor = newZoom;
                    UpdateImageDisplay();
                    ZoomChanged?.Invoke(_zoomFactor);
                }
            }
        }

        // 缩放变化事件
        public event Action<double>? ZoomChanged;

        public ImagePreviewControl()
        {
            InitializeComponent();

            // 添加鼠标事件处理
            ImageContainer.MouseMove += OnMouseMove;
            ImageContainer.MouseLeftButtonDown += OnMouseLeftButtonDown;
            ImageContainer.MouseLeftButtonUp += OnMouseLeftButtonUp;
            ImageContainer.MouseRightButtonDown += OnMouseRightButtonDown;
            ImageContainer.MouseRightButtonUp += OnMouseRightButtonUp;
            ImageContainer.MouseWheel += OnMouseWheel;

            // 注册事件以更新 PreviewState
            ZoomChanged += z => Tunnel_Next.Services.UI.PreviewState.Zoom = z;

            // 初始化时将默认值写入
            Tunnel_Next.Services.UI.PreviewState.Zoom = _zoomFactor;

            // 监听滚动
            ImageScrollViewer.ScrollChanged += (s, e) =>
            {
                Tunnel_Next.Services.UI.PreviewState.ScrollOffsetX = e.HorizontalOffset;
                Tunnel_Next.Services.UI.PreviewState.ScrollOffsetY = e.VerticalOffset;
            };
        }

        private static void OnImageSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ImagePreviewControl control)
            {
                control.UpdateImage();
            }
        }

        private void UpdateImage()
        {
            if (_isUpdating) return;
            _isUpdating = true;

            try
            {
                _currentImage = ImageSource;

                if (_currentImage == null || _currentImage.IsDisposed || _currentImage.Empty())
                {
                    // 如果设置了保持当前图像，则不清除
                    if (_preserveCurrentImage && _originalBitmap != null)
                    {
                        return;
                    }

                    PreviewImage.Source = null;
                    NoImageText.Visibility = Visibility.Visible;
                    ImageInfoText.Text = "无图像";
                    _originalBitmap = null;
                    MousePositionText.Text = "位置: N/A";
                    return;
                }

                var imageSize = _currentImage.Width * _currentImage.Height;
                if (imageSize > 50_000_000) // 超过5000万像素
                {
                    PreviewImage.Source = null;
                    NoImageText.Visibility = Visibility.Visible;
                    NoImageText.Text = "图像过大，无法预览";
                    ImageInfoText.Text = $"图像过大: {_currentImage.Width}x{_currentImage.Height}";
                    _originalBitmap = null;
                    MousePositionText.Text = "位置: N/A";
                    return;
                }

                var formatInfo = GetRevivalScriptImageFormat(_currentImage);
                var alphaInfo = GetAlphaChannelInfo(_currentImage);

                ImageInfoText.Text = $"尺寸: {_currentImage.Width} × {_currentImage.Height}, " +
                                   $"通道: {_currentImage.Channels()}, " +
                                   $"格式: {formatInfo}" +
                                   (string.IsNullOrEmpty(alphaInfo) ? "" : $", {alphaInfo}");


                try
                {
                    BitmapSource? bitmapSource = null;

                    if (formatInfo.Contains("F32BMP") || formatInfo.Contains("F32BGRA") || formatInfo.Contains("F32BGR"))
                    {
                        bitmapSource = ConvertF32BMPtoBitmapSource(_currentImage);
                    }
                    else
                    {
                        var requiredBytes = _currentImage.Width * _currentImage.Height * _currentImage.Channels() * (_currentImage.Depth() == MatType.CV_8U ? 1 : 4);

                        if (requiredBytes > MAX_BUFFER_SIZE)
                        {
                            using var scaledMat = ResizeImageForPreview(_currentImage);
                            bitmapSource = scaledMat.ToBitmapSource();
                        }
                        else
                        {
                            bitmapSource = _currentImage.ToBitmapSource();
                        }
                    }

                    if (bitmapSource != null)
                    {

                    bitmapSource.Freeze();
                    _originalBitmap = bitmapSource;

                    NoImageText.Visibility = Visibility.Collapsed;
                    UpdateTransparencyBackground();
                    UpdateImageDisplay();
                    }
                    else
                    {
                        throw new InvalidOperationException("图像转换失败：生成的BitmapSource为null");
                    }
                }
                catch (Exception ex)
                {
                    if (ex.InnerException != null)
                    {
                    }

                    PreviewImage.Source = null;
                    NoImageText.Visibility = Visibility.Visible;
                    NoImageText.Text = "Revival Scripts图像转换失败";
                    _originalBitmap = null;

                    TryAlternativeImageConversion();
                }
            }
            catch (Exception ex)
            {
                PreviewImage.Source = null;
                NoImageText.Visibility = Visibility.Visible;
                NoImageText.Text = $"图像加载失败: {ex.Message}";
                ImageInfoText.Text = "图像错误";
                _originalBitmap = null;
            }
            finally
            {
                _isUpdating = false;
            }
        }

        private BitmapSource ConvertF32BMPtoBitmapSource(Mat mat)
        {
            int width = mat.Width;
            int height = mat.Height;
            int channels = mat.Channels();

            long requiredMem = (long)width * height * channels * 4; // 每个浮点数占4字节
            if (requiredMem > MAX_BUFFER_SIZE)
            {
                using var resizedMat = ResizeImageForPreview(mat);
                return ConvertF32BMPtoBitmapSource(resizedMat);
            }

            byte[] outputBuffer = new byte[width * height * 4]; // BGRA 8位格式

            try
            {
                const int CHUNK_HEIGHT = 128; // 每次处理128行

                // 使用安全的逐像素访问，避免unsafe内存操作
                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        int destIdx = (y * width + x) * 4; // 目标BGRA格式索引

                        if (channels == 3) // BGR 图像
                        {
                            var pixel = mat.At<Vec3f>(y, x);
                            outputBuffer[destIdx + 0] = (byte)(Math.Clamp(pixel.Item0, 0.0f, 1.0f) * 255.0f); // B
                            outputBuffer[destIdx + 1] = (byte)(Math.Clamp(pixel.Item1, 0.0f, 1.0f) * 255.0f); // G
                            outputBuffer[destIdx + 2] = (byte)(Math.Clamp(pixel.Item2, 0.0f, 1.0f) * 255.0f); // R
                            outputBuffer[destIdx + 3] = 255; // Alpha (不透明)
                        }
                        else if (channels == 1) // 灰度图像
                        {
                            var pixel = mat.At<float>(y, x);
                            byte gray = (byte)(Math.Clamp(pixel, 0.0f, 1.0f) * 255.0f);
                            outputBuffer[destIdx + 0] = gray; // B
                            outputBuffer[destIdx + 1] = gray; // G
                            outputBuffer[destIdx + 2] = gray; // R
                            outputBuffer[destIdx + 3] = 255;  // A
                        }
                        else if (channels == 4) // BGRA 图像 (OpenCV标准格式)
                        {
                            var pixel = mat.At<Vec4f>(y, x);
                            outputBuffer[destIdx + 0] = (byte)(Math.Clamp(pixel.Item0, 0.0f, 1.0f) * 255.0f); // B
                            outputBuffer[destIdx + 1] = (byte)(Math.Clamp(pixel.Item1, 0.0f, 1.0f) * 255.0f); // G
                            outputBuffer[destIdx + 2] = (byte)(Math.Clamp(pixel.Item2, 0.0f, 1.0f) * 255.0f); // R
                            outputBuffer[destIdx + 3] = (byte)(Math.Clamp(pixel.Item3, 0.0f, 1.0f) * 255.0f); // A
                        }
                    }

                    // 定期进行垃圾回收以避免内存压力
                    if (y > 0 && y % 256 == 0)
                    {
                        GC.Collect(0, GCCollectionMode.Optimized);
                    }
                }

                int bitmapStride = width * 4; // BGRA格式，每像素4字节
                BitmapSource bitmapSource = BitmapSource.Create(
                    width, height,
                    96, 96, // DPI设置
                    PixelFormats.Bgra32,
                    null,
                    outputBuffer,
                    bitmapStride);

                bitmapSource.Freeze();
                return bitmapSource;
            }
            catch (Exception ex)
            {

                try
                {
                    using var converted = new Mat();
                    mat.ConvertTo(converted, MatType.CV_8UC3, 255.0);
                    return converted.ToBitmapSource();
                }
                catch
                {
                    throw;
                }
            }
        }

        private Mat ResizeImageForPreview(Mat originalImage)
        {
            int maxPreviewDimension = 1024; // 最大预览尺寸
            double scale = 1.0;

            if (originalImage.Width > maxPreviewDimension || originalImage.Height > maxPreviewDimension)
            {
                double scaleW = maxPreviewDimension / (double)originalImage.Width;
                double scaleH = maxPreviewDimension / (double)originalImage.Height;
                scale = Math.Min(scaleW, scaleH);
            }

            if (Math.Abs(scale - 1.0) < 0.01)
            {
                return originalImage.Clone();
            }

            int newWidth = (int)(originalImage.Width * scale);
            int newHeight = (int)(originalImage.Height * scale);

            Mat resized = new Mat();
            Cv2.Resize(originalImage, resized, new OpenCvSharp.Size(newWidth, newHeight), 0, 0, InterpolationFlags.Area);


            return resized;
        }

        private void TryAlternativeImageConversion()
        {
            if (_currentImage == null || _currentImage.Empty()) return;

            try
            {
                using var smallerMat = ResizeImageForPreview(_currentImage);

                // 创建转换后的Mat，但不使用using，因为需要在异步回调中使用
                Mat convertedMat = new Mat();

                try
                {
                    if (_currentImage.Type().Depth == MatType.CV_32F)
                    {
                        // 检查是否为BGRA格式
                        if (smallerMat.Channels() == 4)
                        {
                            // BGRA格式，直接转换为8位BGRA
                            smallerMat.ConvertTo(convertedMat, MatType.CV_8UC4, 255.0);
                        }
                        else
                        {
                            smallerMat.ConvertTo(convertedMat, MatType.CV_8U, 255.0);
                        }
                    }
                    else
                    {
                        smallerMat.CopyTo(convertedMat);
                    }

                    // 立即转换为BitmapSource，避免异步访问Mat
                    var bitmapSource = convertedMat.ToBitmapSource();
                    if (bitmapSource != null)
                    {
                        bitmapSource.Freeze();

                        // 在UI线程上设置图像
                        Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
                        {
                            _originalBitmap = bitmapSource;
                            PreviewImage.Source = bitmapSource;
                            NoImageText.Visibility = Visibility.Collapsed;
                        }));
                    }
                    else
                    {
                        TrySystemDrawingConversion(_currentImage);
                    }
                }
                finally
                {
                    // 确保Mat被释放
                    convertedMat?.Dispose();
                }
            }
            catch (Exception ex)
            {
                TrySystemDrawingConversion(_currentImage);
            }
        }

        private void TrySystemDrawingConversion(Mat mat)
        {
            try
            {

                Mat matToConvert = mat;
                bool needsDispose = false;

                if (mat.Width > 1024 || mat.Height > 1024)
                {
                    double scale = 1024.0 / Math.Max(mat.Width, mat.Height);
                    int newWidth = (int)(mat.Width * scale);
                    int newHeight = (int)(mat.Height * scale);

                    matToConvert = new Mat();
                    Cv2.Resize(mat, matToConvert, new OpenCvSharp.Size(newWidth, newHeight));
                    needsDispose = true;
                }

                if (matToConvert.Type().Depth == MatType.CV_32F)
                {
                    Mat temp = new Mat();

                    // 检查是否为BGRA格式
                    if (matToConvert.Channels() == 4)
                    {
                        // BGRA格式，提取BGR通道（丢弃Alpha用于临时文件保存）
                        var bgraMat = new Mat();
                        matToConvert.ConvertTo(bgraMat, MatType.CV_8UC4, 255.0);

                        // 提取BGR通道（保持原有顺序）
                        var channels = bgraMat.Split();
                        Cv2.Merge(new Mat[] { channels[0], channels[1], channels[2] }, temp); // B,G,R

                        // 清理资源
                        bgraMat.Dispose();
                        foreach (var ch in channels) ch.Dispose();
                    }
                    else
                    {
                        matToConvert.ConvertTo(temp, MatType.CV_8U, 255.0);
                    }

                    if (needsDispose)
                    {
                        matToConvert.Dispose();
                    }

                    matToConvert = temp;
                    needsDispose = true;
                }

                try
                {
                    string tempFile = System.IO.Path.Combine(
                        System.IO.Path.GetTempPath(),
                        $"revival_preview_{Guid.NewGuid()}.png");

                    Cv2.ImWrite(tempFile, matToConvert);

                    using var bitmap = new System.Drawing.Bitmap(tempFile);

                    var bitmapSource = System.Windows.Interop.Imaging.CreateBitmapSourceFromHBitmap(
                        bitmap.GetHbitmap(),
                        IntPtr.Zero,
                        Int32Rect.Empty,
                        BitmapSizeOptions.FromEmptyOptions());

                    try { System.IO.File.Delete(tempFile); } catch { }

                    if (bitmapSource != null)
                    {
                        bitmapSource.Freeze();
                        Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
                        {
                            _originalBitmap = bitmapSource;
                            PreviewImage.Source = bitmapSource;
                            NoImageText.Visibility = Visibility.Collapsed;
                        }));
                    }
                }
                finally
                {
                    if (needsDispose && matToConvert != null)
                    {
                        matToConvert.Dispose();
                    }
                }
            }
            catch (Exception ex)
            {
            }
        }

        private void UpdateImageDisplay()
        {

            if (_originalBitmap == null)
            {
                return;
            }

            try
            {

                PreviewImage.Source = _originalBitmap;
                PreviewImage.Visibility = Visibility.Visible;

                var scaledWidth = _originalBitmap.PixelWidth * _zoomFactor;
                var scaledHeight = _originalBitmap.PixelHeight * _zoomFactor;

                PreviewImage.Width = scaledWidth;
                PreviewImage.Height = scaledHeight;

                PreviewImage.RenderTransform = null;

                ImageContainer.Width = double.NaN;
                ImageContainer.Height = double.NaN;

                UpdateRenderingQuality();
                UpdateTransparencyBackground();

            }
            catch (Exception ex)
            {
            }
        }

        private void UpdateRenderingQuality()
        {
            if (_zoomFactor < 0.1)
            {
                RenderOptions.SetBitmapScalingMode(PreviewImage, BitmapScalingMode.LowQuality);
                RenderOptions.SetEdgeMode(PreviewImage, EdgeMode.Aliased);
            }
            else if (_zoomFactor < 0.5)
            {
                RenderOptions.SetBitmapScalingMode(PreviewImage, BitmapScalingMode.Linear);
                RenderOptions.SetEdgeMode(PreviewImage, EdgeMode.Aliased);
            }
            else
            {
                RenderOptions.SetBitmapScalingMode(PreviewImage, BitmapScalingMode.Fant);
                RenderOptions.SetEdgeMode(PreviewImage, EdgeMode.Unspecified);
            }
        }

        private void UpdateTransparencyBackground()
        {
            // 检查当前图像是否有Alpha通道
            bool hasAlpha = _currentImage != null && !_currentImage.Empty() && _currentImage.Channels() == 4;

            if (hasAlpha)
            {
                // 显示透明度棋盘背景
                TransparencyBackground.Visibility = Visibility.Visible;

                // 设置背景尺寸与图像匹配
                if (_originalBitmap != null)
                {
                    TransparencyBackground.Width = _originalBitmap.PixelWidth * _zoomFactor;
                    TransparencyBackground.Height = _originalBitmap.PixelHeight * _zoomFactor;
                }
            }
            else
            {
                // 隐藏透明度棋盘背景
                TransparencyBackground.Visibility = Visibility.Collapsed;
            }
        }

        #region 公共方法

        public void ZoomFit()
        {
            if (_currentImage == null || _originalBitmap == null)
                return;

            var viewportWidth = ImageScrollViewer.ViewportWidth;
            var viewportHeight = ImageScrollViewer.ViewportHeight;


            if (viewportWidth <= 0 || viewportHeight <= 0 || double.IsNaN(viewportWidth) || double.IsNaN(viewportHeight))
            {
                _zoomFactor = 1.0;
                UpdateImageDisplay();
                ZoomChanged?.Invoke(_zoomFactor);
                return;
            }

            var scaleX = viewportWidth / _originalBitmap.PixelWidth;
            var scaleY = viewportHeight / _originalBitmap.PixelHeight;

            var fitZoom = Math.Min(scaleX, scaleY);
            fitZoom = Math.Max(MIN_ZOOM, Math.Min(MAX_ZOOM, fitZoom));


            _zoomFactor = fitZoom;
            UpdateImageDisplay();
            ZoomChanged?.Invoke(_zoomFactor);

            CenterImage();
        }

        public void ZoomIn()
        {
            var centerPoint = new System.Windows.Point(
                ImageScrollViewer.ViewportWidth / 2,
                ImageScrollViewer.ViewportHeight / 2);
            ZoomAtPoint(centerPoint, ZOOM_STEP);
        }

        public void ZoomOut()
        {
            var centerPoint = new System.Windows.Point(
                ImageScrollViewer.ViewportWidth / 2,
                ImageScrollViewer.ViewportHeight / 2);
            ZoomAtPoint(centerPoint, 1.0 / ZOOM_STEP);
        }

        public void SaveCurrentImage()
        {
            if (_currentImage == null)
                return;

            var saveDialog = new SaveFileDialog
            {
                Filter = "PNG 图像 (*.png)|*.png|TIFF 图像 (*.tiff)|*.tiff|JPEG 图像 (*.jpg)|*.jpg|BMP 图像 (*.bmp)|*.bmp|所有文件 (*.*)|*.*",
                DefaultExt = "png",
                AddExtension = true,
                Title = "保存图像"
            };

            if (saveDialog.ShowDialog() == true)
            {
                try
                {
                    var extension = System.IO.Path.GetExtension(saveDialog.FileName).ToLower();
                    var supportsAlpha = extension == ".png" || extension == ".tiff" || extension == ".tif";

                    Mat imageToSave;

                    if (_currentImage.Type().Depth == MatType.CV_32F)
                    {
                        // 32位浮点图像需要转换
                        if (_currentImage.Channels() == 4 && supportsAlpha)
                        {
                            // 保存RGBA到支持Alpha的格式
                            imageToSave = new Mat();
                            _currentImage.ConvertTo(imageToSave, MatType.CV_8UC4, 255.0);
                        }
                        else if (_currentImage.Channels() == 4)
                        {
                            // 转换BGRA到BGR（丢弃Alpha）
                            var bgrMat = new Mat();
                            var channels = _currentImage.Split();
                            var bgrChannels = new Mat[] { channels[0], channels[1], channels[2] }; // B,G,R
                            Cv2.Merge(bgrChannels, bgrMat);

                            imageToSave = new Mat();
                            bgrMat.ConvertTo(imageToSave, MatType.CV_8UC3, 255.0);

                            // 清理资源
                            bgrMat.Dispose();
                            foreach (var ch in channels) ch.Dispose();
                        }
                        else
                        {
                            // 其他格式直接转换
                            imageToSave = new Mat();
                            _currentImage.ConvertTo(imageToSave, MatType.CV_8U, 255.0);
                        }
                    }
                    else
                    {
                        imageToSave = _currentImage.Clone();
                    }

                    Cv2.ImWrite(saveDialog.FileName, imageToSave);

                    if (imageToSave != _currentImage)
                    {
                        imageToSave.Dispose();
                    }

                    var alphaInfo = (_currentImage.Channels() == 4 && supportsAlpha) ? " (包含Alpha通道)" : "";
                    MessageBox.Show($"图像保存成功！{alphaInfo}", "保存", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"保存图像失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        public void ClearImage()
        {
            _preserveCurrentImage = false; // 明确清除时，不保持图像
            ImageSource = null;
        }

        /// <summary>
        /// 设置是否在处理过程中保持当前图像
        /// </summary>
        /// <param name="preserve">true: 保持当前图像，false: 允许清除图像</param>
        public void SetPreserveCurrentImage(bool preserve)
        {
            _preserveCurrentImage = preserve;
        }

        public void SetRevivalScriptImage(Mat? image, Dictionary<string, object>? metadata = null)
        {
            ImageSource = image;

            if (metadata != null && metadata.Count > 0)
            {
            }
        }

        private void CenterImage()
        {
            if (_originalBitmap == null)
                return;

            ImageContainer.UpdateLayout();

            var imageWidth = _originalBitmap.PixelWidth * _zoomFactor;
            var imageHeight = _originalBitmap.PixelHeight * _zoomFactor;
            var viewportWidth = ImageScrollViewer.ViewportWidth;
            var viewportHeight = ImageScrollViewer.ViewportHeight;

            var centerX = Math.Max(0, (imageWidth - viewportWidth) / 2);
            var centerY = Math.Max(0, (imageHeight - viewportHeight) / 2);

            ImageScrollViewer.ScrollToHorizontalOffset(centerX);
            ImageScrollViewer.ScrollToVerticalOffset(centerY);

        }

        public static void SetProcessingState(bool isProcessing)
        {
        }

        private static string GetRevivalScriptImageFormat(Mat image)
        {
            if (image == null || image.Empty())
                return "无效";

            var depth = image.Depth();
            var channels = image.Channels();

            // Revival Scripts BGRA一等公民架构 (遵循OpenCV约定)
            if (depth == MatType.CV_32F && channels == 4)
                return "F32BGRA (Revival主格式)";
            else if (depth == MatType.CV_32F && channels == 3)
                return "F32BGR (OpenCV格式)";
            else if (depth == MatType.CV_8U && channels == 4)
                return "8位BGRA";
            else if (depth == MatType.CV_8U && channels == 3)
                return "8位BGR";
            else if (depth == MatType.CV_16U && channels == 4)
                return "16位BGRA";
            else if (depth == MatType.CV_16U && channels == 3)
                return "16位BGR";
            else if (depth == MatType.CV_32F && channels == 1)
                return "32位灰度";
            else if (depth == MatType.CV_8U && channels == 1)
                return "8位灰度";
            else
                return $"{depth}_{channels}通道";
        }

        private static string GetAlphaChannelInfo(Mat image)
        {
            if (image == null || image.Empty() || image.Channels() != 4)
                return "";

            try
            {
                // 分离Alpha通道
                var channels = image.Split();
                var alphaChannel = channels[3];

                // 计算Alpha通道统计信息
                Cv2.MinMaxLoc(alphaChannel, out double minAlpha, out double maxAlpha);
                var meanAlpha = Cv2.Mean(alphaChannel);

                // 清理资源
                foreach (var ch in channels) ch.Dispose();

                // 格式化Alpha信息
                if (image.Type().Depth == MatType.CV_32F)
                {
                    // 32位浮点格式 (0.0-1.0)
                    return $"Alpha: [{minAlpha:F2}-{maxAlpha:F2}] (均值: {meanAlpha.Val0:F2})";
                }
                else
                {
                    // 8位格式 (0-255)
                    return $"Alpha: [{minAlpha:F0}-{maxAlpha:F0}] (均值: {meanAlpha.Val0:F0})";
                }
            }
            catch
            {
                return "Alpha: [信息不可用]";
            }
        }

        public void Dispose()
        {
            _originalBitmap = null;
            _currentImage = null;
        }

        #endregion

        #region 鼠标事件处理

        private void OnMouseMove(object sender, MouseEventArgs e)
        {
            if (_isPanning)
            {
                var currentPanPoint = e.GetPosition(ImageScrollViewer);
                var delta = new Vector(currentPanPoint.X - _lastPanPoint.X, currentPanPoint.Y - _lastPanPoint.Y);
                _lastPanPoint = currentPanPoint;

                ImageScrollViewer.ScrollToHorizontalOffset(ImageScrollViewer.HorizontalOffset - delta.X);
                ImageScrollViewer.ScrollToVerticalOffset(ImageScrollViewer.VerticalOffset - delta.Y);
            }

            // 更新鼠标位置显示
            // 首先确保 _currentImage 和 _originalBitmap 都有效，并且控件尺寸有效
            if (_currentImage != null && !_currentImage.IsDisposed && !_currentImage.Empty() &&
                _originalBitmap != null && PreviewImage.Source != null &&
                PreviewImage.ActualWidth > 0 && PreviewImage.ActualHeight > 0)
            {
                var imagePosition = e.GetPosition(PreviewImage);

                // 计算在原始图像中的坐标
                var imageX = (int)(imagePosition.X * _originalBitmap.PixelWidth / PreviewImage.ActualWidth);
                var imageY = (int)(imagePosition.Y * _originalBitmap.PixelHeight / PreviewImage.ActualHeight);

                // 再次确认 _currentImage 仍然有效（理论上如果上面检查通过，这里应该也是）
                // 并且 imageX 和 imageY 在 _currentImage 的有效范围内
                if (imageX >= 0 && imageX < _currentImage.Width &&
                    imageY >= 0 && imageY < _currentImage.Height)
                {
                    // 获取像素值逻辑可以放在这里，如果需要
                    // 例如: Vec3f pixelValue = _currentImage.At<Vec3f>(imageY, imageX);
                    MousePositionText.Text = $"位置: ({imageX}, {imageY})";
                }
                else
                {
                    MousePositionText.Text = "位置: (越界)";
                }
            }
            else
            {
                // 如果图像无效或控件尺寸无效，不显示坐标
                MousePositionText.Text = "位置: N/A";
            }
        }

        private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (Keyboard.Modifiers == ModifierKeys.Alt)
            {
                _isPanning = true;
                _lastPanPoint = e.GetPosition(ImageScrollViewer);
                ImageContainer.CaptureMouse();
                ImageContainer.Cursor = Cursors.Hand;
            }
        }

        private void OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (_isPanning)
            {
                _isPanning = false;
                ImageContainer.ReleaseMouseCapture();
                ImageContainer.Cursor = Cursors.Arrow;
            }
        }

        private void OnMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            _isPanning = true;
            _lastPanPoint = e.GetPosition(ImageScrollViewer);
            ImageContainer.CaptureMouse();
            ImageContainer.Cursor = Cursors.Hand;
        }

        private void OnMouseRightButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (_isPanning)
            {
                _isPanning = false;
                ImageContainer.ReleaseMouseCapture();
                ImageContainer.Cursor = Cursors.Arrow;
            }
        }

        private void OnMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (Keyboard.Modifiers == ModifierKeys.Control)
            {
                ZoomAtPoint(e.GetPosition(ImageScrollViewer), e.Delta > 0 ? ZOOM_STEP : 1.0 / ZOOM_STEP);
                e.Handled = true;
            }
        }

        private void ZoomAtPoint(System.Windows.Point centerPoint, double zoomDelta)
        {
            if (_originalBitmap == null)
                return;

            var oldZoomFactor = _zoomFactor;

            var newZoomFactor = Math.Max(MIN_ZOOM, Math.Min(MAX_ZOOM, _zoomFactor * zoomDelta));

            if (Math.Abs(newZoomFactor - oldZoomFactor) < 0.001)
                return;

            var oldScrollX = ImageScrollViewer.HorizontalOffset;
            var oldScrollY = ImageScrollViewer.VerticalOffset;

            var oldImageWidth = _originalBitmap.PixelWidth * oldZoomFactor;
            var oldImageHeight = _originalBitmap.PixelHeight * oldZoomFactor;
            var newImageWidth = _originalBitmap.PixelWidth * newZoomFactor;
            var newImageHeight = _originalBitmap.PixelHeight * newZoomFactor;

            var relativeX = (oldScrollX + centerPoint.X) / oldImageWidth;
            var relativeY = (oldScrollY + centerPoint.Y) / oldImageHeight;

            var newScrollX = relativeX * newImageWidth - centerPoint.X;
            var newScrollY = relativeY * newImageHeight - centerPoint.Y;

            _zoomFactor = newZoomFactor;
            UpdateImageDisplay();

            ImageContainer.UpdateLayout();

            ImageScrollViewer.ScrollToHorizontalOffset(Math.Max(0, newScrollX));
            ImageScrollViewer.ScrollToVerticalOffset(Math.Max(0, newScrollY));


            ZoomChanged?.Invoke(_zoomFactor);
        }

        #endregion
    }
}