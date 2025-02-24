# Contributing to AddToPath

## Code of Conduct

### Our Pledge
We are committed to making participation in the AddToPath project a harassment-free experience for everyone, regardless of experience level, age, disability, ethnicity, gender identity and expression, nationality, personal appearance, race, religion, or sexual orientation.

### Our Standards
Examples of behavior that contributes to creating a positive environment include:
- Using welcoming and inclusive language
- Being respectful of differing viewpoints and experiences
- Gracefully accepting constructive criticism
- Focusing on what is best for the community
- Showing empathy towards other community members

Examples of unacceptable behavior include:
- The use of sexualized language or imagery
- Trolling, insulting/derogatory comments, and personal or political attacks
- Public or private harassment
- Publishing others' private information without explicit permission
- Other conduct which could reasonably be considered inappropriate

### Enforcement
Project maintainers are responsible for clarifying and enforcing these standards. They have the right and responsibility to remove, edit, or reject comments, commits, code, issues, and other contributions that are not aligned with this Code of Conduct.

### Reporting
Instances of abusive, harassing, or otherwise unacceptable behavior may be reported by opening an issue with the label "Code of Conduct". All complaints will be reviewed and investigated promptly and fairly.

## Contribution Process

### Issues
1. **Search First**: Before creating a new issue, search existing issues to avoid duplicates
2. **Issue Types**:
   - Bug Report: Describe the bug, steps to reproduce, expected vs actual behavior
   - Feature Request: Describe the feature, its use case, and potential implementation
   - Question: For general questions about usage or development

### Pull Requests
1. **Fork & Branch**:
   - Fork the repository
   - Create a branch with a descriptive name:
     - `feature/description` for new features
     - `fix/description` for bug fixes
     - `docs/description` for documentation changes
     - `refactor/description` for code refactoring

2. **Commit Guidelines**:
   - Write clear, descriptive commit messages
   - Optional: Use conventional commits format for structured changes:
     ```
     type: description
     ```
   - Common types when used: `feat`, `fix`, `docs`, `refactor`
   - Keep commits focused on single changes
   - Reference issues in commit messages when applicable: "fixes #123"

3. **Development**:
   - Write clear, commented, and testable code
   - Follow existing code style and patterns
   - Update documentation if needed
   - Test your changes thoroughly

4. **Pull Request Process**:
   - Create a PR against the `main` branch
   - Use the PR template if provided
   - Include:
     - Clear description of changes
     - Screenshots for UI changes
     - Steps to test the changes
   - Keep PRs focused - one feature/fix per PR
   - Respond to review comments promptly

5. **Code Review**:
   - All PRs require review before merging
   - Address review feedback in new commits
   - Maintainers may request changes or provide suggestions
   - Once approved, maintainers will merge the PR

### Communication
- Keep discussions focused and professional
- Provide context and examples when asking questions
- Tag relevant maintainers when needed
- Be patient - maintainers will respond as time permits

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
├── src/                   # Source code directory
│   ├── AddToPath/         # GUI application project
│   └── a2p/               # CLI tool project
├── bin/                   # Shared build output
│   ├── Debug/             # Debug builds
│   │   ├── AddToPath.exe  # GUI executable
│   │   └── a2p.exe        # CLI executable
│   └── Release/           # Release builds
└── ...
```

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

## Security Guidelines

### Reporting Security Issues
- **Do NOT report security vulnerabilities through public GitHub issues**
- Instead, report them privately through [GitHub's Security Advisory feature](https://github.com/nsxdavid/AddToPath/security/advisories/new)
- The maintainers will be notified and can privately discuss the issue with you
- Please provide detailed information about the vulnerability and steps to reproduce
- Once the issue is addressed, we'll coordinate the public disclosure

### Security Considerations When Contributing
1. **Elevated Permissions**:
   - Any code that requires admin rights must be clearly marked
   - Minimize the scope of elevated operations
   - Always verify user intent before executing privileged operations

2. **File System Operations**:
   - Validate all file paths before modification
   - Use secure file operation practices
   - Be cautious with file permissions

3. **Registry Operations**:
   - Validate registry paths before modification
   - Only modify necessary registry keys
   - Handle registry access errors gracefully

4. **Input Validation**:
   - Validate all user input
   - Sanitize file paths and registry keys
   - Handle invalid input gracefully

### Best Practices for Security
- Follow the principle of least privilege
- Add appropriate error handling for security-sensitive operations
- Document any security-relevant changes in pull requests
- If in doubt about security implications, ask in the PR discussion

## Best Practices
1. Always build and test in Release configuration before committing
2. Verify both executables work standalone before pushing a release tag
3. Keep the distribution clean - all necessary code should be embedded in the exes
4. Test PATH modifications on both user and system level
5. Remember to run with admin rights when testing system PATH changes
6. Test both GUI and CLI functionality after making changes
7. Context menu organization:
   - Menu items are ordered by registry key names (alphabetical sorting)
   - Use numbered prefixes (e.g., "1_AddToPath") to control menu order
   - Keep display names user-friendly using MUIVerb values

### Build Output Directory
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
