# MedTRx Application & Installer

Official lightweight native Windows desktop application and installer for **MedTRx**. Designed for AMiS medical carts and hospital workstations to embed any configured web application in an isolated, high-performance native container with persistent logins, custom branding, and zero bloat.

![MedTRx Banner](assets/logo.ico)

---

## Highlights

- **Ultra-lightweight:** ~50 KB native executable, ~2 MB total portable distribution.
- **Embedded WebView2:** Powered by Microsoft Edge WebView2 Evergreen Runtime.
- **Auto-Installs WebView2:** Automatically detects missing WebView2 on fresh/LTSC Windows machines and installs it seamlessly.
- **Dedicated Profile:** Isolated user session and cookie storage (`%LOCALAPPDATA%\MedTRx\UserData`).
- **Native Window Modes:** Standard native title bar, Minimize, Maximize, Close, plus `F11` toggle for borderless kiosk/fullscreen mode.
- **Offline Resilient:** Hospital-grade connection failure screen with automatic and manual Retry.
- **Single Instance:** Mutex protected—launching a second instance brings the existing window to the front.
- **Zero SDK Build:** Built using Windows built-in `csc.exe`. No Visual Studio or .NET SDK installation needed.
- **1-Click Installer:** Automatically creates Desktop shortcut, Start Menu shortcut, and registers in Windows Settings.

---

## Deployment Packages

Choose the package that fits your hospital cart environment:

1. **100% Offline Air-Gapped Bundle (`MedTRx-v1.0.3-100Percent-Offline-Bundle.zip`):**
   - Contains the complete **Microsoft WebView2 Fixed Version Runtime** pre-extracted in `runtime/`.
   - **Zero installation required, zero internet access needed, zero admin rights.**
   - Ideal for isolated hospital clinical subnets, AMiS carts, or secure hospital wards.
   - Simply extract and run `install.bat` (or run `MedTRx.exe` directly).

2. **Standard Online Package (`MedTRx-v1.0.3-Portable.zip`):**
   - Lightweight (~2.1 MB) package containing the app and the official `MicrosoftEdgeWebview2Setup.exe` bootstrapper.
   - Automatically installs the latest runtime if internet is available.

---

## Quick Start

### 1. Configure the Target URL
Open `config.json` (or `dist\config.json`) and set your server URL:
```json
{
  "url": "https://your-medtrx-server.hospital.com",
  "appName": "MedTRx",
  "startFullscreen": false,
  "startMaximized": true,
  "enableFunctionKeys": true,
  "enableF11FullscreenKey": true,
  "enableF2SettingsKey": true,
  "enableNavigationKeys": true,
  "enableBrowserHotkeys": false,
  "disableCaretBrowsing": true,
  "enableDevTools": false
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
- Copy shortcut to **Startup** (`shell:common startup` or user startup) so MedTRx launches automatically with Windows.

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

## Hotkey Management & Keyboard Shortcuts

MedTRx features a centralized keyboard policy engine with master and granular controls configured via `config.json` or the **Settings Dialog (`F2`)**:

| Key | Config Toggle | Default Action & Behavior |
| :--- | :--- | :--- |
| **`F11`** | `enableF11FullscreenKey` | Toggle Fullscreen / Kiosk Mode (requires master `enableFunctionKeys`). |
| **`Esc`** | *(Safety invariant)* | Exit Fullscreen Mode back to windowed (always enabled in fullscreen). |
| **`F5`** / **`Ctrl + R`** | `enableNavigationKeys` | Reload current web application (requires master `enableFunctionKeys`). |
| **`F2`** / **`Ctrl + ,`** | `enableF2SettingsKey` | Open In-App Configuration Dialog (requires master `enableFunctionKeys`; password-protected if `lockSettings`). |
| **`F12`** | `enableDevTools` | Open Chromium Developer Tools (disabled if `lockSettings`). |
| **`F7`** | `disableCaretBrowsing` | **Caret Browsing Suppression** (Active by default; blocked via CLI, DOM script injection, and accelerator interceptor). |
| **`F1`, `F3`, `F6`, etc.** | `enableBrowserHotkeys` | Browser Shortcuts (Default `false` to prevent kiosk breakout; allow when set to `true`). |

### Hierarchical Settings Dialog UI
Press `F2` or `Ctrl + ,` to open the configuration dialog:
- **Master Hotkey Switch:** Toggling "Enable Function Hotkeys (F1-F12)" on/off dynamically enables or disables all child hotkey checkboxes.
- **Granular Controls:** Independently configure Fullscreen (F11), Settings (F2), Navigation (F5), and Browser Hotkeys.
- **Caret Browsing Protection:** Toggle "Suppress Caret Browsing (F7)" to prevent Edge's caret browsing confirmation modal from popping up during clinical cart operations.

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

## Security & Code Signing

MedTRx binaries are digitally signed with an Authenticode signature (`CN=AMiS eMedication MedTRx SW, O=Advantech Co Ltd, OU=AMiS MedTRx, C=TW`).

### Resolving "Unknown Publisher" / SmartScreen Warnings
When downloading ZIP archives over the internet or intranet, Windows flags files with Mark-of-the-Web (MOTW).

1. **Automated Setup (Recommended):**  
   Run `install.bat` (or `install.ps1`). The installer automatically unblocks all files and registers `MedTRx_Publisher.cer` into the machine's Trusted Publishers store.
2. **Domain / Hospital Fleet GPO Deployment:**  
   IT administrators can silently deploy the public certificate across all AMiS medical carts using Group Policy or by running as Administrator:
   ```cmd
   certutil -addstore -f "Root" MedTRx_Publisher.cer
   certutil -addstore -f "TrustedPublisher" MedTRx_Publisher.cer
   ```
3. **Manual File Unblock:**  
   Right-click `MedTRx.exe` -> Properties -> check **Unblock** -> Apply; or run:
   ```powershell
   Get-ChildItem -Recurse | Unblock-File
   ```

---

## Documentation

See [`PROJECT_MEMORY.md`](PROJECT_MEMORY.md) for full architectural documentation, design decisions, and technical specifications.

