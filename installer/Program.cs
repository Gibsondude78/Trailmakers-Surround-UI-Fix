using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace TrailmakersSurroundUI
{
    internal static class Program
    {
        [STAThread]
        private static int Main(string[] args)
        {
            if (args.Length > 0 && args[0] == "--self-test")
                return InstallerTests.Run(args.Length > 1 ? args[1] : null);
            bool preview = args.Contains("--preview");
            float scale = 1;
            int scaleIndex = Array.IndexOf(args, "--preview-scale");
            if (preview && scaleIndex >= 0 && scaleIndex + 1 < args.Length)
                if (!Single.TryParse(args[scaleIndex + 1], NumberStyles.Float, CultureInfo.InvariantCulture, out scale)
                    || scale < 1 || scale > 2) return 2;
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new InstallerForm(preview, scale));
            return 0;
        }
    }

    internal sealed class Header : Control
    {
        public Header()
        {
            DoubleBuffered = true; TabStop = false;
            AccessibleName = "Trailmakers Surround UI Fix";
            AccessibleDescription = "Centre-screen HUD. Full-width maps.";
            AccessibleRole = AccessibleRole.Graphic;
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            g.Clear(Color.FromArgb(20, 25, 31));
            g.ScaleTransform(Width / 820f, Height / 154f);
            using (SolidBrush accent = new SolidBrush(Branding.Yellow))
            using (SolidBrush plate = new SolidBrush(Color.FromArgb(27, 34, 42)))
            using (SolidBrush white = new SolidBrush(Branding.White))
            using (SolidBrush muted = new SolidBrush(Branding.Muted))
            using (Font title = new Font("Bahnschrift", 34, FontStyle.Bold, GraphicsUnit.Pixel))
            using (Font subtitle = new Font("Bahnschrift", 23, FontStyle.Bold, GraphicsUnit.Pixel))
            using (Font caption = new Font("Segoe UI", 12, FontStyle.Regular, GraphicsUnit.Pixel))
            {
                g.FillRectangle(accent, 0, 0, 820, 4);
                g.FillPolygon(plate, new[] { new Point(580, 4), new Point(820, 4),
                    new Point(820, 154), new Point(525, 154) });
                g.DrawString("TRAILMAKERS", title, accent, 29, 28);
                g.DrawString("SURROUND UI FIX", subtitle, white, 31, 75);
                g.DrawString("Centre-screen HUD. Full-width maps.", caption, muted, 33, 116);
                Branding.DrawMark(g, new RectangleF(659, 16, 122, 122));
            }
        }
    }

    internal sealed class CutButton : Button
    {
        private bool hover, pressed;
        public bool Primary { get; set; }
        public CutButton()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
            Font = new Font("Bahnschrift", 12, FontStyle.Bold);
            Cursor = Cursors.Hand;
            UseVisualStyleBackColor = false;
        }
        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = pressed = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { pressed = true; Invalidate(); base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e) { pressed = false; Invalidate(); base.OnMouseUp(e); }
        protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
        protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }
        protected override void OnEnabledChanged(EventArgs e) { Invalidate(); base.OnEnabledChanged(e); }
        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(Parent == null ? Branding.Background : Parent.BackColor);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            float cut = Height * 0.22f;
            PointF[] shape = { new PointF(1, 1), new PointF(Width - cut - 1, 1),
                new PointF(Width - 1, cut), new PointF(Width - 1, Height - 1),
                new PointF(cut, Height - 1), new PointF(1, Height - cut - 1) };
            Color fill = !Enabled ? Color.FromArgb(48, 55, 63) : Primary
                ? (pressed ? Color.FromArgb(226, 167, 17) : hover ? Color.FromArgb(255, 216, 76) : Branding.Yellow)
                : (pressed ? Color.FromArgb(43, 51, 60) : hover ? Color.FromArgb(68, 80, 93) : Color.FromArgb(51, 61, 72));
            using (SolidBrush brush = new SolidBrush(fill)) e.Graphics.FillPolygon(brush, shape);
            if (!Primary)
                using (Pen pen = new Pen(Color.FromArgb(86, 99, 113))) e.Graphics.DrawPolygon(pen, shape);
            Color ink = !Enabled ? Color.FromArgb(132, 143, 154) : Primary ? Color.FromArgb(22, 27, 33) : Branding.White;
            TextRenderer.DrawText(e.Graphics, Text, Font, ClientRectangle, ink,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
            if (Focused && ShowFocusCues)
            {
                Rectangle focus = ClientRectangle;
                focus.Inflate(-(int)(Height * 0.16f), -(int)(Height * 0.16f));
                ControlPaint.DrawFocusRectangle(e.Graphics, focus, ink, fill);
            }
        }
    }

    internal sealed class StatusPanel : Panel
    {
        public Color Accent = Branding.Yellow;
        public StatusPanel() { DoubleBuffered = true; BackColor = Branding.Panel; }
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            using (SolidBrush brush = new SolidBrush(Accent))
                e.Graphics.FillRectangle(brush, 0, 0, Math.Max(3, Height / 19), Height);
        }
    }

    internal sealed class InstallerForm : Form
    {
        private readonly TextBox folder = new TextBox();
        private readonly Label statusTitle = new Label(), statusText = new Label();
        private readonly StatusPanel status = new StatusPanel();
        private readonly CutButton install = new CutButton(), remove = new CutButton(), browse = new CutButton();
        private readonly Timer folderTimer = new Timer();
        private readonly bool preview;
        private bool busy;
        private Icon appIcon;

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr window, int attribute, ref int value, int size);

        public InstallerForm(bool previewOnly, float previewScale)
        {
            preview = previewOnly;
            SuspendLayout();
            AutoScaleDimensions = new SizeF(96f / previewScale, 96f / previewScale);
            AutoScaleMode = AutoScaleMode.Dpi;
            Font = new Font("Segoe UI", 10);
            ClientSize = new Size(820, 528);
            Text = "Trailmakers Surround UI Fix" + (preview ? " — Preview" : " — Setup");
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Branding.Background;
            ForeColor = Branding.White;
            using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("setup.ico"))
                if (stream != null) { using (Icon source = new Icon(stream, new Size(256, 256))) appIcon = (Icon)source.Clone(); Icon = appIcon; }

            Controls.Add(new Header { Bounds = new Rectangle(0, 0, 820, 154) });
            Controls.Add(new Label { Text = "GAME FOLDER", AutoSize = true,
                Font = new Font("Bahnschrift", 11, FontStyle.Bold), Location = new Point(32, 178) });
            Panel input = new Panel { BackColor = Color.FromArgb(75, 88, 102), Bounds = new Rectangle(32, 207, 636, 41), Padding = new Padding(1) };
            Panel inputInside = new Panel { BackColor = Color.FromArgb(23, 29, 36), Dock = DockStyle.Fill, Padding = new Padding(11, 9, 9, 5) };
            folder.BorderStyle = BorderStyle.None;
            folder.BackColor = inputInside.BackColor;
            folder.ForeColor = Branding.White;
            folder.Font = new Font("Segoe UI", 11);
            folder.Dock = DockStyle.Fill;
            folder.AccessibleName = "Trailmakers installation folder";
            folder.TabIndex = 0;
            inputInside.Controls.Add(folder); input.Controls.Add(inputInside); Controls.Add(input);
            browse.Text = "BROWSE"; browse.Bounds = new Rectangle(680, 207, 108, 41); browse.TabIndex = 1;
            browse.Click += Browse;
            Controls.Add(browse);
            Controls.Add(new Label { Text = "Select the folder containing Trailmakers.exe.", AutoSize = true,
                ForeColor = Branding.Muted, Font = new Font("Segoe UI", 9), Location = new Point(31, 255) });

            status.Bounds = new Rectangle(32, 289, 756, 80);
            statusTitle.Bounds = new Rectangle(19, 11, 714, 23);
            statusTitle.Font = new Font("Bahnschrift", 12, FontStyle.Bold);
            statusText.Bounds = new Rectangle(19, 36, 714, 38);
            statusText.Font = new Font("Segoe UI", 10);
            statusText.ForeColor = Branding.Muted;
            status.Controls.Add(statusTitle); status.Controls.Add(statusText); Controls.Add(status);

            install.Text = "INSTALL FIX"; install.Primary = true;
            install.Bounds = new Rectangle(32, 390, 336, 52); install.TabIndex = 2;
            install.Click += delegate { Change(true); };
            remove.Text = "REMOVE FIX"; remove.Bounds = new Rectangle(380, 390, 164, 52); remove.TabIndex = 3;
            remove.Click += delegate { Change(false); };
            CutButton close = new CutButton { Text = "CLOSE", Bounds = new Rectangle(680, 390, 108, 52), TabIndex = 4 };
            close.Click += delegate { Close(); };
            Controls.Add(install); Controls.Add(remove); Controls.Add(close);

            AddLink("ReShade download", 32, 464, 5, delegate { Open("https://reshade.me/#download"); });
            AddLink("GitHub / help", 205, 464, 6, delegate { Open(InstallerCore.ProjectUrl); });
            AddLink("Check again", 696, 464, 7, delegate { Inspect(); });
            Controls.Add(new Panel { BackColor = Color.FromArgb(58, 69, 81), Bounds = new Rectangle(32, 497, 756, 1) });
            Controls.Add(new Label { Text = "Unofficial add-on", AutoSize = true, Font = new Font("Segoe UI", 8),
                ForeColor = Branding.Muted, Location = new Point(31, 507) });
            Controls.Add(new Label { Text = "v" + PayloadInfo.Version, TextAlign = ContentAlignment.MiddleRight,
                Font = new Font("Segoe UI", 8), ForeColor = Branding.Muted, Bounds = new Rectangle(680, 504, 108, 20) });

            folderTimer.Interval = 300;
            folderTimer.Tick += delegate { folderTimer.Stop(); Inspect(); };
            folder.TextChanged += delegate { if (!busy) { folderTimer.Stop(); folderTimer.Start(); } };
            Activated += delegate { if (!busy) Inspect(); };
            if (preview) folder.Text = @"C:\Program Files (x86)\Steam\steamapps\common\Trailmakers";
            else
            {
                try
                {
                    string detected = InstallerCore.DetectFolders().FirstOrDefault();
                    folder.Text = detected == null ? "" : Path.GetFullPath(detected);
                }
                catch (IOException) { folder.Text = ""; }
                catch (UnauthorizedAccessException) { folder.Text = ""; }
            }
            ResumeLayout(true);
            if (previewScale > 1)
            {
                // Simulate the font raster size as well as control bounds when
                // reviewing a larger DPI layout on a 96-DPI desktop.
                List<Tuple<Control, Font>> fonts = new List<Tuple<Control, Font>>();
                CollectFonts(this, fonts);
                foreach (var entry in fonts)
                    entry.Item1.Font = new Font(entry.Item2.FontFamily, entry.Item2.Size * previewScale, entry.Item2.Style);
            }
            Inspect();
        }

        private static void CollectFonts(Control control, List<Tuple<Control, Font>> fonts)
        {
            fonts.Add(Tuple.Create(control, control.Font));
            foreach (Control child in control.Controls) CollectFonts(child, fonts);
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            try { int dark = 1; DwmSetWindowAttribute(Handle, 20, ref dark, sizeof(int)); }
            catch (DllNotFoundException) { }
            catch (EntryPointNotFoundException) { }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) { folderTimer.Dispose(); if (appIcon != null) { appIcon.Dispose(); appIcon = null; } }
            base.Dispose(disposing);
        }

        private void AddLink(string text, int x, int y, int order, Action action)
        {
            LinkLabel link = new LinkLabel { Text = text, AutoSize = true, Location = new Point(x, y),
                Font = new Font("Segoe UI", 9), LinkColor = Branding.Muted, ActiveLinkColor = Branding.Yellow,
                VisitedLinkColor = Branding.Muted, TabIndex = order, LinkBehavior = LinkBehavior.HoverUnderline };
            link.LinkClicked += delegate { action(); };
            Controls.Add(link);
        }

        private static void Open(string url) { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); }

        private void Browse(object sender, EventArgs e)
        {
            using (FolderBrowserDialog dialog = new FolderBrowserDialog { Description = "Choose the folder containing Trailmakers.exe", ShowNewFolderButton = false })
            {
                if (Directory.Exists(folder.Text)) dialog.SelectedPath = folder.Text;
                if (dialog.ShowDialog(this) == DialogResult.OK) { folder.Text = dialog.SelectedPath; Inspect(); }
            }
        }

        private void SetStatus(string title, string detail, Color accent)
        {
            statusTitle.Text = title;
            statusText.Text = detail;
            status.Accent = accent;
            status.Invalidate();
        }

        private void Inspect()
        {
            if (busy) return;
            if (preview)
            {
                install.Enabled = remove.Enabled = true;
                SetStatus("SETUP PREVIEW", "No game files will be changed.", Branding.Yellow);
                return;
            }
            install.Enabled = remove.Enabled = false;
            install.Text = "INSTALL FIX";
            try
            {
                string root = InstallerCore.GameFolder(folder.Text);
                if (InstallerCore.GameRunning(root))
                {
                    SetStatus("CLOSE THE GAME", "Close Trailmakers, then click Check again.", Branding.Yellow); return;
                }
                bool installed = File.Exists(Path.Combine(root, InstallerCore.AddonName));
                remove.Enabled = installed || File.Exists(Path.Combine(root, InstallerCore.RecordName));
                if (InstallerCore.Loader(root) == null)
                {
                    SetStatus("RESHADE REQUIRED", "Install ReShade 6.8+ with full add-on support, then click Check again.", Branding.Yellow); return;
                }
                install.Enabled = true;
                if (installed && Payload.FileHash(Path.Combine(root, InstallerCore.AddonName)) == PayloadInfo.Sha256)
                {
                    install.Text = "REINSTALL FIX";
                    SetStatus("FIX INSTALLED", "You can reinstall it here, or remove it. Your settings will be kept.", Color.FromArgb(132, 192, 126));
                }
                else if (installed)
                    SetStatus("EXISTING ADD-ON FOUND", "Remove the old version before installing this one. Setup will check the file first.", Branding.Yellow);
                else SetStatus("READY TO INSTALL", "Trailmakers and ReShade found. Press F10 in-game to toggle UI centring.", Color.FromArgb(132, 192, 126));
            }
            catch (Exception error) { SetStatus("CHECK THE GAME FOLDER", error.Message, Branding.Yellow); }
        }

        private void Change(bool installing)
        {
            if (preview) { SetStatus("PREVIEW ONLY", "No files were changed.", Branding.Yellow); return; }
            busy = true;
            folderTimer.Stop();
            install.Enabled = remove.Enabled = browse.Enabled = folder.Enabled = false;
            UseWaitCursor = true;
            bool success = false;
            try
            {
                if (installing) InstallerCore.Install(folder.Text); else InstallerCore.Uninstall(folder.Text);
                success = true;
            }
            catch (UnauthorizedAccessException)
            {
                MessageBox.Show(this, "Setup can't write to this folder. Close it and run as administrator to install here.", "Folder access", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception error)
            { MessageBox.Show(this, error.Message, "Setup couldn't finish", MessageBoxButtons.OK, MessageBoxIcon.Information); }
            finally
            {
                busy = false; UseWaitCursor = false; browse.Enabled = folder.Enabled = true;
                Inspect();
                if (success) SetStatus(installing ? "INSTALLED" : "FIX REMOVED", installing
                    ? "Start Trailmakers. Press F10 to toggle UI centring."
                    : "ReShade, other add-ons and your settings have been kept.", Color.FromArgb(132, 192, 126));
            }
        }
    }
}
