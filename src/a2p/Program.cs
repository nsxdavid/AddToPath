// Copyright (c) 2025 David Whatley
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Principal;
using System.Threading;

namespace a2p
{
    /// <summary>
    /// Command-line interface for AddToPath. Provides add/remove functionality for PATH environment variables
    /// with support for both user and system paths. System path modifications require administrator rights
    /// and are handled through a secure UAC elevation process.
    /// </summary>
    class Program
    {
        static int Main(string[] args)
        {
            try
            {
                // Check if we're the elevated instance by looking for the output file parameter
                string outputFile = null;
                if (args.Length > 0 && args[0].StartsWith("--output-file="))
                {
                    outputFile = args[0].Substring("--output-file=".Length);
                    args = args.Skip(1).ToArray(); // Remove the output file argument
                    AddToPath.Program.LogMessage("Started elevated instance with output file: " + outputFile, AddToPath.LogLevel.Info, "CLI");
                }

                if (args.Length == 0 || args[0].ToLower() == "h" || args[0].ToLower() == "help")
                {
                    ShowUsage();
                    return 0;
                }

                string cmd = args[0].ToLower();
                if (cmd == "l" || cmd == "list")
                {
                    ShowPaths();
                    return 0;
                }

                if (args.Length < 3)
                {
                    ShowUsage();
                    return 1;
                }

                string scope = args[1].ToLower();
                bool isSystem = scope == "s" || scope == "system";
                
                if (scope != "u" && scope != "user" && scope != "s" && scope != "system")
                {
                    Console.WriteLine("Invalid scope. Use:");
                    Console.WriteLine("  u or user: for user PATH");
                    Console.WriteLine("  s or system: for system PATH");
                    return 1;
                }

                string inputPath = args[2];
                string fullPath = Path.GetFullPath(inputPath);

                // If we're attempting to modify the system PATH, we'll need admin rights
                if (isSystem && !AddToPath.Program.IsRunningAsAdmin())
                {
                    // The UAC elevation process works by spawning a new elevated instance of this program.
                    // To maintain a seamless user experience where all output appears in the original console:
                    // 1. The original instance creates a temporary file for communication
                    // 2. The elevated instance is started with this file path and the original arguments
                    // 3. The elevated instance redirects its console output to this file
                    // 4. The original instance monitors the file and displays its contents
                    // This allows us to show output from the elevated process in the original console window.

                    // Create a temporary file that will be used to capture output from the elevated process.
                    // This file is automatically cleaned up after use or in case of errors.
                    string tempFile = Path.Combine(Path.GetTempPath(), $"a2p-{Guid.NewGuid()}.tmp");
                    AddToPath.Program.LogMessage($"Initiating UAC elevation with temp file: {tempFile}", AddToPath.LogLevel.Info, "CLI");
                    
                    try 
                    {
                        Console.WriteLine("Administrator rights are required. Waiting for UAC prompt...");
                        
                        // Start a new elevated instance of ourselves.
                        // UseShellExecute and Verb="runas" trigger the UAC prompt.
                        // WindowStyle=Hidden prevents the elevated console window from flashing.
                        var proc = new ProcessStartInfo
                        {
                            UseShellExecute = true,
                            WorkingDirectory = Environment.CurrentDirectory,
                            FileName = Process.GetCurrentProcess().MainModule.FileName,
                            Verb = "runas",
                            Arguments = $"--output-file={tempFile} {string.Join(" ", args)}",
                            WindowStyle = ProcessWindowStyle.Hidden
                        };

                        AddToPath.Program.LogMessage("Starting elevated process", AddToPath.LogLevel.Info, "CLI");
                        var elevatedProcess = Process.Start(proc);
                        
                        // Monitor the temp file while the elevated process runs.
                        // When content appears, immediately display it and delete the file
                        // to make room for more output.
                        while (!elevatedProcess.HasExited)
                        {
                            Thread.Sleep(100);
                            if (File.Exists(tempFile))
                            {
                                try
                                {
                                    string[] lines = File.ReadAllLines(tempFile);
                                    File.Delete(tempFile);
                                    foreach (var line in lines)
                                    {
                                        Console.WriteLine(line);
                                    }
                                }
                                catch (Exception ex) 
                                { 
                                    AddToPath.Program.LogMessage("Error reading temp file", AddToPath.LogLevel.Error, "CLI", ex);
                                }
                            }
                        }

                        // One final check for output after process exits
                        if (File.Exists(tempFile))
                        {
                            try
                            {
                                string[] lines = File.ReadAllLines(tempFile);
                                File.Delete(tempFile);
                                foreach (var line in lines)
                                {
                                    Console.WriteLine(line);
                                }
                            }
                            catch (Exception ex)
                            {
                                AddToPath.Program.LogMessage("Error reading final temp file", AddToPath.LogLevel.Error, "CLI", ex);
                            }
                        }

                        AddToPath.Program.LogMessage($"Elevated process completed with exit code: {elevatedProcess.ExitCode}", AddToPath.LogLevel.Info, "CLI");
                        return elevatedProcess.ExitCode;
                    }
                    catch (System.ComponentModel.Win32Exception ex) when ((uint)ex.HResult == 0x80004005) // Access Denied (UAC canceled)
                    {
                        AddToPath.Program.LogMessage("UAC elevation was denied by user", AddToPath.LogLevel.Warning, "CLI", ex);
                        Console.WriteLine("Operation canceled - administrator rights were denied.");
                        Console.WriteLine("To modify the system PATH, you must run this command as administrator.");
                        if (File.Exists(tempFile)) File.Delete(tempFile);
                        return 1;
                    }
                    catch (Exception ex)
                    {
                        AddToPath.Program.LogMessage("Failed to start elevated process", AddToPath.LogLevel.Error, "CLI", ex);
                        Console.WriteLine($"Failed to restart as administrator: {ex.Message}");
                        Console.WriteLine("Try running this command as administrator.");
                        if (File.Exists(tempFile)) File.Delete(tempFile);
                        return 1;
                    }
                }

                // If we're the elevated instance, redirect console output to the file
                // so the original process can display it. This ensures all output,
                // including exceptions, appears in the original console window.
                if (outputFile != null)
                {
                    var originalOut = Console.Out;
                    var originalError = Console.Error;
                    try
                    {
                        using (var writer = new StreamWriter(outputFile, false))
                        {
                            Console.SetOut(writer);
                            Console.SetError(writer);
                            try
                            {
                                int result = ExecuteCommand(cmd, isSystem, fullPath);
                                writer.Flush();
                                return result;
                            }
                            catch (InvalidOperationException ex)
                            {
                                // InvalidOperationException is used for normal status messages
                                // like "path not found". These should be displayed without
                                // error formatting or stack traces.
                                AddToPath.Program.LogMessage($"Operation status: {ex.Message}", AddToPath.LogLevel.Info, "CLI");
                                Console.WriteLine(ex.Message);
                                writer.Flush();
                                return 1;
                            }
                            catch (Exception ex)
                            {
                                // Unexpected errors should show full details to help with debugging
                                AddToPath.Program.LogMessage("Unexpected error in elevated process", AddToPath.LogLevel.Error, "CLI", ex);
                                Console.WriteLine($"An unexpected error occurred:");
                                Console.WriteLine(ex.Message);
                                if (ex.InnerException != null)
                                {
                                    Console.WriteLine($"Details: {ex.InnerException.Message}");
                                }
                                Console.WriteLine(ex.StackTrace);
                                writer.Flush();
                                return 1;
                            }
                        }
                    }
                    finally
                    {
                        // Always restore the console output streams
                        Console.SetOut(originalOut);
                        Console.SetError(originalError);
                    }
                }

                return ExecuteCommand(cmd, isSystem, fullPath);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                return 1;
            }
        }

        static int ExecuteCommand(string cmd, bool isSystem, string fullPath)
        {
            if (cmd == "a" || cmd == "add")
            {
                if (isSystem)
                    AddToPath.Program.AddToSystemPath(fullPath);
                else
                    AddToPath.Program.AddToUserPath(fullPath);
                
                return 0;
            }
            else if (cmd == "r" || cmd == "remove")
            {
                if (isSystem)
                    AddToPath.Program.RemoveFromSystemPath(fullPath);
                else
                    AddToPath.Program.RemoveFromUserPath(fullPath);
                
                return 0;
            }
            else
            {
                ShowUsage();
                return 1;
            }
        }

        static void ShowUsage()
        {
            Console.WriteLine("Usage:");
            Console.WriteLine("  a2p h|help                    Show this help");
            Console.WriteLine("  a2p l|list                    List current PATH entries");
            Console.WriteLine("  a2p a|add u|user <path>       Add <path> to user PATH");
            Console.WriteLine("  a2p a|add s|system <path>     Add <path> to system PATH (requires admin)");
            Console.WriteLine("  a2p r|remove u|user <path>    Remove <path> from user PATH");
            Console.WriteLine("  a2p r|remove s|system <path>  Remove <path> from system PATH (requires admin)");
        }

        static void ShowPaths()
        {
            var userPaths = AddToPath.Program.GetUserPaths();
            var systemPaths = AddToPath.Program.GetSystemPaths();

            Console.WriteLine("User PATH:");
            foreach (var path in userPaths)
                Console.WriteLine($"  {path}");

            Console.WriteLine("\nSystem PATH:");
            foreach (var path in systemPaths)
                Console.WriteLine($"  {path}");
        }
    }
}
