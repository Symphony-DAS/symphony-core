using System;
using System.Collections.Generic;
using System.Linq;
using Heka;
using Symphony.Core;

namespace HekaSandbox
{
    static class HekaSandbox
    {
        static void Main(string[] args)
        {
            Logging.ConfigureConsole();

            var outSamples = Enumerable.Range(0, 30 * 10000).Select(i => (short)i).ToArray();

            var result = new IOBridge(IntPtr.Zero, 1, 1).RunTestMain(outSamples, outSamples.Length);

            Console.WriteLine("Read {0} samples.", result.Length);
            Console.WriteLine("Press any key to eixt...");
            Console.ReadKey();
        }
    }
}
