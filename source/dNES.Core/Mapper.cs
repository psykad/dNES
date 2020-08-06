namespace dNES.Core
{
    public abstract class Mapper
    {
        internal readonly byte PrgPages;
        internal readonly byte ChrPages;

        internal Mapper(byte prgPages, byte chrPages)
        {
            PrgPages = prgPages;
            ChrPages = chrPages;
        }

        public abstract bool CpuRead(ushort address, out ushort mappedAddress);
        public abstract bool CpuWrite(ushort address, out ushort mappedAddress, byte data);
        public abstract bool PpuRead(ushort address, out ushort mappedAddress);
        public abstract bool PpuWrite(ushort address, out ushort mappedAddress, byte data);
    }
}