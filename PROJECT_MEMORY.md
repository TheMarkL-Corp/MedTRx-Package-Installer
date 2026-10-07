# Project Memory & Architecture Dump: MedTRx APP

**Project Name:** MedTRx Native Desktop Wrapper & Installer  
**Workspace:** `d:\Antigravity Projects\AMiS-MedTRx-APP Installer`  
**Created:** 2026-09-03  
**Status:** Active / Production Ready (v1.0.3)

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
  "enableFunctionKeys": true,
  "enableF11FullscreenKey": true,
  "enableF2SettingsKey": true,
  "enableNavigationKeys": true,
  "enableBrowserHotkeys": false,
  "disableCaretBrowsing": true,
  "enableDevTools": false,
  "zoomFactor": 1.0,
  "allowExternalLinks": true,
  "turboMode": true,
  "autoOpenPdf": true,
  "pdfViewerMode": "embedded",
  "alwaysOnTop": false,
  "touchFullscreenSidebar": true,
  "lockSettings": false,
  "adminPassword": ""
}
```

### Parameter Reference:
| Key | Type | Default | Description |
| :--- | :--- | :--- | :--- |
| `url` | `string` | `""` | Target web app URL. If blank, triggers the Settings GUI dialog on launch. |
| `appName` | `string` | `"MedTRx"` | Name displayed in window title bar and taskbar. |
| `startFullscreen` | `bool` | `false` | When `true`, opens immediately in borderless fullscreen/kiosk mode. |
| `startMaximized` | `bool` | `true` | When `true`, opens in maximized windowed mode. |
| `enableFunctionKeys` | `bool` | `true` | Master toggle for application-level function hotkeys. When `false`, all function keys (`F1`–`F12`) and navigation shortcuts are disabled. |
| `enableF11FullscreenKey` | `bool` | `true` | When `true` (and `enableFunctionKeys` is `true`), allows `F11` to toggle borderless fullscreen / kiosk mode. Note: `Esc` always returns to windowed mode when currently in fullscreen. |
| `enableF2SettingsKey` | `bool` | `true` | When `true` (and `enableFunctionKeys` is `true`), allows `F2` / `Ctrl+,` to open the configuration dialog (subject to `lockSettings`). |
| `enableNavigationKeys` | `bool` | `true` | When `true` (and `enableFunctionKeys` is `true`), allows `F5` / `Ctrl+R` to reload the page. |
| `enableBrowserHotkeys` | `bool` | `false` | When `true` (and `enableFunctionKeys` is `true`), permits generic browser keys (`F1` Help, `F3` Find, `F6` Focus address, etc.). When `false`, suppresses them to prevent kiosk breakout. |
| `disableCaretBrowsing` | `bool` | `true` | When `true`, suppresses Microsoft Edge / Chromium Caret Browsing modal prompt (`F7`) through multi-layer defense. |
| `enableDevTools` | `bool` | `false` | Enables `F12` Edge Chromium Developer Tools (set to `false` in production; forced `false` when `lockSettings` is `true`). |
| `zoomFactor` | `number` | `1.0` | Default UI zoom ratio (e.g. `1.1` for 110% magnification). |
| `allowExternalLinks` | `bool` | `true` | When `true`, external popups open in system browser rather than hijacking cart. |
| `turboMode` | `bool` | `true` | Performance optimizations for WebView2 rendering and cart response. |
| `autoOpenPdf` | `bool` | `true` | Automatically opens clinical PDF downloads in embedded viewer. |
| `pdfViewerMode` | `string` | `"embedded"` | Viewer mode for PDFs (`"embedded"` or `"system"`). |
| `alwaysOnTop` | `bool` | `false` | When `true`, keeps MedTRx pinned on top of all Windows taskbars and other windows. Child windows (PDF Viewer & Settings) automatically inherit TopMost priority above the main window. |
| `touchFullscreenSidebar` | `bool` | `true` | When `true`, displays a covert, liquid frosted glass slide-out tab with touch vertical edge-dragging (repositionable at any height) and 1-touch fullscreen toggling. |
| `lockSettings` | `bool` | `false` | When `true`, locks the configuration dialog with optional `adminPassword` and forces `enableDevTools` to `false`. |
| `adminPassword` | `string` | `""` | Administrator password required to unlock settings dialog when `lockSettings` is `true`. |

---

## 4. In-App Hotkeys & Navigation

MedTRx implements a dual-layer keyboard interception architecture evaluated via the pure `HotkeyPolicy` decision engine (`HotkeyPolicy.Evaluate(...)`), capturing keystrokes in both native WinForms (`ProcessCmdKey`) and the embedded WebView2 engine (`AcceleratorKeyPressed`):

| Key / Shortcut | Policy Rule | Config Flags | Default Action |
| :--- | :--- | :--- | :--- |
| **`F11`** | `ToggleFullscreen` | `enableFunctionKeys && enableF11FullscreenKey` | Toggle borderless fullscreen / kiosk mode. |
| **`Esc`** | `ExitFullscreen` | (Active only when `IsFullscreen`) | Exit fullscreen mode back to windowed mode (always allowed for safety). |
| **`F5`** / **`Ctrl + R`** | `Reload` | `enableFunctionKeys && enableNavigationKeys` | Reload current web application. |
| **`F2`** / **`Ctrl + ,`** | `OpenSettings` | `enableFunctionKeys && enableF2SettingsKey` | Open in-app Configuration dialog (prompts for admin password if `lockSettings`). |
| **`F12`** | `ToggleDevTools` | `enableDevTools && !lockSettings` | Open Chromium Developer Tools (forced off when locked). |
| **`F7`** | `SuppressCaretBrowsing` | `disableCaretBrowsing` | Suppress Caret Browsing dialogue modal across all frames. |
| **`F1`, `F3`, `F6`, etc.** | `PassThrough` or `Suppress` | `enableBrowserHotkeys` | Pass through if allowed; suppressed when `enableBrowserHotkeys: false`. |

---

## 5. Build Engine Mechanics & Automatic Version Stamping

The build engine (`scripts\build.ps1` / `scripts\build.bat`) operates with **zero external prerequisites**:
1. Checks for Windows built-in `C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe`.
2. Reads the single-source-of-truth version from `VERSION` (e.g., `1.0.3`).
3. Automatically generates/synchronizes `src\AssemblyInfo.cs` with `AssemblyVersion`, `AssemblyFileVersion` (`1.0.3.0`), and `AssemblyInformationalVersion` (`1.0.3`). This guarantees that the Windows binary properties (`Product version` and `File version` in `.exe`) always match the release package version.
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

---

## 8. Military Hospital Hardening & Compliance Specifications (DoD STIG / HIPAA)

For military hospital workstations, AMiS medical carts, and air-gapped clinical wards:

1. **Protocol Scheme Whitelisting & Navigation Restraint (`src/MainForm.cs` & `src/PdfViewerForm.cs`):**
   - Both `CoreWebView2.NavigationStarting` and `CoreWebView2.NewWindowRequested` enforce strict scheme validation via `TryResolveHttpOrHttps`.
   - **Permitted Schemes:** `http://` and `https://` (full support for standard hospital intranets and secure web systems), `about:blank`, `blob:`, and `data:` (internal in-memory charts, reports, and file generation).
   - **Local App Assets:** `file://` URLs are permitted exclusively if they resolve within the application's base directory (e.g. `ErrorPage.html`).
   - **Rejected Schemes:** Arbitrary custom protocols (e.g. `file://` external, `ms-settings:`, `calc:`, `cmd:`, `powershell:`, `smb:`, `ldap:`, `telnet:`) are blocked (`e.Cancel = true` / `e.Handled = true`) to prevent protocol hijacking, Windows shell execution, or kiosk breakout.
   - **PDF Viewer Hardening:** External viewer calls validate local file existence or `http`/`https` protocols prior to invoking `Process.Start`.

2. **Host-Browser IPC Origin Validation (`src/MainForm.cs`):**
   - `WebView_WebMessageReceived` strictly validates `e.Source` via `IsAuthorizedMessageSource`.
   - Cross-origin message injection from untrusted frames or foreign domains is completely rejected. Only messages from the configured hospital origin or the local `ErrorPage.html` are processed.

3. **Kiosk & Admin Locking (`src/ConfigManager.cs`, `src/MainForm.cs`, `src/SettingsForm.cs`):**
   - Added `lockSettings: true` and optional `adminPassword` support.
   - When settings are locked, accessing configuration via `F2`, `Ctrl+,`, or the Touch Sidebar prompts for the administrator password (or displays an administrative lockout notice).
   - Chromium DevTools (`F12`, `enableDevTools`) is permanently forced to `false` when settings are locked.

4. **Supply Chain & 100% Offline Isolation (`src/MainForm.cs`, `scripts/install.ps1`):**
   - Removed all dynamic web download fallbacks to `go.microsoft.com` from runtime error handlers and installation scripts.
   - In air-gapped military networks, missing runtimes report clear offline instructions without attempting unauthorized outbound internet requests.

5. **Binary Hardening & Cryptographic Integrity (`scripts/build.ps1`):**
   - Enabled High-Entropy 64-bit ASLR (`/highentropyva+`) in compilation flags.
   - Integrated optional Authenticode signing hooks for Windows SDK `signtool.exe` with certificate thumbprints/files.
   - Automated generation of `dist/checksums.sha256` SHA-256 integrity manifest for supply chain verification.

6. **HIPAA Logging & ePHI Sanitization (`src/Program.cs`, `scripts/install.ps1`):**
   - Added regex sanitization in `SanitizeLogMessage` to strip URL query strings (`?[QUERY_REDACTED]`), bearer tokens, and session secrets from `crash.log` and `error.log`.
   - `scripts/install.ps1` sets strict NTFS ACLs via `icacls` on `%LOCALAPPDATA%\MedTRx` and program directories, restricting access strictly to the current user, SYSTEM, and Administrators.

7. **Authenticode Code-Signing & Publisher Trust Automation (`scripts/sign_app.ps1`, `scripts/install.ps1`):**
   - Automatically signs `dist/MedTRx.exe` and `WebView2Loader.dll` using SHA-256 Authenticode digital signature (`CN=AMiS eMedication MedTRx SW, O=Advantech Co Ltd, OU=AMiS MedTRx, C=TW`).
   - Exports `dist/MedTRx_Publisher.cer` and `assets/MedTRx_Publisher.cer`.
   - `scripts/install.ps1` automatically executes `Unblock-File` on all deployed assets (neutralizing Windows Mark-of-the-Web / Zone.Identifier) and imports `MedTRx_Publisher.cer` into the `TrustedPublisher` and `Root` certificate stores.
   - Eliminates Windows Defender SmartScreen "Unknown Publisher" block and displays verified publisher status across enterprise workstations and clinical carts.

---

## 9. Multi-Layer Caret Browsing (F7) Suppression & Hierarchical Hotkeys

### Multi-Layer Caret Browsing Defense
In clinical environments, accidental or inadvertent pressing of `F7` triggers Edge/Chromium's "Turn on caret browsing?" modal dialog. In kiosk mode or touch carts, this prompt disrupts clinical workflows and can lead to unintended UI states. MedTRx neutralizes Caret Browsing through a three-layer defense in depth:

1. **Chromium Engine Flag:** During WebView2 environment initialization (`MainForm.InitWebView`), MedTRx appends `--disable-features=CaretBrowsing` to `CoreWebView2EnvironmentOptions.AdditionalBrowserArguments`.
2. **DOM-Level Event Capture Injection:** In `InitWebViewEvents()`, MedTRx registers an asynchronous pre-navigation script via `CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync`. This script listens in the capture phase (`addEventListener('keydown'/'keyup', ..., true)`) on all windows and embedded `<iframe>` elements. When `e.key === 'F7'` or `e.keyCode === 118` is detected, it immediately invokes `e.preventDefault()` and `e.stopPropagation()`.
3. **Host-Level Accelerator Interception:** Keystrokes reaching the native message loop are intercepted at both `CoreWebView2.AcceleratorKeyPressed` and `Form.ProcessCmdKey` via `HotkeyPolicy.Evaluate(...)`. When `disableCaretBrowsing` is active, `F7` evaluates to `HotkeyAction.SuppressCaretBrowsing` and is flagged as handled (`e.Handled = true`), preventing default Chromium routing.

### Hierarchical Settings Dialog UI
The configuration dialog (`SettingsForm.cs`) arranges hotkey management in a structured, hierarchical layout:
- **Master Hotkey Toggle (`enableFunctionKeys`):** Top-level checkbox controls all application function keys.
- **Granular Child Controls:** Sub-checkboxes for `enableF11FullscreenKey`, `enableF2SettingsKey`, `enableNavigationKeys`, and `enableBrowserHotkeys` are visually indented under the master toggle.
- **Dynamic State Cascading:** Unchecking the master toggle automatically disables all child checkboxes, visually signaling that the entire hotkey subsystem is deactivated while preserving the user's granular preferences.
- **Dedicated Caret Browsing Toggle (`disableCaretBrowsing`):** Allows administrators to toggle F7 suppression independently.


