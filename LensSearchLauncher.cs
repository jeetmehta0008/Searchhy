using System;
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace Searchhy;

/// <summary>
/// High-speed, multi-provider image search launcher for Google Lens.
/// Supports global image hosting, Google uploadbyurl, clipboard image + file drop list, and active browser paste.
/// </summary>
public static class LensSearchLauncher
{
    private static readonly HttpClient _httpClient = new HttpClient
    {
        Timeout = TimeSpan.FromSeconds(4)
    };

    static LensSearchLauncher()
    {
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/130.0.0.0 Safari/537.36");
    }

    [DllImport("user32.dll")]
    private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    private const int VK_CONTROL = 0x11;
    private const int VK_V = 0x56;
    private const uint KEYEVENTF_KEYUP = 0x0002;

    public static async Task LaunchSearchAsync(byte[] pngBytes)
    {
        if (pngBytes == null || pngBytes.Length == 0) return;

        try
        {
            // 1. Save temp PNG file for FileDropList and local access
            string tempPngPath = Path.Combine(Path.GetTempPath(), $"searchhy_{Guid.NewGuid():N}.png");
            await File.WriteAllBytesAsync(tempPngPath, pngBytes);

            // 2. Put BOTH Bitmap AND FileDropList onto Windows Clipboard
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                try
                {
                    var dataObj = new System.Windows.DataObject();
                    using var ms = new MemoryStream(pngBytes);
                    using var bmp = new System.Drawing.Bitmap(ms);
                    var bs = ScreenCapture.ToBitmapSource(bmp);
                    dataObj.SetImage(bs);

                    var files = new StringCollection { tempPngPath };
                    dataObj.SetFileDropList(files);

                    System.Windows.Clipboard.SetDataObject(dataObj, true);
                    Logger.LogInfo("Image & FileDrop placed on Windows Clipboard.");
                }
                catch (Exception ex)
                {
                    Logger.LogError("Clipboard placement failed", ex);
                }
            });

            // 3. Attempt fast cloud upload to obtain direct Google Lens Results URL
            string? publicImageUrl = await TryFastUploadAsync(pngBytes);

            if (!string.IsNullOrEmpty(publicImageUrl))
            {
                string lensSearchUrl = $"https://lens.google.com/uploadbyurl?url={Uri.EscapeDataString(publicImageUrl)}";
                Logger.LogInfo($"Launching Google Lens search results: {lensSearchUrl}");

                Process.Start(new ProcessStartInfo
                {
                    FileName = lensSearchUrl,
                    UseShellExecute = true
                });
            }
            else
            {
                // Fallback: Open Google Lens homepage and simulate Ctrl+V
                Logger.LogInfo("Opening Google Lens in browser with clipboard auto-paste...");
                var proc = Process.Start(new ProcessStartInfo
                {
                    FileName = LensConfig.LensUrl,
                    UseShellExecute = true
                });

                _ = Task.Run(async () =>
                {
                    try
                    {
                        // Multi-pulse auto-paste sequence
                        await Task.Delay(800);
                        SimulatePaste();
                        await Task.Delay(1000);
                        SimulatePaste();
                        await Task.Delay(1500);
                        SimulatePaste();
                    }
                    catch { }
                });
            }
        }
        catch (Exception ex)
        {
            Logger.LogError("Error in LaunchSearchAsync", ex);
        }
    }

    private static async Task<string?> TryFastUploadAsync(byte[] pngBytes)
    {
        // Provider 1: FreeImage.host (Fast global CDN, not blocked in any region)
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2.8));
            using var content = new MultipartFormDataContent();
            content.Add(new StringContent("6d207e02198a847f52f0a572e4708424"), "key");
            content.Add(new StringContent("upload"), "action");
            content.Add(new StringContent("json"), "format");
            content.Add(new StringContent(Convert.ToBase64String(pngBytes)), "source");

            using var resp = await _httpClient.PostAsync("https://freeimage.host/api/1/upload", content, cts.Token);
            if (resp.IsSuccessStatusCode)
            {
                string json = await resp.Content.ReadAsStringAsync(cts.Token);
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("image", out var imgElem) &&
                    imgElem.TryGetProperty("url", out var urlElem))
                {
                    string directUrl = urlElem.GetString() ?? "";
                    if (!string.IsNullOrEmpty(directUrl))
                    {
                        Logger.LogInfo($"Upload succeeded (FreeImage): {directUrl}");
                        return directUrl;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Logger.LogWarn($"FreeImage upload skipped: {ex.Message}");
        }

        // Provider 2: Litterbox (Catbox temporary host)
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2.5));
            using var content = new MultipartFormDataContent();
            content.Add(new StringContent("fileupload"), "reqtype");
            content.Add(new StringContent("1h"), "time");
            
            var byteContent = new ByteArrayContent(pngBytes);
            byteContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/png");
            content.Add(byteContent, "fileToUpload", "search.png");

            using var resp = await _httpClient.PostAsync("https://litterbox.catbox.moe/resources/internals/api.php", content, cts.Token);
            if (resp.IsSuccessStatusCode)
            {
                string resultUrl = (await resp.Content.ReadAsStringAsync(cts.Token)).Trim();
                if (resultUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                {
                    Logger.LogInfo($"Upload succeeded (Litterbox): {resultUrl}");
                    return resultUrl;
                }
            }
        }
        catch (Exception ex)
        {
            Logger.LogWarn($"Litterbox upload skipped: {ex.Message}");
        }

        return null;
    }

    private static void SimulatePaste()
    {
        try
        {
            keybd_event((byte)VK_CONTROL, 0, 0, UIntPtr.Zero);
            keybd_event((byte)VK_V, 0, 0, UIntPtr.Zero);
            Thread.Sleep(50);
            keybd_event((byte)VK_V, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
            keybd_event((byte)VK_CONTROL, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
        }
        catch { }
    }
}
