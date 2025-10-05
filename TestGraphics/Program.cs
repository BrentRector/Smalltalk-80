using System;
using System.Threading;
using Smalltalk80.Interpreter.Graphics;

namespace TestGraphics;

/// <summary>
/// Test application for validating graphics and input functionality.
/// Tests BitBlt operations, Windows Forms display, and input events.
/// </summary>
class Program
{
    static void Main(string[] args)
    {
        Console.WriteLine("Smalltalk-80 Graphics & Input Test");
        Console.WriteLine("===================================\n");

        // Create display provider (640x480 monochrome display)
        var display = new WinFormsDisplayProvider(640, 480, "Graphics Test - Smalltalk-80");

        try
        {
            RunGraphicsTests(display);
            RunInputTests(display);
        }
        finally
        {
            display.Dispose();
        }
    }

    static void RunGraphicsTests(WinFormsDisplayProvider display)
    {
        Console.WriteLine("Graphics Tests:");
        Console.WriteLine("---------------");

        // Show the window before drawing
        display.Show();

        var displayBuffer = display.GetDisplayBuffer();

        // Test 1: Clear screen (combination rule 0)
        Console.WriteLine("1. Testing clear screen (rule 0)...");
        TestClearScreen(displayBuffer, display);

        // Test 2: Draw rectangle (combination rule 3 - copy)
        Console.WriteLine("2. Testing rectangle drawing (rule 3 - copy)...");
        TestDrawRectangle(displayBuffer, display);

        // Test 3: Test XOR (combination rule 6)
        Console.WriteLine("3. Testing XOR pattern (rule 6)...");
        TestXorPattern(displayBuffer, display);

        // Test 4: Test clipping
        Console.WriteLine("4. Testing clipping...");
        TestClipping(displayBuffer, display);

        // Test 5: Test halftone pattern
        Console.WriteLine("5. Testing halftone pattern...");
        TestHalftonePattern(displayBuffer, display);

        Console.WriteLine("\nGraphics tests complete!\n");
    }

    static void TestClearScreen(Memory<byte> displayBuffer, WinFormsDisplayProvider display)
    {
        var op = new BitBltOperation
        {
            DestBits = displayBuffer,
            SourceBits = displayBuffer,
            DestWidth = display.Width,
            DestHeight = display.Height,
            SourceWidth = display.Width,
            SourceHeight = display.Height,
            DestX = 0,
            DestY = 0,
            SourceX = 0,
            SourceY = 0,
            Width = display.Width,
            Height = display.Height,
            ClipX = 0,
            ClipY = 0,
            ClipWidth = display.Width,
            ClipHeight = display.Height,
            Rule = CombinationRule.Clear
        };

        BitBltEngine.CopyBits(op);
        display.InvalidateAll();
        display.ProcessEvents();
    }

    static void TestDrawRectangle(Memory<byte> displayBuffer, WinFormsDisplayProvider display)
    {
        // Create a small rectangle bitmap (100x80 pixels, all white)
        int rectWidth = 100;
        int rectHeight = 80;
        byte[] rectBits = new byte[(rectWidth * rectHeight + 7) / 8];

        // Fill with 1s (black in Smalltalk convention)
        for (int i = 0; i < rectBits.Length; i++)
        {
            rectBits[i] = 0xFF;
        }

        var op = new BitBltOperation
        {
            DestBits = displayBuffer,
            SourceBits = new Memory<byte>(rectBits),
            DestWidth = display.Width,
            DestHeight = display.Height,
            SourceWidth = rectWidth,
            SourceHeight = rectHeight,
            DestX = 50,
            DestY = 50,
            SourceX = 0,
            SourceY = 0,
            Width = rectWidth,
            Height = rectHeight,
            ClipX = 0,
            ClipY = 0,
            ClipWidth = display.Width,
            ClipHeight = display.Height,
            Rule = CombinationRule.Copy
        };

        BitBltEngine.CopyBits(op);
        display.InvalidateRect(50, 50, rectWidth, rectHeight);
        display.ProcessEvents();
    }

    static void TestXorPattern(Memory<byte> displayBuffer, WinFormsDisplayProvider display)
    {
        // Create two overlapping rectangles using XOR
        int rectWidth = 120;
        int rectHeight = 100;
        byte[] rectBits = new byte[(rectWidth * rectHeight + 7) / 8];

        for (int i = 0; i < rectBits.Length; i++)
        {
            rectBits[i] = 0xFF;
        }

        // First rectangle
        var op1 = new BitBltOperation
        {
            DestBits = displayBuffer,
            SourceBits = new Memory<byte>(rectBits),
            DestWidth = display.Width,
            DestHeight = display.Height,
            SourceWidth = rectWidth,
            SourceHeight = rectHeight,
            DestX = 200,
            DestY = 150,
            SourceX = 0,
            SourceY = 0,
            Width = rectWidth,
            Height = rectHeight,
            ClipX = 0,
            ClipY = 0,
            ClipWidth = display.Width,
            ClipHeight = display.Height,
            Rule = CombinationRule.Xor
        };

        BitBltEngine.CopyBits(op1);

        // Second rectangle (overlapping)
        var op2 = new BitBltOperation
        {
            DestBits = displayBuffer,
            SourceBits = new Memory<byte>(rectBits),
            DestWidth = display.Width,
            DestHeight = display.Height,
            SourceWidth = rectWidth,
            SourceHeight = rectHeight,
            DestX = 250,
            DestY = 180,
            SourceX = 0,
            SourceY = 0,
            Width = rectWidth,
            Height = rectHeight,
            ClipX = 0,
            ClipY = 0,
            ClipWidth = display.Width,
            ClipHeight = display.Height,
            Rule = CombinationRule.Xor
        };

        BitBltEngine.CopyBits(op2);
        display.InvalidateRect(200, 150, 170, 130);
        display.ProcessEvents();
    }

    static void TestClipping(Memory<byte> displayBuffer, WinFormsDisplayProvider display)
    {
        // Draw a large rectangle but clip it to a smaller region
        int rectWidth = 200;
        int rectHeight = 200;
        byte[] rectBits = new byte[(rectWidth * rectHeight + 7) / 8];

        for (int i = 0; i < rectBits.Length; i++)
        {
            rectBits[i] = 0xFF;
        }

        var op = new BitBltOperation
        {
            DestBits = displayBuffer,
            SourceBits = new Memory<byte>(rectBits),
            DestWidth = display.Width,
            DestHeight = display.Height,
            SourceWidth = rectWidth,
            SourceHeight = rectHeight,
            DestX = 400,
            DestY = 50,
            SourceX = 0,
            SourceY = 0,
            Width = rectWidth,
            Height = rectHeight,
            ClipX = 420,      // Clip 20 pixels from left
            ClipY = 70,       // Clip 20 pixels from top
            ClipWidth = 160,  // Only 160 pixels wide
            ClipHeight = 160, // Only 160 pixels tall
            Rule = CombinationRule.Copy
        };

        BitBltEngine.CopyBits(op);
        display.InvalidateRect(420, 70, 160, 160);
        display.ProcessEvents();
    }

    static void TestHalftonePattern(Memory<byte> displayBuffer, WinFormsDisplayProvider display)
    {
        // Create a checkerboard halftone pattern (16x16)
        byte[] halftone = new byte[(16 * 16 + 7) / 8];

        // Checkerboard: alternating rows of 0xAA and 0x55
        for (int row = 0; row < 16; row++)
        {
            int byteOffset = (row * 16) / 8;
            if (row % 2 == 0)
            {
                halftone[byteOffset] = 0xAA;
                halftone[byteOffset + 1] = 0xAA;
            }
            else
            {
                halftone[byteOffset] = 0x55;
                halftone[byteOffset + 1] = 0x55;
            }
        }

        // Create source rectangle (all 1s)
        int rectWidth = 150;
        int rectHeight = 150;
        byte[] sourceBits = new byte[(rectWidth * rectHeight + 7) / 8];
        for (int i = 0; i < sourceBits.Length; i++)
        {
            sourceBits[i] = 0xFF;
        }

        var op = new BitBltOperation
        {
            DestBits = displayBuffer,
            SourceBits = new Memory<byte>(sourceBits),
            HalftoneBits = new Memory<byte>(halftone),
            DestWidth = display.Width,
            DestHeight = display.Height,
            SourceWidth = rectWidth,
            SourceHeight = rectHeight,
            DestX = 100,
            DestY = 300,
            SourceX = 0,
            SourceY = 0,
            Width = rectWidth,
            Height = rectHeight,
            ClipX = 0,
            ClipY = 0,
            ClipWidth = display.Width,
            ClipHeight = display.Height,
            Rule = CombinationRule.Copy
        };

        BitBltEngine.CopyBits(op);
        display.InvalidateRect(100, 300, rectWidth, rectHeight);
        display.ProcessEvents();
    }

    static void RunInputTests(WinFormsDisplayProvider display)
    {
        Console.WriteLine("Input Tests:");
        Console.WriteLine("------------");
        Console.WriteLine("LEFT CLICK anywhere to draw a rectangle at that position");
        Console.WriteLine("RIGHT CLICK to exit the test\n");

        // Window already shown in RunGraphicsTests, no need to show again
        var displayBuffer = display.GetDisplayBuffer();
        bool waitingForLeftClick = true;
        int lastButtons = 0;

        while (true)
        {
            display.ProcessEvents();

            int currentButtons = display.GetMouseButtons();
            var mousePos = display.GetMousePosition();

            // Detect button press (transition from 0 to non-zero)
            // Button mapping: Left=1 (bit 0), Middle=2 (bit 1), Right=4 (bit 2)
            bool leftClickPressed = (lastButtons == 0) && ((currentButtons & 0x1) != 0);
            bool rightClickPressed = (lastButtons == 0) && ((currentButtons & 0x4) != 0);

            if (waitingForLeftClick && leftClickPressed)
            {
                Console.WriteLine($"Left click detected at ({mousePos.X}, {mousePos.Y})");
                Console.WriteLine("Drawing rectangle...");

                DrawRectangleAt(displayBuffer, display, mousePos.X, mousePos.Y);

                waitingForLeftClick = false;
                Console.WriteLine("Rectangle drawn! Now RIGHT CLICK to exit.\n");
            }

            if (rightClickPressed)
            {
                Console.WriteLine($"Right click detected at ({mousePos.X}, {mousePos.Y})");
                Console.WriteLine("Exiting test...\n");
                break;
            }

            lastButtons = currentButtons;
            Thread.Sleep(50);
        }

        Console.WriteLine("Input tests complete!\n");
    }

    static void DrawRectangleAt(Memory<byte> displayBuffer, WinFormsDisplayProvider display, int x, int y)
    {
        // Create a 60x40 rectangle
        int rectWidth = 60;
        int rectHeight = 40;
        byte[] rectBits = new byte[(rectWidth * rectHeight + 7) / 8];

        // Fill with black pixels
        for (int i = 0; i < rectBits.Length; i++)
        {
            rectBits[i] = 0xFF;
        }

        // Center the rectangle on the click position
        int destX = x - rectWidth / 2;
        int destY = y - rectHeight / 2;

        var op = new BitBltOperation
        {
            DestBits = displayBuffer,
            SourceBits = new Memory<byte>(rectBits),
            DestWidth = display.Width,
            DestHeight = display.Height,
            SourceWidth = rectWidth,
            SourceHeight = rectHeight,
            DestX = destX,
            DestY = destY,
            SourceX = 0,
            SourceY = 0,
            Width = rectWidth,
            Height = rectHeight,
            ClipX = 0,
            ClipY = 0,
            ClipWidth = display.Width,
            ClipHeight = display.Height,
            Rule = CombinationRule.Copy
        };

        BitBltEngine.CopyBits(op);
        display.InvalidateRect(destX, destY, rectWidth, rectHeight);
        display.ProcessEvents();
    }
}
