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

        public PathsDialog(bool showUser = true, bool showSystem = true)
        {
            try
            {
                Program.LogMessage($"Initializing PathsDialog with showUser={showUser}, showSystem={showSystem}");
                this.showUser = showUser;
                this.showSystem = showSystem;

                Text = "PATH Variables";
                Size = new Size(800, 600);
                StartPosition = FormStartPosition.CenterScreen;
                MinimizeBox = false;
                MaximizeBox = true;
                FormBorderStyle = FormBorderStyle.Sizable;

                pathsTextBox = new RichTextBox
                {
                    Dock = DockStyle.Fill,
                    ReadOnly = true,
                    Font = new Font("Consolas", 10F),
                    BackColor = Color.White,
                    ForeColor = Color.Black,
                    Margin = new Padding(10),
                    WordWrap = false
                };

                Controls.Add(pathsTextBox);
                
                Program.LogMessage("Created and configured PathsDialog window");

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
                Program.LogMessage("Loading paths...");
                var sb = new StringBuilder();

                if (showUser)
                {
                    var userPath = Environment.GetEnvironmentVariable("PATH", EnvironmentVariableTarget.User) ?? "";
                    Program.LogMessage($"User PATH: {userPath}");
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
                    Program.LogMessage($"System PATH: {systemPath}");
                    sb.AppendLine("System PATH:");
                    sb.AppendLine("------------");
                    foreach (var path in systemPath.Split(';').Where(p => !string.IsNullOrWhiteSpace(p)))
                    {
                        sb.AppendLine(path);
                    }
                }

                pathsTextBox.Text = sb.ToString();
                Program.LogMessage("Finished loading paths");
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
