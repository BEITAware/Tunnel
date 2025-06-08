using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using OpenCvSharp;
using Tunnel_Next.Models;

namespace Tunnel_Next.Services
{
    /// <summary>
    /// 缩略图生成服务
    /// </summary>
    public class ThumbnailService
    {
        private const int ThumbnailSize = 128;
        private readonly WorkFolderService _workFolderService;

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="workFolderService">工作文件夹服务</param>
        public ThumbnailService(WorkFolderService workFolderService)
        {
            _workFolderService = workFolderService ?? throw new ArgumentNullException(nameof(workFolderService));
        }

        /// <summary>
        /// 为节点图生成缩略图
        /// </summary>
        /// <param name="nodeGraphPath">节点图文件路径</param>
        /// <param name="previewImage">预览图像（Mat格式）</param>
        /// <returns>缩略图文件路径</returns>
        public async Task<string?> GenerateNodeGraphThumbnailAsync(string nodeGraphPath, Mat? previewImage = null)
        {
            try
            {
                if (string.IsNullOrEmpty(nodeGraphPath) || !File.Exists(nodeGraphPath))
                    return null;

                var nodeGraphName = Path.GetFileNameWithoutExtension(nodeGraphPath);
                var thumbnailPath = Path.Combine(_workFolderService.ThumbnailsFolder, $"{nodeGraphName}.png");

                BitmapSource? thumbnail = null;

                if (previewImage != null && !previewImage.Empty())
                {
                    // 从预览图像生成缩略图
                    thumbnail = await CreateThumbnailFromMatAsync(previewImage);
                }
                else
                {
                    // 创建默认缩略图
                    thumbnail = CreateDefaultThumbnail(nodeGraphName);
                }

                if (thumbnail != null)
                {
                    await SaveThumbnailAsync(thumbnail, thumbnailPath);
                    return thumbnailPath;
                }

                return null;
            }
            catch (Exception ex)
            {
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

                        // 从字节数组创建BitmapImage
                        var bitmap = new BitmapImage();
                        bitmap.BeginInit();
                        bitmap.StreamSource = new MemoryStream(bytes);
                        bitmap.CacheOption = BitmapCacheOption.OnLoad;
                        bitmap.EndInit();
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
        /// 创建默认缩略图
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
        /// 保存缩略图到文件
        /// </summary>
        /// <param name="thumbnail">缩略图</param>
        /// <param name="filePath">保存路径</param>
        private async Task SaveThumbnailAsync(BitmapSource thumbnail, string filePath)
        {
            await Task.Run(() =>
            {
                try
                {
                    // 确保目录存在
                    var directory = Path.GetDirectoryName(filePath);
                    if (!string.IsNullOrEmpty(directory))
                        Directory.CreateDirectory(directory);

                    // 删除旧文件
                    if (File.Exists(filePath))
                        File.Delete(filePath);

                    // 保存为PNG
                    var encoder = new PngBitmapEncoder();
                    encoder.Frames.Add(BitmapFrame.Create(thumbnail));

                    using var stream = new FileStream(filePath, FileMode.Create);
                    encoder.Save(stream);
                }
                catch (Exception ex)
                {
                }
            });
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

                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.UriSource = new Uri(thumbnailPath, UriKind.Absolute);
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.EndInit();
                bitmap.Freeze();

                return bitmap;
            }
            catch (Exception ex)
            {
                return null;
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
            return Path.Combine(_workFolderService.ThumbnailsFolder, $"{nodeGraphName}.png");
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
