using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace WorkPartner
{
    public partial class ColorPalette : UserControl
    {
        public event Action<object, Color> ColorChanged;

        private Color _selectedColor = Colors.White;

        public Color SelectedColor
        {
            get => _selectedColor;
            set
            {
                if (_selectedColor != value)
                {
                    _selectedColor = value;
                    UpdateUI();
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
            var colorPanel = FindName("ColorPanel") as WrapPanel;
            if (colorPanel == null) return;

            colorPanel.Children.Clear();

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

        public void Reset()
        {
            SelectedColor = Colors.White;
        }
    }
}