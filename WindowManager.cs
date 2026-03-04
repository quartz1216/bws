using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Interop;
using System.Windows.Media.Imaging;

namespace bws
{
    public class WindowItem
    {
        public IntPtr Hwnd { get; set; }
        public string Title { get; set; } = string.Empty;
        public BitmapSource? Icon { get; set; }
    }

    public static class WindowManager
    {
        public static bool ShowAllWindows { get; set; } = true;

        private static HashSet<string> _blacklist = new(StringComparer.OrdinalIgnoreCase);
        private static Dictionary<IntPtr, BitmapSource> _iconCache = new();
        private static List<IntPtr> _mruList = new List<IntPtr>();
        private static Win32Interop.WinEventDelegate? _winEventDelegate;
        private static IntPtr _hWinEventHook = IntPtr.Zero;

        public static void InitializeMruTracking()
        {
            LoadBlacklist();

            lock (_mruList)
            {
                _mruList.Clear();
                IntPtr hWnd = Win32Interop.GetTopWindow(IntPtr.Zero);
                while (hWnd != IntPtr.Zero)
                {
                    _mruList.Add(hWnd);
                    hWnd = Win32Interop.GetWindow(hWnd, Win32Interop.GW_HWNDNEXT);
                }
            }

            _winEventDelegate = new Win32Interop.WinEventDelegate(WinEventProc);
            _hWinEventHook = Win32Interop.SetWinEventHook(
                Win32Interop.EVENT_SYSTEM_FOREGROUND, Win32Interop.EVENT_SYSTEM_FOREGROUND,
                IntPtr.Zero, _winEventDelegate, 0, 0,
                Win32Interop.WINEVENT_OUTOFCONTEXT | Win32Interop.WINEVENT_SKIPOWNPROCESS);
        }

        private static void LoadBlacklist()
        {
            try
            {
                string exeDir = AppDomain.CurrentDomain.BaseDirectory;
                string blacklistPath = Path.Combine(exeDir, "blacklist.txt");

                if (File.Exists(blacklistPath))
                {
                    var lines = File.ReadAllLines(blacklistPath);
                    foreach (var line in lines)
                    {
                        var trimmed = line.Trim();
                        if (!string.IsNullOrEmpty(trimmed) && !trimmed.StartsWith("#"))
                        {
                            _blacklist.Add(trimmed);
                        }
                    }
                    Console.WriteLine($"[BWS] Loaded {_blacklist.Count} entries from blacklist.");
                }
                else
                {
                    // Create default empty blacklist
                    File.WriteAllText(blacklistPath, "# Add process names here to exclude them from the Alternative Tab Switcher (e.g. TextInputHost.exe, PhoneExperienceHost.exe)\n");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[BWS] Failed to load blacklist: {ex}");
            }
        }

        public static void ShutdownMruTracking()
        {
            if (_hWinEventHook != IntPtr.Zero)
            {
                Win32Interop.UnhookWinEvent(_hWinEventHook);
                _hWinEventHook = IntPtr.Zero;
            }
        }

        private static void WinEventProc(IntPtr hWinEventHook, uint eventType, IntPtr hwnd, int idObject, int idChild, uint dwEventThread, uint dwmsEventTime)
        {
            if (eventType == Win32Interop.EVENT_SYSTEM_FOREGROUND)
            {
                lock (_mruList)
                {
                    _mruList.Remove(hwnd);
                    _mruList.Insert(0, hwnd);
                }

                // Log the focused process for user blacklist profiling
                string procName = GetProcessName(hwnd);
                if (!string.IsNullOrEmpty(procName))
                {
                    Console.WriteLine($"[BWS FOCUS ACTIVATED] Process: {procName} | Title: {GetWindowTitle(hwnd)}");
                }
            }
        }

        public static List<WindowItem> GetOpenWindows()
        {
            var windows = new List<WindowItem>();

            IntPtr hWnd = Win32Interop.GetTopWindow(IntPtr.Zero);
            while (hWnd != IntPtr.Zero)
            {
                if (IsAppWindow(hWnd))
                {
                    string title = GetWindowTitle(hWnd);
                    if (!string.IsNullOrWhiteSpace(title))
                    {
                        windows.Add(new WindowItem
                        {
                            Hwnd = hWnd,
                            Title = title,
                            Icon = GetWindowIcon(hWnd)
                        });
                    }
                }
                
                hWnd = Win32Interop.GetWindow(hWnd, Win32Interop.GW_HWNDNEXT);
            }

            lock (_mruList)
            {
                windows = windows.OrderBy(w =>
                {
                    int index = _mruList.IndexOf(w.Hwnd);
                    return index == -1 ? int.MaxValue : index;
                }).ToList();
            }

            return windows;
        }

        private static bool IsAppWindow(IntPtr hWnd)
        {
            if (!Win32Interop.IsWindowVisible(hWnd))
                return false;

            uint style = Win32Interop.GetWindowLong(hWnd, Win32Interop.GWL_STYLE);
            uint exStyle = Win32Interop.GetWindowLong(hWnd, Win32Interop.GWL_EXSTYLE);

            // Ignore tool windows
            if ((exStyle & Win32Interop.WS_EX_TOOLWINDOW) != 0)
                return false;

            // Check if cloaked by DWM (e.g. background UWP apps)
            Win32Interop.DwmGetWindowAttribute(hWnd, Win32Interop.DWMWA_CLOAKED, out int cloaked, sizeof(int));
            if (cloaked != 0)
            {
                if (ShowAllWindows && cloaked == Win32Interop.DWM_CLOAKED_SHELL)
                {
                    // It's cloaked by the shell (likely on another virtual desktop). 
                    // Allow it since the user explicitly requested all windows.
                }
                else
                {
                    return false;
                }
            }

            // Must have a title
            if (Win32Interop.GetWindowTextLength(hWnd) == 0)
                return false;

            // Check Blacklist
            string procName = GetProcessName(hWnd);
            if (!string.IsNullOrEmpty(procName) && _blacklist.Contains(procName))
                return false;

            return true;
        }

        private static string GetProcessName(IntPtr hWnd)
        {
            try
            {
                Win32Interop.GetWindowThreadProcessId(hWnd, out uint pid);
                if (pid != 0)
                {
                    IntPtr hProc = Win32Interop.OpenProcess(Win32Interop.PROCESS_QUERY_LIMITED_INFORMATION, false, pid);
                    if (hProc != IntPtr.Zero)
                    {
                        try
                        {
                            StringBuilder sb = new StringBuilder(1024);
                            if (Win32Interop.GetModuleFileNameEx(hProc, IntPtr.Zero, sb, sb.Capacity) > 0)
                            {
                                return Path.GetFileName(sb.ToString());
                            }
                        }
                        finally
                        {
                            Win32Interop.CloseHandle(hProc);
                        }
                    }
                }
            }
            catch {}
            return string.Empty;
        }

        private static string GetWindowTitle(IntPtr hWnd)
        {
            int length = Win32Interop.GetWindowTextLength(hWnd);
            if (length == 0) return string.Empty;

            StringBuilder sb = new StringBuilder(length + 1);
            Win32Interop.GetWindowText(hWnd, sb, sb.Capacity);
            return sb.ToString();
        }

        private static BitmapSource? GetWindowIcon(IntPtr hWnd)
        {
            // Check cache first (covers cloaked UWP windows on other desktops)
            if (_iconCache.TryGetValue(hWnd, out var cachedIcon))
            {
                return cachedIcon;
            }

            var result = GetWindowIconInternal(hWnd);

            // Cache the result if we got an icon
            if (result != null)
            {
                _iconCache[hWnd] = result;
            }

            // Periodically clean cache of dead window handles
            if (_iconCache.Count > 100)
            {
                var deadHandles = _iconCache.Keys
                    .Where(h => !Win32Interop.IsWindowVisible(h))
                    .ToList();
                foreach (var h in deadHandles)
                    _iconCache.Remove(h);
            }

            return result;
        }

        private static BitmapSource? GetWindowIconInternal(IntPtr hWnd)
        {
            // 0. Try modern Shell API first (catches UWP and modern apps)
            try
            {
                string aumid = Win32Interop.GetAppUserModelId(hWnd);

                if (string.IsNullOrEmpty(aumid))
                {
                    StringBuilder sbClass = new StringBuilder(256);
                    Win32Interop.GetClassName(hWnd, sbClass, sbClass.Capacity);
                    if (sbClass.ToString() == "ApplicationFrameWindow")
                    {
                        // Try child window enumeration first (works for UWP on current desktop)
                        string childProcAumid = "";
                        Win32Interop.EnumChildWindows(hWnd, (childHwnd, lParam) => {
                            Win32Interop.GetWindowThreadProcessId(childHwnd, out uint cPid);
                            string pAumid = Win32Interop.GetAumidFromProcess(cPid);
                            if (!string.IsNullOrEmpty(pAumid)) {
                                childProcAumid = pAumid;
                                return false; // stop enumeration
                            }
                            return true;
                        }, IntPtr.Zero);
                        aumid = childProcAumid;

                        // Fallback: for cloaked UWP on other desktops, child windows may not be accessible.
                        // Try getting AUMID from the window's own process (ApplicationFrameHost)
                        if (string.IsNullOrEmpty(aumid))
                        {
                            Win32Interop.GetWindowThreadProcessId(hWnd, out uint hostPid);
                            aumid = Win32Interop.GetAumidFromProcess(hostPid);
                        }
                    }
                }

                if (!string.IsNullOrEmpty(aumid))
                {
                    var appInfo = Windows.ApplicationModel.AppInfo.GetFromAppUserModelId(aumid);
                    if (appInfo != null)
                    {
                        var logoStreamRef = appInfo.DisplayInfo.GetLogo(new Windows.Foundation.Size(256, 256));
                        if (logoStreamRef != null)
                        {
                            var task = logoStreamRef.OpenReadAsync().AsTask();
                            task.Wait();
                            var stream = task.Result;

                            using (var managedStream = stream.AsStreamForRead())
                            {
                                var bitmapImage = new BitmapImage();
                                bitmapImage.BeginInit();
                                bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
                                bitmapImage.DecodePixelWidth = 256;
                                bitmapImage.DecodePixelHeight = 256;
                                bitmapImage.StreamSource = managedStream;
                                bitmapImage.EndInit();
                                bitmapImage.Freeze();

                                // To make the icon LARGER, we need a SMALLER crop window to zoom in further, cutting away MORE padding margin.
                                var targetWidth = 96;
                                var targetHeight = 96;
                                var offset = (256 - targetWidth) / 2; // 80 offset on all sides
                                var croppedBitmap = new CroppedBitmap(bitmapImage, new System.Windows.Int32Rect(offset, offset, targetWidth, targetHeight));

                                var drawVisual = new System.Windows.Media.DrawingVisual();
                                using (var drawingContext = drawVisual.RenderOpen())
                                {
                                    drawingContext.DrawImage(croppedBitmap, new System.Windows.Rect(0, 0, 256, 256));
                                }
                                var renderTarget = new RenderTargetBitmap(256, 256, 96, 96, System.Windows.Media.PixelFormats.Pbgra32);
                                renderTarget.Render(drawVisual);
                                renderTarget.Freeze();
                                return renderTarget;
                            }
                        }
                    }
                }
            }
            catch
            {
                // Fallback to Win32 if ShellObject fails or doesn't have an AUMID
            }

            IntPtr hIcon = IntPtr.Zero;

            // 1. Try WM_GETICON (Big)
            hIcon = Win32Interop.SendMessage(hWnd, Win32Interop.WM_GETICON, (IntPtr)Win32Interop.ICON_BIG, IntPtr.Zero);
            
            // 2. Try WM_GETICON (Small)
            if (hIcon == IntPtr.Zero)
                hIcon = Win32Interop.SendMessage(hWnd, Win32Interop.WM_GETICON, (IntPtr)Win32Interop.ICON_SMALL, IntPtr.Zero);

            // 3. Try GCLP_HICON (Class Big)
            if (hIcon == IntPtr.Zero)
                hIcon = Win32Interop.GetClassLongSafe(hWnd, Win32Interop.GCLP_HICON);

            // 4. Try GCLP_HICONSM (Class Small)
            if (hIcon == IntPtr.Zero)
                hIcon = Win32Interop.GetClassLongSafe(hWnd, Win32Interop.GCLP_HICONSM);

            if (hIcon != IntPtr.Zero)
            {
                try
                {
                    var icon = Icon.FromHandle(hIcon);
                    var bitmap = icon.ToBitmap();
                    var source = Imaging.CreateBitmapSourceFromHBitmap(
                        bitmap.GetHbitmap(),
                        IntPtr.Zero,
                        System.Windows.Int32Rect.Empty,
                        BitmapSizeOptions.FromEmptyOptions());
                    source.Freeze();
                    return source;
                }
                catch
                {
                    // Fallback or ignore
                }
            }

            // 5. Final Fallback: Try extracting the default icon from the executable file itself
            try
            {
                Win32Interop.GetWindowThreadProcessId(hWnd, out uint processId);
                var processHandle = Win32Interop.OpenProcess(0x0400 | 0x0010, false, processId);
                if (processHandle != IntPtr.Zero)
                {
                    StringBuilder sb = new StringBuilder(1024);
                    int capacity = sb.Capacity;
                    if (Win32Interop.QueryFullProcessImageName(processHandle, 0, sb, ref capacity))
                    {
                        string exePath = sb.ToString();
                        var exeIcon = Icon.ExtractAssociatedIcon(exePath);
                        if (exeIcon != null)
                        {
                            var bitmap = exeIcon.ToBitmap();
                            var source = Imaging.CreateBitmapSourceFromHBitmap(
                                bitmap.GetHbitmap(),
                                IntPtr.Zero,
                                System.Windows.Int32Rect.Empty,
                                BitmapSizeOptions.FromEmptyOptions());
                            
                            source.Freeze();
                            Win32Interop.CloseHandle(processHandle);
                            return source;
                        }
                    }
                    Win32Interop.CloseHandle(processHandle);
                }
            }
            catch
            {
                // Everything failed
            }

            return null;
        }
    }
}
