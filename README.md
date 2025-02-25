# AddToPath

<p align="center">
  <img src="https://raw.githubusercontent.com/nsxdavid/AddToPath/main/src/AddToPath/Images/AddToPath.png" alt="AddToPath Logo" width="128" height="128">
</p>

<p align="center">
  <a href="https://github.com/nsxdavid/AddToPath/releases/latest"><img src="https://img.shields.io/github/v/release/nsxdavid/AddToPath?cache=1" alt="GitHub release"></a>
  <a href="LICENSE"><img src="https://img.shields.io/github/license/nsxdavid/AddToPath" alt="License"></a>
  <a href="https://github.com/nsxdavid/AddToPath"><img src="https://img.shields.io/badge/platform-Windows-blue" alt="Windows"></a>
</p>

A Windows utility for managing your PATH environment variable through both a context menu and command line interface.

<p align="center">
  <img src="https://raw.githubusercontent.com/nsxdavid/AddToPath/main/src/AddToPath/Images/Screenshot1.png" alt="AddToPath Context Menu" width="600">
  <br>
  <em>AddToPath context menu integration in Windows Explorer</em>
</p>

## Table of Contents
- [Requirements](#requirements)
- [Quick Start](#quick-start)
- [Features](#features)
- [Installation](#installation)
- [CLI Usage](#cli-usage)
- [Uninstall](#uninstall)
- [Troubleshooting](#troubleshooting)
- [Development](#development)
- [License](#license)

## Requirements

- Windows 10 or later
- .NET Framework 4.7.2 (pre-installed on Windows 10)
- Administrator rights for system PATH modifications

## Quick Start

1. [Download latest release](https://github.com/nsxdavid/AddToPath/releases/latest)
2. Run `AddToPath.exe` as administrator
3. Click "Install Tools"
4. Right-click any folder → Path → Add to PATH

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
     - Simple commands for PATH management:
       ```powershell
       # Add current directory to user PATH
       a2p add .
       
       # Add a directory to system PATH (needs admin)
       a2p add -s "C:\Tools"
       
       # Remove from user PATH
       a2p remove "C:\Tools"
       
       # Check if directory is in PATH
       a2p check "C:\Tools"
       ```

- No external dependencies - everything is embedded
- UAC elevation for admin operations
- Works with both user and system PATH
- Changes take effect immediately in new terminals
- Includes scripts to refresh PATH in existing terminals

## Installation

1. Download the [latest release](https://github.com/nsxdavid/AddToPath/releases/latest)
2. Run `AddToPath.exe` as administrator and choose "Install Tools"
   - Both tools will be installed to Program Files
   - Creates context menu entries
   - Adds installation directory to system PATH
   - Creates `updatepath` command for refreshing PATH in current terminals
3. You can now:
   - Right-click folders and use the "Path" menu
   - Use `a2p` commands in any terminal
   - Use `updatepath` to refresh PATH in current terminal

## CLI Usage

The `a2p` command supports the following operations:

```powershell
a2p add <path>      # Add to user PATH
a2p add -s <path>   # Add to system PATH (needs admin)
a2p remove <path>   # Remove from user PATH
a2p remove -s <path># Remove from system PATH (needs admin)
a2p check <path>    # Check if path is in PATH
a2p list           # List all PATH entries
```

After modifying PATH, you can either:
- Open new terminals to see the changes, or
- Refresh PATH in current terminal:
  ```
  updatepath
  ```

## Uninstall

To completely remove both tools, you can either:
1. Run `AddToPath.exe` and choose "Uninstall"
2. Run `AddToPath.exe --uninstall` as administrator

Either way:
- Removes context menu entries
- Removes both tools from Program Files
- Removes installation directory from PATH

## Troubleshooting

The tool automatically detects common issues and will show a "Reinstall Tools" button if it finds any problems. This can fix:
- Missing context menu entries
- Missing PATH entries
- Incorrect installation directory
- Missing or outdated components

For specific issues:

1. **"Access denied" when modifying system PATH**
   - Run the tool as administrator
   - For CLI, use an elevated command prompt

2. **Changes not visible in current terminal**
   - PATH changes only affect new terminals
   - Close and reopen your terminal
   - Use `updatepath` to refresh PATH

3. **Any other issues**
   - Run `AddToPath.exe` - it will detect problems and offer to fix them
   - Click "Reinstall Tools" if offered
   - The tool will repair all components and restore functionality

## Development

This project is built using:
- C# Windows Forms & Console Apps
- .NET Framework 4.7.2
- Visual Studio 2022 or later

See [CONTRIBUTING.md](CONTRIBUTING.md) for development setup and guidelines.

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.
