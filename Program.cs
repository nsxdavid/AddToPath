using System;
using System.Windows.Forms;
using Microsoft.Win32;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Security.Principal;

namespace AddToPath
{
    internal static class Program
    {
        private const string AppName = "Add to PATH";
        private const string MenuName = "Path";
        private static readonly string InstallDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "AddToPath");
        private static readonly string ExePath = Path.Combine(InstallDir, "AddToPath.exe");

        [STAThread]
        static void Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            if (args.Length > 0)
            {
                if (!IsRunningAsAdmin())
                {
                    RestartAsAdmin(args);
                    return;
                }

                switch (args[0].ToLower())
                {
                    case "--uninstall":
                        UninstallContextMenu();
                        return;
                    case "--addtopath":
                        if (args.Length > 1 && Directory.Exists(args[1]))
                        {
                            AddToPath(args[1]);
                        }
                        return;
                    case "--removefrompath":
                        if (args.Length > 1 && Directory.Exists(args[1]))
                        {
                            RemoveFromPath(args[1]);
                        }
                        return;
                    case "--showpaths":
                        ShowPathDialog();
                        return;
                    case "--install":
                        InstallContextMenu();
                        MessageBox.Show(
                            "Context menu installed successfully!\nYou can now right-click any folder to access PATH options.",
                            "Success",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Information);
                        return;
                }
            }

            if (!IsInstalledInProgramFiles())
            {
                Application.Run(new MainForm());
                return;
            }

            // If no arguments and running from Program Files, show the main form
            Application.Run(new MainForm());
        }

        public static bool IsInstalledInProgramFiles()
        {
            return File.Exists(ExePath);
        }

        public static bool IsRunningAsAdmin()
        {
            using (WindowsIdentity identity = WindowsIdentity.GetCurrent())
            {
                WindowsPrincipal principal = new WindowsPrincipal(identity);
                return principal.IsInRole(WindowsBuiltInRole.Administrator);
            }
        }

        public static void RestartAsAdmin(string[] args = null)
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

        public static void InstallContextMenu()
        {
            if (!IsRunningAsAdmin())
            {
                RestartAsAdmin(new[] { "--install" });
                return;
            }

            try
            {
                // Create install directory if it doesn't exist
                Directory.CreateDirectory(InstallDir);

                // Copy executable to Program Files
                File.Copy(Application.ExecutablePath, ExePath, true);

                // Create main menu entry
                using (var key = Registry.ClassesRoot.CreateSubKey(@"Directory\shell\PathMenu"))
                {
                    key.SetValue("", ""); // Empty default value
                    key.SetValue("MUIVerb", MenuName);
                    key.SetValue("ExtendedSubCommandsKey", @"Directory\shell\PathMenu");
                }

                // Create Shell container for submenus
                Registry.ClassesRoot.CreateSubKey(@"Directory\shell\PathMenu\Shell");

                // Add to PATH submenu
                using (var key = Registry.ClassesRoot.CreateSubKey(@"Directory\shell\PathMenu\Shell\Add"))
                {
                    key.SetValue("", ""); // Empty default value
                    key.SetValue("MUIVerb", "Add to PATH");

                    using (var cmdKey = key.CreateSubKey("command"))
                    {
                        cmdKey.SetValue("", $"\"{ExePath}\" --addtopath \"%1\"");
                    }
                }

                // Remove from PATH submenu
                using (var key = Registry.ClassesRoot.CreateSubKey(@"Directory\shell\PathMenu\Shell\Remove"))
                {
                    key.SetValue("", ""); // Empty default value
                    key.SetValue("MUIVerb", "Remove from PATH");

                    using (var cmdKey = key.CreateSubKey("command"))
                    {
                        cmdKey.SetValue("", $"\"{ExePath}\" --removefrompath \"%1\"");
                    }
                }

                // Show PATHs submenu
                using (var key = Registry.ClassesRoot.CreateSubKey(@"Directory\shell\PathMenu\Shell\Show"))
                {
                    key.SetValue("", ""); // Empty default value
                    key.SetValue("MUIVerb", "Show PATHs");

                    using (var cmdKey = key.CreateSubKey("command"))
                    {
                        cmdKey.SetValue("", $"\"{ExePath}\" --showpaths");
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Error installing context menu: {ex.Message}",
                    "Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        public static void UninstallContextMenu()
        {
            if (!IsRunningAsAdmin())
            {
                RestartAsAdmin(new[] { "--uninstall" });
                return;
            }

            try 
            {
                // Remove registry entries
                Registry.ClassesRoot.DeleteSubKeyTree(@"Directory\shell\PathMenu", false);

                // Delete Program Files installation
                if (Directory.Exists(InstallDir))
                {
                    Directory.Delete(InstallDir, true);
                }

                MessageBox.Show(
                    "Context menu removed successfully!",
                    "Success",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Error uninstalling context menu: {ex.Message}",
                    "Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        private static void AddToPath(string path)
        {
            var envPath = Environment.GetEnvironmentVariable("PATH", EnvironmentVariableTarget.Machine) ?? "";
            var paths = envPath.Split(';').Select(p => p.TrimEnd('\\')).ToList();

            if (!paths.Contains(path.TrimEnd('\\')))
            {
                paths.Add(path);
                Environment.SetEnvironmentVariable(
                    "PATH",
                    string.Join(";", paths),
                    EnvironmentVariableTarget.Machine
                );
                MessageBox.Show(
                    $"Added '{path}' to system PATH successfully.",
                    "Success",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information
                );
            }
            else
            {
                MessageBox.Show(
                    $"'{path}' is already in the system PATH.",
                    "Already Added",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information
                );
            }
        }

        private static void RemoveFromPath(string path)
        {
            var envPath = Environment.GetEnvironmentVariable("PATH", EnvironmentVariableTarget.Machine) ?? "";
            var paths = envPath.Split(';').Select(p => p.TrimEnd('\\')).ToList();
            var normalizedPath = path.TrimEnd('\\');

            if (paths.Contains(normalizedPath))
            {
                paths.Remove(normalizedPath);
                Environment.SetEnvironmentVariable(
                    "PATH",
                    string.Join(";", paths),
                    EnvironmentVariableTarget.Machine
                );
                MessageBox.Show(
                    $"Removed '{path}' from system PATH successfully.",
                    "Success",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information
                );
            }
            else
            {
                MessageBox.Show(
                    $"'{path}' is not in the system PATH.",
                    "Not Found",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information
                );
            }
        }

        private static void ShowPathDialog()
        {
            var envPath = Environment.GetEnvironmentVariable("PATH", EnvironmentVariableTarget.Machine) ?? "";
            var paths = envPath.Split(';')
                .Where(p => !string.IsNullOrWhiteSpace(p))
                .Select(p => p.TrimEnd('\\'))
                .OrderBy(p => p)
                .ToList();

            var pathForm = new Form
            {
                Text = "System PATH Entries",
                Size = new System.Drawing.Size(600, 400),
                StartPosition = FormStartPosition.CenterScreen,
                MinimizeBox = false,
                MaximizeBox = false,
                FormBorderStyle = FormBorderStyle.FixedDialog
            };

            var listBox = new ListBox
            {
                Dock = DockStyle.Fill,
                Font = new System.Drawing.Font("Segoe UI", 9.75F),
                IntegralHeight = false
            };
            listBox.Items.AddRange(paths.Cast<object>().ToArray());

            pathForm.Controls.Add(listBox);
            pathForm.ShowDialog();
        }
    }
}