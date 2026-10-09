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

    private TrayHost? _trayHost;
    private MouseChordDetector? _chordDetector;
    private GlobalHotkeyManager? _hotkeyManager;
    private DashboardWindow? _dashboardWindow;
    private OverlayWindow? _currentOverlay;
    private readonly object _overlayLock = new();
    private bool _isPaused;

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool GetCursorPos(out MouseChordDetector.POINT lpPoint);

    public App()
    {
        ShutdownMode = ShutdownMode.OnExplicitShutdown;
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Ensure single-instance execution safely
        bool isNewInstance = false;
        try
        {
            _singleInstanceMutex = new Mutex(true, MutexName, out isNewInstance);
        }
        catch (AbandonedMutexException)
        {
            isNewInstance = true;
            Logger.LogInfo("Acquired ownership of abandoned single-instance mutex.");
        }
        catch (Exception ex)
        {
            Logger.LogWarn($"Mutex initialization warning: {ex.Message}");
            isNewInstance = true;
        }

        // Verify if another active process is actually running
        int currentPid = Process.GetCurrentProcess().Id;
        var otherInstances = Process.GetProcessesByName("Searchhy")
            .Where(p => p.Id != currentPid)
            .ToArray();

        if (otherInstances.Length > 0 && !isNewInstance)
        {
            Logger.LogWarn($"Another instance of Searchhy (PID: {otherInstances[0].Id}) is already running. Exiting.");
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

    private void OpenDashboard()
    {
        Dispatcher.Invoke(() =>
        {
            if (_dashboardWindow == null)
            {
                _dashboardWindow = new DashboardWindow(TriggerCircleToSearchManual, ShutdownApp);
            }
            _dashboardWindow.Show();
            _dashboardWindow.WindowState = WindowState.Normal;
            _dashboardWindow.Activate();
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

            if (_singleInstanceMutex != null)
            {
                _singleInstanceMutex.ReleaseMutex();
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
