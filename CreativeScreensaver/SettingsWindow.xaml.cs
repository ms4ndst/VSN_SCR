using System;
using System.Windows;

namespace VismaSoftwareNordic
{
    public partial class SettingsWindow : Window
    {
        private Settings _settings = Settings.Load();

        public SettingsWindow()
        {
            InitializeComponent();
            LoadToUi();
        }

        private void LoadToUi()
        {
            SlideSecondsText.Text = _settings.SlideDurationSeconds.ToString();
            TransitionSecondsText.Text = _settings.TransitionSeconds.ToString();
            RandomizeCheck.IsChecked = _settings.RandomizeOrder;
            IntensitySlider.Value = _settings.AnimationIntensity;
            IntensityLabel.Text = _settings.AnimationIntensity.ToString();
            DisplayScaleSlider.Value = Math.Max(70, Math.Min(100, _settings.DisplayScalePercent));
            DisplayScaleLabel.Text = _settings.DisplayScalePercent.ToString();
            ShowClockCheck.IsChecked = _settings.ShowClockBetweenSlides;
            ClockSecondsText.Text = _settings.ClockDurationSeconds.ToString();
            ClockFontText.Text = _settings.ClockFontFamily;
            ClockFontSizeText.Text = _settings.ClockFontSize.ToString();
            AnimationCombo.SelectedItem = null;
            foreach (var item in AnimationCombo.Items)
            {
                if (item is System.Windows.Controls.ComboBoxItem cbi && (string)cbi.Content == _settings.AnimationStyle)
                {
                    AnimationCombo.SelectedItem = cbi; break;
                }
            }
            if (AnimationCombo.SelectedItem == null) AnimationCombo.SelectedIndex = 0;
        }

        private void IntensitySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (IntensityLabel != null)
            {
                IntensityLabel.Text = ((int)IntensitySlider.Value).ToString();
            }
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            if (!int.TryParse(SlideSecondsText.Text, out int slide) || slide < 2) slide = 8;
            if (!int.TryParse(TransitionSecondsText.Text, out int trans) || trans < 1) trans = 2;
            var anim = (AnimationCombo.SelectedItem as System.Windows.Controls.ComboBoxItem)?.Content?.ToString() ?? "Random";

            _settings.SlideDurationSeconds = slide;
            _settings.TransitionSeconds = trans;
            _settings.RandomizeOrder = RandomizeCheck.IsChecked == true;
            _settings.AnimationStyle = anim;
            _settings.AnimationIntensity = (int)IntensitySlider.Value;
            _settings.DisplayScalePercent = (int)DisplayScaleSlider.Value;
            _settings.ShowClockBetweenSlides = ShowClockCheck.IsChecked == true;
            if (!int.TryParse(ClockSecondsText.Text, out int clk) || clk < 1) clk = 3;
            _settings.ClockDurationSeconds = clk;
            if (!int.TryParse(ClockFontSizeText.Text, out int fz) || fz < 8) fz = 64;
            _settings.ClockFontSize = fz;
            _settings.ClockFontFamily = string.IsNullOrWhiteSpace(ClockFontText.Text) ? _settings.ClockFontFamily : ClockFontText.Text;
            _settings.Save();
            DialogResult = true;
            Close();
        }

        private void DisplayScaleSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (DisplayScaleLabel != null)
            {
                DisplayScaleLabel.Text = ((int)DisplayScaleSlider.Value).ToString();
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
