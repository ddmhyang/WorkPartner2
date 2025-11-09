// WorkPartner/HslColorPicker.xaml.cs
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace WorkPartner
{
    public partial class HslColorPicker : UserControl
    {
        public event Action<object, Color> ColorChanged;

        private bool _isUpdatingFromSetter = false;
        private bool _isDragging = false;

        private double _currentHue = 0;
        private double _currentSaturation = 1;
        private double _currentValue = 1;

        public HslColorPicker()
        {
            InitializeComponent();

            // 컨트롤 로드 시 UI 업데이트
            this.Loaded += (s, e) => UpdateUIFromState(false);

            // ✨ [수정] 마우스 버튼 'Up' 이벤트를 UserControl 전체에 연결
            // (캡처된 마우스가 어디에서 해제되든 감지하기 위함)
            this.MouseLeftButtonUp += HslColorPicker_MouseLeftButtonUp;
        }

        #region 마우스 입력 및 UI 업데이트 로직

        // 1. 색조(H) 슬라이더가 변경될 때
        private void HueSlider_ValueChanged(object sender, System.Windows.RoutedPropertyChangedEventArgs<double> e)
        {
            if (_isUpdatingFromSetter) return;

            _currentHue = HueSlider.Value;

            // 색상판의 배경색(순수 H)을 변경
            HueBrush.Color = HsvToRgb(_currentHue, 1, 1);

            // 최종 색상 업데이트
            UpdateColorFromState(true);
        }

        // 2. 색상판(S/V)을 클릭하거나 드래그할 때
        private void ColorPlane_MouseInteraction(object sender, MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                // ✨ [수정] 드래그 시작 시 '마우스 캡처' 활성화
                if (!_isDragging)
                {
                    _isDragging = true;
                    // (중요) 마우스 이벤트를 이 컨트롤(Canvas)이 독점합니다.
                    ColorPlaneCanvas.CaptureMouse();
                }
            }

            // 드래그 중이 아니면 종료
            if (!_isDragging) return;

            Point position = e.GetPosition(ColorPlaneCanvas);

            // 마우스 위치(X, Y)를 S, V 값 (0-1)으로 변환
            // (컨트롤 밖으로 나가도 GetPosition은 캡처된 컨트롤 기준 좌표를 반환함)
            double s = Math.Clamp(position.X / ColorPlaneCanvas.ActualWidth, 0, 1);
            double v = 1 - Math.Clamp(position.Y / ColorPlaneCanvas.ActualHeight, 0, 1); // Y는 반대

            _currentSaturation = s;
            _currentValue = v;

            // 선택기(Thumb) 위치 이동
            UpdateSelectorThumbPosition();

            // 최종 색상 업데이트
            UpdateColorFromState(true);
        }

        // ✨ [추가] 마우스 버튼을 놓았을 때 (컨트롤 밖이어도 호출됨)
        private void HslColorPicker_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (_isDragging)
            {
                _isDragging = false;
                // (중요) 마우스 캡처를 해제합니다.
                ColorPlaneCanvas.ReleaseMouseCapture();
            }
        }

        // 3. 내부 (H, S, V) 상태로부터 최종 색상(RGB)을 계산하고 이벤트를 발생시킴
        private void UpdateColorFromState(bool raiseEvent)
        {
            Color rgbColor = HsvToRgb(_currentHue, _currentSaturation, _currentValue);
            PreviewBrush.Color = rgbColor;

            if (raiseEvent && !_isUpdatingFromSetter)
            {
                ColorChanged?.Invoke(this, rgbColor);
            }
        }

        // 4. 선택기(Thumb) 위치를 현재 S, V 값에 맞게 이동
        private void UpdateSelectorThumbPosition()
        {
            if (SelectorThumb == null || ColorPlaneCanvas == null) return;

            double x = _currentSaturation * ColorPlaneCanvas.ActualWidth;
            double y = (1 - _currentValue) * ColorPlaneCanvas.ActualHeight;

            if (SelectorThumb.RenderTransform is TranslateTransform transform)
            {
                transform.X = x;
                transform.Y = y;
            }
        }

        // 5. (H, S, V) 상태로부터 전체 UI를 업데이트 (SetHsl에서 호출됨)
        private void UpdateUIFromState(bool raiseEvent)
        {
            // H 슬라이더 값 설정
            HueSlider.Value = _currentHue;

            // 색상판 배경색 설정
            HueBrush.Color = HsvToRgb(_currentHue, 1, 1);

            // S/V 선택기 위치 설정
            UpdateSelectorThumbPosition();

            // 최종 색상 및 이벤트
            UpdateColorFromState(raiseEvent);
        }

        #endregion

        #region HSL <-> HSV <-> RGB 변환 (핵심)

        // (외부 호출) AvatarPage는 HSL 값을 사용하므로, HSL을 받아 내부 HSV로 변환
        public void SetHsl(double h, double s, double l)
        {
            _isUpdatingFromSetter = true;

            // (H, S, L) -> (H, S, V) 변환
            (double h_hsv, double s_hsv, double v_hsv) = HslToHsv(h, s, l);

            _currentHue = h_hsv;
            _currentSaturation = s_hsv;
            _currentValue = v_hsv;

            UpdateUIFromState(false); // UI 업데이트 (이벤트 미발생)

            _isUpdatingFromSetter = false;
        }

        // HSL -> HSV 변환
        public static (double H, double S, double V) HslToHsv(double h, double s, double l)
        {
            double v = l + s * Math.Min(l, 1 - l);
            double s_hsv = (v == 0) ? 0 : 2 * (1 - l / v);
            return (h, s_hsv, v);
        }

        // HSV -> HSL 변환 (참고용)
        public static (double H, double S, double L) HsvToHsl(double h, double s, double v)
        {
            double l = v * (1 - s / 2);
            double s_hsl = (l == 0 || l == 1) ? 0 : (v - l) / Math.Min(l, 1 - l);
            return (h, s_hsl, l);
        }

        // HSV -> RGB(Color) 변환
        public static Color HsvToRgb(double h, double s, double v)
        {
            int i = (int)Math.Floor(h / 60) % 6;
            double f = h / 60 - Math.Floor(h / 60);
            double p = v * (1 - s);
            double q = v * (1 - f * s);
            double t = v * (1 - (1 - f) * s);

            double r, g, b;
            switch (i)
            {
                case 0: r = v; g = t; b = p; break;
                case 1: r = q; g = v; b = p; break;
                case 2: r = p; g = v; b = t; break;
                case 3: r = p; g = q; b = v; break;
                case 4: r = t; g = p; b = v; break;
                default: r = v; g = p; b = q; break; // case 5
            }

            return Color.FromRgb((byte)(r * 255), (byte)(g * 255), (byte)(b * 255));
        }

        #endregion
    }
}