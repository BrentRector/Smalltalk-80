namespace Smalltalk80.Interpreter.Tests.Graphics;

/// <summary>
/// Tests for Color and TranslucentColor class logic.
/// These tests verify the RGB packing/unpacking mathematics that the Smalltalk
/// Color class implements. This C# test validates the expected behavior for
/// use by BitBltEngine32 and other graphics code.
/// </summary>
[Trait("Category", "Graphics")]
public class ColorTests
{
    #region RGB Packing (10-bit per component)

    /// <summary>
    /// Color stores RGB as 10 bits per component:
    /// R: bits 20-29, G: bits 10-19, B: bits 0-9
    /// </summary>
    [Fact]
    public void RgbPacking_BlackColor_ReturnsZero()
    {
        // Black = r:0 g:0 b:0
        int rgb = PackRgb(0.0, 0.0, 0.0);
        Assert.Equal(0, rgb);
    }

    [Fact]
    public void RgbPacking_WhiteColor_ReturnsMaxValue()
    {
        // White = r:1 g:1 b:1
        // Each component at max = 1023 (10 bits)
        int rgb = PackRgb(1.0, 1.0, 1.0);
        int expected = (1023 << 20) + (1023 << 10) + 1023;
        Assert.Equal(expected, rgb);
    }

    [Fact]
    public void RgbPacking_PureRed_SetsOnlyRedBits()
    {
        int rgb = PackRgb(1.0, 0.0, 0.0);
        Assert.Equal(1023 << 20, rgb);
    }

    [Fact]
    public void RgbPacking_PureGreen_SetsOnlyGreenBits()
    {
        int rgb = PackRgb(0.0, 1.0, 0.0);
        Assert.Equal(1023 << 10, rgb);
    }

    [Fact]
    public void RgbPacking_PureBlue_SetsOnlyBlueBits()
    {
        int rgb = PackRgb(0.0, 0.0, 1.0);
        Assert.Equal(1023, rgb);
    }

    [Fact]
    public void RgbPacking_MidGray_ReturnsHalfValues()
    {
        // Gray 0.5 -> approximately 511-512 per component
        int rgb = PackRgb(0.5, 0.5, 0.5);
        int r = (rgb >> 20) & 1023;
        int g = (rgb >> 10) & 1023;
        int b = rgb & 1023;

        // Should be around 511-512 (half of 1023)
        Assert.InRange(r, 510, 513);
        Assert.InRange(g, 510, 513);
        Assert.InRange(b, 510, 513);
    }

    #endregion

    #region RGB Unpacking

    [Fact]
    public void RgbUnpacking_Black_ReturnsZeroComponents()
    {
        int rgb = 0;
        (double r, double g, double b) = UnpackRgb(rgb);
        Assert.Equal(0.0, r);
        Assert.Equal(0.0, g);
        Assert.Equal(0.0, b);
    }

    [Fact]
    public void RgbUnpacking_White_ReturnsOneComponents()
    {
        int rgb = (1023 << 20) + (1023 << 10) + 1023;
        (double r, double g, double b) = UnpackRgb(rgb);
        Assert.Equal(1.0, r);
        Assert.Equal(1.0, g);
        Assert.Equal(1.0, b);
    }

    [Fact]
    public void RgbUnpacking_Roundtrip_PreservesValues()
    {
        double originalR = 0.75;
        double originalG = 0.25;
        double originalB = 0.5;

        int rgb = PackRgb(originalR, originalG, originalB);
        (double r, double g, double b) = UnpackRgb(rgb);

        // Allow small rounding error (1/1023 ≈ 0.001)
        Assert.InRange(r, originalR - 0.002, originalR + 0.002);
        Assert.InRange(g, originalG - 0.002, originalG + 0.002);
        Assert.InRange(b, originalB - 0.002, originalB + 0.002);
    }

    #endregion

    #region asPixelValue (32-bit ARGB)

    [Fact]
    public void AsPixelValue_Black_ReturnsOpaqueBlack()
    {
        // Black opaque: alpha=255, r=0, g=0, b=0
        uint expected = 0xFF000000;
        uint actual = ToPixelValue(0.0, 0.0, 0.0, 1.0);
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void AsPixelValue_White_ReturnsOpaqueWhite()
    {
        // White opaque: alpha=255, r=255, g=255, b=255
        uint expected = 0xFFFFFFFF;
        uint actual = ToPixelValue(1.0, 1.0, 1.0, 1.0);
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void AsPixelValue_Red_ReturnsOpaqueRed()
    {
        // Pure red: 0xFFFF0000
        uint expected = 0xFFFF0000;
        uint actual = ToPixelValue(1.0, 0.0, 0.0, 1.0);
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void AsPixelValue_Green_ReturnsOpaqueGreen()
    {
        // Pure green: 0xFF00FF00
        uint expected = 0xFF00FF00;
        uint actual = ToPixelValue(0.0, 1.0, 0.0, 1.0);
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void AsPixelValue_Blue_ReturnsOpaqueBlue()
    {
        // Pure blue: 0xFF0000FF
        uint expected = 0xFF0000FF;
        uint actual = ToPixelValue(0.0, 0.0, 1.0, 1.0);
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void AsPixelValue_SemiTransparent_IncludesAlpha()
    {
        // 50% transparent black: alpha=128
        uint actual = ToPixelValue(0.0, 0.0, 0.0, 0.5);
        int alpha = (int)((actual >> 24) & 0xFF);
        Assert.InRange(alpha, 127, 128);
    }

    [Fact]
    public void AsPixelValue_FullyTransparent_ReturnsZeroAlpha()
    {
        // Fully transparent: alpha=0
        uint actual = ToPixelValue(0.0, 0.0, 0.0, 0.0);
        Assert.Equal(0u, actual);
    }

    #endregion

    #region fromInteger (32-bit ARGB to Color)

    [Fact]
    public void FromInteger_OpaqueBlack_ExtractsCorrectComponents()
    {
        uint argb = 0xFF000000;
        (double r, double g, double b) = FromPixelValue(argb);
        Assert.Equal(0.0, r);
        Assert.Equal(0.0, g);
        Assert.Equal(0.0, b);
    }

    [Fact]
    public void FromInteger_OpaqueWhite_ExtractsCorrectComponents()
    {
        uint argb = 0xFFFFFFFF;
        (double r, double g, double b) = FromPixelValue(argb);
        Assert.Equal(1.0, r);
        Assert.Equal(1.0, g);
        Assert.Equal(1.0, b);
    }

    [Fact]
    public void FromInteger_PureRed_ExtractsCorrectComponents()
    {
        uint argb = 0xFFFF0000;
        (double r, double g, double b) = FromPixelValue(argb);
        Assert.Equal(1.0, r);
        Assert.Equal(0.0, g);
        Assert.Equal(0.0, b);
    }

    [Fact]
    public void FromInteger_MixedColor_ExtractsCorrectComponents()
    {
        // R=128, G=64, B=192
        uint argb = 0xFF8040C0;
        (double r, double g, double b) = FromPixelValue(argb);
        Assert.InRange(r, 0.50, 0.51); // 128/255 ≈ 0.502
        Assert.InRange(g, 0.25, 0.26); // 64/255 ≈ 0.251
        Assert.InRange(b, 0.75, 0.76); // 192/255 ≈ 0.753
    }

    [Fact]
    public void FromInteger_Roundtrip_PreservesColor()
    {
        // Create a color, convert to pixel, convert back
        double originalR = 0.8;
        double originalG = 0.4;
        double originalB = 0.6;

        uint pixel = ToPixelValue(originalR, originalG, originalB, 1.0);
        (double r, double g, double b) = FromPixelValue(pixel);

        // Allow for 8-bit quantization error (1/255 ≈ 0.004)
        Assert.InRange(r, originalR - 0.01, originalR + 0.01);
        Assert.InRange(g, originalG - 0.01, originalG + 0.01);
        Assert.InRange(b, originalB - 0.01, originalB + 0.01);
    }

    #endregion

    #region asFormColor (Luminance-based 1bpp conversion)

    [Fact]
    public void AsFormColor_Black_ReturnsOne()
    {
        // Black has luminance 0, which is < 0.5, so returns 1 (black in 1bpp)
        int formColor = ToFormColor(0.0, 0.0, 0.0);
        Assert.Equal(1, formColor);
    }

    [Fact]
    public void AsFormColor_White_ReturnsZero()
    {
        // White has luminance 1.0, which is >= 0.5, so returns 0 (white in 1bpp)
        int formColor = ToFormColor(1.0, 1.0, 1.0);
        Assert.Equal(0, formColor);
    }

    [Fact]
    public void AsFormColor_DarkGray_ReturnsOne()
    {
        // Dark gray (luminance < 0.5) should return 1 (black)
        int formColor = ToFormColor(0.3, 0.3, 0.3);
        Assert.Equal(1, formColor);
    }

    [Fact]
    public void AsFormColor_LightGray_ReturnsZero()
    {
        // Light gray (luminance >= 0.5) should return 0 (white)
        int formColor = ToFormColor(0.7, 0.7, 0.7);
        Assert.Equal(0, formColor);
    }

    [Fact]
    public void AsFormColor_PureRed_UsesCorrectLuminanceWeight()
    {
        // Pure red: luminance = 0.299 (< 0.5) -> black (1)
        int formColor = ToFormColor(1.0, 0.0, 0.0);
        Assert.Equal(1, formColor);
    }

    [Fact]
    public void AsFormColor_PureGreen_UsesCorrectLuminanceWeight()
    {
        // Pure green: luminance = 0.587 (>= 0.5) -> white (0)
        int formColor = ToFormColor(0.0, 1.0, 0.0);
        Assert.Equal(0, formColor);
    }

    [Fact]
    public void AsFormColor_PureBlue_UsesCorrectLuminanceWeight()
    {
        // Pure blue: luminance = 0.114 (< 0.5) -> black (1)
        int formColor = ToFormColor(0.0, 0.0, 1.0);
        Assert.Equal(1, formColor);
    }

    #endregion

    #region TranslucentColor

    [Fact]
    public void TranslucentColor_AsPixelValue_IncludesAlpha()
    {
        // 75% opaque red
        uint pixel = ToPixelValue(1.0, 0.0, 0.0, 0.75);
        int alpha = (int)((pixel >> 24) & 0xFF);
        Assert.InRange(alpha, 190, 192); // 0.75 * 255 ≈ 191
    }

    [Fact]
    public void TranslucentColor_FromInteger_ExtractsAlpha()
    {
        // 50% transparent white (0x80FFFFFF)
        uint argb = 0x80FFFFFF;
        double alpha = FromPixelValueAlpha(argb);
        Assert.InRange(alpha, 0.50, 0.51); // 128/255 ≈ 0.502
    }

    [Fact]
    public void TranslucentColor_FullyTransparent_HasZeroAlpha()
    {
        uint pixel = ToPixelValue(1.0, 1.0, 1.0, 0.0);
        int alpha = (int)((pixel >> 24) & 0xFF);
        Assert.Equal(0, alpha);
    }

    [Fact]
    public void TranslucentColor_FullyOpaque_Has255Alpha()
    {
        uint pixel = ToPixelValue(0.5, 0.5, 0.5, 1.0);
        int alpha = (int)((pixel >> 24) & 0xFF);
        Assert.Equal(255, alpha);
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void RgbClamping_NegativeValues_ClampedToZero()
    {
        int rgb = PackRgb(-0.5, -1.0, -100.0);
        (double r, double g, double b) = UnpackRgb(rgb);
        Assert.Equal(0.0, r);
        Assert.Equal(0.0, g);
        Assert.Equal(0.0, b);
    }

    [Fact]
    public void RgbClamping_ValuesOverOne_ClampedToOne()
    {
        int rgb = PackRgb(1.5, 2.0, 100.0);
        (double r, double g, double b) = UnpackRgb(rgb);
        Assert.Equal(1.0, r);
        Assert.Equal(1.0, g);
        Assert.Equal(1.0, b);
    }

    [Fact]
    public void AlphaClamping_NegativeValue_ClampedToZero()
    {
        uint pixel = ToPixelValue(1.0, 1.0, 1.0, -0.5);
        int alpha = (int)((pixel >> 24) & 0xFF);
        Assert.Equal(0, alpha);
    }

    [Fact]
    public void AlphaClamping_ValueOverOne_ClampedToOne()
    {
        uint pixel = ToPixelValue(1.0, 1.0, 1.0, 1.5);
        int alpha = (int)((pixel >> 24) & 0xFF);
        Assert.Equal(255, alpha);
    }

    #endregion

    #region Helper Methods (Mirrors Smalltalk Color implementation)

    /// <summary>
    /// Pack RGB float values (0.0-1.0) into 30-bit integer (10 bits per component).
    /// Mirrors Color>>setRed:green:blue:
    /// </summary>
    private static int PackRgb(double r, double g, double b)
    {
        int ri = (int)Math.Round(Math.Clamp(r, 0.0, 1.0) * 1023);
        int gi = (int)Math.Round(Math.Clamp(g, 0.0, 1.0) * 1023);
        int bi = (int)Math.Round(Math.Clamp(b, 0.0, 1.0) * 1023);
        return (ri << 20) + (gi << 10) + bi;
    }

    /// <summary>
    /// Unpack 30-bit RGB integer to float values (0.0-1.0).
    /// Mirrors Color>>red, green, blue
    /// </summary>
    private static (double r, double g, double b) UnpackRgb(int rgb)
    {
        double r = (rgb >> 20) / 1023.0;
        double g = ((rgb >> 10) & 1023) / 1023.0;
        double b = (rgb & 1023) / 1023.0;
        return (r, g, b);
    }

    /// <summary>
    /// Convert RGBA to 32-bit ARGB pixel value.
    /// Mirrors Color>>asPixelValue and TranslucentColor>>asPixelValue
    /// </summary>
    private static uint ToPixelValue(double r, double g, double b, double alpha)
    {
        int a = (int)Math.Round(Math.Clamp(alpha, 0.0, 1.0) * 255);
        int ri = (int)Math.Round(Math.Clamp(r, 0.0, 1.0) * 255);
        int gi = (int)Math.Round(Math.Clamp(g, 0.0, 1.0) * 255);
        int bi = (int)Math.Round(Math.Clamp(b, 0.0, 1.0) * 255);
        return (uint)((a << 24) + (ri << 16) + (gi << 8) + bi);
    }

    /// <summary>
    /// Extract RGB from 32-bit ARGB pixel value.
    /// Mirrors Color class>>fromInteger:
    /// </summary>
    private static (double r, double g, double b) FromPixelValue(uint argb)
    {
        double r = ((argb >> 16) & 0xFF) / 255.0;
        double g = ((argb >> 8) & 0xFF) / 255.0;
        double b = (argb & 0xFF) / 255.0;
        return (r, g, b);
    }

    /// <summary>
    /// Extract alpha from 32-bit ARGB pixel value.
    /// Mirrors TranslucentColor class>>fromInteger:
    /// </summary>
    private static double FromPixelValueAlpha(uint argb)
    {
        return ((argb >> 24) & 0xFF) / 255.0;
    }

    /// <summary>
    /// Convert RGB to 1bpp form color using luminance threshold.
    /// Mirrors Color>>asFormColor
    /// </summary>
    private static int ToFormColor(double r, double g, double b)
    {
        double luminance = (r * 0.299) + (g * 0.587) + (b * 0.114);
        return luminance < 0.5 ? 1 : 0;
    }

    #endregion
}
