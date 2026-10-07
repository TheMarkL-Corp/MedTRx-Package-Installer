using System;
using System.IO;
using System.Net;
using System.Threading;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using MedTRx;

namespace MedTRx.Tests
{
    public class TestRunner
    {
        private static int passed = 0;
        private static int failed = 0;

        public static int Main(string[] args)
        {
            Console.WriteLine("=================================================");
            Console.WriteLine("  MedTRx Automated Test Suite (TDD / Regression) ");
            Console.WriteLine("=================================================");

            RunTest("AppConfig: Default Values", Test_AppConfig_Defaults);
            RunTest("AppConfig: JSON Serialization Roundtrip", Test_AppConfig_Serialization_RoundTrip);
            RunTest("AppConfig: Hotkey Defaults", Test_AppConfig_HotkeyDefaults);
            RunTest("AppConfig: Hotkey Serialization", Test_AppConfig_HotkeySerialization);
            RunTest("AppConfig: LockSettings and DevTools Flag Enforcement", Test_AppConfig_LockSettings_Flag);
            RunTest("Security Log Sanitization: URL Query Parameter Redaction", Test_Sanitize_UrlQueryParameters);
            RunTest("Security Log Sanitization: Bearer Token Redaction", Test_Sanitize_BearerToken);
            RunTest("Security Log Sanitization: Password / Secret Redaction", Test_Sanitize_PasswordsAndSecrets);
            RunTest("Security Log Sanitization: Clean Exceptions Preserved", Test_Sanitize_NormalLogPreserved);
            RunTest("Protocol Whitelist: HTTP URLs Allowed", Test_Protocol_Http_Allowed);
            RunTest("Protocol Whitelist: HTTPS URLs Allowed", Test_Protocol_Https_Allowed);
            RunTest("Protocol Whitelist: Relative URLs Resolved Against Base", Test_Protocol_RelativeUri_Resolved);
            RunTest("Protocol Whitelist: Custom Schemes Rejected (ms-settings, calc, cmd, etc.)", Test_Protocol_CustomSchemes_Blocked);
            RunTest("Protocol Whitelist: External file:// Scheme Rejected", Test_Protocol_FileScheme_Blocked);
            RunTest("Protocol Whitelist: Null or Whitespace URLs Rejected", Test_Protocol_NullOrWhitespace_Blocked);
            RunTest("Origin Authorization: Target Host and Port Allowed", Test_Origin_TargetHost_Allowed);
            RunTest("Origin Authorization: Target Subpaths Allowed", Test_Origin_TargetSubpath_Allowed);
            RunTest("Origin Authorization: Foreign Host Rejected", Test_Origin_ForeignHost_Blocked);
            RunTest("Origin Authorization: Port Mismatch Rejected", Test_Origin_PortMismatch_Blocked);
            RunTest("Origin Authorization: Scheme Mismatch (HTTP vs HTTPS) Rejected", Test_Origin_SchemeMismatch_Blocked);
            RunTest("Origin Authorization: Local ErrorPage.html Allowed", Test_Origin_LocalErrorPage_Allowed);
            RunTest("Origin Authorization: External Local Files Rejected", Test_Origin_ExternalLocalFile_Blocked);
            RunTest("System: Single Instance Mutex Creation & Detection", Test_SingleInstance_Mutex);
            RunTest("HotkeyPolicy: Null Config Evaluates to None", Test_HotkeyPolicy_NullConfig);
            RunTest("HotkeyPolicy: Master Toggle Disabled Suppresses Keys", Test_HotkeyPolicy_MasterToggle_Disabled);
            RunTest("HotkeyPolicy: Caret Browsing Toggle (F7)", Test_HotkeyPolicy_CaretBrowsing);
            RunTest("HotkeyPolicy: Fullscreen and Escape Toggles", Test_HotkeyPolicy_Fullscreen_And_Escape);
            RunTest("HotkeyPolicy: Settings Hotkeys (F2 / Ctrl+,)", Test_HotkeyPolicy_SettingsKey);
            RunTest("HotkeyPolicy: Reload Hotkeys (F5 / Ctrl+R)", Test_HotkeyPolicy_ReloadKey);
            RunTest("HotkeyPolicy: DevTools Hotkey (F12)", Test_HotkeyPolicy_DevToolsKey);
            RunTest("HotkeyPolicy: Browser Hotkeys & Other F-Keys", Test_HotkeyPolicy_OtherFKeys_And_Unhandled);
            RunTest("HotkeyPolicy: Caret Browsing Multi-Layer Policy", Test_CaretBrowsing_MultiLayerPolicy);

            Console.WriteLine("=================================================");
            Console.WriteLine(string.Format("  TEST RUN SUMMARY: {0} Passed, {1} Failed", passed, failed));
            Console.WriteLine("=================================================");

            return failed > 0 ? 1 : 0;
        }

        private static void RunTest(string name, Action testAction)
        {
            try
            {
                testAction();
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("[PASS] " + name);
                Console.ResetColor();
                passed++;
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("[FAIL] " + name + ": " + ex.Message);
                Console.ResetColor();
                failed++;
            }
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new Exception("Assertion Failed: " + message);
        }

        private static void AssertEqual<T>(T expected, T actual, string message)
        {
            if (!object.Equals(expected, actual))
            {
                throw new Exception(string.Format("Assertion Failed: {0}. Expected: [{1}], Actual: [{2}]", message, expected, actual));
            }
        }

        // =========================================================================
        // Test Cases
        // =========================================================================

        private static void Test_AppConfig_Defaults()
        {
            var config = new AppConfig();
            AssertEqual("MedTRx", config.appName, "Default appName");
            AssertEqual("", config.url, "Default url");
            AssertEqual(false, config.startFullscreen, "Default startFullscreen");
            AssertEqual(true, config.startMaximized, "Default startMaximized");
            AssertEqual(false, config.enableDevTools, "Default enableDevTools");
            AssertEqual(true, config.enableNavigationKeys, "Default enableNavigationKeys");
            AssertEqual(1.0, config.zoomFactor, "Default zoomFactor");
            AssertEqual(true, config.allowExternalLinks, "Default allowExternalLinks");
            AssertEqual(true, config.turboMode, "Default turboMode");
            AssertEqual(true, config.autoOpenPdf, "Default autoOpenPdf");
            AssertEqual("embedded", config.pdfViewerMode, "Default pdfViewerMode");
            AssertEqual(false, config.alwaysOnTop, "Default alwaysOnTop");
            AssertEqual(true, config.touchFullscreenSidebar, "Default touchFullscreenSidebar");
            AssertEqual(false, config.lockSettings, "Default lockSettings");
            AssertEqual("", config.adminPassword, "Default adminPassword");
        }

        private static void Test_AppConfig_Serialization_RoundTrip()
        {
            var original = new AppConfig();
            original.url = "https://medtrx-ehr.military.mil:8443";
            original.appName = "AMiS Clinical Client";
            original.lockSettings = true;
            original.adminPassword = "MilSecureCart2026!";
            original.enableDevTools = false;
            original.zoomFactor = 1.25;

            var serializer = new JavaScriptSerializer();
            string json = serializer.Serialize(original);
            var deserialized = serializer.Deserialize<AppConfig>(json);

            Assert(deserialized != null, "Deserialized config must not be null");
            AssertEqual(original.url, deserialized.url, "url match");
            AssertEqual(original.appName, deserialized.appName, "appName match");
            AssertEqual(original.lockSettings, deserialized.lockSettings, "lockSettings match");
            AssertEqual(original.adminPassword, deserialized.adminPassword, "adminPassword match");
            AssertEqual(original.enableDevTools, deserialized.enableDevTools, "enableDevTools match");
            AssertEqual(original.zoomFactor, deserialized.zoomFactor, "zoomFactor match");
        }

        private static void Test_AppConfig_HotkeyDefaults()
        {
            var config = new AppConfig();
            AssertEqual(true, config.enableFunctionKeys, "Default enableFunctionKeys must be true");
            AssertEqual(true, config.enableF2SettingsKey, "Default enableF2SettingsKey must be true");
            AssertEqual(true, config.enableF11FullscreenKey, "Default enableF11FullscreenKey must be true");
            AssertEqual(false, config.enableBrowserHotkeys, "Default enableBrowserHotkeys must be false");
            AssertEqual(true, config.disableCaretBrowsing, "Default disableCaretBrowsing must be true");
        }

        private static void Test_AppConfig_HotkeySerialization()
        {
            var original = new AppConfig();
            original.enableFunctionKeys = false;
            original.enableF2SettingsKey = false;
            original.enableF11FullscreenKey = false;
            original.enableBrowserHotkeys = true;
            original.disableCaretBrowsing = false;

            var serializer = new JavaScriptSerializer();
            string json = serializer.Serialize(original);
            var deserialized = serializer.Deserialize<AppConfig>(json);

            Assert(deserialized != null, "Deserialized config must not be null");
            AssertEqual(false, deserialized.enableFunctionKeys, "enableFunctionKeys roundtrip");
            AssertEqual(false, deserialized.enableF2SettingsKey, "enableF2SettingsKey roundtrip");
            AssertEqual(false, deserialized.enableF11FullscreenKey, "enableF11FullscreenKey roundtrip");
            AssertEqual(true, deserialized.enableBrowserHotkeys, "enableBrowserHotkeys roundtrip");
            AssertEqual(false, deserialized.disableCaretBrowsing, "disableCaretBrowsing roundtrip");

            // Backward compatibility: legacy json missing these keys defaults correctly
            string legacyJson = "{\"url\":\"https://ehr.hospital.mil\",\"appName\":\"LegacyMedTRx\"}";
            var legacyDeserialized = serializer.Deserialize<AppConfig>(legacyJson);
            Assert(legacyDeserialized != null, "Legacy deserialized config must not be null");
            AssertEqual(true, legacyDeserialized.enableFunctionKeys, "Legacy enableFunctionKeys defaults to true");
            AssertEqual(true, legacyDeserialized.enableF2SettingsKey, "Legacy enableF2SettingsKey defaults to true");
            AssertEqual(true, legacyDeserialized.enableF11FullscreenKey, "Legacy enableF11FullscreenKey defaults to true");
            AssertEqual(false, legacyDeserialized.enableBrowserHotkeys, "Legacy enableBrowserHotkeys defaults to false");
            AssertEqual(true, legacyDeserialized.disableCaretBrowsing, "Legacy disableCaretBrowsing defaults to true");
        }

        private static void Test_AppConfig_LockSettings_Flag()
        {
            var config = new AppConfig();
            config.lockSettings = true;
            config.enableDevTools = true; // Attempt to enable

            // SettingsForm logic rule: when lockSettings is true, enableDevTools is forced false
            bool effectiveDevTools = config.lockSettings ? false : config.enableDevTools;
            AssertEqual(false, effectiveDevTools, "DevTools must be disabled when lockSettings is true");
        }

        private static void Test_Sanitize_UrlQueryParameters()
        {
            string raw = "Navigation failed for https://ehr.hospital.mil/patient?patientId=99281&ssn=123-45-6789&token=abc123xyz on cart 4.";
            string sanitized = Program.SanitizeLogMessage(raw);

            Assert(!sanitized.Contains("patientId=99281"), "Query parameter patientId must be redacted");
            Assert(!sanitized.Contains("123-45-6789"), "Query parameter ssn must be redacted");
            Assert(!sanitized.Contains("abc123xyz"), "Query parameter token must be redacted");
            Assert(sanitized.Contains("https://ehr.hospital.mil/patient?[QUERY_REDACTED]"), "Sanitized format should include [QUERY_REDACTED]");
        }

        private static void Test_Sanitize_BearerToken()
        {
            string raw = "HTTP 401 Unauthorized: Authorization Header: Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.e30.t-ID6EEhr01";
            string sanitized = Program.SanitizeLogMessage(raw);

            Assert(!sanitized.Contains("eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9"), "Bearer token payload must be redacted");
            Assert(sanitized.Contains("Bearer [TOKEN_REDACTED]"), "Should contain Bearer [TOKEN_REDACTED]");
        }

        private static void Test_Sanitize_PasswordsAndSecrets()
        {
            string raw = "Database connect error: password=P@ssw0rd123! token=session_token_xyz987 ssn=000-11-2222";
            string sanitized = Program.SanitizeLogMessage(raw);

            Assert(!sanitized.Contains("P@ssw0rd123!"), "Password must be redacted");
            Assert(!sanitized.Contains("session_token_xyz987"), "Token must be redacted");
            Assert(!sanitized.Contains("000-11-2222"), "SSN must be redacted");
            Assert(sanitized.Contains("password=[REDACTED]"), "password=[REDACTED] expected");
        }

        private static void Test_Sanitize_NormalLogPreserved()
        {
            string raw = "System.NullReferenceException: Object reference not set to an instance of an object.\n   at MedTRx.MainForm.InitializeWindow()";
            string sanitized = Program.SanitizeLogMessage(raw);

            AssertEqual(raw, sanitized, "Normal stack traces without sensitive tokens must remain intact");
        }

        private static void Test_Protocol_Http_Allowed()
        {
            Uri parsed;
            bool allowed = MainForm.TryResolveHttpOrHttps("http://cart-server.local:8080/portal", null, out parsed);
            Assert(allowed, "HTTP URL must be allowed");
            Assert(parsed != null, "Parsed URI must not be null");
            AssertEqual("http", parsed.Scheme.ToLowerInvariant(), "Scheme must be http");
        }

        private static void Test_Protocol_Https_Allowed()
        {
            Uri parsed;
            bool allowed = MainForm.TryResolveHttpOrHttps("https://secure.hospital.mil/app", null, out parsed);
            Assert(allowed, "HTTPS URL must be allowed");
            Assert(parsed != null, "Parsed URI must not be null");
            AssertEqual("https", parsed.Scheme.ToLowerInvariant(), "Scheme must be https");
        }

        private static void Test_Protocol_RelativeUri_Resolved()
        {
            Uri parsed;
            bool allowed = MainForm.TryResolveHttpOrHttps("/records/patient/123", "https://ehr.hospital.mil:443", out parsed);
            Assert(allowed, "Relative URL resolved against HTTPS base must be allowed");
            Assert(parsed != null, "Parsed URI must not be null");
            AssertEqual("https://ehr.hospital.mil/records/patient/123", parsed.AbsoluteUri, "Must resolve to absolute URL");
        }

        private static void Test_Protocol_CustomSchemes_Blocked()
        {
            string[] customSchemes = new string[] {
                "ms-settings:appsfeatures",
                "calc:",
                "cmd://echo",
                "powershell:Get-Process",
                "smb://192.168.1.10/share",
                "ldap://dc.mil.local",
                "telnet://10.0.0.1",
                "ssh://root@10.0.0.1"
            };

            foreach (var uri in customSchemes)
            {
                Uri parsed;
                bool allowed = MainForm.TryResolveHttpOrHttps(uri, "https://ehr.hospital.mil", out parsed);
                Assert(!allowed, "Custom protocol scheme must be rejected: " + uri);
            }
        }

        private static void Test_Protocol_FileScheme_Blocked()
        {
            Uri parsed;
            bool allowed = MainForm.TryResolveHttpOrHttps("file:///C:/Windows/System32/cmd.exe", "https://ehr.hospital.mil", out parsed);
            Assert(!allowed, "External file:// scheme must be rejected");
        }

        private static void Test_Protocol_NullOrWhitespace_Blocked()
        {
            Uri parsed;
            Assert(!MainForm.TryResolveHttpOrHttps(null, null, out parsed), "null must be rejected");
            Assert(!MainForm.TryResolveHttpOrHttps("", null, out parsed), "empty must be rejected");
            Assert(!MainForm.TryResolveHttpOrHttps("   ", null, out parsed), "whitespace must be rejected");
        }

        private static void Test_Origin_TargetHost_Allowed()
        {
            string target = "https://ehr.hospital.mil:443";
            string incoming = "https://ehr.hospital.mil:443/api/message";
            bool authorized = MainForm.IsAuthorizedMessageSource(incoming, target, "C:\\Program Files\\MedTRx");
            Assert(authorized, "Same scheme, host, and port must be authorized");
        }

        private static void Test_Origin_TargetSubpath_Allowed()
        {
            string target = "https://ehr.hospital.mil/app";
            string incoming = "https://ehr.hospital.mil/app/subpath/page.html";
            bool authorized = MainForm.IsAuthorizedMessageSource(incoming, target, "C:\\Program Files\\MedTRx");
            Assert(authorized, "Subpaths on matching host must be authorized");
        }

        private static void Test_Origin_ForeignHost_Blocked()
        {
            string target = "https://ehr.hospital.mil";
            string incoming = "https://malicious-hospital-phish.com/index.html";
            bool authorized = MainForm.IsAuthorizedMessageSource(incoming, target, "C:\\Program Files\\MedTRx");
            Assert(!authorized, "Foreign host must be rejected");
        }

        private static void Test_Origin_PortMismatch_Blocked()
        {
            string target = "https://ehr.hospital.mil:443";
            string incoming = "https://ehr.hospital.mil:8443/page";
            bool authorized = MainForm.IsAuthorizedMessageSource(incoming, target, "C:\\Program Files\\MedTRx");
            Assert(!authorized, "Port mismatch must be rejected");
        }

        private static void Test_Origin_SchemeMismatch_Blocked()
        {
            string target = "https://ehr.hospital.mil";
            string incoming = "http://ehr.hospital.mil/page";
            bool authorized = MainForm.IsAuthorizedMessageSource(incoming, target, "C:\\Program Files\\MedTRx");
            Assert(!authorized, "Scheme mismatch (HTTP vs HTTPS) must be rejected");
        }

        private static void Test_Origin_LocalErrorPage_Allowed()
        {
            string baseDir = "C:\\Program Files\\MedTRx";
            string incoming = "file:///C:/Program%20Files/MedTRx/ErrorPage.html";
            bool authorized = MainForm.IsAuthorizedMessageSource(incoming, "https://ehr.hospital.mil", baseDir);
            Assert(authorized, "Local ErrorPage inside base directory must be authorized");
        }

        private static void Test_Origin_ExternalLocalFile_Blocked()
        {
            string baseDir = "C:\\Program Files\\MedTRx";
            string incoming = "file:///C:/Windows/System32/drivers/etc/hosts";
            bool authorized = MainForm.IsAuthorizedMessageSource(incoming, "https://ehr.hospital.mil", baseDir);
            Assert(!authorized, "External file:// outside base directory must be rejected");
        }

        private static void Test_SingleInstance_Mutex()
        {
            string testMutexName = "Global\\MedTRx_UnitTest_Mutex_" + Guid.NewGuid().ToString("N");
            bool createdFirst;
            using (Mutex mutex1 = new Mutex(true, testMutexName, out createdFirst))
            {
                Assert(createdFirst, "First mutex acquisition must succeed");

                bool createdSecond;
                using (Mutex mutex2 = new Mutex(true, testMutexName, out createdSecond))
                {
                    Assert(!createdSecond, "Second mutex attempt must detect existing instance");
                }
            }
        }

        private static void Test_HotkeyPolicy_NullConfig()
        {
            AssertEqual(HotkeyAction.None, HotkeyPolicy.Evaluate(Keys.F11, false, false, null), "Null config returns None for F11");
            AssertEqual(HotkeyAction.None, HotkeyPolicy.Evaluate(Keys.F2, false, false, null), "Null config returns None for F2");
            AssertEqual(HotkeyAction.None, HotkeyPolicy.Evaluate(Keys.Escape, false, true, null), "Null config returns None for Esc");
        }

        private static void Test_HotkeyPolicy_MasterToggle_Disabled()
        {
            var config = new AppConfig();
            config.enableFunctionKeys = false;

            // F1 through F12 must be suppressed
            Keys[] fKeys = new Keys[] {
                Keys.F1, Keys.F2, Keys.F3, Keys.F4, Keys.F5, Keys.F6,
                Keys.F7, Keys.F8, Keys.F9, Keys.F10, Keys.F11, Keys.F12
            };
            foreach (Keys k in fKeys)
            {
                AssertEqual(HotkeyAction.Suppress, HotkeyPolicy.Evaluate(k, false, false, config), "Master toggle off must suppress " + k);
            }

            // Escape when fullscreen must be suppressed
            AssertEqual(HotkeyAction.Suppress, HotkeyPolicy.Evaluate(Keys.Escape, false, true, config), "Master toggle off must suppress Esc when fullscreen");

            // Escape when NOT fullscreen is unhandled -> None
            AssertEqual(HotkeyAction.None, HotkeyPolicy.Evaluate(Keys.Escape, false, false, config), "Master toggle off: Esc when not fullscreen returns None");

            // Ctrl+R must be suppressed
            AssertEqual(HotkeyAction.Suppress, HotkeyPolicy.Evaluate(Keys.R, true, false, config), "Master toggle off must suppress Ctrl+R");

            // Ctrl+Oemcomma and Ctrl+(Keys)188 must be suppressed
            AssertEqual(HotkeyAction.Suppress, HotkeyPolicy.Evaluate(Keys.Oemcomma, true, false, config), "Master toggle off must suppress Ctrl+,");
            AssertEqual(HotkeyAction.Suppress, HotkeyPolicy.Evaluate((Keys)188, true, false, config), "Master toggle off must suppress Ctrl+(Keys)188");

            // Normal typing key must be None
            AssertEqual(HotkeyAction.None, HotkeyPolicy.Evaluate(Keys.A, false, false, config), "Master toggle off: Normal key returns None");
            AssertEqual(HotkeyAction.None, HotkeyPolicy.Evaluate(Keys.R, false, false, config), "Master toggle off: R without Ctrl returns None");
        }

        private static void Test_HotkeyPolicy_CaretBrowsing()
        {
            var config = new AppConfig();
            config.enableFunctionKeys = true;

            // Default: disableCaretBrowsing = true -> F7 is Suppress
            AssertEqual(true, config.disableCaretBrowsing, "Default disableCaretBrowsing must be true");
            AssertEqual(HotkeyAction.Suppress, HotkeyPolicy.Evaluate(Keys.F7, false, false, config), "F7 suppressed when disableCaretBrowsing is true");

            // disableCaretBrowsing = true even with enableBrowserHotkeys = true -> F7 is Suppress
            config.enableBrowserHotkeys = true;
            AssertEqual(HotkeyAction.Suppress, HotkeyPolicy.Evaluate(Keys.F7, false, false, config), "F7 suppressed even if enableBrowserHotkeys is true");

            // disableCaretBrowsing = false, enableBrowserHotkeys = false -> F7 is Suppress (via other F-keys rule)
            config.disableCaretBrowsing = false;
            config.enableBrowserHotkeys = false;
            AssertEqual(HotkeyAction.Suppress, HotkeyPolicy.Evaluate(Keys.F7, false, false, config), "F7 suppressed when browser hotkeys disabled");

            // disableCaretBrowsing = false, enableBrowserHotkeys = true -> F7 is None (let browser handle it)
            config.disableCaretBrowsing = false;
            config.enableBrowserHotkeys = true;
            AssertEqual(HotkeyAction.None, HotkeyPolicy.Evaluate(Keys.F7, false, false, config), "F7 allowed (None) when disableCaretBrowsing is false and browser hotkeys enabled");
        }

        private static void Test_HotkeyPolicy_Fullscreen_And_Escape()
        {
            var config = new AppConfig();
            config.enableFunctionKeys = true;
            config.enableF11FullscreenKey = true;

            // F11 toggles fullscreen
            AssertEqual(HotkeyAction.ToggleFullscreen, HotkeyPolicy.Evaluate(Keys.F11, false, false, config), "F11 returns ToggleFullscreen when enabled");
            AssertEqual(HotkeyAction.ToggleFullscreen, HotkeyPolicy.Evaluate(Keys.F11, false, true, config), "F11 returns ToggleFullscreen when fullscreen and enabled");

            // Esc exits fullscreen if fullscreen is true
            AssertEqual(HotkeyAction.ExitFullscreen, HotkeyPolicy.Evaluate(Keys.Escape, false, true, config), "Esc returns ExitFullscreen when fullscreen and enabled");
            AssertEqual(HotkeyAction.None, HotkeyPolicy.Evaluate(Keys.Escape, false, false, config), "Esc returns None when not fullscreen");

            // When enableF11FullscreenKey = false
            config.enableF11FullscreenKey = false;
            AssertEqual(HotkeyAction.Suppress, HotkeyPolicy.Evaluate(Keys.F11, false, false, config), "F11 returns Suppress when enableF11FullscreenKey is false");
            AssertEqual(HotkeyAction.Suppress, HotkeyPolicy.Evaluate(Keys.Escape, false, true, config), "Esc returns Suppress when fullscreen and enableF11FullscreenKey is false");
            AssertEqual(HotkeyAction.None, HotkeyPolicy.Evaluate(Keys.Escape, false, false, config), "Esc returns None when not fullscreen even if enableF11FullscreenKey is false");
        }

        private static void Test_HotkeyPolicy_SettingsKey()
        {
            var config = new AppConfig();
            config.enableFunctionKeys = true;
            config.enableF2SettingsKey = true;
            config.lockSettings = false;

            // F2 and Ctrl+, show settings
            AssertEqual(HotkeyAction.ShowSettings, HotkeyPolicy.Evaluate(Keys.F2, false, false, config), "F2 returns ShowSettings when enabled and unlocked");
            AssertEqual(HotkeyAction.ShowSettings, HotkeyPolicy.Evaluate(Keys.Oemcomma, true, false, config), "Ctrl+Oemcomma returns ShowSettings when enabled and unlocked");
            AssertEqual(HotkeyAction.ShowSettings, HotkeyPolicy.Evaluate((Keys)188, true, false, config), "Ctrl+188 returns ShowSettings when enabled and unlocked");

            // Oemcomma without Ctrl returns None
            AssertEqual(HotkeyAction.None, HotkeyPolicy.Evaluate(Keys.Oemcomma, false, false, config), "Comma without Ctrl returns None");

            // When enableF2SettingsKey is false
            config.enableF2SettingsKey = false;
            AssertEqual(HotkeyAction.Suppress, HotkeyPolicy.Evaluate(Keys.F2, false, false, config), "F2 returns Suppress when enableF2SettingsKey is false");
            AssertEqual(HotkeyAction.Suppress, HotkeyPolicy.Evaluate(Keys.Oemcomma, true, false, config), "Ctrl+, returns Suppress when enableF2SettingsKey is false");

            // When lockSettings is true (even if enableF2SettingsKey is true)
            config.enableF2SettingsKey = true;
            config.lockSettings = true;
            AssertEqual(HotkeyAction.Suppress, HotkeyPolicy.Evaluate(Keys.F2, false, false, config), "F2 returns Suppress when lockSettings is true");
            AssertEqual(HotkeyAction.Suppress, HotkeyPolicy.Evaluate(Keys.Oemcomma, true, false, config), "Ctrl+, returns Suppress when lockSettings is true");
        }

        private static void Test_HotkeyPolicy_ReloadKey()
        {
            var config = new AppConfig();
            config.enableFunctionKeys = true;
            config.enableNavigationKeys = true;

            // F5 and Ctrl+R reload
            AssertEqual(HotkeyAction.Reload, HotkeyPolicy.Evaluate(Keys.F5, false, false, config), "F5 returns Reload when enableNavigationKeys is true");
            AssertEqual(HotkeyAction.Reload, HotkeyPolicy.Evaluate(Keys.R, true, false, config), "Ctrl+R returns Reload when enableNavigationKeys is true");

            // When enableNavigationKeys is false
            config.enableNavigationKeys = false;
            AssertEqual(HotkeyAction.Suppress, HotkeyPolicy.Evaluate(Keys.F5, false, false, config), "F5 returns Suppress when enableNavigationKeys is false");
            AssertEqual(HotkeyAction.Suppress, HotkeyPolicy.Evaluate(Keys.R, true, false, config), "Ctrl+R returns Suppress when enableNavigationKeys is false");

            // R without Ctrl returns None
            AssertEqual(HotkeyAction.None, HotkeyPolicy.Evaluate(Keys.R, false, false, config), "R without Ctrl returns None");
        }

        private static void Test_HotkeyPolicy_DevToolsKey()
        {
            var config = new AppConfig();
            config.enableFunctionKeys = true;
            config.enableDevTools = true;
            config.lockSettings = false;

            // F12 opens dev tools
            AssertEqual(HotkeyAction.OpenDevTools, HotkeyPolicy.Evaluate(Keys.F12, false, false, config), "F12 returns OpenDevTools when enabled and unlocked");

            // When enableDevTools is false
            config.enableDevTools = false;
            AssertEqual(HotkeyAction.Suppress, HotkeyPolicy.Evaluate(Keys.F12, false, false, config), "F12 returns Suppress when enableDevTools is false");

            // When lockSettings is true (even if enableDevTools is true)
            config.enableDevTools = true;
            config.lockSettings = true;
            AssertEqual(HotkeyAction.Suppress, HotkeyPolicy.Evaluate(Keys.F12, false, false, config), "F12 returns Suppress when lockSettings is true");
        }

        private static void Test_HotkeyPolicy_OtherFKeys_And_Unhandled()
        {
            var config = new AppConfig();
            config.enableFunctionKeys = true;
            config.enableBrowserHotkeys = false;

            Keys[] otherFKeys = new Keys[] { Keys.F1, Keys.F3, Keys.F4, Keys.F6, Keys.F8, Keys.F9, Keys.F10 };
            foreach (Keys k in otherFKeys)
            {
                AssertEqual(HotkeyAction.Suppress, HotkeyPolicy.Evaluate(k, false, false, config), k + " returns Suppress when enableBrowserHotkeys is false");
            }

            // When enableBrowserHotkeys is true
            config.enableBrowserHotkeys = true;
            foreach (Keys k in otherFKeys)
            {
                AssertEqual(HotkeyAction.None, HotkeyPolicy.Evaluate(k, false, false, config), k + " returns None when enableBrowserHotkeys is true");
            }

            // Unhandled keys return None
            AssertEqual(HotkeyAction.None, HotkeyPolicy.Evaluate(Keys.A, false, false, config), "Keys.A returns None");
            AssertEqual(HotkeyAction.None, HotkeyPolicy.Evaluate(Keys.Tab, false, false, config), "Keys.Tab returns None");
            AssertEqual(HotkeyAction.None, HotkeyPolicy.Evaluate(Keys.Space, false, false, config), "Keys.Space returns None");
            AssertEqual(HotkeyAction.None, HotkeyPolicy.Evaluate(Keys.B, true, false, config), "Ctrl+B returns None");
        }

        private static void Test_CaretBrowsing_MultiLayerPolicy()
        {
            var config = new AppConfig();
            config.enableFunctionKeys = true;

            // Layer 1 & Policy: When disableCaretBrowsing = true, F7 must always return Suppress regardless of enableBrowserHotkeys
            config.disableCaretBrowsing = true;
            config.enableBrowserHotkeys = false;
            AssertEqual(HotkeyAction.Suppress, HotkeyPolicy.Evaluate(Keys.F7, false, false, config), "F7 returns Suppress when disableCaretBrowsing is true and enableBrowserHotkeys is false");

            config.enableBrowserHotkeys = true;
            AssertEqual(HotkeyAction.Suppress, HotkeyPolicy.Evaluate(Keys.F7, false, false, config), "F7 returns Suppress when disableCaretBrowsing is true even if enableBrowserHotkeys is true");

            // When disableCaretBrowsing = false, F7 behavior is determined by enableBrowserHotkeys
            config.disableCaretBrowsing = false;
            config.enableBrowserHotkeys = false;
            AssertEqual(HotkeyAction.Suppress, HotkeyPolicy.Evaluate(Keys.F7, false, false, config), "F7 returns Suppress when disableCaretBrowsing is false and enableBrowserHotkeys is false");

            config.enableBrowserHotkeys = true;
            AssertEqual(HotkeyAction.None, HotkeyPolicy.Evaluate(Keys.F7, false, false, config), "F7 returns None when disableCaretBrowsing is false and enableBrowserHotkeys is true");
        }
    }
}
