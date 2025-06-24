using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using WinForms = System.Windows.Forms;
using MediaColor = System.Windows.Media.Color;
using System.Globalization;

namespace TNX_Scripts.Controls
{
    public class GradientEditor : Canvas
    {
        public static readonly DependencyProperty StopsProperty = DependencyProperty.Register(
            nameof(Stops), typeof(ObservableCollection<IGradientStopData>), typeof(GradientEditor),
            new FrameworkPropertyMetadata(null, OnStopsChanged));

        public ObservableCollection<IGradientStopData> Stops
        {
            get => (ObservableCollection<IGradientStopData>)GetValue(StopsProperty);
            set => SetValue(StopsProperty, value);
        }

        public static readonly DependencyProperty GradientTypeProperty = DependencyProperty.Register(
            nameof(GradientType), typeof(GradientType), typeof(GradientEditor),
            new FrameworkPropertyMetadata(GradientType.Linear, FrameworkPropertyMetadataOptions.AffectsRender));

        public GradientType GradientType
        {
            get => (GradientType)GetValue(GradientTypeProperty);
            set => SetValue(GradientTypeProperty, value);
        }

        private const double ThumbSize = 16;
        private const double BarHeight = 20;

        public GradientEditor()
        {
            this.Height = BarHeight + ThumbSize;
            this.MinWidth = 100; // Give it a minimum size
            this.Loaded += OnLoaded;
            this.MouseLeftButtonDown += OnMouseLeftButtonDown;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            BuildThumbs();
            InvalidateVisual();
        }

        private static void OnStopsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is GradientEditor ge)
            {
                // Unsubscribe from old collection
                if (e.OldValue is ObservableCollection<IGradientStopData> oldCol)
                {
                    oldCol.CollectionChanged -= ge.Stops_CollectionChanged;
                    foreach (var stop in oldCol)
                        if (stop is INotifyPropertyChanged npc) npc.PropertyChanged -= ge.Stop_PropertyChanged;
                }
                // Subscribe to new collection
                if (e.NewValue is ObservableCollection<IGradientStopData> newCol)
                {
                    newCol.CollectionChanged += ge.Stops_CollectionChanged;
                    foreach (var stop in newCol)
                        if (stop is INotifyPropertyChanged npc) npc.PropertyChanged += ge.Stop_PropertyChanged;
                }

                if (ge.IsLoaded)
                {
                    ge.BuildThumbs();
                    ge.InvalidateVisual();
                }
            }
        }

        private void Stops_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            // Detach handlers from removed items
            if (e.OldItems != null)
            {
                foreach (IGradientStopData s in e.OldItems)
                    if (s is INotifyPropertyChanged npc) npc.PropertyChanged -= Stop_PropertyChanged;
            }
            // Attach handlers to new items
            if (e.NewItems != null)
            {
                foreach (IGradientStopData s in e.NewItems)
                    if (s is INotifyPropertyChanged npc) npc.PropertyChanged += Stop_PropertyChanged;
            }

            // Rebuild the entire UI to reflect the collection change
            BuildThumbs();
            InvalidateVisual();
        }

        private void Stop_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(IGradientStopData.Offset))
            {
                if (sender is IGradientStopData sd) UpdateThumbPosition(sd);
            }
            // Any property change on a stop (Offset, H, S, L) requires a full redraw of the gradient bar
            InvalidateVisual();
        }

        private void BuildThumbs()
        {
            Children.Clear();
            if (Stops == null) return;
            foreach (var stop in Stops)
            {
                var thumb = new Thumb
                {
                    Width = ThumbSize,
                    Height = ThumbSize,
                    BorderBrush = Brushes.Black,
                    BorderThickness = new Thickness(1),
                    Tag = stop,
                    Cursor = System.Windows.Input.Cursors.Hand,
                };

                var thumbStyle = new Style(typeof(Thumb));
                thumbStyle.Setters.Add(new Setter(Control.TemplateProperty, CreateThumbTemplate()));
                thumb.Style = thumbStyle;

                thumb.DragDelta += Thumb_DragDelta;
                thumb.MouseDoubleClick += Thumb_MouseDoubleClick;
                thumb.MouseRightButtonUp += Thumb_MouseRightButtonUp;
                Children.Add(thumb);
                UpdateThumbPosition(stop);
            }
        }

        private void UpdateThumbPosition(IGradientStopData stop)
        {
            var thumb = Children.OfType<Thumb>().FirstOrDefault(t => t.Tag == stop);
            if (thumb != null)
            {
                double x = stop.Offset * Math.Max(1, ActualWidth) - ThumbSize / 2;
                SetLeft(thumb, x);
                SetTop(thumb, BarHeight);
                thumb.Background = new SolidColorBrush(HslToColor(stop.H, stop.S, stop.L));
            }
        }

        private void Thumb_DragDelta(object sender, DragDeltaEventArgs e)
        {
            if (sender is Thumb thumb && thumb.Tag is IGradientStopData stop)
            {
                double newOffset = stop.Offset + e.HorizontalChange / Math.Max(1, ActualWidth);
                stop.Offset = Math.Max(0, Math.Min(1, newOffset));
            }
        }

        private void Thumb_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (sender is Thumb thumb && thumb.Tag is IGradientStopData stop)
            {
                var dlg = new WinForms.ColorDialog
                {
                    AllowFullOpen = true,
                    FullOpen = true,
                    Color = System.Drawing.ColorTranslator.FromHtml(ColorToHex(HslToColor(stop.H, stop.S, stop.L)))
                };
                if (dlg.ShowDialog() == WinForms.DialogResult.OK)
                {
                    var c = dlg.Color;
                    var sc = MediaColor.FromRgb(c.R, c.G, c.B);
                    RgbToHsl(sc, out double h, out double s, out double l);
                    stop.H = h;
                    stop.S = s;
                    stop.L = l;
                    InvalidateVisual();
                }
            }
        }
        
        private void Thumb_MouseRightButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (sender is Thumb thumb && thumb.Tag is IGradientStopData stop)
            {
                if (Stops.Count > 1) // Do not allow deleting the last stop
                {
                    Stops.Remove(stop);
                }
            }
        }

        private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.Source == this) // Only when clicking the canvas itself
            {
                var pos = e.GetPosition(this);
                if (pos.Y >= 0 && pos.Y <= BarHeight)
                {
                    double offset = Math.Max(0, Math.Min(1, pos.X / Math.Max(1, ActualWidth)));
                    // Interpolate color for the new stop
                    var sortedStops = Stops.OrderBy(s => s.Offset).ToList();
                    var color = HslToColor(0,0,1); // default to white
                    if (sortedStops.Count > 0)
                    {
                        var before = sortedStops.LastOrDefault(s => s.Offset <= offset) ?? sortedStops.First();
                        var after = sortedStops.FirstOrDefault(s => s.Offset >= offset) ?? sortedStops.Last();
                        if (before == after)
                        {
                            color = HslToColor(before.H, before.S, before.L);
                        }
                        else
                        {
                           double range = after.Offset - before.Offset;
                           double factor = range == 0 ? 0 : (offset - before.Offset) / range;
                           color = Interpolate(HslToColor(before.H, before.S, before.L), HslToColor(after.H, after.S, after.L), factor);
                        }
                    }
                    RgbToHsl(color, out double h, out double s, out double l);
                    Stops.Add(new GradientStopDataImpl { Offset = offset, H = h, S = s, L = l });
                }
            }
        }

        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);
            if (Stops == null || Stops.Count == 0 || this.ActualWidth < 1) 
            {
                dc.DrawRectangle(Brushes.Transparent, null, new Rect(0, 0, ActualWidth, BarHeight));
                return;
            }
            
            var gradientStops = new GradientStopCollection(Stops.OrderBy(s => s.Offset).Select(s => new GradientStop(HslToColor(s.H, s.S, s.L), s.Offset)));
            
            // Ensure at least two stops for a valid gradient
            if (gradientStops.Count == 1)
            {
                gradientStops.Add(new GradientStop(gradientStops[0].Color, 1.0));
            }

            GradientBrush brush;
            if (GradientType == GradientType.Linear)
            {
                brush = new LinearGradientBrush(gradientStops, new Point(0, 0.5), new Point(1, 0.5));
            }
            else
            {
                brush = new RadialGradientBrush(gradientStops)
                {
                    RadiusX = 0.5, RadiusY = 0.5, Center = new Point(0.5, 0.5), GradientOrigin = new Point(0.5, 0.5)
                };
            }
            dc.DrawRectangle(brush, null, new Rect(0, 0, ActualWidth, BarHeight));
        }
        
        private ControlTemplate CreateThumbTemplate()
        {
            var template = new ControlTemplate(typeof(Thumb));
            var border = new FrameworkElementFactory(typeof(Border));
            border.SetBinding(Border.BackgroundProperty, new System.Windows.Data.Binding("Background") { RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent) });
            border.SetValue(Border.CornerRadiusProperty, new CornerRadius(2));
            border.SetValue(Border.BorderBrushProperty, Brushes.Gray);
            border.SetValue(Border.BorderThicknessProperty, new Thickness(1));

            var path = new FrameworkElementFactory(typeof(System.Windows.Shapes.Path));
            path.SetValue(System.Windows.Shapes.Path.DataProperty, Geometry.Parse("M 0,0 L 8,8 L 16,0 Z"));
            path.SetValue(System.Windows.Shapes.Path.FillProperty, Brushes.White);
            path.SetValue(System.Windows.Shapes.Path.StrokeProperty, Brushes.Black);
            path.SetValue(System.Windows.Shapes.Path.StrokeThicknessProperty, 1.0);
            
            var grid = new FrameworkElementFactory(typeof(Grid));
            grid.AppendChild(border);
            grid.AppendChild(path);

            template.VisualTree = grid;
            return template;
        }

        // Color helper methods
        private MediaColor HslToColor(double h, double s, double l)
        {
            h /= 360.0;
            double r = l, g = l, b = l;
            if (s != 0)
            {
                double q = l < 0.5 ? l * (1 + s) : (l + s - l * s);
                double p = 2 * l - q;
                r = HueToRGB(p, q, h + 1.0 / 3.0);
                g = HueToRGB(p, q, h);
                b = HueToRGB(p, q, h - 1.0 / 3.0);
            }
            return MediaColor.FromScRgb(1, (float)r, (float)g, (float)b);
        }

        private double HueToRGB(double p, double q, double t)
        {
            if (t < 0) t += 1;
            if (t > 1) t -= 1;
            if (t < 1.0 / 6.0) return p + (q - p) * 6 * t;
            if (t < 0.5) return q;
            if (t < 2.0 / 3.0) return p + (q - p) * (2.0 / 3.0 - t) * 6;
            return p;
        }

        private static string ColorToHex(MediaColor c) => $"#{c.R:X2}{c.G:X2}{c.B:X2}";
        private static void RgbToHsl(MediaColor color, out double h, out double s, out double l)
        {
            float r = color.ScR, g = color.ScG, b = color.ScB;
            float max = Math.Max(r, Math.Max(g, b)), min = Math.Min(r, Math.Min(g, b));
            h = 0; s = 0; l = (max + min) / 2.0;
            if (max != min)
            {
                float d = max - min;
                s = l > 0.5 ? d / (2 - max - min) : d / (max + min);
                if (max == r) h = (g - b) / d + (g < b ? 6 : 0);
                else if (max == g) h = (b - r) / d + 2;
                else h = (r - g) / d + 4;
                h *= 60;
            }
        }
        private static MediaColor Interpolate(MediaColor a, MediaColor b, double factor)
        {
            return MediaColor.FromScRgb(
                (float)(a.ScA + (b.ScA - a.ScA) * factor),
                (float)(a.ScR + (b.ScR - a.ScR) * factor),
                (float)(a.ScG + (b.ScG - a.ScG) * factor),
                (float)(a.ScB + (b.ScB - a.ScB) * factor)
            );
        }

        private class GradientStopDataImpl : IGradientStopData
        {
            private double _offset, _h, _s, _l;
            public double Offset { get => _offset; set { _offset = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Offset))); } }
            public double H { get => _h; set { _h = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(H))); } }
            public double S { get => _s; set { _s = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(S))); } }
            public double L { get => _l; set { _l = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(L))); } }
            public event PropertyChangedEventHandler PropertyChanged;
        }
    }
}