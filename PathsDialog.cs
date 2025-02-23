// Copyright (c) 2025 David Whatley
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace AddToPath
{
    public partial class PathsDialog : Form
    {
        private readonly RichTextBox pathsTextBox;
        private readonly bool showUser;
        private readonly bool showSystem;
        private readonly Panel headerPanel;
        private readonly Label titleLabel;

        public PathsDialog(bool showUser = true, bool showSystem = true)
        {
            try
            {
                this.showUser = showUser;
                this.showSystem = showSystem;

                // Form settings
                Text = "PATH Variables";
                Size = new Size(800, 600);
                StartPosition = FormStartPosition.CenterScreen;
                MinimizeBox = true;
                MaximizeBox = true;
                FormBorderStyle = FormBorderStyle.Sizable;
                BackColor = Color.White;
                Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);

                // Main container
                var mainContainer = new TableLayoutPanel
                {
                    Dock = DockStyle.Fill,
                    RowCount = 2,
                    ColumnCount = 1,
                    Margin = new Padding(0),
                    Padding = new Padding(0)
                };
                mainContainer.RowStyles.Add(new RowStyle(SizeType.Absolute, 60));
                mainContainer.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
                Controls.Add(mainContainer);

                // Header panel
                headerPanel = new Panel
                {
                    Dock = DockStyle.Fill,
                    BackColor = Color.FromArgb(0, 120, 212),
                    Margin = new Padding(0)
                };

                titleLabel = new Label
                {
                    Text = showUser && showSystem ? "All PATHs" :
                           showUser ? "User PATH" : "System PATH",
                    ForeColor = Color.White,
                    Font = new Font("Segoe UI", 16, FontStyle.Regular),
                    AutoSize = true,
                    Location = new Point(20, 15)
                };

                headerPanel.Controls.Add(titleLabel);
                mainContainer.Controls.Add(headerPanel, 0, 0);

                // Main content
                var contentPanel = new Panel
                {
                    Dock = DockStyle.Fill,
                    Padding = new Padding(20),
                    Margin = new Padding(0)
                };

                pathsTextBox = new RichTextBox
                {
                    Dock = DockStyle.Fill,
                    ReadOnly = true,
                    Font = new Font("Cascadia Code", 10F),
                    BackColor = Color.White,
                    ForeColor = Color.Black,
                    BorderStyle = BorderStyle.None,
                    WordWrap = false,
                    Margin = new Padding(0)
                };

                // Add a subtle border to the text box
                var borderPanel = new Panel
                {
                    Dock = DockStyle.Fill,
                    Padding = new Padding(1),
                    BackColor = Color.FromArgb(200, 200, 200),
                    Margin = new Padding(0)
                };
                borderPanel.Controls.Add(pathsTextBox);
                contentPanel.Controls.Add(borderPanel);
                mainContainer.Controls.Add(contentPanel, 0, 1);

                LoadPaths();
            }
            catch (Exception ex)
            {
                Program.LogMessage($"Error in PathsDialog constructor: {ex}");
                throw;
            }
        }

        private void LoadPaths()
        {
            try
            {
                var sb = new StringBuilder();

                if (showUser)
                {
                    var userPath = Environment.GetEnvironmentVariable("PATH", EnvironmentVariableTarget.User) ?? "";
                    sb.AppendLine("User PATH:");
                    sb.AppendLine("----------");
                    foreach (var path in userPath.Split(';').Where(p => !string.IsNullOrWhiteSpace(p)))
                    {
                        sb.AppendLine(path);
                    }
                    sb.AppendLine();
                }

                if (showSystem)
                {
                    var systemPath = Environment.GetEnvironmentVariable("PATH", EnvironmentVariableTarget.Machine) ?? "";
                    sb.AppendLine("System PATH:");
                    sb.AppendLine("------------");
                    foreach (var path in systemPath.Split(';').Where(p => !string.IsNullOrWhiteSpace(p)))
                    {
                        sb.AppendLine(path);
                    }
                }

                pathsTextBox.Text = sb.ToString();
            }
            catch (Exception ex)
            {
                Program.LogMessage($"Error in LoadPaths: {ex}");
                throw;
            }
        }

        protected override void OnSizeChanged(EventArgs e)
        {
            base.OnSizeChanged(e);
            if (pathsTextBox != null)
            {
                pathsTextBox.SelectionStart = 0;
                pathsTextBox.SelectionLength = 0;
            }
        }
    }
}
