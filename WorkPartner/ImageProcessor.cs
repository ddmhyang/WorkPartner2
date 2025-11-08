using System;
using System.Drawing; // ⚠️ System.Drawing.Common NuGet 패키지 필요!
using System.Drawing.Imaging;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace WorkPartner
{
    public static class ImageProcessor
    {
        /// <summary>
        /// 회색조 이미지에 '곱하기(Multiply)' 효과로 색상을 입힙니다.
        /// </summary>
        /// <param name="sourceImage">원본 BitmapSource (회색조)</param>
        /// <param name="tintColor">입힐 색상 (WPF Color)</param>
        /// <returns>색상이 적용된 새 BitmapSource</returns>
        public static BitmapSource ApplyTint(BitmapSource sourceImage, System.Windows.Media.Color tintColor)
        {
            if (sourceImage == null) return null;

            try
            {
                // 1. WPF BitmapSource -> GDI+ Bitmap 으로 변환
                Bitmap gdiBitmap = ConvertBitmapSourceToBitmap(sourceImage);

                // 2. '곱하기(Multiply)' 효과를 내는 ColorMatrix 생성
                ImageAttributes attributes = new ImageAttributes();
                attributes.SetColorMatrix(CreateMultiplyMatrix(tintColor));

                // 3. 새 Bitmap에 색상이 적용된 이미지 그리기
                Bitmap resultBitmap = new Bitmap(gdiBitmap.Width, gdiBitmap.Height);
                using (Graphics g = Graphics.FromImage(resultBitmap))
                {
                    g.DrawImage(
                        gdiBitmap,
                        new Rectangle(0, 0, gdiBitmap.Width, gdiBitmap.Height),
                        0, 0, gdiBitmap.Width, gdiBitmap.Height,
                        GraphicsUnit.Pixel,
                        attributes
                    );
                }

                // 4. GDI+ Bitmap -> WPF BitmapSource 로 다시 변환
                return ConvertBitmapToBitmapSource(resultBitmap);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ImageProcessor.ApplyTint] Error: {ex.Message}");
                return sourceImage; // 오류 시 원본 반환
            }
        }

        /// <summary>
        /// '곱하기(Multiply)' 블렌드 모드 ColorMatrix를 생성합니다.
        /// </summary>
        private static ColorMatrix CreateMultiplyMatrix(System.Windows.Media.Color color)
        {
            // 색상 값을 0.0 ~ 1.0 범위로 정규화
            float r = color.R / 255f;
            float g = color.G / 255f;
            float b = color.B / 255f;
            float a = color.A / 255f; // 알파값도 곱하기에 포함

            return new ColorMatrix(new float[][]
            {
                new float[] { r, 0, 0, 0, 0 }, // R_out = R_in * r
                new float[] { 0, g, 0, 0, 0 }, // G_out = G_in * g
                new float[] { 0, 0, b, 0, 0 }, // B_out = B_in * b
                new float[] { 0, 0, 0, a, 0 }, // A_out = A_in * a
                new float[] { 0, 0, 0, 0, 1 }
            });
        }

        // --- Bitmap <-> BitmapSource 변환 헬퍼 ---

        private static Bitmap ConvertBitmapSourceToBitmap(BitmapSource bmpSource)
        {
            using (MemoryStream stream = new MemoryStream())
            {
                BitmapEncoder encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(bmpSource));
                encoder.Save(stream);
                return new Bitmap(stream);
            }
        }

        private static BitmapSource ConvertBitmapToBitmapSource(Bitmap bitmap)
        {
            using (MemoryStream stream = new MemoryStream())
            {
                bitmap.Save(stream, ImageFormat.Png);
                stream.Position = 0;
                BitmapImage bmpImage = new BitmapImage();
                bmpImage.BeginInit();
                bmpImage.StreamSource = stream;
                bmpImage.CacheOption = BitmapCacheOption.OnLoad;
                bmpImage.EndInit();
                bmpImage.Freeze();
                return bmpImage;
            }
        }
    }
}