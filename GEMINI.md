Act as an expert C# desktop application developer specializing in .NET 10, WPF, and Win32 API integration.
I want to build "bws" (Better Window Switcher), a custom, high-performance Alt+Tab alternative for Windows.

### Tech Stack
- .NET 10
- WPF (Windows Presentation Foundation)
- C#

### Core Requirements
1. **Window-Based, Not App-Based:** The switcher must display individual windows, not group them by application. If I have two Chrome windows open, show two Chrome icons.
2. **macOS-Like Horizontal Icon UI:** The main UI should be a sleek, horizontally centered list of large application icons representing the open windows. 
3. **High-Performance Live Preview:** Do NOT use standard screenshot captures for the window preview. You MUST use Desktop Window Manager (DWM) APIs (`DwmRegisterThumbnail`, `DwmUpdateThumbnailProperties`) to project a live, zero-lag preview of the currently selected window above or below the icon list.
4. **Keyboard Hook (Alt+Tab Override):** Use `SetWindowsHookEx` with `WH_KEYBOARD_LL` to intercept `Alt + Tab` (forward) and `Alt + Shift + Tab` (backward). Suppress the default Windows Alt+Tab menu.
5. **Window Enumeration & Filtering:** Use `EnumWindows`. Strictly filter out hidden, background, or system tooltips. Only show user-facing application windows. Extract high-quality icons using `GetClassLongPtr` (GCL_HICON) or `SendMessage` (WM_GETICON).
6. **Aesthetics & Scaling:** - The UI must be a borderless, transparent window (`AllowsTransparency="True"`, `WindowStyle="None"`).
   - Apply a modern Dark Theme (to match heavy 3DCG software environments).
   - Ensure the application is Per-Monitor V2 High DPI aware, as it will be used on a 32-inch 4K display. The icons and UI elements should be large and clearly visible.

### Expected Output
Please provide the complete, ready-to-run code for:
1. `MainWindow.xaml` (The modern, dark-themed UI layout).
2. `MainWindow.xaml.cs` (The code-behind handling UI logic and DWM rendering).
3. A separate utility class (e.g., `Win32Interop.cs`) containing all necessary P/Invoke signatures (`SetWindowsHookEx`, `EnumWindows`, `DwmRegisterThumbnail`, etc.).
4. `App.xaml` / `App.xaml.cs` configurations if necessary for DPI awareness or startup logic.

Ensure the code is clean, well-commented, and Do not start `dotnet run`