using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Windows.Forms;

namespace Searchhy;

/// <summary>
/// Manages the system tray icon, context menu, and background lifecycle.
/// </summary>
public sealed class TrayHost : IDisposable
{
    private NotifyIcon? _notifyIcon;
    private readonly Action _onOpenDashboard;
    private readonly Action _onExitRequested;

    public TrayHost(Action onOpenDashboard, Action onExitRequested)
    {
        _onOpenDashboard = onOpenDashboard;
        _onExitRequested = onExitRequested;
        InitializeTray();
    }

    private void InitializeTray()
    {
        var contextMenu = new ContextMenuStrip();

        var dashboardItem = new ToolStripMenuItem("📊  Open Dashboard & Diagnostics", null, (s, e) => _onOpenDashboard())
        {
            Font = new Font("Segoe UI", 9f, FontStyle.Bold)
        };

        var autoStartItem = new ToolStripMenuItem("Start with Windows", null, (s, e) =>
        {
            if (StartupHelper.IsAutoStartEnabled())
            {
                StartupHelper.DisableAutoStart();
            }
            else
            {
                StartupHelper.EnableAutoStart();
            }
        })
        {
            CheckOnClick = true,
            Checked = StartupHelper.IsAutoStartEnabled(),
            Font = new Font("Segoe UI", 9f, FontStyle.Regular)
        };

        var exitItem = new ToolStripMenuItem("✕  Exit Searchhy", null, (s, e) => _onExitRequested())
        {
            Font = new Font("Segoe UI", 9f, FontStyle.Regular)
        };

        contextMenu.Items.Add(dashboardItem);
        contextMenu.Items.Add(new ToolStripSeparator());
        contextMenu.Items.Add(autoStartItem);
        contextMenu.Items.Add(new ToolStripSeparator());
        contextMenu.Items.Add(exitItem);

        Icon trayIcon;
        string icoPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "app.ico");
        if (File.Exists(icoPath))
        {
            trayIcon = new Icon(icoPath, 32, 32);
        }
        else
        {
            trayIcon = CreateModernTrayIcon();
        }

        _notifyIcon = new NotifyIcon
        {
            Icon = trayIcon,
            Text = "Searchhy — Circle to Search",
            Visible = true,
            ContextMenuStrip = contextMenu
        };

        _notifyIcon.DoubleClick += (s, e) => _onOpenDashboard();

        Logger.LogInfo("System tray icon initialized.");
    }

    public void ShowReadyNotification()
    {
        ShowNotification("Searchhy Ready", "Hold Left + Right click or press Ctrl + Shift + L to Circle to Search.");
    }

    public void ShowNotification(string title, string message)
    {
        try
        {
            _notifyIcon?.ShowBalloonTip(3000, title, message, ToolTipIcon.Info);
        }
        catch { }
    }

    [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyIcon(IntPtr hIcon);

    /// <summary>
    /// Generates a clean, modern vector search icon for the system tray.
    /// </summary>
    private static Icon CreateModernTrayIcon()
    {
        int size = 32;
        using var bitmap = new Bitmap(size, size);
        using (var g = Graphics.FromImage(bitmap))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.Clear(Color.Transparent);

            using var bgBrush = new SolidBrush(Color.FromArgb(230, 26, 115, 232));
            g.FillEllipse(bgBrush, 2, 2, 28, 28);

            using var ringPen = new Pen(Color.White, 2.5f);
            g.DrawEllipse(ringPen, 8, 8, 11, 11);

            using var handlePen = new Pen(Color.White, 2.5f) { StartCap = LineCap.Round, EndCap = LineCap.Round };
            g.DrawLine(handlePen, 16.5f, 16.5f, 22.5f, 22.5f);
        }

        IntPtr hIcon = bitmap.GetHicon();
        var icon = (Icon)Icon.FromHandle(hIcon).Clone();
        DestroyIcon(hIcon);
        return icon;
    }

    public void Dispose()
    {
        if (_notifyIcon != null)
        {
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
            _notifyIcon = null;
        }
    }
}
