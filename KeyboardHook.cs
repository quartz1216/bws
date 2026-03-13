using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;

namespace bws
{
    public enum MoveDirection
    {
        Left, Right, Up, Down, Home, End, PageUp, PageDown
    }

    public class KeyboardHook : IDisposable
    {
        private Win32Interop.LowLevelKeyboardProc _proc;
        private IntPtr _hookID = IntPtr.Zero;

        public event EventHandler<bool>? AltTabOpen; // bool isSticky
        public event EventHandler? AltReleased;
        public event EventHandler? EnterPressed;
        public event EventHandler? EscPressed;
        public event EventHandler? QPressed;
        public event EventHandler<MoveDirection>? DirectionKeyPressed;

        public Func<bool>? IsSwitcherActive { get; set; }
        
        public static bool UseMetaKeyShortcuts { get; set; } = false;
        private bool _suppressNextWinKeyUp = false;

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

                    bool isCtrlPressed = (Win32Interop.GetAsyncKeyState(Win32Interop.VK_LCONTROL) & 0x8000) != 0 ||
                                         (Win32Interop.GetAsyncKeyState(Win32Interop.VK_RCONTROL) & 0x8000) != 0;

                    bool isMetaPressed = UseMetaKeyShortcuts && ((Win32Interop.GetAsyncKeyState(0x5B) & 0x8000) != 0 ||
                                         (Win32Interop.GetAsyncKeyState(0x5C) & 0x8000) != 0);

                    bool isActive = IsSwitcherActive != null && IsSwitcherActive();

                    if (!isActive)
                    {
                        if (vkCode == Win32Interop.VK_TAB && isAltPressed)
                        {
                            System.Windows.Application.Current.Dispatcher.Invoke(() =>
                            {
                                AltTabOpen?.Invoke(this, isCtrlPressed);
                            });
                            return (IntPtr)1;
                        }
                        
                        if (UseMetaKeyShortcuts && isMetaPressed)
                        {
                            MoveDirection? initialDir = null;
                            if (vkCode == 0x44 || vkCode == 0x4C) initialDir = MoveDirection.Right; // D, L
                            else if (vkCode == 0x41 || vkCode == 0x48) initialDir = MoveDirection.Left; // A, H
                            else if (vkCode == 0x57 || vkCode == 0x4B) initialDir = MoveDirection.Up; // W, K
                            else if ((vkCode == 0x53 || vkCode == 0x4A) && !isShiftPressed) initialDir = MoveDirection.Down; // S, J (Exclude Shift+S for Snipping Tool)

                            if (initialDir != null)
                            {
                                _suppressNextWinKeyUp = true;
                                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                                {
                                    AltTabOpen?.Invoke(this, false);
                                    if (initialDir != MoveDirection.Right)
                                    {
                                        DirectionKeyPressed?.Invoke(this, initialDir.Value);
                                    }
                                });
                                return (IntPtr)1;
                            }
                        }
                    }
                    else
                    {
                        MoveDirection? dir = null;

                        if (vkCode == Win32Interop.VK_TAB)
                        {
                            dir = isShiftPressed ? MoveDirection.Left : MoveDirection.Right;
                        }
                        else if (vkCode == 0x44 || vkCode == 0x4C || vkCode == Win32Interop.VK_RIGHT) // D, L or Right
                        {
                            if (vkCode != Win32Interop.VK_RIGHT && isMetaPressed) _suppressNextWinKeyUp = true;
                            dir = MoveDirection.Right;
                        }
                        else if (vkCode == 0x41 || vkCode == 0x48 || vkCode == Win32Interop.VK_LEFT) // A, H or Left
                        {
                            if (vkCode != Win32Interop.VK_LEFT && isMetaPressed) _suppressNextWinKeyUp = true;
                            dir = MoveDirection.Left;
                        }
                        else if (vkCode == 0xC0) // Grave (`)
                        {
                            dir = isShiftPressed ? MoveDirection.Up : MoveDirection.Down;
                        }
                        else if ((vkCode == 0x53 || vkCode == 0x4A) && !isShiftPressed || vkCode == Win32Interop.VK_DOWN) // S, J or Down
                        {
                            if (vkCode != Win32Interop.VK_DOWN && isMetaPressed) _suppressNextWinKeyUp = true;
                            dir = MoveDirection.Down;
                        }
                        else if (vkCode == 0x57 || vkCode == 0x4B || vkCode == Win32Interop.VK_UP) // W, K or Up
                        {
                            if (vkCode != Win32Interop.VK_UP && isMetaPressed) _suppressNextWinKeyUp = true;
                            dir = MoveDirection.Up;
                        }
                        else if (vkCode == 0x24) // Home
                        {
                            dir = MoveDirection.Home;
                        }
                        else if (vkCode == 0x23) // End
                        {
                            dir = MoveDirection.End;
                        }
                        else if (vkCode == 0x21) // Page Up
                        {
                            dir = MoveDirection.PageUp;
                        }
                        else if (vkCode == 0x22) // Page Down
                        {
                            dir = MoveDirection.PageDown;
                        }

                        if (dir != null)
                        {
                            System.Windows.Application.Current.Dispatcher.Invoke(() => DirectionKeyPressed?.Invoke(this, dir.Value));
                            return (IntPtr)1;
                        }

                        if (vkCode == 0x0D || vkCode == 0x20) // Enter or Space
                        {
                            System.Windows.Application.Current.Dispatcher.Invoke(() => EnterPressed?.Invoke(this, EventArgs.Empty));
                            return (IntPtr)1;
                        }
                        if (vkCode == Win32Interop.VK_ESCAPE)
                        {
                            System.Windows.Application.Current.Dispatcher.Invoke(() => EscPressed?.Invoke(this, EventArgs.Empty));
                            return (IntPtr)1;
                        }
                        if (vkCode == 0x51) // Q
                        {
                            System.Windows.Application.Current.Dispatcher.Invoke(() => QPressed?.Invoke(this, EventArgs.Empty));
                            return (IntPtr)1;
                        }

                        // Block other typing keys while switcher is active to prevent typing into background apps
                        if (vkCode >= 0x41 && vkCode <= 0x5A) // A-Z
                        {
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
                    
                    if (vkCode == 0x5B || vkCode == 0x5C) // LWIN or RWIN
                    {
                        if (UseMetaKeyShortcuts && IsSwitcherActive != null && IsSwitcherActive())
                        {
                            System.Windows.Application.Current.Dispatcher.Invoke(() =>
                            {
                                AltReleased?.Invoke(this, EventArgs.Empty);
                            });
                        }
                        
                        if (_suppressNextWinKeyUp)
                        {
                            _suppressNextWinKeyUp = false;
                            
                            // To suppress the start menu, inject a dummy keystroke
                            Win32Interop.keybd_event(0xE8, 0, 0, IntPtr.Zero);
                            Win32Interop.keybd_event(0xE8, 0, Win32Interop.KEYEVENTF_KEYUP, IntPtr.Zero);
                        }
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
