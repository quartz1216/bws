using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;

namespace bws
{
    public partial class MainWindow : Window
    {
        private List<WindowItem> _windows = new();
        private List<WorkspaceItem> _workspaces = new();
        private bool _isWorkspaceMode = false;
        
        private int _selectedIndex = 0;
        private IntPtr _thumbnailId = IntPtr.Zero;

        public bool IsSwitcherActive => _isWorkspaceMode ? _workspaces.Count > 0 : _windows.Count > 0;

        public MainWindow()
        {
            InitializeComponent();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // Initial hide since App.xaml launches it, but we only show on Alt+Tab
            this.Hide();
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            IntPtr hwnd = new WindowInteropHelper(this).Handle;
            uint exStyle = Win32Interop.GetWindowLong(hwnd, Win32Interop.GWL_EXSTYLE);
            Win32Interop.SetWindowLong(hwnd, Win32Interop.GWL_EXSTYLE, exStyle | Win32Interop.WS_EX_NOACTIVATE);
        }

        // Called by App.cs when Alt+Tab is intercepted
        public void ShowSwitcher(bool isShiftPressed)
        {
            if (!this.IsVisible)
            {
                _isWorkspaceMode = false;
                
                // Populate windows
                _windows = WindowManager.GetOpenWindows();
                
                // Do not count ourselves if we appear
                _windows.RemoveAll(w => w.Hwnd == new WindowInteropHelper(this).Handle);

                if (_windows.Count == 0) return;

                // Bind to list
                IconList.ItemsSource = _windows;
                
                // Force Icon Template
                IconList.ItemTemplate = (DataTemplate)this.Resources["WindowItemTemplate"];
                
                // Start selection at 1 (the previous window) if possible
                _selectedIndex = _windows.Count > 1 ? 1 : 0;
                
                this.UpdateLayout();
                UpdateSelectionUI();

                this.Show();
            }
            else if (!_isWorkspaceMode)
            {
                CycleSelection(isShiftPressed);
                UpdateSelectionUI();
            }
        }

        // Called by App.cs when Alt+Esc is intercepted
        public void ShowWorkspaceSwitcher(bool isShiftPressed)
        {
            if (!this.IsVisible)
            {
                _isWorkspaceMode = true;

                // Populate workspaces
                _workspaces = WorkspaceManager.GetWorkspacesInMruOrder();

                if (_workspaces.Count == 0) return;

                // Bind to list
                IconList.ItemsSource = _workspaces;
                
                // Force Workspace Template
                IconList.ItemTemplate = (DataTemplate)this.Resources["WorkspaceItemTemplate"];
                
                // Start selection at 1 (the previous workspace) if possible
                _selectedIndex = _workspaces.Count > 1 ? 1 : 0;
                
                // Hide thumbnail anchor for workspace mode
                ThumbnailAnchor.Visibility = Visibility.Collapsed;
                ActiveWindowText.Text = "Virtual Desktops";
                
                this.UpdateLayout();
                UpdateSelectionUI();

                this.Show();
            }
            else if (_isWorkspaceMode)
            {
                CycleSelection(isShiftPressed);
                UpdateSelectionUI();
            }
        }

        private void CycleSelection(bool isShiftPressed)
        {
            int count = _isWorkspaceMode ? _workspaces.Count : _windows.Count;
            if (count == 0) return;

            if (isShiftPressed)
            {
                _selectedIndex--;
                if (_selectedIndex < 0) _selectedIndex = count - 1;
            }
            else
            {
                _selectedIndex++;
                if (_selectedIndex >= count) _selectedIndex = 0;
            }
        }

        public void ShiftSelection(bool isLeft)
        {
            int count = _isWorkspaceMode ? _workspaces.Count : _windows.Count;
            if (count == 0) return;

            if (isLeft)
            {
                _selectedIndex--;
                if (_selectedIndex < 0) _selectedIndex = count - 1;
            }
            else
            {
                _selectedIndex++;
                if (_selectedIndex >= count) _selectedIndex = 0;
            }

            UpdateSelectionUI();
        }

        private void UpdateSelectionUI()
        {
            int count = _isWorkspaceMode ? _workspaces.Count : _windows.Count;
            if (count == 0) return;

            // Highlight the correct item in ItemsControl (Hack for simple styling)
            foreach (var item in VisualTreeHelperEx.FindVisualChildren<Border>(IconList))
            {
                if (item.Name == "IconBorder")
                {
                    item.Background = System.Windows.Media.Brushes.Transparent;
                }
            }

            var container = IconList.ItemContainerGenerator.ContainerFromIndex(_selectedIndex) as ContentPresenter;
            if (container != null)
            {
                var border = VisualTreeHelperEx.FindVisualChild<Border>(container);
                if (border != null)
                {
                    // Slightly translucent white to highlight
                    border.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(0x44, 0xFF, 0xFF, 0xFF));
                }
            }

            if (_isWorkspaceMode)
            {
                var selectedWorkspace = _workspaces[_selectedIndex];
                ActiveWindowText.Text = selectedWorkspace.Name;
                UnregisterThumbnail();
            }
            else
            {
                var selectedWindow = _windows[_selectedIndex];
                ActiveWindowText.Text = selectedWindow.Title;
                RegisterThumbnail(selectedWindow.Hwnd);
            }
        }

        private void RegisterThumbnail(IntPtr targetHwnd)
        {
            UnregisterThumbnail();

            IntPtr myHwnd = new WindowInteropHelper(this).Handle;
            int hResult = Win32Interop.DwmRegisterThumbnail(myHwnd, targetHwnd, out _thumbnailId);
            
            if (hResult == 0 && _thumbnailId != IntPtr.Zero)
            {
                UpdateThumbnailSize();
            }
        }

        private void UpdateThumbnailSize()
        {
            if (_thumbnailId == IntPtr.Zero) return;

            // Query native source size to preserve aspect ratio
            Win32Interop.DwmQueryThumbnailSourceSize(_thumbnailId, out Win32Interop.SIZE sourceSize);
            if (sourceSize.cx == 0 || sourceSize.cy == 0) return;

            // Get absolute coordinates of ThumbnailAnchor
            System.Windows.Point destPoint = ThumbnailAnchor.TransformToAncestor(this).Transform(new System.Windows.Point(0, 0));
            
            // Adjust for DPI scale since DWM requires physical pixels
            var source = PresentationSource.FromVisual(this);
            double dpiX = 1.0;
            double dpiY = 1.0;
            if (source?.CompositionTarget != null)
            {
                dpiX = source.CompositionTarget.TransformToDevice.M11;
                dpiY = source.CompositionTarget.TransformToDevice.M22;
            }

            double anchorWidth = ThumbnailAnchor.ActualWidth;
            double anchorHeight = ThumbnailAnchor.ActualHeight;

            // Calculate ratios to letterbox/pillarbox while preserving aspect
            double scaleX = anchorWidth / sourceSize.cx;
            double scaleY = anchorHeight / sourceSize.cy;
            double scale = Math.Min(scaleX, scaleY);

            // Scale cannot exceed 1.0 (do not magnify tiny windows past their actual size)
            if (scale > 1.0) scale = 1.0;

            double finalWidth = sourceSize.cx * scale;
            double finalHeight = sourceSize.cy * scale;

            double offsetX = (anchorWidth - finalWidth) / 2.0;
            double offsetY = (anchorHeight - finalHeight) / 2.0;

            Win32Interop.RECT destRect = new Win32Interop.RECT
            {
                left = (int)((destPoint.X + offsetX) * dpiX),
                top = (int)((destPoint.Y + offsetY) * dpiY),
                right = (int)((destPoint.X + offsetX + finalWidth) * dpiX),
                bottom = (int)((destPoint.Y + offsetY + finalHeight) * dpiY)
            };

            Win32Interop.DWM_THUMBNAIL_PROPERTIES props = new Win32Interop.DWM_THUMBNAIL_PROPERTIES
            {
                dwFlags = Win32Interop.DWM_TNP_VISIBLE | Win32Interop.DWM_TNP_RECTDESTINATION | Win32Interop.DWM_TNP_OPACITY,
                fVisible = 1,
                opacity = 255,
                rcDestination = destRect
            };

            Win32Interop.DwmUpdateThumbnailProperties(_thumbnailId, ref props);
        }

        private void UnregisterThumbnail()
        {
            if (_thumbnailId != IntPtr.Zero)
            {
                Win32Interop.DwmUnregisterThumbnail(_thumbnailId);
                _thumbnailId = IntPtr.Zero;
            }
        }

        private void Window_KeyUp(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == Key.System && e.SystemKey == Key.LeftAlt || e.Key == Key.LeftAlt || e.Key == Key.RightAlt)
            {
                CommitSelection();
            }
        }

        private void Window_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                HideSwitcher();
            }
        }

        public void CommitSelection()
        {
            if (_isWorkspaceMode)
            {
                if (_workspaces.Count > 0 && _selectedIndex >= 0 && _selectedIndex < _workspaces.Count)
                {
                    var selected = _workspaces[_selectedIndex];
                    HideSwitcher();
                    WorkspaceManager.SwitchToWorkspace(selected.Id);
                }
                else
                {
                    HideSwitcher();
                }
            }
            else
            {
                if (_windows.Count > 0 && _selectedIndex >= 0 && _selectedIndex < _windows.Count)
                {
                    var selected = _windows[_selectedIndex];

                    // Important: Hide the switcher FIRST so WPF doesn't steal focus back when it closes
                    HideSwitcher();

                    if (Win32Interop.IsWindowVisible(selected.Hwnd))
                    {
                        if (Win32Interop.IsIconic(selected.Hwnd))
                        {
                            Win32Interop.ShowWindow(selected.Hwnd, Win32Interop.SW_RESTORE);
                        }
                        
                        // Alt key focus hack: simulates Alt press to force OS to allow focus steal
                        Win32Interop.keybd_event(0x12, 0, 0, IntPtr.Zero); // 0x12 = VK_MENU (Alt)
                        Win32Interop.keybd_event(0x12, 0, Win32Interop.KEYEVENTF_KEYUP, IntPtr.Zero);
                        
                        Win32Interop.SetForegroundWindow(selected.Hwnd);
                    }
                }
                else
                {
                    HideSwitcher();
                }
            }
        }

        public void CloseSelection()
        {
            if (_isWorkspaceMode)
            {
                // Closing a virtual desktop is visually complex to orchestrate smoothly from the switcher, 
                // so we will just ignore it for now or implement COM destroy later if explicitly requested.
                return;
            }

            if (_windows.Count > 0 && _selectedIndex >= 0 && _selectedIndex < _windows.Count)
            {
                var selected = _windows[_selectedIndex];
                
                // Send graceful close request (like clicking the X button)
                Win32Interop.SendMessage(selected.Hwnd, 0x0010 /* WM_CLOSE */, IntPtr.Zero, IntPtr.Zero);
                
                // Remove it from our internal list immediately
                _windows.RemoveAt(_selectedIndex);
                
                // If there are no more windows, hide the switcher
                if (_windows.Count == 0)
                {
                    HideSwitcher();
                }
                else
                {
                    // Adjust selection index if we closed the last item in the list
                    if (_selectedIndex >= _windows.Count)
                    {
                        _selectedIndex = _windows.Count - 1;
                    }
                    
                    // Refresh the UI list
                    IconList.ItemsSource = null;
                    IconList.ItemsSource = _windows;
                    
                    // Force the template again to be safe
                    IconList.ItemTemplate = (DataTemplate)this.Resources["WindowItemTemplate"];
                    
                    // Update the visual selection ring and thumbnail
                    UpdateSelectionUI();
                }
            }
        }

        public void HideSwitcher()
        {
            UnregisterThumbnail();
            this.Hide();
            IconList.ItemsSource = null;
            _windows.Clear();
            _workspaces.Clear();
            _isWorkspaceMode = false;
            ThumbnailAnchor.Visibility = Visibility.Visible;
        }
    }

    // Helper for visual tree
    public static class VisualTreeHelperEx
    {
        public static IEnumerable<T> FindVisualChildren<T>(DependencyObject depObj) where T : DependencyObject
        {
            if (depObj != null)
            {
                for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(depObj); i++)
                {
                    DependencyObject child = System.Windows.Media.VisualTreeHelper.GetChild(depObj, i);
                    if (child != null && child is T)
                    {
                        yield return (T)child;
                    }

                    foreach (T childOfChild in FindVisualChildren<T>(child))
                    {
                        yield return childOfChild;
                    }
                }
            }
        }

        public static T? FindVisualChild<T>(DependencyObject depObj) where T : DependencyObject
        {
            foreach (var child in FindVisualChildren<T>(depObj)) return child;
            return null;
        }
    }
}