namespace dNES.Core
{
    class Mapper000 : Mapper
    {
        /* ####################################################################
         * iNES Mapper ID: 0
         * Name: NROM
         * PRG ROM capacity: 16K or 32K
         * PRG ROM window: n/a
         * PRG RAM capacity: 2K or 4K in Family Basic only
         * PRG RAM window: n/a
         * CHR capacity: 8K
         * CHR window: n/a
         * Nametable mirroring: Fixed H or V, controlled by solder pads (*V only)
         * Bus conflicts: Yes
         * IRQ: No
         * Audio: No
         * ####################################################################
         */
        public Mapper000(byte prgPages, byte chrPages) : base(prgPages, chrPages) { }

        public override bool CpuRead(ushort address, out ushort mappedAddress)
        {
            mappedAddress = 0x0000;

            // First 16 KB of PRG ROM.
            if (address >= 0x8000 && address <= 0xBFFF)
            {
                mappedAddress = (ushort) (address & 0x3FFF);
                return true;
            }

            // Last 16 KB of PRG ROM
            if (address >= 0xC000 && address <= 0xFFFF)
            {
                /*
                 * If ROM is only 1 page (16 KB), mirror the first page of PRG ROM.
                 * Otherwise use the actual address.
                 */
                mappedAddress = (ushort) (address & (PrgPages > 1 ? 0x7FFF : 0x3FFF));
                return true;
            }

            return false;
        }

        public override bool CpuWrite(ushort address, out ushort mappedAddress, byte data)
        {
            mappedAddress = 0;

            return true;
        }

        public override bool PpuRead(ushort address, out ushort mappedAddress)
        {
            mappedAddress = 0;

            return true;
        }

        public override bool PpuWrite(ushort address, out ushort mappedAddress, byte data)
        {
            mappedAddress = 0;

            return true;
        }
    }
}