# Well-Known Paths and Theme File Design

**Version:** 1.3
**Date:** 2026-01-03
**Purpose:** Enable dynamic theme file loading from standard Windows application data locations

**Constraint:** Theme files must use the exact VS Code theme JSON format, allowing users to copy VS Code theme files directly into the themes folder.

---

## Overview

This design extends the interpreter to support well-known Windows file system paths, enabling the interpreter to read configuration files (such as syntax highlighting themes) from standard application data locations.

**Goals:**
1. Expose Windows special folder paths to Smalltalk via `Environment.SpecialFolder` API
2. Extend `WindowsPath` class with mapping from symbolic identifiers to paths
3. Establish a standard location for interpreter configuration files
4. Enable dynamic loading of syntax highlighting themes

---

## Architecture

### Three-Layer Design

```
┌─────────────────────────────────────────────────────────────────┐
│                     Smalltalk Application Layer                  │
│  ┌─────────────────┐  ┌─────────────────┐  ┌─────────────────┐ │
│  │  SyntaxStyler   │  │   Future Config │  │   Future...     │ │
│  │  (theme files)  │  │   (settings)    │  │                 │ │
│  └────────┬────────┘  └────────┬────────┘  └────────┬────────┘ │
│           │                    │                    │           │
│           ▼                    ▼                    ▼           │
│  ┌──────────────────────────────────────────────────────────┐  │
│  │                    WindowsPath Class                      │  │
│  │  ┌────────────────────────────────────────────────────┐  │  │
│  │  │ SpecialFolders (Dictionary)                        │  │  │
│  │  │   #appData → 'C:\Users\...\AppData\Roaming'       │  │  │
│  │  │   #localAppData → 'C:\Users\...\AppData\Local'    │  │  │
│  │  │   #userProfile → 'C:\Users\...'                   │  │  │
│  │  │   ...                                              │  │  │
│  │  └────────────────────────────────────────────────────┘  │  │
│  │                                                           │  │
│  │  API Methods:                                             │  │
│  │    specialFolder: #appData      → path string             │  │
│  │    appDataPath                  → WindowsPath             │  │
│  │    applicationPath: 'AppName'   → WindowsPath             │  │
│  └──────────────────────────────────────────────────────────┘  │
└─────────────────────────────────────────────────────────────────┘
                              │
                              │ Primitive 160
                              ▼
┌─────────────────────────────────────────────────────────────────┐
│                        C# Primitive Layer                        │
│  ┌──────────────────────────────────────────────────────────┐  │
│  │  GetSpecialFolder (Primitive 160)                         │  │
│  │                                                           │  │
│  │  Input: Symbol (#appData, #localAppData, etc.)           │  │
│  │  Output: String path                                      │  │
│  │                                                           │  │
│  │  Implementation:                                          │  │
│  │    Environment.GetFolderPath(SpecialFolder.XXX)          │  │
│  └──────────────────────────────────────────────────────────┘  │
└─────────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────────┐
│                     Windows Operating System                     │
│  ┌──────────────────────────────────────────────────────────┐  │
│  │  Environment.SpecialFolder Enum                           │  │
│  │    ApplicationData      → %APPDATA%                       │  │
│  │    LocalApplicationData → %LOCALAPPDATA%                  │  │
│  │    UserProfile          → %USERPROFILE%                   │  │
│  │    Personal             → Documents                       │  │
│  │    Desktop              → Desktop                         │  │
│  │    CommonApplicationData → %PROGRAMDATA%                  │  │
│  │    ProgramFiles         → %PROGRAMFILES%                  │  │
│  │    System               → %SYSTEMROOT%\System32           │  │
│  │    Windows              → %SYSTEMROOT%                    │  │
│  └──────────────────────────────────────────────────────────┘  │
└─────────────────────────────────────────────────────────────────┘
```

---

## Layer 1: C# Primitive Specification

### Primitive 160: GetSpecialFolder

**Location:** `Smalltalk80.Interpreter/Core/VM/Primitives/FileInfoPrimitives.cs`

**Signature:**
```
Receiver: WindowsPath class (or any object)
Argument: Symbol - folder identifier
Returns: String - absolute path, or nil if unknown/unavailable
```

**Symbol-to-SpecialFolder Mapping:**

| Smalltalk Symbol   | C# SpecialFolder              | Typical Path                              |
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
        // Symbol is stored as a String-like object
        // Extract the bytes and convert to string
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

**Registration:**

```csharp
// In PrimitiveRegistry.CreateWithNewPrimitives()
// Special folder primitives (160-169)
registry.Register(160, new FileInfoPrimitives.GetSpecialFolder());
```

**Error Handling:**
- Unknown symbol: Returns `nil`
- Empty path (folder not available on system): Returns `nil`
- Exception during path retrieval: Returns `nil`
- Primitive fails (wrong argument type): Falls through to Smalltalk fallback

---

## Layer 2: WindowsPath Class Extension

### Class Variable Addition

```smalltalk
Path subclass: #WindowsPath
    instanceVariableNames: 'driveLetter uncServer uncShare'
    classVariableNames: 'Separator SpecialFolders'
    poolDictionaries: ''
    category: 'Files-Windows'
```

**SpecialFolders:** A `Dictionary` mapping Symbol keys to String path values, lazily populated on first access.

### Class-Side Methods

```smalltalk
!WindowsPath class methodsFor: 'class initialization'!

initialize
    "Initialize the path separator and special folders."
    Separator := $\.
    SpecialFolders := nil.  "Lazy initialization"
    ^self!

initializeSpecialFolders
    "Populate SpecialFolders dictionary from primitive.
     Called once on first access. Keys are symbols, values are path strings.
     Returns self."
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
     This folder roams with the user profile in domain environments.

     Example: WindowsPath appDataPath
              => WindowsPath for 'C:\Users\johndoe\AppData\Roaming'"
    | path |
    path := self specialFolder: #appData.
    path ifNil: [self error: 'AppData folder not available'].
    ^self fromString: path!

localAppDataPath
    "Answer a WindowsPath for the user's local AppData folder.
     This folder does NOT roam - use for machine-specific data.

     Example: WindowsPath localAppDataPath
              => WindowsPath for 'C:\Users\johndoe\AppData\Local'"
    | path |
    path := self specialFolder: #localAppData.
    path ifNil: [self error: 'LocalAppData folder not available'].
    ^self fromString: path!

userProfilePath
    "Answer a WindowsPath for the user's profile folder.

     Example: WindowsPath userProfilePath
              => WindowsPath for 'C:\Users\johndoe'"
    | path |
    path := self specialFolder: #userProfile.
    path ifNil: [self error: 'UserProfile folder not available'].
    ^self fromString: path!

documentsPath
    "Answer a WindowsPath for the user's Documents folder.

     Example: WindowsPath documentsPath
              => WindowsPath for 'C:\Users\johndoe\Documents'"
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
     Use for data shared between all users on this machine.

     Example: WindowsPath commonAppDataPath
              => WindowsPath for 'C:\ProgramData'"
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
     Use for machine-specific data that should not roam.

     Example: WindowsPath localApplicationPath: 'Smalltalk-80'
              => WindowsPath for 'C:\Users\johndoe\AppData\Local\Smalltalk-80'"
    ^self localAppDataPath / appName!

sharedApplicationPath: appName
    "Answer a WindowsPath for an application's shared data folder.
     Use for data shared between all users.

     Example: WindowsPath sharedApplicationPath: 'Smalltalk-80'
              => WindowsPath for 'C:\ProgramData\Smalltalk-80'"
    ^self commonAppDataPath / appName! !

!WindowsPath class methodsFor: 'special folders - primitive'!

primitiveGetSpecialFolder: aSymbol
    "Primitive 160: Get path for a Windows special folder.
     Answer the path string for aSymbol, or nil if unknown.

     This is the low-level interface to Environment.SpecialFolder."
    <primitive: 160>
    ^nil  "Fallback if primitive fails or not implemented"! !
```

### Usage Examples

```smalltalk
"Get raw path string"
WindowsPath specialFolder: #appData
    => 'C:\Users\johndoe\AppData\Roaming'

"Get as WindowsPath object"
WindowsPath appDataPath
    => a WindowsPath for 'C:\Users\johndoe\AppData\Roaming'

"Get application-specific path"
WindowsPath applicationPath: 'Smalltalk-80'
    => a WindowsPath for 'C:\Users\johndoe\AppData\Roaming\Smalltalk-80'

"Build a path to a specific file"
(WindowsPath applicationPath: 'Smalltalk-80') / 'themes' / 'VSCodeLight.theme'
    => a WindowsPath for 'C:\Users\johndoe\AppData\Roaming\Smalltalk-80\themes\VSCodeLight.theme'

"Check folder availability"
WindowsPath hasSpecialFolder: #appData
    => true
```

---

## Layer 3: Theme File Infrastructure

### Directory Structure

```
%APPDATA%\Smalltalk-80\
├── themes\
│   ├── light_plus.json        (copied from VS Code)
│   ├── dark_plus.json         (copied from VS Code)
│   ├── monokai-color-theme.json
│   └── my-custom-theme.json
├── settings.st          (future: user preferences)
└── history.st           (future: command history)
```

**Rationale for `%APPDATA%` (Roaming):**
- Theme preferences should follow the user across machines in domain environments
- Small files, no performance concern for roaming
- Standard Windows convention for user preferences

### Theme File Format: VS Code JSON

Theme files use the **exact VS Code color theme JSON format**. Users can copy any VS Code theme file directly into the themes folder.

**VS Code Theme Structure:**

```json
{
  "$schema": "vscode://schemas/color-theme",
  "name": "Light+ (default light)",
  "type": "light",
  "colors": {
    "editor.background": "#FFFFFF",
    "editor.foreground": "#000000"
  },
  "tokenColors": [
    {
      "name": "Comment",
      "scope": "comment",
      "settings": {
        "foreground": "#008000"
      }
    },
    {
      "name": "String",
      "scope": ["string", "string.quoted"],
      "settings": {
        "foreground": "#A31515"
      }
    },
    {
      "name": "Keyword",
      "scope": "keyword.control",
      "settings": {
        "foreground": "#AF00DB",
        "fontStyle": "bold"
      }
    },
    {
      "name": "Function",
      "scope": [
        "entity.name.function",
        "support.function"
      ],
      "settings": {
        "foreground": "#795E26"
      }
    }
  ],
  "semanticHighlighting": true,
  "semanticTokenColors": {
    "newOperator": "#0000FF",
    "stringLiteral": "#A31515"
  }
}
```

**Key Format Elements:**

| Field | Type | Description |
|-------|------|-------------|
| `$schema` | string | Optional. VS Code schema URL (ignored by our parser) |
| `name` | string | Theme display name |
| `type` | string | `"light"` or `"dark"` - indicates background type |
| `include` | string | Optional. Path to base theme to inherit from |
| `colors` | object | Editor UI colors (we ignore these, only relevant for VS Code UI) |
| `tokenColors` | array | **Primary data** - syntax highlighting rules |
| `semanticHighlighting` | boolean | Optional. Enable semantic tokens (ignored) |
| `semanticTokenColors` | object | Optional. Semantic token colors (ignored) |

**tokenColors Array Entry:**

| Field | Type | Description |
|-------|------|-------------|
| `name` | string | Optional. Human-readable description |
| `scope` | string or array | TextMate scope selector(s) |
| `settings.foreground` | string | Hex color `#RRGGBB` or `#RRGGBBAA` |
| `settings.fontStyle` | string | Optional. `"bold"`, `"italic"`, `"underline"`, or combinations |

**Scope Matching:**

The `scope` field uses TextMate scope selectors. A scope is a dot-separated hierarchy:
- `comment` - matches all comments
- `comment.block` - matches block comments specifically
- `keyword.control` - matches control keywords
- `entity.name.function` - matches function names

Multiple scopes can be specified as an array. The most specific matching scope wins.

---

## JSON Parsing: Pure Smalltalk Implementation

JSON parsing is implemented in pure Smalltalk using `JsonReader` and `JsonWriter` classes.

**Full specification:** See [JSON-PROCESSING-IN-SMALLTALK.md](JSON-PROCESSING-IN-SMALLTALK.md)

**Key points:**
- `JsonReader parse: aString` - parses JSON to Smalltalk objects
- `JsonWriter stringify: anObject` - writes Smalltalk objects as JSON
- Leverages existing `Number readFrom:` for number parsing
- No C# primitive needed

**Quick Reference:**

```smalltalk
"Parsing"
JsonReader parse: '{"name": "Alice", "age": 30}'
    => a Dictionary('name'->'Alice' 'age'->30)

"Writing"
JsonWriter stringify: (Dictionary new at: 'key' put: 'value'; yourself)
    => '{"key":"value"}'
```

---

### SyntaxStyler Theme Loading

```smalltalk
!SyntaxStyler class methodsFor: 'theme configuration'!

applicationName
    "Answer the application name for path construction."
    ^'Smalltalk-80'!

themeDirectory
    "Answer the WindowsPath to the themes directory.
     Does not create the directory."
    ^(WindowsPath applicationPath: self applicationName) / 'themes'!

themeDirectoryString
    "Answer the full path string to the themes directory."
    ^self themeDirectory asString!

ensureThemeDirectory
    "Ensure the themes directory exists, creating it if necessary.
     Answer the directory path."
    | dir |
    dir := WindowsDirectory on: self themeDirectoryString.
    dir exists ifFalse: [dir create].
    ^self themeDirectory! !

!SyntaxStyler class methodsFor: 'theme loading'!

loadThemeNamed: themeName
    "Load a VS Code theme from the themes directory.
     themeName should be the filename without .json extension.
     Answer the color Dictionary, or nil if not found.

     Example: SyntaxStyler loadThemeNamed: 'light_plus'"
    | path file contents |
    path := self themeDirectory / (themeName, '.json').
    file := WindowsFile on: path asString.
    file exists ifFalse: [^nil].
    contents := file readStream contents.
    ^self parseVSCodeTheme: contents!

parseVSCodeTheme: jsonString
    "Parse a VS Code theme JSON string and answer a color Dictionary.
     Maps TextMate scopes to TextColor objects."
    | json tokenColors result |
    [json := JsonReader parse: jsonString]
        on: Error do: [:ex |
            Transcript show: 'Theme parse error: ', ex messageText; cr.
            ^nil].
    (json isKindOf: Dictionary) ifFalse: [
        Transcript show: 'Theme file must be a JSON object'; cr.
        ^nil].

    tokenColors := json at: 'tokenColors' ifAbsent: [#()].
    (tokenColors isKindOf: Array) ifFalse: [
        Transcript show: 'tokenColors must be an array'; cr.
        ^nil].

    result := Dictionary new.
    tokenColors do: [:rule |
        self processTokenColorRule: rule into: result].
    ^result!

processTokenColorRule: rule into: result
    "Process a single tokenColors entry from VS Code theme.
     rule is a Dictionary with 'scope' and 'settings' keys."
    | scopes settings foreground color |
    (rule isKindOf: Dictionary) ifFalse: [^self].

    settings := rule at: 'settings' ifAbsent: [^self].
    (settings isKindOf: Dictionary) ifFalse: [^self].

    foreground := settings at: 'foreground' ifAbsent: [^self].
    color := Color fromHexString: foreground.
    color ifNil: [^self].

    scopes := rule at: 'scope' ifAbsent: [^self].

    "scope can be a string or an array of strings"
    scopes isString
        ifTrue: [result at: scopes put: (TextColor color: color)]
        ifFalse: [
            (scopes isKindOf: Array) ifTrue: [
                scopes do: [:scope |
                    scope isString ifTrue: [
                        result at: scope put: (TextColor color: color)]]]].!

useThemeNamed: themeName
    "Load and activate a VS Code theme by name.
     Answer true if successful, false if theme not found.

     Example: SyntaxStyler useThemeNamed: 'light_plus'"
    | colors |
    colors := self loadThemeNamed: themeName.
    colors ifNil: [^false].
    self useColorSet: themeName colors: colors.
    ^true!

availableThemes
    "Answer a collection of available theme names (without extension).
     Scans the themes directory for .json files."
    | dir files |
    dir := WindowsDirectory on: self themeDirectoryString.
    dir exists ifFalse: [^#()].
    files := dir filesMatching: '*.json'.
    ^files collect: [:f | f baseName]! !

!Color class methodsFor: 'instance creation'!

fromHexString: hexString
    "Create a Color from a hex string like '#RRGGBB' or '#RRGGBBAA'.
     Returns nil if the format is invalid.

     Example: Color fromHexString: '#FF5733'
              Color fromHexString: '#F00'  (short form)"
    | hex r g b |
    hex := hexString.
    (hex size > 0 and: [hex first = $#])
        ifTrue: [hex := hex copyFrom: 2 to: hex size].

    "Handle short form #RGB"
    hex size = 3 ifTrue: [
        hex := String streamContents: [:s |
            hex do: [:c | s nextPut: c; nextPut: c]]].

    hex size = 6 ifFalse: [^nil].

    [r := Integer readFrom: (hex copyFrom: 1 to: 2) base: 16.
     g := Integer readFrom: (hex copyFrom: 3 to: 4) base: 16.
     b := Integer readFrom: (hex copyFrom: 5 to: 6) base: 16]
        on: Error do: [:ex | ^nil].

    ^self r: (r / 255.0) g: (g / 255.0) b: (b / 255.0)! !
```

### Theme Inheritance (include)

VS Code themes can inherit from other themes using the `include` field:

```json
{
  "name": "My Custom Theme",
  "include": "./light_plus.json",
  "tokenColors": [
    {
      "scope": "comment",
      "settings": { "foreground": "#999999" }
    }
  ]
}
```

**Implementation:**

```smalltalk
parseVSCodeTheme: jsonString
    "Parse a VS Code theme JSON string, handling inheritance."
    | json baseTheme result |
    [json := JsonReader parse: jsonString]
        on: Error do: [:ex | ^nil].

    "Handle theme inheritance"
    (json includesKey: 'include') ifTrue: [
        | includePath |
        includePath := json at: 'include'.
        baseTheme := self loadThemeNamed: (self extractBaseName: includePath).
        baseTheme ifNil: [baseTheme := Dictionary new]]
    ifFalse: [
        baseTheme := Dictionary new].

    "Process tokenColors and merge with base"
    result := baseTheme copy.
    (json at: 'tokenColors' ifAbsent: [#()]) do: [:rule |
        self processTokenColorRule: rule into: result].
    ^result!
```

### Obtaining VS Code Themes

Users can obtain VS Code theme files from:

1. **VS Code Installation:**
   ```
   %USERPROFILE%\.vscode\extensions\<theme-extension>\themes\
   ```

2. **VS Code GitHub Repository:**
   ```
   https://github.com/microsoft/vscode/tree/main/extensions/theme-defaults/themes
   ```

3. **Theme Extension Repositories:**
   - Dracula: https://github.com/dracula/visual-studio-code
   - One Dark Pro: https://github.com/Binaryify/OneDark-Pro
   - Nord: https://github.com/arcticicestudio/nord-visual-studio-code

**Installation Steps:**
1. Download the `.json` theme file
2. Copy to `%APPDATA%\Smalltalk-80\themes\`
3. Load with `SyntaxStyler useThemeNamed: 'theme-name'`

---

## Initialization Sequence

### Cold Start Integration

The special folders should be initialized early but after basic file system is available:

```smalltalk
"In SystemDictionary>>initialize or a dedicated init expression"
WindowsPath initializeSpecialFolders.

"Optional: Install default themes on first run"
SyntaxStyler installDefaultThemes.
```

### Lazy Initialization Alternative

If cold start initialization is not desired, lazy initialization occurs automatically on first access to `WindowsPath specialFolder:`.

---

## Error Handling

### Primitive Level
- Invalid argument type: Primitive fails, Smalltalk fallback returns `nil`
- Unknown folder symbol: Returns `nil`
- Folder not available on system: Returns `nil`
- Exception during path retrieval: Returns `nil`

### Smalltalk Level
- Missing folder: Convenience methods (`appDataPath`, etc.) raise descriptive errors
- Missing theme file: `loadThemeNamed:` returns `nil`
- Invalid theme file syntax: Logs to Transcript, returns `nil`
- Theme not a Dictionary: Logs to Transcript, returns `nil`

---

## Security Considerations

1. **Read-Only Primitive**: The primitive only reads system information, does not modify anything
2. **No Path Traversal**: Paths are provided by the OS, not user input
3. **Theme File Evaluation**: Uses Smalltalk Compiler, which is sandboxed to the interpreter
4. **No External Network Access**: All operations are local filesystem only

---

## Testing Strategy

### Unit Tests (C#)

```csharp
[TestFixture]
public class GetSpecialFolderTests
{
    [Test]
    public void AppData_ReturnsNonEmptyPath()
    {
        var result = ExecutePrimitive160("#appData");
        Assert.That(result, Is.Not.Null);
        Assert.That(result, Does.Contain("AppData"));
    }

    [Test]
    public void UnknownFolder_ReturnsNil()
    {
        var result = ExecutePrimitive160("#unknownFolder");
        Assert.That(result, Is.Null);
    }
}
```

### Integration Tests (Smalltalk)

```smalltalk
"Test special folder access"
(WindowsPath specialFolder: #appData) notNil ifFalse: [self error: 'appData failed'].
(WindowsPath specialFolder: #unknownXYZ) isNil ifFalse: [self error: 'unknown should be nil'].

"Test path construction"
| path |
path := WindowsPath applicationPath: 'TestApp'.
(path asString includesSubstring: 'AppData') ifFalse: [self error: 'path wrong'].
(path asString includesSubstring: 'TestApp') ifFalse: [self error: 'app name missing'].

"Test theme loading (requires theme file)"
SyntaxStyler ensureThemeDirectory.
SyntaxStyler installDefaultThemes.
(SyntaxStyler availableThemes includes: 'VSCodeLight')
    ifFalse: [self error: 'theme not installed'].
```

### JSON Parser Tests (Smalltalk)

```smalltalk
"Test JsonReader - basic values"
(JsonReader parse: '42') = 42 ifFalse: [self error: 'integer failed'].
(JsonReader parse: '3.14') = 3.14 ifFalse: [self error: 'float failed'].
(JsonReader parse: '"hello"') = 'hello' ifFalse: [self error: 'string failed'].
(JsonReader parse: 'true') == true ifFalse: [self error: 'true failed'].
(JsonReader parse: 'false') == false ifFalse: [self error: 'false failed'].
(JsonReader parse: 'null') isNil ifFalse: [self error: 'null failed'].

"Test JsonReader - string escapes"
(JsonReader parse: '"line1\nline2"') = ('line1', Character lf asString, 'line2')
    ifFalse: [self error: 'newline escape failed'].
(JsonReader parse: '"\u0041"') = 'A' ifFalse: [self error: 'unicode escape failed'].

"Test JsonReader - arrays"
| arr |
arr := JsonReader parse: '[1, 2, 3]'.
(arr size = 3 and: [arr first = 1]) ifFalse: [self error: 'array failed'].

"Test JsonReader - objects"
| obj |
obj := JsonReader parse: '{"name": "Alice", "age": 30}'.
((obj at: 'name') = 'Alice' and: [(obj at: 'age') = 30])
    ifFalse: [self error: 'object failed'].

"Test JsonReader - nested"
| nested |
nested := JsonReader parse: '{"items": [1, 2], "meta": {"count": 2}}'.
((nested at: 'items') size = 2 and: [((nested at: 'meta') at: 'count') = 2])
    ifFalse: [self error: 'nested failed'].

"Test JsonWriter - round trip"
| original roundTrip |
original := Dictionary new at: 'key' put: 'value'; yourself.
roundTrip := JsonReader parse: (JsonWriter stringify: original).
((roundTrip at: 'key') = 'value') ifFalse: [self error: 'round trip failed'].
```

---

## Future Extensions

1. **Settings File**: `%APPDATA%\Smalltalk-80\settings.st` for user preferences
2. **History File**: Command history for interactive sessions
3. **Cache Directory**: `%LOCALAPPDATA%\Smalltalk-80\cache\` for generated files
4. **Plugin Directory**: User-installed extensions
5. **Cross-Platform**: Abstract special folders for Linux/macOS (XDG, ~/Library)

---

## Implementation Checklist

- [ ] **Phase 1: C# Primitive (GetSpecialFolder only)**
  - [ ] Add `GetSpecialFolder` (160) to `FileInfoPrimitives.cs`
  - [ ] Register primitive 160 in `PrimitiveRegistry.cs`
  - [ ] Add C# unit tests for primitive 160

- [ ] **Phase 2: WindowsPath Extension**
  - [ ] Add `SpecialFolders` class variable to WindowsPath
  - [ ] Implement `initializeSpecialFolders`
  - [ ] Implement `specialFolder:` and convenience methods
  - [ ] Implement `applicationPath:`
  - [ ] Add to init expression if desired

- [ ] **Phase 3: JSON Parsing (Pure Smalltalk)**
  - [ ] Add `JsonReader` class to `Smalltalk-80.st`
  - [ ] Add `JsonWriter` class to `Smalltalk-80.st`
  - [ ] Test JsonReader with various JSON inputs:
    - [ ] Objects: `{"key": "value"}`
    - [ ] Arrays: `[1, 2, 3]`
    - [ ] Nested structures
    - [ ] Numbers (integer, float, exponent)
    - [ ] Strings with escapes (`\n`, `\t`, `\"`, `\\`, `\uXXXX`)
    - [ ] Booleans and null
  - [ ] Test JsonWriter round-trip (parse then stringify)

- [ ] **Phase 4: Color Hex Parsing (Smalltalk)**
  - [ ] Add `Color class>>fromHexString:` (pure Smalltalk)
  - [ ] Test with various hex formats (#RGB, #RRGGBB)

- [ ] **Phase 5: VS Code Theme Loading**
  - [ ] Implement `themeDirectory` in SyntaxStyler
  - [ ] Implement `parseVSCodeTheme:` using `JsonReader parse:`
  - [ ] Implement `processTokenColorRule:into:`
  - [ ] Implement `loadThemeNamed:` and `useThemeNamed:`
  - [ ] Implement `availableThemes`
  - [ ] Test with actual VS Code theme files (light_plus.json, dark_plus.json)

- [ ] **Phase 6: Theme Inheritance**
  - [ ] Implement `include` field handling
  - [ ] Implement `extractBaseName:` for path parsing
  - [ ] Test with themes that inherit from base themes

- [ ] **Phase 7: Integration & Polish**
  - [ ] Create `%APPDATA%\Smalltalk-80\themes\` on first use
  - [ ] Add theme selection UI (future)
  - [ ] Persist theme preference (future)
  - [ ] Document for users

---

## References

- [Environment.SpecialFolder Enum](https://docs.microsoft.com/en-us/dotnet/api/system.environment.specialfolder)
- [Windows Known Folders](https://docs.microsoft.com/en-us/windows/win32/shell/known-folders)
- [VS Code Color Theme Guide](https://code.visualstudio.com/api/extension-guides/color-theme)
- [VS Code Syntax Highlighting Guide](https://code.visualstudio.com/api/language-extensions/syntax-highlight-guide)
- [TextMate Language Grammars](https://macromates.com/manual/en/language_grammars)
- [VS Code Default Themes (GitHub)](https://github.com/microsoft/vscode/tree/main/extensions/theme-defaults/themes)
- [XDG Base Directory Specification](https://specifications.freedesktop.org/basedir-spec/basedir-spec-latest.html) (for future cross-platform)
