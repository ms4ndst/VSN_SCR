using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace VismaSoftwareNordic
{
    public partial class ScreensaverWindow : Window
    {
        private readonly bool _isPreview;
        private readonly IntPtr _previewParent;
        private System.Drawing.Rectangle _screenBounds;
        private System.Windows.Point _lastMousePos;
        private bool _mouseInitialized = false;
        private readonly ImageAnimator _animator;

        public ScreensaverWindow(Screen screen)
        {
            InitializeComponent();
            _isPreview = false;
            _previewParent = IntPtr.Zero;
            _screenBounds = screen.Bounds;
            _animator = new ImageAnimator(RootCanvas);
            ConfigureForScreen();
        }

        public ScreensaverWindow(IntPtr previewParent)
        {
            InitializeComponent();
            _isPreview = true;
            _previewParent = previewParent;
            _screenBounds = new System.Drawing.Rectangle(0, 0, 320, 200);
            _animator = new ImageAnimator(RootCanvas);
        }

        private void ConfigureForScreen()
        {
            GetMonitorScale(_screenBounds, out double scaleX, out double scaleY);
            Left = _screenBounds.Left / scaleX;
            Top = _screenBounds.Top / scaleY;
            Width = _screenBounds.Width / scaleX;
            Height = _screenBounds.Height / scaleY;
            WindowState = WindowState.Normal; // set bounds explicitly for multi-monitor correctness
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            if (_isPreview && _previewParent != IntPtr.Zero)
            {
                // Try to parent our window into preview handle
                var helper = new System.Windows.Interop.WindowInteropHelper(this);
                SetParent(helper.EnsureHandle(), _previewParent);
                // Resize to fit parent
                GetClientRect(_previewParent, out RECT rect);
                Width = Math.Max(1, rect.Right - rect.Left);
                Height = Math.Max(1, rect.Bottom - rect.Top);
            }

            StartShow();
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            try { _animator?.Stop(); } catch { }
        }

        private void StartShow()
        {
            var settings = Settings.Load();
            // Always use embedded images (locked build)
            var files = LoadEmbeddedImagePaths();
            if (files.Count == 0)
            {
                ShowNoImagesMessage();
                return;
            }

            _animator.Start(files, settings);
        }

        private List<string> LoadEmbeddedImagePaths()
        {
            var assembly = System.Reflection.Assembly.GetExecutingAssembly();
            return assembly.GetManifestResourceNames()
                .Where(r => new[] { ".jpg", ".jpeg", ".png", ".bmp", ".gif" }.Any(ext => r.EndsWith(ext, StringComparison.OrdinalIgnoreCase)))
                .ToList();
        }

        private void ShowNoImagesMessage()
        {
            var tb = new TextBlock { Text = "No images found.", Foreground = System.Windows.Media.Brushes.White, FontSize = 20, Opacity = 0.4 };
            Canvas.SetLeft(tb, 24);
            Canvas.SetTop(tb, 24);
            RootCanvas.Children.Add(tb);
        }

        private void Window_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            CloseAll();
        }

        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            CloseAll();
        }

        private void Window_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            var pos = e.GetPosition(this);
            if (!_mouseInitialized)
            {
                _lastMousePos = pos;
                _mouseInitialized = true;
                return;
            }
            if ((Math.Abs(pos.X - _lastMousePos.X) > 6) || (Math.Abs(pos.Y - _lastMousePos.Y) > 6))
            {
                CloseAll();
            }
        }

        private static void CloseAll()
        {
            try
            {
                var app = System.Windows.Application.Current;
                if (app != null)
                {
                    foreach (Window w in app.Windows.Cast<Window>().ToList())
                    {
                        try { w.Close(); } catch { }
                    }
                }
            }
            catch { }
            finally
            {
                // Force immediate process termination
                Environment.Exit(0);
            }
        }

        #region Win32
        [DllImport("user32.dll")] private static extern IntPtr SetParent(IntPtr hWndChild, IntPtr hWndNewParent);
        [DllImport("user32.dll")] private static extern bool GetClientRect(IntPtr hWnd, out RECT lpRect);
        [DllImport("user32.dll")] private static extern IntPtr MonitorFromPoint(POINT pt, uint dwFlags);
        [DllImport("Shcore.dll")] private static extern int GetDpiForMonitor(IntPtr hmonitor, int dpiType, out uint dpiX, out uint dpiY);
        [StructLayout(LayoutKind.Sequential)] private struct RECT { public int Left; public int Top; public int Right; public int Bottom; }
        [StructLayout(LayoutKind.Sequential)] private struct POINT { public int X; public int Y; }

        private const int MDT_EFFECTIVE_DPI = 0;
        private const uint MONITOR_DEFAULTTONEAREST = 2;

        private static void GetMonitorScale(System.Drawing.Rectangle bounds, out double scaleX, out double scaleY)
        {
            try
            {
                var center = new POINT { X = bounds.Left + bounds.Width / 2, Y = bounds.Top + bounds.Height / 2 };
                var hmon = MonitorFromPoint(center, MONITOR_DEFAULTTONEAREST);
                if (hmon != IntPtr.Zero && GetDpiForMonitor(hmon, MDT_EFFECTIVE_DPI, out uint dx, out uint dy) == 0)
                {
                    scaleX = Math.Max(0.5, dx / 96.0);
                    scaleY = Math.Max(0.5, dy / 96.0);
                    return;
                }
            }
            catch { }
            scaleX = 1.0; scaleY = 1.0;
        }
        #endregion
    }
}
