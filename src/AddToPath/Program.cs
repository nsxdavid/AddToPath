// Copyright (c) 2025 David Whatley
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Windows.Forms;
using Microsoft.Win32;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Security.Principal;
using System.Text;
using System.Management;
using System.Runtime.InteropServices;
using System.Drawing;

namespace AddToPath
{
    internal static class NativeMethods
    {
        public const int WM_SETICON = 0x0080;

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern IntPtr SendMessage(IntPtr hWnd, int Msg, IntPtr wParam, IntPtr lParam);
    }

    public enum LogLevel
    {
        Error,
        Warning,
        Info,
        Debug
    }

    internal static class Logger
    {
        private static readonly string LogDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "AddToPath", "Logs");
        
        private const int DAYS_TO_KEEP_LOGS = 7;

        static Logger()
        {
            try
            {
                if (!Directory.Exists(LogDirectory))
                {
                    Directory.CreateDirectory(LogDirectory);
                }
                CleanupOldLogs();
            }
            catch
            {
                // Ignore initialization errors
            }
        }

        public static void Log(LogLevel level, string category, string message, Exception ex = null)
        {
            try
            {
                var logFile = Path.Combine(LogDirectory, $"AddToPath_{DateTime.Now:yyyyMMdd}.log");
                var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                var logMessage = $"{timestamp}|{level}|{category}|{message}";
                
                if (ex != null)
                {
                    logMessage += $"\nException: {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}";
                }

                logMessage += "\n";
                
                File.AppendAllText(logFile, logMessage);
            }
            catch
            {
                // Ignore logging errors
            }
        }

        private static void CleanupOldLogs()
        {
            try
            {
                var cutoff = DateTime.Now.AddDays(-DAYS_TO_KEEP_LOGS);
                var oldLogs = Directory.GetFiles(LogDirectory, "AddToPath_*.log")
                    .Select(f => new FileInfo(f))
                    .Where(f => f.LastWriteTime < cutoff);

                foreach (var log in oldLogs)
                {
                    try
                    {
                        log.Delete();
                    }
                    catch
                    {
                        // Ignore individual file deletion errors
                    }
                }
            }
            catch
            {
                // Ignore cleanup errors
            }
        }
    }

    public static class ProcessExtensions
    {
        public static Process Parent(this Process process)
        {
            try
            {
                using (var query = new ManagementObjectSearcher(
                    $"SELECT ParentProcessId FROM Win32_Process WHERE ProcessId = {process.Id}"))
                {
                    foreach (var mo in query.Get())
                    {
                        var parentId = (uint)mo["ParentProcessId"];
                        return Process.GetProcessById((int)parentId);
                    }
                }
            }
            catch
            {
                // Ignore any errors, just return null
            }
            return null;
        }
    }

    public class Program
    {
        private const string AppName = "Add to PATH";
        private const string MenuName = "Path";
        private static readonly string InstallDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "AddToPath");
        private static string ExePath => Path.Combine(InstallDir, "AddToPath.exe");

        public static void LogMessage(string message, LogLevel level = LogLevel.Info, string category = "General", Exception ex = null)
        {
            Logger.Log(level, category, message, ex);
        }

        private static void Log(string message)
        {
            MessageBox.Show(message, AppName, MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        [STAThread]
        private static void Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            if (args.Length == 0)
            {
                Application.Run(new MainForm());
                return;
            }

            string cmd = args[0].ToLower();
            LogMessage($"Command received: {cmd} with {args.Length} arguments", LogLevel.Info, "Program");

            // If we have multiple arguments and it's a path command, join them
            string path = null;
            if (args.Length > 1 && (cmd == "--addtosystempath" || cmd == "--removefromsystempath" || cmd == "--addtouserpath" || cmd == "--removefromuserpath"))
            {
                // Join all arguments after the command into a single path
                path = string.Join(" ", args.Skip(1));
                LogMessage($"Reconstructed path: {path}", LogLevel.Info, "Program");
            }
            else if (args.Length > 1)
            {
                path = args[1];
                LogMessage($"Argument 1: {path}", LogLevel.Info, "Program");
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
                    LogMessage("Showing all paths", LogLevel.Info, "Program");
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
            try
            {
                // Check if executable exists in Program Files
                if (!File.Exists(ExePath))
                {
                    LogMessage("Executable not found in Program Files", LogLevel.Debug, "Installation");
                    return false;
                }

                // Check if registry keys exist
                using (var key = Registry.ClassesRoot.OpenSubKey(@"Directory\shell\Path"))
                {
                    if (key == null)
                    {
                        LogMessage("Registry key not found", LogLevel.Debug, "Installation");
                        return false;
                    }

                    // Verify the key has our expected structure
                    var subCommands = key.GetValue("SubCommands") as string;
                    if (string.IsNullOrEmpty(subCommands) || !subCommands.Contains("AddToPath"))
                    {
                        LogMessage("Registry key missing expected structure", LogLevel.Debug, "Installation");
                        return false;
                    }
                }

                LogMessage("Installation detected successfully", LogLevel.Debug, "Installation");
                return true;
            }
            catch (Exception ex)
            {
                LogMessage("Error checking installation status", LogLevel.Error, "Installation", ex);
                return false;
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
            catch (Exception ex)
            {
                LogMessage("Failed to restart as admin", LogLevel.Error, "Program", ex);
                MessageBox.Show(
                    "Administrator rights are required to modify the system PATH and registry.",
                    "Admin Rights Required",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }
        }

        private static string GetProcessDetails(Process proc)
        {
            try
            {
                return $"ID={proc.Id}, " +
                       $"Path={proc.MainModule?.FileName ?? "unknown"}, " +
                       $"Started={proc.StartTime:HH:mm:ss.fff}, " +
                       $"Parent={proc.Parent()?.Id ?? 0}";
            }
            catch (Exception ex)
            {
                return $"ID={proc.Id}, Error getting details: {ex.Message}";
            }
        }

        private static bool IsSameApplication(string path1, string path2)
        {
            if (string.Equals(path1, path2, StringComparison.OrdinalIgnoreCase))
                return true;

            // Consider Program Files version as the same application
            var programFilesPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "AddToPath", "AddToPath.exe");
            return (string.Equals(path1, programFilesPath, StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(path2, programFilesPath, StringComparison.OrdinalIgnoreCase));
        }

        private static bool AreOtherInstancesRunning()
        {
            var currentProcess = Process.GetCurrentProcess();
            var currentPath = currentProcess.MainModule?.FileName;
            LogMessage($"Current process: {GetProcessDetails(currentProcess)}", LogLevel.Debug, "Program");
            
            var processes = Process.GetProcessesByName(Path.GetFileNameWithoutExtension(ExePath));
            LogMessage($"Found {processes.Length} total processes with our name", LogLevel.Debug, "Program");
            
            foreach (var proc in processes)
            {
                LogMessage($"Found process: {GetProcessDetails(proc)}", LogLevel.Debug, "Program");
            }
            
            return processes.Any(p => p.Id != currentProcess.Id && 
                                   IsSameApplication(p.MainModule?.FileName, currentPath));
        }

        private static bool KillOtherInstances()
        {
            var currentProcess = Process.GetCurrentProcess();
            var currentPath = currentProcess.MainModule?.FileName;
            var processes = Process.GetProcessesByName(Path.GetFileNameWithoutExtension(ExePath));
            var otherProcesses = processes
                .Where(p => p.Id != currentProcess.Id && 
                           IsSameApplication(p.MainModule?.FileName, currentPath))
                .ToList();
            
            if (!otherProcesses.Any())
            {
                LogMessage("No other instances found to kill", LogLevel.Debug, "Program");
                return true;
            }

            LogMessage($"Found {otherProcesses.Count} other instances to kill", LogLevel.Debug, "Program");
            foreach (var proc in otherProcesses)
            {
                LogMessage($"Will attempt to kill: {GetProcessDetails(proc)}", LogLevel.Debug, "Program");
            }
            
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
                    LogMessage($"Failed to kill process {process.Id}", LogLevel.Error, "Program", ex);
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
                catch (Exception ex)
                {
                    LogMessage("Failed to delete PathMenu registry key", LogLevel.Error, "Registry", ex);
                }

                try 
                {
                    Registry.ClassesRoot.DeleteSubKeyTree(@"Directory\shell\Path", false);
                } 
                catch (Exception ex)
                {
                    LogMessage("Failed to delete Path registry key", LogLevel.Error, "Registry", ex);
                }

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
                LogMessage("Failed to install context menu", LogLevel.Error, "Registry", ex);
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
                LogMessage("Failed to uninstall context menu", LogLevel.Error, "Registry", ex);
                MessageBox.Show(
                    $"Error uninstalling context menu: {ex.Message}",
                    "Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        public static void AddToPath(string path, bool isSystem, bool showUI = true)
        {
            try 
            {
                if (string.IsNullOrWhiteSpace(path))
                {
                    if (showUI)
                        MessageBox.Show("Please enter a valid path.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    throw new ArgumentException("Path cannot be empty");
                }

                if (!Directory.Exists(path))
                {
                    if (showUI)
                        MessageBox.Show("Directory does not exist.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    throw new ArgumentException("Directory does not exist");
                }

                EnvironmentVariableTarget target = isSystem ? EnvironmentVariableTarget.Machine : EnvironmentVariableTarget.User;
                var envPath = Environment.GetEnvironmentVariable("PATH", target) ?? "";
                var paths = envPath.Split(';').Select(p => p.Trim()).Where(p => !string.IsNullOrWhiteSpace(p)).ToList();

                if (paths.Contains(path, StringComparer.OrdinalIgnoreCase))
                {
                    if (showUI)
                        MessageBox.Show("Path already exists.", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    throw new InvalidOperationException("Path already exists");
                }

                paths.Add(path);
                Environment.SetEnvironmentVariable("PATH", string.Join(";", paths), target);

                if (showUI)
                    MessageBox.Show("Path added successfully.", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);

                LogMessage($"Added {path} to {(isSystem ? "system" : "user")} PATH", LogLevel.Info, "PathOperation");
            }
            catch (Exception ex)
            {
                LogMessage($"Error adding path: {ex.Message}", LogLevel.Error, "PathOperation", ex);
                throw;
            }
        }

        public static void RemoveFromPath(string path, bool isSystem, bool showUI = true)
        {
            EnvironmentVariableTarget target = isSystem ? EnvironmentVariableTarget.Machine : EnvironmentVariableTarget.User;
            var envPath = Environment.GetEnvironmentVariable("PATH", target) ?? "";
            var paths = envPath.Split(';').Select(p => p.Trim()).Where(p => !string.IsNullOrWhiteSpace(p)).ToList();

            if (!paths.Contains(path, StringComparer.OrdinalIgnoreCase))
            {
                if (showUI)
                    MessageBox.Show("Path not found.", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                throw new InvalidOperationException("Path not found");
            }

            paths.RemoveAll(p => string.Equals(p, path, StringComparison.OrdinalIgnoreCase));
            Environment.SetEnvironmentVariable("PATH", string.Join(";", paths), target);

            if (showUI)
                MessageBox.Show("Path removed successfully.", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);

            LogMessage($"Removed {path} from {(isSystem ? "system" : "user")} PATH", LogLevel.Info, "PathOperation");
        }

        public static void AddToUserPath(string path)
        {
            AddToPath(path, false, false);
        }

        public static void AddToSystemPath(string path)
        {
            AddToPath(path, true, false);
        }

        public static void RemoveFromUserPath(string path)
        {
            RemoveFromPath(path, false, false);
        }

        public static void RemoveFromSystemPath(string path)
        {
            RemoveFromPath(path, true, false);
        }

        public static void ShowPaths(bool showUser = true, bool showSystem = true)
        {
            try
            {
                LogMessage($"ShowPaths called with showUser={showUser}, showSystem={showSystem}", LogLevel.Info, "Program");
                using (var dialog = new PathsDialog(showUser, showSystem))
                {
                    LogMessage("Created PathsDialog, showing dialog...", LogLevel.Info, "Program");
                    Application.Run(dialog);
                }
            }
            catch (Exception ex)
            {
                LogMessage("Failed to show paths", LogLevel.Error, "Program", ex);
                MessageBox.Show($"Error showing paths: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        public static string[] GetUserPaths()
        {
            return Environment.GetEnvironmentVariable("PATH", EnvironmentVariableTarget.User)?.Split(';') ?? new string[0];
        }

        public static string[] GetSystemPaths()
        {
            return Environment.GetEnvironmentVariable("PATH", EnvironmentVariableTarget.Machine)?.Split(';') ?? new string[0];
        }
    }
}