using System;
using dNES.Core;

namespace dNES.ConsoleHarness
{
    class Program
    {
        static void Main(string[] args)
        {
            var cartridge = new Cartridge(@"C:\users\ryan\desktop\nestest.nes");

            var NES = new Emulator();
            NES.InsertCartridge(cartridge);
            NES.Start();

            Console.ReadKey();
        }
    }
}