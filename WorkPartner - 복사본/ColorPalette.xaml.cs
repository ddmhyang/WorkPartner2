using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace WorkPartner
{
    public partial class ColorPalette : Window
    {
        public Color SelectedColor { get; private set; }

        private readonly List<string> _colorHexes = new List<string>
        {
            "#FF5252", "#FF4081", "#E040FB", "#7C4DFF", "#536DFE", "#448AFF",
            "#03A9F4", "#00BCD4", "#009688", "#4CAF50", "#8BC34A", "#CDDC39",
            "#FFEB3B", "#FFC107", "#FF9800", "#FF5722", "#795548", "#9E9E9E",
            "#607D8B", "#FFFFFF", "#000000"
        };

        public ColorPalette()
        {
            InitializeComponent();
            PopulateColors();
        }

        private void PopulateColors()
        {
            foreach (var hex in _colorHexes)
            {
                var color = (Color)ColorConverter.ConvertFromString(hex);
                var colorButton = new Button
                {
                    Width = 25,
                    Height = 25,
                    Margin = new Thickness(3),
                    Background = new SolidColorBrush(color),
                    Tag = color,
                    Cursor = Cursors.Hand
                };

                // 테두리를 추가하여 흰색과 같은 밝은 색을 구분
                if (color == Colors.White)
                {
                    colorButton.BorderBrush = Brushes.LightGray;
                    colorButton.BorderThickness = new Thickness(1);
                }

                colorButton.Click += ColorButton_Click;
                ColorPanel.Children.Add(colorButton);
            }
        }

        private void ColorButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is Color color)
            {
                SelectedColor = color;
                this.DialogResult = true;
                this.Close();
            }
        }
    }
}