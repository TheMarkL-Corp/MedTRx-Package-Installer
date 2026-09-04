using System;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace MedTRx
{
    public class MainForm : Form
    {
        private WebView2 webView;
        private AppConfig config;
        private bool isFullscreen = false;
        private FormWindowState previousWindowState = FormWindowState.Normal;
        private FormBorderStyle previousBorderStyle = FormBorderStyle.Sizable;
        private Rectangle previousBounds;
        private string appDataPath;
        private bool isErrorState = false;
        private ProgressBar topProgressBar;

        public MainForm(AppConfig initialConfig)
        {
            this.config = initialConfig;
            this.appDataPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "MedTRx"
            );

            InitializeWindow();
            InitializeWebViewAsync();
        }

        private void InitializeWindow()
        {
            this.Text = !string.IsNullOrEmpty(config.appName) ? config.appName : "MedTRx";
            this.Size = new Size(1280, 800);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.BackColor = Color.White;
            this.KeyPreview = true;

            // Sleek Top Loading Indicator (3px)
            topProgressBar = new ProgressBar();
            topProgressBar.Dock = DockStyle.Top;
            topProgressBar.Height = 3;
            topProgressBar.Style = ProgressBarStyle.Marquee;
            topProgressBar.MarqueeAnimationSpeed = 30;
            topProgressBar.Visible = false;
            this.Controls.Add(topProgressBar);

            // Load Application Icon
            try
            {
                string iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logo.ico");
                if (File.Exists(iconPath))
                {
                    this.Icon = new Icon(iconPath);
                }
            }
            catch { }

            // Apply Initial Window State
            if (config.startFullscreen)
            {
                SetFullscreen(true);
            }
            else if (config.startMaximized)
            {
                this.WindowState = FormWindowState.Maximized;
            }

            // Keyboard Shortcuts
            this.KeyDown += new KeyEventHandler(MainForm_KeyDown);
        }

        private async void InitializeWebViewAsync()
        {
            webView = new WebView2();
            webView.Dock = DockStyle.Fill;
            webView.DefaultBackgroundColor = Color.White;
            this.Controls.Add(webView);

            try
            {
                string userDataFolder = Path.Combine(appDataPath, "UserData");
                if (!Directory.Exists(userDataFolder))
                {
                    Directory.CreateDirectory(userDataFolder);
                }

                // Check for bundled Fixed Version runtime (100% offline mode)
                string browserExecutableFolder = FindBundledRuntime();

                // Turbocharger Options
                CoreWebView2EnvironmentOptions options = null;
                if (config.turboMode)
                {
                    options = new CoreWebView2EnvironmentOptions();
                    options.AdditionalBrowserArguments = 
                        "--enable-gpu-rasterization " +
                        "--enable-zero-copy " +
                        "--ignore-gpu-blocklist " +
                        "--enable-features=CanvasOopRasterization,ParallelDownloading,Prerender2 " +
                        "--disk-cache-size=209715200 " +
                        "--disable-background-timer-throttling";
                }

                // CoreWebView2Environment with isolated user data folder and optional turbo args
                var env = await CoreWebView2Environment.CreateAsync(browserExecutableFolder, userDataFolder, options);
                await webView.EnsureCoreWebView2Async(env);

                // Configure WebView2 Settings
                var settings = webView.CoreWebView2.Settings;
                settings.AreDevToolsEnabled = config.enableDevTools;
                settings.IsStatusBarEnabled = false;
                settings.IsZoomControlEnabled = true;
                settings.AreDefaultContextMenusEnabled = true;

                // Turbocharger DOM acceleration injection
                try
                {
                    await webView.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync(
                        "(function() {" +
                        "  var style = document.createElement('style');" +
                        "  style.textContent = '* { -webkit-font-smoothing: antialiased; } img, svg { content-visibility: auto; }';" +
                        "  if (document.head) { document.head.appendChild(style); }" +
                        "  else { document.addEventListener('DOMContentLoaded', function() { if (document.head) document.head.appendChild(style); }); }" +
                        "})();"
                    );
                }
                catch { }

                // Set zoom factor
                if (config.zoomFactor > 0.1 && config.zoomFactor < 5.0)
                {
                    webView.ZoomFactor = config.zoomFactor;
                }

                // Top Loading Indicator
                webView.NavigationStarting += (s, e) =>
                {
                    if (topProgressBar != null) topProgressBar.Visible = true;
                };

                // Title update from webpage
                webView.CoreWebView2.DocumentTitleChanged += (s, e) =>
                {
                    if (webView != null && webView.CoreWebView2 != null && !string.IsNullOrEmpty(webView.CoreWebView2.DocumentTitle) && !isErrorState)
                    {
                        this.Text = webView.CoreWebView2.DocumentTitle + " - " + config.appName;
                    }
                };

                // Navigation completed / error handling
                webView.NavigationCompleted += new EventHandler<CoreWebView2NavigationCompletedEventArgs>(WebView_NavigationCompleted);

                // Handle web messages from error page (retry or open settings)
                webView.WebMessageReceived += new EventHandler<CoreWebView2WebMessageReceivedEventArgs>(WebView_WebMessageReceived);

                // Handle popup links / external links
                webView.CoreWebView2.NewWindowRequested += new EventHandler<CoreWebView2NewWindowRequestedEventArgs>(CoreWebView2_NewWindowRequested);

                // Handle PDF and file downloads
                webView.CoreWebView2.DownloadStarting += new EventHandler<CoreWebView2DownloadStartingEventArgs>(CoreWebView2_DownloadStarting);

                // Navigate to Target URL
                NavigateToTargetUrl();
            }
            catch (Exception ex)
            {
                HandleWebView2InitFailure(ex);
            }
        }

        private void HandleWebView2InitFailure(Exception ex)
        {
            string msg = ex.ToString();
            bool isMissingRuntime = msg.IndexOf("WebView2RuntimeNotFoundException", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                    msg.IndexOf("Couldn't find a compatible Webview2 Runtime", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                    msg.IndexOf("WebView2", StringComparison.OrdinalIgnoreCase) >= 0;

            if (isMissingRuntime)
            {
                DialogResult result = MessageBox.Show(
                    "Microsoft Edge WebView2 Evergreen Runtime is required to run MedTRx, but was not found on this computer.\n\n" +
                    "Would you like MedTRx to automatically install it now?",
                    "Microsoft Edge WebView2 Required",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question
                );

                if (result == DialogResult.Yes)
                {
                    TryInstallWebView2AndRestart();
                    return;
                }
                else
                {
                    try
                    {
                        System.Diagnostics.Process.Start("https://go.microsoft.com/fwlink/p/?LinkId=2124703");
                    }
                    catch { }
                }
            }
            else
            {
                MessageBox.Show(
                    "Error initializing WebView2 runtime: " + ex.Message + "\n\nPlease ensure Microsoft Edge WebView2 Evergreen Runtime is installed.",
                    "WebView2 Initialization Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error
                );
            }
        }

        private void TryInstallWebView2AndRestart()
        {
            try
            {
                this.Cursor = Cursors.WaitCursor;
                string setupExe = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "MicrosoftEdgeWebview2Setup.exe");
                if (!File.Exists(setupExe))
                {
                    setupExe = Path.Combine(Path.GetTempPath(), "MicrosoftEdgeWebview2Setup.exe");
                    System.Net.ServicePointManager.SecurityProtocol = (System.Net.SecurityProtocolType)3072;
                    using (System.Net.WebClient client = new System.Net.WebClient())
                    {
                        client.DownloadFile("https://go.microsoft.com/fwlink/p/?LinkId=2124703", setupExe);
                    }
                }

                if (File.Exists(setupExe))
                {
                    System.Diagnostics.ProcessStartInfo startInfo = new System.Diagnostics.ProcessStartInfo();
                    startInfo.FileName = setupExe;
                    startInfo.Arguments = "/silent /install";
                    startInfo.UseShellExecute = true;

                    System.Diagnostics.Process proc = System.Diagnostics.Process.Start(startInfo);
                    if (proc != null)
                    {
                        proc.WaitForExit();
                    }

                    this.Cursor = Cursors.Default;
                    MessageBox.Show(
                        "Microsoft Edge WebView2 Runtime installation completed! MedTRx will now restart.",
                        "Installation Complete",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information
                    );
                    Application.Restart();
                    Environment.Exit(0);
                    return;
                }
            }
            catch (Exception ex)
            {
                this.Cursor = Cursors.Default;
                MessageBox.Show(
                    "Could not automatically install WebView2: " + ex.Message + "\n\nOpening Microsoft download page in browser...",
                    "Installation Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning
                );
                try
                {
                    System.Diagnostics.Process.Start("https://go.microsoft.com/fwlink/p/?LinkId=2124703");
                }
                catch { }
            }
        }

        private string FindBundledRuntime()
        {
            try
            {
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                string[] candidates = new string[]
                {
                    Path.Combine(baseDir, "runtime"),
                    Path.Combine(baseDir, "FixedVersionRuntime"),
                    Path.Combine(baseDir, "WebView2Runtime")
                };

                foreach (string dir in candidates)
                {
                    if (Directory.Exists(dir) && File.Exists(Path.Combine(dir, "msedgewebview2.exe")))
                    {
                        return dir;
                    }
                }

                // Check 1-level subdirectories (e.g. if extracted with version name like 152.0.4191.62)
                foreach (string sub in Directory.GetDirectories(baseDir))
                {
                    if (File.Exists(Path.Combine(sub, "msedgewebview2.exe")))
                    {
                        return sub;
                    }
                }
            }
            catch { }

            return null; // Fallback to system-wide Evergreen runtime
        }

        private void NavigateToTargetUrl()
        {
            if (string.IsNullOrEmpty(config.url))
            {
                ShowSettingsDialog();
                return;
            }

            isErrorState = false;
            try
            {
                webView.CoreWebView2.Navigate(config.url);
            }
            catch
            {
                LoadLocalErrorPage(config.url);
            }
        }

        private void WebView_NavigationCompleted(object sender, CoreWebView2NavigationCompletedEventArgs e)
        {
            if (topProgressBar != null)
            {
                topProgressBar.Visible = false;
            }

            if (!e.IsSuccess)
            {
                isErrorState = true;
                this.Text = "Connection Error - " + config.appName;
                LoadLocalErrorPage(config.url);
            }
            else
            {
                isErrorState = false;
            }
        }

        private void LoadLocalErrorPage(string failedUrl)
        {
            try
            {
                string errorHtmlPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "src", "ErrorPage.html");
                if (!File.Exists(errorHtmlPath))
                {
                    errorHtmlPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ErrorPage.html");
                }

                if (File.Exists(errorHtmlPath))
                {
                    string safeUrl = Uri.EscapeDataString(failedUrl != null ? failedUrl : "");
                    webView.CoreWebView2.Navigate(new Uri(errorHtmlPath).AbsoluteUri + "?target=" + safeUrl);
                }
                else
                {
                    webView.CoreWebView2.NavigateToString(
                        "<html><body style='font-family:sans-serif;padding:40px;text-align:center;background:#f1f5f9;'>" +
                        "<h2>Unable to connect to MedTRx server</h2>" +
                        "<p>Target URL: " + failedUrl + "</p>" +
                        "<p>Press <b>F5</b> to retry or <b>Ctrl+,</b> for settings.</p></body></html>"
                    );
                }
            }
            catch { }
        }

        private void WebView_WebMessageReceived(object sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            try
            {
                string msg = e.TryGetWebMessageAsString();
                if (string.IsNullOrEmpty(msg))
                {
                    string json = e.WebMessageAsJson;
                    if (json != null && json.Contains("\"retry\""))
                    {
                        NavigateToTargetUrl();
                    }
                    else if (json != null && json.Contains("\"settings\""))
                    {
                        ShowSettingsDialog();
                    }
                }
                else
                {
                    if (msg.Equals("retry", StringComparison.OrdinalIgnoreCase))
                    {
                        NavigateToTargetUrl();
                    }
                    else if (msg.Equals("settings", StringComparison.OrdinalIgnoreCase))
                    {
                        ShowSettingsDialog();
                    }
                }
            }
            catch { }
        }

        private void CoreWebView2_NewWindowRequested(object sender, CoreWebView2NewWindowRequestedEventArgs e)
        {
            string uri = e.Uri != null ? e.Uri.Trim() : "";

            // 1. Guard against blank / about: protocols - NEVER call Process.Start on about: links
            if (string.IsNullOrEmpty(uri) || 
                uri.StartsWith("about:", StringComparison.OrdinalIgnoreCase) || 
                uri.StartsWith("javascript:", StringComparison.OrdinalIgnoreCase))
            {
                // Let WebView2 manage internal popup/blank window without triggering Windows Shell error
                return;
            }

            // 2. Direct PDF links or requests
            if (uri.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase) || 
                uri.IndexOf(".pdf?", StringComparison.OrdinalIgnoreCase) >= 0 || 
                uri.IndexOf("/pdf", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                e.Handled = true;
                OpenPdfViewer(uri);
                return;
            }

            // 3. Blob / Data URLs: handle internally within WebView2
            if (uri.StartsWith("blob:", StringComparison.OrdinalIgnoreCase) || 
                uri.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
            {
                e.Handled = true;
                if (webView != null && webView.CoreWebView2 != null)
                {
                    webView.CoreWebView2.Navigate(uri);
                }
                return;
            }

            // 4. External HTTP/HTTPS links
            if (config.allowExternalLinks)
            {
                e.Handled = true;
                try
                {
                    System.Diagnostics.ProcessStartInfo psi = new System.Diagnostics.ProcessStartInfo(uri);
                    psi.UseShellExecute = true;
                    System.Diagnostics.Process.Start(psi);
                }
                catch
                {
                    if (webView != null && webView.CoreWebView2 != null)
                    {
                        webView.CoreWebView2.Navigate(uri);
                    }
                }
            }
            else
            {
                e.Handled = true;
                if (webView != null && webView.CoreWebView2 != null)
                {
                    webView.CoreWebView2.Navigate(uri);
                }
            }
        }

        private void CoreWebView2_DownloadStarting(object sender, CoreWebView2DownloadStartingEventArgs e)
        {
            string targetFile = e.ResultFilePath;
            if (string.IsNullOrEmpty(targetFile)) return;

            e.DownloadOperation.StateChanged += (s, args) =>
            {
                if (e.DownloadOperation.State == CoreWebView2DownloadState.Completed)
                {
                    if (config.autoOpenPdf && 
                        targetFile.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase) && 
                        File.Exists(targetFile))
                    {
                        this.BeginInvoke(new Action(() =>
                        {
                            OpenPdfViewer(targetFile);
                        }));
                    }
                }
            };
        }

        public void OpenPdfViewer(string pathOrUrl)
        {
            try
            {
                string mode = !string.IsNullOrEmpty(config.pdfViewerMode) ? config.pdfViewerMode.ToLowerInvariant() : "embedded";
                
                if (mode == "chrome")
                {
                    try
                    {
                        System.Diagnostics.ProcessStartInfo psi = new System.Diagnostics.ProcessStartInfo("chrome.exe", "\"" + pathOrUrl + "\"");
                        psi.UseShellExecute = true;
                        System.Diagnostics.Process.Start(psi);
                        return;
                    }
                    catch { } // Fallback to embedded if chrome is not found
                }
                else if (mode == "system")
                {
                    try
                    {
                        System.Diagnostics.ProcessStartInfo psi = new System.Diagnostics.ProcessStartInfo(pathOrUrl);
                        psi.UseShellExecute = true;
                        System.Diagnostics.Process.Start(psi);
                        return;
                    }
                    catch { }
                }

                // Default / Embedded: Launch dedicated In-App Chromium PDF Viewer
                string browserFolder = FindBundledRuntime();
                PdfViewerForm viewer = new PdfViewerForm(pathOrUrl, browserFolder);
                viewer.Show(this);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Could not open PDF viewer: " + ex.Message, "PDF Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void MainForm_KeyDown(object sender, KeyEventArgs e)
        {
            // F11: Toggle Fullscreen / Windowed
            if (e.KeyCode == Keys.F11)
            {
                e.Handled = true;
                ToggleFullscreen();
                return;
            }

            // ESC: Exit Fullscreen
            if (e.KeyCode == Keys.Escape && isFullscreen)
            {
                e.Handled = true;
                SetFullscreen(false);
                return;
            }

            // F5 or Ctrl+R: Reload
            if (config.enableNavigationKeys && (e.KeyCode == Keys.F5 || (e.Control && e.KeyCode == Keys.R)))
            {
                e.Handled = true;
                if (webView != null && webView.CoreWebView2 != null)
                {
                    webView.CoreWebView2.Reload();
                }
                return;
            }

            // F2 or Ctrl+,: Settings
            if (e.KeyCode == Keys.F2 || (e.Control && e.KeyCode == Keys.Oemcomma))
            {
                e.Handled = true;
                ShowSettingsDialog();
                return;
            }

            // F12: DevTools
            if (e.KeyCode == Keys.F12 && config.enableDevTools)
            {
                e.Handled = true;
                if (webView != null && webView.CoreWebView2 != null)
                {
                    webView.CoreWebView2.OpenDevToolsWindow();
                }
                return;
            }
        }

        public void ToggleFullscreen()
        {
            SetFullscreen(!isFullscreen);
        }

        public void SetFullscreen(bool fullscreen)
        {
            if (isFullscreen == fullscreen) return;

            if (fullscreen)
            {
                previousWindowState = this.WindowState;
                previousBorderStyle = this.FormBorderStyle;
                previousBounds = this.Bounds;

                this.FormBorderStyle = FormBorderStyle.None;
                this.WindowState = FormWindowState.Normal;
                this.Bounds = Screen.FromControl(this).Bounds;
                this.TopMost = false;
                isFullscreen = true;
            }
            else
            {
                this.FormBorderStyle = previousBorderStyle;
                this.WindowState = previousWindowState;
                if (previousWindowState == FormWindowState.Normal)
                {
                    this.Bounds = previousBounds;
                }
                isFullscreen = false;
            }
        }

        public void ShowSettingsDialog()
        {
            using (var settingsForm = new SettingsForm(config))
            {
                if (settingsForm.ShowDialog(this) == DialogResult.OK && settingsForm.Saved)
                {
                    this.config = settingsForm.Config;
                    this.Text = config.appName;
                    if (webView != null && webView.CoreWebView2 != null)
                    {
                        webView.CoreWebView2.Settings.AreDevToolsEnabled = config.enableDevTools;
                        NavigateToTargetUrl();
                    }
                }
            }
        }
    }
}
