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
        private static string ExePath { get; set; }

        [STAThread]
        static void Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            ExePath = args.Length > 0 && args[0].ToLower() == "--install" 
                ? Path.Combine(InstallDir, "AddToPath.exe")
                : Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "AddToPath.exe");

            if (args.Length > 0)
            {
                string cmd = args[0].ToLower();
                LogMessage($"Command received: {cmd} with {args.Length} arguments");

                // If we have multiple arguments and it's a path command, join them
                string path = null;
                if (args.Length > 1 && (cmd == "--addtosystempath" || cmd == "--removefromsystempath" || cmd == "--addtouserpath" || cmd == "--removefromuserpath"))
                {
                    // Join all arguments after the command into a single path
                    path = string.Join(" ", args.Skip(1));
                    LogMessage($"Reconstructed path: {path}");
                }
                else if (args.Length > 1)
                {
                    path = args[1];
                    LogMessage($"Argument 1: {path}");
                }

                bool needsAdmin = cmd == "--install" || 
                                cmd == "--uninstall" || 
                                cmd == "--addtosystempath" || 
                                cmd == "--removefromsystempath";

                if (needsAdmin && !IsRunningAsAdmin())
                {
                    RestartAsAdmin(args);
                    return;
                }

                switch (cmd)
                {
                    case "--uninstall":
                        UninstallContextMenu();
                        return;
                    case "--addtouserpath":
                        if (path != null && Directory.Exists(path))
                        {
                            AddToPath(path, false);
                        }
                        return;
                    case "--addtosystempath":
                        if (path != null && Directory.Exists(path))
                        {
                            AddToPath(path, true);
                        }
                        return;
                    case "--removefromuserpath":
                        if (path != null && Directory.Exists(path))
                        {
                            RemoveFromPath(path, false);
                        }
                        return;
                    case "--removefromsystempath":
                        if (path != null && Directory.Exists(path))
                        {
                            RemoveFromPath(path, true);
                        }
                        return;
                    case "--showpaths":
                        Log($"Showing all paths");
                        ShowPaths(true, true);
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

        private static bool AreOtherInstancesRunning()
        {
            var currentProcess = Process.GetCurrentProcess();
            var processes = Process.GetProcessesByName(Path.GetFileNameWithoutExtension(ExePath));
            return processes.Any(p => p.Id != currentProcess.Id);
        }

        private static bool KillOtherInstances()
        {
            var currentProcess = Process.GetCurrentProcess();
            var processes = Process.GetProcessesByName(Path.GetFileNameWithoutExtension(ExePath));
            var otherProcesses = processes.Where(p => p.Id != currentProcess.Id).ToList();
            
            if (!otherProcesses.Any())
                return true;

            var result = MessageBox.Show(
                "Other instances of Add to PATH are running and must be closed to continue.\n\n" +
                "Would you like to close them now?",
                "Close Running Instances",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (result != DialogResult.Yes)
                return false;

            foreach (var process in otherProcesses)
            {
                try
                {
                    process.Kill();
                    process.WaitForExit(5000); // Wait up to 5 seconds for each process
                }
                catch (Exception ex)
                {
                    LogMessage($"Failed to kill process {process.Id}: {ex.Message}");
                }
            }

            // Double check all processes were killed
            return !AreOtherInstancesRunning();
        }

        public static void InstallContextMenu()
        {
            if (!IsRunningAsAdmin())
            {
                RestartAsAdmin(new[] { "--install" });
                return;
            }

            if (AreOtherInstancesRunning())
            {
                if (!KillOtherInstances())
                {
                    MessageBox.Show(
                        "Installation cannot proceed while other instances are running.\n" +
                        "Please close all instances of Add to PATH and try again.",
                        "Installation Cancelled",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                    return;
                }
            }

            try
            {
                // Clean up any existing registry entries first
                try 
                {
                    Registry.ClassesRoot.DeleteSubKeyTree(@"Directory\shell\PathMenu", false);
                } 
                catch (Exception) { /* Ignore if key doesn't exist */ }

                try 
                {
                    Registry.ClassesRoot.DeleteSubKeyTree(@"Directory\shell\Path", false);
                } 
                catch (Exception) { /* Ignore if key doesn't exist */ }

                // Create install directory if it doesn't exist
                Directory.CreateDirectory(InstallDir);

                // Copy executable to Program Files
                File.Copy(Application.ExecutablePath, ExePath, true);

                // Create main menu entry
                using (var key = Registry.ClassesRoot.CreateSubKey(@"Directory\shell\Path"))
                {
                    key.SetValue("", ""); // Empty default value
                    key.SetValue("MUIVerb", "Path");
                    key.SetValue("SubCommands", "AddToPath;RemoveFromPath;ShowPaths"); // Main menu commands
                }

                // Create Shell container for submenus
                Registry.ClassesRoot.CreateSubKey(@"Directory\shell\Path\Shell");

                // Add to PATH submenu
                using (var addKey = Registry.ClassesRoot.CreateSubKey(@"Directory\shell\Path\Shell\AddToPath"))
                {
                    addKey.SetValue("", ""); // Empty default value
                    addKey.SetValue("MUIVerb", "Add to PATH");
                    addKey.SetValue("SubCommands", ""); // This tells Windows it has subcommands

                    // Add User PATH command
                    using (var userKey = addKey.CreateSubKey(@"Shell\User"))
                    {
                        userKey.SetValue("MUIVerb", "User PATH");
                        using (var cmdKey = userKey.CreateSubKey("command"))
                        {
                            cmdKey.SetValue("", $"\"{ExePath}\" --addtouserpath \"%1\"");
                        }
                    }

                    // Add System PATH command
                    using (var systemKey = addKey.CreateSubKey(@"Shell\System"))
                    {
                        systemKey.SetValue("MUIVerb", "System PATH");
                        using (var cmdKey = systemKey.CreateSubKey("command"))
                        {
                            cmdKey.SetValue("", $"\"{ExePath}\" --addtosystempath \"%1\"");
                        }
                    }
                }

                // Remove from PATH submenu
                using (var removeKey = Registry.ClassesRoot.CreateSubKey(@"Directory\shell\Path\Shell\RemoveFromPath"))
                {
                    removeKey.SetValue("", ""); // Empty default value
                    removeKey.SetValue("MUIVerb", "Remove from PATH");
                    removeKey.SetValue("SubCommands", ""); // This tells Windows it has subcommands

                    // Remove User PATH command
                    using (var userKey = removeKey.CreateSubKey(@"Shell\User"))
                    {
                        userKey.SetValue("MUIVerb", "User PATH");
                        using (var cmdKey = userKey.CreateSubKey("command"))
                        {
                            cmdKey.SetValue("", $"\"{ExePath}\" --removefromuserpath \"%1\"");
                        }
                    }

                    // Remove System PATH command
                    using (var systemKey = removeKey.CreateSubKey(@"Shell\System"))
                    {
                        systemKey.SetValue("MUIVerb", "System PATH");
                        using (var cmdKey = systemKey.CreateSubKey("command"))
                        {
                            cmdKey.SetValue("", $"\"{ExePath}\" --removefromsystempath \"%1\"");
                        }
                    }
                }

                // Show PATHs command (simplified)
                using (var showKey = Registry.ClassesRoot.CreateSubKey(@"Directory\shell\Path\Shell\ShowPaths"))
                {
                    showKey.SetValue("MUIVerb", "Show PATHs");
                    using (var cmdKey = showKey.CreateSubKey("command"))
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

            if (AreOtherInstancesRunning())
            {
                if (!KillOtherInstances())
                {
                    MessageBox.Show(
                        "Uninstallation cannot proceed while other instances are running.\n" +
                        "Please close all instances of Add to PATH and try again.",
                        "Uninstallation Cancelled",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                    return;
                }
            }

            try 
            {
                // Remove registry entries
                Registry.ClassesRoot.DeleteSubKeyTree(@"Directory\shell\Path", false);

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

        public static void LogMessage(string message)
        {
            try
            {
                var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                var logPath = Path.Combine(Path.GetTempPath(), "AddToPath.log");
                File.AppendAllText(logPath, $"{timestamp} - {message}\n");
            }
            catch
            {
                // Ignore logging errors
            }
        }

        private static void Log(string message)
        {
            LogMessage(message);
        }

        private static void AddToPath(string path, bool isSystem)
        {
            try 
            {
                EnvironmentVariableTarget target = isSystem ? EnvironmentVariableTarget.Machine : EnvironmentVariableTarget.User;
                var envPath = Environment.GetEnvironmentVariable("PATH", target) ?? "";
                var paths = envPath.Split(';').Select(p => p.TrimEnd('\\')).ToList();
                var normalizedPath = path.TrimEnd('\\');

                Log($"Current PATH: {envPath}");
                Log($"Adding path: {normalizedPath}");

                if (!paths.Contains(normalizedPath))
                {
                    paths.Add(normalizedPath);
                    var newPath = string.Join(";", paths);
                    Log($"New PATH will be: {newPath}");

                    Environment.SetEnvironmentVariable(
                        "PATH",
                        newPath,
                        target
                    );

                    var verifyPath = Environment.GetEnvironmentVariable("PATH", target) ?? "";
                    Log($"Verified PATH after set: {verifyPath}");

                    MessageBox.Show(
                        $"Added '{path}' to {(isSystem ? "system" : "user")} PATH successfully.",
                        "Success",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information
                    );
                }
                else
                {
                    Log($"Path already exists in: {envPath}");
                    MessageBox.Show(
                        $"'{path}' is already in the {(isSystem ? "system" : "user")} PATH.",
                        "Already Added",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information
                    );
                }
            }
            catch (Exception ex)
            {
                Log($"Error in AddToPath: {ex}");
                MessageBox.Show(
                    $"Error adding to PATH: {ex.Message}",
                    "Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error
                );
            }
        }

        private static void RemoveFromPath(string path, bool isSystem)
        {
            EnvironmentVariableTarget target = isSystem ? EnvironmentVariableTarget.Machine : EnvironmentVariableTarget.User;
            var envPath = Environment.GetEnvironmentVariable("PATH", target) ?? "";
            var paths = envPath.Split(';').Select(p => p.TrimEnd('\\')).ToList();
            var normalizedPath = path.TrimEnd('\\');

            if (paths.Contains(normalizedPath))
            {
                paths.Remove(normalizedPath);
                Environment.SetEnvironmentVariable(
                    "PATH",
                    string.Join(";", paths),
                    target
                );
                MessageBox.Show(
                    $"Removed '{path}' from {(isSystem ? "system" : "user")} PATH successfully.",
                    "Success",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information
                );
            }
            else
            {
                MessageBox.Show(
                    $"'{path}' is not in the {(isSystem ? "system" : "user")} PATH.",
                    "Not Found",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information
                );
            }
        }

        private static void ShowPaths(bool showUser = true, bool showSystem = true)
        {
            try
            {
                Log($"ShowPaths called with showUser={showUser}, showSystem={showSystem}");
                using (var dialog = new PathsDialog(showUser, showSystem))
                {
                    Log("Created PathsDialog, showing dialog...");
                    Application.Run(dialog);
                }
            }
            catch (Exception ex)
            {
                Log($"Error in ShowPaths: {ex}");
                MessageBox.Show($"Error showing paths: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}