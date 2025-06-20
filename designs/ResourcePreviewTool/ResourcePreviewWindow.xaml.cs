using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Markup;
using System.Xml;

namespace ResourcePreviewTool
{
    public partial class ResourcePreviewWindow : Window
    {
        public class PreviewItem
        {
            public string Name { get; set; } = string.Empty;
            public Brush PreviewBrush { get; set; } = Brushes.Transparent;
        }

        public ResourcePreviewWindow()
        {
            InitializeComponent();
            LoadPreviews();
        }

        private void LoadPreviews()
        {
            var dir = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) ?? string.Empty;
            // Assume the window resides two directories deeper than designs folder when compiled. Adjust by searching nearest "designs" folder.
            var designsDir = FindDesignsDirectory(dir);
            if (designsDir == null) return;

            var items = new List<PreviewItem>();
            foreach (var file in Directory.GetFiles(designsDir, "*.xaml", SearchOption.TopDirectoryOnly))
            {
                try
                {
                    using var reader = XmlReader.Create(file);
                    var rd = (ResourceDictionary)XamlReader.Load(reader);
                    // 找到第一个Brush资源
                    var brush = rd.Values.OfType<Brush>().FirstOrDefault();
                    if (brush != null)
                    {
                        items.Add(new PreviewItem { Name = Path.GetFileNameWithoutExtension(file), PreviewBrush = brush });
                    }
                }
                catch
                {
                    // Ignore parse errors
                }
            }
            PreviewItemsControl.ItemsSource = items;
        }

        private string? FindDesignsDirectory(string startDir)
        {
            var dirInfo = new DirectoryInfo(startDir);
            while (dirInfo != null && dirInfo.Exists)
            {
                var designsPath = Path.Combine(dirInfo.FullName, "designs");
                if (Directory.Exists(designsPath)) return designsPath;
                dirInfo = dirInfo.Parent;
            }
            return null;
        }
    }
} 