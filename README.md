# MedTRx Application & Installer

Official lightweight native Windows desktop application and installer for **MedTRx**. Designed for AMiS medical carts and hospital workstations to embed any configured web application in an isolated, high-performance native container with persistent logins, custom branding, and zero bloat.

![MedTRx Banner](assets/logo.ico)

---

## Highlights

- **Ultra-lightweight:** ~48 KB native executable, <1 MB total portable distribution.
- **Embedded WebView2:** Powered by Microsoft Edge WebView2 Evergreen Runtime.
- **Dedicated Profile:** Isolated user session and cookie storage (`%LOCALAPPDATA%\MedTRx\UserData`).
- **Native Window Modes:** Standard native title bar, Minimize, Maximize, Close, plus `F11` toggle for borderless kiosk/fullscreen mode.
- **Offline Resilient:** Hospital-grade connection failure screen with automatic and manual Retry.
- **Single Instance:** Mutex protected—launching a second instance brings the existing window to the front.
- **Zero SDK Build:** Built using Windows built-in `csc.exe`. No Visual Studio or .NET SDK installation needed.
- **1-Click Installer:** Automatically creates Desktop shortcut, Start Menu shortcut, and registers in Windows Settings.

---

## Quick Start

### 1. Configure the Target URL
Open `config.json` (or `dist\config.json`) and set your server URL:
```json
{
  "url": "https://your-medtrx-server.hospital.com",
  "appName": "MedTRx",
  "startFullscreen": false,
  "startMaximized": true
}
```
*(If you leave `"url": ""` empty, MedTRx will prompt you with a configuration dialog on launch).*

### 2. Install on Workstation / Cart
Double-click:
```cmd
scripts\install.bat
```
*(Or simply run `install.bat` inside the `dist\` folder).*  
This will:
- Copy the application to `%LOCALAPPDATA%\Programs\MedTRx` (no Admin rights required).
- Place a **MedTRx** shortcut on your **Desktop**.
- Add **MedTRx** to your **Start Menu**.

---

## Building from Source

To compile the latest binary from source code:
```cmd
scripts\build.bat
```
This script automatically:
1. Locates Windows built-in `csc.exe`.
2. Downloads and caches the official `Microsoft.Web.WebView2` NuGet dependencies.
3. Compiles `dist\MedTRx.exe` with `logo.ico` embedded directly into the Win32 resources.
4. Prepares the `dist\` folder for distribution.

---

## Keyboard Shortcuts

| Key | Function |
| :--- | :--- |
| **`F11`** | Toggle Fullscreen / Kiosk Mode |
| **`Esc`** | Exit Fullscreen Mode back to windowed |
| **`F5`** / **`Ctrl + R`** | Reload current web application |
| **`F2`** / **`Ctrl + ,`** | Open Application Configuration Dialog |
| **`F12`** | Open Developer Tools *(if `enableDevTools: true`)* |

---

## Customizing the Logo

1. Replace `assets\logo.ico` with your official multi-resolution icon.
2. Run `scripts\build.bat` to recompile the binary with the new icon embedded.
3. Run `scripts\install.bat` to update the installed app and desktop shortcut.

---

## Uninstalling

To cleanly remove the application:
```cmd
scripts\uninstall.bat
```
*(Or search for "MedTRx Application" in Windows Settings -> Installed Apps and click Uninstall).*

---

## Documentation

See [`PROJECT_MEMORY.md`](PROJECT_MEMORY.md) for full architectural documentation, design decisions, and technical specifications.
