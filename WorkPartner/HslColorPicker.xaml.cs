// WorkPartner/HslColorPicker.xaml.cs
using System;
using System.Windows.Controls;
using System.Windows.Media;

namespace WorkPartner
{
    public partial class HslColorPicker : UserControl
    {
        // 슬라이더 값이 바뀔 때마다 이 이벤트가 발생합니다.
        // (★참고: AvatarPage.xaml.cs의 기존 이벤트 핸들러와 맞추기 위해 object sender 추가)
        public event Action<object, Color> ColorChanged;

        private Color _currentColor;
        private bool _isUpdatingFromSetter = false; // SetHsl() 호출 시 이벤트 중복 방지

        public HslColorPicker()
        {
            InitializeComponent();
            UpdateColor(false); // 초기 색상 설정 (이벤트 미발생)
        }

        private void Slider_ValueChanged(object sender, System.Windows.RoutedPropertyChangedEventArgs<double> e)
        {
            // SetHsl()에 의해 값이 변경 중일 때는 이벤트를 발생시키지 않음
            if (_isUpdatingFromSetter) return;

            UpdateColor(true); // 사용자 조작이므로 이벤트 발생
        }

        private void UpdateColor(bool raiseEvent)
        {
            if (HueSlider == null || SaturationSlider == null || LightnessSlider == null)
                return;

            double h = HueSlider.Value;
            double s = SaturationSlider.Value;
            double l = LightnessSlider.Value;

            _currentColor = HslToWpfColor(h, s, l);

            if (PreviewBrush != null)
            {
                PreviewBrush.Color = _currentColor;
            }

            // AvatarPage에 이벤트 알림
            if (raiseEvent)
            {
                ColorChanged?.Invoke(this, _currentColor);
            }
        }

        /// <summary>
        /// 외부에서 HSL 값을 설정합니다. (이때는 ColorChanged 이벤트를 발생시키지 않습니다)
        /// </summary>
        public void SetHsl(double h, double s, double l)
        {
            _isUpdatingFromSetter = true; // 이벤트 방지 플래그 On
            try
            {
                HueSlider.Value = h;
                SaturationSlider.Value = s;
                LightnessSlider.Value = l;
                UpdateColor(false); // UI만 업데이트 (이벤트 미발생)
            }
            finally
            {
                _isUpdatingFromSetter = false; // 이벤트 방지 플래그 Off
            }
        }

        // --- HSL to RGB (WPF Color) 변환 헬퍼 ---
        public static Color HslToWpfColor(double h, double s, double l)
        {
            double r, g, b;

            if (s == 0)
            {
                r = g = b = l; // 회색조
            }
            else
            {
                double q = l < 0.5 ? l * (1 + s) : l + s - l * s;
                double p = 2 * l - q;
                r = HueToRgb(p, q, h / 360 + 1.0 / 3.0);
                g = HueToRgb(p, q, h / 360);
                b = HueToRgb(p, q, h / 360 - 1.0 / 3.0);
            }
            return Color.FromRgb((byte)(r * 255), (byte)(g * 255), (byte)(b * 255));
        }

        private static double HueToRgb(double p, double q, double t)
        {
            if (t < 0) t += 1;
            if (t > 1) t -= 1;
            if (t < 1.0 / 6.0) return p + (q - p) * 6 * t;
            if (t < 1.0 / 2.0) return q;
            if (t < 2.0 / 3.0) return p + (q - p) * (2.0 / 3.0 - t) * 6;
            return p;
        }
    }
}