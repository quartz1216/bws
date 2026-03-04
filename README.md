> [!CAUTION]
> This is a project where I basically let Antigravity wing it to create some utilities I needed. Fair warning: the project is likely a chaotic, disjointed mess

> [!CAUTION]
> **Warning: Experimental & Fragile**  
> This project heavily relies on `IVirtualDesktopManagerInternal`, a **private and undocumented** Windows COM API. Microsoft can and will change this interface in any Windows update, which **will break this application** without notice.
>
> **Confirmed Working Environment:**  
> - **Windows 11 25H2 (Build 26200.7922)**
> 
> Operation on any other builds or versions is **untested and not guaranteed**. Use at your own risk.

<p align="center">
  <img src="docs/screenshot.png" alt="bws - Better Window Switcher" width="720" />
</p>

<h1 align="center">bws — Better Window Switcher</h1>

<p align="center">
  A high-performance, macOS-inspired <code>Alt+Tab</code> replacement for Windows.<br/>
  Built with .NET 10 &amp; WPF. Designed for power users and multi-monitor 4K workflows.
</p>

<p align="center">
  <img src="https://img.shields.io/badge/.NET-10-512BD4?logo=dotnet&logoColor=white" alt=".NET 10" />
  <img src="https://img.shields.io/badge/WPF-Desktop-0078D4?logo=windows&logoColor=white" alt="WPF" />
  <img src="https://img.shields.io/badge/DPI-Per--Monitor%20V2-green" alt="Per-Monitor V2 DPI" />
  <img src="https://img.shields.io/badge/license-MIT-blue" alt="License" />
</p>

---

## ✨ Features

| Feature | Description |
|---|---|
| **Window-Based Switching** | Shows individual windows, not grouped by app. Two Chrome windows → two icons. |
| **DWM Live Thumbnail** | Zero-lag live preview via `DwmRegisterThumbnail` — no screenshots. |
| **Virtual Desktop Aware** | Full support for Windows 11 24H2. Groups windows by workspace with MRU-ordered desktop rows. |
| **Vim-Style Navigation** | `H/J/K/L`, `A/S/D/W`, arrow keys, or `Tab`/`Shift+Tab` to navigate. |
| **Quick Actions** | `Q` to close a window ・ `Enter`/`Space` to switch ・ `Esc` to cancel. |
| **Sticky Mode** | `Ctrl+Alt+Tab` opens persistent switcher (stays open after releasing keys). |
| **Blacklist** | Filter out noisy background processes via `blacklist.txt`. |
| **System Tray** | Runs silently in the tray. Right-click for options. |
| **4K / High DPI** | Per-Monitor V2 DPI aware. Crisp on 32" 4K displays. |
| **Dark Theme** | Sleek glassmorphism UI with semi-transparent backplate. |

---

## 🛠️ Operating System Compatibility

`bws` is deep-integrated with Windows 11's private COM APIs to ensure seamless workspace switching.

- **Windows 11 24H2 / 23H2 / 22H2**: Fully supported.
- **Insider Preview**: Actively tracked and supported.
- **Windows 10**: Basic window switching works; virtual desktop features may vary.

We use `IVirtualDesktopManagerInternal` to handle desktop enumeration and switching, which allows us to bypass the usual API limitations and provide a much faster experience than standard switchers.

---

## 🚀 Quick Start

### Prerequisites

- **Windows 10 / 11** (Build 19041+)
- [**.NET 10 SDK**](https://dotnet.microsoft.com/download/dotnet/10.0)

### Build & Run

```powershell
git clone https://github.com/yourname/bws.git
cd bws
dotnet build
```

Run the built executable from `bin/Debug/net10.0-windows10.0.19041.0/bws.exe`.

> [!IMPORTANT]  
> **Do not use `dotnet run`** — the keyboard hook requires the process to run as a standard Windows app, not through the dotnet CLI host.

---

## ⌨️ Keyboard Shortcuts

### Activation

| Shortcut | Action |
|---|---|
| `Alt + Tab` | Open switcher (release `Alt` to switch) |
| `Alt + Shift + Tab` | Open switcher, move backward |
| `Ctrl + Alt + Tab` | Open switcher in **sticky mode** (stays open) |

### Navigation (while switcher is open)

| Shortcut | Action |
|---|---|
| `Tab` / `D` / `L` / `→` | Move right |
| `Shift+Tab` / `A` / `H` / `←` | Move left |
| `` ` `` / `S` / `J` / `↓` | Move down (next desktop row) |
| `` Shift+` `` / `W` / `K` / `↑` | Move up (previous desktop row) |
| `Enter` / `Space` | Switch to selected window |
| `Q` | Close the selected window |
| `Esc` | Cancel and hide |
| Mouse click | Switch to clicked window |
| Middle click | Close clicked window |

---

## 🏗️ Architecture

```
bws/
├── App.xaml / App.xaml.cs        # Entry point, tray icon, keyboard hook wiring
├── MainWindow.xaml / .xaml.cs    # Switcher UI, DWM thumbnail rendering, selection logic
├── KeyboardHook.cs               # Global low-level keyboard hook (WH_KEYBOARD_LL)
├── WindowManager.cs              # Window enumeration, MRU tracking, icon extraction
├── WorkspaceManager.cs           # Virtual desktop integration, workspace switching
├── VirtualDesktop.cs             # COM interop for Windows 11 Virtual Desktop API
├── Win32Interop.cs               # P/Invoke signatures (DWM, User32, Shell32, etc.)
├── app.manifest                  # Per-Monitor V2 DPI awareness manifest
└── blacklist.txt                 # Process names to exclude from the switcher
```

### Key Technologies

- **`SetWindowsHookEx` (WH_KEYBOARD_LL)** — intercepts `Alt+Tab` system-wide and suppresses the default Windows switcher.
- **`DwmRegisterThumbnail` / `DwmUpdateThumbnailProperties`** — projects a live, GPU-accelerated window preview with zero capture overhead.
- **`EnumWindows` + `WinEventHook`** — enumerates windows and tracks foreground changes for MRU ordering.
- **Virtual Desktop COM API** — based on [MScholtes/VirtualDesktop](https://github.com/MScholtes/VirtualDesktop) for Windows 11 24H2 support.

---

## ⚙️ Configuration

### Blacklist

Edit `blacklist.txt` (next to the exe) to exclude processes from appearing in the switcher:

```
# One process name per line
TextInputHost.exe
PhoneExperienceHost.exe
```

### Tray Menu Options

- **Show Windows from All Desktops** — toggle to display windows from every virtual desktop, not just the current one.
- **Quit bws** — exit the application.

---

## 🤝 Contributing

Pull requests are welcome! Feel free to submit issues for bugs or feature requests.

---

## 📄 License

MIT License — see [LICENSE](LICENSE) for details.
