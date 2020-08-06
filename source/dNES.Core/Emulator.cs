using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace dNES.Core
{
    public class Emulator
    {
        private MOS6502 _cpu;
        private IC2C02 _ppu;
        private Bus _bus;
        private Cartridge _cartridge;

        public Emulator()
        {
            _bus = new Bus();
            _cpu = _bus.AttachCPU();
            _ppu = _bus.AttachPPU();
        }

        public void InsertCartridge(Cartridge cartridge)
        {
            _cartridge = cartridge;
            _bus.AttachCartridge(cartridge);
        }

        public void Start()
        {
            _cpu.PowerOn();
            _bus.ResetRAM();

            while (true)
            {
                _cpu.Clock();
            }
        }

        public void Reset()
        {
            _cpu.Reset();
        }
    }
}
