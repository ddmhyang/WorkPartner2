using System;
using System.Drawing;
// using System.Drawing.Imaging; // (주석 처리)
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
// ✨ [수정] GDI+의 PixelFormat에 대한 별칭(alias) 생성
using GdiPixelFormat = System.Drawing.Imaging.PixelFormat;

namespace WorkPartner
{
    public static class ImageProcessor
    {
        public static BitmapSource ApplyColor(BitmapSource sourceImage, System.Windows.Media.Color tintColor)
        {
            if (sourceImage == null) return null;

            if (tintColor == System.Windows.Media.Colors.White)
            {
                return sourceImage;
            }

            try
            {
                System.Drawing.Color gdiTint = System.Drawing.Color.FromArgb(tintColor.A, tintColor.R, tintColor.G, tintColor.B);

                float tintHue = gdiTint.GetHue();
                float tintSat = gdiTint.GetSaturation();
                float tintLum = gdiTint.GetBrightness();

                Bitmap gdiBitmap = ConvertBitmapSourceToBitmap(sourceImage);

                Rectangle rect = new Rectangle(0, 0, gdiBitmap.Width, gdiBitmap.Height);

                // ✨ [수정] 별칭을 사용하여 GDI+ PixelFormat을 명시
                System.Drawing.Imaging.BitmapData bmpData = gdiBitmap.LockBits(rect, System.Drawing.Imaging.ImageLockMode.ReadWrite, GdiPixelFormat.Format32bppArgb);

                IntPtr ptr = bmpData.Scan0;
                int bytes = Math.Abs(bmpData.Stride) * gdiBitmap.Height;
                byte[] bgraValues = new byte[bytes];
                System.Runtime.InteropServices.Marshal.Copy(ptr, bgraValues, 0, bytes);

                for (int i = 0; i < bgraValues.Length; i += 4)
                {
                    byte b = bgraValues[i];
                    byte g = bgraValues[i + 1];
                    byte r = bgraValues[i + 2];
                    byte a = bgraValues[i + 3];

                    if (a == 0) continue;

                    System.Drawing.Color originalPixel = System.Drawing.Color.FromArgb(a, r, g, b);
                    float originalLum = originalPixel.GetBrightness();
                    float newLum = originalLum * tintLum;
                    System.Drawing.Color newPixel = HslToRgb(tintHue, tintSat, newLum, a);

                    bgraValues[i] = newPixel.B;
                    bgraValues[i + 1] = newPixel.G;
                    bgraValues[i + 2] = newPixel.R;
                    bgraValues[i + 3] = a;
                }

                System.Runtime.InteropServices.Marshal.Copy(bgraValues, 0, ptr, bytes);
                gdiBitmap.UnlockBits(bmpData);

                return ConvertBitmapToBitmapSource(gdiBitmap);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ImageProcessor.ApplyColor] Error: {ex.Message}");
                System.Diagnostics.Debug.WriteLine(ex.StackTrace);
                return sourceImage;
            }
        }

        private static System.Drawing.Color HslToRgb(float h, float s, float l, byte a)
        {
            l = Math.Clamp(l, 0f, 1f);

            if (s == 0)
            {
                byte gray = (byte)(l * 255);
                return System.Drawing.Color.FromArgb(a, gray, gray, gray);
            }

            float q = l < 0.5f ? l * (1 + s) : l + s - l * s;
            float p = 2 * l - q;

            float r = HueToComponent(p, q, h / 360f + 1f / 3f);
            float g = HueToComponent(p, q, h / 360f);
            float b = HueToComponent(p, q, h / 360f - 1f / 3f);

            return System.Drawing.Color.FromArgb(a, (byte)(r * 255), (byte)(g * 255), (byte)(b * 255));
        }

        private static float HueToComponent(float p, float q, float t)
        {
            if (t < 0) t += 1;
            if (t > 1) t -= 1;
            if (t < 1f / 6f) return p + (q - p) * 6 * t;
            if (t < 1f / 2f) return q;
            if (t < 2f / 3f) return p + (q - p) * (2f / 3f - t) * 6;
            return p;
        }


        // --- Bitmap <-> BitmapSource 변환 헬퍼 (고화질 버전) ---

        private static Bitmap ConvertBitmapSourceToBitmap(BitmapSource bmpSource)
        {
            var formattedSource = new FormatConvertedBitmap(bmpSource, PixelFormats.Bgra32, null, 0);

            Bitmap gdiBitmap = new Bitmap(
                formattedSource.PixelWidth,
                formattedSource.PixelHeight,
                // ✨ [수정] 별칭을 사용하여 GDI+ PixelFormat을 명시
                GdiPixelFormat.Format32bppArgb);

            Rectangle rect = new Rectangle(0, 0, gdiBitmap.Width, gdiBitmap.Height);

            // ✨ [수정] 별칭을 사용하여 GDI+ PixelFormat을 명시
            System.Drawing.Imaging.BitmapData bmpData = gdiBitmap.LockBits(rect, System.Drawing.Imaging.ImageLockMode.WriteOnly, GdiPixelFormat.Format32bppArgb);

            try
            {
                formattedSource.CopyPixels(
                    Int32Rect.Empty,
                    bmpData.Scan0,
                    bmpData.Stride * bmpData.Height,
                    bmpData.Stride
                );
            }
            finally
            {
                gdiBitmap.UnlockBits(bmpData);
            }

            return gdiBitmap;
        }

        private static BitmapSource ConvertBitmapToBitmapSource(Bitmap bitmap)
        {
            Rectangle rect = new Rectangle(0, 0, bitmap.Width, bitmap.Height);

            // ✨ [수정] 별칭을 사용하여 GDI+ PixelFormat을 명시
            System.Drawing.Imaging.BitmapData bmpData = bitmap.LockBits(rect, System.Drawing.Imaging.ImageLockMode.ReadOnly, GdiPixelFormat.Format32bppArgb);

            BitmapSource bmpSource = null;

            try
            {
                // (이 부분은 WPF의 PixelFormats가 맞으므로 수정하지 않음)
                bmpSource = BitmapSource.Create(
                    bitmap.Width,
                    bitmap.Height,
                    bitmap.HorizontalResolution,
                    bitmap.VerticalResolution,
                    PixelFormats.Bgra32,
                    null,
                    bmpData.Scan0,
                    bmpData.Stride * bmpData.Height,
                    bmpData.Stride
                );

                bmpSource.Freeze();
            }
            finally
            {
                bitmap.UnlockBits(bmpData);
            }

            bitmap.Dispose();

            return bmpSource;
        }
    }
}