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

### Dependencies
- **[Costura.Fody](https://github.com/Fody/Costura)**: Embeds all DLLs into the executable at build time
  - Configured in `FodyWeavers.xml` ([configuration docs](https://github.com/Fody/Costura#configuration-options))
  - Eliminates need to distribute separate DLLs
  - Makes the app portable (single-file executable)
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

### Testing
- Test on a clean Windows 10+ machine to verify:
  - No missing dependencies
  - UAC elevation works correctly
  - Context menu integration functions
  - PATH modifications succeed

## Best Practices
1. Always build and test in Release configuration before committing
2. Verify the executable works standalone before pushing a release tag
3. Keep the distribution clean - all necessary code should be embedded in the exe
4. Test PATH modifications on both user and system level
5. Remember to run with admin rights when testing system PATH changes
