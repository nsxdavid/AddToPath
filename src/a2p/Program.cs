// Copyright (c) 2025 David Whatley
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.IO;
using System.Linq;

namespace a2p
{
    class Program
    {
        static int Main(string[] args)
        {
            try
            {
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

                if (isSystem && !AddToPath.Program.IsRunningAsAdmin())
                {
                    Console.WriteLine("Administrator rights are required to modify the system PATH.");
                    return 1;
                }

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
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
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
