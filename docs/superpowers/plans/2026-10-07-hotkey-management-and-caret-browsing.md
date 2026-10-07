# Hotkey Management & Caret Browsing Suppression Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Provide a comprehensive two-tier configuration system to enable/disable all function hotkeys (F1-F12, Esc, Ctrl aliases) with granular control over F2, F5, F11, and F12, while permanently disabling Caret Browsing (F7) by default across all loaded HTML pages.

**Architecture:** Implement configuration properties in `AppConfig`, a pure testable `HotkeyPolicy` decision engine, a multi-layer Caret Browsing suppression system (Chromium flags, WebView2 settings, controller accelerator interception, and DOM script injection), and an organized hierarchical UI in `SettingsForm`.

**Tech Stack:** C# 5, .NET Framework 4.0/4.5 (WinForms), Microsoft.Web.WebView2 WinForms & Core, JavaScript DOM event interception.

**Spec:** In-chat approved architecture specification for Hotkey Management and Caret Browsing Suppression.

## Global Constraints

- Must compile cleanly with Windows built-in `C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe` (C# 5 syntax limits: no out variable inline declarations, no string interpolation `$""`, no ternary operator in PS 5.1).
- Maintain backward compatibility with existing `config.json` files.
- Preserve air-gapped isolation and Authenticode signing (`CN=AMiS eMedication MedTRx SW, O=Advantech Co Ltd, OU=AMiS MedTRx, C=TW`).
- Touchscreen Sidebar must remain fully functional even when all physical keyboard F-hotkeys are disabled.
- Caret Browsing must never activate regardless of navigation, iframes, or external web page contents.

## Review Focus

1. **Focus Inside Inputs/Iframes:** Does pressing F11, F2, F5, or F7 inside a web `<input>` or `<iframe>` leak through to Chromium or WinForms?
2. **Kiosk Breakout via Esc:** Does pressing `Escape` drop fullscreen when `enableF11FullscreenKey` or `enableFunctionKeys` is `false`?
3. **Caret Confirmation Dialog:** Does pressing F7 ever prompt the native "Turn on caret browsing?" modal?
4. **Touch Sidebar Persistence:** Does disabling keyboard F-keys accidentally disable on-screen Touch Sidebar reload or fullscreen toggling?
5. **LockSettings Interlock:** When `lockSettings` is `true`, are F2 and F12 strictly disabled even if their individual checkboxes were enabled?

---

### Task 1: Core Configuration Models & Defaults

**Files:**
- Modify: `src/ConfigManager.cs:8-45`
- Test: `tests/TestRunner.cs:18-70`

**Interfaces:**
- Consumes: `AppConfig` class in `MedTRx` namespace
- Produces: New boolean properties:
  - `enableFunctionKeys` (default `true`)
  - `enableF2SettingsKey` (default `true`)
  - `enableF11FullscreenKey` (default `true`)
  - `enableBrowserHotkeys` (default `false`)
  - `disableCaretBrowsing` (default `true`)

- [ ] **Step 1: Write unit tests in `tests/TestRunner.cs` for new config fields**
  Add `Test_AppConfig_HotkeyDefaults()` and `Test_AppConfig_HotkeySerialization()` asserting defaults and JSON roundtrips.

- [ ] **Step 2: Run test suite to verify tests fail**
  Command: `powershell -NoProfile -ExecutionPolicy Bypass -File tests\run_tests.ps1`
  Expected: Compiler error (properties not defined).

- [ ] **Step 3: Implement new properties in `src/ConfigManager.cs`**
  Add properties to `AppConfig` with default initializations in the constructor.

- [ ] **Step 4: Run test suite to verify tests pass**
  Command: `powershell -NoProfile -ExecutionPolicy Bypass -File tests\run_tests.ps1`
  Expected: All unit tests pass.

- [ ] **Step 5: Commit changes**
  Command: `git add src/ConfigManager.cs tests/TestRunner.cs && git commit -m "feat(config): add hotkey management and caret browsing configuration properties"`

---

### Task 2: Pure Hotkey Decision Policy Engine

**Files:**
- Create: `src/HotkeyPolicy.cs`
- Modify: `src/ConfigManager.cs`
- Test: `tests/TestRunner.cs`

**Interfaces:**
- Consumes: `AppConfig`, `System.Windows.Forms.Keys`
- Produces:
  ```csharp
  public enum HotkeyAction { None, ToggleFullscreen, ExitFullscreen, Reload, ShowSettings, OpenDevTools, Suppress }
  public static class HotkeyPolicy {
      public static HotkeyAction Evaluate(Keys keyCode, bool ctrl, bool isFullscreen, AppConfig config);
  }
  ```

- [ ] **Step 1: Write comprehensive test cases in `tests/TestRunner.cs`**
  Write tests covering:
  - Master toggle `enableFunctionKeys = false` returns `Suppress` for F1-F12, Esc, Ctrl+R, Ctrl+,.
  - Granular toggle for F11/Esc when `enableF11FullscreenKey = false`.
  - Granular toggle for F2/Ctrl+, when `enableF2SettingsKey = false` or `lockSettings = true`.
  - Granular toggle for F5/Ctrl+R when `enableNavigationKeys = false`.
  - Granular toggle for F12 when `enableDevTools = false` or `lockSettings = true`.
  - F7 returns `Suppress` whenever `disableCaretBrowsing = true`.

- [ ] **Step 2: Run test suite to verify failure**
  Command: `powershell -NoProfile -ExecutionPolicy Bypass -File tests\run_tests.ps1`
  Expected: Compilation fails (missing `HotkeyPolicy`).

- [ ] **Step 3: Implement `src/HotkeyPolicy.cs`**
  Write the pure evaluation method according to the specified rule matrix.

- [ ] **Step 4: Run test suite to verify pass**
  Command: `powershell -NoProfile -ExecutionPolicy Bypass -File tests\run_tests.ps1`
  Expected: All unit tests pass.

- [ ] **Step 5: Commit changes**
  Command: `git add src/HotkeyPolicy.cs tests/TestRunner.cs && git commit -m "feat(hotkeys): implement pure HotkeyPolicy decision engine"`

---

### Task 3: Caret Browsing Multi-Layer Suppression

**Files:**
- Modify: `src/MainForm.cs:95-135`
- Test: `tests/TestRunner.cs`

**Interfaces:**
- Consumes: `CoreWebView2EnvironmentOptions`, `CoreWebView2Settings`, `AppConfig`
- Produces:
  - Command line switch `--disable-features=CaretBrowsing` in `CoreWebView2EnvironmentOptions`
  - `settings.AreBrowserAcceleratorKeysEnabled = config.enableBrowserHotkeys;`
  - Injected DOM key capture suppressing F7 on all frames/documents.

- [ ] **Step 1: Write test case in `tests/TestRunner.cs` verifying Caret Browsing policy**
  Assert that `HotkeyPolicy.Evaluate(Keys.F7, false, false, config)` returns `HotkeyAction.Suppress`.

- [ ] **Step 2: Run test suite**
  Command: `powershell -NoProfile -ExecutionPolicy Bypass -File tests\run_tests.ps1`

- [ ] **Step 3: Implement Caret Browsing suppression layers in `src/MainForm.cs`**
  - Add `--disable-features=CaretBrowsing` to `AdditionalBrowserArguments`.
  - Set `settings.AreBrowserAcceleratorKeysEnabled = config.enableBrowserHotkeys;` (defaults to `false`).
  - Add script in `AddScriptToExecuteOnDocumentCreatedAsync` capturing `keydown` for `F7` and calling `preventDefault()` and `stopPropagation()`.

- [ ] **Step 4: Verify compilation and tests**
  Command: `powershell -NoProfile -ExecutionPolicy Bypass -File tests\run_tests.ps1`
  Expected: PASS.

- [ ] **Step 5: Commit changes**
  Command: `git add src/MainForm.cs tests/TestRunner.cs && git commit -m "feat(security): enforce multi-layer Caret Browsing suppression"`

---

### Task 4: Dual-Layer Hotkey Interception Integration

**Files:**
- Modify: `src/MainForm.cs:115-130`, `src/MainForm.cs:703-750`
- Test: `tests/TestRunner.cs`

**Interfaces:**
- Consumes: `HotkeyPolicy`, `CoreWebView2Controller.AcceleratorKeyPressed`, `MainForm_KeyDown`
- Produces: Full suppression and dispatch of keyboard actions in both WinForms and CoreWebView2.

- [ ] **Step 1: Hook `AcceleratorKeyPressed` on `webView.CoreWebView2Controller` in `MainForm.cs`**
  Handle key down events via `HotkeyPolicy.Evaluate`, dispatching actions (`ToggleFullscreen`, `Reload`, etc.) and setting `e.Handled = true`.

- [ ] **Step 2: Update `MainForm_KeyDown` in `MainForm.cs`**
  Route WinForms key events through `HotkeyPolicy.Evaluate`.

- [ ] **Step 3: Run test suite and manual verification**
  Command: `powershell -NoProfile -ExecutionPolicy Bypass -File tests\run_tests.ps1`
  Expected: All 21+ unit tests pass, loop tests pass.

- [ ] **Step 4: Commit changes**
  Command: `git add src/MainForm.cs && git commit -m "feat(hotkeys): integrate dual-layer hotkey interception in MainForm"`

---

### Task 5: Settings Form UI Redesign (`ui-ux-pro-max`)

**Files:**
- Modify: `src/SettingsForm.cs:140-190`, `src/SettingsForm.cs:235-255`
- Test: Visual form layout and interaction

**Interfaces:**
- Consumes: `AppConfig`, WinForms controls
- Produces:
  - `chkFunctionKeys`: Master toggle checkbox for all function keys.
  - `chkF11Fullscreen`: "Allow F11 & Esc (Fullscreen & Kiosk Toggle)"
  - `chkNavigationKeys`: "Allow F5 & Ctrl+R (Page Reload)"
  - `chkF2Settings`: "Allow F2 & Ctrl+, (Settings Configuration)"
  - `chkEnableDevTools`: "Allow F12 Developer Tools (Debugging)"
  - Interactive event: toggling master checkbox dynamically enables/disables child checkboxes.
  - Lock settings event: checking `chkLockSettings` disables `chkF2Settings` and `chkEnableDevTools`.

- [ ] **Step 1: Update control declarations and layout in `src/SettingsForm.cs`**
  Add checkboxes, indent child controls by 20px, adjust form height.

- [ ] **Step 2: Wire up dependency events in `src/SettingsForm.cs`**
  Implement `UpdateHotkeyControlsState()` helper called on form load, master checkbox change, and lock checkbox change.

- [ ] **Step 3: Update `SaveConfig()` in `src/SettingsForm.cs`**
  Save all new checkbox states into `Config`.

- [ ] **Step 4: Run test harness to ensure zero compiler regressions**
  Command: `powershell -NoProfile -ExecutionPolicy Bypass -File tests\run_tests.ps1`
  Expected: PASS.

- [ ] **Step 5: Commit changes**
  Command: `git add src/SettingsForm.cs && git commit -m "feat(ui): redesign hotkey settings controls with hierarchical master toggle"`

---

### Task 6: Full Build, Re-Signing & Package Verification

**Files:**
- Execute: `scripts/build.ps1`
- Execute: `tests/run_tests.ps1`
- Execute: `scripts/package_release.ps1`
- Execute: `scripts/package_offline_release.ps1`
- Modify: `PROJECT_MEMORY.md`, `README.md`

- [ ] **Step 1: Compile and sign binary**
  Run `scripts/build.ps1` to re-sign binary with `CN=AMiS eMedication MedTRx SW, O=Advantech Co Ltd, OU=AMiS MedTRx, C=TW`.

- [ ] **Step 2: Run verification harness**
  Run `tests/run_tests.ps1` and confirm 100% pass across all phases.

- [ ] **Step 3: Package distribution archives**
  Generate `MedTRx-v1.0.3-Portable.zip` and `MedTRx-v1.0.3-100Percent-Offline-Bundle.zip`.

- [ ] **Step 4: Update documentation in `PROJECT_MEMORY.md` and `README.md`**
  Document the hotkey configuration options and Caret Browsing suppression.

- [ ] **Step 5: Commit changes**
  Command: `git add . && git commit -m "chore(release): verify hotkey management and update documentation"`

---

## Plan Review Checklist

1. **Spec Coverage:** Covers master hotkey toggle, granular F2/F5/F11/F12 controls, Caret Browsing (F7) multi-layer suppression, and UI redesign.
2. **Step Scan:** Every step defines exact files, commands, and expected outputs.
3. **Type Consistency:** Method signatures and enum values (`HotkeyAction`, `HotkeyPolicy.Evaluate`) match across tasks.
4. **Review Focus:** Explicitly tests focus inside iframes/inputs, Esc handling in fullscreen, F7 suppression, and lockSettings interlock.
5. **Proportion:** Structured into 6 self-contained, testable tasks.
