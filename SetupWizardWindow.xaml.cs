using System;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;

namespace Searchhy;

public partial class SetupWizardWindow : Window
{
    private readonly Action _triggerTestOverlayAction;
    private readonly Action _onSetupCompletedAction;
    private readonly AppSettings _settings;

    public SetupWizardWindow(Action triggerTestOverlayAction, Action onSetupCompletedAction)
    {
        InitializeComponent();

        _triggerTestOverlayAction = triggerTestOverlayAction;
        _onSetupCompletedAction = onSetupCompletedAction;
        _settings = AppSettings.Load();

        Loaded += SetupWizardWindow_Loaded;
    }

    private void SetupWizardWindow_Loaded(object sender, RoutedEventArgs e)
    {
        LoadBrandingImages();

        // Load current preferences
        ChkStartWithWindows.IsChecked = _settings.StartWithWindows;
        ChkDesktopShortcut.IsChecked = _settings.CreateDesktopShortcut;
        ChkSystemTrayNotification.IsChecked = _settings.ShowTrayNotificationOnStart;
    }

    private void LoadBrandingImages()
    {
        try
        {
            string projDir = AppDomain.CurrentDomain.BaseDirectory;
            string[] possiblePaths = new[]
            {
                Path.Combine(projDir, "logo.png"),
                @"c:\Users\Jeet Mehta\Desktop\Pojects\Searchhy\logo.png",
                Path.Combine(Directory.GetCurrentDirectory(), "logo.png")
            };

            string? foundLogo = null;
            foreach (var p in possiblePaths)
            {
                if (File.Exists(p))
                {
                    foundLogo = p;
                    break;
                }
            }

            if (foundLogo != null)
            {
                var bi = new BitmapImage();
                bi.BeginInit();
                bi.UriSource = new Uri(foundLogo, UriKind.Absolute);
                bi.CacheOption = BitmapCacheOption.OnLoad;
                bi.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
                bi.EndInit();
                bi.Freeze();

                HeaderLogoImage.Source = bi;
                HeroLogoImage.Source = bi;
            }
        }
        catch (Exception ex)
        {
            Logger.LogWarn($"Could not load branding image: {ex.Message}");
        }
    }

    private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left)
        {
            DragMove();
        }
    }

    private void BtnTryTest_Click(object sender, RoutedEventArgs e)
    {
        Logger.LogInfo("Setup Wizard: User triggered live practice test.");
        _triggerTestOverlayAction();
    }

    private void BtnFinishSetup_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            _settings.HasCompletedSetup = true;
            _settings.StartWithWindows = ChkStartWithWindows.IsChecked == true;
            _settings.CreateDesktopShortcut = ChkDesktopShortcut.IsChecked == true;
            _settings.ShowTrayNotificationOnStart = ChkSystemTrayNotification.IsChecked == true;
            _settings.Save();

            // Apply Start With Windows
            if (_settings.StartWithWindows)
            {
                StartupHelper.EnableAutoStart();
            }
            else
            {
                StartupHelper.DisableAutoStart();
            }

            // Apply Desktop Shortcut
            if (_settings.CreateDesktopShortcut)
            {
                CreateDesktopShortcut();
            }

            Logger.LogInfo("Setup Wizard completed by user.");
            Close();
            _onSetupCompletedAction();
        }
        catch (Exception ex)
        {
            Logger.LogError("Error finishing setup wizard", ex);
            Close();
            _onSetupCompletedAction();
        }
    }

    private static void CreateDesktopShortcut()
    {
        try
        {
            string projDir = AppDomain.CurrentDomain.BaseDirectory;
            string icoPath = Path.Combine(projDir, "app.ico");
            if (!File.Exists(icoPath))
            {
                icoPath = @"c:\Users\Jeet Mehta\Desktop\Pojects\Searchhy\app.ico";
            }

            string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            string lnkPath = Path.Combine(desktopPath, "Searchhy.lnk");

            var wshType = Type.GetTypeFromProgID("WScript.Shell");
            if (wshType != null)
            {
                dynamic wsh = Activator.CreateInstance(wshType)!;
                dynamic shortcut = wsh.CreateShortcut(lnkPath);
                shortcut.TargetPath = Path.Combine(projDir, "Searchhy.exe");
                shortcut.WorkingDirectory = projDir;
                if (File.Exists(icoPath))
                {
                    shortcut.IconLocation = $"{icoPath},0";
                }
                shortcut.Description = "Searchhy - Google Circle to Search for Windows";
                shortcut.Save();
            }

            IconBuilder.RefreshWindowsDesktop();
        }
        catch { }
    }

    private void BtnClose_Click(object sender, RoutedEventArgs e)
    {
        Close();
        _onSetupCompletedAction();
    }
}
