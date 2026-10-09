using System;
using System.Drawing;
using System.Drawing.Imaging;

namespace Searchhy;

/// <summary>
/// AI-like edge and contour detector that automatically detects and snaps to prominent shapes/icons
/// inside the drawn circle selection (Google Lens style object snapping).
/// </summary>
public static class SmartSnapDetector
{
    /// <summary>
    /// Analyzes the bitmap inside the rough bounding box and returns an adjusted, tightly-fitted bounding box
    /// if a prominent shape, icon, or card is detected.
    /// </summary>
    public static Rectangle SnapToContour(Bitmap screenBmp, Rectangle roughRect)
    {
        if (roughRect.Width < 30 || roughRect.Height < 30) return roughRect;

        int clampX = Math.Max(0, Math.Min(roughRect.X, screenBmp.Width - 1));
        int clampY = Math.Max(0, Math.Min(roughRect.Y, screenBmp.Height - 1));
        int clampW = Math.Max(10, Math.Min(roughRect.Width, screenBmp.Width - clampX));
        int clampH = Math.Max(10, Math.Min(roughRect.Height, screenBmp.Height - clampY));

        try
        {
            var rect = new Rectangle(clampX, clampY, clampW, clampH);
            var bmpData = screenBmp.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);

            int stride = bmpData.Stride;
            IntPtr scan0 = bmpData.Scan0;
            int bytes = Math.Abs(stride) * clampH;
            byte[] rgbValues = new byte[bytes];
            System.Runtime.InteropServices.Marshal.Copy(scan0, rgbValues, 0, bytes);
            screenBmp.UnlockBits(bmpData);

            // Compute background sample from the 4 outer corners
            int bgR = 0, bgG = 0, bgB = 0;
            int cornerSamples = 0;

            void SampleCorner(int px, int py)
            {
                if (px < 0 || px >= clampW || py < 0 || py >= clampH) return;
                int idx = py * stride + px * 4;
                if (idx + 3 < bytes)
                {
                    bgB += rgbValues[idx];
                    bgG += rgbValues[idx + 1];
                    bgR += rgbValues[idx + 2];
                    cornerSamples++;
                }
            }

            SampleCorner(2, 2);
            SampleCorner(clampW - 3, 2);
            SampleCorner(2, clampH - 3);
            SampleCorner(clampW - 3, clampH - 3);

            if (cornerSamples > 0)
            {
                bgR /= cornerSamples;
                bgG /= cornerSamples;
                bgB /= cornerSamples;
            }

            int minX = clampW, maxX = 0;
            int minY = clampH, maxY = 0;
            int foregroundPixelCount = 0;

            // Scan pixels for contrast against background and internal edge gradient
            const int colorThreshold = 28;
            for (int y = 2; y < clampH - 2; y += 2)
            {
                int rowIdx = y * stride;
                for (int x = 2; x < clampW - 2; x += 2)
                {
                    int idx = rowIdx + x * 4;
                    int b = rgbValues[idx];
                    int g = rgbValues[idx + 1];
                    int r = rgbValues[idx + 2];

                    int diff = Math.Abs(r - bgR) + Math.Abs(g - bgG) + Math.Abs(b - bgB);
                    if (diff > colorThreshold * 3)
                    {
                        minX = Math.Min(minX, x);
                        maxX = Math.Max(maxX, x);
                        minY = Math.Min(minY, y);
                        maxY = Math.Max(maxY, y);
                        foregroundPixelCount++;
                    }
                }
            }

            int totalSampled = (clampW / 2) * (clampH / 2);
            if (foregroundPixelCount > 30 && foregroundPixelCount < totalSampled * 0.92)
            {
                int detectedW = maxX - minX;
                int detectedH = maxY - minY;

                // Ensure detected shape is significant (> 25% of rough crop dimension)
                if (detectedW >= clampW * 0.25 && detectedH >= clampH * 0.25)
                {
                    // Add 6px padding around detected shape
                    int snapX = Math.Max(0, clampX + minX - 6);
                    int snapY = Math.Max(0, clampY + minY - 6);
                    int snapW = Math.Min(screenBmp.Width - snapX, detectedW + 12);
                    int snapH = Math.Min(screenBmp.Height - snapY, detectedH + 12);

                    Logger.LogInfo($"Smart Snap detected shape: {snapW}x{snapH} at ({snapX},{snapY})");
                    return new Rectangle(snapX, snapY, snapW, snapH);
                }
            }
        }
        catch (Exception ex)
        {
            Logger.LogWarn($"SmartSnap error: {ex.Message}");
        }

        return roughRect;
    }
}
