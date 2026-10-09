using System;
using System.Collections.Generic;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;

namespace Searchhy;

/// <summary>
/// Google "Circle to Search" overlay with freeform circle drawing,
/// intelligent smart shape/icon snapping, interactive croppable handles, and instant Google Lens search.
/// </summary>
public partial class OverlayWindow : Window
{
    private const int HWND_TOPMOST = -1;
    private const uint SWP_SHOWWINDOW = 0x0040;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

    [DllImport("user32.dll")]
    public static extern short GetAsyncKeyState(int vKey);

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out POINT lpPoint);

    [StructLayout(LayoutKind.Sequential)]
    public struct POINT
    {
        public int X;
        public int Y;
    }

    public const int VK_LBUTTON = 0x01;
    public const int VK_RBUTTON = 0x02;

    private enum DragMode { None, DrawingCircle, MovingBox, ResizeTopLeft, ResizeTopRight, ResizeBottomLeft, ResizeBottomRight }
    private DragMode _currentDragMode = DragMode.None;

    private readonly Bitmap _capturedScreenBitmap;
    private readonly ScreenCapture.VirtualScreenBounds _virtualBounds;
    private readonly List<System.Windows.Point> _trailPoints = new();

    private double _dpiScaleX = 1.0;
    private double _dpiScaleY = 1.0;

    private double _cropLeft, _cropTop, _cropWidth, _cropHeight;
    private System.Windows.Point _lastMousePos;
    private bool _hasDrawnCircle;
    private int _isFinishedInt;

    public event Action? OverlayClosed;

    public OverlayWindow(Bitmap screenBitmap, ScreenCapture.VirtualScreenBounds bounds, MouseChordDetector.POINT startPhysicalPt)
    {
        InitializeComponent();

        _capturedScreenBitmap = screenBitmap;
        _virtualBounds = bounds;

        var bitmapSource = ScreenCapture.ToBitmapSource(_capturedScreenBitmap);
        ScreenImage.Source = bitmapSource;

        Loaded += OverlayWindow_Loaded;
    }

    private void OverlayWindow_Loaded(object sender, RoutedEventArgs e)
    {
        // 1. Calculate DPI Scaling Factor
        var dpi = VisualTreeHelper.GetDpi(this);
        _dpiScaleX = dpi.DpiScaleX > 0 ? dpi.DpiScaleX : 1.0;
        _dpiScaleY = dpi.DpiScaleY > 0 ? dpi.DpiScaleY : 1.0;

        // 2. Set Exact 1:1 DIP Window Bounds (Zero Zooming)
        double dipLeft = _virtualBounds.Left / _dpiScaleX;
        double dipTop = _virtualBounds.Top / _dpiScaleY;
        double dipWidth = _virtualBounds.Width / _dpiScaleX;
        double dipHeight = _virtualBounds.Height / _dpiScaleY;

        this.Left = dipLeft;
        this.Top = dipTop;
        this.Width = dipWidth;
        this.Height = dipHeight;

        ScreenImage.Width = dipWidth;
        ScreenImage.Height = dipHeight;
        RootGrid.Width = dipWidth;
        RootGrid.Height = dipHeight;

        IntPtr hwnd = new WindowInteropHelper(this).EnsureHandle();
        SetWindowPos(hwnd, (IntPtr)HWND_TOPMOST, _virtualBounds.Left, _virtualBounds.Top, _virtualBounds.Width, _virtualBounds.Height, SWP_SHOWWINDOW);

        Focus();
        CaptureMouse();

        // 3. Get exact current physical cursor position
        GetCursorPos(out var p);
        double curDipX = (p.X - _virtualBounds.Left) / _dpiScaleX;
        double curDipY = (p.Y - _virtualBounds.Top) / _dpiScaleY;
        var curPos = new System.Windows.Point(curDipX, curDipY);
        _lastMousePos = curPos;

        // 4. Initial state: clean listening mode (no pre-drawn line or box)
        _currentDragMode = DragMode.None;
        _hasDrawnCircle = false;
        _trailPoints.Clear();
    }

    private void Window_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (Interlocked.CompareExchange(ref _isFinishedInt, 0, 0) == 1) return;

        var pos = e.GetPosition(this);
        double deltaX = pos.X - _lastMousePos.X;
        double deltaY = pos.Y - _lastMousePos.Y;

        switch (_currentDragMode)
        {
            case DragMode.DrawingCircle:
                if (_trailPoints.Count == 0 || (pos - _lastMousePos).Length >= 1.5)
                {
                    _trailPoints.Add(pos);
                    UpdateTrailVisual();
                }
                break;

            case DragMode.MovingBox:
                _cropLeft = Math.Max(0, Math.Min(_cropLeft + deltaX, this.Width - _cropWidth));
                _cropTop = Math.Max(0, Math.Min(_cropTop + deltaY, this.Height - _cropHeight));
                UpdateCroppableBoxVisuals();
                UpdateDimensionTag();
                break;

            case DragMode.ResizeTopLeft:
                double newLeftTL = Math.Min(pos.X, _cropLeft + _cropWidth - 30);
                double newTopTL = Math.Min(pos.Y, _cropTop + _cropHeight - 30);
                _cropWidth += (_cropLeft - newLeftTL);
                _cropHeight += (_cropTop - newTopTL);
                _cropLeft = newLeftTL;
                _cropTop = newTopTL;
                UpdateCroppableBoxVisuals();
                UpdateDimensionTag();
                break;

            case DragMode.ResizeTopRight:
                double newRightTR = Math.Max(pos.X, _cropLeft + 30);
                double newTopTR = Math.Min(pos.Y, _cropTop + _cropHeight - 30);
                _cropWidth = newRightTR - _cropLeft;
                _cropHeight += (_cropTop - newTopTR);
                _cropTop = newTopTR;
                UpdateCroppableBoxVisuals();
                UpdateDimensionTag();
                break;

            case DragMode.ResizeBottomLeft:
                double newLeftBL = Math.Min(pos.X, _cropLeft + _cropWidth - 30);
                double newBottomBL = Math.Max(pos.Y, _cropTop + 30);
                _cropWidth += (_cropLeft - newLeftBL);
                _cropHeight = newBottomBL - _cropTop;
                _cropLeft = newLeftBL;
                UpdateCroppableBoxVisuals();
                UpdateDimensionTag();
                break;

            case DragMode.ResizeBottomRight:
                double newRightBR = Math.Max(pos.X, _cropLeft + 30);
                double newBottomBR = Math.Max(pos.Y, _cropTop + 30);
                _cropWidth = newRightBR - _cropLeft;
                _cropHeight = newBottomBR - _cropTop;
                UpdateCroppableBoxVisuals();
                UpdateDimensionTag();
                break;
        }

        _lastMousePos = pos;
    }

    private void Window_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (Interlocked.CompareExchange(ref _isFinishedInt, 0, 0) == 1) return;

        var pos = e.GetPosition(this);
        _lastMousePos = pos;

        if (_hasDrawnCircle)
        {
            if (e.ChangedButton == MouseButton.Right)
            {
                // Right click cancels or starts new circle
                CancelSelection();
                return;
            }

            // Check if clicking on or near the Floating Action Bar
            if (FloatingActionsBar.Visibility == Visibility.Visible)
            {
                double barLeft = Canvas.GetLeft(FloatingActionsBar);
                double barTop = Canvas.GetTop(FloatingActionsBar);
                double barWidth = FloatingActionsBar.ActualWidth > 0 ? FloatingActionsBar.ActualWidth : 400;
                double barHeight = FloatingActionsBar.ActualHeight > 0 ? FloatingActionsBar.ActualHeight : 60;

                if (pos.X >= barLeft - 10 && pos.X <= barLeft + barWidth + 10 &&
                    pos.Y >= barTop - 10 && pos.Y <= barTop + barHeight + 10)
                {
                    // Click is over the action buttons — let button events execute!
                    return;
                }
            }

            // Check if clicking near any of the 4 corner handles
            const double handleRadius = 32;

            if (IsNearPoint(pos, _cropLeft, _cropTop, handleRadius))
            {
                _currentDragMode = DragMode.ResizeTopLeft;
                ShowDimensionTag();
            }
            else if (IsNearPoint(pos, _cropLeft + _cropWidth, _cropTop, handleRadius))
            {
                _currentDragMode = DragMode.ResizeTopRight;
                ShowDimensionTag();
            }
            else if (IsNearPoint(pos, _cropLeft, _cropTop + _cropHeight, handleRadius))
            {
                _currentDragMode = DragMode.ResizeBottomLeft;
                ShowDimensionTag();
            }
            else if (IsNearPoint(pos, _cropLeft + _cropWidth, _cropTop + _cropHeight, handleRadius))
            {
                _currentDragMode = DragMode.ResizeBottomRight;
                ShowDimensionTag();
            }
            else if (pos.X >= _cropLeft && pos.X <= _cropLeft + _cropWidth && pos.Y >= _cropTop && pos.Y <= _cropTop + _cropHeight)
            {
                _currentDragMode = DragMode.MovingBox;
                ShowDimensionTag();
            }
            else
            {
                // Clicked outside: start drawing a new circle
                _trailPoints.Clear();
                _trailPoints.Add(pos);
                _hasDrawnCircle = false;
                _currentDragMode = DragMode.DrawingCircle;
                HideCroppableBox();
                UpdateTrailVisual();
            }
        }
        else
        {
            // Initial state: Start drawing circle on Left or Right button press
            _trailPoints.Clear();
            _trailPoints.Add(pos);
            _currentDragMode = DragMode.DrawingCircle;
            UpdateTrailVisual();
        }
    }

    private void Window_MouseUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (Interlocked.CompareExchange(ref _isFinishedInt, 0, 0) == 1) return;

        HideDimensionTag();

        if (_currentDragMode == DragMode.DrawingCircle)
        {
            _currentDragMode = DragMode.None;

            if (_trailPoints.Count >= 3)
            {
                CalculateBoundingBoxFromTrailWithSmartSnap();

                if (_cropWidth >= 20 && _cropHeight >= 20)
                {
                    // Hide trail line so only the clean Google Lens croppable box is visible
                    DrawingTrailPath.Data = null;
                    _hasDrawnCircle = true;
                    UpdateCroppableBoxVisuals();
                }
                else
                {
                    _trailPoints.Clear();
                    DrawingTrailPath.Data = null;
                    HideCroppableBox();
                }
            }
            else
            {
                _trailPoints.Clear();
                DrawingTrailPath.Data = null;
            }
        }
        else
        {
            _currentDragMode = DragMode.None;
        }
    }

    private void Window_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == System.Windows.Input.Key.Escape)
        {
            CancelSelection();
        }
        else if (e.Key == System.Windows.Input.Key.Enter)
        {
            if (_hasDrawnCircle)
            {
                ExecuteSearch();
            }
        }
    }

    private static bool IsNearPoint(System.Windows.Point p, double targetX, double targetY, double threshold)
    {
        return Math.Abs(p.X - targetX) <= threshold && Math.Abs(p.Y - targetY) <= threshold;
    }

    /// <summary>
    /// Phase 1: Updates glowing chromatic circle trail while drawing.
    /// </summary>
    private void UpdateTrailVisual()
    {
        if (_trailPoints.Count > 1)
        {
            var geometry = new StreamGeometry();
            using (var ctx = geometry.Open())
            {
                ctx.BeginFigure(_trailPoints[0], false, false);
                for (int i = 1; i < _trailPoints.Count; i++)
                {
                    ctx.LineTo(_trailPoints[i], true, true);
                }
            }
            geometry.Freeze();
            DrawingTrailPath.Data = geometry;
        }
    }

    /// <summary>
    /// Calculates rough bounding box from drawn points and uses SmartSnapDetector
    /// to snap tightly to pre-existing shapes/icons inside the selection.
    /// </summary>
    private void CalculateBoundingBoxFromTrailWithSmartSnap()
    {
        if (_trailPoints.Count == 0) return;

        double minX = double.MaxValue, minY = double.MaxValue;
        double maxX = double.MinValue, maxY = double.MinValue;

        foreach (var pt in _trailPoints)
        {
            minX = Math.Min(minX, pt.X);
            minY = Math.Min(minY, pt.Y);
            maxX = Math.Max(maxX, pt.X);
            maxY = Math.Max(maxY, pt.Y);
        }

        double roughLeft = Math.Max(0, minX - 8);
        double roughTop = Math.Max(0, minY - 8);
        double roughWidth = Math.Max(30, (maxX - minX) + 16);
        double roughHeight = Math.Max(30, (maxY - minY) + 16);

        // Convert DIP rough rect to physical pixels for Smart Snap analysis
        int physicalX = (int)Math.Round(roughLeft * _dpiScaleX);
        int physicalY = (int)Math.Round(roughTop * _dpiScaleY);
        int physicalW = (int)Math.Round(roughWidth * _dpiScaleX);
        int physicalH = (int)Math.Round(roughHeight * _dpiScaleY);

        var roughPhysicalRect = new System.Drawing.Rectangle(physicalX, physicalY, physicalW, physicalH);

        // Smart shape / icon snapping
        var snappedPhysicalRect = SmartSnapDetector.SnapToContour(_capturedScreenBitmap, roughPhysicalRect);

        // Convert back to DIP coordinates
        _cropLeft = snappedPhysicalRect.X / _dpiScaleX;
        _cropTop = snappedPhysicalRect.Y / _dpiScaleY;
        _cropWidth = snappedPhysicalRect.Width / _dpiScaleX;
        _cropHeight = snappedPhysicalRect.Height / _dpiScaleY;

        if (_cropLeft + _cropWidth > this.Width) _cropWidth = this.Width - _cropLeft;
        if (_cropTop + _cropHeight > this.Height) _cropHeight = this.Height - _cropTop;
    }

    /// <summary>
    /// Phase 2: Displays Google Lens Croppable Box with 4 white corner brackets and Action Bar.
    /// </summary>
    private void UpdateCroppableBoxVisuals()
    {
        // 1. Position Selection Box
        SelectionBox.Visibility = Visibility.Visible;
        SelectionBox.Width = _cropWidth;
        SelectionBox.Height = _cropHeight;
        Canvas.SetLeft(SelectionBox, _cropLeft);
        Canvas.SetTop(SelectionBox, _cropTop);

        // 2. Position 4 White Corner Brackets
        HandleTopLeft.Visibility = Visibility.Visible;
        Canvas.SetLeft(HandleTopLeft, _cropLeft);
        Canvas.SetTop(HandleTopLeft, _cropTop);

        HandleTopRight.Visibility = Visibility.Visible;
        Canvas.SetLeft(HandleTopRight, _cropLeft + _cropWidth - 26);
        Canvas.SetTop(HandleTopRight, _cropTop);

        HandleBottomLeft.Visibility = Visibility.Visible;
        Canvas.SetLeft(HandleBottomLeft, _cropLeft);
        Canvas.SetTop(HandleBottomLeft, _cropTop + _cropHeight - 26);

        HandleBottomRight.Visibility = Visibility.Visible;
        Canvas.SetLeft(HandleBottomRight, _cropLeft + _cropWidth - 26);
        Canvas.SetTop(HandleBottomRight, _cropTop + _cropHeight - 26);

        // 3. Cutout Mask
        var fullRect = new RectangleGeometry(new Rect(0, 0, this.Width, this.Height));
        var selRect = new RectangleGeometry(new Rect(_cropLeft, _cropTop, _cropWidth, _cropHeight), 16, 16);
        DimmedMaskPath.Data = new CombinedGeometry(GeometryCombineMode.Exclude, fullRect, selRect);

        // 4. Floating Action Bar
        FloatingActionsBar.Visibility = Visibility.Visible;
        double barLeft = Math.Max(16, Math.Min(_cropLeft + (_cropWidth / 2) - 150, this.Width - 360));
        double barTop = _cropTop + _cropHeight + 16;
        if (barTop + 54 > this.Height)
        {
            barTop = Math.Max(16, _cropTop - 56);
        }
        Canvas.SetLeft(FloatingActionsBar, barLeft);
        Canvas.SetTop(FloatingActionsBar, barTop);
    }

    private void ShowDimensionTag()
    {
        DimensionTag.Visibility = Visibility.Visible;
        UpdateDimensionTag();
    }

    private void UpdateDimensionTag()
    {
        if (DimensionTag.Visibility != Visibility.Visible) return;
        int physicalW = (int)Math.Round(_cropWidth * _dpiScaleX);
        int physicalH = (int)Math.Round(_cropHeight * _dpiScaleY);
        DimensionText.Text = $"✨ {physicalW} × {physicalH} px";
        Canvas.SetLeft(DimensionTag, _cropLeft + 12);
        Canvas.SetTop(DimensionTag, Math.Max(8, _cropTop - 28));
    }

    private void HideDimensionTag()
    {
        DimensionTag.Visibility = Visibility.Collapsed;
    }

    private void HideCroppableBox()
    {
        SelectionBox.Visibility = Visibility.Collapsed;
        HandleTopLeft.Visibility = Visibility.Collapsed;
        HandleTopRight.Visibility = Visibility.Collapsed;
        HandleBottomLeft.Visibility = Visibility.Collapsed;
        HandleBottomRight.Visibility = Visibility.Collapsed;
        FloatingActionsBar.Visibility = Visibility.Collapsed;
        DimensionTag.Visibility = Visibility.Collapsed;
        DimmedMaskPath.Data = null;
    }

    private void BtnConfirmSearch_PreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
        ExecuteSearch();
    }

    private void BtnConfirmSearch_Click(object sender, RoutedEventArgs e)
    {
        ExecuteSearch();
    }

    private void BtnCopyImage_PreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
        BtnCopyImage_Click(sender, e);
    }

    private async void BtnCopyImage_Click(object sender, RoutedEventArgs e)
    {
        CopyCroppedImageToClipboard();
        CopyLabelText.Text = "Copied!";
        CopyIconText.Text = "✓";
        await Task.Delay(350);
        CloseOverlay();
    }

    private void BtnDismiss_PreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
        CancelSelection();
    }

    private void BtnDismiss_Click(object sender, RoutedEventArgs e)
    {
        CancelSelection();
    }

    private async void ExecuteSearch()
    {
        if (Interlocked.Exchange(ref _isFinishedInt, 1) != 0) return;

        try
        {
            ReleaseMouseCapture();

            SearchLabelText.Text = "Launching Google Lens...";
            SearchIconText.Text = "🚀";

            if (_cropWidth >= LensConfig.MinSelectionDimensionPx && _cropHeight >= LensConfig.MinSelectionDimensionPx)
            {
                int physicalX = (int)Math.Round(_cropLeft * _dpiScaleX);
                int physicalY = (int)Math.Round(_cropTop * _dpiScaleY);
                int physicalW = (int)Math.Round(_cropWidth * _dpiScaleX);
                int physicalH = (int)Math.Round(_cropHeight * _dpiScaleY);

                var cropRect = new System.Drawing.Rectangle(physicalX, physicalY, physicalW, physicalH);
                Logger.LogInfo($"Executing Google Lens search for crop: {physicalW}x{physicalH} at ({physicalX}, {physicalY})");

                byte[] pngBytes = ScreenCapture.CropToPngBytes(_capturedScreenBitmap, cropRect);

                _ = LensSearchLauncher.LaunchSearchAsync(pngBytes);
            }

            await Task.Delay(200);
        }
        catch (Exception ex)
        {
            Logger.LogError("Error during ExecuteSearch", ex);
        }
        finally
        {
            CloseOverlay();
        }
    }

    private void CopyCroppedImageToClipboard()
    {
        try
        {
            int physicalX = (int)Math.Round(_cropLeft * _dpiScaleX);
            int physicalY = (int)Math.Round(_cropTop * _dpiScaleY);
            int physicalW = (int)Math.Round(_cropWidth * _dpiScaleX);
            int physicalH = (int)Math.Round(_cropHeight * _dpiScaleY);

            var cropRect = new System.Drawing.Rectangle(physicalX, physicalY, physicalW, physicalH);
            byte[] pngBytes = ScreenCapture.CropToPngBytes(_capturedScreenBitmap, cropRect);

            using var ms = new System.IO.MemoryStream(pngBytes);
            using var bmp = new System.Drawing.Bitmap(ms);
            var bs = ScreenCapture.ToBitmapSource(bmp);
            System.Windows.Clipboard.SetImage(bs);
            Logger.LogInfo("Cropped image copied to clipboard.");
        }
        catch (Exception ex)
        {
            Logger.LogError("Failed to copy cropped image", ex);
        }
    }

    private void CancelSelection()
    {
        if (Interlocked.Exchange(ref _isFinishedInt, 1) != 0) return;
        Logger.LogInfo("Overlay dismissed by user.");
        CloseOverlay();
    }

    private void CloseOverlay()
    {
        try
        {
            ReleaseMouseCapture();
            _capturedScreenBitmap.Dispose();
        }
        catch { }

        Close();
        OverlayClosed?.Invoke();
    }
}
