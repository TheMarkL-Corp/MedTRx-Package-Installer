using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace MedTRx
{
    internal static class Program
    {
        private const string AppUserModelId = "MedTRx.MedicalApp.Client";
        private const string MutexName = "Global\\MedTRx_SingleInstance_Mutex";

        [DllImport("shell32.dll", SetLastError = true)]
        private static extern void SetCurrentProcessExplicitAppUserModelID([MarshalAs(UnmanagedType.LPWStr)] string AppID);

        [DllImport("user32.dll")]
        private static extern bool SetProcessDPIAware();

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        private const int SW_RESTORE = 9;

        [STAThread]
        private static void Main(string[] args)
        {
            // 1. Single Instance Mutex
            bool createdNew;
            using (Mutex mutex = new Mutex(true, MutexName, out createdNew))
            {
                if (!createdNew)
                {
                    // Another instance is already running - focus it
                    BringExistingInstanceToFront();
                    return;
                }

                // 2. High-DPI Awareness for crisp display
                try
                {
                    if (Environment.OSVersion.Version.Major >= 6)
                    {
                        SetProcessDPIAware();
                    }
                }
                catch { }

                // 3. Set Windows AppUserModelID for proper taskbar grouping & icon resolution
                try
                {
                    SetCurrentProcessExplicitAppUserModelID(AppUserModelId);
                }
                catch { }

                // 4. Windows Forms Setup
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);

                // 5. Global Exception Handlers
                SetupExceptionHandling();

                // 6. Load Config & Launch
                AppConfig config = ConfigManager.Load();

                // If launched with command-line arguments (e.g. MedTRx.exe "https://...")
                if (args.Length > 0 && !string.IsNullOrWhiteSpace(args[0]))
                {
                    string arg = args[0].Trim();
                    if (arg.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                        arg.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                    {
                        config.url = arg;
                    }
                }

                Application.Run(new MainForm(config));
            }
        }

        private static void BringExistingInstanceToFront()
        {
            Process current = Process.GetCurrentProcess();
            foreach (Process process in Process.GetProcessesByName(current.ProcessName))
            {
                if (process.Id != current.Id && process.MainWindowHandle != IntPtr.Zero)
                {
                    ShowWindow(process.MainWindowHandle, SW_RESTORE);
                    SetForegroundWindow(process.MainWindowHandle);
                    break;
                }
            }
        }

        private static void SetupExceptionHandling()
        {
            Application.ThreadException += (s, e) =>
            {
                LogCrash(e.Exception);
                MessageBox.Show(
                    "An unexpected error occurred in MedTRx:\n" + e.Exception.Message + "\n\nSee crash.log for details.",
                    "MedTRx Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error
                );
            };

            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
            {
                Exception ex = e.ExceptionObject as Exception;
                if (ex != null)
                {
                    LogCrash(ex);
                }
            };
        }

        private static void LogCrash(Exception ex)
        {
            try
            {
                string logPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "MedTRx",
                    "crash.log"
                );
                string dir = Path.GetDirectoryName(logPath);
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

                string sanitized = SanitizeLogMessage(ex != null ? ex.ToString() : "Unknown exception");
                File.AppendAllText(
                    logPath,
                    "[" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "] " + sanitized + Environment.NewLine + Environment.NewLine
                );
            }
            catch { }
        }

        internal static string SanitizeLogMessage(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";

            // Redact URL query parameters (e.g. ?patientId=..., ?token=...)
            text = System.Text.RegularExpressions.Regex.Replace(
                text, 
                @"(https?://[^\s""'>]+)\?([^\s""'>]+)", 
                "$1?[QUERY_REDACTED]", 
                System.Text.RegularExpressions.RegexOptions.IgnoreCase
            );

            // Redact Bearer tokens, passwords, session secrets
            text = System.Text.RegularExpressions.Regex.Replace(
                text, 
                @"(Bearer\s+)[A-Za-z0-9\-_\.=]+", 
                "$1[TOKEN_REDACTED]", 
                System.Text.RegularExpressions.RegexOptions.IgnoreCase
            );
            text = System.Text.RegularExpressions.Regex.Replace(
                text, 
                @"(password|pwd|secret|auth|token|sessionid|ssn)\s*[:=]\s*[^,\s;&]+", 
                "$1=[REDACTED]", 
                System.Text.RegularExpressions.RegexOptions.IgnoreCase
            );

            return text;
        }
    }
}
