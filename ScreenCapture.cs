using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;

namespace Searchhy;

/// <summary>
/// Ultra-fast multi-monitor DPI-aware screen capture and memory cropping in physical pixels.
/// Optimized for zero latency.
/// </summary>
public static class ScreenCapture
{
    private const int SM_XVIRTUALSCREEN = 76;
    private const int SM_YVIRTUALSCREEN = 77;
    private const int SM_CXVIRTUALSCREEN = 78;
    private const int SM_CYVIRTUALSCREEN = 79;

    private const int SRCCOPY = 0x00CC0020;
    private const int CAPTUREBLT = 0x40000000;

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int nIndex);

    [DllImport("user32.dll")]
    private static extern IntPtr GetDC(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateCompatibleDC(IntPtr hdc);

    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateCompatibleBitmap(IntPtr hdc, int nWidth, int nHeight);

    [DllImport("gdi32.dll")]
    private static extern IntPtr SelectObject(IntPtr hdc, IntPtr hgdiobj);

    [DllImport("gdi32.dll")]
    private static extern bool BitBlt(IntPtr hdcDest, int nXDest, int nYDest, int nWidth, int nHeight, IntPtr hdcSrc, int nXSrc, int nYSrc, int dwRop);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteDC(IntPtr hdc);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteObject(IntPtr hObject);

    public record VirtualScreenBounds(int Left, int Top, int Width, int Height);

    public static VirtualScreenBounds GetVirtualScreenBounds()
    {
        int left = GetSystemMetrics(SM_XVIRTUALSCREEN);
        int top = GetSystemMetrics(SM_YVIRTUALSCREEN);
        int width = GetSystemMetrics(SM_CXVIRTUALSCREEN);
        int height = GetSystemMetrics(SM_CYVIRTUALSCREEN);

        return new VirtualScreenBounds(left, top, width, height);
    }

    public static (Bitmap Bitmap, VirtualScreenBounds Bounds) CaptureVirtualScreen()
    {
        var bounds = GetVirtualScreenBounds();
        IntPtr hScreenDC = GetDC(IntPtr.Zero);
        IntPtr hMemoryDC = CreateCompatibleDC(hScreenDC);
        IntPtr hBitmap = CreateCompatibleBitmap(hScreenDC, bounds.Width, bounds.Height);
        IntPtr hOldBitmap = SelectObject(hMemoryDC, hBitmap);

        try
        {
            BitBlt(hMemoryDC, 0, 0, bounds.Width, bounds.Height, hScreenDC, bounds.Left, bounds.Top, SRCCOPY | CAPTUREBLT);
            SelectObject(hMemoryDC, hOldBitmap);

            Bitmap screenBitmap = Image.FromHbitmap(hBitmap);
            return (screenBitmap, bounds);
        }
        catch (Exception ex)
        {
            Logger.LogError("Failed during screen capture BitBlt", ex);
            throw;
        }
        finally
        {
            DeleteObject(hBitmap);
            DeleteDC(hMemoryDC);
            ReleaseDC(IntPtr.Zero, hScreenDC);
        }
    }

    /// <summary>
    /// Converts a System.Drawing.Bitmap to a WPF BitmapSource in under 1ms via native HBitmap.
    /// </summary>
    public static BitmapSource ToBitmapSource(Bitmap bitmap)
    {
        IntPtr hBitmap = bitmap.GetHbitmap();
        try
        {
            var source = Imaging.CreateBitmapSourceFromHBitmap(
                hBitmap,
                IntPtr.Zero,
                Int32Rect.Empty,
                BitmapSizeOptions.FromEmptyOptions());
            source.Freeze();
            return source;
        }
        finally
        {
            DeleteObject(hBitmap);
        }
    }

    public static byte[] CropToPngBytes(Bitmap fullScreenshot, Rectangle cropRect)
    {
        int x = Math.Max(0, Math.Min(cropRect.X, fullScreenshot.Width - 1));
        int y = Math.Max(0, Math.Min(cropRect.Y, fullScreenshot.Height - 1));
        int width = Math.Max(1, Math.Min(cropRect.Width, fullScreenshot.Width - x));
        int height = Math.Max(1, Math.Min(cropRect.Height, fullScreenshot.Height - y));

        var clampedRect = new Rectangle(x, y, width, height);

        using var croppedBitmap = new Bitmap(clampedRect.Width, clampedRect.Height, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(croppedBitmap))
        {
            g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
            g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;

            g.DrawImage(
                fullScreenshot,
                new Rectangle(0, 0, clampedRect.Width, clampedRect.Height),
                clampedRect,
                GraphicsUnit.Pixel);
        }

        using var ms = new MemoryStream();
        croppedBitmap.Save(ms, ImageFormat.Png);
        return ms.ToArray();
    }
}
