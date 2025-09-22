using System;
using System.Windows;
using System.Windows.Media;

namespace WorkPartner
{
    public partial class ColorPickerWindow : Window
    {
        public Color SelectedColor { get; set; }

        public ColorPickerWindow()
        {
            InitializeComponent();
            this.Loaded += (s, e) => UpdatePreview();
        }

        // [FIX] Added constructor that takes an initial color to fix CS1729 in SettingsPage.xaml.cs
        public ColorPickerWindow(Color initialColor)
        {
            InitializeComponent();
            SelectedColor = initialColor;
            this.Loaded += (s, e) =>
            {
                (double hue, double saturation, double lightness) = RgbToHsl(initialColor);
                HueSlider.Value = hue;
                SaturationSlider.Value = saturation;
                LightnessSlider.Value = lightness;
                UpdatePreview();
            };
        }

        private void Slider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (IsLoaded)
            {
                UpdatePreview();
            }
        }

        private void UpdatePreview()
        {
            SelectedColor = HslToRgb(HueSlider.Value, SaturationSlider.Value, LightnessSlider.Value);
            ColorPreview.Fill = new SolidColorBrush(SelectedColor);
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
        }

        // [NEW] RGB to HSL conversion method
        public static (double h, double s, double l) RgbToHsl(Color c)
        {
            double r = c.R / 255.0;
            double g = c.G / 255.0;
            double b = c.B / 255.0;
            double max = Math.Max(r, Math.Max(g, b));
            double min = Math.Min(r, Math.Min(g, b));
            double h = 0, s = 0, l = (max + min) / 2.0;

            if (max != min)
            {
                double d = max - min;
                s = l > 0.5 ? d / (2.0 - max - min) : d / (max + min);
                if (max == r) h = (g - b) / d + (g < b ? 6.0 : 0);
                else if (max == g) h = (b - r) / d + 2.0;
                else if (max == b) h = (r - g) / d + 4.0;
                h /= 6.0;
            }
            return (h * 360, s, l);
        }

        // [FIX] Renamed lambda parameters to fix CS0136
        public static Color HslToRgb(double h, double s, double l)
        {
            double r, g, b;
            if (s == 0)
            {
                r = g = b = l;
            }
            else
            {
                Func<double, double, double, double> hue2rgb = (p_lambda, q_lambda, t_lambda) =>
                {
                    if (t_lambda < 0) t_lambda += 1;
                    if (t_lambda > 1) t_lambda -= 1;
                    if (t_lambda < 1.0 / 6) return p_lambda + (q_lambda - p_lambda) * 6 * t_lambda;
                    if (t_lambda < 1.0 / 2) return q_lambda;
                    if (t_lambda < 2.0 / 3) return p_lambda + (q_lambda - p_lambda) * (2.0 / 3 - t_lambda) * 6;
                    return p_lambda;
                };
                var q = l < 0.5 ? l * (1 + s) : l + s - l * s;
                var p = 2 * l - q;
                r = hue2rgb(p, q, h / 360 + 1.0 / 3);
                g = hue2rgb(p, q, h / 360);
                b = hue2rgb(p, q, h / 360 - 1.0 / 3);
            }
            return Color.FromRgb((byte)(r * 255), (byte)(g * 255), (byte)(b * 255));
        }
    }
}

