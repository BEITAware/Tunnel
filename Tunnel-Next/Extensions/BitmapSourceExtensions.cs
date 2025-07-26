using System;
using System.IO;
using System.Windows.Media.Imaging;

namespace Tunnel_Next.Extensions
{
    /// <summary>
    /// BitmapSource扩展方法
    /// </summary>
    public static class BitmapSourceExtensions
    {
        /// <summary>
        /// 安全地从文件路径加载BitmapSource，确保文件句柄被正确释放
        /// </summary>
        /// <param name="filePath">图像文件路径</param>
        /// <param name="decodePixelWidth">解码像素宽度（可选）</param>
        /// <param name="decodePixelHeight">解码像素高度（可选）</param>
        /// <returns>BitmapSource或null</returns>
        public static BitmapSource? LoadFromFile(string filePath, int? decodePixelWidth = null, int? decodePixelHeight = null)
        {
            try
            {
                if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
                    return null;

                // 使用FileStream读取文件到内存，然后立即关闭文件句柄
                byte[] imageBytes;
                using (var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    imageBytes = new byte[fileStream.Length];
                    fileStream.Read(imageBytes, 0, imageBytes.Length);
                }

                // 从内存中的字节数组创建BitmapImage，确保MemoryStream被正确释放
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                using (var memoryStream = new MemoryStream(imageBytes))
                {
                    bitmap.StreamSource = memoryStream;
                    if (decodePixelWidth.HasValue)
                        bitmap.DecodePixelWidth = decodePixelWidth.Value;
                    if (decodePixelHeight.HasValue)
                        bitmap.DecodePixelHeight = decodePixelHeight.Value;
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.EndInit();
                }
                bitmap.Freeze();
                return bitmap;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[BitmapSourceExtensions] 加载图像失败 {filePath}: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 安全地从URI加载BitmapSource
        /// </summary>
        /// <param name="uri">图像URI</param>
        /// <param name="decodePixelWidth">解码像素宽度（可选）</param>
        /// <param name="decodePixelHeight">解码像素高度（可选）</param>
        /// <returns>BitmapSource或null</returns>
        public static BitmapSource? LoadFromUri(Uri uri, int? decodePixelWidth = null, int? decodePixelHeight = null)
        {
            try
            {
                if (uri == null)
                    return null;

                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.UriSource = uri;
                if (decodePixelWidth.HasValue)
                    bitmap.DecodePixelWidth = decodePixelWidth.Value;
                if (decodePixelHeight.HasValue)
                    bitmap.DecodePixelHeight = decodePixelHeight.Value;
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.EndInit();
                bitmap.Freeze();
                return bitmap;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[BitmapSourceExtensions] 加载图像失败 {uri}: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 智能加载BitmapSource，自动判断是文件路径还是URI
        /// </summary>
        /// <param name="source">图像源（文件路径或URI字符串）</param>
        /// <param name="decodePixelWidth">解码像素宽度（可选）</param>
        /// <param name="decodePixelHeight">解码像素高度（可选）</param>
        /// <returns>BitmapSource或null</returns>
        public static BitmapSource? LoadSmart(string source, int? decodePixelWidth = null, int? decodePixelHeight = null)
        {
            if (string.IsNullOrEmpty(source))
                return null;

            // 如果是文件路径，使用文件加载方式
            if (File.Exists(source))
            {
                return LoadFromFile(source, decodePixelWidth, decodePixelHeight);
            }

            // 尝试作为URI处理
            try
            {
                var uri = new Uri(source, UriKind.RelativeOrAbsolute);
                return LoadFromUri(uri, decodePixelWidth, decodePixelHeight);
            }
            catch
            {
                return null;
            }
        }
    }
}
