// EXPERIMENTAL: GDI-based bitmap font support for Fixedsys
// This file can be safely deleted if the experiment is unsuccessful.
// To disable: set UseGdiBitmapFonts = false in FontBridgeFactory.cs

#if WINDOWS
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace Smalltalk80.Interpreter.Core.VM.Graphics;

/// <summary>
/// GDI-based font bridge for rendering bitmap fonts like Fixedsys.
///
/// EXPERIMENTAL: This class uses native GDI (not GDI+) to load and render
/// .FON bitmap fonts which cannot be loaded by GDI+/System.Drawing.
///
/// Key advantage: Fixedsys is already a 1bpp bitmap font, so we get
/// pixel-perfect rendering without antialiasing artifacts.
///
/// To remove this experiment:
/// 1. Delete this file
/// 2. Remove references from FontBridgeFactory.cs
/// </summary>
[SupportedOSPlatform("windows")]
public class GdiBitmapFontBridge : IFontBridge, IDisposable
{
    #region Native GDI P/Invoke Declarations

    private const int FW_NORMAL = 400;
    private const int FW_BOLD = 700;
    private const int DEFAULT_CHARSET = 1;
    private const int OUT_DEFAULT_PRECIS = 0;
    private const int CLIP_DEFAULT_PRECIS = 0;
    private const int DEFAULT_QUALITY = 0;
    private const int FIXED_PITCH = 1;
    private const int FF_MODERN = 48;
    private const int TRANSPARENT = 1;
    private const int OPAQUE = 2;
    private const int SRCCOPY = 0x00CC0020;
    private const int DIB_RGB_COLORS = 0;
    private const int BI_RGB = 0;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct TEXTMETRIC
    {
        public int tmHeight;
        public int tmAscent;
        public int tmDescent;
        public int tmInternalLeading;
        public int tmExternalLeading;
        public int tmAveCharWidth;
        public int tmMaxCharWidth;
        public int tmWeight;
        public int tmOverhang;
        public int tmDigitizedAspectX;
        public int tmDigitizedAspectY;
        public char tmFirstChar;
        public char tmLastChar;
        public char tmDefaultChar;
        public char tmBreakChar;
        public byte tmItalic;
        public byte tmUnderlined;
        public byte tmStruckOut;
        public byte tmPitchAndFamily;
        public byte tmCharSet;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BITMAPINFOHEADER
    {
        public uint biSize;
        public int biWidth;
        public int biHeight;
        public ushort biPlanes;
        public ushort biBitCount;
        public uint biCompression;
        public uint biSizeImage;
        public int biXPelsPerMeter;
        public int biYPelsPerMeter;
        public uint biClrUsed;
        public uint biClrImportant;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RGBQUAD
    {
        public byte rgbBlue;
        public byte rgbGreen;
        public byte rgbRed;
        public byte rgbReserved;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BITMAPINFO_1BPP
    {
        public BITMAPINFOHEADER bmiHeader;
        public RGBQUAD bmiColor0; // Black
        public RGBQUAD bmiColor1; // White
    }

    [DllImport("gdi32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr CreateFont(
        int nHeight, int nWidth, int nEscapement, int nOrientation, int fnWeight,
        uint fdwItalic, uint fdwUnderline, uint fdwStrikeOut, uint fdwCharSet,
        uint fdwOutputPrecision, uint fdwClipPrecision, uint fdwQuality,
        uint fdwPitchAndFamily, string lpszFace);

    [DllImport("gdi32.dll")]
    private static extern IntPtr SelectObject(IntPtr hdc, IntPtr hgdiobj);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteObject(IntPtr hObject);

    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateCompatibleDC(IntPtr hdc);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteDC(IntPtr hdc);

    [DllImport("gdi32.dll", CharSet = CharSet.Unicode)]
    private static extern bool GetTextMetrics(IntPtr hdc, out TEXTMETRIC lptm);

    [DllImport("gdi32.dll", CharSet = CharSet.Unicode)]
    private static extern bool GetCharWidth32(IntPtr hdc, uint iFirst, uint iLast, [Out] int[] lpBuffer);

    [DllImport("gdi32.dll", CharSet = CharSet.Unicode)]
    private static extern bool TextOut(IntPtr hdc, int x, int y, string lpString, int c);

    [DllImport("gdi32.dll")]
    private static extern int SetBkMode(IntPtr hdc, int mode);

    [DllImport("gdi32.dll")]
    private static extern uint SetTextColor(IntPtr hdc, uint color);

    [DllImport("gdi32.dll")]
    private static extern uint SetBkColor(IntPtr hdc, uint color);

    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateDIBSection(IntPtr hdc, ref BITMAPINFO_1BPP pbmi, uint usage,
        out IntPtr ppvBits, IntPtr hSection, uint offset);

    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateCompatibleBitmap(IntPtr hdc, int cx, int cy);

    [DllImport("user32.dll")]
    private static extern IntPtr GetDC(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

    [DllImport("gdi32.dll")]
    private static extern bool BitBlt(IntPtr hdcDest, int xDest, int yDest, int w, int h,
        IntPtr hdcSrc, int xSrc, int ySrc, int rop);

    [DllImport("gdi32.dll")]
    private static extern bool PatBlt(IntPtr hdc, int x, int y, int w, int h, uint rop);

    private const uint WHITENESS = 0x00FF0062;
    private const uint BLACKNESS = 0x00000042;

    #endregion

    private const int FixedDpi = 96;
    private readonly string _fontName;
    private readonly Dictionary<FontCacheKey, StrikeFontData> _fontCache = new();
    private bool _disposed;
    private bool _fontAvailable;

    /// <summary>
    /// Creates a GDI bitmap font bridge for the specified font.
    /// </summary>
    /// <param name="fontName">Font name (e.g., "Fixedsys", "Terminal")</param>
    public GdiBitmapFontBridge(string fontName = "Fixedsys")
    {
        _fontName = fontName;
        _fontAvailable = CheckFontAvailable();
    }

    /// <summary>
    /// Gets whether the requested bitmap font is available on this system.
    /// </summary>
    public bool IsFontAvailable => _fontAvailable;

    /// <inheritdoc/>
    public int RenderingDpi => FixedDpi;

    /// <inheritdoc/>
    public bool IsDeterministicMode => true;

    private bool CheckFontAvailable()
    {
        IntPtr hdc = IntPtr.Zero;
        IntPtr hFont = IntPtr.Zero;
        IntPtr hOldFont = IntPtr.Zero;

        try
        {
            hdc = GetDC(IntPtr.Zero);
            if (hdc == IntPtr.Zero) return false;

            // Create the font with a reasonable size to test
            hFont = CreateFont(
                -16, 0, 0, 0, FW_NORMAL,
                0, 0, 0, DEFAULT_CHARSET,
                OUT_DEFAULT_PRECIS, CLIP_DEFAULT_PRECIS, DEFAULT_QUALITY,
                FIXED_PITCH | FF_MODERN, _fontName);

            if (hFont == IntPtr.Zero) return false;

            hOldFont = SelectObject(hdc, hFont);

            // Get text metrics to verify the font loaded correctly
            if (!GetTextMetrics(hdc, out var tm))
                return false;

            // Verify it's a fixed-pitch font (bitmap fonts like Fixedsys are fixed-pitch)
            return tm.tmAveCharWidth == tm.tmMaxCharWidth || tm.tmMaxCharWidth > 0;
        }
        finally
        {
            if (hOldFont != IntPtr.Zero && hdc != IntPtr.Zero)
                SelectObject(hdc, hOldFont);
            if (hFont != IntPtr.Zero)
                DeleteObject(hFont);
            if (hdc != IntPtr.Zero)
                ReleaseDC(IntPtr.Zero, hdc);
        }
    }

    /// <inheritdoc/>
    public bool ResolveFace(FontDescriptor descriptor)
    {
        // We only support the configured bitmap font
        return _fontAvailable &&
               descriptor.FamilyName.Equals(_fontName, StringComparison.OrdinalIgnoreCase);
    }

    /// <inheritdoc/>
    public FontMetrics GetMetrics(FontDescriptor descriptor)
    {
        IntPtr hdc = IntPtr.Zero;
        IntPtr hFont = IntPtr.Zero;
        IntPtr hOldFont = IntPtr.Zero;

        try
        {
            hdc = GetDC(IntPtr.Zero);
            hFont = CreateGdiFont(descriptor);
            hOldFont = SelectObject(hdc, hFont);

            if (!GetTextMetrics(hdc, out var tm))
            {
                return new FontMetrics
                {
                    Ascent = descriptor.SizePixels,
                    Descent = 2,
                    Leading = 1,
                    MaxWidth = descriptor.SizePixels / 2,
                    SpaceWidth = descriptor.SizePixels / 2,
                    AverageWidth = descriptor.SizePixels / 2
                };
            }

            return new FontMetrics
            {
                Ascent = tm.tmAscent,
                Descent = tm.tmDescent,
                Leading = tm.tmExternalLeading,
                MaxWidth = tm.tmMaxCharWidth,
                SpaceWidth = tm.tmAveCharWidth,
                AverageWidth = tm.tmAveCharWidth,
                SubscriptOffset = tm.tmDescent,
                SuperscriptOffset = tm.tmAscent / 2
            };
        }
        finally
        {
            if (hOldFont != IntPtr.Zero && hdc != IntPtr.Zero)
                SelectObject(hdc, hOldFont);
            if (hFont != IntPtr.Zero)
                DeleteObject(hFont);
            if (hdc != IntPtr.Zero)
                ReleaseDC(IntPtr.Zero, hdc);
        }
    }

    /// <inheritdoc/>
    public int GetCharWidth(FontDescriptor descriptor, char character)
    {
        IntPtr hdc = IntPtr.Zero;
        IntPtr hFont = IntPtr.Zero;
        IntPtr hOldFont = IntPtr.Zero;

        try
        {
            hdc = GetDC(IntPtr.Zero);
            hFont = CreateGdiFont(descriptor);
            hOldFont = SelectObject(hdc, hFont);

            int[] widths = new int[1];
            if (GetCharWidth32(hdc, character, character, widths))
                return widths[0];

            // Fallback: use average char width from metrics
            if (GetTextMetrics(hdc, out var tm))
                return tm.tmAveCharWidth;

            return descriptor.SizePixels / 2;
        }
        finally
        {
            if (hOldFont != IntPtr.Zero && hdc != IntPtr.Zero)
                SelectObject(hdc, hOldFont);
            if (hFont != IntPtr.Zero)
                DeleteObject(hFont);
            if (hdc != IntPtr.Zero)
                ReleaseDC(IntPtr.Zero, hdc);
        }
    }

    /// <inheritdoc/>
    public int GetStringWidth(FontDescriptor descriptor, string text)
    {
        if (string.IsNullOrEmpty(text)) return 0;

        int total = 0;
        foreach (char c in text)
            total += GetCharWidth(descriptor, c);
        return total;
    }

    /// <inheritdoc/>
    public GlyphData RenderGlyph(FontDescriptor descriptor, char character)
    {
        var metrics = GetMetrics(descriptor);
        int width = GetCharWidth(descriptor, character);
        int height = metrics.Ascent + metrics.Descent;

        if (width <= 0) width = 1;
        if (height <= 0) height = 1;

        var bitmap = RenderCharacterToBitmap(descriptor, character, width, height);

        return new GlyphData
        {
            Character = character,
            Width = width,
            Bitmap = bitmap,
            Height = height,
            BitmapWidth = width
        };
    }

    /// <inheritdoc/>
    public StrikeFontData BuildStrikeFont(FontDescriptor descriptor, int minAscii = 0, int maxAscii = 255)
    {
        // Check cache first
        var cacheKey = new FontCacheKey
        {
            FamilyName = descriptor.FamilyName,
            SizePixels = descriptor.SizePixels,
            Dpi = FixedDpi,
            Bold = descriptor.Bold,
            Italic = descriptor.Italic,
            HintingEnabled = false
        };

        if (_fontCache.TryGetValue(cacheKey, out var cached))
            return cached;

        var metrics = GetMetrics(descriptor);
        int charCount = maxAscii - minAscii + 1;
        int fontHeight = metrics.Ascent + metrics.Descent;

        // Measure all character widths
        int[] widths = new int[charCount];
        int totalWidth = 0;

        IntPtr hdc = IntPtr.Zero;
        IntPtr hFont = IntPtr.Zero;
        IntPtr hOldFont = IntPtr.Zero;

        try
        {
            hdc = GetDC(IntPtr.Zero);
            hFont = CreateGdiFont(descriptor);
            hOldFont = SelectObject(hdc, hFont);

            // Get all character widths in one call for efficiency
            int[] allWidths = new int[256];
            if (GetCharWidth32(hdc, 0, 255, allWidths))
            {
                for (int i = 0; i < charCount; i++)
                {
                    int charCode = minAscii + i;
                    widths[i] = charCode < 256 ? Math.Max(1, allWidths[charCode]) : metrics.AverageWidth;
                    totalWidth += widths[i];
                }
            }
            else
            {
                // Fallback: use average width
                for (int i = 0; i < charCount; i++)
                {
                    widths[i] = Math.Max(1, metrics.AverageWidth);
                    totalWidth += widths[i];
                }
            }
        }
        finally
        {
            if (hOldFont != IntPtr.Zero && hdc != IntPtr.Zero)
                SelectObject(hdc, hOldFont);
            if (hFont != IntPtr.Zero)
                DeleteObject(hFont);
            if (hdc != IntPtr.Zero)
                ReleaseDC(IntPtr.Zero, hdc);
        }

        // Build xTable
        int[] xTable = new int[charCount + 2];
        int x = 0;
        for (int i = 0; i < charCount; i++)
        {
            xTable[i] = x;
            x += widths[i];
        }
        xTable[charCount] = x;
        xTable[charCount + 1] = x;

        int strikeLength = x;

        // Render all characters to combined bitmap
        var glyphsBitmap = RenderAllCharactersToBitmap(descriptor, minAscii, maxAscii,
            xTable, strikeLength, fontHeight);

        var strikeFontData = new StrikeFontData
        {
            Name = $"{_fontName}-{descriptor.SizePixels}",
            Metrics = metrics,
            MinAscii = minAscii,
            MaxAscii = maxAscii,
            XTable = xTable,
            GlyphsBitmap = glyphsBitmap,
            StrikeLength = strikeLength,
            XOffset = 0,
            Emphasis = (descriptor.Bold ? 1 : 0) | (descriptor.Italic ? 2 : 0),
            Type = 0,
            WidthTable = widths
        };

        _fontCache[cacheKey] = strikeFontData;
        return strikeFontData;
    }

    /// <inheritdoc/>
    public FontIdentity GetFontIdentity(FontDescriptor descriptor)
    {
        return new FontIdentity
        {
            FamilyName = _fontName,
            FilePath = null,
            FileHash = null,
            Weight = descriptor.Bold ? 700 : 400,
            Style = descriptor.Italic ? "Italic" : "Normal",
            Dpi = FixedDpi,
            HintingEnabled = false
        };
    }

    private IntPtr CreateGdiFont(FontDescriptor descriptor)
    {
        // Negative height means we want character height, not cell height
        int height = -descriptor.SizePixels;

        return CreateFont(
            height, 0, 0, 0,
            descriptor.Bold ? FW_BOLD : FW_NORMAL,
            descriptor.Italic ? 1u : 0u, 0, 0,
            DEFAULT_CHARSET,
            OUT_DEFAULT_PRECIS, CLIP_DEFAULT_PRECIS, DEFAULT_QUALITY,
            FIXED_PITCH | FF_MODERN, _fontName);
    }

    private uint[] RenderCharacterToBitmap(FontDescriptor descriptor, char character, int width, int height)
    {
        int wordsPerRow = (width + 31) / 32;
        uint[] result = new uint[wordsPerRow * height];

        IntPtr screenDC = IntPtr.Zero;
        IntPtr memDC = IntPtr.Zero;
        IntPtr hBitmap = IntPtr.Zero;
        IntPtr hOldBitmap = IntPtr.Zero;
        IntPtr hFont = IntPtr.Zero;
        IntPtr hOldFont = IntPtr.Zero;
        IntPtr ppvBits = IntPtr.Zero;

        try
        {
            screenDC = GetDC(IntPtr.Zero);
            memDC = CreateCompatibleDC(screenDC);

            // Create 1bpp DIB section
            var bmi = new BITMAPINFO_1BPP
            {
                bmiHeader = new BITMAPINFOHEADER
                {
                    biSize = (uint)Marshal.SizeOf<BITMAPINFOHEADER>(),
                    biWidth = width,
                    biHeight = -height, // Top-down DIB
                    biPlanes = 1,
                    biBitCount = 1,
                    biCompression = BI_RGB
                },
                bmiColor0 = new RGBQUAD { rgbBlue = 255, rgbGreen = 255, rgbRed = 255 }, // White = background
                bmiColor1 = new RGBQUAD { rgbBlue = 0, rgbGreen = 0, rgbRed = 0 }        // Black = foreground
            };

            hBitmap = CreateDIBSection(memDC, ref bmi, DIB_RGB_COLORS, out ppvBits, IntPtr.Zero, 0);
            if (hBitmap == IntPtr.Zero || ppvBits == IntPtr.Zero)
                return result;

            hOldBitmap = SelectObject(memDC, hBitmap);

            // Clear to white
            PatBlt(memDC, 0, 0, width, height, WHITENESS);

            // Select font
            hFont = CreateGdiFont(descriptor);
            hOldFont = SelectObject(memDC, hFont);

            // Set text colors (black text on white background)
            SetBkMode(memDC, TRANSPARENT);
            SetTextColor(memDC, 0x00000000); // Black
            SetBkColor(memDC, 0x00FFFFFF);   // White

            // Render character (handle underscore -> left arrow conversion)
            char renderChar = character == '_' ? '\u2190' : character;
            TextOut(memDC, 0, 0, renderChar.ToString(), 1);

            // Extract bits from DIB - GDI uses DWORD-aligned rows
            int bytesPerRow = ((width + 31) / 32) * 4;
            byte[] dibBits = new byte[bytesPerRow * height];
            Marshal.Copy(ppvBits, dibBits, 0, dibBits.Length);

            // Convert GDI 1bpp format to Smalltalk 1bpp format
            // GDI: MSB first, DWORD-aligned rows, 0=white, 1=black
            // Smalltalk: MSB first within 32-bit words, 1=black (ink)
            for (int y = 0; y < height; y++)
            {
                for (int wordX = 0; wordX < wordsPerRow; wordX++)
                {
                    uint word = 0;
                    for (int bit = 0; bit < 32; bit++)
                    {
                        int pixelX = wordX * 32 + bit;
                        if (pixelX >= width) break;

                        int byteIndex = y * bytesPerRow + pixelX / 8;
                        int bitInByte = 7 - (pixelX % 8); // GDI is MSB first

                        if (byteIndex < dibBits.Length)
                        {
                            bool isBlack = (dibBits[byteIndex] & (1 << bitInByte)) != 0;
                            if (isBlack)
                            {
                                word |= (uint)(1 << (31 - bit)); // Smalltalk MSB first
                            }
                        }
                    }
                    result[y * wordsPerRow + wordX] = word;
                }
            }
        }
        finally
        {
            if (hOldFont != IntPtr.Zero && memDC != IntPtr.Zero)
                SelectObject(memDC, hOldFont);
            if (hFont != IntPtr.Zero)
                DeleteObject(hFont);
            if (hOldBitmap != IntPtr.Zero && memDC != IntPtr.Zero)
                SelectObject(memDC, hOldBitmap);
            if (hBitmap != IntPtr.Zero)
                DeleteObject(hBitmap);
            if (memDC != IntPtr.Zero)
                DeleteDC(memDC);
            if (screenDC != IntPtr.Zero)
                ReleaseDC(IntPtr.Zero, screenDC);
        }

        return result;
    }

    private uint[] RenderAllCharactersToBitmap(FontDescriptor descriptor, int minAscii, int maxAscii,
        int[] xTable, int totalWidth, int height)
    {
        int wordsPerRow = (totalWidth + 31) / 32;
        uint[] result = new uint[wordsPerRow * height];

        IntPtr screenDC = IntPtr.Zero;
        IntPtr memDC = IntPtr.Zero;
        IntPtr hBitmap = IntPtr.Zero;
        IntPtr hOldBitmap = IntPtr.Zero;
        IntPtr hFont = IntPtr.Zero;
        IntPtr hOldFont = IntPtr.Zero;
        IntPtr ppvBits = IntPtr.Zero;

        try
        {
            screenDC = GetDC(IntPtr.Zero);
            memDC = CreateCompatibleDC(screenDC);

            // Create 1bpp DIB section for entire strike
            var bmi = new BITMAPINFO_1BPP
            {
                bmiHeader = new BITMAPINFOHEADER
                {
                    biSize = (uint)Marshal.SizeOf<BITMAPINFOHEADER>(),
                    biWidth = totalWidth,
                    biHeight = -height, // Top-down DIB
                    biPlanes = 1,
                    biBitCount = 1,
                    biCompression = BI_RGB
                },
                bmiColor0 = new RGBQUAD { rgbBlue = 255, rgbGreen = 255, rgbRed = 255 },
                bmiColor1 = new RGBQUAD { rgbBlue = 0, rgbGreen = 0, rgbRed = 0 }
            };

            hBitmap = CreateDIBSection(memDC, ref bmi, DIB_RGB_COLORS, out ppvBits, IntPtr.Zero, 0);
            if (hBitmap == IntPtr.Zero || ppvBits == IntPtr.Zero)
                return result;

            hOldBitmap = SelectObject(memDC, hBitmap);

            // Clear to white
            PatBlt(memDC, 0, 0, totalWidth, height, WHITENESS);

            // Select font
            hFont = CreateGdiFont(descriptor);
            hOldFont = SelectObject(memDC, hFont);

            SetBkMode(memDC, TRANSPARENT);
            SetTextColor(memDC, 0x00000000);
            SetBkColor(memDC, 0x00FFFFFF);

            // Render each character at its position
            int charCount = maxAscii - minAscii + 1;
            for (int i = 0; i < charCount; i++)
            {
                char c = (char)(minAscii + i);
                // Render left-arrow for underscore (Smalltalk-80 assignment operator)
                if (c == '_') c = '\u2190';

                TextOut(memDC, xTable[i], 0, c.ToString(), 1);
            }

            // Extract bits from DIB
            int bytesPerRow = ((totalWidth + 31) / 32) * 4;
            byte[] dibBits = new byte[bytesPerRow * height];
            Marshal.Copy(ppvBits, dibBits, 0, dibBits.Length);

            // Convert to Smalltalk format (32-bit words)
            for (int y = 0; y < height; y++)
            {
                for (int wordX = 0; wordX < wordsPerRow; wordX++)
                {
                    uint word = 0;
                    for (int bit = 0; bit < 32; bit++)
                    {
                        int pixelX = wordX * 32 + bit;
                        if (pixelX >= totalWidth) break;

                        int byteIndex = y * bytesPerRow + pixelX / 8;
                        int bitInByte = 7 - (pixelX % 8);

                        if (byteIndex < dibBits.Length)
                        {
                            bool isBlack = (dibBits[byteIndex] & (1 << bitInByte)) != 0;
                            if (isBlack)
                            {
                                word |= (uint)(1 << (31 - bit));
                            }
                        }
                    }
                    result[y * wordsPerRow + wordX] = word;
                }
            }
        }
        finally
        {
            if (hOldFont != IntPtr.Zero && memDC != IntPtr.Zero)
                SelectObject(memDC, hOldFont);
            if (hFont != IntPtr.Zero)
                DeleteObject(hFont);
            if (hOldBitmap != IntPtr.Zero && memDC != IntPtr.Zero)
                SelectObject(memDC, hOldBitmap);
            if (hBitmap != IntPtr.Zero)
                DeleteObject(hBitmap);
            if (memDC != IntPtr.Zero)
                DeleteDC(memDC);
            if (screenDC != IntPtr.Zero)
                ReleaseDC(IntPtr.Zero, screenDC);
        }

        return result;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _fontCache.Clear();
        GC.SuppressFinalize(this);
    }
}
#endif
