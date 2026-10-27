using System;
using System.Drawing;
using System.Diagnostics;
using System.IO;
using System.Linq;
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
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new InstallerForm(args.Length > 0 && args[0] == "--preview"));
            return 0;
        }
    }

    internal sealed class InstallerForm : Form
    {
        private readonly TextBox folder = new TextBox();
        private readonly Label status = new Label();
        private readonly Button install = new Button();
        private readonly Button uninstall = new Button();
        private readonly bool preview;

        public InstallerForm(bool previewOnly)
        {
            preview = previewOnly;
            Text = "Trailmakers Centre Screen UI — Setup";
            ClientSize = new Size(680, 450);
            MinimumSize = MaximumSize = Size;
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Color.FromArgb(246, 248, 251);
            Font = new Font("Segoe UI", 10);
            AutoScaleMode = AutoScaleMode.Dpi;
            Icon = SystemIcons.Application;
            Panel header = new Panel { BackColor = Color.FromArgb(19, 37, 59), Bounds = new Rectangle(0, 0, 680, 112) };
            header.Controls.Add(new Label { Text = "Trailmakers Centre Screen UI", ForeColor = Color.White,
                Font = new Font("Segoe UI", 21, FontStyle.Bold), AutoSize = true, Location = new Point(26, 21) });
            header.Controls.Add(new Label { Text = "HUD, menus and inventory on the middle screen", ForeColor = Color.FromArgb(192, 208, 225),
                AutoSize = true, Location = new Point(29, 70) });
            Controls.Add(header);
            Controls.Add(new Label { Text = "Your Surround view stays at full resolution.", AutoSize = true, Location = new Point(29, 133) });
            Controls.Add(new Label { Text = "Trailmakers installation folder", AutoSize = true, Font = new Font(Font, FontStyle.Bold), Location = new Point(29, 174) });
            folder.Bounds = new Rectangle(30, 201, 506, 29);
            Button browse = new Button { Text = "Browse…", Bounds = new Rectangle(545, 199, 104, 32) };
            browse.Click += delegate
            {
                using (FolderBrowserDialog dialog = new FolderBrowserDialog { Description = "Select the folder containing Trailmakers.exe", ShowNewFolderButton = false })
                {
                    if (Directory.Exists(folder.Text)) dialog.SelectedPath = folder.Text;
                    if (dialog.ShowDialog(this) == DialogResult.OK) { folder.Text = dialog.SelectedPath; Inspect(); }
                }
            };
            folder.Leave += delegate { Inspect(); };
            Controls.Add(folder); Controls.Add(browse);
            status.Bounds = new Rectangle(30, 243, 620, 49);
            status.ForeColor = Color.FromArgb(45, 94, 66);
            Controls.Add(status);
            LinkLabel reshade = new LinkLabel { Text = "Official ReShade download (full add-on support)", AutoSize = true, Location = new Point(28, 300) };
            reshade.LinkClicked += delegate { Open("https://reshade.me/#download"); };
            Controls.Add(reshade);
            install.Text = "Install"; install.Bounds = new Rectangle(30, 342, 174, 42);
            install.BackColor = Color.FromArgb(27, 92, 166); install.ForeColor = Color.White;
            install.FlatStyle = FlatStyle.Flat; install.FlatAppearance.BorderSize = 0;
            install.Click += delegate { Change(true); };
            uninstall.Text = "Uninstall"; uninstall.Bounds = new Rectangle(215, 342, 150, 42);
            uninstall.Click += delegate { Change(false); };
            Button close = new Button { Text = "Close", Bounds = new Rectangle(545, 342, 104, 42) };
            close.Click += delegate { Close(); };
            Controls.Add(install); Controls.Add(uninstall); Controls.Add(close);
            LinkLabel project = new LinkLabel { Text = "Experimental community add-on · Project & compatibility notes", AutoSize = true, Location = new Point(28, 409) };
            project.LinkClicked += delegate { Open(InstallerCore.ProjectUrl); };
            Controls.Add(project);
            string detected = InstallerCore.DetectFolders().FirstOrDefault();
            folder.Text = detected ?? "";
            if (preview)
            {
                Text += " (preview)";
                install.Enabled = uninstall.Enabled = false;
                status.Text = "Preview only. No game files will be changed.";
            }
            else Inspect();
        }

        private static void Open(string url) { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); }

        private void Inspect()
        {
            if (preview) return;
            try
            {
                string root = InstallerCore.GameFolder(folder.Text);
                if (InstallerCore.GameRunning(root)) throw new InvalidOperationException("Close Trailmakers before installing or uninstalling.");
                if (InstallerCore.Loader(root) == null) throw new InvalidOperationException("ReShade is required. Install version 6.8+ with full add-on support using the link below.");
                status.ForeColor = Color.FromArgb(45, 94, 66);
                status.Text = "Game folder and ReShade loader detected. Ready to install.";
            }
            catch (Exception error) { status.ForeColor = Color.FromArgb(135, 83, 16); status.Text = error.Message; }
        }

        private void Change(bool installing)
        {
            install.Enabled = uninstall.Enabled = false;
            UseWaitCursor = true;
            try
            {
                if (installing) InstallerCore.Install(folder.Text); else InstallerCore.Uninstall(folder.Text);
                status.ForeColor = Color.FromArgb(45, 94, 66);
                status.Text = installing ? "Installed. Start Trailmakers normally. F10 toggles the centred UI."
                    : "Uninstalled. ReShade, other add-ons and your settings were kept.";
            }
            catch (UnauthorizedAccessException)
            {
                MessageBox.Show(this, "This game folder is not writable. Close setup and run it as administrator if you want to install here.", "Folder access", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception error)
            { MessageBox.Show(this, error.Message, "Setup could not complete", MessageBoxButtons.OK, MessageBoxIcon.Information); }
            finally { UseWaitCursor = false; install.Enabled = uninstall.Enabled = true; }
        }
    }
}
