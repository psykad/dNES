using System;
using System.Diagnostics;

namespace dNES.Core
{
    class Bus
    {
        private MOS6502 _cpu;
        private IC2C02 _ppu;
        private Cartridge _cartridge;
        private byte[] _internalRAM;

        public Bus()
        {
            _internalRAM = new byte[2048];
        }

        internal void ResetRAM()
        {
            for (var i = 0x00; i < 0x0800; i++) _internalRAM[i] = 0x00;
        }

        internal MOS6502 AttachCPU()
        {
            _cpu = new MOS6502(CPU_Read, CPU_Write);

            return _cpu;
        }

        public IC2C02 AttachPPU()
        {
            _ppu = new IC2C02(PPU_Read, PPU_Write);

            return _ppu;
        }

        internal void AttachCartridge(Cartridge cartridge)
        {
            _cartridge = cartridge;
        }

        internal byte CPU_Read(ushort address)
        {
            if (address >= 0x0000 && address <= 0x1FFF)
            {
                return _internalRAM[address & 0x07FF];
            }

            if (address >= 0x2000 && address <= 0x3FFF)
            {
                var adjustedAddress = (ushort)(0x2000 + (address & 0x0007));

                switch (adjustedAddress)
                {
                    case 0x2000: return _ppu.PPUCTRL;
                    case 0x2001: return _ppu.PPUMASK;
                    case 0x2002: return _ppu.PPUSTATUS;
                    case 0x2003: return _ppu.OAMADDR;
                    case 0x2004: return _ppu.OAMDATA;
                    case 0x2005: return _ppu.PPUSCROLL;
                    case 0x2006: return _ppu.PPUADDR;
                    case 0x2007: return _ppu.PPUDATA;
                }
            }

            if (address >= 0x8000 && address <= 0xFFFF)
            {
                return _cartridge.CpuRead(address);
            }

            throw new NotImplementedException();
        }

        internal void CPU_Write(ushort address, byte data)
        {
            Debug.WriteLine($"CPU WRITE {address:X4} | {data:X2}");
            if (address >= 0x0000 && address <= 0x1FFF)
            {
                _internalRAM[address % 0x800] = data;
                if (address == 0x0002 || address == 0x0003)
                    Debug.WriteLine($"TEST CODES: {_internalRAM[0x02]:X2} {_internalRAM[0x03]:X2} / {address:X4} / {data:X2}");
                return;
            }

            if (address >= 0x2000 && address <= 0x3FFF)
            {
                var adjustedAddress = (ushort)(0x2000 + (address & 0x0007));

                switch (adjustedAddress)
                {
                    case 0x2000: _ppu.PPUCTRL = data; break;
                    case 0x2001: _ppu.PPUMASK = data; break;
                    case 0x2002: _ppu.PPUSTATUS = data; break;
                    case 0x2003: _ppu.OAMADDR = data; break;
                    case 0x2004: _ppu.OAMDATA = data; break;
                    case 0x2005: _ppu.PPUSCROLL = data; break;
                    case 0x2006: _ppu.PPUADDR = data; break;
                    case 0x2007: _ppu.PPUDATA = data; break;
                }

                return;
            }

            if (address >= 0x8000 && address <= 0xFFFF)
            {
                _cartridge.CpuWrite(address, data);
                return;
            }

            throw new NotImplementedException();
        }

        internal byte PPU_Read(ushort address)
        {
            throw new NotImplementedException();
        }

        internal void PPU_Write(ushort address, byte data)
        {
            throw new NotImplementedException();
        }
    }
}