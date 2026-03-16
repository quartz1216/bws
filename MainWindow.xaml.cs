using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;

namespace bws
{
    public class WorkspaceRow
    {
        public WorkspaceItem Workspace { get; set; } = new();
        public List<WindowItem> Windows { get; set; } = new();
    }

    public partial class MainWindow : Window
    {
        private List<WorkspaceRow> _grid = new();
        
        private int _selectedRowIndex = 0;
        private int _selectedColIndex = 0;
        private bool _isStickyMode = false;
        private IntPtr _thumbnailId = IntPtr.Zero;

        public bool IsSwitcherActive => _grid.Count > 0;

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

        public void ShowSwitcher(bool isSticky)
        {
            if (this.IsVisible) return;

            _isStickyMode = isSticky;
            
            var workspaces = WorkspaceManager.GetWorkspacesInMruOrder();
            var allWindows = WindowManager.GetOpenWindows();
            allWindows.RemoveAll(w => w.Hwnd == new WindowInteropHelper(this).Handle);
            
            IntPtr foregroundHwnd = Win32Interop.GetForegroundWindow();

            _grid.Clear();

            foreach (var ws in workspaces)
            {
                _grid.Add(new WorkspaceRow { Workspace = ws });
            }

            Guid fallbackDeskId = workspaces.FirstOrDefault(w => w.IsCurrent)?.Id ?? Guid.Empty;

            foreach (var win in allWindows)
            {
                Guid deskId = WorkspaceManager.GetDesktopIdForWindow(win.Hwnd) ?? fallbackDeskId;
                var row = _grid.FirstOrDefault(r => r.Workspace.Id == deskId);
                if (row != null)
                {
                    row.Windows.Add(win);
                }
                else if (_grid.Count > 0)
                {
                    _grid[0].Windows.Add(win);
                }
            }

            // Move the foreground window to the end of the list, but only if it's in the current active workspace
            var currentWorkspace = workspaces.FirstOrDefault(w => w.IsCurrent);
            if (currentWorkspace != null)
            {
                var currentRow = _grid.FirstOrDefault(r => r.Workspace.Id == currentWorkspace.Id);
                if (currentRow != null)
                {
                    var fgWin = currentRow.Windows.FirstOrDefault(w => w.Hwnd == foregroundHwnd);
                    if (fgWin != null)
                    {
                        currentRow.Windows.Remove(fgWin);
                        currentRow.Windows.Add(fgWin);
                    }
                }
            }

            // Remove rows that have no windows
            _grid.RemoveAll(r => r.Windows.Count == 0);

            if (_grid.Count == 0) return;

            WorkspaceList.ItemsSource = null;
            WorkspaceList.ItemsSource = _grid;
            
            _selectedRowIndex = 0;
            _selectedColIndex = 0;
            
            ThumbnailAnchor.Visibility = Visibility.Visible;
            
            this.UpdateLayout();
            UpdateSelectionUI();

            // Force the window to the primary monitor
            this.WindowState = WindowState.Normal;
            this.Left = 0;
            this.Top = 0;
            this.WindowState = WindowState.Maximized;

            this.Show();
            this.Activate();
        }

        public void MoveSelection(MoveDirection dir)
        {
            if (_grid.Count == 0) return;

            if (dir == MoveDirection.Right)
            {
                _selectedColIndex++;
                if (_selectedColIndex >= _grid[_selectedRowIndex].Windows.Count)
                {
                    _selectedColIndex = 0; 
                }
            }
            else if (dir == MoveDirection.Left)
            {
                _selectedColIndex--;
                if (_selectedColIndex < 0)
                {
                    _selectedColIndex = Math.Max(0, _grid[_selectedRowIndex].Windows.Count - 1);
                }
            }
            else if (dir == MoveDirection.Down)
            {
                _selectedRowIndex++;
                if (_selectedRowIndex >= _grid.Count)
                {
                    _selectedRowIndex = 0;
                }
                _selectedColIndex = Math.Min(_selectedColIndex, Math.Max(0, _grid[_selectedRowIndex].Windows.Count - 1));
            }
            else if (dir == MoveDirection.Up)
            {
                _selectedRowIndex--;
                if (_selectedRowIndex < 0)
                {
                    _selectedRowIndex = _grid.Count - 1;
                }
                _selectedColIndex = Math.Min(_selectedColIndex, Math.Max(0, _grid[_selectedRowIndex].Windows.Count - 1));
            }
            else if (dir == MoveDirection.Home)
            {
                _selectedColIndex = 0;
            }
            else if (dir == MoveDirection.End)
            {
                _selectedColIndex = Math.Max(0, _grid[_selectedRowIndex].Windows.Count - 1);
            }
            else if (dir == MoveDirection.PageUp)
            {
                _selectedRowIndex = 0;
                _selectedColIndex = Math.Min(_selectedColIndex, Math.Max(0, _grid[_selectedRowIndex].Windows.Count - 1));
            }
            else if (dir == MoveDirection.PageDown)
            {
                _selectedRowIndex = _grid.Count - 1;
                _selectedColIndex = Math.Min(_selectedColIndex, Math.Max(0, _grid[_selectedRowIndex].Windows.Count - 1));
            }
            
            UpdateSelectionUI();
        }

        private void UpdateSelectionUI()
        {
            if (_grid.Count == 0) return;

            // Clear all highlights
            foreach (var border in VisualTreeHelperEx.FindVisualChildren<Border>(WorkspaceList))
            {
                if (border.Name == "IconBorder")
                {
                    border.Background = System.Windows.Media.Brushes.Transparent;
                }
            }

            // Find the active nested ItemsControl
            var rowContainer = WorkspaceList.ItemContainerGenerator.ContainerFromIndex(_selectedRowIndex) as ContentPresenter;
            if (rowContainer != null)
            {
                var innerItemsControl = VisualTreeHelperEx.FindVisualChild<ItemsControl>(rowContainer);
                if (innerItemsControl != null)
                {
                    var colContainer = innerItemsControl.ItemContainerGenerator.ContainerFromIndex(_selectedColIndex) as ContentPresenter;
                    if (colContainer != null)
                    {
                        var border = VisualTreeHelperEx.FindVisualChild<Border>(colContainer);
                        if (border != null)
                        {
                            border.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(0x4D, 0xFF, 0xFF, 0xFF));
                        }

                        // Auto-scroll horizontally and vertically after layout completes
                        var capturedCol = colContainer;
                        var capturedRow = rowContainer;
                        Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Loaded, new Action(() =>
                        {
                            capturedRow?.BringIntoView();
                            capturedCol?.BringIntoView();
                        }));
                    }
                }
            }

            var selectedWindow = _grid[_selectedRowIndex].Windows[_selectedColIndex];
            ActiveWindowText.Text = selectedWindow.Title;
            RegisterThumbnail(selectedWindow.Hwnd);
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

        private void IconBorder_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (sender is Border border && border.DataContext is WindowItem clickedWindow)
            {
                // Find indices
                for (int r = 0; r < _grid.Count; r++)
                {
                    for (int c = 0; c < _grid[r].Windows.Count; c++)
                    {
                        if (_grid[r].Windows[c] == clickedWindow)
                        {
                            _selectedRowIndex = r;
                            _selectedColIndex = c;
                            UpdateSelectionUI();
                            CommitSelection(true); // Ignore sticky, user clicked directly
                            return;
                        }
                    }
                }
            }
        }

        private void IconBorder_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Middle)
            {
                if (sender is Border border && border.DataContext is WindowItem clickedWindow)
                {
                    for (int r = 0; r < _grid.Count; r++)
                    {
                        for (int c = 0; c < _grid[r].Windows.Count; c++)
                        {
                            if (_grid[r].Windows[c] == clickedWindow)
                            {
                                _selectedRowIndex = r;
                                _selectedColIndex = c;
                                UpdateSelectionUI();
                                CloseSelection();
                                return;
                            }
                        }
                    }
                }
            }
        }


        public void CommitSelection(bool ignoreSticky = false)
        {
            if (ignoreSticky || !_isStickyMode)
            {
                PerformSwitch();
            }
        }

        private void PerformSwitch()
        {
            if (_grid.Count > 0 && _selectedRowIndex >= 0 && _selectedColIndex >= 0)
            {
                var selectedWindow = _grid[_selectedRowIndex].Windows[_selectedColIndex];
                var targetDeskId = _grid[_selectedRowIndex].Workspace.Id;
                var currentDeskId = WorkspaceManager.GetWorkspacesInMruOrder().FirstOrDefault(w => w.IsCurrent)?.Id;
                
                HideSwitcher();

                if (currentDeskId != null && targetDeskId != currentDeskId)
                {
                    WorkspaceManager.SwitchToWorkspace(targetDeskId);
                }
                
                Win32Interop.ForceForegroundWindow(selectedWindow.Hwnd);
            }
            else
            {
                HideSwitcher();
            }
        }

        public void CloseSelection()
        {
            if (_grid.Count > 0)
            {
                var selectedWindow = _grid[_selectedRowIndex].Windows[_selectedColIndex];
                
                Win32Interop.SendMessage(selectedWindow.Hwnd, 0x0010 /* WM_CLOSE */, IntPtr.Zero, IntPtr.Zero);
                
                var row = _grid[_selectedRowIndex];
                row.Windows.RemoveAt(_selectedColIndex);
                
                if (row.Windows.Count == 0)
                {
                    _grid.RemoveAt(_selectedRowIndex);
                    if (_selectedRowIndex >= _grid.Count)
                    {
                        _selectedRowIndex = Math.Max(0, _grid.Count - 1);
                    }
                    _selectedColIndex = 0;
                }
                else
                {
                    if (_selectedColIndex >= row.Windows.Count)
                    {
                        _selectedColIndex = Math.Max(0, row.Windows.Count - 1);
                    }
                }
                
                if (_grid.Count == 0)
                {
                    HideSwitcher();
                }
                else
                {
                    WorkspaceList.ItemsSource = null;
                    WorkspaceList.ItemsSource = _grid;
                    this.UpdateLayout();
                    UpdateSelectionUI();
                }
            }
        }

        public void HideSwitcher()
        {
            UnregisterThumbnail();
            this.Hide();
            WorkspaceList.ItemsSource = null;
            _grid.Clear();
            _isStickyMode = false;
            ThumbnailAnchor.Visibility = Visibility.Collapsed;
        }

        private void WindowBackground_MouseDown(object sender, MouseButtonEventArgs e)
        {
            var pos = e.GetPosition(MainPanel);
            if (pos.X < 0 || pos.Y < 0 || pos.X > MainPanel.ActualWidth || pos.Y > MainPanel.ActualHeight)
            {
                HideSwitcher();
            }
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

                    if (child != null)
                    {
                        foreach (T childOfChild in FindVisualChildren<T>(child))
                        {
                            yield return childOfChild;
                        }
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