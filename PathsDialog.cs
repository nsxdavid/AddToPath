using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace AddToPath
{
    public class PathsDialog : Form
    {
        private readonly ListView listView;

        public PathsDialog()
        {
            Text = "System PATH Entries";
            Size = new Size(600, 400);
            MinimumSize = new Size(400, 300);
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.Sizable;

            listView = new ListView
            {
                Dock = DockStyle.Fill,
                View = View.Details,
                FullRowSelect = true,
                GridLines = true,
                HeaderStyle = ColumnHeaderStyle.Nonclickable,
                Scrollable = true
            };
            listView.Columns.Add("Path", -2, HorizontalAlignment.Left);
            Controls.Add(listView);

            LoadPaths();
        }

        private void LoadPaths()
        {
            listView.Items.Clear();
            var paths = Environment.GetEnvironmentVariable("PATH", EnvironmentVariableTarget.Machine)?.Split(';') ?? Array.Empty<string>();
            
            foreach (var path in paths.Where(p => !string.IsNullOrWhiteSpace(p)))
            {
                var item = new ListViewItem(path.Trim());
                item.ToolTipText = path.Trim(); // Show full path on hover
                listView.Items.Add(item);
            }

            // Auto-size column after adding items
            listView.Columns[0].Width = -2;
        }

        protected override void OnSizeChanged(EventArgs e)
        {
            base.OnSizeChanged(e);
            // Keep column width matched to form width
            if (listView != null && listView.Columns.Count > 0)
            {
                listView.Columns[0].Width = -2;
            }
        }
    }
}
