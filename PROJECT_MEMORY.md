# Project Memory & Architecture Dump: MedTRx APP

**Project Name:** MedTRx Native Desktop Wrapper & Installer  
**Workspace:** `d:\Antigravity Projects\AMiS-MedTRx-APP Installer`  
**Created:** 2026-09-03  
**Status:** Active / Production Ready (v1.0.0)

---

## 1. Executive Summary & Design Decisions

MedTRx APP is an official, lightweight native Windows desktop wrapper and installer designed specifically for hospital workstations and AMiS medical carts. Instead of running a heavy Chromium browser or a bloated Electron runtime (~150MB+), MedTRx embeds the system's high-performance **Microsoft Edge WebView2 Evergreen Runtime** via a compiled native C# Windows Forms container.

### Key Decisions Resolved during Discovery (/grill-me):
- **Core Technology:** Native C# (.NET Framework 4.5+ / C# 5) + `Microsoft.Web.WebView2`. Compiled via Windows built-in `csc.exe`. Zero external compiler or SDK dependencies required.
- **Payload Size:** The compiled binary is only **~48 KB**, and the entire portable distribution package is **under 1 MB**.
- **Window Modes:** Native standard Windows title bar with Min/Max/Close, high-DPI scaling, and full keyboard toggle support (`F11` for true borderless fullscreen/kiosk mode, `Esc` to return to windowed).
- **Configuration:** External `config.json` next to the executable. Falls back to an in-app setup dialog if unconfigured, allowing IT admins or clinicians to set the hospital server URL effortlessly.
- **Persistence & Isolation:** Dedicated user data and session cache stored in `%LOCALAPPDATA%\MedTRx\UserData`, keeping logins, cookies, and tokens active across cart reboots without interfering with personal Edge browsing.
- **Single-Instance Mutex:** Only one instance of MedTRx runs at a time. Launching a second instance brings the existing window to the front.
- **Taskbar & Shell Integration:** Explicit `AppUserModelID` (`MedTRx.MedicalApp.Client`) registered for clean taskbar grouping and pinning.

---

## 2. Directory Structure

```text
AMiS-MedTRx-APP Installer/
├── .gitignore                      # Git exclusion rules (build caches, user data, binaries)
├── config.json                     # Default root configuration template
├── README.md                       # Quick start and operator guide
├── PROJECT_MEMORY.md               # Persistent architectural memory dump (this file)
│
├── assets/
│   └── logo.ico                    # Official application icon (multi-res 16px to 256px)
│
├── src/
│   ├── Program.cs                  # Entry point, single-instance mutex, AppUserModelID, crash logging
│   ├── MainForm.cs                 # Main form hosting WebView2, navigation events, F11 fullscreen
│   ├── SettingsForm.cs             # In-app configuration dialog (F2 / Ctrl+,)
│   ├── ConfigManager.cs            # JSON config loader/saver (System.Web.Script.Serialization)
│   └── ErrorPage.html              # Clean hospital-grade offline screen with Retry button
│
├── scripts/
│   ├── generate_icon.py            # Python PIL icon generator for logo.ico
│   ├── build.ps1                   # Automated build engine using csc.exe & NuGet download
│   ├── build.bat                   # Double-click launcher for build.ps1
│   ├── install.ps1                 # User-level installer (Desktop + Start Menu + Taskbar pin)
│   ├── install.bat                 # Double-click launcher for install.ps1
│   ├── uninstall.ps1               # Clean uninstaller script
│   └── uninstall.bat               # Double-click launcher for uninstall.ps1
│
└── dist/                           # Standalone portable distribution folder
    ├── MedTRx.exe                  # Compiled native binary (48 KB)
    ├── Microsoft.Web.WebView2.Core.dll
    ├── Microsoft.Web.WebView2.WinForms.dll
    ├── WebView2Loader.dll
    ├── config.json                 # Distribution configuration
    ├── logo.ico                    # App and shortcut icon
    ├── ErrorPage.html              # Embedded offline fallback page
    ├── install.bat                 # Portable installer for client machines
    ├── install.ps1
    ├── uninstall.bat               # Portable uninstaller for client machines
    ├── uninstall.ps1
    └── runtimes/                   # Native architecture loaders (x64 / x86)
```

---

## 3. Configuration Specification (`config.json`)

The configuration file is formatted as standard JSON. It can be placed directly alongside `MedTRx.exe` (primary) or in `%LOCALAPPDATA%\MedTRx\config.json` (user override).

```json
{
  "url": "https://medtrx.your-hospital.com",
  "appName": "MedTRx",
  "startFullscreen": false,
  "startMaximized": true,
  "enableDevTools": false,
  "enableNavigationKeys": true,
  "zoomFactor": 1.0,
  "allowExternalLinks": true,
  "turboMode": true,
  "autoOpenPdf": true,
  "pdfViewerMode": "embedded",
  "alwaysOnTop": false,
  "touchFullscreenSidebar": true
}
```

### Parameter Reference:
| Key | Type | Default | Description |
| :--- | :--- | :--- | :--- |
| `url` | `string` | `""` | Target web app URL. If blank, triggers the Settings GUI dialog on launch. |
| `appName` | `string` | `"MedTRx"` | Name displayed in window title bar and taskbar. |
| `startFullscreen` | `bool` | `false` | When `true`, opens immediately in borderless fullscreen/kiosk mode. |
| `startMaximized` | `bool` | `true` | When `true`, opens in maximized windowed mode. |
| `alwaysOnTop` | `bool` | `false` | When `true`, keeps MedTRx pinned on top of all Windows taskbars and other windows. Child windows (PDF Viewer & Settings) automatically inherit TopMost priority above the main window. |
| `touchFullscreenSidebar` | `bool` | `true` | When `true`, displays a covert, liquid frosted glass slide-out tab with touch vertical edge-dragging (repositionable at any height) and 1-touch fullscreen toggling. |
| `enableDevTools` | `bool` | `false` | Enables `F12` Edge Chromium Developer Tools (set to `false` in production). |
| `enableNavigationKeys` | `bool` | `true` | Enables `F5` / `Ctrl+R` page reload and browser navigation shortcuts. |
| `zoomFactor` | `number` | `1.0` | Default UI zoom ratio (e.g. `1.1` for 110% magnification). |
| `allowExternalLinks` | `bool` | `true` | When `true`, external popups open in system browser rather than hijacking cart. |

---

## 4. In-App Hotkeys & Navigation

- **`F11`**: Toggle borderless fullscreen / windowed mode.
- **`Esc`**: Exit fullscreen mode back to windowed mode.
- **`F5` / `Ctrl + R`**: Reload current web application.
- **`F2` / `Ctrl + ,`**: Open the in-app Configuration & Server URL dialog.
- **`F12`**: Open Chromium DevTools (only when `enableDevTools` is `true`).

---

## 5. Build Engine Mechanics & Automatic Version Stamping

The build engine (`scripts\build.ps1` / `scripts\build.bat`) operates with **zero external prerequisites**:
1. Checks for Windows built-in `C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe`.
2. Reads the single-source-of-truth version from `VERSION` (e.g., `1.0.2`).
3. Automatically generates/synchronizes `src\AssemblyInfo.cs` with `AssemblyVersion`, `AssemblyFileVersion` (`1.0.2.0`), and `AssemblyInformationalVersion` (`1.0.2`). This guarantees that the Windows binary properties (`Product version` and `File version` in `.exe`) always match the release package version.
4. Downloads `Microsoft.Web.WebView2` NuGet package from nuget.org (cached locally in `.cache/`).
5. Extracts `Microsoft.Web.WebView2.Core.dll`, `Microsoft.Web.WebView2.WinForms.dll`, and native `WebView2Loader.dll`.
6. Compiles `src\AssemblyInfo.cs`, `src\Program.cs`, `src\MainForm.cs`, `src\PdfViewerForm.cs`, `src\SettingsForm.cs`, and `src\ConfigManager.cs` via a generated compiler response file (`build.rsp`) embedding `assets\logo.ico` into the executable's Win32 resources.
7. Assembles all dependencies into `dist/`.
8. `scripts\install.ps1` automatically queries `(Get-Item MedTRx.exe).VersionInfo.ProductVersion` to stamp the exact version into Windows Settings / Add & Remove Programs registry (`DisplayVersion`).

---

## 6. Installation & Deployment Architecture

### Deployment Locations:
- **Target Folder:** `%LOCALAPPDATA%\Programs\MedTRx` (no Administrator or UAC permissions required).
- **Desktop Shortcut:** `%USERPROFILE%\Desktop\MedTRx.lnk` pointing to `MedTRx.exe` with `logo.ico`.
- **Start Menu:** `%APPDATA%\Microsoft\Windows\Start Menu\Programs\MedTRx.lnk`.
- **Startup Shortcut:** Copies Desktop shortcut to `shell:common startup` (`%ProgramData%\Microsoft\Windows\Start Menu\Programs\Startup`) for all users, or falls back seamlessly to current user's Startup folder (`%APPDATA%\Microsoft\Windows\Start Menu\Programs\Startup`).
- **Add/Remove Programs:** Registered under `HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\MedTRx` for native Windows uninstallation.
- **Data & Cache:** Persistent profile data in `%LOCALAPPDATA%\MedTRx\UserData`.
- **Logs:** Handled exceptions and crashes recorded in `%LOCALAPPDATA%\MedTRx\crash.log`.

### Taskbar Pinning Note:
Windows 10/11 deprecates programmatic verb execution for taskbar pinning to prevent unauthorized adware pinning. `install.ps1` attempts the shell verb automatically; in environments where Windows 11 blocks the verb, the shortcut and executable have an embedded `AppUserModelID`, allowing one-click manual pinning from the Desktop icon or running window with persistent grouping.

### WebView2 Evergreen Runtime Auto-Installation:
On Windows 10 LTSC, LTSB, or embedded medical cart environments where WebView2 is not pre-installed:
- Both `install.bat` / `install.ps1` and `MedTRx.exe` automatically check the Windows registry for `Microsoft.EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}`.
- If missing, `install.bat` automatically runs `MicrosoftEdgeWebview2Setup.exe /silent /install`.
- If launched portably, `MedTRx.exe` detects the missing runtime, prompts the clinician/user with a one-click dialog, automatically installs `MicrosoftEdgeWebview2Setup.exe`, and restarts itself cleanly.
- `MicrosoftEdgeWebview2Setup.exe` (1.78 MB) is bundled directly in the distribution and release ZIP for zero-friction offline cart setups.

### 100% Offline Fixed-Version Runtime Bundle:
For strictly air-gapped hospital subnets and medical carts with zero internet access:
- Uses `scripts/package_offline_release.bat` / `scripts/package_offline_release.ps1` to query Microsoft's CDN API and download `Microsoft.WebView2.FixedVersionRuntime.<version>.x64.cab`.
- Extracts the complete runtime into `runtime/` alongside `MedTRx.exe`.
- `MainForm.cs` checks `FindBundledRuntime()`: when `runtime/msedgewebview2.exe` is present, it directly passes this folder to `CoreWebView2Environment.CreateAsync(browserExecutableFolder, ...)`.
- Completely bypasses Windows registry lookup and system installation.
- Works 100% offline with zero external dependencies.

---

## 7. Replacing `logo.ico`

To update the application icon with a new corporate or hospital design:
1. Replace `assets\logo.ico` with the new multi-resolution `.ico` file.
2. Run `scripts\build.bat`.
3. Run `scripts\install.bat` to refresh the desktop shortcut and deployed binary.
