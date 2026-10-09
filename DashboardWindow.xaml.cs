using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Microsoft.Win32;

namespace Searchhy;

/// <summary>
/// Interactive Diagnostic & Control Center Window.
/// Shows live hardware button states, real-time logs, manual test triggers,
/// and desktop icon customization controls.
/// </summary>
public partial class DashboardWindow : Window
{
    private readonly DispatcherTimer _monitorTimer;
    private readonly Action _triggerOverlayAction;
    private readonly Action _shutdownAction;

    public DashboardWindow(Action triggerOverlayAction, Action shutdownAction)
    {
        InitializeComponent();

        _triggerOverlayAction = triggerOverlayAction;
        _shutdownAction = shutdownAction;

        // Monitor timer for live hardware states (every 30ms)
        _monitorTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(30)
        };
        _monitorTimer.Tick += MonitorTimer_Tick;
        _monitorTimer.Start();

        Loaded += DashboardWindow_Loaded;
        Closing += DashboardWindow_Closing;
    }

    private void DashboardWindow_Loaded(object sender, RoutedEventArgs e)
    {
        RefreshLogs();
        LoadIconPreview();
    }

    private void DashboardWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        e.Cancel = true;
        this.Hide();
    }

    private void LoadIconPreview()
    {
        try
        {
            string projDir = AppDomain.CurrentDomain.BaseDirectory;
            string logoPath = Path.Combine(projDir, "logo.png");
            if (!File.Exists(logoPath))
            {
                logoPath = @"c:\Users\Jeet Mehta\Desktop\Pojects\Searchhy\logo.png";
            }

            if (File.Exists(logoPath))
            {
                var bi = new BitmapImage();
                bi.BeginInit();
                bi.UriSource = new Uri(logoPath, UriKind.Absolute);
                bi.CacheOption = BitmapCacheOption.OnLoad;
                bi.EndInit();
                IconPreviewImage.Source = bi;
            }
        }
        catch { }
    }

    private void MonitorTimer_Tick(object? sender, EventArgs e)
    {
        bool leftDown = (MouseChordDetector.GetAsyncKeyState(MouseChordDetector.VK_LBUTTON) & 0x8000) != 0;
        bool rightDown = (MouseChordDetector.GetAsyncKeyState(MouseChordDetector.VK_RBUTTON) & 0x8000) != 0;

        // Update Left Button Indicator
        if (leftDown)
        {
            LeftIndicatorDot.Fill = new SolidColorBrush(System.Windows.Media.Color.FromRgb(34, 197, 94)); // Green
            LeftIndicatorText.Text = "PRESSED (Down)";
            LeftIndicatorText.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(34, 197, 94));
        }
        else
        {
            LeftIndicatorDot.Fill = new SolidColorBrush(System.Windows.Media.Color.FromRgb(239, 68, 68)); // Red
            LeftIndicatorText.Text = "RELEASED (Up)";
            LeftIndicatorText.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(239, 68, 68));
        }

        // Update Right Button Indicator
        if (rightDown)
        {
            RightIndicatorDot.Fill = new SolidColorBrush(System.Windows.Media.Color.FromRgb(34, 197, 94)); // Green
            RightIndicatorText.Text = "PRESSED (Down)";
            RightIndicatorText.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(34, 197, 94));
        }
        else
        {
            RightIndicatorDot.Fill = new SolidColorBrush(System.Windows.Media.Color.FromRgb(239, 68, 68)); // Red
            RightIndicatorText.Text = "RELEASED (Up)";
            RightIndicatorText.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(239, 68, 68));
        }

        // Update Chord Indicator
        if (leftDown && rightDown)
        {
            ChordIndicatorDot.Fill = new SolidColorBrush(System.Windows.Media.Color.FromRgb(56, 189, 248)); // Cyan
            ChordIndicatorText.Text = "CHORD DETECTED! (Both Down)";
            ChordIndicatorText.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(56, 189, 248));
        }
        else
        {
            ChordIndicatorDot.Fill = new SolidColorBrush(System.Windows.Media.Color.FromRgb(100, 116, 139));
            ChordIndicatorText.Text = "LISTENING (Ready)";
            ChordIndicatorText.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(148, 163, 184));
        }

        RefreshLogs();
    }

    private void RefreshLogs()
    {
        try
        {
            string logPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LensChord", "log.txt");
            if (File.Exists(logPath))
            {
                using var fs = new FileStream(logPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                using var reader = new StreamReader(fs);
                string text = reader.ReadToEnd();
                if (LogTextBox.Text.Length != text.Length)
                {
                    LogTextBox.Text = text;
                    LogScrollViewer.ScrollToEnd();
                }
            }
        }
        catch { }
    }

    private void BtnChangeIcon_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "Image Files (*.png;*.jpg;*.jpeg;*.ico)|*.png;*.jpg;*.jpeg;*.ico|All Files (*.*)|*.*",
            Title = "Select New Searchhy App Icon"
        };

        if (dlg.ShowDialog() == true)
        {
            try
            {
                string targetLogoPng = @"c:\Users\Jeet Mehta\Desktop\Pojects\Searchhy\logo.png";
                File.Copy(dlg.FileName, targetLogoPng, true);
                ApplyDesktopIcon(targetLogoPng);
                LoadIconPreview();
                IconStatusText.Text = "✓ Custom icon updated successfully!";
                Logger.LogInfo($"User selected new custom icon: {dlg.FileName}");
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to update custom icon", ex);
                System.Windows.MessageBox.Show($"Failed to apply icon: {ex.Message}", "Icon Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private void BtnRefreshIcon_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            string targetLogoPng = @"c:\Users\Jeet Mehta\Desktop\Pojects\Searchhy\logo.png";
            ApplyDesktopIcon(targetLogoPng);
            LoadIconPreview();
            IconStatusText.Text = "✓ Desktop shortcut icon refreshed and cache flushed!";
            Logger.LogInfo("Desktop icon refreshed via dashboard.");
        }
        catch (Exception ex)
        {
            Logger.LogError("Failed to refresh desktop icon", ex);
        }
    }

    private void ApplyDesktopIcon(string sourceImage)
    {
        string projDir = @"c:\Users\Jeet Mehta\Desktop\Pojects\Searchhy";
        string versionedIcoName = $"app_v{DateTime.UtcNow.Ticks}.ico";
        string targetIcoPath = Path.Combine(projDir, versionedIcoName);

        // Convert PNG to true multi-resolution ICO
        IconBuilder.ConvertPngToIco(sourceImage, targetIcoPath);

        // Also copy as standard app.ico
        string defaultIcoPath = Path.Combine(projDir, "app.ico");
        File.Copy(targetIcoPath, defaultIcoPath, true);

        // Recreate Desktop Shortcut pointing to the fresh versioned ICO (bypasses Windows Explorer memory cache)
        string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
        string lnkPath = Path.Combine(desktopPath, "Searchhy.lnk");

        var wshType = Type.GetTypeFromProgID("WScript.Shell");
        if (wshType != null)
        {
            dynamic wsh = Activator.CreateInstance(wshType)!;
            dynamic shortcut = wsh.CreateShortcut(lnkPath);
            shortcut.TargetPath = Path.Combine(projDir, @"bin\Release\net8.0-windows\Searchhy.exe");
            shortcut.WorkingDirectory = Path.Combine(projDir, @"bin\Release\net8.0-windows");
            shortcut.IconLocation = $"{targetIcoPath},0";
            shortcut.Description = "Searchhy - Circle to Search for Windows";
            shortcut.Save();
        }

        // Broadcast shell cache update
        IconBuilder.RefreshWindowsDesktop();
    }

    private void BtnOpenSetupWizard_Click(object sender, RoutedEventArgs e)
    {
        var wizard = new SetupWizardWindow(_triggerOverlayAction, () => { });
        wizard.Owner = this;
        wizard.ShowDialog();
    }

    private void BtnTestOverlay_Click(object sender, RoutedEventArgs e)
    {
        Logger.LogInfo("Manual Test: Launching Circle to Search overlay via Dashboard button.");
        _triggerOverlayAction();
    }

    private void BtnTestUpload_Click(object sender, RoutedEventArgs e)
    {
        Logger.LogInfo("Manual Test: Testing Searchhy visual search upload with a generated test pattern.");

        try
        {
            using var bmp = new Bitmap(400, 200);
            using (var g = Graphics.FromImage(bmp))
            {
                g.Clear(System.Drawing.Color.FromArgb(24, 27, 36));
                using var brush = new SolidBrush(System.Drawing.Color.FromArgb(66, 133, 244));
                g.FillEllipse(brush, 40, 40, 120, 120);
                using var font = new Font("Segoe UI", 16f, System.Drawing.FontStyle.Bold);
                g.DrawString("Searchhy Test", font, System.Drawing.Brushes.White, 180, 80);
            }

            using var ms = new MemoryStream();
            bmp.Save(ms, ImageFormat.Png);
            byte[] bytes = ms.ToArray();

            _ = LensSearchLauncher.LaunchSearchAsync(bytes);
        }
        catch (Exception ex)
        {
            Logger.LogError("Test upload failed", ex);
        }
    }

    private void BtnClearLog_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            string logPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LensChord", "log.txt");
            if (File.Exists(logPath))
            {
                File.WriteAllText(logPath, "");
            }
            LogTextBox.Text = "";
        }
        catch { }
    }

    private void BtnMinimizeToTray_Click(object sender, RoutedEventArgs e)
    {
        this.Hide();
    }

    private void BtnExit_Click(object sender, RoutedEventArgs e)
    {
        _shutdownAction();
    }
}
