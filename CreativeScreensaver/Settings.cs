using System;
using System.IO;
using System.Text.Json;

namespace VismaSoftwareNordic
{
    public class Settings
    {
        public string ImagesFolder { get; set; } = string.Empty;
        public int SlideDurationSeconds { get; set; } = 8;
        public int TransitionSeconds { get; set; } = 2;
        public bool RandomizeOrder { get; set; } = true;
        public string AnimationStyle { get; set; } = "Random"; // Random, KenBurns, Pan, Rotate, Parallax, CrossZoom, Tiles
        public int AnimationIntensity { get; set; } = 50; // 0-100, controls movement range and speed
        public bool UseEmbeddedImages { get; set; } = true;
        public int DisplayScalePercent { get; set; } = 90; // 70-100, scales displayed image size
        public bool ShowClockBetweenSlides { get; set; } = true;
        public int ClockDurationSeconds { get; set; } = 3;
        public string ClockFontFamily { get; set; } = "FiraMono Nerd Font";
        public int ClockFontSize { get; set; } = 64;
        public string ClockFormat { get; set; } = "dddd dd MMM yyyy HH:mm";

        public static string GetConfigPath()
        {
            var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "VismaSoftwareNordic");
            Directory.CreateDirectory(dir);
            return Path.Combine(dir, "settings.json");
        }

        public static Settings Load()
        {
            try
            {
                var path = GetConfigPath();
                if (File.Exists(path))
                {
                    var json = File.ReadAllText(path);
                    var s = JsonSerializer.Deserialize<Settings>(json);
                    if (s != null)
                    {
                        // Locked build: force embedded images regardless of old settings
                        s.UseEmbeddedImages = true;
                        s.ImagesFolder = string.Empty;
                        return s;
                    }
                }
            }
            catch { }

            return new Settings
            {
                ImagesFolder = string.Empty,
                SlideDurationSeconds = 8,
                TransitionSeconds = 2,
                RandomizeOrder = true,
                AnimationStyle = "Random",
                AnimationIntensity = 50,
                UseEmbeddedImages = true,
                DisplayScalePercent = 90,
                ShowClockBetweenSlides = true,
                ClockDurationSeconds = 3,
                ClockFontFamily = "FiraMono Nerd Font",
                ClockFontSize = 64,
                ClockFormat = "dddd dd MMM yyyy HH:mm"
            };
        }

        public void Save()
        {
            try
            {
                var path = GetConfigPath();
                var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(path, json);
            }
            catch { }
        }

        private static string DefaultImagesFolder()
        {
            // Prefer an "images" folder next to the repo root, else Pictures
            try
            {
                var exeDir = AppContext.BaseDirectory;
                var candidate1 = Path.Combine(exeDir, "images");
                var candidate2 = Path.GetFullPath(Path.Combine(exeDir, "..", "images"));
                var candidate3 = Path.GetFullPath(Path.Combine(exeDir, "..", "..", "images"));
                if (Directory.Exists(candidate1)) return candidate1;
                if (Directory.Exists(candidate2)) return candidate2;
                if (Directory.Exists(candidate3)) return candidate3;
            }
            catch { }

            return Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
        }
    }
}
