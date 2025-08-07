# Bootstrap Files Build Configuration

## Overview

The Smalltalk-80 build system is configured to automatically copy bootstrap JSON files to a `Bootstrap` subfolder relative to the executable. This ensures the VM can always find its bootstrap configuration at runtime.

## Build Process

### 1. **File Organization**

```
Solution Root/
├── Bootstrap/
│   └── Smalltalk80.Bootstrap.Data/
│       ├── bootstrap_phase1.json   # Phase 1 VM primitives
│       ├── bootstrap_phase2.json   # Phase 2 messaging
│       └── bootstrap_phase3.json   # Phase 3 file loading
│
└── CLI/
    └── Smalltalk80.CLI/
        └── bin/
            └── Debug/net8.0/
                ├── Smalltalk80.exe
                └── Bootstrap/           # Auto-created on build
                    ├── bootstrap_phase1.json
                    ├── bootstrap_phase2.json
                    └── bootstrap_phase3.json
```

### 2. **MSBuild Configuration**

The build system uses MSBuild targets to handle bootstrap files:

#### **Smalltalk80.Bootstrap.Data.csproj**
- Marks JSON files as `Content` with `CopyToOutputDirectory`
- Defines custom targets for copying to `Bootstrap` subfolder
- Handles cleanup on `Clean`

#### **Smalltalk80.CLI.csproj**
- References Bootstrap.Data project (without referencing assembly)
- Copies bootstrap files after build
- Handles both Debug/Release and Publish scenarios
- Cleans up Bootstrap folder on clean

#### **Directory.Build.props**
- Defines common properties for all projects
- Sets up paths and configurations
- Enables C# 13 features

## Build Targets

### **CopyBootstrapFiles** (After Build)
```xml
<Target Name="CopyBootstrapFiles" AfterTargets="Build">
  <!-- Creates Bootstrap subfolder -->
  <!-- Copies all bootstrap_phase*.json files -->
  <!-- Shows confirmation message -->
</Target>
```

### **CleanBootstrapFiles** (Before Clean)
```xml
<Target Name="CleanBootstrapFiles" BeforeTargets="Clean">
  <!-- Removes Bootstrap subfolder -->
  <!-- Shows cleanup message -->
</Target>
```

### **CopyBootstrapFilesForPublish** (After Publish)
```xml
<Target Name="CopyBootstrapFilesForPublish" AfterTargets="Publish">
  <!-- Copies files to publish directory -->
  <!-- Maintains Bootstrap subfolder structure -->
</Target>
```

## Runtime Behavior

### Default Bootstrap Location

The executable looks for bootstrap files in this order:

1. **Command-line specified**: `--bootstrap-dir <path>`
2. **Executable directory**: `<exe-dir>/Bootstrap/`
3. **Development paths**: Various relative paths for debugging
4. **Current directory**: `./Bootstrap/`

### Program.cs Implementation

```csharp
private static string GetBootstrapDirectory(string? bootstrapDir)
{
    if (!string.IsNullOrEmpty(bootstrapDir))
        return bootstrapDir;
        
    // Default to Bootstrap subfolder relative to executable
    var exeDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
    var defaultDir = Path.Combine(exeDir, "Bootstrap");
    
    // Check various paths...
    return defaultDir;
}
```

## Usage Examples

### Build and Run

```bash
# Build the solution
dotnet build

# Run with default bootstrap location
./bin/Debug/net8.0/Smalltalk80.exe

# Run with custom bootstrap location
./bin/Debug/net8.0/Smalltalk80.exe --bootstrap-dir ./custom/bootstrap

# Validate bootstrap files
./bin/Debug/net8.0/Smalltalk80.exe validate
```

### Clean Build

```bash
# Clean removes Bootstrap folder and all generated files
dotnet clean

# Verify Bootstrap folder is removed
ls ./bin/Debug/net8.0/  # Bootstrap folder should not exist
```

### Publish for Distribution

```bash
# Publish creates self-contained deployment
dotnet publish -c Release

# Bootstrap files are included in publish directory
ls ./bin/Release/net8.0/publish/Bootstrap/
```

## Modifying Bootstrap Files

### During Development

Edit files directly in `Bootstrap/Smalltalk80.Bootstrap.Data/`:
- Changes are copied on next build
- No need to modify build output

### For Testing

1. Build the project: `dotnet build`
2. Edit files in `bin/Debug/net8.0/Bootstrap/`
3. Run to test changes immediately
4. Copy working changes back to source

### For Distribution

Bootstrap files are included automatically when publishing:
```bash
dotnet publish -c Release -r win-x64 --self-contained
```

## Troubleshooting

### Bootstrap Files Not Found

If the VM reports "Bootstrap directory not found":

1. Check build output for copy confirmation:
   ```
   Copied 3 bootstrap files to: bin\Debug\net8.0\Bootstrap
   ```

2. Verify files exist:
   ```bash
   ls bin/Debug/net8.0/Bootstrap/
   ```

3. Run with explicit path:
   ```bash
   Smalltalk80.exe --bootstrap-dir ./Bootstrap
   ```

### Files Not Updating

If changes to JSON files aren't reflected:

1. Clean and rebuild:
   ```bash
   dotnet clean
   dotnet build
   ```

2. Check for build errors in output

3. Verify source files are saved

### Build Warnings

The build system will show warnings if:
- Bootstrap files are missing
- Copy operations fail
- Directory creation fails

## Benefits

1. **Automatic Deployment**: Bootstrap files always deployed with executable
2. **Clean Separation**: JSON files separate from compiled code
3. **Easy Updates**: Modify JSON without recompiling
4. **Consistent Structure**: Same layout in Debug, Release, and Publish
5. **Development Friendly**: Works in IDE and command line
6. **Cross-Platform**: Works on Windows, Linux, macOS