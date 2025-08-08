using System;
using System.IO;

namespace Smalltalk80.Test
{
    class TestPrimitiveParsing
    {
        static void Main()
        {
            var testSource = @"+ aNumber
	<primitive: 1>
	^super + aNumber";

            Console.WriteLine($"Testing source: {testSource}");

            var parser = new SmalltalkParser(testSource);

            try
            {
                var result = parser.ParseMethod("", new List<string> { "aNumber" }, new List<string>());
                Console.WriteLine($"Parse successful: HasPrimitive={result.HasPrimitive}, Index={result.PrimitiveIndex}");
            }
            catch (Exception e)
            {
                Console.WriteLine($"Parse failed: {e.Message}");
            }
        }
    }
}
