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

            // Apply Always-on-Top & Initial Window State
            this.TopMost = config.alwaysOnTop;

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

                // Caret Browsing Suppression: Layer 1 - Chromium feature flag switch
                if (config.disableCaretBrowsing)
                {
                    if (options == null)
                    {
                        options = new CoreWebView2EnvironmentOptions();
                    }
                    if (string.IsNullOrEmpty(options.AdditionalBrowserArguments))
                    {
                        options.AdditionalBrowserArguments = "--disable-features=CaretBrowsing";
                    }
                    else
                    {
                        options.AdditionalBrowserArguments += " --disable-features=CaretBrowsing";
                    }
                }

                // CoreWebView2Environment with isolated user data folder and optional turbo args
                var env = await CoreWebView2Environment.CreateAsync(browserExecutableFolder, userDataFolder, options);
                await webView.EnsureCoreWebView2Async(env);

                // Dual-Layer Hotkey Interception: Hook CoreWebView2Controller accelerator key events
                CoreWebView2Controller controller = GetCoreWebView2Controller();
                if (controller != null)
                {
                    controller.AcceleratorKeyPressed += CoreWebView2Controller_AcceleratorKeyPressed;
                }

                // Configure WebView2 Settings
                var settings = webView.CoreWebView2.Settings;
                settings.AreDevToolsEnabled = config.enableDevTools && !config.lockSettings;
                settings.IsStatusBarEnabled = false;
                settings.IsZoomControlEnabled = true;
                settings.AreDefaultContextMenusEnabled = true;
                settings.AreBrowserAcceleratorKeysEnabled = config.enableBrowserHotkeys;

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

                // Caret Browsing Suppression: Layer 3 - DOM Keydown Event Trap
                if (config.disableCaretBrowsing)
                {
                    try
                    {
                        await webView.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync(
                            "(function() { window.addEventListener('keydown', function(e) { if (e.key === 'F7' || e.keyCode === 118) { e.preventDefault(); e.stopPropagation(); } }, true); })();"
                        );
                    }
                    catch { }
                }

                // Touchscreen Fullscreen Sidebar injection
                if (config.touchFullscreenSidebar)
                {
                    try
                    {
                        string sidebarScript = GetTouchSidebarInjectionScript(isFullscreen);
                        await webView.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync(sidebarScript);
                    }
                    catch { }
                }

                // Set zoom factor
                if (config.zoomFactor > 0.1 && config.zoomFactor < 5.0)
                {
                    webView.ZoomFactor = config.zoomFactor;
                }

                // Top Loading Indicator & Navigation Scheme Validation
                webView.NavigationStarting += new EventHandler<CoreWebView2NavigationStartingEventArgs>(WebView_NavigationStarting);

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
                string setupExe = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "MicrosoftEdgeWebview2Setup.exe");
                if (File.Exists(setupExe))
                {
                    DialogResult result = MessageBox.Show(
                        "Microsoft Edge WebView2 Runtime is required to run MedTRx, but was not found on this computer.\n\n" +
                        "An offline installer was found in the application directory. Would you like MedTRx to install it now?",
                        "Microsoft Edge WebView2 Required",
                        MessageBoxButtons.YesNo,
                        MessageBoxIcon.Question
                    );

                    if (result == DialogResult.Yes)
                    {
                        TryInstallWebView2OfflineAndRestart(setupExe);
                        return;
                    }
                }
                else
                {
                    MessageBox.Show(
                        "Microsoft Edge WebView2 Runtime was not detected on this machine.\n\n" +
                        "For air-gapped/secure hospital deployments, please deploy the 100% Offline Fixed-Version Bundle (containing runtime/) " +
                        "or place MicrosoftEdgeWebview2Setup.exe into the application folder.",
                        "WebView2 Runtime Missing",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning
                    );
                }
            }
            else
            {
                MessageBox.Show(
                    "Error initializing WebView2 runtime: " + ex.Message + "\n\nPlease ensure Microsoft Edge WebView2 Runtime is installed.",
                    "WebView2 Initialization Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error
                );
            }
        }

        private void TryInstallWebView2OfflineAndRestart(string setupExe)
        {
            try
            {
                if (File.Exists(setupExe))
                {
                    this.Cursor = Cursors.WaitCursor;
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
                    "Could not install offline WebView2: " + ex.Message,
                    "Installation Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error
                );
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

        private bool TryResolveHttpOrHttps(string uriString, out Uri parsedUri)
        {
            return TryResolveHttpOrHttps(uriString, config != null ? config.url : null, out parsedUri);
        }

        internal static bool TryResolveHttpOrHttps(string uriString, string configuredBaseUrl, out Uri parsedUri)
        {
            parsedUri = null;
            if (string.IsNullOrWhiteSpace(uriString))
            {
                return false;
            }

            if (Uri.TryCreate(uriString, UriKind.Absolute, out parsedUri))
            {
                return parsedUri.Scheme.Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) ||
                       parsedUri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase);
            }

            if (!string.IsNullOrEmpty(configuredBaseUrl))
            {
                Uri baseUri;
                if (Uri.TryCreate(configuredBaseUrl, UriKind.Absolute, out baseUri))
                {
                    if (Uri.TryCreate(baseUri, uriString, out parsedUri))
                    {
                        return parsedUri.Scheme.Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) ||
                               parsedUri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase);
                    }
                }
            }

            return false;
        }

        private void WebView_NavigationStarting(object sender, CoreWebView2NavigationStartingEventArgs e)
        {
            if (topProgressBar != null)
            {
                topProgressBar.Visible = true;
            }

            string navUri = e.Uri != null ? e.Uri.Trim() : "";
            if (string.IsNullOrEmpty(navUri)) return;

            // Allow internal WebView2 browser protocols
            if (navUri.StartsWith("about:", StringComparison.OrdinalIgnoreCase) ||
                navUri.StartsWith("blob:", StringComparison.OrdinalIgnoreCase) ||
                navUri.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            // Allow HTTP and HTTPS
            Uri parsedUri;
            if (TryResolveHttpOrHttps(navUri, out parsedUri))
            {
                return;
            }

            // Allow local file:// ONLY if it is inside the application directory (e.g. ErrorPage.html)
            if (Uri.TryCreate(navUri, UriKind.Absolute, out parsedUri) &&
                parsedUri.Scheme.Equals(Uri.UriSchemeFile, StringComparison.OrdinalIgnoreCase))
            {
                string localPath = parsedUri.LocalPath;
                string appDir = AppDomain.CurrentDomain.BaseDirectory.TrimEnd('\\', '/');
                if (!string.IsNullOrEmpty(localPath) && localPath.StartsWith(appDir, StringComparison.OrdinalIgnoreCase))
                {
                    return; // Legitimate local app asset
                }
            }

            // Reject any other custom protocol schemes (e.g. file:// external, ms-*, smb://, cmd:, etc.)
            e.Cancel = true;
            if (topProgressBar != null)
            {
                topProgressBar.Visible = false;
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

        private bool IsAuthorizedMessageSource(string sourceUri)
        {
            return IsAuthorizedMessageSource(sourceUri, config != null ? config.url : null, AppDomain.CurrentDomain.BaseDirectory);
        }

        internal static bool IsAuthorizedMessageSource(string sourceUri, string configuredUrl, string baseDirectory)
        {
            if (string.IsNullOrWhiteSpace(sourceUri)) return false;

            Uri source;
            if (Uri.TryCreate(sourceUri, UriKind.Absolute, out source))
            {
                // 1. Allow local application ErrorPage.html or local assets
                if (source.Scheme.Equals(Uri.UriSchemeFile, StringComparison.OrdinalIgnoreCase))
                {
                    string localPath = source.LocalPath;
                    string appDir = !string.IsNullOrEmpty(baseDirectory) ? baseDirectory.TrimEnd('\\', '/') : "";
                    return !string.IsNullOrEmpty(localPath) && localPath.StartsWith(appDir, StringComparison.OrdinalIgnoreCase);
                }

                // 2. Allow configured hospital web server origin
                if (!string.IsNullOrEmpty(configuredUrl))
                {
                    Uri target;
                    if (Uri.TryCreate(configuredUrl, UriKind.Absolute, out target))
                    {
                        return source.Scheme.Equals(target.Scheme, StringComparison.OrdinalIgnoreCase) &&
                               source.Host.Equals(target.Host, StringComparison.OrdinalIgnoreCase) &&
                               source.Port == target.Port;
                    }
                }
            }

            return false;
        }

        private void WebView_WebMessageReceived(object sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            try
            {
                // Reject untrusted cross-origin messages
                if (!IsAuthorizedMessageSource(e.Source))
                {
                    return;
                }

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
                    else if (json != null && json.Contains("\"toggle_fullscreen\""))
                    {
                        this.BeginInvoke(new Action(() => ToggleFullscreen()));
                    }
                    else if (json != null && json.Contains("\"reload\""))
                    {
                        this.BeginInvoke(new Action(() => {
                            if (webView != null && webView.CoreWebView2 != null) webView.CoreWebView2.Reload();
                        }));
                    }
                }
                else
                {
                    if (msg.Equals("retry", StringComparison.OrdinalIgnoreCase))
                    {
                        NavigateToTargetUrl();
                    }
                    else if (msg.Equals("settings", StringComparison.OrdinalIgnoreCase) || msg.Equals("open_settings", StringComparison.OrdinalIgnoreCase))
                    {
                        ShowSettingsDialog();
                    }
                    else if (msg.Equals("toggle_fullscreen", StringComparison.OrdinalIgnoreCase))
                    {
                        this.BeginInvoke(new Action(() => ToggleFullscreen()));
                    }
                    else if (msg.Equals("reload", StringComparison.OrdinalIgnoreCase))
                    {
                        this.BeginInvoke(new Action(() => {
                            if (webView != null && webView.CoreWebView2 != null) webView.CoreWebView2.Reload();
                        }));
                    }
                }
            }
            catch { }
        }

        private void CoreWebView2_NewWindowRequested(object sender, CoreWebView2NewWindowRequestedEventArgs e)
        {
            string uri = e.Uri != null ? e.Uri.Trim() : "";

            // 1. Guard against blank / about: protocols - let WebView2 manage internal popup/blank window safely
            if (string.IsNullOrEmpty(uri) || 
                uri.StartsWith("about:", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            // Reject javascript: pseudo-protocol in new window
            if (uri.StartsWith("javascript:", StringComparison.OrdinalIgnoreCase))
            {
                e.Handled = true;
                return;
            }

            // 2. Blob / Data URLs: handle internally within WebView2 (client-side generated reports/previews)
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

            // 3. Scheme Validation: ONLY allow HTTP and HTTPS protocols. Reject any other custom protocol schemes.
            Uri parsedUri;
            if (!TryResolveHttpOrHttps(uri, out parsedUri))
            {
                // Reject custom protocol schemes (e.g. file:, ms-*, smb:, cmd:, calc:, etc.)
                e.Handled = true;
                return;
            }

            string targetUrl = parsedUri.AbsoluteUri;

            // 4. Direct PDF links or requests over HTTP/HTTPS
            if (targetUrl.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase) || 
                targetUrl.IndexOf(".pdf?", StringComparison.OrdinalIgnoreCase) >= 0 || 
                targetUrl.IndexOf("/pdf", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                e.Handled = true;
                OpenPdfViewer(targetUrl);
                return;
            }

            // 5. External HTTP/HTTPS links
            if (config.allowExternalLinks)
            {
                e.Handled = true;
                try
                {
                    System.Diagnostics.ProcessStartInfo psi = new System.Diagnostics.ProcessStartInfo(targetUrl);
                    psi.UseShellExecute = true;
                    System.Diagnostics.Process.Start(psi);
                }
                catch
                {
                    if (webView != null && webView.CoreWebView2 != null)
                    {
                        webView.CoreWebView2.Navigate(targetUrl);
                    }
                }
            }
            else
            {
                e.Handled = true;
                if (webView != null && webView.CoreWebView2 != null)
                {
                    webView.CoreWebView2.Navigate(targetUrl);
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
                        Uri testUri;
                        if (File.Exists(pathOrUrl) || TryResolveHttpOrHttps(pathOrUrl, out testUri))
                        {
                            System.Diagnostics.ProcessStartInfo psi = new System.Diagnostics.ProcessStartInfo("chrome.exe", "\"" + pathOrUrl + "\"");
                            psi.UseShellExecute = true;
                            System.Diagnostics.Process.Start(psi);
                            return;
                        }
                    }
                    catch { } // Fallback to embedded if chrome is not found
                }
                else if (mode == "system")
                {
                    try
                    {
                        Uri testUri;
                        if (File.Exists(pathOrUrl) || TryResolveHttpOrHttps(pathOrUrl, out testUri))
                        {
                            System.Diagnostics.ProcessStartInfo psi = new System.Diagnostics.ProcessStartInfo(pathOrUrl);
                            psi.UseShellExecute = true;
                            System.Diagnostics.Process.Start(psi);
                            return;
                        }
                    }
                    catch { }
                }

                // Default / Embedded: Launch dedicated In-App Chromium PDF Viewer
                string browserFolder = FindBundledRuntime();
                PdfViewerForm viewer = new PdfViewerForm(pathOrUrl, browserFolder, this.TopMost);
                viewer.Owner = this;
                viewer.Show(this);
                viewer.BringToFront();
                viewer.Activate();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Could not open PDF viewer: " + ex.Message, "PDF Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void MainForm_KeyDown(object sender, KeyEventArgs e)
        {
            HotkeyAction action = HotkeyPolicy.Evaluate(e.KeyCode, e.Control, isFullscreen, config);
            if (action != HotkeyAction.None)
            {
                e.Handled = true;
                ExecuteHotkeyAction(action);
            }
        }

        private void CoreWebView2Controller_AcceleratorKeyPressed(object sender, CoreWebView2AcceleratorKeyPressedEventArgs e)
        {
            if (e.KeyEventKind == CoreWebView2KeyEventKind.KeyDown || e.KeyEventKind == CoreWebView2KeyEventKind.SystemKeyDown)
            {
                Keys keyCode = (Keys)e.VirtualKey;
                bool ctrl = (Control.ModifierKeys & Keys.Control) == Keys.Control;
                HotkeyAction action = HotkeyPolicy.Evaluate(keyCode, ctrl, isFullscreen, config);
                if (action != HotkeyAction.None)
                {
                    e.Handled = true;
                    ExecuteHotkeyAction(action);
                }
            }
        }

        private void ExecuteHotkeyAction(HotkeyAction action)
        {
            switch (action)
            {
                case HotkeyAction.ToggleFullscreen:
                    ToggleFullscreen();
                    break;
                case HotkeyAction.ExitFullscreen:
                    SetFullscreen(false);
                    break;
                case HotkeyAction.Reload:
                    if (webView != null && webView.CoreWebView2 != null)
                    {
                        webView.CoreWebView2.Reload();
                    }
                    break;
                case HotkeyAction.ShowSettings:
                    ShowSettingsDialog();
                    break;
                case HotkeyAction.OpenDevTools:
                    if (webView != null && webView.CoreWebView2 != null)
                    {
                        webView.CoreWebView2.OpenDevToolsWindow();
                    }
                    break;
                case HotkeyAction.Suppress:
                    // Suppressed hotkey - do nothing
                    break;
                case HotkeyAction.None:
                default:
                    break;
            }
        }

        private CoreWebView2Controller GetCoreWebView2Controller()
        {
            if (webView == null) return null;
            try
            {
                System.Reflection.PropertyInfo prop = typeof(WebView2).GetProperty(
                    "CoreWebView2Controller",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance
                );
                if (prop != null)
                {
                    return prop.GetValue(webView, null) as CoreWebView2Controller;
                }

                System.Reflection.FieldInfo field = typeof(WebView2).GetField(
                    "_coreWebView2Controller",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance
                );
                if (field != null)
                {
                    return field.GetValue(webView) as CoreWebView2Controller;
                }
            }
            catch { }
            return null;
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
                this.TopMost = config.alwaysOnTop;
                isFullscreen = true;
                NotifyFullscreenStateChanged(true);
            }
            else
            {
                this.FormBorderStyle = previousBorderStyle;
                this.WindowState = previousWindowState;
                if (previousWindowState == FormWindowState.Normal)
                {
                    this.Bounds = previousBounds;
                }
                this.TopMost = config.alwaysOnTop;
                isFullscreen = false;
                NotifyFullscreenStateChanged(false);
            }
        }

        private void NotifyFullscreenStateChanged(bool fullscreen)
        {
            try
            {
                if (webView != null && webView.CoreWebView2 != null)
                {
                    string js = string.Format("if (window.__medtrxSetFullscreenState) {{ window.__medtrxSetFullscreenState({0}); }}", fullscreen ? "true" : "false");
                    webView.CoreWebView2.ExecuteScriptAsync(js);
                }
            }
            catch { }
        }

        public void ShowSettingsDialog()
        {
            if (config != null && config.lockSettings)
            {
                if (string.IsNullOrEmpty(config.adminPassword))
                {
                    MessageBox.Show(
                        "MedTRx application settings are locked by your hospital system administrator.",
                        "Settings Locked",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information
                    );
                    return;
                }
                else
                {
                    string inputPwd = PromptAdminPassword();
                    if (inputPwd == null)
                    {
                        return; // User cancelled
                    }
                    if (!inputPwd.Equals(config.adminPassword))
                    {
                        MessageBox.Show(
                            "Incorrect administrator password. Access denied.",
                            "Authentication Failed",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Warning
                        );
                        return;
                    }
                }
            }

            using (var settingsForm = new SettingsForm(config))
            {
                settingsForm.TopMost = this.TopMost;
                if (settingsForm.ShowDialog(this) == DialogResult.OK && settingsForm.Saved)
                {
                    this.config = settingsForm.Config;
                    this.Text = config.appName;
                    this.TopMost = config.alwaysOnTop;
                    if (webView != null && webView.CoreWebView2 != null)
                    {
                        webView.CoreWebView2.Settings.AreDevToolsEnabled = config.enableDevTools && !config.lockSettings;
                        NavigateToTargetUrl();
                    }
                }
            }
        }

        private string PromptAdminPassword()
        {
            using (Form prompt = new Form())
            {
                prompt.Width = 360;
                prompt.Height = 160;
                prompt.FormBorderStyle = FormBorderStyle.FixedDialog;
                prompt.Text = "Administrator Authentication";
                prompt.StartPosition = FormStartPosition.CenterParent;
                prompt.MaximizeBox = false;
                prompt.MinimizeBox = false;
                prompt.TopMost = this.TopMost;
                prompt.BackColor = Color.FromArgb(248, 250, 252);
                prompt.Font = new Font("Segoe UI", 9.5f, FontStyle.Regular);

                Label textLabel = new Label() { Left = 20, Top = 16, Text = "Enter Administrator Password:", AutoSize = true, Font = new Font("Segoe UI", 9.5f, FontStyle.Bold) };
                TextBox textBox = new TextBox() { Left = 20, Top = 40, Width = 300, PasswordChar = '●' };
                Button confirmation = new Button() { Text = "Unlock", Left = 150, Width = 80, Top = 76, DialogResult = DialogResult.OK, BackColor = Color.FromArgb(14, 116, 144), ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
                confirmation.FlatAppearance.BorderSize = 0;
                Button cancel = new Button() { Text = "Cancel", Left = 240, Width = 80, Top = 76, DialogResult = DialogResult.Cancel, BackColor = Color.FromArgb(226, 232, 240), FlatStyle = FlatStyle.Flat };
                cancel.FlatAppearance.BorderSize = 0;

                confirmation.Click += (sender, e) => { prompt.Close(); };
                cancel.Click += (sender, e) => { prompt.Close(); };

                prompt.Controls.Add(textLabel);
                prompt.Controls.Add(textBox);
                prompt.Controls.Add(confirmation);
                prompt.Controls.Add(cancel);
                prompt.AcceptButton = confirmation;
                prompt.CancelButton = cancel;

                return prompt.ShowDialog(this) == DialogResult.OK ? textBox.Text : null;
            }
        }

        private string GetTouchSidebarInjectionScript(bool startFullscreenState)
        {
            return @"
(function() {
    if (window.__medtrxSidebarInjected) return;
    window.__medtrxSidebarInjected = true;

    var isFullscreen = " + (startFullscreenState ? "true" : "false") + @";
    var isExpanded = false;
    var side = 'right'; // 'right' or 'left'
    var topPos = 32;    // vertical pixels from top
    var autoHideTimer = null;
    var isDragging = false;
    var dragStartY = 0;
    var initialTop = 0;
    var hasMoved = false;

    // Load saved position
    try {
        var saved = localStorage.getItem('medtrx_sidebar_cfg');
        if (saved) {
            var cfg = JSON.parse(saved);
            if (cfg.side) side = cfg.side;
            if (typeof cfg.top === 'number') topPos = cfg.top;
        }
    } catch(e) {}

    function savePosition() {
        try {
            localStorage.setItem('medtrx_sidebar_cfg', JSON.stringify({ side: side, top: topPos }));
        } catch(e) {}
    }

    function createElements() {
        if (document.getElementById('medtrx-touch-container')) return;

        var host = document.createElement('div');
        host.id = 'medtrx-touch-container';

        var style = document.createElement('style');
        style.textContent = `
            #medtrx-touch-container {
                position: fixed;
                z-index: 2147483647;
                font-family: -apple-system, BlinkMacSystemFont, 'SF Pro Text', 'Segoe UI', Roboto, Helvetica, Arial, sans-serif;
                user-select: none;
                -webkit-user-select: none;
                right: 0px;
                top: ` + topPos + `px;
                display: flex;
                align-items: flex-start;
                transition: transform 0.32s cubic-bezier(0.16, 1, 0.3, 1), opacity 0.3s ease;
                touch-action: none;
            }
            #medtrx-touch-container.medtrx-left {
                right: auto;
                left: 0px;
                flex-direction: row-reverse;
            }
            #medtrx-touch-container.medtrx-collapsed-right {
                transform: translateX(192px);
            }
            #medtrx-touch-container.medtrx-collapsed-left {
                transform: translateX(-192px);
            }
            #medtrx-touch-container.medtrx-dragging {
                transition: none !important;
                opacity: 0.95 !important;
            }
            #medtrx-touch-container.medtrx-idle:not(:hover) {
                opacity: 0.22;
            }
            #medtrx-touch-container:hover, #medtrx-touch-container:active {
                opacity: 1 !important;
            }

            /* Liquid Frosted Glass Grab Tab */
            #medtrx-touch-tab {
                width: 26px;
                height: 52px;
                background: rgba(255, 255, 255, 0.2);
                backdrop-filter: blur(20px) saturate(190%);
                -webkit-backdrop-filter: blur(20px) saturate(190%);
                border: 1px solid rgba(255, 255, 255, 0.35);
                border-right: none;
                border-radius: 12px 0 0 12px;
                display: flex;
                flex-direction: column;
                align-items: center;
                justify-content: center;
                cursor: grab;
                box-shadow: -4px 6px 20px rgba(0, 0, 0, 0.18), inset 0 1px 0 rgba(255, 255, 255, 0.4);
                color: rgba(255, 255, 255, 0.92);
                transition: background 0.2s ease, transform 0.15s ease, box-shadow 0.2s ease;
                touch-action: none;
            }
            #medtrx-touch-container.medtrx-left #medtrx-touch-tab {
                border-radius: 0 12px 12px 0;
                border-left: none;
                border-right: 1px solid rgba(255, 255, 255, 0.35);
                box-shadow: 4px 6px 20px rgba(0, 0, 0, 0.18), inset 0 1px 0 rgba(255, 255, 255, 0.4);
            }
            #medtrx-touch-tab:hover {
                background: rgba(255, 255, 255, 0.3);
                color: #ffffff;
            }
            #medtrx-touch-tab:active, #medtrx-touch-container.medtrx-dragging #medtrx-touch-tab {
                cursor: grabbing;
                background: rgba(14, 116, 144, 0.45);
                border-color: rgba(56, 189, 248, 0.6);
            }
            .medtrx-tab-icon {
                font-size: 13px;
                line-height: 1;
                filter: drop-shadow(0 1px 2px rgba(0,0,0,0.5));
            }
            .medtrx-tab-arrow {
                font-size: 9px;
                line-height: 1;
                margin-top: 4px;
                opacity: 0.85;
                transition: transform 0.25s ease;
            }

            /* Liquid Frosted Glass Slide Panel */
            #medtrx-touch-panel {
                width: 192px;
                background: rgba(15, 23, 42, 0.58);
                backdrop-filter: blur(28px) saturate(210%);
                -webkit-backdrop-filter: blur(28px) saturate(210%);
                border: 1px solid rgba(255, 255, 255, 0.18);
                border-radius: 0 0 0 16px;
                box-shadow: 0 16px 45px rgba(0, 0, 0, 0.45), inset 0 1px 0 rgba(255, 255, 255, 0.25);
                padding: 10px;
                box-sizing: border-box;
                display: flex;
                flex-direction: column;
                gap: 8px;
            }
            #medtrx-touch-container.medtrx-left #medtrx-touch-panel {
                border-radius: 0 0 16px 0;
            }

            /* Touch Friendly Glass Buttons */
            .medtrx-glass-btn {
                background: rgba(255, 255, 255, 0.09);
                color: #ffffff;
                border: 1px solid rgba(255, 255, 255, 0.16);
                border-radius: 10px;
                padding: 11px 8px;
                font-size: 13px;
                font-weight: 600;
                letter-spacing: 0.2px;
                text-align: center;
                cursor: pointer;
                display: flex;
                align-items: center;
                justify-content: center;
                gap: 8px;
                white-space: nowrap;
                touch-action: manipulation;
                box-shadow: 0 2px 8px rgba(0,0,0,0.15), inset 0 1px 0 rgba(255,255,255,0.18);
                transition: all 0.18s cubic-bezier(0.16, 1, 0.3, 1);
            }
            .medtrx-glass-btn:active {
                transform: scale(0.96);
                background: rgba(14, 116, 144, 0.5);
                border-color: rgba(56, 189, 248, 0.5);
            }
            .medtrx-glass-btn-primary {
                background: linear-gradient(135deg, rgba(14, 116, 144, 0.8), rgba(6, 182, 212, 0.65));
                border: 1px solid rgba(56, 189, 248, 0.45);
                font-size: 13.5px;
                font-weight: 700;
                box-shadow: 0 4px 16px rgba(6, 182, 212, 0.25), inset 0 1px 0 rgba(255, 255, 255, 0.35);
            }
            .medtrx-glass-btn-primary:active {
                background: linear-gradient(135deg, rgba(14, 116, 144, 0.95), rgba(6, 182, 212, 0.85));
            }
            .medtrx-glass-row {
                display: flex;
                gap: 6px;
            }
            .medtrx-glass-row .medtrx-glass-btn {
                flex: 1;
                padding: 8px 4px;
                font-size: 11.5px;
                font-weight: 500;
            }
        `;

        var tab = document.createElement('div');
        tab.id = 'medtrx-touch-tab';
        tab.title = 'Drag to reposition vertically • Tap to open';
        tab.innerHTML = '<span class=""medtrx-tab-icon"">⛶</span><span id=""medtrx-touch-tab-arrow"" class=""medtrx-tab-arrow"">◀</span>';

        var panel = document.createElement('div');
        panel.id = 'medtrx-touch-panel';

        // 1. Toggle Fullscreen Button
        var btnFullscreen = document.createElement('button');
        btnFullscreen.id = 'medtrx-btn-fullscreen';
        btnFullscreen.className = 'medtrx-glass-btn medtrx-glass-btn-primary';
        btnFullscreen.innerHTML = isFullscreen ? '<span>🗗</span> Exit Fullscreen' : '<span>⛶</span> Enter Fullscreen';
        btnFullscreen.title = isFullscreen ? 'Exit Fullscreen (F11 or Esc)' : 'Enter Fullscreen (F11)';

        // 2. Secondary Row: Reload & Swap Corner
        var secRow = document.createElement('div');
        secRow.className = 'medtrx-glass-row';

        var btnReload = document.createElement('button');
        btnReload.className = 'medtrx-glass-btn';
        btnReload.innerHTML = '🔄 Reload';
        btnReload.title = 'Reload web page (F5)';

        var btnSwap = document.createElement('button');
        btnSwap.id = 'medtrx-btn-swap';
        btnSwap.className = 'medtrx-glass-btn';
        btnSwap.innerHTML = (side === 'right' ? '← Dock Left' : 'Dock Right →');
        btnSwap.title = (side === 'right' ? 'Move panel to left screen edge' : 'Move panel to right screen edge');

        secRow.appendChild(btnReload);
        secRow.appendChild(btnSwap);

        panel.appendChild(btnFullscreen);
        panel.appendChild(secRow);

        host.appendChild(tab);
        host.appendChild(panel);
        document.body.appendChild(style);
        document.body.appendChild(host);

        // Apply initial layout & idle dimming
        updateSidebarClass();
        startIdleTimer();

        // Pointer / Touch Dragging & Click Handling
        tab.addEventListener('pointerdown', function(e) {
            isDragging = true;
            hasMoved = false;
            dragStartY = e.clientY;
            initialTop = topPos;
            host.classList.add('medtrx-dragging');
            host.classList.remove('medtrx-idle');
            try { tab.setPointerCapture(e.pointerId); } catch(err) {}
            e.preventDefault();
        });

        tab.addEventListener('pointermove', function(e) {
            if (!isDragging) return;
            var deltaY = e.clientY - dragStartY;
            if (Math.abs(deltaY) > 5) {
                hasMoved = true;
            }
            if (hasMoved) {
                var maxTop = window.innerHeight - 80;
                var newTop = Math.max(16, Math.min(maxTop, initialTop + deltaY));
                topPos = Math.round(newTop);
                host.style.top = topPos + 'px';
            }
        });

        tab.addEventListener('pointerup', function(e) {
            if (!isDragging) return;
            isDragging = false;
            host.classList.remove('medtrx-dragging');
            try { tab.releasePointerCapture(e.pointerId); } catch(err) {}

            if (hasMoved) {
                savePosition();
                startIdleTimer();
            } else {
                // It was a tap -> toggle open/close
                toggleSidebar();
            }
        });

        tab.addEventListener('pointercancel', function() {
            isDragging = false;
            host.classList.remove('medtrx-dragging');
        });

        // Panel Actions
        btnFullscreen.addEventListener('click', function(e) {
            e.stopPropagation();
            sendHostMessage('toggle_fullscreen');
            resetAutoHide();
        });

        btnReload.addEventListener('click', function(e) {
            e.stopPropagation();
            sendHostMessage('reload');
            collapseSidebar();
        });

        btnSwap.addEventListener('click', function(e) {
            e.stopPropagation();
            side = (side === 'right' ? 'left' : 'right');
            updateSidebarClass();
            savePosition();
            resetAutoHide();
        });

        panel.addEventListener('pointerdown', function() { resetAutoHide(); });
        panel.addEventListener('click', function() { resetAutoHide(); });

        document.addEventListener('click', function(e) {
            if (isExpanded && !host.contains(e.target)) {
                collapseSidebar();
            }
        });

        // Window resize boundary guard
        window.addEventListener('resize', function() {
            var maxTop = window.innerHeight - 80;
            if (topPos > maxTop) {
                topPos = Math.max(16, maxTop);
                host.style.top = topPos + 'px';
                savePosition();
            }
        });
    }

    function sendHostMessage(msg) {
        try {
            if (window.chrome && window.chrome.webview) {
                window.chrome.webview.postMessage(msg);
            }
        } catch(e) {}
    }

    function toggleSidebar() {
        if (isExpanded) {
            collapseSidebar();
        } else {
            expandSidebar();
        }
    }

    function expandSidebar() {
        isExpanded = true;
        updateSidebarClass();
        resetAutoHide();
    }

    function collapseSidebar() {
        isExpanded = false;
        updateSidebarClass();
        startIdleTimer();
    }

    function resetAutoHide() {
        var host = document.getElementById('medtrx-touch-container');
        if (host) host.classList.remove('medtrx-idle');
        if (autoHideTimer) clearTimeout(autoHideTimer);
        autoHideTimer = setTimeout(function() {
            if (isExpanded) {
                collapseSidebar();
            } else {
                startIdleTimer();
            }
        }, 4000);
    }

    function startIdleTimer() {
        if (autoHideTimer) clearTimeout(autoHideTimer);
        autoHideTimer = setTimeout(function() {
            var host = document.getElementById('medtrx-touch-container');
            if (host && !isExpanded && !isDragging) {
                host.classList.add('medtrx-idle');
            }
        }, 3000);
    }

    function updateSidebarClass() {
        var host = document.getElementById('medtrx-touch-container');
        if (!host) return;

        host.classList.remove('medtrx-left', 'medtrx-collapsed-right', 'medtrx-collapsed-left', 'medtrx-idle');

        if (side === 'left') {
            host.classList.add('medtrx-left');
            if (!isExpanded) host.classList.add('medtrx-collapsed-left');
        } else {
            if (!isExpanded) host.classList.add('medtrx-collapsed-right');
        }

        var arrow = document.getElementById('medtrx-touch-tab-arrow');
        if (arrow) {
            if (side === 'right') {
                arrow.textContent = isExpanded ? '▶' : '◀';
            } else {
                arrow.textContent = isExpanded ? '◀' : '▶';
            }
        }

        var btnSwap = document.getElementById('medtrx-btn-swap');
        if (btnSwap) {
            btnSwap.innerHTML = (side === 'right' ? '← Dock Left' : 'Dock Right →');
            btnSwap.title = (side === 'right' ? 'Move panel to left screen edge' : 'Move panel to right screen edge');
        }
    }

    window.__medtrxSetFullscreenState = function(fullscreen) {
        isFullscreen = fullscreen;
        var btn = document.getElementById('medtrx-btn-fullscreen');
        if (btn) {
            btn.innerHTML = isFullscreen ? '<span>🗗</span> Exit Fullscreen' : '<span>⛶</span> Enter Fullscreen';
            btn.title = isFullscreen ? 'Exit Fullscreen (F11 or Esc)' : 'Enter Fullscreen (F11)';
        }
    };

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', createElements);
    } else {
        createElements();
    }
})();
";
        }
    }
}
