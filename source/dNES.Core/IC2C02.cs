using System;

namespace dNES.Core
{
    class IC2C02
    {
        private readonly Func<ushort, byte> _read;
        private readonly Action<ushort, byte> _write;

        /// <summary>
        /// PPU control register
        /// </summary>
        /// <remarks>
        /// CPU memory location 0x2000
        /// Flags:
        /// 7  bit  0
        /// ---- ----
        /// VPHB SINN
        /// |||| ||||
        /// |||| ||++- Base nametable address
        /// |||| ||    (0 = $2000; 1 = $2400; 2 = $2800; 3 = $2C00)
        /// |||| |+--- VRAM address increment per CPU read/write of PPUDATA
        /// |||| |     (0: add 1, going across; 1: add 32, going down)
        /// |||| +---- Sprite pattern table address for 8x8 sprites
        /// ||||       (0: $0000; 1: $1000; ignored in 8x16 mode)
        /// |||+------ Background pattern table address(0: $0000; 1: $1000)
        /// ||+------- Sprite size(0: 8x8 pixels; 1: 8x16 pixels)
        /// |+-------- PPU master/slave select
        /// |          (0: read backdrop from EXT pins; 1: output color on EXT pins)
        /// +--------- Generate an NMI at the start of the
        ///            vertical blanking interval(0: off; 1: on)
        /// </remarks>
        public byte PPUCTRL { get; set; }

        /// <summary>
        /// PPU mask register
        /// </summary>
        /// <remarks>
        /// CPU memory location 0x2001
        /// 7  bit  0
        /// ---- ----
        /// BGRs bMmG
        /// |||| ||||
        /// |||| |||+- Greyscale(0: normal color, 1: produce a greyscale display)
        /// |||| ||+-- 1: Show background in leftmost 8 pixels of screen, 0: Hide
        /// |||| |+--- 1: Show sprites in leftmost 8 pixels of screen, 0: Hide
        /// |||| +---- 1: Show background
        /// |||+------ 1: Show sprites
        /// ||+------- Emphasize red
        /// |+-------- Emphasize green
        /// +--------- Emphasize blue
        /// </remarks>
        public byte PPUMASK { get; set; }

        /// <summary>
        /// PPU status register
        /// </summary>
        /// <remarks>
        /// CPU memory location 0x2002
        /// 7  bit  0
        /// ---- ----
        /// VSO. ....
        /// |||| ||||
        /// |||+-++++- Least significant bits previously written into a PPU register
        /// |||        (due to register not being updated for this address)
        /// ||+------- Sprite overflow.The intent was for this flag to be set
        /// ||         whenever more than eight sprites appear on a scanline, but a
        /// ||         hardware bug causes the actual behavior to be more complicated
        /// ||         and generate false positives as well as false negatives; see
        /// ||         PPU sprite evaluation.This flag is set during sprite
        /// ||         evaluation and cleared at dot 1 (the second dot) of the
        /// ||         pre-render line.
        /// |+-------- Sprite 0 Hit.Set when a nonzero pixel of sprite 0 overlaps
        /// |          a nonzero background pixel; cleared at dot 1 of the pre-render
        /// |          line.Used for raster timing.
        /// +--------- Vertical blank has started (0: not in vblank; 1: in vblank).
        ///            Set at dot 1 of line 241 (the line * after* the post-render
        ///            line); cleared after reading $2002 and at dot 1 of the
        ///            pre-render line.
        /// </remarks>
        public byte PPUSTATUS { get; set; }

        /// <summary>
        /// OAM address port
        /// </summary>
        /// <remarks>
        /// CPU memory location 0x2003
        /// </remarks>
        public byte OAMADDR { get; set; }

        /// <summary>
        /// OAM data port
        /// </summary>
        /// <remarks>
        /// CPU memory location 0x2004
        /// </remarks>
        public byte OAMDATA { get; set; }

        /// <summary>
        /// PPU scrolling position register
        /// </summary>
        /// <remarks>
        /// CPU memory location 0x2005
        /// </remarks>
        public byte PPUSCROLL { get; set; }

        /// <summary>
        /// PPU address register
        /// </summary>
        /// <remarks>
        /// CPU memory location 0x2006
        /// </remarks>
        public byte PPUADDR { get; set; }

        /// <summary>
        /// PPU data port
        /// </summary>
        /// <remarks>
        /// CPU memory location 0x2007
        /// </remarks>
        public byte PPUDATA { get; set; }

        public IC2C02(Func<ushort, byte> read, Action<ushort, byte> write)
        {
            _read = read;
            _write = write;
        }
    }
}