using System;
using System.Drawing;
using System.Windows.Forms;
using System.Diagnostics;
using System.Reflection;

namespace AddToPath
{
    public class MainForm : Form
    {
        private readonly Button installButton;
        private readonly Button uninstallButton;
        private readonly Button showPathsButton;
        private readonly Label titleLabel;
        private readonly Label descriptionLabel;
        private readonly Panel contentPanel;
        private readonly TableLayoutPanel buttonPanel;

        public MainForm()
        {
            // Get version from assembly
            var version = Assembly.GetExecutingAssembly().GetName().Version;
            Text = "Add to PATH - Windows Context Menu Utility";
            Size = new Size(520, 340);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Color.White;
            Font = new Font("Segoe UI", 9F);
            Padding = new Padding(20);
            Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);

            // Main content panel
            contentPanel = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(0, 0, 0, 10)
            };
            Controls.Add(contentPanel);

            // Title
            titleLabel = new Label
            {
                Text = "Add to PATH Context Menu",
                Font = new Font("Segoe UI", 16F, FontStyle.Regular),
                ForeColor = Color.FromArgb(0, 99, 155),
                AutoSize = true,
                Margin = new Padding(0, 0, 0, 15)
            };
            contentPanel.Controls.Add(titleLabel);

            // Version label
            var versionLabel = new Label
            {
                Text = $"v{version.Major}.{version.Minor}.{version.Build}",
                Font = new Font("Segoe UI", 9F),
                ForeColor = Color.FromArgb(120, 120, 120),
                AutoSize = true,
                Location = new Point(contentPanel.Width - 60, titleLabel.Top + 8)  // Align vertically with title
            };
            contentPanel.Controls.Add(versionLabel);

            // Description text
            descriptionLabel = new Label
            {
                Location = new Point(0, titleLabel.Bottom + 15),
                Size = new Size(460, 140),
                Font = new Font("Segoe UI", 9.75F),
                Text = "This utility adds a 'Path' submenu to your Windows context menu " +
                      "when right-clicking on folders.\n\n" +
                      "After installation, you can right-click any folder to access the following options:\n" +
                      "• Add to PATH - Add the folder to system PATH\n" +
                      "• Remove from PATH - Remove the folder from system PATH\n" +
                      "• Show PATHs - View all current PATH entries\n\n" +
                      "Note: Installing or uninstalling will require administrator privileges to modify " +
                      "the registry and system PATH.",
                TextAlign = ContentAlignment.TopLeft
            };
            contentPanel.Controls.Add(descriptionLabel);

            // Button panel
            buttonPanel = new TableLayoutPanel
            {
                ColumnCount = 3,
                Dock = DockStyle.Bottom,
                Height = 40,
                Padding = new Padding(0),
                ColumnStyles = {
                    new ColumnStyle(SizeType.Percent, 33.33F),
                    new ColumnStyle(SizeType.Percent, 33.33F),
                    new ColumnStyle(SizeType.Percent, 33.33F)
                }
            };

            Controls.Add(buttonPanel);

            // Install button
            installButton = new Button
            {
                Size = new Size(150, 32),
                Font = new Font("Segoe UI", 9.75F),
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                Anchor = AnchorStyles.None,
                UseVisualStyleBackColor = true
            };
            installButton.FlatAppearance.BorderColor = Color.FromArgb(0, 120, 215);
            installButton.Click += InstallButton_Click;
            buttonPanel.Controls.Add(installButton, 0, 0);

            // Uninstall button
            uninstallButton = new Button
            {
                Size = new Size(150, 32),
                Font = new Font("Segoe UI", 9.75F),
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                Anchor = AnchorStyles.None,
                UseVisualStyleBackColor = true
            };
            uninstallButton.FlatAppearance.BorderColor = Color.FromArgb(170, 170, 170);
            uninstallButton.Click += UninstallButton_Click;
            buttonPanel.Controls.Add(uninstallButton, 1, 0);

            // Show paths button
            showPathsButton = new Button
            {
                Size = new Size(150, 32),
                Font = new Font("Segoe UI", 9.75F),
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                Anchor = AnchorStyles.None,
                UseVisualStyleBackColor = true
            };
            showPathsButton.FlatAppearance.BorderColor = Color.FromArgb(0, 120, 215);
            showPathsButton.Click += ShowPathsButton_Click;
            buttonPanel.Controls.Add(showPathsButton, 2, 0);

            // Update button states
            UpdateButtonStates();
        }

        private void UpdateButtonStates()
        {
            bool isInstalled = Program.IsInstalledInProgramFiles();
            
            // Install button styling
            installButton.Text = isInstalled ? "Repair Installation" : "Install Context Menu";
            installButton.BackColor = Color.FromArgb(0, 120, 215);
            installButton.ForeColor = Color.White;
            
            // Uninstall button styling
            uninstallButton.Text = "Uninstall";
            uninstallButton.Enabled = isInstalled;
            uninstallButton.BackColor = uninstallButton.Enabled ? Color.White : Color.FromArgb(235, 235, 235);
            uninstallButton.ForeColor = uninstallButton.Enabled ? Color.FromArgb(51, 51, 51) : Color.FromArgb(160, 160, 160);
            uninstallButton.FlatAppearance.BorderColor = uninstallButton.Enabled ? Color.FromArgb(170, 170, 170) : Color.FromArgb(200, 200, 200);

            // Show paths button styling
            showPathsButton.Text = "Show PATHs";
            showPathsButton.BackColor = Color.White;
            showPathsButton.ForeColor = Color.FromArgb(51, 51, 51);
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
                "This will add the 'Path' menu to your folder context menu.\n\n" +
                "Administrator rights will be required to continue.",
                "Confirm Installation",
                MessageBoxButtons.OKCancel,
                MessageBoxIcon.Information) == DialogResult.OK)
            {
                Program.InstallContextMenu();
                UpdateButtonStates();
            }
        }

        private void UninstallButton_Click(object sender, EventArgs e)
        {
            if (MessageBox.Show(
                "This will remove the 'Path' menu from your folder context menu.\n\n" +
                "Administrator rights will be required to continue.",
                "Confirm Uninstallation",
                MessageBoxButtons.OKCancel,
                MessageBoxIcon.Warning) == DialogResult.OK)
            {
                if (!Program.IsRunningAsAdmin())
                {
                    RestartAsAdmin(new[] { "--uninstall" });
                    return;
                }
                Program.UninstallContextMenu();
                UpdateButtonStates();
            }
        }

        private void ShowPathsButton_Click(object sender, EventArgs e)
        {
            using (var dialog = new PathsDialog())
            {
                dialog.ShowDialog();
            }
        }
    }
}
