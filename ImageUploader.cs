using System;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Searchhy;

/// <summary>
/// Fast, robust image uploader for Google Lens reverse image search.
/// Uploads the cropped PNG to temporary storage so Google Lens can process it via URL directly.
/// </summary>
public static class ImageUploader
{
    private static readonly HttpClient _httpClient = new HttpClient
    {
        Timeout = TimeSpan.FromSeconds(5)
    };

    static ImageUploader()
    {
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/130.0.0.0 Safari/537.36");
    }

    /// <summary>
    /// Uploads PNG bytes to temporary image hosting and returns the public direct URL.
    /// Returns null if upload fails or times out.
    /// </summary>
    public static async Task<string?> UploadToTempHostAsync(byte[] pngBytes, CancellationToken ct = default)
    {
        if (pngBytes == null || pngBytes.Length == 0) return null;

        // Try Litterbox (Catbox temporary 1-hour host)
        try
        {
            using var content = new MultipartFormDataContent();
            content.Add(new StringContent("fileupload"), "reqtype");
            content.Add(new StringContent("1h"), "time");
            
            var byteContent = new ByteArrayContent(pngBytes);
            byteContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/png");
            content.Add(byteContent, "fileToUpload", "search_crop.png");

            using var resp = await _httpClient.PostAsync("https://litterbox.catbox.moe/resources/internals/api.php", content, ct);
            if (resp.IsSuccessStatusCode)
            {
                string resultUrl = (await resp.Content.ReadAsStringAsync(ct)).Trim();
                if (Uri.TryCreate(resultUrl, UriKind.Absolute, out var uri) && (uri.Scheme == "http" || uri.Scheme == "https"))
                {
                    Logger.LogInfo($"Image uploaded successfully to Litterbox: {resultUrl}");
                    return resultUrl;
                }
            }
        }
        catch (Exception ex)
        {
            Logger.LogWarn($"Litterbox upload failed: {ex.Message}");
        }

        // Fallback 1: TmpFiles.org
        try
        {
            using var content = new MultipartFormDataContent();
            var byteContent = new ByteArrayContent(pngBytes);
            byteContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/png");
            content.Add(byteContent, "file", "search_crop.png");

            using var resp = await _httpClient.PostAsync("https://tmpfiles.org/api/v1/upload", content, ct);
            if (resp.IsSuccessStatusCode)
            {
                string json = await resp.Content.ReadAsStringAsync(ct);
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("data", out var dataElem) &&
                    dataElem.TryGetProperty("url", out var urlElem))
                {
                    string rawUrl = urlElem.GetString() ?? "";
                    // Convert https://tmpfiles.org/12345/img.png -> https://tmpfiles.org/dl/12345/img.png for direct link
                    string directUrl = rawUrl.Replace("tmpfiles.org/", "tmpfiles.org/dl/");
                    Logger.LogInfo($"Image uploaded successfully to TmpFiles: {directUrl}");
                    return directUrl;
                }
            }
        }
        catch (Exception ex)
        {
            Logger.LogWarn($"TmpFiles upload failed: {ex.Message}");
        }

        // Fallback 2: FreeImage.host
        try
        {
            using var content = new MultipartFormDataContent();
            content.Add(new StringContent("6d207e02198a847f52f0a572e4708424"), "key");
            content.Add(new StringContent("upload"), "action");
            content.Add(new StringContent("json"), "format");
            content.Add(new StringContent(Convert.ToBase64String(pngBytes)), "source");

            using var resp = await _httpClient.PostAsync("https://freeimage.host/api/1/upload", content, ct);
            if (resp.IsSuccessStatusCode)
            {
                string json = await resp.Content.ReadAsStringAsync(ct);
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("image", out var imgElem) &&
                    imgElem.TryGetProperty("url", out var urlElem))
                {
                    string directUrl = urlElem.GetString() ?? "";
                    Logger.LogInfo($"Image uploaded successfully to FreeImage: {directUrl}");
                    return directUrl;
                }
            }
        }
        catch (Exception ex)
        {
            Logger.LogWarn($"FreeImage upload failed: {ex.Message}");
        }

        return null;
    }
}
