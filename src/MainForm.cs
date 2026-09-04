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

                // CoreWebView2Environment with isolated user data folder
                var env = await CoreWebView2Environment.CreateAsync(null, userDataFolder);
                await webView.EnsureCoreWebView2Async(env);

                // Configure WebView2 Settings
                var settings = webView.CoreWebView2.Settings;
                settings.AreDevToolsEnabled = config.enableDevTools;
                settings.IsStatusBarEnabled = false;
                settings.IsZoomControlEnabled = true;
                settings.AreDefaultContextMenusEnabled = true;

                // Set zoom factor
                if (config.zoomFactor > 0.1 && config.zoomFactor < 5.0)
                {
                    webView.ZoomFactor = config.zoomFactor;
                }

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
            e.Handled = true;
            if (config.allowExternalLinks)
            {
                try
                {
                    System.Diagnostics.Process.Start(e.Uri);
                }
                catch
                {
                    webView.CoreWebView2.Navigate(e.Uri);
                }
            }
            else
            {
                webView.CoreWebView2.Navigate(e.Uri);
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
