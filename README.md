# Add to PATH Context Menu Utility

A simple Windows utility that adds a "Add to PATH" option to the context menu when right-clicking folders. This allows you to quickly add any folder to your system's PATH environment variable.

## Features

- Adds "Add to PATH" to folder context menu
- One-click addition of folders to system PATH
- Native Windows integration
- No external dependencies required
- UAC elevation for admin rights
- Visual feedback for operations

## Requirements

- Windows 10 or later
- .NET Framework 4.7.2 (pre-installed on Windows 10)

## Installation

1. Download the latest release
2. Run `AddToPath.exe` as administrator
   - The application will install itself to Program Files
   - Creates the "Add to System PATH" context menu entry
3. You can now right-click any folder and select "Add to PATH"

## Uninstallation

To completely remove the application:
1. Run `AddToPath.exe --uninstall` as administrator
   - Removes the context menu entry
   - Removes the application from Program Files
   - Does not remove any folders you've previously added to PATH

## Development

This project is built using:
- C# Windows Forms
- .NET Framework 4.7.2
- Visual Studio 2022 or later
