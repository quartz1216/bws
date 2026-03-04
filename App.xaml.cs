using System;
using System.Drawing;
using System.Threading;
using System.Windows;

namespace bws
{
    public partial class App : System.Windows.Application
    {
        private System.Windows.Forms.NotifyIcon? _notifyIcon;
        private KeyboardHook? _keyboardHook;
        private MainWindow? _mainWindow;
        private Mutex? _mutex;

        private void Application_Startup(object sender, StartupEventArgs e)
        {
            const string appName = "bws_single_instance_mutex";
            bool createdNew;

            _mutex = new Mutex(true, appName, out createdNew);

            if (!createdNew)
            {
                // App is already running
                System.Windows.MessageBox.Show("bws is already running.", "Better Window Switcher", MessageBoxButton.OK, MessageBoxImage.Information);
                System.Windows.Application.Current.Shutdown();
                return;
            }

            // Initialize Tray Icon
            _notifyIcon = new System.Windows.Forms.NotifyIcon();
            _notifyIcon.Icon = SystemIcons.Application; // Ensure standard icon, or replace with custom embedded icon later
            _notifyIcon.Visible = true;
            _notifyIcon.Text = "Better Window Switcher (bws)";

            var contextMenu = new System.Windows.Forms.ContextMenuStrip();
            
            var toggleItem = new System.Windows.Forms.ToolStripMenuItem("Show Windows from All Desktops");
            toggleItem.CheckOnClick = true;
            toggleItem.CheckedChanged += (s, ev) => 
            {
                WindowManager.ShowAllWindows = toggleItem.Checked;
            };
            
            contextMenu.Items.Add(toggleItem);
            contextMenu.Items.Add(new System.Windows.Forms.ToolStripSeparator());
            contextMenu.Items.Add("Quit bws", null, OnQuitClicked);
            
            _notifyIcon.ContextMenuStrip = contextMenu;

            // Initialize MainWindow
            _mainWindow = new MainWindow();
            
            // Initialize Tracking Managers
            WindowManager.InitializeMruTracking();
            WorkspaceManager.Initialize();
            
            // Initialize Global Hook
            try
            {
                _keyboardHook = new KeyboardHook();
                _keyboardHook.AltTabPressed += KeyboardHook_AltTabPressed;
                _keyboardHook.AltWPressed += KeyboardHook_AltWPressed;
                _keyboardHook.AltReleased += KeyboardHook_AltReleased;
                _keyboardHook.EnterPressed += KeyboardHook_EnterPressed;
                _keyboardHook.EscPressed += KeyboardHook_EscPressed;
                _keyboardHook.QPressed += KeyboardHook_QPressed;
                _keyboardHook.ArrowKeyPressed += KeyboardHook_ArrowKeyPressed;
                _keyboardHook.IsSwitcherActive = () => _mainWindow != null && _mainWindow.IsSwitcherActive;
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Failed to install keyboard hook: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                System.Windows.Application.Current.Shutdown();
            }
        }

        private void KeyboardHook_AltTabPressed(object? sender, bool isShiftPressed)
        {
            System.Windows.Application.Current.Dispatcher.BeginInvoke(new Action(() =>
            {
                _mainWindow?.ShowSwitcher(isShiftPressed);
            }));
        }

        private void KeyboardHook_AltWPressed(object? sender, bool isShiftPressed)
        {
            System.Windows.Application.Current.Dispatcher.BeginInvoke(new Action(() =>
            {
                _mainWindow?.ShowWorkspaceSwitcher(isShiftPressed);
            }));
        }

        private void KeyboardHook_AltReleased(object? sender, EventArgs e)
        {
            System.Windows.Application.Current.Dispatcher.BeginInvoke(new Action(() =>
            {
                if (_mainWindow != null && _mainWindow.IsSwitcherActive)
                {
                    _mainWindow.CommitSelection();
                }
            }));
        }

        private void KeyboardHook_EnterPressed(object? sender, EventArgs e)
        {
            System.Windows.Application.Current.Dispatcher.BeginInvoke(new Action(() =>
            {
                if (_mainWindow != null && _mainWindow.IsSwitcherActive)
                {
                    _mainWindow.CommitSelection();
                }
            }));
        }

        private void KeyboardHook_EscPressed(object? sender, EventArgs e)
        {
            System.Windows.Application.Current.Dispatcher.BeginInvoke(new Action(() =>
            {
                if (_mainWindow != null && _mainWindow.IsSwitcherActive)
                {
                    _mainWindow.HideSwitcher();
                }
            }));
        }

        private void KeyboardHook_QPressed(object? sender, EventArgs e)
        {
            System.Windows.Application.Current.Dispatcher.BeginInvoke(new Action(() =>
            {
                if (_mainWindow != null && _mainWindow.IsSwitcherActive)
                {
                    _mainWindow.CloseSelection();
                }
            }));
        }

        private void KeyboardHook_ArrowKeyPressed(object? sender, bool isLeft)
        {
            System.Windows.Application.Current.Dispatcher.BeginInvoke(new Action(() =>
            {
                if (_mainWindow != null && _mainWindow.IsSwitcherActive)
                {
                    _mainWindow.ShiftSelection(isLeft);
                }
            }));
        }

        private void OnQuitClicked(object? sender, EventArgs e)
        {
            System.Windows.Application.Current.Shutdown();
        }

        private void Application_Exit(object sender, ExitEventArgs e)
        {
            if (_notifyIcon != null)
            {
                _notifyIcon.Visible = false;
                _notifyIcon.Dispose();
            }

            if (_keyboardHook != null)
            {
                _keyboardHook.Dispose();
            }

            WindowManager.ShutdownMruTracking();
            WorkspaceManager.Shutdown();

            if (_mutex != null)
            {
                _mutex.ReleaseMutex();
                _mutex.Dispose();
            }
        }
    }
}
