using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;

namespace Searchhy;

public static class IconBuilder
{
    [DllImport("shell32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern void SHChangeNotify(int wEventId, uint uFlags, IntPtr dwItem1, IntPtr dwItem2);

    private const int SHCNE_ASSOCCHANGED = 0x08000000;
    private const uint SHCNF_IDLIST = 0x0000;

    public static void ConvertPngToIco(string pngPath, string icoPath)
    {
        using var original = new Bitmap(pngPath);
        int[] sizes = { 256, 128, 64, 48, 32, 16 };

        using var fs = new FileStream(icoPath, FileMode.Create);
        using var bw = new BinaryWriter(fs);

        // ICO Header
        bw.Write((short)0);           // Reserved
        bw.Write((short)1);           // ICO type (1 = icon)
        bw.Write((short)sizes.Length); // Image count

        var imageStreams = new MemoryStream[sizes.Length];
        int offset = 6 + (16 * sizes.Length);

        for (int i = 0; i < sizes.Length; i++)
        {
            int size = sizes[i];
            var resized = new Bitmap(size, size, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(resized))
            {
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.SmoothingMode = SmoothingMode.HighQuality;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.CompositingQuality = CompositingQuality.HighQuality;
                g.DrawImage(original, 0, 0, size, size);
            }

            var ms = new MemoryStream();
            resized.Save(ms, ImageFormat.Png);
            imageStreams[i] = ms;

            // Directory entry
            bw.Write((byte)(size == 256 ? 0 : size)); // Width
            bw.Write((byte)(size == 256 ? 0 : size)); // Height
            bw.Write((byte)0);                         // Color palette
            bw.Write((byte)0);                         // Reserved
            bw.Write((short)1);                        // Color planes
            bw.Write((short)32);                       // Bits per pixel
            bw.Write((int)ms.Length);                  // Image size
            bw.Write(offset);                          // Offset to image data

            offset += (int)ms.Length;
            resized.Dispose();
        }

        // Write image data
        for (int i = 0; i < sizes.Length; i++)
        {
            bw.Write(imageStreams[i].ToArray());
            imageStreams[i].Dispose();
        }

        bw.Flush();
        fs.Flush();
    }

    public static void RefreshWindowsDesktop()
    {
        try
        {
            SHChangeNotify(SHCNE_ASSOCCHANGED, SHCNF_IDLIST, IntPtr.Zero, IntPtr.Zero);
        }
        catch { }
    }
}
