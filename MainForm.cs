using System;
using System.Drawing;
using System.Windows.Forms;
using System.Diagnostics;

namespace AddToPath
{
    public class MainForm : Form
    {
        private readonly Button installButton;
        private readonly Button uninstallButton;
        private readonly Label descriptionLabel;

        public MainForm()
        {
            Text = "Add to PATH - Windows Context Menu Utility";
            Size = new Size(500, 300);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;

            // Description text
            descriptionLabel = new Label
            {
                Location = new Point(20, 20),
                Size = new Size(440, 160),
                Text = "This utility adds an 'Add to System PATH' option to your Windows context menu " +
                      "when right-clicking on folders.\n\n" +
                      "After installation, you can right-click any folder and select 'Add to System PATH' " +
                      "to quickly add that folder to your system's PATH environment variable.\n\n" +
                      "Note: Installing or uninstalling will require administrator privileges to modify " +
                      "the registry and system PATH.",
                TextAlign = ContentAlignment.TopLeft
            };
            Controls.Add(descriptionLabel);

            // Install button
            installButton = new Button
            {
                Text = "Install Context Menu",
                Size = new Size(150, 30),
                Location = new Point(80, 200)
            };
            installButton.Click += InstallButton_Click;
            Controls.Add(installButton);

            // Uninstall button
            uninstallButton = new Button
            {
                Text = "Uninstall",
                Size = new Size(150, 30),
                Location = new Point(250, 200)
            };
            uninstallButton.Click += UninstallButton_Click;
            Controls.Add(uninstallButton);

            // Update button states based on installation status
            UpdateButtonStates();
        }

        private void UpdateButtonStates()
        {
            bool isInstalled = Program.IsInstalledInProgramFiles();
            installButton.Text = isInstalled ? "Repair Installation" : "Install Context Menu";
            uninstallButton.Enabled = isInstalled;
        }

        private void RestartAsAdmin(string[] args = null)
        {
            ProcessStartInfo proc = new ProcessStartInfo
            {
                UseShellExecute = true,
                WorkingDirectory = Environment.CurrentDirectory,
                FileName = Application.ExecutablePath,
                Verb = "runas"
            };

            if (args != null && args.Length > 0)
            {
                proc.Arguments = string.Join(" ", args);
            }

            try
            {
                Process.Start(proc);
                Application.Exit();
            }
            catch (Exception)
            {
                MessageBox.Show(
                    "Administrator rights are required to modify the system PATH and registry.",
                    "Admin Rights Required",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }
        }

        private void InstallButton_Click(object sender, EventArgs e)
        {
            if (MessageBox.Show(
                "This will add the 'Add to System PATH' option to your folder context menu.\n\n" +
                "Administrator rights will be required to continue.",
                "Confirm Installation",
                MessageBoxButtons.OKCancel,
                MessageBoxIcon.Information) == DialogResult.OK)
            {
                RestartAsAdmin();
            }
        }

        private void UninstallButton_Click(object sender, EventArgs e)
        {
            if (MessageBox.Show(
                "This will remove the 'Add to System PATH' option from your folder context menu.\n\n" +
                "Administrator rights will be required to continue.",
                "Confirm Uninstallation",
                MessageBoxButtons.OKCancel,
                MessageBoxIcon.Warning) == DialogResult.OK)
            {
                RestartAsAdmin(new[] { "--uninstall" });
            }
        }
    }
}
