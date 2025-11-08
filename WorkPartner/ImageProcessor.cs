using System;
using System.Drawing; // ⚠️ 1단계에서 System.Drawing.Common NuGet 패키지 설치 필수!
using System.Drawing.Imaging;
using System.IO;
using System.Windows.Media.Imaging;

namespace WorkPartner
{
    public static class ImageProcessor
    {
        /// <summary>
        /// ✨ [수정] "곱하기"가 아닌 "색상화(Colorize)"를 적용합니다.
        /// 원본의 밝기(L)는 유지하고, 선택한 색의 색조(H)와 채도(S)를 입힙니다.
        /// </summary>
        public static BitmapSource ApplyColor(BitmapSource sourceImage, System.Windows.Media.Color tintColor)
        {
            if (sourceImage == null) return null;

            // ✨ 색이 흰색(기본값)이면 변환할 필요 없이 원본 반환
            if (tintColor == System.Windows.Media.Colors.White)
            {
                return sourceImage;
            }

            try
            {
                // 1. WPF Color -> GDI+ Color
                System.Drawing.Color gdiTint = System.Drawing.Color.FromArgb(tintColor.A, tintColor.R, tintColor.G, tintColor.B);

                // 2. Get the HUE and SATURATION from the tint.
                float tintHue = gdiTint.GetHue();
                float tintSat = gdiTint.GetSaturation();
                float tintLum = gdiTint.GetBrightness(); // (선택한 색이 회색조일 때 사용)

                // 3. WPF BitmapSource -> GDI+ Bitmap
                Bitmap gdiBitmap = ConvertBitmapSourceToBitmap(sourceImage);

                // 3.5 GDI+ 비트맵이 32bpp ARGB가 아니면 변환 (픽셀 접근을 위해)
                if (gdiBitmap.PixelFormat != PixelFormat.Format32bppArgb)
                {
                    Bitmap temp = new Bitmap(gdiBitmap.Width, gdiBitmap.Height, PixelFormat.Format32bppArgb);
                    using (Graphics g = Graphics.FromImage(temp))
                    {
                        g.DrawImage(gdiBitmap, new Rectangle(0, 0, temp.Width, temp.Height));
                    }
                    gdiBitmap.Dispose(); // 원본 GDI 비트맵 해제
                    gdiBitmap = temp;
                }

                // 4. Use LockBits for fast pixel manipulation
                Rectangle rect = new Rectangle(0, 0, gdiBitmap.Width, gdiBitmap.Height);
                BitmapData bmpData = gdiBitmap.LockBits(rect, ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb);

                IntPtr ptr = bmpData.Scan0;
                int bytes = Math.Abs(bmpData.Stride) * gdiBitmap.Height;
                byte[] bgraValues = new byte[bytes];
                System.Runtime.InteropServices.Marshal.Copy(ptr, bgraValues, 0, bytes);

                // 5. Loop through pixels (BGRA format)
                for (int i = 0; i < bgraValues.Length; i += 4)
                {
                    byte b = bgraValues[i];
                    byte g = bgraValues[i + 1];
                    byte r = bgraValues[i + 2];
                    byte a = bgraValues[i + 3];

                    // Skip transparent pixels
                    if (a == 0) continue;

                    // Get original image's HSL
                    // (Since it's grayscale, R=G=B, so H=0, S=0)
                    // We just need the Luminance (Brightness)
                    System.Drawing.Color originalPixel = System.Drawing.Color.FromArgb(a, r, g, b);
                    float originalLum = originalPixel.GetBrightness();

                    System.Drawing.Color newPixel;
                    if (tintSat < 0.01f)
                    {
                        // The tint is grayscale (white/gray/black).
                        // "색상화" 대신 "곱하기"를 해서 밝기만 조절
                        float lum = originalLum * tintLum;
                        byte newGray = (byte)(Math.Clamp(lum, 0, 1) * 255);
                        newPixel = System.Drawing.Color.FromArgb(a, newGray, newGray, newGray);
                    }
                    else
                    {
                        // The tint has color. Use Tint's H+S, Image's L.
                        newPixel = HslToRgb(tintHue, tintSat, originalLum, a);
                    }

                    bgraValues[i] = newPixel.B;
                    bgraValues[i + 1] = newPixel.G;
                    bgraValues[i + 2] = newPixel.R;
                    bgraValues[i + 3] = a; // Preserve original alpha
                }

                // 6. Copy back
                System.Runtime.InteropServices.Marshal.Copy(bgraValues, 0, ptr, bytes);
                gdiBitmap.UnlockBits(bmpData);

                // 7. GDI+ Bitmap -> WPF BitmapSource
                return ConvertBitmapToBitmapSource(gdiBitmap);
            }
            catch (Exception ex)
            {
                // ⚠️ 여기가 실행되면 "색조 반영이 안 되는" 증상이 나타남
                System.Diagnostics.Debug.WriteLine($"[ImageProcessor.ApplyColor] Error: {ex.Message}");
                System.Diagnostics.Debug.WriteLine(ex.StackTrace);
                return sourceImage; // 오류 시 원본 반환
            }
        }

        // HSL to RGB Helper (System.Drawing.Color 기반)
        private static System.Drawing.Color HslToRgb(float h, float s, float l, byte a)
        {
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

        // Hue to Component Helper
        private static float HueToComponent(float p, float q, float t)
        {
            if (t < 0) t += 1;
            if (t > 1) t -= 1;
            if (t < 1f / 6f) return p + (q - p) * 6 * t;
            if (t < 1f / 2f) return q;
            if (t < 2f / 3f) return p + (q - p) * (2f / 3f - t) * 6;
            return p;
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