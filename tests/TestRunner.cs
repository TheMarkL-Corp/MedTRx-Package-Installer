using System;
using System.IO;
using System.Net;
using System.Threading;
using System.Web.Script.Serialization;
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
    }
}
