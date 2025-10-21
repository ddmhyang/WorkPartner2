// [신규 추가] WorkPartner/HueShiftEffect.cs
using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Effects;

namespace WorkPartner
{
    public class HueShiftEffect : ShaderEffect
    {
        // DependencyProperty for shader Input
        public static readonly DependencyProperty InputProperty =
            RegisterPixelShaderSamplerProperty("Input", typeof(HueShiftEffect), 0);

        // DependencyProperty for HueShift value
        public static readonly DependencyProperty HueShiftProperty =
            DependencyProperty.Register("HueShift", typeof(double), typeof(HueShiftEffect),
                new UIPropertyMetadata(0.0, PixelShaderConstantCallback(0)));

        private PixelShader _pixelShaderInstance;

        private static bool _isShaderLoadedSuccessfully = false;
        private static PixelShader _sharedPixelShader; // 성공적으로 로드된 셰이더 공유

        public HueShiftEffect()
        {
            // ✨ [수정] 셰이더 로딩 로직을 생성자로 이동하고 오류 처리 추가
            try
            {
                // 처음 로드 시도 또는 이전에 실패했을 경우에만 로드
                if (_sharedPixelShader == null && !_isShaderLoadedSuccessfully)
                {
                    // .csproj에서 Resource로 포함된 .ps 파일을 로드합니다.
                    _sharedPixelShader = new PixelShader { UriSource = new Uri("pack://application:,,,/WorkPartner;component/shaders/HueShift.ps") };
                    // 여기서 강제로 컴파일/로드를 시도합니다. 실패하면 예외 발생.
                    _sharedPixelShader.Freeze(); // 리소스를 프리즈하여 스레드 안전성 확보 및 성능 향상
                    _isShaderLoadedSuccessfully = true; // 성공 플래그 설정
                    Debug.WriteLine("HueShift.ps loaded successfully.");
                }

                // 성공적으로 로드된 공유 셰이더 인스턴스를 사용
                if (_isShaderLoadedSuccessfully)
                {
                    this.PixelShader = _sharedPixelShader;
                    UpdateShaderValue(InputProperty);
                    UpdateShaderValue(HueShiftProperty);
                }
                else
                {
                    // 로딩 실패 시 효과 비활성화 (오류 대신 빈 효과 적용)
                    this.PixelShader = null;
                    Debug.WriteLine("HueShiftEffect disabled due to previous loading failure.");
                }
            }
            catch (Exception ex)
            {
                // 로딩 실패 시 예외 기록 (Output 창 확인)
                Debug.WriteLine($"!!! Critical Error loading PixelShader 'HueShift.ps': {ex.Message}");
                Debug.WriteLine($"StackTrace: {ex.StackTrace}");
                if (ex.InnerException != null)
                {
                    Debug.WriteLine($"--- Inner Exception: {ex.InnerException.Message}");
                    Debug.WriteLine($"--- Inner StackTrace: {ex.InnerException.StackTrace}");
                }
                // 실패 시 효과 비활성화
                this.PixelShader = null;
                // 실패 상태 기록 (다시 로드 시도 방지)
                _isShaderLoadedSuccessfully = false;
                _sharedPixelShader = null;

                // ✨ 중요: XamlParseException을 방지하기 위해 생성자에서 예외를 throw하지 않습니다.
                // 대신 디버그 출력으로 오류를 알리고 효과를 비활성화합니다.
                // throw; // 여기서 throw하면 XamlParseException 발생
            }
        }
        // Getter/Setter for Input
        public Brush Input
        {
            get => (Brush)GetValue(InputProperty);
            set => SetValue(InputProperty, value);
        }

        /// <summary>
        /// Hue shift value (0.0 to 360.0).
        /// 0 = original, 180 = opposite color, 360 = original
        /// </summary>
        public double HueShift
        {
            get => (double)GetValue(HueShiftProperty);
            set
            {
                // C#에서는 0~360 값을 받고, 셰이더에는 0~1 값으로 정규화하여 전달
                double normalizedValue = (value % 360.0) / 360.0;
                SetValue(HueShiftProperty, normalizedValue);
            }
        }
    }
}