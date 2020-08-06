using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace dNES.Core
{
    public class Cartridge
    {
        private const byte BIT0 = 0x01, BIT1 = 0x02, BIT2 = 0x04, BIT3 = 0x08, BIT4 = 0x10, BIT5 = 0x20, BIT6 = 0x40, BIT7 = 0x80;

        /// <summary>
        /// Full, uncut rom data from the file.
        /// </summary>
        private readonly byte[] _rom;

        private readonly byte[] _prgRom;
        private readonly byte[] _chrRom;

        /// <summary>
        /// Size of the PRG ROM in 16 KB units.
        /// </summary>
        private readonly byte _prgPages;

        /// <summary>
        /// Size of the CHR ROM in 8 KB units.
        /// </summary>
        private readonly byte _chrPages;

        /// <summary>
        /// iNES mapper number
        /// </summary>
        private readonly byte _mapperNumber;

        /// <summary>
        /// Ignore mirroring control or mirroring bit; instead provide four-screen VRAM.
        /// </summary>
        private readonly bool _fourScreenMode;

        /// <summary>
        /// 0: horizontal (vertical arrangement)
        /// 1: vertical(horizontal arrangement)
        /// </summary>
        private readonly byte _mirroring;

        /// <summary>
        /// Cartridge contains battery-backed PRG RAM ($6000-7FFF) or other persistent memory.
        /// </summary>
        private readonly bool _batteryBacked;

        /// <summary>
        /// 512-byte trainer at $7000-$71FF (stored before PRG data)
        /// </summary>
        private readonly bool _hasTrainer;

        private readonly Mapper _mapper;

        private static readonly Dictionary<int, Type> Mappers = new Dictionary<int, Type>
        {
            {0, typeof(Mapper000)}
        };

        public Cartridge(string filePath)
        {
            // Read file into byte array.
            _rom = System.IO.File.ReadAllBytes(filePath);

            // Verify iNES header.
            if (Encoding.UTF8.GetString(_rom.Take(3).ToArray()) != "NES")
                throw new Exception("Not a valid iNES file."); // TODO: Make this its own exception.

            // Number of 16k PRG pages.
            _prgPages = _rom[4];

            // Number of 8k CHR pages.
            _chrPages = _rom[5];

            // Flags byte 6
            _mirroring = (byte)(_rom[6] & BIT0);
            _batteryBacked = (_rom[6] & BIT1) == BIT1;
            _hasTrainer = (_rom[6] & BIT2) == BIT2;
            _fourScreenMode = (_rom[6] & BIT3) == BIT3;
            _mapperNumber = (byte)((_rom[6] >> 4) & 0x0F); // Bits 4-7 are lower nybble of mapper number.

            // Flags byte 7

            // Initialize cartridge mapper.
            if (!Mappers.ContainsKey(_mapperNumber))
                throw new Exception("Mapper not implemented"); // TODO: Custom exception?

            _mapper = (Mapper)Activator.CreateInstance(Mappers[_mapperNumber], _prgPages, _chrPages);

            // Generate PRG and CHR ROMs
            var headerSize = _hasTrainer ? 528 : 16;

            _prgRom = _rom.Skip(headerSize).Take(_prgPages * 0x4000).ToArray();
            _chrRom = _rom.Skip(headerSize + _prgRom.Length).Take(_chrPages * 0x2000).ToArray();
        }

        public byte CpuRead(ushort address)
        {
            if (_mapper.CpuRead(address, out var mappedAddress))
            {
                return _prgRom[mappedAddress];
            }
            
            throw new Exception($"Unexpected CPU read at {address}");
        }

        public void CpuWrite(ushort address, byte data)
        {
            if (_mapper.CpuWrite(address, out var mappedAddress, data))
            {
                return;
            }

            throw new Exception($"Unexpected CPU write at {address}");
        }

        public byte PpuRead(ushort address)
        {
            if (_mapper.PpuRead(address, out var mappedAddress))
            {
                return _chrRom[mappedAddress];
            }

            throw new Exception($"Unexpected PPU read at {address}");
        }

        public void PpuWrite(ushort address, byte data)
        {
            if (_mapper.PpuWrite(address, out var mappedAddress, data))
            {
                return;
            }

            throw new Exception($"Unexpected PPU write at {address}");
        }
    }
}