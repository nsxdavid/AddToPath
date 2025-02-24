# AddToPath

A Windows utility for managing your PATH environment variable through both a context menu and command line interface.

## Features

- Two ways to manage PATH:

  1. Context Menu (GUI)
     - Right-click any folder and use the "Path" menu
     - Add folders to system or user PATH
     - Remove folders from PATH
     - Check if folders are in PATH
     - View all PATH entries
     
  2. Command Line (CLI)
     - Use `a2p` command from any terminal
     - Manage PATH entries through simple commands
     - Run without arguments to see available commands
- No external dependencies - everything is embedded
- UAC elevation for admin operations

## Requirements

- Windows 10 or later
- .NET Framework 4.7.2 (pre-installed on Windows 10)

## Installation

1. Download the latest release
2. Run `AddToPath.exe` and chose "Install Tools"
   - Both tools will be installed to Program Files
   - Creates context menu entries
   - Adds installation directory to system PATH
3. You can now:
   - Right-click folders and use the "Path" menu
   - Use `a2p` commands in any terminal

## Unisntall

To completely remove both tools, you can either:
1. Run AddToPath.exe for GUI and use uninstall option

2. Run `AddToPath.exe --uninstall` as administrator

Either way:
- Removes context menu entries
- Removes both tools from Program Files
- Removes installation directory from PATH

## Update / Repair Install

Similar to installation, you can either:
1. Run `AddToPath.exe` and chose "Repair Install"
2. Run `AddToPath.exe --uninstall`

## Development

This project is built using:
- C# Windows Forms & Console Apps
- .NET Framework 4.7.2
- Visual Studio 2022 or later

See [CONTRIBUTING.md](CONTRIBUTING.md) for development setup and guidelines.

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.
