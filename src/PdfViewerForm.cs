using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace MedTRx
{
    public class PdfViewerForm : Form
    {
        private WebView2 webView;
        private string pdfPath;
        private string browserFolder;
        private Panel topBar;
        private Label lblTitle;
        private Button btnPrint;
        private Button btnExternal;
        private Button btnClose;

        public PdfViewerForm(string filePathOrUrl, string browserExecutableFolder)
        {
            this.pdfPath = filePathOrUrl;
            this.browserFolder = browserExecutableFolder;
            InitializeWindow();
            InitializeWebViewAsync();
        }

        private void InitializeWindow()
        {
            string fileName = Path.GetFileName(pdfPath);
            if (string.IsNullOrEmpty(fileName)) fileName = "Document.pdf";

            this.Text = fileName + " - MedTRx Document Viewer";
            this.Size = new Size(1100, 800);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.BackColor = Color.FromArgb(241, 245, 249);
            this.KeyPreview = true;

            try
            {
                string iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logo.ico");
                if (File.Exists(iconPath))
                {
                    this.Icon = new Icon(iconPath);
                }
            }
            catch { }

            // Minimalist Top Bar
            topBar = new Panel();
            topBar.Dock = DockStyle.Top;
            topBar.Height = 42;
            topBar.BackColor = Color.FromArgb(15, 23, 42); // Dark slate header

            lblTitle = new Label();
            lblTitle.Text = "📄 " + fileName;
            lblTitle.ForeColor = Color.White;
            lblTitle.Font = new Font("Segoe UI", 10f, FontStyle.Bold);
            lblTitle.Location = new Point(16, 11);
            lblTitle.AutoSize = true;
            topBar.Controls.Add(lblTitle);

            // Action Buttons
            btnClose = new Button();
            btnClose.Text = "✕ Close";
            btnClose.Size = new Size(80, 28);
            btnClose.Location = new Point(this.ClientSize.Width - 96, 7);
            btnClose.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            btnClose.BackColor = Color.FromArgb(51, 65, 85);
            btnClose.ForeColor = Color.White;
            btnClose.FlatStyle = FlatStyle.Flat;
            btnClose.FlatAppearance.BorderSize = 0;
            btnClose.Cursor = Cursors.Hand;
            btnClose.Click += new EventHandler(BtnClose_Click);
            topBar.Controls.Add(btnClose);

            btnExternal = new Button();
            btnExternal.Text = "↗ Open in External App";
            btnExternal.Size = new Size(150, 28);
            btnExternal.Location = new Point(this.ClientSize.Width - 256, 7);
            btnExternal.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            btnExternal.BackColor = Color.FromArgb(14, 116, 144);
            btnExternal.ForeColor = Color.White;
            btnExternal.FlatStyle = FlatStyle.Flat;
            btnExternal.FlatAppearance.BorderSize = 0;
            btnExternal.Cursor = Cursors.Hand;
            btnExternal.Click += new EventHandler(BtnExternal_Click);
            topBar.Controls.Add(btnExternal);

            btnPrint = new Button();
            btnPrint.Text = "🖶 Print";
            btnPrint.Size = new Size(80, 28);
            btnPrint.Location = new Point(this.ClientSize.Width - 346, 7);
            btnPrint.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            btnPrint.BackColor = Color.FromArgb(13, 148, 136);
            btnPrint.ForeColor = Color.White;
            btnPrint.FlatStyle = FlatStyle.Flat;
            btnPrint.FlatAppearance.BorderSize = 0;
            btnPrint.Cursor = Cursors.Hand;
            btnPrint.Click += new EventHandler(BtnPrint_Click);
            topBar.Controls.Add(btnPrint);

            this.Controls.Add(topBar);

            this.KeyDown += new KeyEventHandler(PdfViewerForm_KeyDown);
        }

        private async void InitializeWebViewAsync()
        {
            webView = new WebView2();
            webView.Dock = DockStyle.Fill;
            webView.DefaultBackgroundColor = Color.FromArgb(82, 86, 89); // Classic PDF reader dark background
            this.Controls.Add(webView);
            webView.BringToFront();

            try
            {
                string tempUserFolder = Path.Combine(Path.GetTempPath(), "MedTRx_PdfSession");
                var env = await CoreWebView2Environment.CreateAsync(browserFolder, tempUserFolder);
                await webView.EnsureCoreWebView2Async(env);

                var s = webView.CoreWebView2.Settings;
                s.AreDevToolsEnabled = false;
                s.IsStatusBarEnabled = false;
                s.AreDefaultContextMenusEnabled = true;

                // Navigate to PDF
                if (File.Exists(pdfPath))
                {
                    webView.CoreWebView2.Navigate(new Uri(pdfPath).AbsoluteUri);
                }
                else
                {
                    webView.CoreWebView2.Navigate(pdfPath);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Unable to initialize PDF viewer: " + ex.Message,
                    "PDF Viewer Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error
                );
            }
        }

        private void BtnClose_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        private void BtnExternal_Click(object sender, EventArgs e)
        {
            try
            {
                if (File.Exists(pdfPath))
                {
                    ProcessStartInfo psi = new ProcessStartInfo(pdfPath);
                    psi.UseShellExecute = true;
                    Process.Start(psi);
                }
                else
                {
                    Process.Start(pdfPath);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Could not open external app: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void BtnPrint_Click(object sender, EventArgs e)
        {
            if (webView != null && webView.CoreWebView2 != null)
            {
                webView.CoreWebView2.ExecuteScriptAsync("window.print();");
            }
        }

        private void PdfViewerForm_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape)
            {
                this.Close();
                e.Handled = true;
            }
            else if (e.Control && e.KeyCode == Keys.P)
            {
                BtnPrint_Click(sender, e);
                e.Handled = true;
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (webView != null)
                {
                    webView.Dispose();
                }
            }
            base.Dispose(disposing);
        }
    }
}
