# Searchhy 🔍✨
### Google "Circle to Search" — Built Natively for Windows PC

[![Release](https://img.shields.io/github/v/release/jeetmehta0008/Searchhy?color=38bdf8&label=Release&style=flat-square)](https://github.com/jeetmehta0008/Searchhy/releases)
[![Platform](https://img.shields.io/badge/Platform-Windows%2010%20%7C%2011-0078D6?style=flat-square&logo=windows)](https://github.com/jeetmehta0008/Searchhy)
[![.NET](https://img.shields.io/badge/.NET-8.0%20(WPF)-512BD4?style=flat-square&logo=dotnet)](https://dotnet.microsoft.com/)
[![License](https://img.shields.io/badge/License-MIT-emerald?style=flat-square)](LICENSE)

<p align="center">
  <img src="logo.png" alt="Searchhy Logo" width="160" style="border-radius: 32px;" />
</p>

<p align="center">
  <b>Searchhy</b> brings the authentic Google <b>Circle to Search</b> experience to Windows 10 & 11 in high-performance C# / .NET 8 (WPF).<br/>
  Circle, scribble, or lasso anything on your screen — and instantly get Google Lens visual search results in your default browser.
</p>

---

## ⚡ Quick Download & Install

👉 **[Download Latest Standalone Release (Searchhy-v1.0-Windows-x64.zip)](https://github.com/jeetmehta0008/Searchhy/releases/latest)**

1. Download and extract the `.zip` archive.
2. Double-click `Searchhy.exe` to start.
3. The interactive **Setup & Onboarding Wizard** will appear to guide you through gestures and settings!

---

## ✨ Key Features

- **🖱️ Mouse Chord Detection (Default)**: Hold `Left + Right` mouse buttons together (~30ms) anywhere on your screen to freeze and circle.
- **⌨️ Global Keyboard Shortcut**: Press `Ctrl + Shift + L` anytime to trigger Circle to Search immediately.
- **🎯 Smart Shape Snapping**: Draw a circle or lasso around any logo, image, math equation, or text — Searchhy auto-detects and snaps the bounding box to fit the object.
- **🌈 Chromatic Google Lens Trail**: Ultra-smooth 144Hz multi-color glowing particle trail that mirrors the native Google Circle to Search aesthetic.
- **🚀 One-Click Google Lens Integration**: Directly submits the cropped image in-memory to Google Lens with zero file footprint.
- **📋 Instant Clipboard Copy**: One-click copy for cropped areas without saving to disk.
- **🖥️ Multi-Monitor & DPI-Aware**: Seamless operation across multi-monitor setups and dynamic scaling (100%, 125%, 150%, 200%).
- **🛡️ Silent System Tray Integration**: Minimal resource usage (<15MB RAM), starts with Windows, and lives quietly in the taskbar tray.

---

## 🎮 How to Use

| Action | Shortcut / Gesture |
|---|---|
| **Trigger Circle to Search** | Hold **Left + Right Mouse Buttons** together OR press **`Ctrl + Shift + L`** |
| **Draw Circle / Selection** | Drag mouse around any object, image, or text |
| **Search on Google Lens** | Click **Search with Google Lens** OR press **`Enter`** |
| **Copy Image to Clipboard** | Click **📋 Copy** |
| **Cancel / Dismiss** | Press **`ESC`** or Right-Click anywhere |
| **Pause / Resume Hotkey** | Press **`Ctrl + Shift + P`** |

---

## 🛠️ Tech Stack & Architecture

- **Language & Framework**: C# 12, .NET 8.0 Windows (WPF)
- **Screen Capture Engine**: Multi-monitor native Win32 `BitBlt` & GDI+ interop
- **Input System**: Asynchronous low-level `WH_MOUSE_LL` hook + Win32 `RegisterHotKey`
- **Vision Pipeline**: Direct multipart in-memory payload pipeline to Google Lens endpoints

```
Searchhy/
├── App.xaml / App.xaml.cs               # Application Lifecycle & Single-Instance Mutex
├── SetupWizardWindow.xaml / .cs         # Modern Onboarding & Setup UI
├── DashboardWindow.xaml / .cs           # Live Diagnostic & Hardware Chord Monitor
├── OverlayWindow.xaml / .cs             # Fullscreen Chromatic Drawing & Snapping Overlay
├── MouseChordDetector.cs                # Win32 Low-Level Mouse Hook Engine
├── GlobalHotkeyManager.cs               # Global Hotkey Registrar (Ctrl+Shift+L / P)
├── ScreenCapture.cs                     # High-DPI Multi-Monitor Screen Grabber
├── LensSearchLauncher.cs                # Zero-Footprint Google Lens Launcher
└── IconBuilder.cs                       # Dynamic Desktop & Tray Icon Manager
```

---

## 🧑‍💻 Building from Source

```powershell
# Clone repository
git clone https://github.com/jeetmehta0008/Searchhy.git
cd Searchhy

# Build solution in Release mode
dotnet build -c Release

# Run
dotnet run -c Release
```

---

## 📄 License

This project is licensed under the [MIT License](LICENSE).

Developed with ❤️ by **[Jeet Mehta](https://github.com/jeetmehta0008)**.
