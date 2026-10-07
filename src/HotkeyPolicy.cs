using System.Windows.Forms;

namespace MedTRx
{
    public enum HotkeyAction
    {
        None,
        ToggleFullscreen,
        ExitFullscreen,
        Reload,
        ShowSettings,
        OpenDevTools,
        Suppress
    }

    public static class HotkeyPolicy
    {
        public static HotkeyAction Evaluate(Keys keyCode, bool ctrl, bool isFullscreen, AppConfig config)
        {
            // 1. Null config check
            if (config == null)
            {
                return HotkeyAction.None;
            }

            // Safety Invariant: Escape in fullscreen mode must always exit fullscreen
            // to prevent kiosk entrapment, regardless of master or granular hotkey toggles.
            if (isFullscreen && keyCode == Keys.Escape)
            {
                return HotkeyAction.ExitFullscreen;
            }

            // 2. Caret Browsing check
            if (config.disableCaretBrowsing && keyCode == Keys.F7)
            {
                return HotkeyAction.Suppress;
            }

            // 3. Master toggle
            if (!config.enableFunctionKeys)
            {
                if ((keyCode >= Keys.F1 && keyCode <= Keys.F12) ||
                    (ctrl && keyCode == Keys.R) ||
                    (ctrl && (keyCode == Keys.Oemcomma || keyCode == (Keys)188)))
                {
                    return HotkeyAction.Suppress;
                }

                return HotkeyAction.None;
            }

            // 4. Granular actions when master toggle is enabled

            // Fullscreen: F11
            if (keyCode == Keys.F11)
            {
                if (config.enableF11FullscreenKey)
                {
                    return HotkeyAction.ToggleFullscreen;
                }
                return HotkeyAction.Suppress;
            }

            // Settings: F2 or Ctrl+,
            if (keyCode == Keys.F2 || (ctrl && (keyCode == Keys.Oemcomma || keyCode == (Keys)188)))
            {
                if (config.lockSettings)
                {
                    return HotkeyAction.Suppress;
                }

                if (config.enableF2SettingsKey)
                {
                    return HotkeyAction.ShowSettings;
                }
                return HotkeyAction.Suppress;
            }

            // Reload: F5 or Ctrl+R
            if (keyCode == Keys.F5 || (ctrl && keyCode == Keys.R))
            {
                if (config.enableNavigationKeys)
                {
                    return HotkeyAction.Reload;
                }
                return HotkeyAction.Suppress;
            }

            // DevTools: F12
            if (keyCode == Keys.F12)
            {
                if (config.lockSettings)
                {
                    return HotkeyAction.Suppress;
                }

                if (config.enableDevTools)
                {
                    return HotkeyAction.OpenDevTools;
                }
                return HotkeyAction.Suppress;
            }

            // Other F-keys (F1, F3, F4, F6, F7, F8, F9, F10)
            if (keyCode >= Keys.F1 && keyCode <= Keys.F12)
            {
                if (!config.enableBrowserHotkeys)
                {
                    return HotkeyAction.Suppress;
                }
                return HotkeyAction.None;
            }

            // 5. All other unhandled keys
            return HotkeyAction.None;
        }
    }
}
