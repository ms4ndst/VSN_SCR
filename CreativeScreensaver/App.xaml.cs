using System;
using System.Linq;
using System.Windows;
// Avoid pulling Forms into this file to prevent Application ambiguity

namespace VismaSoftwareNordic
{
    public partial class App : System.Windows.Application
    {
        private const string AppTitle = "Visma Software Nordic Screensaver";

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            string[] args = Environment.GetCommandLineArgs();
            // Windows usually calls .scr with one of: /s, /c, /p <hwnd>
            string mode = args.Skip(1).FirstOrDefault()?.ToLowerInvariant() ?? "/s";

            if (mode.StartsWith("/c"))
            {
                ShowConfig();
                Shutdown();
                return;
            }

            if (mode.StartsWith("/p"))
            {
                // Preview mode; Control Panel passes parent HWND as next arg.
                IntPtr parent = IntPtr.Zero;
                if (args.Length >= 3 && long.TryParse(args[2], out long hwnd))
                {
                    parent = new IntPtr(hwnd);
                }
                var preview = new ScreensaverWindow(previewParent: parent);
                preview.Show();
                return;
            }

            // Default or /s -> run on all monitors
            foreach (var screen in System.Windows.Forms.Screen.AllScreens)
            {
                var win = new ScreensaverWindow(screen);
                win.Show();
            }
        }

        private static void ShowConfig()
        {
            var dlg = new SettingsWindow();
            dlg.Title = AppTitle + " Settings";
            dlg.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            dlg.ShowDialog();
        }
    }
}
