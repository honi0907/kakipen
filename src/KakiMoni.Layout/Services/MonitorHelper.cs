using System.Runtime.InteropServices;

namespace KakiMoni_Layout.Services;

public static class MonitorHelper
{
    private const int MonitorInfoPrimary = 0x00000001;
    private const uint MonitorDefaultToNearest = 2;
    private const uint SwpShowWindow = 0x0040;
    private const uint SwpFrameChanged = 0x0020;
    private const uint SwpNoActivate = 0x0010;
    private const uint SwpNoMove = 0x0002;
    private const uint SwpNoSize = 0x0001;
    private const uint SwpNoZOrder = 0x0004;
    private const int GwlStyle = -16;
    private const int GwlExStyle = -20;
    private const int WsBorder = 0x00800000;
    private const int WsCaption = 0x00C00000;
    private const int WsThickFrame = 0x00040000;
    private const int WsExWindowEdge = 0x00000100;
    private const int WsExClientEdge = 0x00000200;
    private const int WsPopup = unchecked((int)0x80000000);
    private const int WsVisible = 0x10000000;
    private const int DwmwaWindowCornerPreference = 33;
    private const int DwmwcpDonotround = 1;
    private const int DwmwaBorderColor = 34;
    private const int DwmwaNcRenderingPolicy = 2;
    private const int DwmncrpDisabled = 1;
    private const int DwmwaColorNone = unchecked((int)0xFFFFFFFE);
    // #0d1b2a ? COLORREF (BGR) ???
    private const int DwmCoverNavyBgr = 0x002A1B0D;

    public readonly record struct MonitorBounds(int Left, int Top, int Width, int Height, bool IsPrimary);

    public static bool IsOnSecondaryMonitor(IntPtr hwnd)
    {
        var monitor = FindMonitorForWindow(hwnd);
        return monitor is not null && !monitor.Value.IsPrimary;
    }

    public static int MonitorCount => EnumerateMonitors().Count;

    public static string FormatMonitorLabel(int index, bool useFullMonitorBounds = true)
    {
        var bounds = GetMonitorBoundsByIndex(index, useFullMonitorBounds);
        if (bounds is null)
            return $"モニター {index + 1}";

        var primary = bounds.Value.IsPrimary ? " メイン" : "";
        return $"モニター {index + 1}  {bounds.Value.Width}×{bounds.Value.Height}{primary}";
    }

    public static MonitorBounds? GetMonitorBoundsByIndex(int index, bool useFullMonitorBounds)
    {
        var monitors = EnumerateMonitors();
        if (index < 0 || index >= monitors.Count)
            return null;

        var monitor = monitors[index];
        var rect = useFullMonitorBounds ? monitor.Monitor : monitor.Work;
        return new MonitorBounds(
            rect.Left,
            rect.Top,
            rect.Right - rect.Left,
            rect.Bottom - rect.Top,
            monitor.IsPrimary);
    }

    public static MonitorBounds? GetSecondaryMonitorBounds(bool useFullMonitorBounds)
    {
        var monitor = GetSecondaryMonitor();
        if (monitor is null)
            return null;

        var rect = useFullMonitorBounds ? monitor.Value.Monitor : monitor.Value.Work;
        return new MonitorBounds(
            rect.Left,
            rect.Top,
            rect.Right - rect.Left,
            rect.Bottom - rect.Top,
            false);
    }

    public static MonitorBounds? GetWindowBounds(IntPtr hwnd)
    {
        if (!GetWindowRect(hwnd, out var rect))
            return null;

        return new MonitorBounds(
            rect.Left,
            rect.Top,
            rect.Right - rect.Left,
            rect.Bottom - rect.Top,
            false);
    }

    public static string DescribePlacement(IntPtr hwnd, bool useFullMonitorBounds)
    {
        var count = MonitorCount;
        var target = GetSecondaryMonitorBounds(useFullMonitorBounds);
        var actual = GetWindowBounds(hwnd);
        var onSecondary = IsOnSecondaryMonitor(hwnd);

        var targetText = target is null ? "??" : $"{target.Value.Left},{target.Value.Top}";
        var actualText = actual is null ? "?" : $"{actual.Value.Left},{actual.Value.Top}";
        return $"{count}? ??:{targetText} ??:{actualText} {(onSecondary ? "OK" : "NG")}";
    }

    public static void LogMonitors(string tag)
    {
        foreach (var monitor in EnumerateMonitors())
        {
            var r = monitor.Monitor;
            System.Diagnostics.Debug.WriteLine(
                $"[{tag}] Monitor primary={monitor.IsPrimary} rect={r.Left},{r.Top},{r.Right - r.Left}x{r.Bottom - r.Top}");
        }
    }

    /// <summary>
    /// DWM ????????????????????????????????
    /// </summary>
    public static MonitorBounds ExpandBounds(MonitorBounds bounds, int overscanPixels)
    {
        var px = Math.Max(0, overscanPixels);
        return new MonitorBounds(
            bounds.Left - px,
            bounds.Top - px,
            bounds.Width + px * 2,
            bounds.Height + px * 2,
            bounds.IsPrimary);
    }

    /// <summary>WS_POPUP + DWM ???????1??????????????????</summary>
    public static bool ApplyWin32BorderlessAndBounds(IntPtr hwnd, MonitorBounds bounds, bool popupStyle = true)
    {
        ApplyWin32Borderless(hwnd, popupStyle);
        return SetWindowBounds(hwnd, bounds);
    }

    /// <summary>
    /// WinUI ? MoveAndResize ????????1??????????? DWM ??????????????????
    /// </summary>
    public static void ApplyWin32Borderless(IntPtr hwnd, bool popupStyle = false)
    {
        try
        {
            if (popupStyle)
            {
                SetWindowLong(hwnd, GwlStyle, WsPopup | WsVisible);
            }
            else
            {
                var style = GetWindowLong(hwnd, GwlStyle);
                style &= ~(WsBorder | WsCaption | WsThickFrame);
                SetWindowLong(hwnd, GwlStyle, style);
            }

            var exStyle = GetWindowLong(hwnd, GwlExStyle);
            exStyle &= ~(WsExWindowEdge | WsExClientEdge);
            SetWindowLong(hwnd, GwlExStyle, exStyle);

            ApplyDwmBorderless(hwnd);

            SetWindowPos(
                hwnd,
                IntPtr.Zero,
                0,
                0,
                0,
                0,
                SwpNoMove | SwpNoSize | SwpNoZOrder | SwpFrameChanged | SwpNoActivate);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MonitorHelper] ApplyWin32Borderless failed: {ex}");
        }
    }

    /// <summary>Win32 ??????????????????????????</summary>
    public static bool SetWindowBounds(IntPtr hwnd, MonitorBounds bounds)
    {
        if (bounds.Width <= 0 || bounds.Height <= 0)
            return false;

        try
        {
            var flags = SwpShowWindow | SwpFrameChanged | SwpNoActivate;
            return SetWindowPos(hwnd, IntPtr.Zero, bounds.Left, bounds.Top, bounds.Width, bounds.Height, flags);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MonitorHelper] SetWindowBounds failed: {ex}");
            return false;
        }
    }

    /// <summary>
    /// ??????? ExtendFrameIntoClientArea ???1px??????????????
    /// Win11 ? DWM ?????????????????
    /// </summary>
    private static void ApplyDwmBorderless(IntPtr hwnd)
    {
        try
        {
            var corner = DwmwcpDonotround;
            _ = DwmSetWindowAttribute(hwnd, DwmwaWindowCornerPreference, ref corner, sizeof(int));

            var policy = DwmncrpDisabled;
            _ = DwmSetWindowAttribute(hwnd, DwmwaNcRenderingPolicy, ref policy, sizeof(int));

            var colorNone = DwmwaColorNone;
            if (DwmSetWindowAttribute(hwnd, DwmwaBorderColor, ref colorNone, sizeof(int)) != 0)
            {
                var navy = DwmCoverNavyBgr;
                _ = DwmSetWindowAttribute(hwnd, DwmwaBorderColor, ref navy, sizeof(int));
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MonitorHelper] ApplyDwmBorderless failed: {ex}");
        }
    }

    private static MonitorInfo? GetSecondaryMonitor()
    {
        var monitors = EnumerateMonitors();
        if (monitors.Count <= 1)
            return null;

        foreach (var monitor in monitors)
        {
            if (!monitor.IsPrimary)
                return monitor;
        }

        return monitors[1];
    }

    private static MonitorInfo? FindMonitorForWindow(IntPtr hwnd)
    {
        var handle = MonitorFromWindow(hwnd, MonitorDefaultToNearest);
        if (handle == IntPtr.Zero)
            return null;

        var info = CreateMonitorInfo();
        return GetMonitorInfo(handle, ref info) ? ToMonitorInfo(handle, info) : null;
    }

    private static List<MonitorInfo> EnumerateMonitors()
    {
        var list = new List<MonitorInfo>();
        EnumDisplayMonitors(
            IntPtr.Zero,
            IntPtr.Zero,
            (hMonitor, _, _, _) =>
            {
                var info = CreateMonitorInfo();
                if (GetMonitorInfo(hMonitor, ref info))
                    list.Add(ToMonitorInfo(hMonitor, info));
                return true;
            },
            IntPtr.Zero);
        return list;
    }

    private static MonitorInfo ToMonitorInfo(IntPtr handle, NativeMonitorInfo info) => new(
        handle,
        ToRect(info.rcMonitor),
        ToRect(info.rcWork),
        (info.dwFlags & MonitorInfoPrimary) != 0);

    private static Rect ToRect(NativeRect native) =>
        new(native.Left, native.Top, native.Right, native.Bottom);

    private static NativeMonitorInfo CreateMonitorInfo() => new() { cbSize = Marshal.SizeOf<NativeMonitorInfo>() };

    private readonly struct MonitorInfo(
        IntPtr handle,
        Rect monitor,
        Rect work,
        bool isPrimary)
    {
        public IntPtr Handle { get; } = handle;
        public Rect Monitor { get; } = monitor;
        public Rect Work { get; } = work;
        public bool IsPrimary { get; } = isPrimary;
    }

    private readonly struct Rect(int left, int top, int right, int bottom)
    {
        public int Left { get; } = left;
        public int Top { get; } = top;
        public int Right { get; } = right;
        public int Bottom { get; } = bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeMonitorInfo
    {
        public int cbSize;
        public NativeRect rcMonitor;
        public NativeRect rcWork;
        public uint dwFlags;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeWindowRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    private delegate bool MonitorEnumProc(IntPtr hMonitor, IntPtr hdcMonitor, IntPtr lprcMonitor, IntPtr dwData);

    [DllImport("user32.dll")]
    private static extern bool EnumDisplayMonitors(
        IntPtr hdc,
        IntPtr lprcClip,
        MonitorEnumProc lpfnEnum,
        IntPtr dwData);

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern bool GetMonitorInfo(IntPtr hMonitor, ref NativeMonitorInfo lpmi);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowPos(
        IntPtr hWnd,
        IntPtr hWndInsertAfter,
        int x,
        int y,
        int cx,
        int cy,
        uint uFlags);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool GetWindowRect(IntPtr hWnd, out NativeWindowRect lpRect);

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int pvAttribute, int cbAttribute);

    [DllImport("user32.dll", EntryPoint = "GetWindowLong")]
    private static extern int GetWindowLong32(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtr")]
    private static extern IntPtr GetWindowLongPtr64(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "SetWindowLong")]
    private static extern int SetWindowLong32(IntPtr hWnd, int nIndex, int dwNewLong);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtr")]
    private static extern IntPtr SetWindowLongPtr64(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

    private static int GetWindowLong(IntPtr hwnd, int index) =>
        IntPtr.Size == 8
            ? (int)GetWindowLongPtr64(hwnd, index)
            : GetWindowLong32(hwnd, index);

    private static void SetWindowLong(IntPtr hwnd, int index, int value)
    {
        if (IntPtr.Size == 8)
            SetWindowLongPtr64(hwnd, index, new IntPtr(value));
        else
            SetWindowLong32(hwnd, index, value);
    }
}
