# Windows Special Folders Implementation Proposal

**Version:** 1.0
**Date:** 2026-01-03
**Purpose:** Provide access to Windows special folder paths from Smalltalk

---

## Overview

This document describes the implementation of Windows special folder path access for the Smalltalk-80 interpreter. This enables Smalltalk code to locate standard Windows directories like `%APPDATA%`, `%LOCALAPPDATA%`, `%USERPROFILE%`, etc.

**Primary Use Cases:**
1. Theme file storage (`%APPDATA%\Smalltalk-80\themes\`)
2. User configuration files
3. Cache directories
4. Log files

---

## Architecture

### Single C# Primitive Approach

```
┌─────────────────────────────────────────────────────────────────┐
│                     Smalltalk Layer                              │
│  ┌──────────────────────────────────────────────────────────┐   │
│  │  WindowsPath class                                        │   │
│  │    - specialFolder: aSymbol → path string                 │   │
│  │    - SpecialFolders (Dictionary) → cached paths           │   │
│  │    - appDataPath, localAppDataPath, etc. → WindowsPath    │   │
│  └──────────────────────────────────────────────────────────┘   │
│                              │                                   │
│                              │ primitiveGetSpecialFolder:        │
│                              ▼                                   │
└─────────────────────────────────────────────────────────────────┘
                               │
                               │ Primitive 160
                               ▼
┌─────────────────────────────────────────────────────────────────┐
│                         C# Layer                                 │
│  ┌──────────────────────────────────────────────────────────┐   │
│  │  GetSpecialFolder primitive                               │   │
│  │    - Maps Symbol → Environment.SpecialFolder              │   │
│  │    - Returns path string or nil                           │   │
│  └──────────────────────────────────────────────────────────┘   │
│                              │                                   │
│                              ▼                                   │
│                    Environment.GetFolderPath()                   │
└─────────────────────────────────────────────────────────────────┘
```

---

## Component 1: C# Primitive

### Primitive 160: GetSpecialFolder

**File:** `Smalltalk80.Interpreter/Core/VM/Primitives/FileInfoPrimitives.cs`

**Specification:**
```
Receiver: any object (typically WindowsPath class)
Argument: Symbol - folder identifier (#appData, #localAppData, etc.)
Returns: String path, or nil if unknown/unavailable
```

**Symbol Mapping:**

| Smalltalk Symbol   | C# SpecialFolder              | Example Path                              |
|--------------------|-------------------------------|-------------------------------------------|
| `#appData`         | `ApplicationData`             | `C:\Users\<user>\AppData\Roaming`         |
| `#localAppData`    | `LocalApplicationData`        | `C:\Users\<user>\AppData\Local`           |
| `#userProfile`     | `UserProfile`                 | `C:\Users\<user>`                         |
| `#personal`        | `Personal`                    | `C:\Users\<user>\Documents`               |
| `#desktop`         | `Desktop`                     | `C:\Users\<user>\Desktop`                 |
| `#programFiles`    | `ProgramFiles`                | `C:\Program Files`                        |
| `#programFilesX86` | `ProgramFilesX86`             | `C:\Program Files (x86)`                  |
| `#commonAppData`   | `CommonApplicationData`       | `C:\ProgramData`                          |
| `#system`          | `System`                      | `C:\Windows\System32`                     |
| `#windows`         | `Windows`                     | `C:\Windows`                              |
| `#startup`         | `Startup`                     | `<user>\Start Menu\Programs\Startup`      |
| `#fonts`           | `Fonts`                       | `C:\Windows\Fonts`                        |

**Implementation:**

```csharp
/// <summary>
/// Primitive 160: Get path for a Windows special folder.
/// Receiver: WindowsPath class or any object.
/// Argument: Symbol identifier for the special folder.
/// Returns: String path or nil if folder unknown or unavailable.
/// </summary>
public class GetSpecialFolder : IPrimitive
{
    private static readonly Dictionary<string, Environment.SpecialFolder> FolderMap = new()
    {
        { "appData", Environment.SpecialFolder.ApplicationData },
        { "localAppData", Environment.SpecialFolder.LocalApplicationData },
        { "userProfile", Environment.SpecialFolder.UserProfile },
        { "personal", Environment.SpecialFolder.Personal },
        { "desktop", Environment.SpecialFolder.Desktop },
        { "programFiles", Environment.SpecialFolder.ProgramFiles },
        { "programFilesX86", Environment.SpecialFolder.ProgramFilesX86 },
        { "commonAppData", Environment.SpecialFolder.CommonApplicationData },
        { "system", Environment.SpecialFolder.System },
        { "windows", Environment.SpecialFolder.Windows },
        { "startup", Environment.SpecialFolder.Startup },
        { "fonts", Environment.SpecialFolder.Fonts },
    };

    public PrimitiveResult Execute(PrimitiveExecutionContext context)
    {
        if (context.Arguments.Length != 1)
            return PrimitiveResult.Fail(context.Metadata!);

        var symbolOOP = context.Arguments[0];
        if (symbolOOP.IsSmallInteger)
            return PrimitiveResult.Fail(context.Metadata!);

        // Extract symbol name from Symbol object
        string? symbolName = ExtractSymbolName(context, symbolOOP);
        if (symbolName == null)
            return PrimitiveResult.Fail(context.Metadata!);

        // Look up in folder map
        if (!FolderMap.TryGetValue(symbolName, out var folder))
            return PrimitiveResult.Ok(context.HeapManager.GetNil());

        try
        {
            string path = Environment.GetFolderPath(folder);
            if (string.IsNullOrEmpty(path))
                return PrimitiveResult.Ok(context.HeapManager.GetNil());

            return PrimitiveResult.Ok(FilePrimitiveHelpers.CreateString(context, path));
        }
        catch
        {
            return PrimitiveResult.Ok(context.HeapManager.GetNil());
        }
    }

    private static string? ExtractSymbolName(PrimitiveExecutionContext context, SmalltalkOOP symbolOOP)
    {
        // Symbol is stored as a String-like object (bytes are character codes)
        var heap = context.HeapManager;
        int length = heap.GetIndexedSize(symbolOOP);
        if (length <= 0) return null;

        var chars = new char[length];
        for (int i = 0; i < length; i++)
        {
            var byteOOP = heap.GetIndexedOOP(symbolOOP, i);
            if (!byteOOP.IsSmallInteger) return null;
            chars[i] = (char)byteOOP.AsSmallInteger();
        }
        return new string(chars);
    }
}
```

**Registration in PrimitiveRegistry.cs:**

```csharp
// In CreateWithNewPrimitives() method:

// Special folder primitives (160-169)
registry.Register(160, new FileInfoPrimitives.GetSpecialFolder());
```

---

## Component 2: WindowsPath Smalltalk Extension

### Class Modification

Add `SpecialFolders` class variable:

```smalltalk
Path subclass: #WindowsPath
    instanceVariableNames: 'driveLetter uncServer uncShare'
    classVariableNames: 'Separator SpecialFolders'
    poolDictionaries: ''
    category: 'Files-Windows'
```

### Class-Side Methods

```smalltalk
!WindowsPath class methodsFor: 'class initialization'!

initialize
    "Initialize the path separator. SpecialFolders uses lazy init."
    Separator := $\.
    SpecialFolders := nil.
    ^self!

initializeSpecialFolders
    "Populate SpecialFolders dictionary from primitive.
     Called once on first access. Keys are symbols, values are path strings."
    | keys |
    SpecialFolders := Dictionary new.
    keys := #(appData localAppData userProfile personal desktop
              programFiles programFilesX86 commonAppData system
              windows startup fonts).
    keys do: [:key |
        | path |
        path := self primitiveGetSpecialFolder: key.
        path ifNotNil: [SpecialFolders at: key put: path]].
    ^self! !

!WindowsPath class methodsFor: 'special folders - accessing'!

specialFolder: aSymbol
    "Answer the path string for aSymbol, or nil if unknown.
     Lazily initializes SpecialFolders on first call.

     Example: WindowsPath specialFolder: #appData
              => 'C:\Users\johndoe\AppData\Roaming'"
    SpecialFolders ifNil: [self initializeSpecialFolders].
    ^SpecialFolders at: aSymbol ifAbsent: [nil]!

specialFolderNames
    "Answer a collection of all known special folder symbols."
    ^#(appData localAppData userProfile personal desktop
       programFiles programFilesX86 commonAppData system
       windows startup fonts)!

hasSpecialFolder: aSymbol
    "Answer whether the special folder aSymbol is available on this system."
    ^(self specialFolder: aSymbol) notNil! !

!WindowsPath class methodsFor: 'special folders - convenience'!

appDataPath
    "Answer a WindowsPath for the user's roaming AppData folder.
     This folder roams with the user profile in domain environments."
    | path |
    path := self specialFolder: #appData.
    path ifNil: [self error: 'AppData folder not available'].
    ^self fromString: path!

localAppDataPath
    "Answer a WindowsPath for the user's local AppData folder.
     This folder does NOT roam - use for machine-specific data."
    | path |
    path := self specialFolder: #localAppData.
    path ifNil: [self error: 'LocalAppData folder not available'].
    ^self fromString: path!

userProfilePath
    "Answer a WindowsPath for the user's profile folder."
    | path |
    path := self specialFolder: #userProfile.
    path ifNil: [self error: 'UserProfile folder not available'].
    ^self fromString: path!

documentsPath
    "Answer a WindowsPath for the user's Documents folder."
    | path |
    path := self specialFolder: #personal.
    path ifNil: [self error: 'Documents folder not available'].
    ^self fromString: path!

desktopPath
    "Answer a WindowsPath for the user's Desktop folder."
    | path |
    path := self specialFolder: #desktop.
    path ifNil: [self error: 'Desktop folder not available'].
    ^self fromString: path!

commonAppDataPath
    "Answer a WindowsPath for the shared application data folder.
     Use for data shared between all users on this machine."
    | path |
    path := self specialFolder: #commonAppData.
    path ifNil: [self error: 'CommonAppData folder not available'].
    ^self fromString: path! !

!WindowsPath class methodsFor: 'special folders - application'!

applicationPath: appName
    "Answer a WindowsPath for an application's data folder under AppData.
     Creates the path object but does NOT create the directory.

     Example: WindowsPath applicationPath: 'Smalltalk-80'
              => WindowsPath for 'C:\Users\johndoe\AppData\Roaming\Smalltalk-80'"
    ^self appDataPath / appName!

localApplicationPath: appName
    "Answer a WindowsPath for an application's local data folder.
     Use for machine-specific data that should not roam."
    ^self localAppDataPath / appName!

sharedApplicationPath: appName
    "Answer a WindowsPath for an application's shared data folder.
     Use for data shared between all users."
    ^self commonAppDataPath / appName! !

!WindowsPath class methodsFor: 'special folders - primitive'!

primitiveGetSpecialFolder: aSymbol
    "Primitive 160: Get path for a Windows special folder.
     Answer the path string for aSymbol, or nil if unknown."
    <primitive: 160>
    ^nil  "Fallback if primitive fails"! !
```

---

## Usage Examples

### Basic Usage

```smalltalk
"Get raw path string"
WindowsPath specialFolder: #appData
    => 'C:\Users\johndoe\AppData\Roaming'

"Get as WindowsPath object"
WindowsPath appDataPath
    => a WindowsPath for 'C:\Users\johndoe\AppData\Roaming'

"Check folder availability"
WindowsPath hasSpecialFolder: #appData
    => true
```

### Application-Specific Paths

```smalltalk
"Get application data folder"
WindowsPath applicationPath: 'Smalltalk-80'
    => WindowsPath for 'C:\Users\johndoe\AppData\Roaming\Smalltalk-80'

"Build a path to a specific file"
(WindowsPath applicationPath: 'Smalltalk-80') / 'themes' / 'dark.json'
    => WindowsPath for 'C:\Users\johndoe\AppData\Roaming\Smalltalk-80\themes\dark.json'

"Create directory if needed"
| appDir |
appDir := WindowsPath applicationPath: 'Smalltalk-80'.
(WindowsDirectory on: appDir asString)
    exists ifFalse: [self create].
```

### Choosing the Right Folder

| Use Case | Folder | Method |
|----------|--------|--------|
| User preferences that should roam | `#appData` | `appDataPath` |
| Cache, temp files, machine-specific | `#localAppData` | `localAppDataPath` |
| Shared between all users | `#commonAppData` | `commonAppDataPath` |
| User documents | `#personal` | `documentsPath` |

---

## Error Handling

### Primitive Level
- **Invalid argument type**: Primitive fails, fallback returns nil
- **Unknown symbol**: Returns nil (not an error)
- **Folder not available**: Returns nil (some folders may not exist on all Windows versions)
- **Exception**: Caught, returns nil

### Smalltalk Level
- **Missing folder via `specialFolder:`**: Returns nil
- **Missing folder via convenience methods**: Raises descriptive error

---

## Testing Strategy

### C# Unit Tests

```csharp
[TestFixture]
public class GetSpecialFolderTests
{
    [Test]
    public void AppData_ReturnsNonEmptyPath()
    {
        var result = ExecutePrimitive160(CreateSymbol("appData"));
        Assert.That(result, Is.Not.Null);
        Assert.That(ExtractString(result), Does.Contain("AppData"));
    }

    [Test]
    public void LocalAppData_ReturnsNonEmptyPath()
    {
        var result = ExecutePrimitive160(CreateSymbol("localAppData"));
        Assert.That(result, Is.Not.Null);
        Assert.That(ExtractString(result), Does.Contain("AppData"));
        Assert.That(ExtractString(result), Does.Contain("Local"));
    }

    [Test]
    public void UnknownFolder_ReturnsNil()
    {
        var result = ExecutePrimitive160(CreateSymbol("unknownFolder"));
        Assert.That(IsNil(result), Is.True);
    }

    [Test]
    public void InvalidArgument_PrimitiveFails()
    {
        // Passing SmallInteger instead of Symbol
        var result = ExecutePrimitive160WithSmallInteger(42);
        Assert.That(result.Success, Is.False);
    }
}
```

### Smalltalk Integration Tests

```smalltalk
"Test primitive access"
(WindowsPath primitiveGetSpecialFolder: #appData) notNil
    ifFalse: [self error: 'primitive failed for appData'].

(WindowsPath primitiveGetSpecialFolder: #unknownXYZ) isNil
    ifFalse: [self error: 'primitive should return nil for unknown'].

"Test lazy initialization"
| path1 path2 |
path1 := WindowsPath specialFolder: #appData.
path2 := WindowsPath specialFolder: #appData.
path1 = path2 ifFalse: [self error: 'paths should be equal'].

"Test path construction"
| appPath |
appPath := WindowsPath applicationPath: 'TestApp'.
(appPath asString includesSubstring: 'AppData')
    ifFalse: [self error: 'should include AppData'].
(appPath asString includesSubstring: 'TestApp')
    ifFalse: [self error: 'should include TestApp'].

"Test all known folders return non-nil"
WindowsPath specialFolderNames do: [:sym |
    (WindowsPath specialFolder: sym) ifNil: [
        Transcript show: 'Warning: ', sym, ' not available'; cr]].
```

---

## Implementation Checklist

- [ ] **Phase 1: C# Primitive**
  - [ ] Add `GetSpecialFolder` class to `FileInfoPrimitives.cs`
  - [ ] Add symbol name extraction helper
  - [ ] Register primitive 160 in `PrimitiveRegistry.cs`
  - [ ] Add C# unit tests

- [ ] **Phase 2: Smalltalk Extension**
  - [ ] Add `SpecialFolders` class variable to WindowsPath
  - [ ] Implement `primitiveGetSpecialFolder:` with primitive pragma
  - [ ] Implement `initializeSpecialFolders` for lazy init
  - [ ] Implement `specialFolder:` accessor
  - [ ] Implement convenience methods (`appDataPath`, etc.)
  - [ ] Implement `applicationPath:` for app-specific paths
  - [ ] Add Smalltalk integration tests

- [ ] **Phase 3: Integration**
  - [ ] Optionally add `WindowsPath initializeSpecialFolders` to init expressions
  - [ ] Update documentation

---

## Security Considerations

1. **Read-Only Operation**: Primitive only reads system paths, never modifies
2. **No User Input in Paths**: All paths come from Windows API, not user strings
3. **No Privilege Escalation**: Uses standard .NET APIs available to any process
4. **Sandboxed to Known Folders**: Only exposes specific, safe folder paths

---

## Future Extensions

1. **Cross-Platform Support**: Add Linux XDG and macOS Library paths
2. **Environment Variable Expansion**: `WindowsPath expandEnvironment: '%TEMP%'`
3. **Known Folder GUIDs**: Access newer Windows 10/11 folders via GUID
4. **Virtual Folders**: Support for virtual folder shell items

---

## References

- [Environment.SpecialFolder Enum (Microsoft Docs)](https://docs.microsoft.com/en-us/dotnet/api/system.environment.specialfolder)
- [Environment.GetFolderPath Method](https://docs.microsoft.com/en-us/dotnet/api/system.environment.getfolderpath)
- [Known Folders (Windows Shell)](https://docs.microsoft.com/en-us/windows/win32/shell/known-folders)
- [XDG Base Directory Specification](https://specifications.freedesktop.org/basedir-spec/basedir-spec-latest.html)
