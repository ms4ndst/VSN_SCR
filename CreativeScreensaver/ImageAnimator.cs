using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace VismaSoftwareNordic
{
    public class ImageAnimator
    {
        private readonly Canvas _root;
        private readonly Canvas _imageLayer;
        private readonly Canvas _particleLayer;
        private readonly Random _rng = new Random();
        private readonly DispatcherTimer _timer;
        private List<string> _paths = new();
        private int _index;
        private Settings _settings = new();
        private System.Windows.Size _viewport;
        private bool _useEmbedded;
        private double _intensityFactor = 1.0; // 0.0 to 2.0 based on intensity setting
        private bool _showingClock = false;

        public ImageAnimator(System.Windows.Controls.Canvas host)
        {
            _root = host;
            _imageLayer = new Canvas { IsHitTestVisible = false };
            _particleLayer = new Canvas { IsHitTestVisible = false };
            _root.Children.Add(_imageLayer);
            _root.Children.Add(_particleLayer);
            _timer = new DispatcherTimer();
            _timer.Tick += (s, e) => Next();
            _root.SizeChanged += (s, e) => { _viewport = new System.Windows.Size(_root.ActualWidth, _root.ActualHeight); };
        }

        public void Stop()
        {
            try
            {
                if (_timer != null)
                {
                    _timer.Stop();
                    _timer.IsEnabled = false;
                }
                _imageLayer?.Children.Clear();
                _particleLayer?.Children.Clear();
                _root?.Children.Clear();
            }
            catch { }
        }

        public void Start(List<string> files, Settings settings)
        {
            _settings = settings;
            _useEmbedded = settings.UseEmbeddedImages;
            _intensityFactor = settings.AnimationIntensity / 50.0; // 0-100 -> 0.0-2.0
            _paths = settings.RandomizeOrder ? files.OrderBy(_ => _rng.Next()).ToList() : new List<string>(files);
            _index = 0;
            _viewport = new System.Windows.Size(_root.ActualWidth, _root.ActualHeight);
            _showingClock = false;
            _timer.Interval = TimeSpan.FromSeconds(Math.Max(1, _settings.SlideDurationSeconds));
            _imageLayer.Children.Clear();
            _particleLayer.Children.Clear();
            StartParticles();
            Next();
            _timer.Start();
        }

        private void Next()
        {
            if (_paths.Count == 0 || _viewport.Width < 1 || _viewport.Height < 1) return;
            if (_settings.ShowClockBetweenSlides)
            {
                if (_showingClock)
                {
                    // Now show an image
                    _showingClock = false;
                    ShowNextImage();
                    _timer.Interval = TimeSpan.FromSeconds(Math.Max(1, _settings.SlideDurationSeconds));
                }
                else
                {
                    // Show clock
                    _showingClock = true;
                    ShowClock();
                    // Match clock visibility to image slide duration
                    _timer.Interval = TimeSpan.FromSeconds(Math.Max(1, _settings.SlideDurationSeconds));
                }
            }
            else
            {
                // Always show images
                ShowNextImage();
                _timer.Interval = TimeSpan.FromSeconds(Math.Max(1, _settings.SlideDurationSeconds));
            }
        }

        private void ShowNextImage()
        {
            if (_paths.Count == 0) return;
            if (_index >= _paths.Count) _index = 0;
            var path = _paths[_index++];

            var img = CreateImage(path, _viewport);
            if (img == null) return;

            _imageLayer.Children.Add(img);
            System.Windows.Controls.Panel.SetZIndex(img, 1);

            var style = (_settings.AnimationStyle ?? "Random").ToLowerInvariant();
            if (style == "random")
            {
                var choices = new[] { "kenburns", "pan", "rotate", "parallax", "crosszoom", "tiles" };
                style = choices[_rng.Next(choices.Length)];
            }

            switch (style)
            {
                case "kenburns": ApplyKenBurns(img); break;
                case "pan": ApplyPan(img); break;
                case "rotate": ApplyRotate(img); break;
                case "parallax": ApplyParallax(img); break;
                case "crosszoom": ApplyCrossZoom(img); break;
                case "tiles": ApplyTiles(img); break;
                default: ApplyKenBurns(img); break;
            }

            FadeOutOlder();
        }

        private void ShowClock()
        {
            var container = new Grid
            {
                Width = _viewport.Width,
                Height = _viewport.Height,
                IsHitTestVisible = false
            };
            var tb = new TextBlock
            {
                Text = DateTime.Now.ToString(_settings.ClockFormat),
                Foreground = System.Windows.Media.Brushes.White,
                Opacity = 0,
                FontSize = Math.Max(12, _settings.ClockFontSize),
                TextWrapping = TextWrapping.NoWrap,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                VerticalAlignment = System.Windows.VerticalAlignment.Center
            };
            try { tb.FontFamily = new System.Windows.Media.FontFamily(_settings.ClockFontFamily); } catch { }

            container.Children.Add(tb);
            System.Windows.Controls.Canvas.SetLeft(container, 0);
            System.Windows.Controls.Canvas.SetTop(container, 0);
            _imageLayer.Children.Add(container);
            System.Windows.Controls.Panel.SetZIndex(container, 3);

            // Single storyboard with keyframes: fade-in, hold, fade-out
            double total = Math.Max(1, _settings.SlideDurationSeconds);
            double dIn = Math.Max(0.2, _settings.TransitionSeconds * 0.5);
            double dOut = Math.Max(0.2, _settings.TransitionSeconds * 0.5);
            double plateau = Math.Max(0, total - dIn - dOut);

            var opacityKeys = new DoubleAnimationUsingKeyFrames();
            opacityKeys.KeyFrames.Add(new DiscreteDoubleKeyFrame(0.0, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(0))));
            opacityKeys.KeyFrames.Add(new EasingDoubleKeyFrame(1.0, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(dIn))) { EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut } });
            opacityKeys.KeyFrames.Add(new DiscreteDoubleKeyFrame(1.0, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(dIn + plateau))));
            opacityKeys.KeyFrames.Add(new EasingDoubleKeyFrame(0.0, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(dIn + plateau + dOut))) { EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn } });

            var sb = new Storyboard { Duration = TimeSpan.FromSeconds(total) };
            Storyboard.SetTarget(opacityKeys, tb);
            Storyboard.SetTargetProperty(opacityKeys, new PropertyPath(UIElement.OpacityProperty));
            sb.Children.Add(opacityKeys);
            sb.Completed += (s, e) => _imageLayer.Children.Remove(container);
            sb.Begin();

            FadeOutOlder();
        }

        private void FadeOutOlder()
        {
            for (int i = 0; i < _imageLayer.Children.Count - 1; i++)
            {
                if (_imageLayer.Children[i] is UIElement el)
                {
                    var fade = new DoubleAnimation(1, 0, TimeSpan.FromSeconds(Math.Max(1, _settings.TransitionSeconds)));
                    fade.Completed += (s, e) => _imageLayer.Children.Remove(el);
                    el.BeginAnimation(UIElement.OpacityProperty, fade);
                }
            }
        }

        private System.Windows.Controls.Image? CreateImage(string path, System.Windows.Size viewport)
        {
            try
            {
                BitmapImage bi;
                int srcW = 0, srcH = 0;
                if (_useEmbedded)
                {
                    // Load from embedded resource and read dimensions
                    var assembly = System.Reflection.Assembly.GetExecutingAssembly();
                    using var raw = assembly.GetManifestResourceStream(path);
                    if (raw == null) return null;
                    using var ms = new MemoryStream();
                    raw.CopyTo(ms);
                    ms.Position = 0;
                    var decoder = BitmapDecoder.Create(ms, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
                    var frame = decoder.Frames[0];
                    srcW = frame.PixelWidth; srcH = frame.PixelHeight;
                    ms.Position = 0;

                    // Compute decode constraint while preserving aspect ratio
                    var scale = Math.Min(1.0, Math.Min(1920.0 / Math.Max(1, srcW), 1280.0 / Math.Max(1, srcH)));
                    int decW = (int)Math.Floor(srcW * scale);
                    int decH = (int)Math.Floor(srcH * scale);

                    bi = new BitmapImage();
                    bi.BeginInit();
                    bi.CacheOption = BitmapCacheOption.OnLoad;
                    bi.StreamSource = ms; // use copied stream
                    if (decW > 0 && (1920.0 / Math.Max(1, srcW)) <= (1280.0 / Math.Max(1, srcH)))
                        bi.DecodePixelWidth = decW; // width-limited
                    else if (decH > 0)
                        bi.DecodePixelHeight = decH; // height-limited
                    bi.EndInit();
                    bi.Freeze();
                }
                else
                {
                    // Load from file and read dimensions
                    var full = Path.GetFullPath(path);
                    using (var fsInfo = File.OpenRead(full))
                    {
                        var decoder = BitmapDecoder.Create(fsInfo, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
                        var frame = decoder.Frames[0];
                        srcW = frame.PixelWidth; srcH = frame.PixelHeight;
                    }

                    var scale = Math.Min(1.0, Math.Min(1920.0 / Math.Max(1, srcW), 1280.0 / Math.Max(1, srcH)));
                    int decW = (int)Math.Floor(srcW * scale);
                    int decH = (int)Math.Floor(srcH * scale);

                    bi = new BitmapImage();
                    bi.BeginInit();
                    bi.CacheOption = BitmapCacheOption.OnLoad;
                    bi.UriSource = new Uri(full);
                    if (decW > 0 && (1920.0 / Math.Max(1, srcW)) <= (1280.0 / Math.Max(1, srcH)))
                        bi.DecodePixelWidth = decW;
                    else if (decH > 0)
                        bi.DecodePixelHeight = decH;
                    bi.EndInit();
                    bi.Freeze();
                }

                // Display the image slightly smaller than the full viewport to avoid an over-expanded look
                double displayScale = Math.Max(0.7, Math.Min(1.0, _settings.DisplayScalePercent / 100.0));
                double targetWidth = Math.Max(1, viewport.Width * displayScale);
                double targetHeight = Math.Max(1, viewport.Height * displayScale);

                var img = new System.Windows.Controls.Image
                {
                    Source = bi,
                    Stretch = Stretch.Uniform,
                    RenderTransformOrigin = new System.Windows.Point(0.5, 0.5),
                    Width = targetWidth,
                    Height = targetHeight
                };
                var rtg = new TransformGroup();
                rtg.Children.Add(new ScaleTransform(1, 1));
                rtg.Children.Add(new RotateTransform(0));
                rtg.Children.Add(new TranslateTransform(0, 0));
                img.RenderTransform = rtg;
                // Center the image within the viewport
                Canvas.SetLeft(img, (viewport.Width - targetWidth) / 2);
                Canvas.SetTop(img, (viewport.Height - targetHeight) / 2);
                img.Opacity = 0;
                img.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromSeconds(Math.Max(0.5, _settings.TransitionSeconds))));
                return img;
            }
            catch { return null; }
        }

        private void ApplyKenBurns(System.Windows.Controls.Image img)
        {
            var duration = TimeSpan.FromSeconds(Math.Max(2, _settings.SlideDurationSeconds));
            var rtg = (TransformGroup)img.RenderTransform;
            var scale = (ScaleTransform)rtg.Children[0];
            var rotate = (RotateTransform)rtg.Children[1];
            var translate = (TranslateTransform)rtg.Children[2];

            double startScale = 1.05 + _rng.NextDouble() * 0.2 * _intensityFactor;
            double endScale = startScale + (0.2 + _rng.NextDouble() * 0.2) * _intensityFactor;
            double startAngle = (-2 + _rng.NextDouble() * 4) * _intensityFactor;
            double endAngle = startAngle + (-1 + _rng.NextDouble() * 2) * _intensityFactor;
            double startX = (-50 + _rng.NextDouble() * 100) * _intensityFactor;
            double endX = (-50 + _rng.NextDouble() * 100) * _intensityFactor;
            double startY = (-30 + _rng.NextDouble() * 60) * _intensityFactor;
            double endY = (-30 + _rng.NextDouble() * 60) * _intensityFactor;

            var sb = new Storyboard { Duration = duration };
            AddDoubleAnim(sb, scale, ScaleTransform.ScaleXProperty, startScale, endScale, duration);
            AddDoubleAnim(sb, scale, ScaleTransform.ScaleYProperty, startScale, endScale, duration);
            AddDoubleAnim(sb, rotate, RotateTransform.AngleProperty, startAngle, endAngle, duration);
            AddDoubleAnim(sb, translate, TranslateTransform.XProperty, startX, endX, duration);
            AddDoubleAnim(sb, translate, TranslateTransform.YProperty, startY, endY, duration);
            sb.Begin();
        }

        private void ApplyPan(System.Windows.Controls.Image img)
        {
            var duration = TimeSpan.FromSeconds(Math.Max(2, _settings.SlideDurationSeconds));
            var rtg = (TransformGroup)img.RenderTransform;
            var translate = (TranslateTransform)rtg.Children[2];
            double startX = (-100 + _rng.NextDouble() * 200) * _intensityFactor;
            double endX = (-100 + _rng.NextDouble() * 200) * _intensityFactor;
            double startY = (-100 + _rng.NextDouble() * 200) * _intensityFactor;
            double endY = (-100 + _rng.NextDouble() * 200) * _intensityFactor;
            var sb = new Storyboard { Duration = duration };
            AddDoubleAnim(sb, translate, TranslateTransform.XProperty, startX, endX, duration);
            AddDoubleAnim(sb, translate, TranslateTransform.YProperty, startY, endY, duration);
            sb.Begin();
        }

        private void ApplyRotate(System.Windows.Controls.Image img)
        {
            var duration = TimeSpan.FromSeconds(Math.Max(2, _settings.SlideDurationSeconds));
            var rtg = (TransformGroup)img.RenderTransform;
            var rotate = (RotateTransform)rtg.Children[1];
            var scale = (ScaleTransform)rtg.Children[0];
            var sb = new Storyboard { Duration = duration };
            double angle = (-10 + _rng.NextDouble() * 20) * _intensityFactor;
            AddDoubleAnim(sb, rotate, RotateTransform.AngleProperty, -angle, angle, duration);
            AddDoubleAnim(sb, scale, ScaleTransform.ScaleXProperty, 1.1, 1.1, duration);
            AddDoubleAnim(sb, scale, ScaleTransform.ScaleYProperty, 1.1, 1.1, duration);
            sb.Begin();
        }

        private void ApplyParallax(System.Windows.Controls.Image img)
        {
            var bg = new System.Windows.Controls.Image
            {
                Source = img.Source,
                Stretch = Stretch.UniformToFill,
                Opacity = 0.35,
                RenderTransformOrigin = new System.Windows.Point(0.5, 0.5),
                Width = _viewport.Width * Math.Max(0.7, Math.Min(1.0, _settings.DisplayScalePercent / 100.0)),
                Height = _viewport.Height * Math.Max(0.7, Math.Min(1.0, _settings.DisplayScalePercent / 100.0))
            };
            var bgRtg = new TransformGroup();
            bgRtg.Children.Add(new ScaleTransform(1.2, 1.2));
            bgRtg.Children.Add(new RotateTransform(0));
            bgRtg.Children.Add(new TranslateTransform(0, 0));
            bg.RenderTransform = bgRtg;
            _imageLayer.Children.Insert(Math.Max(0, _imageLayer.Children.Count - 1), bg);
            System.Windows.Controls.Canvas.SetLeft(bg, (_viewport.Width - bg.Width) / 2);
            System.Windows.Controls.Canvas.SetTop(bg, (_viewport.Height - bg.Height) / 2);

            var duration = TimeSpan.FromSeconds(Math.Max(2, _settings.SlideDurationSeconds));
            var fgTrans = ((TransformGroup)img.RenderTransform).Children[2] as TranslateTransform;
            var bgTrans = (bgRtg.Children[2] as TranslateTransform)!;

            double fx1 = (-60 + _rng.NextDouble() * 120) * _intensityFactor;
            double fy1 = (-40 + _rng.NextDouble() * 80) * _intensityFactor;
            double fx2 = (-60 + _rng.NextDouble() * 120) * _intensityFactor;
            double fy2 = (-40 + _rng.NextDouble() * 80) * _intensityFactor;

            var sb = new Storyboard { Duration = duration };
            AddDoubleAnim(sb, fgTrans!, TranslateTransform.XProperty, fx1, fx2, duration);
            AddDoubleAnim(sb, fgTrans!, TranslateTransform.YProperty, fy1, fy2, duration);
            AddDoubleAnim(sb, bgTrans, TranslateTransform.XProperty, fx1 * 0.4, fx2 * 0.4, duration);
            AddDoubleAnim(sb, bgTrans, TranslateTransform.YProperty, fy1 * 0.4, fy2 * 0.4, duration);
            sb.Begin();
        }

        private void ApplyCrossZoom(System.Windows.Controls.Image img)
        {
            // Cross-zoom: simultaneous zoom in/out with rotation and fade
            var duration = TimeSpan.FromSeconds(Math.Max(2, _settings.SlideDurationSeconds));
            var rtg = (TransformGroup)img.RenderTransform;
            var scale = (ScaleTransform)rtg.Children[0];
            var rotate = (RotateTransform)rtg.Children[1];

            bool zoomIn = _rng.Next(2) == 0;
            double startScale = zoomIn ? 1.0 : (1.3 + 0.2 * _intensityFactor);
            double endScale = zoomIn ? (1.3 + 0.2 * _intensityFactor) : 1.0;
            double startAngle = (-5 + _rng.NextDouble() * 10) * _intensityFactor;
            double endAngle = (-5 + _rng.NextDouble() * 10) * _intensityFactor;

            var sb = new Storyboard { Duration = duration };
            AddDoubleAnim(sb, scale, ScaleTransform.ScaleXProperty, startScale, endScale, duration);
            AddDoubleAnim(sb, scale, ScaleTransform.ScaleYProperty, startScale, endScale, duration);
            AddDoubleAnim(sb, rotate, RotateTransform.AngleProperty, startAngle, endAngle, duration);
            sb.Begin();
        }

        private void ApplyTiles(System.Windows.Controls.Image img)
        {
            // Tiles: create a grid of clipped regions that reveal with stagger
            img.Opacity = 1; // Override fade-in since we'll reveal via clips

            int rows = 3 + (int)(_intensityFactor * 2); // 3-7 rows based on intensity
            int cols = 4 + (int)(_intensityFactor * 3); // 4-10 cols
            double tileW = _viewport.Width / cols;
            double tileH = _viewport.Height / rows;

            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < cols; c++)
                {
                    var tileImg = new System.Windows.Controls.Image
                    {
                        Source = img.Source,
                        Stretch = Stretch.UniformToFill,
                        Width = _viewport.Width,
                        Height = _viewport.Height,
                        RenderTransformOrigin = new System.Windows.Point(0.5, 0.5)
                    };

                    var clipRect = new RectangleGeometry
                    {
                        Rect = new System.Windows.Rect(c * tileW, r * tileH, tileW, tileH)
                    };
                    tileImg.Clip = clipRect;

                    var rtg = new TransformGroup();
                    rtg.Children.Add(new ScaleTransform(1, 1));
                    rtg.Children.Add(new RotateTransform(0));
                    rtg.Children.Add(new TranslateTransform(0, 0));
                    tileImg.RenderTransform = rtg;

                    System.Windows.Controls.Canvas.SetLeft(tileImg, 0);
                    System.Windows.Controls.Canvas.SetTop(tileImg, 0);
                    tileImg.Opacity = 0;

                    _imageLayer.Children.Add(tileImg);
                    System.Windows.Controls.Panel.SetZIndex(tileImg, 2);

                    // Stagger reveal
                    double delay = (r * cols + c) * 0.03 * (2.0 - _intensityFactor * 0.5);
                    var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromSeconds(0.3))
                    {
                        BeginTime = TimeSpan.FromSeconds(delay)
                    };
                    tileImg.BeginAnimation(UIElement.OpacityProperty, fadeIn);

                    // Subtle zoom per tile
                    var scale = (ScaleTransform)rtg.Children[0];
                    var scaleAnim = new DoubleAnimation(0.95, 1.0, TimeSpan.FromSeconds(Math.Max(2, _settings.SlideDurationSeconds) * 0.8))
                    {
                        BeginTime = TimeSpan.FromSeconds(delay)
                    };
                    scale.BeginAnimation(ScaleTransform.ScaleXProperty, scaleAnim);
                    scale.BeginAnimation(ScaleTransform.ScaleYProperty, scaleAnim);
                }
            }

            // Hide the main image since we're using tiles
            img.Opacity = 0;
        }

        private static void AddDoubleAnim(Storyboard sb, DependencyObject target, DependencyProperty dp, double from, double to, TimeSpan duration)
        {
            var da = new DoubleAnimation(from, to, duration) { EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseInOut } };
            Storyboard.SetTarget(da, target);
            Storyboard.SetTargetProperty(da, new PropertyPath(dp));
            sb.Children.Add(da);
        }

        private void StartParticles()
        {
            int count = Math.Max(8, (int)(_viewport.Width * _viewport.Height / 300000));
            for (int i = 0; i < count; i++)
            {
                var ellipse = new System.Windows.Shapes.Ellipse
                {
                    Fill = new SolidColorBrush(System.Windows.Media.Color.FromArgb((byte)_rng.Next(20, 60), 255, 255, 255)),
                    Width = _rng.Next(2, 5),
                    Height = _rng.Next(2, 5)
                };
                _particleLayer.Children.Add(ellipse);
                ResetParticle(ellipse, true);
                AnimateParticle(ellipse);
            }
        }

        private void ResetParticle(UIElement el, bool randomY)
        {
            double x = _viewport.Width + _rng.NextDouble() * _viewport.Width * 0.5;
            double y = randomY ? _rng.NextDouble() * _viewport.Height : Canvas.GetTop(el);
            Canvas.SetLeft(el, x);
            Canvas.SetTop(el, y);
        }

        private void AnimateParticle(UIElement el)
        {
            double startX = Canvas.GetLeft(el);
            double endX = -50;
            double y = Canvas.GetTop(el);
            double drift = -20 + _rng.NextDouble() * 40;
            double seconds = 10 + _rng.NextDouble() * 20;

            var animX = new DoubleAnimation(startX, endX, TimeSpan.FromSeconds(seconds));
            var animY = new DoubleAnimation(y, y + drift, TimeSpan.FromSeconds(seconds));
            animX.Completed += (s, e) => { ResetParticle(el, true); AnimateParticle(el); };
            el.BeginAnimation(Canvas.LeftProperty, animX);
            el.BeginAnimation(Canvas.TopProperty, animY);
        }
    }
}
