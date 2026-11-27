using System;
using System.Drawing;
using System.Drawing.Text;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using BlackIceGPUTemp.Logging;

namespace BlackIceGPUTemp.Tray
{
    internal static class TrayIconRenderer
    {
        private const int IconBaseSize = 16;
        private const int BaseDpi = 96;
        private const float SegoeUiFontSize = 12f;
        private const float TahomaFontSize = 11f;
        private const float DefaultFontSize = 12f;
        private const float FontSizeMultiplier = 1f;
        private const FontStyle IconFontStyle = FontStyle.Bold;
        private const float VerticalOffset = -1f;
        private const string SegoeUiFontFamily = "Segoe UI";
        private const string TahomaFontFamily = "Tahoma";

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool DestroyIcon(IntPtr hIcon);

        internal static Icon? CreateTemperatureIcon(string text)
        {
            float dpiX, dpiY;
            using (var bmp = new Bitmap(1, 1, System.Drawing.Imaging.PixelFormat.Format32bppArgb))
            {
                dpiX = bmp.HorizontalResolution;
                dpiY = bmp.VerticalResolution;
            }

            int width = (int)Math.Round(IconBaseSize * dpiX / BaseDpi);
            int height = (int)Math.Round(IconBaseSize * dpiY / BaseDpi);
            width = width < IconBaseSize ? IconBaseSize : width;
            height = height < IconBaseSize ? IconBaseSize : height;

            var fontFamily = SystemFonts.MessageBoxFont.FontFamily;

            float baseSize = fontFamily.Name switch
            {
                SegoeUiFontFamily => SegoeUiFontSize,
                TahomaFontFamily => TahomaFontSize,
                _ => DefaultFontSize
            };

            float fontSize = baseSize * width / IconBaseSize * FontSizeMultiplier;

            var bitmap = new Bitmap(width, height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            IntPtr hIcon = IntPtr.Zero;

            try
            {
                using (var graphics = Graphics.FromImage(bitmap))
                {
                    graphics.Clear(Color.Transparent);

                    graphics.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
                    graphics.SmoothingMode = SmoothingMode.HighQuality;

                    using (var font = new Font(fontFamily, fontSize, IconFontStyle, GraphicsUnit.Pixel))
                    using (var brush = new SolidBrush(Color.White))
                    {
                        var bounds = new Rectangle(0, 0, width, height);
                        var flags = TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding;

                        if (VerticalOffset != 0)
                        {
                            bounds = new Rectangle(0, (int)Math.Round(VerticalOffset), width, height);
                        }

                        TextRenderer.DrawText(graphics, text, font, bounds, Color.White, Color.Transparent, flags);
                    }
                }

                hIcon = bitmap.GetHicon();
                if (hIcon == IntPtr.Zero)
                {
                    return null;
                }

                var icon = Icon.FromHandle(hIcon);
                var clonedIcon = (Icon)icon.Clone();
                return clonedIcon;
            }
            catch (Exception ex)
            {
                Logger.LogError("CreateTemperatureIcon: exception creating temperature icon", ex);
                return null;
            }
            finally
            {
                if (hIcon != IntPtr.Zero)
                {
                    DestroyIcon(hIcon);
                }
                bitmap.Dispose();
            }
        }
    }
}