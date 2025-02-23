// Copyright (c) 2025 David Whatley
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Drawing;
using System.Windows.Forms;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Win32;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace AddToPath
{
    /// <summary>
    /// Dialog for displaying and managing PATH environment variables.
    /// Implements window position/size persistence and handles environment change notifications.
    /// Only one instance of this dialog can exist across all processes.
    /// </summary>
    public partial class PathsDialog : Form
    {
        private readonly RichTextBox pathsTextBox;
        private readonly bool showUser;
        private readonly bool showSystem;
        private readonly Panel headerPanel;
        private readonly Label titleLabel;

        // Constants for window identification and registry storage
        private const string WINDOW_TITLE = "AddToPath - View PATH Variables";
        private const string REG_KEY_PATH = @"Software\AddToPath";
        
        // Registry value names for storing window position and state
        private const string REG_VALUE_WINDOW_LEFT = "PathsDialogLeft";
        private const string REG_VALUE_WINDOW_TOP = "PathsDialogTop";
        private const string REG_VALUE_WINDOW_WIDTH = "PathsDialogWidth";
        private const string REG_VALUE_WINDOW_HEIGHT = "PathsDialogHeight";
        private const string REG_VALUE_WINDOW_STATE = "PathsDialogState";

        /// <summary>
        /// Factory method to show the paths dialog. Ensures only one instance exists across processes.
        /// If an existing window is found, it will be activated and its content refreshed.
        /// </summary>
        /// <param name="showUser">Whether to show user PATH entries</param>
        /// <param name="showSystem">Whether to show system PATH entries</param>
        /// <returns>True if a new window was created, false if existing window was activated</returns>
        public static bool ShowPathsDialog(bool showUser = true, bool showSystem = true)
        {
            // Check for existing window first
            var existingWindow = FindExistingPathsWindow();
            if (existingWindow != IntPtr.Zero)
            {
                // Restore window if minimized
                NativeMethods.ShowWindow(existingWindow, NativeMethods.SW_RESTORE);
                // Bring to front
                NativeMethods.SetForegroundWindow(existingWindow);
                // Send refresh message
                NativeMethods.SendMessage(existingWindow, NativeMethods.WM_REFRESH_PATHS, IntPtr.Zero, IntPtr.Zero);
                return false;
            }

            // Create and show new dialog
            using (var dialog = new PathsDialog(showUser, showSystem))
            {
                dialog.ShowDialog();
            }
            return true;
        }

        /// <summary>
        /// Searches for an existing instance of the paths dialog across all processes.
        /// </summary>
        /// <returns>Window handle if found, IntPtr.Zero if not found</returns>
        internal static IntPtr FindExistingPathsWindow()
        {
            // Find existing window with the same title
            foreach (Process proc in Process.GetProcesses())
            {
                if (proc.MainWindowTitle == WINDOW_TITLE)
                {
                    var hwnd = proc.MainWindowHandle;
                    // Restore window if minimized
                    NativeMethods.ShowWindow(hwnd, NativeMethods.SW_RESTORE);
                    // Bring to front
                    NativeMethods.SetForegroundWindow(hwnd);
                    // Send refresh message
                    NativeMethods.SendMessage(hwnd, NativeMethods.WM_REFRESH_PATHS, IntPtr.Zero, IntPtr.Zero);
                    return hwnd;
                }
            }
            return IntPtr.Zero;
        }

        private PathsDialog(bool showUser = true, bool showSystem = true)
        {
            try
            {
                this.showUser = showUser;
                this.showSystem = showSystem;

                // Form settings
                Text = WINDOW_TITLE;
                Size = new Size(800, 600);
                StartPosition = FormStartPosition.CenterScreen; // This will be overridden if we have saved settings
                MinimizeBox = true;
                MaximizeBox = true;
                FormBorderStyle = FormBorderStyle.Sizable;
                BackColor = Color.White;
                Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);

                // Load any saved window settings
                LoadWindowSettings();

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
                Logger.Log(LogLevel.Error, "PathsDialog", "Error in PathsDialog constructor", ex);
                throw;
            }
        }

        /// <summary>
        /// Handles window messages including:
        /// - WM_REFRESH_PATHS: Custom message to refresh path contents
        /// - WM_SETTINGCHANGE: System message for environment variable changes
        /// </summary>
        protected override void WndProc(ref Message m)
        {
            if (m.Msg == NativeMethods.WM_REFRESH_PATHS)
            {
                LoadPaths();
                m.Result = IntPtr.Zero;
                return;
            }
            else if (m.Msg == NativeMethods.WM_SETTINGCHANGE)
            {
                // Check if this is an Environment change
                string param = Marshal.PtrToStringAuto(m.LParam);
                if (param == "Environment")
                {
                    Logger.Log(LogLevel.Info, "PathsDialog", "Received environment change notification, refreshing paths");
                    LoadPaths();
                }
            }
            base.WndProc(ref m);
        }

        /// <summary>
        /// Saves the current window position, size, and state to the registry.
        /// This allows the window to reopen in the same position next time.
        /// </summary>
        private void SaveWindowSettings()
        {
            try
            {
                using (var key = Registry.CurrentUser.CreateSubKey(REG_KEY_PATH))
                {
                    if (WindowState == FormWindowState.Normal)
                    {
                        key.SetValue(REG_VALUE_WINDOW_LEFT, Left, RegistryValueKind.DWord);
                        key.SetValue(REG_VALUE_WINDOW_TOP, Top, RegistryValueKind.DWord);
                        key.SetValue(REG_VALUE_WINDOW_WIDTH, Width, RegistryValueKind.DWord);
                        key.SetValue(REG_VALUE_WINDOW_HEIGHT, Height, RegistryValueKind.DWord);
                    }
                    key.SetValue(REG_VALUE_WINDOW_STATE, (int)WindowState, RegistryValueKind.DWord);
                }
            }
            catch (Exception ex)
            {
                Logger.Log(LogLevel.Warning, "PathsDialog", "Failed to save window settings", ex);
            }
        }

        /// <summary>
        /// Loads and applies previously saved window position, size, and state from the registry.
        /// Ensures the window remains visible on screen even if the saved position would put it off-screen.
        /// </summary>
        private void LoadWindowSettings()
        {
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(REG_KEY_PATH))
                {
                    if (key != null)
                    {
                        // Load window state first
                        var state = key.GetValue(REG_VALUE_WINDOW_STATE);
                        if (state != null)
                        {
                            WindowState = (FormWindowState)((int)state);
                        }

                        // Only restore position/size if we have all values and window isn't maximized
                        if (WindowState != FormWindowState.Maximized)
                        {
                            var left = key.GetValue(REG_VALUE_WINDOW_LEFT);
                            var top = key.GetValue(REG_VALUE_WINDOW_TOP);
                            var width = key.GetValue(REG_VALUE_WINDOW_WIDTH);
                            var height = key.GetValue(REG_VALUE_WINDOW_HEIGHT);

                            if (left != null && top != null && width != null && height != null)
                            {
                                // Ensure the window will be visible on screen
                                var screen = Screen.FromPoint(new Point((int)left, (int)top));
                                var workingArea = screen.WorkingArea;

                                Left = Math.Max(workingArea.Left, Math.Min((int)left, workingArea.Right - (int)width));
                                Top = Math.Max(workingArea.Top, Math.Min((int)top, workingArea.Bottom - (int)height));
                                Width = (int)width;
                                Height = (int)height;
                                StartPosition = FormStartPosition.Manual;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Log(LogLevel.Warning, "PathsDialog", "Failed to load window settings", ex);
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
                Logger.Log(LogLevel.Error, "PathsDialog", "Error in LoadPaths", ex);
                throw;
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            SaveWindowSettings();
            base.OnFormClosing(e);
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
