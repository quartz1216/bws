using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;

namespace bws
{
    public class KeyboardHook : IDisposable
    {
        private Win32Interop.LowLevelKeyboardProc _proc;
        private IntPtr _hookID = IntPtr.Zero;

        public event EventHandler<bool>? AltTabPressed; // bool isShiftPressed
        public event EventHandler<bool>? AltWPressed; // bool isShiftPressed (Alt + W)
        public event EventHandler? AltReleased;
        public event EventHandler? EnterPressed;
        public event EventHandler? EscPressed;
        public event EventHandler? QPressed;
        public event EventHandler<bool>? ArrowKeyPressed; // bool isLeft

        public Func<bool>? IsSwitcherActive { get; set; }

        public KeyboardHook()
        {
            _proc = HookCallback;
            using (Process curProcess = Process.GetCurrentProcess())
            using (ProcessModule curModule = curProcess.MainModule!)
            {
                _hookID = Win32Interop.SetWindowsHookEx(Win32Interop.WH_KEYBOARD_LL, _proc,
                    Win32Interop.GetModuleHandle(curModule.ModuleName!), 0);
            }
        }

        private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0)
            {
                var hookStruct = Marshal.PtrToStructure<Win32Interop.KBDLLHOOKSTRUCT>(lParam);
                int vkCode = (int)hookStruct.vkCode;

                if (wParam == (IntPtr)Win32Interop.WM_SYSKEYDOWN || wParam == (IntPtr)Win32Interop.WM_KEYDOWN)
                {
                    bool isAltPressed = (Win32Interop.GetAsyncKeyState(Win32Interop.VK_LMENU) & 0x8000) != 0 ||
                                      (Win32Interop.GetAsyncKeyState(Win32Interop.VK_RMENU) & 0x8000) != 0;
                    
                    bool isShiftPressed = (Win32Interop.GetAsyncKeyState(Win32Interop.VK_LSHIFT) & 0x8000) != 0 ||
                                        (Win32Interop.GetAsyncKeyState(Win32Interop.VK_RSHIFT) & 0x8000) != 0;

                    if (isAltPressed)
                    {
                        Console.WriteLine($"[Hook] Raw Key Event -> Alt is Down. vkCode: {vkCode:X2} ({(System.Windows.Forms.Keys)vkCode})");
                    }

                    if (vkCode == Win32Interop.VK_TAB && isAltPressed)
                    {
                        System.Windows.Application.Current.Dispatcher.Invoke(() =>
                        {
                            AltTabPressed?.Invoke(this, isShiftPressed);
                        });
                        return (IntPtr)1;
                    }

                    if (vkCode == 0x57 && isAltPressed) // VK_W (W key)
                    {
                        System.Windows.Application.Current.Dispatcher.Invoke(() =>
                        {
                            try
                            {
                                AltWPressed?.Invoke(this, isShiftPressed);
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"[Hook] Exception in AltWPressed: {ex}");
                            }
                        });
                        return (IntPtr)1;
                    }

                    if (IsSwitcherActive != null && IsSwitcherActive())
                    {
                        if (vkCode == 0x0D) // VK_RETURN
                        {
                            System.Windows.Application.Current.Dispatcher.Invoke(() => EnterPressed?.Invoke(this, EventArgs.Empty));
                            return (IntPtr)1;
                        }
                        if (vkCode == Win32Interop.VK_ESCAPE)
                        {
                            System.Windows.Application.Current.Dispatcher.Invoke(() => EscPressed?.Invoke(this, EventArgs.Empty));
                            return (IntPtr)1;
                        }
                        if (vkCode == 0x51) // VK_Q (Q key)
                        {
                            System.Windows.Application.Current.Dispatcher.Invoke(() => QPressed?.Invoke(this, EventArgs.Empty));
                            return (IntPtr)1;
                        }
                        if (vkCode == 0x25) // VK_LEFT
                        {
                            System.Windows.Application.Current.Dispatcher.Invoke(() => ArrowKeyPressed?.Invoke(this, true));
                            return (IntPtr)1;
                        }
                        if (vkCode == 0x27) // VK_RIGHT
                        {
                            System.Windows.Application.Current.Dispatcher.Invoke(() => ArrowKeyPressed?.Invoke(this, false));
                            return (IntPtr)1;
                        }
                    }
                }
                else if (wParam == (IntPtr)Win32Interop.WM_KEYUP || wParam == (IntPtr)Win32Interop.WM_SYSKEYUP)
                {
                    if (vkCode == Win32Interop.VK_LMENU || vkCode == Win32Interop.VK_RMENU)
                    {
                        System.Windows.Application.Current.Dispatcher.Invoke(() =>
                        {
                            AltReleased?.Invoke(this, EventArgs.Empty);
                        });
                    }
                }
            }

            return Win32Interop.CallNextHookEx(_hookID, nCode, wParam, lParam);
        }

        public void Dispose()
        {
            if (_hookID != IntPtr.Zero)
            {
                Win32Interop.UnhookWindowsHookEx(_hookID);
                _hookID = IntPtr.Zero;
            }
        }
    }
}
