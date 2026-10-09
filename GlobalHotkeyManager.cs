using System;
using System.Runtime.InteropServices;
using System.Windows.Interop;

namespace Searchhy;

/// <summary>
/// Manages system-wide global hotkeys using Win32 RegisterHotKey attached to an anchor window handle.
/// Holds strong delegate references to prevent GC collection crashes.
/// </summary>
public sealed class GlobalHotkeyManager : IDisposable
{
    private const int WM_HOTKEY = 0x0312;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    public const uint MOD_ALT = 0x0001;
    public const uint MOD_CONTROL = 0x0002;
    public const uint MOD_SHIFT = 0x0004;
    public const uint MOD_WIN = 0x0008;
    public const uint MOD_NOREPEAT = 0x4000;

    private IntPtr _windowHandle = IntPtr.Zero;
    private HwndSource? _hwndSource;
    private readonly HwndSourceHook _hwndHook;
    private readonly int _triggerHotKeyId = 9001;
    private readonly int _toggleHotKeyId = 9002;

    public event Action? TriggerRequested;
    public event Action? TogglePauseRequested;

    public GlobalHotkeyManager()
    {
        // Strong reference to prevent GC collection of native hook
        _hwndHook = HwndHook;
    }

    public void Initialize(IntPtr windowHandle)
    {
        if (windowHandle == IntPtr.Zero)
        {
            Logger.LogWarn("GlobalHotkeyManager: windowHandle is IntPtr.Zero, skipping hotkey registration.");
            return;
        }

        try
        {
            _windowHandle = windowHandle;
            _hwndSource = HwndSource.FromHwnd(_windowHandle);
            _hwndSource?.AddHook(_hwndHook);

            // Hotkey 1: Ctrl + Shift + L -> Trigger Lens Selection directly
            const uint VK_L = 0x4C;
            bool registeredTrigger = RegisterHotKey(_windowHandle, _triggerHotKeyId, MOD_CONTROL | MOD_SHIFT | MOD_NOREPEAT, VK_L);
            if (registeredTrigger)
            {
                Logger.LogInfo("Global hotkey registered: Ctrl + Shift + L (Trigger Lens Search)");
            }
            else
            {
                Logger.LogWarn("Failed to register global hotkey Ctrl + Shift + L");
            }

            // Hotkey 2: Ctrl + Shift + P -> Pause / Resume Mouse Chord Detection
            const uint VK_P = 0x50;
            bool registeredToggle = RegisterHotKey(_windowHandle, _toggleHotKeyId, MOD_CONTROL | MOD_SHIFT | MOD_NOREPEAT, VK_P);
            if (registeredToggle)
            {
                Logger.LogInfo("Global hotkey registered: Ctrl + Shift + P (Toggle Pause/Resume)");
            }
        }
        catch (Exception ex)
        {
            Logger.LogError("GlobalHotkeyManager initialization error", ex);
        }
    }

    private IntPtr HwndHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_HOTKEY)
        {
            int id = wParam.ToInt32();
            if (id == _triggerHotKeyId)
            {
                TriggerRequested?.Invoke();
                handled = true;
            }
            else if (id == _toggleHotKeyId)
            {
                TogglePauseRequested?.Invoke();
                handled = true;
            }
        }
        return IntPtr.Zero;
    }

    public void Dispose()
    {
        if (_windowHandle != IntPtr.Zero)
        {
            UnregisterHotKey(_windowHandle, _triggerHotKeyId);
            UnregisterHotKey(_windowHandle, _toggleHotKeyId);
            _hwndSource?.RemoveHook(_hwndHook);
            _hwndSource = null;
            _windowHandle = IntPtr.Zero;
        }
    }
}
