using System;
using System.Windows.Forms;
using Microsoft.Win32;
using System.Security.Principal;
using System.Diagnostics;
using System.IO;

namespace AddToPath
{
    internal static class Program
    {
        private static readonly string ProgramFilesPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            "AddToPath");
        private static readonly string InstalledExePath = Path.Combine(ProgramFilesPath, "AddToPath.exe");

        [STAThread]
        static void Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            if (args.Length > 0)
            {
                // If we have arguments, we're either:
                // 1. Being called from context menu with a folder path
                // 2. Being called with --uninstall
                HandleCommandLineArgs(args);
                return;
            }

            // Check if we're running elevated without arguments
            // This means we were restarted for installation
            if (IsRunningAsAdmin())
            {
                InstallApplication();
                return;
            }

            // No arguments and not elevated - show the main form
            Application.Run(new MainForm());
        }

        private static void HandleCommandLineArgs(string[] args)
        {
            if (!IsRunningAsAdmin())
            {
                RestartAsAdmin(args);
                return;
            }

            if (args[0].ToLower() == "--uninstall")
            {
                UninstallContextMenu();
            }
            else
            {
                // Assume it's a folder path
                string folderPath = args[0].Trim('"');
                AddFolderToPath(folderPath);
            }
        }

        public static bool IsInstalledInProgramFiles()
        {
            // Check if we're running from Program Files
            bool runningFromProgramFiles = Application.ExecutablePath.Equals(
                InstalledExePath, 
                StringComparison.OrdinalIgnoreCase);

            // Or check if the Program Files copy exists
            bool installedCopyExists = File.Exists(InstalledExePath);

            return runningFromProgramFiles || installedCopyExists;
        }

        public static void InstallApplication()
        {
            if (!IsRunningAsAdmin())
            {
                RestartAsAdmin();
                return;
            }

            try
            {
                string currentExePath = Application.ExecutablePath;

                // Create program files directory if it doesn't exist
                if (!Directory.Exists(ProgramFilesPath))
                {
                    Directory.CreateDirectory(ProgramFilesPath);
                }

                // If we're not already in Program Files, copy ourselves there
                if (!IsInstalledInProgramFiles())
                {
                    if (File.Exists(InstalledExePath))
                    {
                        File.Delete(InstalledExePath);
                    }
                    File.Copy(currentExePath, InstalledExePath);

                    // Also copy the manifest file
                    string manifestPath = Path.Combine(
                        Path.GetDirectoryName(currentExePath),
                        "app.manifest");
                    if (File.Exists(manifestPath))
                    {
                        File.Copy(manifestPath,
                            Path.Combine(ProgramFilesPath, "app.manifest"),
                            true);
                    }

                    // Start the installed version and exit this one
                    Process.Start(InstalledExePath);
                    Environment.Exit(0);
                }

                // Install the context menu
                InstallContextMenuOnly();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Error installing application: {ex.Message}",
                    "Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        public static void InstallContextMenuOnly()
        {
            if (!IsRunningAsAdmin())
            {
                RestartAsAdmin();
                return;
            }

            try
            {
                using (RegistryKey key = Registry.ClassesRoot.CreateSubKey(@"Directory\shell\AddToPath"))
                {
                    key.SetValue("", "Add to System PATH");
                    key.SetValue("Icon", "%SystemRoot%\\System32\\shell32.dll,3");
                }

                using (RegistryKey key = Registry.ClassesRoot.CreateSubKey(@"Directory\shell\AddToPath\command"))
                {
                    key.SetValue("", $"\"{InstalledExePath}\" \"%1\"");
                }

                MessageBox.Show(
                    "Context menu installed successfully!\nYou can now right-click any folder and select 'Add to System PATH'.",
                    "Success",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
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
                // Remove the context menu entries
                Registry.ClassesRoot.DeleteSubKeyTree(@"Directory\shell\AddToPath", false);

                // Remove the program files directory
                if (Directory.Exists(ProgramFilesPath))
                {
                    Directory.Delete(ProgramFilesPath, true);
                }

                MessageBox.Show(
                    "Application has been uninstalled successfully.",
                    "Uninstall Complete",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Error uninstalling application: {ex.Message}",
                    "Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        private static void AddFolderToPath(string folderPath)
        {
            try
            {
                string currentPath = Environment.GetEnvironmentVariable("PATH", EnvironmentVariableTarget.Machine) ?? "";
                string[] paths = currentPath.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);

                // Check if path already exists (case-insensitive)
                if (Array.Exists(paths, p => string.Equals(p, folderPath, StringComparison.OrdinalIgnoreCase)))
                {
                    MessageBox.Show(
                        "This folder is already in the system PATH.",
                        "Information",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                    return;
                }

                // Add the new path
                string newPath = currentPath.TrimEnd(';') + ";" + folderPath;
                Environment.SetEnvironmentVariable("PATH", newPath, EnvironmentVariableTarget.Machine);

                MessageBox.Show(
                    $"Added '{folderPath}' to system PATH successfully!",
                    "Success",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Error adding to PATH: {ex.Message}\n\nMake sure to run as administrator.",
                    "Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        public static bool IsRunningAsAdmin()
        {
            using (WindowsIdentity identity = WindowsIdentity.GetCurrent())
            {
                WindowsPrincipal principal = new WindowsPrincipal(identity);
                return principal.IsInRole(WindowsBuiltInRole.Administrator);
            }
        }

        private static void RestartAsAdmin(string[] args = null)
        {
            ProcessStartInfo proc = new ProcessStartInfo
            {
                UseShellExecute = true,
                WorkingDirectory = Environment.CurrentDirectory,
                FileName = Application.ExecutablePath,
                Verb = "runas"
            };

            // Add arguments if present
            if (args != null && args.Length > 0)
            {
                proc.Arguments = string.Join(" ", args);
            }

            try
            {
                Process.Start(proc);
            }
            catch (Exception)
            {
                MessageBox.Show(
                    "This tool needs to be run as administrator.",
                    "Admin Rights Required",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }
        }
    }
}