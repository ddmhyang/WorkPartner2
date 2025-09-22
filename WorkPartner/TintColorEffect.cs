using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Effects;

namespace WorkPartner
{
    // WPF의 ShaderEffect를 상속받아 커스텀 셰이더 효과를 정의합니다.
    public class TintColorEffect : ShaderEffect
    {
        // 셰이더 파일(.ps)을 리소스로 지정합니다.
        // "YourAppName"은 실제 프로젝트 이름(네임스페이스)으로 변경해야 합니다.
        private static PixelShader _pixelShader = new PixelShader { UriSource = new Uri("/WorkPartner;component/Shaders/TintColor.ps", UriKind.Relative) };

        // 1. TintColor 속성 정의 (C#에서 사용할 의존성 속성)
        public static readonly DependencyProperty TintColorProperty =
            DependencyProperty.Register("TintColor", typeof(Color), typeof(TintColorEffect),
                new PropertyMetadata(Colors.White, PixelShaderConstantCallback(0)));

        public Color TintColor
        {
            get { return (Color)GetValue(TintColorProperty); }
            set { SetValue(TintColorProperty, value); }
        }

        // 2. Input 속성 정의 (셰이더에 전달할 원본 이미지를 받기 위한 속성)
        public static readonly DependencyProperty InputProperty =
            ShaderEffect.RegisterPixelShaderSamplerProperty("Input", typeof(TintColorEffect), 0);

        public Brush Input
        {
            get { return (Brush)GetValue(InputProperty); }
            set { SetValue(InputProperty, value); }
        }

        // 생성자
        public TintColorEffect()
        {
            this.PixelShader = _pixelShader;
            UpdateShaderValue(TintColorProperty);
            UpdateShaderValue(InputProperty);
        }
    }
}
