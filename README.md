# Searchhy — Google "Circle to Search" for Windows 🔍✨

**Searchhy** brings the authentic Google **Circle to Search** experience to Windows 10 & 11 in C# / .NET 8 (WPF).

Hold your **Left and Right mouse buttons together** on any screen (or press `Ctrl + Shift + L`), and draw a glowing circle, scribble, or lasso loop around anything. On release, Google Lens automatically opens and visual search results load in your default browser.

---

## ✨ Google "Circle to Search" Experience

- **Freeform Circle & Lasso Drawing**: Draw a circle, scribble, or drag a box around any image, text, or UI element.
- **Chromatic Glowing Particle Trail**: Displays Google's glowing neon brush stroke with smooth spline interpolation.
- **Automatic Bounding Box Fitting**: Automatically fits a crisp highlight box and cutout around the circled region.
- **Ultra-Responsive Mouse Chord**: 12ms high-speed hardware polling guarantees instantaneous trigger whenever both buttons are pressed.
- **Zero Friction & Auto-Dismiss**:
  - Releasing your fingers immediately triggers the search.
  - Right-click anywhere or press `ESC` to dismiss instantly.
- **No 403 Errors**: Direct form submission to Google's official visual search endpoint with instant clipboard fallback.
- **Runs Silently in Background**: Starts with Windows and lives in the system tray.
- **Non-Intrusive**: Normal left-clicks, right-clicks, and drag-selections continue to work with zero lag.
- **Multi-Monitor & DPI-Aware**: Captures across all screens and DPI scales (100%, 125%, 150%, 200%).
- **Direct Google Lens Results**: Auto-uploads image in memory to Google Lens in your default browser. No toast notifications, no saving screenshots to disk.

---

## 🏗️ Architecture

The codebase is organized into clean, single-responsibility modules:

- **[`LensConfig.cs`](file:///c:/Users/Jeet%20Mehta/Desktop/Pojects/Searchhy/LensConfig.cs)**: Centralized constants for Google Lens upload endpoints, form field names, fallback URLs, and gesture timings.
- **[`MouseChordDetector.cs`](file:///c:/Users/Jeet%20Mehta/Desktop/Pojects/Searchhy/MouseChordDetector.cs)**: Low-level `WH_MOUSE_LL` Windows mouse hook with an asynchronous chord detection engine.
- **[`ScreenCapture.cs`](file:///c:/Users/Jeet%20Mehta/Desktop/Pojects/Searchhy/ScreenCapture.cs)**: DPI-aware multi-monitor screen capture (`BitBlt`) and in-memory PNG cropping.
- **[`OverlayWindow.xaml`](file:///c:/Users/Jeet%20Mehta/Desktop/Pojects/Searchhy/OverlayWindow.xaml) / [`.cs`](file:///c:/Users/Jeet%20Mehta/Desktop/Pojects/Searchhy/OverlayWindow.xaml.cs)**: Full-desktop overlay with a dimmed background, interactive cutout, live pixel dimensions badge, and ESC cancellation.
- **[`LensSearchLauncher.cs`](file:///c:/Users/Jeet%20Mehta/Desktop/Pojects/Searchhy/LensSearchLauncher.cs)**: Ephemeral localhost HTTP server that loads the PNG into a standard form `<input type="file" name="encoded_image">` via `DataTransfer` and submits directly to Google Lens.
- **[`TrayHost.cs`](file:///c:/Users/Jeet%20Mehta/Desktop/Pojects/Searchhy/TrayHost.cs)**: System tray icon with "Start with Windows" and "Exit Searchhy" options.
- **[`StartupHelper.cs`](file:///c:/Users/Jeet%20Mehta/Desktop/Pojects/Searchhy/StartupHelper.cs)**: Manages automatic launch on Windows boot via `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`.
- **[`Logger.cs`](file:///c:/Users/Jeet%20Mehta/Desktop/Pojects/Searchhy/Logger.cs)**: Thread-safe file logger writing to `%LOCALAPPDATA%\LensChord\log.txt`.

---

## 🚀 How to Build and Run

### Prerequisites
- Windows 10 or Windows 11
- [.NET 8 SDK or higher](https://dotnet.microsoft.com/download)

### Build
Open PowerShell in the project directory:
```powershell
dotnet build
```

### Run
Launch Searchhy in the background:
```powershell
dotnet run
```
Or run the compiled executable directly:
```powershell
.\bin\Debug\net8.0-windows\Searchhy.exe
```

When started, Searchhy will display a tray icon in your taskbar. It stays active in the background ready to trigger whenever you perform the mouse chord.

### Automatic Launch on Windows Startup
- Right-click the **Searchhy** tray icon and toggle **Start with Windows**.
- Alternatively, Searchhy can be placed in your Windows Startup folder (`Win + R` -> `shell:startup`).

---

## ⚙️ How to Change the Google Lens Endpoint

All endpoints and field names are isolated in [LensConfig.cs](file:///c:/Users/Jeet%20Mehta/Desktop/Pojects/Searchhy/LensConfig.cs):

```csharp
public static class LensConfig
{
    // The Google Lens upload endpoint URL (accepts multipart/form-data)
    public const string UploadUrl = "https://lens.google.com/upload";

    // The form field name for the image file
    public const string FileFieldName = "encoded_image";

    // Fallback URL if automated upload cannot proceed
    public const string FallbackUrl = "https://lens.google.com";
}
```

If Google ever modifies its upload endpoint URL or form field name in the future, simply update the strings in `LensConfig.cs` and recompile with `dotnet build`.

---

## 📋 Manual Test Checklist

| # | Test Case | Action | Expected Result | Pass? |
|---|---|---|---|:---:|
| 1 | **Normal Right-Click** | Right-click any file on Desktop or page in browser | Context menu opens immediately without lag | ✅ |
| 2 | **Normal Text Selection** | Click and drag left mouse button over text | Text is highlighted/selected normally without triggering overlay | ✅ |
| 3 | **Mouse Chord Trigger** | Press and hold `Left + Right` mouse buttons together for ~150ms | Screen freezes instantly with dimmed overlay across all monitors | ✅ |
| 4 | **Multi-Monitor Dragging** | Drag the selection box from primary to secondary monitor | Selection marquee expands smoothly across monitor boundaries | ✅ |
| 5 | **DPI Scaling Accuracy** | Test on 125% / 150% scaled display | Cropped region matches the selection box pixel-for-pixel | ✅ |
| 6 | **ESC Cancellation** | Press `ESC` while overlay is visible | Overlay closes immediately; no search is triggered | ✅ |
| 7 | **Small Selection Ignore** | Make a selection smaller than 10×10px and release | Overlay closes cleanly without triggering browser search | ✅ |
| 8 | **Automated Lens Search** | Drag a selection over any image/text and release mouse buttons | Default browser opens directly with Google Lens search results | ✅ |
| 9 | **Single Instance Lock** | Launch `Searchhy.exe` a second time | Second instance exits immediately without duplicate tray icons | ✅ |
| 10 | **Exit Application** | Right-click tray icon -> Click **✕ Exit Searchhy** | Mouse hook unhooks and application closes cleanly | ✅ |

---

## 📝 Logs

Diagnostic logs are saved to:
`%LOCALAPPDATA%\LensChord\log.txt`
