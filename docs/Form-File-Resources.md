# Form File Resources and Image Locations

This document tracks online resources where Smalltalk-80 Form file bitmaps, screenshots, and related graphics may be found.

## Primary Archives

### 1. Computer History Museum - Xerox Alto Archive
**URL**: https://xeroxalto.computerhistory.org/

- Contains snapshots of Alto source code, executables, documentation, and files from 1975-1987
- Smalltalk-80 located at: `[Indigo] <BasicDisks> Smalltalk14.bfs!1>`
- 14,680 total files (8,598 distinct)
- Access requires navigating the archive (some access restrictions apply)

**Status**: Archive exists but individual .form files not yet extracted

### 2. Mario Wolczko's ST80 Virtual Image Archive
**URL**: http://www.wolczko.com/st80/

**Main File**: http://www.wolczko.com/st80/image.tar.gz

**Contents**:
- Original Xerox Release 2 virtual image from magnetic tape
- Smalltalk-80.sources file (Dan Ingalls supplied)
- Original Xerox trace2 and trace3 files
- Instruction manuals and booklet scans
- Hand-typed Smalltalk sources from the Bluebook

**Note**: This is the canonical Smalltalk-80 virtual image in interchange format. The .form files may be embedded in the virtual image rather than as separate external files.

**Status**: Archive accessible but site blocks automated access (403 errors)

### 3. Computer History Museum - Smalltalk Zoo
**URL**: https://smalltalkzoo.computerhistory.org/

**Description**: Interactive browser-based emulators of Smalltalk versions from 1972 to modern Squeak

**Versions Available**:
- Smalltalk-72
- Smalltalk-76
- Smalltalk-78
- Smalltalk-80
- Modern Squeak (1995+)

**Features**:
- Run directly in web browser
- Historical screenshots of each version
- May show form editor and bitmap tools in action

**Status**: Accessible, may contain visible examples of Form files in screenshots

## GitHub Implementations

### 4. dbanay/Smalltalk
**URL**: https://github.com/dbanay/Smalltalk

**Description**: "By the Bluebook" C++ implementation of Smalltalk-80

**Resources**:
- `/images/desktop.png` - Screenshot of Smalltalk-80 system interface
- Runs on OS X, Windows, Linux, OpenBSD, FreeBSD
- Can export Forms to .pbm (Portable Bitmap) format

**Status**: Active repository with screenshots

### 5. rochus-keller/Smalltalk
**URL**: https://github.com/rochus-keller/Smalltalk

**Description**: Parser, code model, interpreter and navigable browser for original Xerox Smalltalk-80 v2 sources

**Resources**:
- Screenshots of running VM
- Image viewer with F5/F10 step controls
- Reproduces original trace2/trace3 files from Wolczko archive
- Binary downloads available for Windows

**Status**: Active repository with visual tools

### 6. devhawala/ST80
**URL**: https://github.com/devhawala/ST80

**Description**: Smalltalk-80 VM based on "Bluebook" specification (Java 8)

**Resources**:
- Can run Smalltalk-80 Version 2 image from 1983
- Handles bitmap/form files from virtual image

**Status**: Active implementation

## Documentation with Screenshots

### 7. Byte Magazine - August 1981 Smalltalk Special Issue
**URL**: https://archive.org/details/byte-magazine-1981-08

**Key Articles**:
- "The Smalltalk Graphics Kernel" by Daniel H. H. Ingalls (page 168)
- "Design Principles Behind Smalltalk" by Daniel H. H. Ingalls (page 286)
- Multiple illustrated articles about Smalltalk-80 system

**Status**: Full issue available for download/viewing on Internet Archive

### 8. Smalltalk-80: The Interactive Programming Environment (Red Book)
**URLs**:
- HAL Science PDF: https://hal.science/hal-05183024v1/file/TheInteractiveProgrammingEnv.pdf
- Scribd: https://www.scribd.com/document/377505670/Smalltalk-80-The-Interactive-Programming-Environment
- Download link: https://www.e-booksdirectory.com/details.php?ebook=3384

**Description**: 530-page book by Adele Goldberg (1983)

**Contents**:
- All figures created using Smalltalk-80 system itself
- Illustrated descriptions of class exploration and editors
- Likely contains screenshots of form editor and bitmap tools

**Status**: PDF available (40MB), but automated access blocked

### 9. Ken Shirriff's Blog - Xerox Alto and Smalltalk
**URL**: http://www.righto.com/2017/10/the-xerox-alto-smalltalk-and-rewriting.html

**Description**: Detailed blog post about Xerox Alto and Smalltalk-80 with screenshots

**Status**: Contains historical screenshots and technical details (403 access errors)

### 10. Computer History Museum - Dan Ingalls Smalltalk Demo Video
**URL**: https://www.computerhistory.org/collections/catalog/102738723

**Description**: Video demonstration of Smalltalk on restored Xerox Alto (Feb/Jun 2018)

**Features**:
- Dan Ingalls demonstrating integrated environment
- Shows overlapping windows, popup menus, class browsers, debuggers
- Demonstrates BitBLT graphics primitives
- May show form editor and cursor tools in action

**Status**: Video available for viewing

## Form File Locations in Source Code

Based on our analysis of the Smalltalk-80 source code, the following files are referenced:

### FormMenuView Required Files (21 button forms)
```
select.form.          - Form source selector
singlecopy.form.      - Single copy tool
repeatcopy.form.      - Repeat copy tool
line.form.            - Line drawing tool
curve.form.           - Curve drawing tool
block.form            - Block selection tool
over.form.            - Over display mode
under.form.           - Under display mode
reverse.form.         - Reverse display mode
erase.form.           - Erase mode
in.form.              - Filing in
magnify.form.         - Bit editing/magnification
white.form            - White color selector
lightgray.form        - Light gray color
gray.form             - Gray color
darkgray.form         - Dark gray color
black.form            - Black color selector
xgrid.form.           - X grid toggle
ygrid.form.           - Y grid toggle
togglegrids.form.     - Grid settings
out.form.             - Filing out
```

### Border Forms (2 files)
```
specialborderform.form - Special border decoration
borderform.form        - Standard border decoration
```

## Next Steps

To obtain these bitmap files:

1. **Download Wolczko's image.tar.gz** and extract to examine virtual image contents
2. **Access Smalltalk Zoo emulators** and take screenshots of form editor in action
3. **Download Byte Magazine August 1981** issue and extract form editor screenshots
4. **View Dan Ingalls demo video** to see cursor and form files in use
5. **Check GitHub repositories** for any bundled bitmap resources
6. **Examine Interactive Programming Environment PDF** for form editor illustrations
7. **Contact Computer History Museum** for access to Alto filesystem archive

## File Format Reference

See `Form-File-Format.md` for complete specification of the binary .form file format:
- 16-bit word-based, big-endian
- Header: fileCode(1), width, height, offsetX, offsetY
- Bitmap data in column-major order
- Based on Form class>>readFormFile: and Form>>writeOn: methods

## Reconstruction Options

If original files cannot be located:

1. **Create from screenshots**: Use emulator screenshots to recreate bitmaps pixel-by-pixel
2. **Generate programmatically**: Write Form files using documented binary format
3. **Use placeholders**: Create simple test bitmaps to enable FormMenuView testing
4. **Modern conversions**: Convert from other bitmap formats (PBM, XBM) to Form format

## Related Resources

- **Smalltalk/X Documentation**: https://live.exept.de/doc/online/english/overview/gui/image.html
  - Mentions "old Smalltalk-80 form file format" support

- **Class Form Tutorial**: http://bildungsgueter.de/Smalltalk/Pages/MVCTutorial/Pages/Form.htm
  - Discusses Form class and bitmap handling

## Status Summary

| Resource | Accessibility | Contains Forms | Notes |
|----------|---------------|----------------|-------|
| CHM Alto Archive | Limited | Unknown | Access restrictions |
| Wolczko ST80 | Available | Likely embedded | Download blocked |
| Smalltalk Zoo | Open | Screenshots only | Browser emulators |
| GitHub repos | Open | Screenshots | No .form files |
| Byte 1981 | Open | Screenshots | Archive.org |
| Red Book PDF | Available | Illustrations | Download blocked |
| Dan Ingalls Video | Open | Visual demo | CHM catalog |

**Last Updated**: 2025-11-22
