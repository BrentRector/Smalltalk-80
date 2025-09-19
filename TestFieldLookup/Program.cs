using System;

namespace TestFieldLookup
{
    public static class Program
    {
        public static void Main(string[] args)
        {
            if (args.Length > 0 && args[0] == "--bootstrap")
            {
                TestBootstrapWithPhaseMinusOne.Main2(args);
            }
            else
            {
                TestPhaseMinusOne.Main1(args);
            }
        }
    }
}