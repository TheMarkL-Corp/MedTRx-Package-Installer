using System;
using System.Drawing;
using System.Windows.Forms;

namespace MedTRx
{
    public class SettingsForm : Form
    {
        private TextBox txtUrl;
        private TextBox txtAppName;
        private CheckBox chkStartMaximized;
        private CheckBox chkStartFullscreen;
        private CheckBox chkAlwaysOnTop;
        private CheckBox chkTouchSidebar;
        private CheckBox chkFunctionKeys;
        private CheckBox chkF11Fullscreen;
        private CheckBox chkNavigationKeys;
        private CheckBox chkF2Settings;
        private CheckBox chkEnableDevTools;
        private CheckBox chkLockSettings;
        private TextBox txtAdminPassword;
        private Button btnSave;
        private Button btnCancel;

        public AppConfig Config { get; private set; }
        public bool Saved { get; private set; }

        public SettingsForm(AppConfig currentConfig)
        {
            this.Saved = false;
            this.Config = currentConfig != null ? currentConfig : new AppConfig();
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            this.Text = "MedTRx - Application Configuration";
            this.Size = new Size(540, 600);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.StartPosition = FormStartPosition.CenterScreen;
            this.Font = new Font("Segoe UI", 9.5f, FontStyle.Regular);
            this.BackColor = Color.FromArgb(248, 250, 252);

            try
            {
                string iconPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logo.ico");
                if (System.IO.File.Exists(iconPath))
                {
                    this.Icon = new Icon(iconPath);
                }
            }
            catch { }

            Panel headerPanel = new Panel();
            headerPanel.Dock = DockStyle.Top;
            headerPanel.Height = 65;
            headerPanel.BackColor = Color.FromArgb(14, 116, 144);

            Label lblHeaderTitle = new Label();
            lblHeaderTitle.Text = "MedTRx Settings";
            lblHeaderTitle.Font = new Font("Segoe UI", 13f, FontStyle.Bold);
            lblHeaderTitle.ForeColor = Color.White;
            lblHeaderTitle.Location = new Point(20, 12);
            lblHeaderTitle.AutoSize = true;

            Label lblHeaderSub = new Label();
            lblHeaderSub.Text = "Configure the embedded web application server URL and display settings";
            lblHeaderSub.Font = new Font("Segoe UI", 8.5f, FontStyle.Regular);
            lblHeaderSub.ForeColor = Color.FromArgb(224, 242, 254);
            lblHeaderSub.Location = new Point(21, 38);
            lblHeaderSub.AutoSize = true;

            headerPanel.Controls.Add(lblHeaderTitle);
            headerPanel.Controls.Add(lblHeaderSub);
            this.Controls.Add(headerPanel);

            int startY = 80;

            // URL
            Label lblUrl = new Label();
            lblUrl.Text = "Web Application URL:";
            lblUrl.Location = new Point(20, startY);
            lblUrl.AutoSize = true;
            lblUrl.Font = new Font("Segoe UI", 9.5f, FontStyle.Bold);
            this.Controls.Add(lblUrl);

            txtUrl = new TextBox();
            txtUrl.Location = new Point(20, startY + 22);
            txtUrl.Width = 480;
            txtUrl.Text = Config.url != null ? Config.url : "";
            this.Controls.Add(txtUrl);

            Label lblUrlHint = new Label();
            lblUrlHint.Text = "Example: https://medtrx.hospital.local or https://app.medtrx.com";
            lblUrlHint.Location = new Point(20, startY + 50);
            lblUrlHint.ForeColor = Color.Gray;
            lblUrlHint.Font = new Font("Segoe UI", 8f);
            lblUrlHint.AutoSize = true;
            this.Controls.Add(lblUrlHint);

            // App Name
            Label lblAppName = new Label();
            lblAppName.Text = "Application / Window Title:";
            lblAppName.Location = new Point(20, startY + 75);
            lblAppName.AutoSize = true;
            lblAppName.Font = new Font("Segoe UI", 9.5f, FontStyle.Bold);
            this.Controls.Add(lblAppName);

            txtAppName = new TextBox();
            txtAppName.Location = new Point(20, startY + 97);
            txtAppName.Width = 480;
            txtAppName.Text = !string.IsNullOrEmpty(Config.appName) ? Config.appName : "MedTRx";
            this.Controls.Add(txtAppName);

            // Checkboxes
            chkStartMaximized = new CheckBox();
            chkStartMaximized.Text = "Start Maximized";
            chkStartMaximized.Location = new Point(20, startY + 130);
            chkStartMaximized.AutoSize = true;
            chkStartMaximized.Checked = Config.startMaximized;
            this.Controls.Add(chkStartMaximized);

            chkStartFullscreen = new CheckBox();
            chkStartFullscreen.Text = "Start in Fullscreen / Kiosk Mode (F11 toggles anytime)";
            chkStartFullscreen.Location = new Point(20, startY + 154);
            chkStartFullscreen.AutoSize = true;
            chkStartFullscreen.Checked = Config.startFullscreen;
            this.Controls.Add(chkStartFullscreen);

            chkAlwaysOnTop = new CheckBox();
            chkAlwaysOnTop.Text = "Keep Window Always on Top (Pins above taskbars & windows)";
            chkAlwaysOnTop.Location = new Point(20, startY + 178);
            chkAlwaysOnTop.AutoSize = true;
            chkAlwaysOnTop.Checked = Config.alwaysOnTop;
            chkAlwaysOnTop.Font = new Font("Segoe UI", 9.5f, FontStyle.Regular);
            this.Controls.Add(chkAlwaysOnTop);

            chkTouchSidebar = new CheckBox();
            chkTouchSidebar.Text = "Show Touchscreen Fullscreen Sidebar (Upper-right corner tab)";
            chkTouchSidebar.Location = new Point(20, startY + 202);
            chkTouchSidebar.AutoSize = true;
            chkTouchSidebar.Checked = Config.touchFullscreenSidebar;
            chkTouchSidebar.Font = new Font("Segoe UI", 9.5f, FontStyle.Regular);
            this.Controls.Add(chkTouchSidebar);

            // Function Hotkey Hierarchy (Master toggle & fine-grained overrides)
            chkFunctionKeys = new CheckBox();
            chkFunctionKeys.Text = "Enable Physical Keyboard Function Hotkeys (F1–F12, Esc)";
            chkFunctionKeys.Font = new Font("Segoe UI", 9.5f, FontStyle.Bold);
            chkFunctionKeys.Location = new Point(20, startY + 230);
            chkFunctionKeys.AutoSize = true;
            chkFunctionKeys.Checked = Config.enableFunctionKeys;
            this.Controls.Add(chkFunctionKeys);

            chkF11Fullscreen = new CheckBox();
            chkF11Fullscreen.Text = "Allow F11 Fullscreen Toggle (Esc always exits)";
            chkF11Fullscreen.Location = new Point(40, startY + 254);
            chkF11Fullscreen.AutoSize = true;
            chkF11Fullscreen.Checked = Config.enableF11FullscreenKey;
            chkF11Fullscreen.Font = new Font("Segoe UI", 9.5f, FontStyle.Regular);
            this.Controls.Add(chkF11Fullscreen);

            chkNavigationKeys = new CheckBox();
            chkNavigationKeys.Text = "Allow F5 & Ctrl+R (Page Reload)";
            chkNavigationKeys.Location = new Point(40, startY + 278);
            chkNavigationKeys.AutoSize = true;
            chkNavigationKeys.Checked = Config.enableNavigationKeys;
            chkNavigationKeys.Font = new Font("Segoe UI", 9.5f, FontStyle.Regular);
            this.Controls.Add(chkNavigationKeys);

            chkF2Settings = new CheckBox();
            chkF2Settings.Text = "Allow F2 & Ctrl+, (Settings Configuration)";
            chkF2Settings.Location = new Point(40, startY + 302);
            chkF2Settings.AutoSize = true;
            chkF2Settings.Checked = Config.enableF2SettingsKey;
            chkF2Settings.Font = new Font("Segoe UI", 9.5f, FontStyle.Regular);
            this.Controls.Add(chkF2Settings);

            chkEnableDevTools = new CheckBox();
            chkEnableDevTools.Text = "Allow F12 Developer Tools (Debugging)";
            chkEnableDevTools.Location = new Point(40, startY + 326);
            chkEnableDevTools.AutoSize = true;
            chkEnableDevTools.Checked = Config.enableDevTools;
            chkEnableDevTools.Font = new Font("Segoe UI", 9.5f, FontStyle.Regular);
            this.Controls.Add(chkEnableDevTools);

            // Military / Kiosk Lock Controls
            chkLockSettings = new CheckBox();
            chkLockSettings.Text = "Lock Settings & DevTools (Kiosk / Medical Cart Mode)";
            chkLockSettings.Location = new Point(20, startY + 356);
            chkLockSettings.AutoSize = true;
            chkLockSettings.Checked = Config.lockSettings;
            chkLockSettings.Font = new Font("Segoe UI", 9.5f, FontStyle.Bold);
            chkLockSettings.ForeColor = Color.FromArgb(185, 28, 28);
            this.Controls.Add(chkLockSettings);

            Label lblAdminPwd = new Label();
            lblAdminPwd.Text = "Admin Password (optional):";
            lblAdminPwd.Location = new Point(20, startY + 386);
            lblAdminPwd.AutoSize = true;
            this.Controls.Add(lblAdminPwd);

            txtAdminPassword = new TextBox();
            txtAdminPassword.Location = new Point(195, startY + 383);
            txtAdminPassword.Width = 305;
            txtAdminPassword.PasswordChar = '●';
            txtAdminPassword.Text = Config.adminPassword != null ? Config.adminPassword : "";
            this.Controls.Add(txtAdminPassword);

            // Action Buttons
            btnSave = new Button();
            btnSave.Text = "Save & Launch";
            btnSave.Location = new Point(260, startY + 426);
            btnSave.Size = new Size(130, 36);
            btnSave.BackColor = Color.FromArgb(14, 116, 144);
            btnSave.ForeColor = Color.White;
            btnSave.FlatStyle = FlatStyle.Flat;
            btnSave.Cursor = Cursors.Hand;
            btnSave.FlatAppearance.BorderSize = 0;
            btnSave.Click += BtnSave_Click;
            this.Controls.Add(btnSave);

            btnCancel = new Button();
            btnCancel.Text = "Cancel";
            btnCancel.Location = new Point(400, startY + 426);
            btnCancel.Size = new Size(100, 36);
            btnCancel.BackColor = Color.FromArgb(226, 232, 240);
            btnCancel.ForeColor = Color.FromArgb(30, 41, 59);
            btnCancel.FlatStyle = FlatStyle.Flat;
            btnCancel.Cursor = Cursors.Hand;
            btnCancel.FlatAppearance.BorderSize = 0;
            btnCancel.Click += new EventHandler(BtnCancel_Click);
            this.Controls.Add(btnCancel);

            this.AcceptButton = btnSave;
            this.CancelButton = btnCancel;

            // Wire up interactive dependency updates
            chkFunctionKeys.CheckedChanged += delegate { UpdateHotkeyControlsState(); };
            chkLockSettings.CheckedChanged += delegate { UpdateHotkeyControlsState(); };
            UpdateHotkeyControlsState();
        }

        private void UpdateHotkeyControlsState()
        {
            bool master = chkFunctionKeys.Checked;
            bool locked = chkLockSettings.Checked;

            chkF11Fullscreen.Enabled = master;
            chkNavigationKeys.Enabled = master;
            chkF2Settings.Enabled = master && !locked;
            chkEnableDevTools.Enabled = master && !locked;
        }

        private void BtnCancel_Click(object sender, EventArgs e)
        {
            this.DialogResult = DialogResult.Cancel;
            this.Close();
        }

        private void BtnSave_Click(object sender, EventArgs e)
        {
            string url = txtUrl.Text.Trim();
            if (string.IsNullOrEmpty(url))
            {
                MessageBox.Show(
                    "Please enter a valid URL for the MedTRx web application.",
                    "URL Required",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning
                );
                txtUrl.Focus();
                return;
            }

            if (!url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
                !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                url = "https://" + url;
            }

            Config.url = url;
            Config.appName = !string.IsNullOrEmpty(txtAppName.Text.Trim()) ? txtAppName.Text.Trim() : "MedTRx";
            Config.startMaximized = chkStartMaximized.Checked;
            Config.startFullscreen = chkStartFullscreen.Checked;
            Config.alwaysOnTop = chkAlwaysOnTop.Checked;
            Config.touchFullscreenSidebar = chkTouchSidebar.Checked;
            Config.enableFunctionKeys = chkFunctionKeys.Checked;
            Config.enableF11FullscreenKey = chkF11Fullscreen.Checked;
            Config.enableNavigationKeys = chkNavigationKeys.Checked;
            Config.enableF2SettingsKey = chkF2Settings.Checked;
            Config.lockSettings = chkLockSettings.Checked;
            Config.adminPassword = txtAdminPassword.Text.Trim();
            Config.enableDevTools = Config.lockSettings ? false : chkEnableDevTools.Checked;

            ConfigManager.Save(Config);
            this.Saved = true;
            this.DialogResult = DialogResult.OK;
            this.Close();
        }
    }
}
