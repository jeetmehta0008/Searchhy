using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Windows;

namespace Searchhy;

/// <summary>
/// Main application coordinator for Searchhy.
/// Launches the Dashboard UI window and manages background gesture hooks.
/// </summary>
public partial class App : System.Windows.Application
{
    private const string MutexName = "Searchhy_SingleInstance_Mutex";
    private static Mutex? _singleInstanceMutex;

    private const int HWND_BROADCAST = 0xFFFF;
    private const int SW_RESTORE = 9;
    private const int SW_SHOW = 5;

    [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true, CharSet = System.Runtime.InteropServices.CharSet.Auto)]
    private static extern uint RegisterWindowMessage(string lpString);

    [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)]
    private static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool GetCursorPos(out MouseChordDetector.POINT lpPoint);

    private uint _wmShowSearchhy;
    private System.Windows.Interop.HwndSource? _hwndSource;
    private TrayHost? _trayHost;
    private MouseChordDetector? _chordDetector;
    private GlobalHotkeyManager? _hotkeyManager;
    private DashboardWindow? _dashboardWindow;
    private OverlayWindow? _currentOverlay;
    private readonly object _overlayLock = new();
    private bool _isPaused;
    private bool _isPrimaryInstance;

    public App()
    {
        ShutdownMode = ShutdownMode.OnExplicitShutdown;
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _wmShowSearchhy = RegisterWindowMessage("SEARCHHY_SHOW_ME_MSG");

        // Ensure single-instance execution safely
        bool isNewInstance = false;
        try
        {
            _singleInstanceMutex = new Mutex(true, MutexName, out isNewInstance);
            _isPrimaryInstance = isNewInstance;
        }
        catch (AbandonedMutexException)
        {
            isNewInstance = true;
            _isPrimaryInstance = true;
            Logger.LogInfo("Acquired ownership of abandoned single-instance mutex.");
        }
        catch (Exception ex)
        {
            Logger.LogWarn($"Mutex initialization warning: {ex.Message}");
            isNewInstance = true;
            _isPrimaryInstance = true;
        }

        // Verify if another active process is actually running
        int currentPid = Process.GetCurrentProcess().Id;
        var otherInstances = Process.GetProcessesByName("Searchhy")
            .Where(p => p.Id != currentPid)
            .ToArray();

        if (otherInstances.Length > 0 && !isNewInstance)
        {
            Logger.LogInfo($"Searchhy is already running (PID: {otherInstances[0].Id}). Signaling existing instance to open.");
            PostMessage((IntPtr)HWND_BROADCAST, _wmShowSearchhy, IntPtr.Zero, IntPtr.Zero);

            try
            {
                foreach (var proc in otherInstances)
                {
                    if (proc.MainWindowHandle != IntPtr.Zero)
                    {
                        ShowWindow(proc.MainWindowHandle, SW_RESTORE);
                        SetForegroundWindow(proc.MainWindowHandle);
                    }
                }
            }
            catch { }

            Shutdown();
            return;
        }

        // Global exception logging
        AppDomain.CurrentDomain.UnhandledException += (s, args) =>
        {
            Logger.LogError("Unhandled AppDomain exception", args.ExceptionObject as Exception);
        };

        DispatcherUnhandledException += (s, args) =>
        {
            Logger.LogError("Unhandled Dispatcher exception", args.Exception);
            args.Handled = true;
        };

        try
        {
            Logger.LogInfo("=== Searchhy Starting ===");

            var settings = AppSettings.Load();

            // Automatically ensure Searchhy starts with Windows if enabled
            if (settings.StartWithWindows && !StartupHelper.IsAutoStartEnabled())
            {
                StartupHelper.EnableAutoStart();
                Logger.LogInfo("Searchhy registered to start automatically with Windows.");
            }

            // Initialize System Tray
            _trayHost = new TrayHost(OpenDashboard, ShutdownApp);

            // Initialize Diagnostic Dashboard Window
            _dashboardWindow = new DashboardWindow(TriggerCircleToSearchManual, ShutdownApp);

            // Ensure window handle exists immediately without having to show the window if setup wizard is running
            IntPtr hostHandle = new System.Windows.Interop.WindowInteropHelper(_dashboardWindow).EnsureHandle();

            // Hook window messages to restore Dashboard on second-instance launch
            _hwndSource = System.Windows.Interop.HwndSource.FromHwnd(hostHandle);
            _hwndSource?.AddHook(WndProc);

            if (!settings.HasCompletedSetup)
            {
                // First launch: show Setup & Onboarding Wizard
                var wizard = new SetupWizardWindow(TriggerCircleToSearchManual, () =>
                {
                    _dashboardWindow.Show();
                    if (settings.ShowTrayNotificationOnStart)
                    {
                        _trayHost.ShowReadyNotification();
                    }
                });
                wizard.Show();
            }
            else
            {
                _dashboardWindow.Show();
                if (settings.ShowTrayNotificationOnStart)
                {
                    _trayHost.ShowReadyNotification();
                }
            }

            // Initialize Gesture Engine
            _chordDetector = new MouseChordDetector();
            _chordDetector.ChordActivated += OnChordActivated;
            _chordDetector.Start();

            // Initialize Global Hotkeys (Ctrl+Shift+L to trigger, Ctrl+Shift+P to pause/resume)
            _hotkeyManager = new GlobalHotkeyManager();
            _hotkeyManager.TriggerRequested += OnTriggerHotkey;
            _hotkeyManager.TogglePauseRequested += OnTogglePauseHotkey;
            _hotkeyManager.Initialize(hostHandle);

            Logger.LogInfo("Searchhy initialized and listening for chords.");
        }
        catch (Exception ex)
        {
            Logger.LogError("Failed during application startup", ex);
            System.Windows.MessageBox.Show(
                $"Failed to start Searchhy: {ex.Message}\n\nCheck %LOCALAPPDATA%\\LensChord\\log.txt for details.",
                "Searchhy Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown();
        }
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == _wmShowSearchhy && _wmShowSearchhy != 0)
        {
            Logger.LogInfo("Received broadcast signal to show Searchhy Dashboard.");
            OpenDashboard();
            handled = true;
        }
        return IntPtr.Zero;
    }

    private void OpenDashboard()
    {
        Dispatcher.Invoke(() =>
        {
            if (_dashboardWindow == null)
            {
                _dashboardWindow = new DashboardWindow(TriggerCircleToSearchManual, ShutdownApp);
            }

            _dashboardWindow.Show();
            if (_dashboardWindow.WindowState == WindowState.Minimized)
            {
                _dashboardWindow.WindowState = WindowState.Normal;
            }
            _dashboardWindow.Activate();
            _dashboardWindow.Topmost = true;
            _dashboardWindow.Topmost = false;
            _dashboardWindow.Focus();

            try
            {
                IntPtr hwnd = new System.Windows.Interop.WindowInteropHelper(_dashboardWindow).Handle;
                if (hwnd != IntPtr.Zero)
                {
                    ShowWindow(hwnd, SW_RESTORE);
                    SetForegroundWindow(hwnd);
                }
            }
            catch { }
        });
    }

    private void TriggerCircleToSearchManual()
    {
        GetCursorPos(out var pt);
        OnChordActivated(pt);
    }

    private void OnChordActivated(MouseChordDetector.POINT startPt)
    {
        Dispatcher.BeginInvoke(new Action(() =>
        {
            lock (_overlayLock)
            {
                if (_currentOverlay != null || _chordDetector == null) return;

                try
                {
                    _chordDetector.IsOverlayActive = true;

                    // Capture virtual screen before displaying overlay
                    var (screenBitmap, bounds) = ScreenCapture.CaptureVirtualScreen();

                    _currentOverlay = new OverlayWindow(screenBitmap, bounds, startPt);
                    _currentOverlay.OverlayClosed += () =>
                    {
                        lock (_overlayLock)
                        {
                            _currentOverlay = null;
                            if (_chordDetector != null)
                            {
                                _chordDetector.IsOverlayActive = false;
                            }
                        }
                    };

                    _currentOverlay.Show();
                    Logger.LogInfo("Circle to Search overlay opened.");
                }
                catch (Exception ex)
                {
                    Logger.LogError("Failed to activate Circle to Search overlay", ex);
                    if (_chordDetector != null)
                    {
                        _chordDetector.IsOverlayActive = false;
                    }
                }
            }
        }));
    }

    private void OnTriggerHotkey()
    {
        if (_isPaused) return;

        GetCursorPos(out var pt);
        OnChordActivated(pt);
    }

    private void OnTogglePauseHotkey()
    {
        _isPaused = !_isPaused;
        if (_isPaused)
        {
            _chordDetector?.Stop();
            _trayHost?.ShowNotification("Searchhy Paused", "Mouse chord detection is paused. Press Ctrl + Shift + P to resume.");
            Logger.LogInfo("Searchhy paused via hotkey.");
        }
        else
        {
            _chordDetector?.Start();
            _trayHost?.ShowNotification("Searchhy Resumed", "Mouse chord detection is active. Hold Left + Right click to search.");
            Logger.LogInfo("Searchhy resumed via hotkey.");
        }
    }

    private void ShutdownApp()
    {
        Logger.LogInfo("Shutdown requested by user.");
        Shutdown();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        Logger.LogInfo("Cleaning up resources before exit...");

        try
        {
            _hotkeyManager?.Dispose();
            _hotkeyManager = null;

            _chordDetector?.Dispose();
            _chordDetector = null;

            _trayHost?.Dispose();
            _trayHost = null;

            if (_singleInstanceMutex != null && _isPrimaryInstance)
            {
                try
                {
                    _singleInstanceMutex.ReleaseMutex();
                }
                catch { }
                _singleInstanceMutex.Dispose();
                _singleInstanceMutex = null;
            }
        }
        catch (Exception ex)
        {
            Logger.LogError("Error during shutdown cleanup", ex);
        }

        Logger.LogInfo("=== Searchhy Terminated Cleanly ===");
        base.OnExit(e);
    }
}
