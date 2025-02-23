# Contributing to AddToPath

## Development Setup

### Prerequisites
- Visual Studio 2022 or Visual Studio Code
- .NET SDK 8.0 or later (for building)
- .NET Framework 4.7.2 Developer Pack (for debugging)

### Framework Choice
- Project targets .NET Framework 4.7.2 specifically because:
  - It's included by default in Windows 10 (since version 1803, April 2018)
  - Users don't need to install any additional runtimes
  - Provides maximum compatibility with Windows 10 and newer systems
  - All required dependencies are available in the base framework

### Building the Project
```powershell
dotnet build -c Release
```

## Project Structure

### Solution Layout
```
AddToPath/
├── src/                    # Source code directory
│   ├── AddToPath/         # GUI application project
│   └── a2p/               # CLI tool project
├── bin/                   # Shared build output
│   ├── Debug/            # Debug builds
│   │   ├── AddToPath.exe # GUI executable
│   │   └── a2p.exe      # CLI executable
│   └── Release/          # Release builds
└── ...
```

### Build Process
- Each project builds to its own output directory (`src/*/bin/Debug/net472/`)
- Post-build events copy executables to the shared `bin` directory
- Only executables are copied (no dependencies needed - they're embedded)
- This setup serves multiple purposes:
  1. During development:
     - Makes it easy to find and run the latest builds
     - Ensures the GUI installer can find the CLI tool for installation
     - Simulates the installed state where both tools are in the same directory
  2. For releases:
     - Provides a clean directory with just the executables
     - Simplifies the release process by having all artifacts in one place
     - No dependencies to manage - everything is embedded

### Dependencies
- **[Costura.Fody](https://github.com/Fody/Costura)**: Embeds all DLLs into the executable at build time
  - Makes both tools completely self-contained
  - No external dependencies needed - everything is in the exe
  - Currently embeds:
    - `System.Resources.Extensions.dll` (needed for resource handling in SDK-style projects)
    - All referenced .NET Framework assemblies are available on Windows 10+ by default
  - Note: While Costura.Fody is in maintenance mode, it remains the best option for .NET Framework projects
    targeting Windows. For .NET Core 3.0+ projects, consider using [single-file executables](https://docs.microsoft.com/en-us/dotnet/core/whats-new/dotnet-core-3-0)
    instead.

### Resource Management
- Application icon (`AddToPath.ico`) is embedded in the executable
- Icon configuration in `AddToPath.csproj`:
  ```xml
  <PropertyGroup>
    <ApplicationIcon>Images\AddToPath.ico</ApplicationIcon>
  </PropertyGroup>
  <ItemGroup>
    <None Remove="Images\**" />
    <EmbeddedResource Include="Images\AddToPath.ico" />
  </ItemGroup>
  ```

### Version Management
- Uses [GitVersion](https://github.com/GitTools/GitVersion) for semantic versioning
- Configuration in `GitVersion.yml`
- Version is automatically determined from Git history
- Tagged commits (e.g., `v1.0.0`) trigger GitHub release workflow

### Release Process
1. GitHub Actions workflow in `.github/workflows/release.yml`
2. Triggered by pushing a tag starting with 'v'
3. Creates a draft release containing:
   - Single executable with embedded resources
   - README.md
   - LICENSE

### Installation Process
- The GUI application (`AddToPath.exe`) handles installation for both tools
- During installation:
  1. Both executables are copied to `%ProgramFiles%\AddToPath\`
  2. The install directory is added to system PATH
  3. Context menu entries are created for the GUI tool
- The CLI tool (`a2p.exe`) becomes available from any terminal after installation

### Testing
- Test on a clean Windows 10+ machine to verify:
  - No missing dependencies
  - UAC elevation works correctly
  - Context menu integration functions
  - PATH modifications succeed
  - CLI tool is accessible from PATH

## Best Practices
1. Always build and test in Release configuration before committing
2. Verify both executables work standalone before pushing a release tag
3. Keep the distribution clean - all necessary code should be embedded in the exes
4. Test PATH modifications on both user and system level
5. Remember to run with admin rights when testing system PATH changes
6. Test both GUI and CLI functionality after making changes
