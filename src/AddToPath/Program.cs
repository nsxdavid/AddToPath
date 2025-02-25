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
        public const int HWND_BROADCAST = 0xFFFF;
        public const int WM_SETTINGCHANGE = 0x001A;
        public const uint SMTO_ABORTIFHUNG = 0x0002;
        public const int SW_RESTORE = 9;
        public const int WM_APP = 0x8000;
        public const int WM_REFRESH_PATHS = WM_APP + 1;

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern IntPtr SendMessage(IntPtr hWnd, int Msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        public static extern IntPtr SendMessageTimeout(
            IntPtr hWnd,
            uint Msg,
            UIntPtr wParam,
            string lParam,
            uint fuFlags,
            uint uTimeout,
            out UIntPtr lpdwResult);

        [DllImport("user32.dll")]
        public static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
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
                case "--checkpath":
                    if (path != null)
                    {
                        var (inUserPath, inSystemPath) = CheckPathLocation(path);
                        string msg;
                        if (!inUserPath && !inSystemPath)
                            msg = $"Path {path} is not in either PATH";
                        else if (inUserPath && inSystemPath)
                            msg = $"Path {path} is in both user and system PATH";
                        else if (inUserPath)
                            msg = $"Path {path} is in user PATH";
                        else
                            msg = $"Path {path} is in system PATH";
                        LogMessage(msg, LogLevel.Info, "Program");
                        MessageBox.Show(msg, "Path Status", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    return;
                case "--install":
                    if (InstallContextMenu())
                    {
                        MessageBox.Show(
                            "AddToPath GUI and CLI (a2p) tools installed successfully!\n" +
                            "You can now:\n" +
                            "1. Use the context menu to manage PATH entries\n" +
                            "2. Run 'a2p' from any terminal to manage PATH entries",
                            "Installation Complete",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Information);
                    }
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
                "Other instances of AddToPath GUI and CLI (a2p) are running and must be closed to continue.\n\n" +
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

        public static bool InstallContextMenu()
        {
            if (!IsRunningAsAdmin())
            {
                RestartAsAdmin(new[] { "--install" });
                return false;
            }

            if (AreOtherInstancesRunning())
            {
                if (!KillOtherInstances())
                {
                    MessageBox.Show(
                        "Installation cannot proceed while other instances are running.\n" +
                        "Please close all instances of AddToPath GUI and CLI (a2p) and try again.",
                        "Installation Cancelled",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                    return false;
                }
            }

            try
            {
                // Create installation directory if it doesn't exist
                if (!Directory.Exists(InstallDir))
                {
                    try
                    {
                        Directory.CreateDirectory(InstallDir);
                    }
                    catch (Exception ex)
                    {
                        LogMessage("Failed to create installation directory", LogLevel.Error, "Installation", ex);
                        MessageBox.Show(
                            $"Installation failed: Could not create installation directory\n{ex.Message}",
                            "Error",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Error);
                        return false;
                    }
                }

                // Check source files exist
                string sourceExe = Application.ExecutablePath;
                string sourceDir = Path.GetDirectoryName(sourceExe);
                string a2pSourcePath = Path.Combine(sourceDir, "a2p.exe");

                if (!File.Exists(sourceExe) || !File.Exists(a2pSourcePath))
                {
                    LogMessage("Required files missing", LogLevel.Error, "Installation");
                    MessageBox.Show(
                        "Installation failed: Required files not found.\n" +
                        "Make sure both AddToPath.exe and a2p.exe are in the same directory.",
                        "Error",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                    return false;
                }

                // Copy executables
                try
                {
                    File.Copy(sourceExe, ExePath, true);
                    File.Copy(a2pSourcePath, Path.Combine(InstallDir, "a2p.exe"), true);
                }
                catch (Exception ex)
                {
                    LogMessage("Failed to copy executables", LogLevel.Error, "Installation", ex);
                    MessageBox.Show(
                        $"Installation failed: Could not copy files to Program Files\n{ex.Message}",
                        "Error",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                    return false;
                }

                // Create helper scripts
                try
                {
                    File.WriteAllText(
                        Path.Combine(InstallDir, "updatepath.ps1"),
                        "$env:Path = [System.Environment]::GetEnvironmentVariable(\"Path\",\"Machine\") + \";\" + [System.Environment]::GetEnvironmentVariable(\"Path\",\"User\")");

                    File.WriteAllText(
                        Path.Combine(InstallDir, "updatepath.bat"),
                        "@echo off\nfor /f \"tokens=2*\" %%a in ('reg query \"HKLM\\SYSTEM\\CurrentControlSet\\Control\\Session Manager\\Environment\" /v Path') do set SYSPATH=%%b\nfor /f \"tokens=2*\" %%a in ('reg query \"HKCU\\Environment\" /v Path') do set USERPATH=%%b\nset PATH=%SYSPATH%;%USERPATH%");
                }
                catch (Exception ex)
                {
                    LogMessage("Failed to create helper scripts", LogLevel.Error, "Installation", ex);
                    MessageBox.Show(
                        $"Installation failed: Could not create helper scripts\n{ex.Message}",
                        "Error",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                    return false;
                }

                // Create registry entries
                try
                {
                    // Create main menu entry
                    using (var key = Registry.ClassesRoot.CreateSubKey(@"Directory\shell\Path"))
                    {
                        key.SetValue("", ""); // Empty default value
                        key.SetValue("MUIVerb", "Path");
                        key.SetValue("Icon", $"\"{ExePath}\""); // Add icon from our executable
                        key.SetValue("SubCommands", "1_AddToPath;2_RemoveFromPath;3_CheckPath;4_ShowPaths"); // Main menu commands
                    }

                    // Create Shell container for submenus
                    Registry.ClassesRoot.CreateSubKey(@"Directory\shell\Path\Shell");

                    // Add to PATH submenu
                    using (var addKey = Registry.ClassesRoot.CreateSubKey(@"Directory\shell\Path\Shell\1_AddToPath"))
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
                    using (var removeKey = Registry.ClassesRoot.CreateSubKey(@"Directory\shell\Path\Shell\2_RemoveFromPath"))
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

                    // Check PATH Status command
                    using (var checkKey = Registry.ClassesRoot.CreateSubKey(@"Directory\shell\Path\Shell\3_CheckPath"))
                    {
                        checkKey.SetValue("MUIVerb", "Check PATH Status");
                        using (var cmdKey = checkKey.CreateSubKey("command"))
                        {
                            cmdKey.SetValue("", $"\"{ExePath}\" --checkpath \"%1\"");
                        }
                    }

                    // Show PATHs command
                    using (var showKey = Registry.ClassesRoot.CreateSubKey(@"Directory\shell\Path\Shell\4_ShowPaths"))
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
                    LogMessage("Failed to create registry entries", LogLevel.Error, "Installation", ex);
                    MessageBox.Show(
                        $"Installation failed: Could not create context menu entries\n{ex.Message}",
                        "Error",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                    return false;
                }

                // Add to system PATH if needed
                try 
                {
                    var (inUserPath, inSystemPath) = CheckPathLocation(InstallDir);
                    if (!inSystemPath)
                    {
                        AddToPath(InstallDir, true, false);
                    }
                }
                catch (Exception ex)
                {
                    LogMessage("Failed to add to system PATH", LogLevel.Error, "Installation", ex);
                    MessageBox.Show(
                        $"Installation failed: Could not add to system PATH\n{ex.Message}",
                        "Error",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                LogMessage("Failed to install context menu", LogLevel.Error, "Registry", ex);
                MessageBox.Show(
                    $"Error installing context menu: {ex.Message}",
                    "Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                return false;
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
                        "Please close all instances of AddToPath GUI and CLI (a2p) and try again.",
                        "Uninstallation Cancelled",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                    return;
                }
            }

            try 
            {
                // Remove registry entries
                Registry.ClassesRoot.DeleteSubKeyTree(@"Directory\shell\" + MenuName, false);
                Registry.ClassesRoot.DeleteSubKeyTree(@"Directory\Background\shell\" + MenuName, false);

                // Remove from PATH
                RemoveFromPath(InstallDir, true, true, true);  // Silent if not found during uninstall

                // Delete installed files
                if (Directory.Exists(InstallDir))
                {
                    foreach (var file in new[] { 
                        "AddToPath.exe", "a2p.exe", 
                        "updatepath.ps1", "updatepath.bat"
                    })
                    {
                        try
                        {
                            File.Delete(Path.Combine(InstallDir, file));
                        }
                        catch
                        {
                            // Ignore individual file deletion errors
                        }
                    }
                    try
                    {
                        Directory.Delete(InstallDir);
                    }
                    catch
                    {
                        // Ignore directory deletion error
                    }
                }

                Log("Uninstalled successfully");
            }
            catch (Exception ex)
            {
                LogMessage("Failed to uninstall", LogLevel.Error, "Uninstall", ex);
                MessageBox.Show($"Failed to uninstall: {ex.Message}", AppName, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private static void BroadcastEnvironmentChange()
        {
            try
            {
                UIntPtr result;
                NativeMethods.SendMessageTimeout(
                    (IntPtr)NativeMethods.HWND_BROADCAST,
                    NativeMethods.WM_SETTINGCHANGE,
                    UIntPtr.Zero,
                    "Environment",
                    NativeMethods.SMTO_ABORTIFHUNG,
                    1000,
                    out result);
                LogMessage("Broadcast environment change notification", LogLevel.Debug, "Environment");
            }
            catch (Exception ex)
            {
                LogMessage("Failed to broadcast environment change", LogLevel.Warning, "Environment", ex);
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
                    var msg = $"Path {path} already exists in {(isSystem ? "system" : "user")} PATH";
                    if (showUI)
                        MessageBox.Show(msg, "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    throw new InvalidOperationException(msg);
                }

                paths.Add(path);
                Environment.SetEnvironmentVariable("PATH", string.Join(";", paths), target);

                if (showUI)
                    MessageBox.Show(
                        $"Added {path} to {(isSystem ? "system" : "user")} PATH.\n\n" +
                        "Run 'updatepath' to refresh PATH in any current terminal.\n",
                        "Success",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                else
                {
                    Console.WriteLine($"Added {path} to {(isSystem ? "system" : "user")} PATH");
                    Console.WriteLine("Run 'updatepath' to refresh PATH in current terminal");
                }

                LogMessage($"Added {path} to {(isSystem ? "system" : "user")} PATH", LogLevel.Info, "PathOperation");
                BroadcastEnvironmentChange();
            }
            catch (Exception ex)
            {
                LogMessage($"Error adding path: {ex.Message}", LogLevel.Error, "PathOperation", ex);
                throw;
            }
        }

        public static bool RemoveFromPath(string path, bool isSystem, bool showUI = true, bool silentIfNotFound = false)
        {
            EnvironmentVariableTarget target = isSystem ? EnvironmentVariableTarget.Machine : EnvironmentVariableTarget.User;
            var envPath = Environment.GetEnvironmentVariable("PATH", target) ?? "";
            var paths = envPath.Split(';').Select(p => p.Trim()).Where(p => !string.IsNullOrWhiteSpace(p)).ToList();

            if (!paths.Contains(path, StringComparer.OrdinalIgnoreCase))
            {
                var msg = $"Path {path} not found in {(isSystem ? "system" : "user")} PATH";
                if (showUI && !silentIfNotFound)
                    MessageBox.Show(msg, "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                if (!silentIfNotFound)
                    throw new InvalidOperationException(msg);
                return false;
            }

            paths.RemoveAll(p => string.Equals(p, path, StringComparison.OrdinalIgnoreCase));
            Environment.SetEnvironmentVariable("PATH", string.Join(";", paths), target);

            if (showUI)
                MessageBox.Show(
                    $"Removed {path} from {(isSystem ? "system" : "user")} PATH.\n\n" +
                    "Run 'updatepath' to refresh PATH in any current terminal.\n",
                    "Success",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            else
            {
                Console.WriteLine($"Removed {path} from {(isSystem ? "system" : "user")} PATH");
                Console.WriteLine("Run 'updatepath' to refresh PATH in current terminal");
            }

            LogMessage($"Removed {path} from {(isSystem ? "system" : "user")} PATH", LogLevel.Info, "PathOperation");

            BroadcastEnvironmentChange();
            return true;
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
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                
                if (!PathsDialog.ShowPathsDialog(showUser, showSystem))
                {
                    LogMessage("Existing paths window found and activated", LogLevel.Info, "Program");
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

        public static (bool InUserPath, bool InSystemPath) CheckPathLocation(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return (false, false);

            // Normalize the path for comparison
            path = Path.GetFullPath(path).TrimEnd('\\');
            
            var userPaths = GetUserPaths()
                .Where(p => !string.IsNullOrWhiteSpace(p))
                .Select(p => Path.GetFullPath(p).TrimEnd('\\'));
                
            var systemPaths = GetSystemPaths()
                .Where(p => !string.IsNullOrWhiteSpace(p))
                .Select(p => Path.GetFullPath(p).TrimEnd('\\'));
                
            return (
                userPaths.Contains(path, StringComparer.OrdinalIgnoreCase),
                systemPaths.Contains(path, StringComparer.OrdinalIgnoreCase)
            );
        }
    }
}