using System;
using System.Diagnostics;
using System.Drawing; // ⚠️ 1단계에서 System.Drawing.Common NuGet 패키지 설치 필수!
using System.Drawing.Imaging;
using System.IO;
using System.Windows.Media; // (WPF Color)
using System.Windows.Media.Imaging;

namespace WorkPartner
{
    public static class ImageProcessor
    {
        /// <summary>
        /// ✨ [수정] "색상화"가 아닌 "곱하기(Multiply)"를 적용합니다.
        /// (회색조 이미지 * 선택한 색상 = 어두운 색상)
        /// </summary>
        public static BitmapSource ApplyTint(BitmapSource sourceImage, System.Windows.Media.Color tintColor)
        {
            Debug.WriteLine($"[ImageProcessor] ApplyTint 호출됨. 색상: {tintColor}");
            if (sourceImage == null) return null;

            // ✨ 곱하는 색이 흰색(기본값)이면, 변환할 필요 없이 원본을 반환
            if (tintColor == Colors.White)
            {
                return sourceImage;
            }

            try
            {
                // 1. WPF BitmapSource -> GDI+ Bitmap
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
                // ⚠️ 여기가 실행되면 "색조 반영이 안 되는" 증상이 나타남
                System.Diagnostics.Debug.WriteLine($"[ImageProcessor.ApplyTint] Error: {ex.Message}");
                System.Diagnostics.Debug.WriteLine(ex.StackTrace);
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
            BitmapSource bmpSource;
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
                bmpSource = bmpImage;
            }
            bitmap.Dispose(); // GDI+ 비트맵 리소스 해제
            return bmpSource;
        }
    }
}