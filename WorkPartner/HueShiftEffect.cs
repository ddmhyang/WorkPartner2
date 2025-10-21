// [신규 추가] WorkPartner/HueShiftEffect.cs
using System;
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

        private static readonly PixelShader _pixelShader =
            new PixelShader { UriSource = new Uri("pack://application:,,,/WorkPartner;component/shaders/HueShift.ps") };

        public HueShiftEffect()
        {
            PixelShader = _pixelShader;
            UpdateShaderValue(InputProperty);
            UpdateShaderValue(HueShiftProperty);
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