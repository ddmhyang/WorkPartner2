using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace WorkPartner
{
    // ✨ [수정] Window가 아닌 UserControl을 상속
    public partial class ColorPalette : UserControl
    {
        // ✨ [신규 추가] AvatarPage.xaml.cs가 호출할 이벤트
        public event Action<object, Color> ColorChanged;

        private Color _selectedColor = Colors.White;

        // ✨ [수정] set;을 public으로 변경 (private 삭제)
        public Color SelectedColor
        {
            get => _selectedColor;
            set
            {
                if (_selectedColor != value)
                {
                    _selectedColor = value;
                    UpdateUI();
                    // ✨ [신규 추가] 색이 바뀌면 이벤트를 호출
                    ColorChanged?.Invoke(this, _selectedColor);
                }
            }
        }

        public ColorPalette()
        {
            InitializeComponent();
            GenerateColorSwatches();
        }

        private void GenerateColorSwatches()
        {
            // XAML의 ColorPanel을 찾습니다.
            var colorPanel = FindName("ColorPanel") as WrapPanel;
            if (colorPanel == null) return;

            colorPanel.Children.Clear();

            // ✨ [수정] 요청하신 Hex 코드로 색상 리스트 변경
            List<string> colors = new List<string>
            {
                "#FFFFFF", "#9E9E9E", "#607D8B", "#000000", // 흰/회/검 계열
                "#FF5252", "#FF4081", "#E040FB", "#7C4DFF", "#536DFE", "#448AFF",
                "#03A9F4", "#00BCD4", "#009688", "#4CAF50", "#8BC34A", "#CDDC39",
                "#FFEB3B", "#FFC107", "#FF9800", "#FF5722", "#795548"
            };

            foreach (var hex in colors)
            {
                var color = (Color)ColorConverter.ConvertFromString(hex);
                var swatch = new Rectangle
                {
                    Width = 20,
                    Height = 20,
                    Fill = new SolidColorBrush(color),
                    Margin = new Thickness(2),
                    Cursor = Cursors.Hand,
                };
                swatch.MouseLeftButtonDown += Swatch_Click;
                colorPanel.Children.Add(swatch);
            }
        }

        private void Swatch_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is Rectangle swatch && swatch.Fill is SolidColorBrush brush)
            {
                SelectedColor = brush.Color;
            }
        }

        private void UpdateUI()
        {
            // (선택된 색상 테두리 표시 등... UI 업데이트 로직)
        }

        // ✨ [신규 추가] AvatarPage.xaml.cs가 호출할 리셋 함수
        public void Reset()
        {
            // 기본값 (흰색)으로 되돌림
            SelectedColor = Colors.White;
        }
    }
}