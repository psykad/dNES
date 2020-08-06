using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace dNES.Core
{
    class MOS6502
    {
        private readonly Func<ushort, byte> _read;
        private readonly Action<ushort, byte> _write;

        /// <summary>Carry Flag</summary>
        private const byte C = 0;

        /// <summary>Zero Flag</summary>
        private const byte Z = 1;

        /// <summary>Interrupt Disable Flag</summary>
        private const byte I = 2;

        /// <summary>Decimal Flag</summary>
        private const byte D = 3;

        /// <summary>Overflow Flag</summary>
        private const byte V = 6;

        /// <summary>Negative Flag</summary>
        private const byte N = 7;

        private struct Registers
        {
            /// <summary>Accumulator Register</summary>
            public byte A;

            /// <summary>Index X Register</summary>
            public byte X;

            /// <summary>Index Y Register</summary>
            public byte Y;

            /// <summary>Program Counter Register</summary>
            public ushort PC;

            /// <summary>Stack Pointer Register</summary>
            public byte S;

            /// <summary>Processor Status Register</summary>
            public byte P;
        }

        private readonly Dictionary<byte, Instruction> _instructions;
        private Registers _registers;
        private Instruction _currentInstruction;
        private ushort _effectiveAddress;
        private byte _fetched;
        private byte _cycles;

        private int _totalCycles = 7;
        private bool _paged;


        private const ushort ResetVector = 0xFFFC;

        public MOS6502(Func<ushort, byte> read, Action<ushort, byte> write)
        {
            _read = read;
            _write = write;

            #region Initialize instruction table
            var instructions = new List<Instruction>
            {
                new Instruction(0x69, ADC, IMM), new Instruction(0x65, ADC, ZPG), new Instruction(0x75, ADC, ZPX), new Instruction(0x6D, ADC, ABS), new Instruction(0x7D, ADC, ABX), new Instruction(0x79, ADC, ABY), new Instruction(0x61, ADC, IDX), new Instruction(0x71, ADC, IDY),
                new Instruction(0x29, AND, IMM), new Instruction(0x25, AND, ZPG), new Instruction(0x35, AND, ZPX), new Instruction(0x2D, AND, ABS), new Instruction(0x3D, AND, ABX), new Instruction(0x39, AND, ABY), new Instruction(0x21, AND, IDX), new Instruction(0x31, AND, IDY),
                new Instruction(0x0A, ASL, ACC), new Instruction(0x06, ASL, ZPG), new Instruction(0x16, ASL, ZPX), new Instruction(0x0E, ASL, ABS), new Instruction(0x1E, ASL, ABX),
                new Instruction(0x90, BCC, REL),
                new Instruction(0xB0, BCS, REL),
                new Instruction(0xF0, BEQ, REL),
                new Instruction(0x24, BIT, ZPG), new Instruction(0x2C, BIT, ABS),
                new Instruction(0x30, BMI, REL),
                new Instruction(0xD0, BNE, REL),
                new Instruction(0x10, BPL, REL),
                new Instruction(0x00, BRK, IMP),
                new Instruction(0x50, BVC, REL),
                new Instruction(0x70, BVS, REL),
                new Instruction(0x18, CLC, IMP),
                new Instruction(0xD8, CLD, IMP),
                new Instruction(0x58, CLI, IMP),
                new Instruction(0xB8, CLV, IMP),
                new Instruction(0xC9, CMP, IMM), new Instruction(0xC5, CMP, ZPG), new Instruction(0xD5, CMP, ZPX), new Instruction(0xCD, CMP, ABS), new Instruction(0xDD, CMP, ABX), new Instruction(0xD9, CMP, ABY), new Instruction(0xC1, CMP, IDX), new Instruction(0xD1, CMP, IDY),
                new Instruction(0xE0, CPX, IMM), new Instruction(0xE4, CPX, ZPG),new Instruction(0xEC, CPX, ABS),
                new Instruction(0xC0, CPY, IMM), new Instruction(0xC4, CPY, ZPG), new Instruction(0xCC, CPY, ABS),
                new Instruction(0xC6, DEC, ZPG), new Instruction(0xD6, DEC, ZPX), new Instruction(0xCE, DEC, ABS),new Instruction(0xDE, DEC, ABX),
                new Instruction(0xCA, DEX, IMP),
                new Instruction(0x88, DEY, IMP),
                new Instruction(0x49, EOR, IMM), new Instruction(0x45, EOR, ZPG), new Instruction(0x55, EOR, ZPX), new Instruction(0x4D, EOR, ABS), new Instruction(0x5D, EOR, ABX), new Instruction(0x59, EOR, ABY), new Instruction(0x41, EOR, IDX), new Instruction(0x51, EOR, IDY),
                new Instruction(0xE6, INC, ZPG), new Instruction(0xF6, INC, ZPX), new Instruction(0xEE, INC, ABS), new Instruction(0xFE, INC, ABX),
                new Instruction(0xE8, INX, IMP),
                new Instruction(0xC8, INY, IMP),
                new Instruction(0x4C, JMP, ABS), new Instruction(0x6C, JMP, IND),
                new Instruction(0x20, JSR, ABS),
                new Instruction(0xA9, LDA, IMM), new Instruction(0xA5, LDA, ZPG), new Instruction(0xB5, LDA, ZPX), new Instruction(0xAD, LDA, ABS), new Instruction(0xBD, LDA, ABX), new Instruction(0xB9, LDA, ABY), new Instruction(0xA1, LDA, IDX), new Instruction(0xB1, LDA, IDY),
                new Instruction(0xA2, LDX, IMM), new Instruction(0xA6, LDX, ZPG), new Instruction(0xB6, LDX, ZPY), new Instruction(0xAE, LDX, ABS), new Instruction(0xBE, LDX, ABY),
                new Instruction(0xA0, LDY, IMM), new Instruction(0xA4, LDY, ZPG), new Instruction(0xB4, LDY, ZPX), new Instruction(0xAC, LDY, ABS), new Instruction(0xBC, LDY, ABX),
                new Instruction(0x4A, LSR, ACC), new Instruction(0x46, LSR, ZPG), new Instruction(0x56, LSR, ZPX), new Instruction(0x4E, LSR, ABS), new Instruction(0x5E, LSR, ABX),
                new Instruction(0xEA, NOP, IMP),
                new Instruction(0x09, ORA, IMM), new Instruction(0x05, ORA, ZPG), new Instruction(0x15, ORA, ZPX), new Instruction(0x0D, ORA, ABS), new Instruction(0x1D, ORA, ABX), new Instruction(0x19, ORA, ABY), new Instruction(0x01, ORA, IDX), new Instruction(0x11, ORA, IDY),
                new Instruction(0x48, PHA, IMP),
                new Instruction(0x08, PHP, IMP),
                new Instruction(0x68, PLA, IMP),
                new Instruction(0x28, PLP, IMP),
                new Instruction(0x2A, ROL, ACC), new Instruction(0x26, ROL, ZPG), new Instruction(0x36, ROL, ZPX), new Instruction(0x2E, ROL, ABS), new Instruction(0x3E, ROL, ABX),
                new Instruction(0x6A, ROR, ACC), new Instruction(0x66, ROR, ZPG), new Instruction(0x76, ROR, ZPX), new Instruction(0x6E, ROR, ABS), new Instruction(0x7E, ROR, ABX),
                new Instruction(0x40, RTI, IMP),
                new Instruction(0x60, RTS, IMP),
                new Instruction(0xE9, SBC, IMM), new Instruction(0xE5, SBC, ZPG), new Instruction(0xF5, SBC, ZPX), new Instruction(0xED, SBC, ABS), new Instruction(0xFD, SBC, ABX), new Instruction(0xF9, SBC, ABY), new Instruction(0xE1, SBC, IDX), new Instruction(0xF1, SBC, IDY),
                new Instruction(0x38, SEC, IMP),
                new Instruction(0xF8, SED, IMP),
                new Instruction(0x78, SEI, IMP),
                new Instruction(0x85, STA, ZPG), new Instruction(0x95, STA, ZPX), new Instruction(0x8D, STA, ABS), new Instruction(0x9D, STA, ABX), new Instruction(0x99, STA, ABY), new Instruction(0x81, STA, IDX), new Instruction(0x91, STA, IDY),
                new Instruction(0x86, STX, ZPG), new Instruction(0x96, STX, ZPY), new Instruction(0x8E, STX, ABS),
                new Instruction(0x84, STY, ZPG), new Instruction(0x94, STY, ZPX), new Instruction(0x8C, STY, ABS),
                new Instruction(0xAA, TAX, IMP),
                new Instruction(0xA8, TAY, IMP),
                new Instruction(0xBA, TSX, IMP),
                new Instruction(0x8A, TXA, IMP),
                new Instruction(0x9A, TXS, IMP),
                new Instruction(0x98, TYA, IMP),

                // Illegal Opcodes
                //new Instruction(0x04, NOP, IMP),
                //new Instruction(0x44, NOP, IMP),
                //new Instruction(0x64, NOP, IMP),


                //new Instruction(0x0C, NOP, IMP),
                //new Instruction(0x14, NOP, IMP),
                //new Instruction(0x34, NOP, IMP),
                //new Instruction(0x54, NOP, IMP),
                //new Instruction(0x74, NOP, IMP),
                //new Instruction(0xD4, NOP, IMP),
                //new Instruction(0xF4, NOP, IMP),
                //new Instruction(0x1A, NOP, IMP),
                //new Instruction(0x3A, NOP, IMP),
                //new Instruction(0x5A, NOP, IMP),
                //new Instruction(0x7A, NOP, IMP),
                //new Instruction(0xDA, NOP, IMP),
                //new Instruction(0xFA, NOP, IMP),
                //new Instruction(0x80, NOP, IMP),
                //new Instruction(0x1C, NOP, IMP),
                //new Instruction(0x3C, NOP, IMP),
                //new Instruction(0x5C, NOP, IMP),
                //new Instruction(0x7C, NOP, IMP),
                //new Instruction(0xDC, NOP, IMP),
                //new Instruction(0xFC, NOP, IMP),
            };

            _instructions = new Dictionary<byte, Instruction>();

            instructions.ForEach(instruction =>
            {
                _instructions.Add(instruction.OpCode, instruction);
            });
            #endregion
        }

        /// <summary>
        /// Executes one clock cycle on the CPU.
        /// </summary>
        public void Clock()
        {
            if (_cycles == 0)
            {
                _paged = false;

                var output = $"{_registers.PC:X4}";
                var opCode = Read(_registers.PC++);
                _currentInstruction = _instructions[opCode];
                output += $"\tA:{_registers.A:X2}\tX:{_registers.X:X2}\tY:{_registers.Y:X2}\tP:{_registers.P:X2}\tSP:{_registers.S:X2}\tCYC:{_totalCycles}\t{(_registers.PC - 1):X4}\t{opCode:X2}\t{_currentInstruction.Operation.Method.Name}\t{_currentInstruction.AddressMode.Method.Name}";
                Debug.WriteLine(output);
                //Log(output);
                _currentInstruction.AddressMode();
                _currentInstruction.Operation();
                _totalCycles += _cycles;
            }

            _cycles--;
        }

        private void Log(string message)
        {
            var filename = @"C:\users\ryan\desktop\dnes.log";

            using (var streamWriter = new StreamWriter(filename, true))
            {
                streamWriter.WriteLine(message);
            }
        }

        /// <summary>
        /// Initializes CPU to power on state.
        /// </summary>
        public void PowerOn()
        {
            // http://wiki.nesdev.com/w/index.php/CPU_power_up_state
            _registers.PC = (ushort)((_read(ResetVector + 1) << 8) + _read(ResetVector));

            _registers.PC = 0xC000;
            Debug.WriteLine($"Reset Vector: {ResetVector:X4}");
            Debug.WriteLine($"Program Start: {_registers.PC:X4}");
            _registers.P = 0x24; // IRQ disabled
            _registers.A = 0;
            _registers.X = 0;
            _registers.Y = 0;
            _registers.S = 0xFD;
        }

        /// <summary>
        /// Initializes CPU to reset state.
        /// </summary>
        public void Reset()
        {
            // http://wiki.nesdev.com/w/index.php/CPU_power_up_state
            _registers.PC = (ushort)((_read(0xFFFC) << 8) + _read(0xFFFD));
            _registers.S -= 3;
            _registers.P |= 0x04;
        }

        /// <summary>
        /// Reads the byte at the given address. Adds 1 cycle to the clock.
        /// </summary>
        /// <param name="address">The 16-bit address.</param>
        /// <returns>An 8-bit value.</returns>
        byte Read(ushort address)
        {
            _cycles++;
            return _read(address);
        }

        /// <summary>
        /// Writes the given byte at the address. Adds 1 cycle to the clock.
        /// </summary>
        /// <param name="address">The 16-bit address.</param>
        /// <param name="data">The 8-bit data.</param>
        void Write(ushort address, byte data)
        {
            _cycles++;
            _write(address, data);
        }

        void Push(byte data)
        {
            Write((ushort)(0x0100 + _registers.S--), data);
        }

        byte Pull()
        {
            return Read((ushort)(0x0100 + ++_registers.S));
        }

        /// <summary>
        /// Fetches the byte located at the effective address or accumulator.
        /// </summary>
        /// <returns>The 8-bit data.</returns>
        byte Fetch()
        {
            if (_currentInstruction.Operation != IMP) _fetched = Read(_effectiveAddress);

            return _fetched;
        }

        /// <summary>
        /// Sets the given status flag's state.
        /// </summary>
        /// <param name="flag"></param>
        /// <param name="value"></param>
        void SetFlag(byte flag, bool value)
        {
            if (value)
                _registers.P |= (byte)(0x01 << flag);
            else
                _registers.P &= (byte)~(0x01 << flag);
        }

        /// <summary>
        /// Gets the given status flag's state.
        /// </summary>
        /// <param name="flag"></param>
        /// <returns></returns>
        bool GetFlag(byte flag)
        {
            return (_registers.P & (0x01 << flag)) > 0;
        }

        /// <summary>
        /// 8-bit adder with carry.
        /// </summary>
        /// <param name="a"></param>
        /// <param name="b"></param>
        /// <param name="carry"></param>
        /// <returns></returns>
        byte Adder(byte a, byte b, out bool carry)
        {
            var result = a + b;

            carry = (result & 0x100) > 0;

            return (byte)result;
        }

        #region Instruction Helpers
        void Branch(bool condition)
        {
            // Cycle 2
            var operand = _fetched;

            if (condition) // Take branch
            {
                // Cycle 3
                Read(_registers.PC);

                var PCL = (byte)(_registers.PC & 0xFF);
                var PCH = (byte)(_registers.PC >> 8);

                int offset = operand;

                if (offset > 127) offset = -(byte)(~operand + 1);

                PCL = (byte)(PCL + offset);
                var targetPC = (ushort)(_registers.PC + offset);
                _registers.PC = (ushort)((PCH << 8) + PCL);

                var paged = false;
                if (_registers.PC != targetPC)
                {
                    // PCH doesn't match. Use correct PC.
                    _registers.PC = targetPC;
                    paged = true;
                }

                if (paged)
                {
                    // Cycle 4
                    Read(_registers.PC++);
                }
            }
        }
        #endregion

        #region Addressing Modes
        /// <summary>
        /// Accumulator
        /// </summary>
        /// <remarks>
        /// Pulls value from accumulator into fetched byte.
        /// </remarks>
        void ACC()
        {
            _fetched = _registers.A;
            Read(_registers.PC);
        }

        /// <summary>
        /// Implied
        /// </summary>
        /// <remarks>
        /// Read next address and throw it away.
        /// </remarks>
        void IMP()
        {
            _fetched = Read(_registers.PC);
        }

        /// <summary>
        /// Immediate
        /// </summary>
        /// <remarks>
        /// Immediate addressing allows the programmer to directly specify an
        /// 8 bit constant within the instruction. It is indicated by a '#' symbol
        /// followed by an numeric expression.
        /// </remarks>
        void IMM()
        {
            _effectiveAddress = _registers.PC++;
        }

        /// <summary>
        /// Zero Page
        /// </summary>
        /// <remarks>
        /// An instruction using zero page addressing mode has only an 8 bit address operand.
        /// This limits it to addressing only the first 256 bytes of memory (e.g. $0000 to $00FF)
        /// where the most significant byte of the address is always zero. In zero page mode
        /// only the least significant byte of the address is held in the instruction making
        /// it shorter by one byte (important for space saving) and one less memory fetch during
        /// execution (important for speed).
        /// 
        /// An assembler will automatically select zero page addressing mode if the operand
        /// evaluates to a zero page address and the instruction supports the mode(not all do).
        /// </remarks>
        void ZPG()
        {
            var lo = Read(_registers.PC++);

            _effectiveAddress = lo;
        }

        /// <summary>
        /// Zero Page, X
        /// </summary>
        /// <remarks>
        /// The address to be accessed by an instruction using indexed zero page addressing is
        /// calculated by taking the 8 bit zero page address from the instruction and adding the
        /// current value of the X register to it. For example if the X register contains $0F
        /// and the instruction LDA $80,X is executed then the accumulator will be loaded from
        /// $008F (e.g. $80 + $0F => $8F).
        /// </remarks>
        void ZPX()
        {
            var lo = Read(_registers.PC++);
            Read(lo);
            lo += _registers.X;

            _effectiveAddress = lo;
        }

        /// <summary>
        /// Zero Page, Y
        /// </summary>
        /// <remarks>
        /// The address to be accessed by an instruction using indexed zero page addressing is
        /// calculated by taking the 8 bit zero page address from the instruction and adding the
        /// current value of the Y register to it.
        /// </remarks>
        void ZPY()
        {
            var lo = Read(_registers.PC++);
            Read(lo);
            lo += _registers.Y;

            _effectiveAddress = lo;
        }

        /// <summary>
        /// Absolute
        /// </summary>
        /// <remarks>
        /// Instructions using absolute addressing contain a full 16 bit address to identify the target location.
        /// </remarks>
        void ABS()
        {
            // Cycle 2
            var lo = Read(_registers.PC++);

            // Cycle 3
            var hi = Read(_registers.PC++);

            _effectiveAddress = (ushort)((hi << 8) + lo);
        }

        /// <summary>
        /// Absolute, X
        /// </summary>
        /// <remarks>
        /// The address to be accessed by an instruction using X register indexed absolute
        /// addressing is computed by taking the 16 bit address from the instruction and
        /// added the contents of the X register. For example if X contains $92 then an
        /// STA $2000,X instruction will store the accumulator at $2092 (e.g. $2000 + $92).
        /// </remarks>
        void ABX()
        {
            // Cycle 2
            var lo = Read(_registers.PC++);

            // Cycle 3
            var hi = Read(_registers.PC++);
            lo = Adder(lo, _registers.X, out var carry);

            // Cycle 4
            _effectiveAddress = (ushort)((hi << 8) + lo);

            if (new List<string> {"ASL", "LSR", "ROL", "ROR", "INC", "DEC", "STA", "STX", "STY"}
                .Exists(item => item == _currentInstruction.Operation.Method.Name))
                Read(_effectiveAddress);

            if (!carry) return;

            // Cycle 5
            _paged = true;
            hi++; // A carry occurred to HI byte.
            _effectiveAddress = (ushort)((hi << 8) + lo);
        }

        /// <summary>
        /// Absolute, Y
        /// </summary>
        /// <remarks>
        /// The Y register indexed absolute addressing mode is the same as the previous mode
        /// only with the contents of the Y register added to the 16 bit address from the instruction.
        /// </remarks>
        void ABY()
        {
            // Cycle 2
            var lo = Read(_registers.PC++);

            // Cycle 3
            var hi = Read(_registers.PC++);
            lo = Adder(lo, _registers.Y, out var carry);

            // Cycle 4
            _effectiveAddress = (ushort)((hi << 8) + lo);

            if (_currentInstruction.Operation == STA) Read(_effectiveAddress);

            if (!carry) return;

            _paged = true;
            hi++;
            _effectiveAddress = (ushort)((hi << 8) + lo);
        }

        /// <summary>
        /// Indirect Addressing
        /// </summary>
        void IND()
        {
            // Cycle 2
            var lo = Read(_registers.PC++);

            // Cycle 3
            var hi = Read(_registers.PC++);

            // Cycle 4
            var pointer = (ushort)((hi << 8) + lo++);
            var PCL = Read(pointer);

            // Cycle 5
            pointer = (ushort)((hi << 8) + lo);
            var PCH = Read(pointer);

            _effectiveAddress = (ushort)((PCH << 8) + PCL);
        }

        /// <summary>
        /// Indexed Indirect
        /// </summary>
        /// <remarks>
        /// Indexed indirect addressing is normally used in conjunction with a table of address held
        /// on zero page. The address of the table is taken from the instruction and the X register
        /// added to it (with zero page wrap around) to give the location of the least significant
        /// byte of the target address.
        /// </remarks>
        void IDX()
        {
            // Cycle 2
            var pointerAddress = Read(_registers.PC++);

            // Cycle 3
            Read(pointerAddress);
            var zeroPage = (byte)(pointerAddress + _registers.X);

            // Cycle 4
            var lo = Read(zeroPage++);

            // Cycle 5
            var hi = Read(zeroPage);

            _effectiveAddress = (ushort)((hi << 8) + lo);
        }

        /// <summary>
        /// Indirect Indexed
        /// </summary>
        /// <remarks>
        /// Indirect indirect addressing is the most common indirection mode used on the 6502.
        /// In instruction contains the zero page location of the least significant byte of
        /// 16 bit address. The Y register is dynamically added to this value to generated the
        /// actual target address for operation.
        /// </remarks>
        void IDY()
        {
            // Cycle 2
            var pointerAddress = Read(_registers.PC++);

            // Cycle 3
            var lo = Read(pointerAddress);

            // Cycle 4
            var hi = Read((byte)(pointerAddress + 1));
            lo = Adder(lo, _registers.Y, out var carry);

            _effectiveAddress = (ushort)((hi << 8) + lo);
            if (_currentInstruction.Operation == STA) Read(_effectiveAddress);

            if (!carry) return;

            _paged = true;
            hi++;
            _effectiveAddress = (ushort)((hi << 8) + lo);
        }

        /// <summary>
        /// Relative Addressing
        /// </summary>
        void REL()
        {
            _fetched = Read(_registers.PC++);
        }
        #endregion

        #region Instructions
        /// <summary>
        /// Add Memory to Accumulator with Carry
        /// </summary>
        /// <remarks>
        /// A + M + C → A, C                 N Z C I D V
        ///                                  + + + - - +
        /// addressing    assembler    opc  bytes  cycles
        /// --------------------------------------------
        /// immediate     ADC #oper     69    2     2
        /// zeropage      ADC oper      65    2     3
        /// zeropage,X    ADC oper,X    75    2     4
        /// absolute      ADC oper      6D    3     4
        /// absolute,X    ADC oper,X    7D    3     4*
        /// absolute,Y    ADC oper,Y    79    3     4*
        /// (indirect,X)  ADC (oper,X)  61    2     6
        /// (indirect),Y  ADC (oper),Y  71    2     5*
        /// </remarks>
        void ADC()
        {
            var value = Fetch();

            var mod = _registers.A + (GetFlag(C) ? 1 : 0);
            var result = Adder(value, (byte)mod, out var carry);

            SetFlag(C, carry);
            SetFlag(V, (~(_registers.A ^ value) & (_registers.A ^ result) & 0x80) > 0);
            SetFlag(N, result >> 7 > 0);
            SetFlag(Z, result == 0);

            _registers.A = result;

            if (_paged) _cycles++;
        }

        /// <summary>
        /// AND Memory with Accumulator
        /// </summary>
        /// <remarks>
        /// A AND M → A                      N Z C I D V
        ///                                  + + - - - -       
        /// addressing    assembler    opc  bytes  cycles
        /// --------------------------------------------
        /// immediate     AND #oper     29    2     2
        /// zeropage      AND oper      25    2     3
        /// zeropage,X    AND oper,X    35    2     4
        /// absolute      AND oper      2D    3     4
        /// absolute,X    AND oper,X    3D    3     4*
        /// absolute,Y    AND oper,Y    39    3     4*
        /// (indirect,X)  AND (oper,X)  21    2     6
        /// (indirect),Y  AND (oper),Y  31    2     5*
        /// </remarks>
        void AND()
        {
            var data = Fetch();

            _registers.A &= data;

            SetFlag(N, (_registers.A & 0x80) > 0);
            SetFlag(Z, _registers.A == 0);

            if (_paged) _cycles++;
        }

        /// <summary>
        /// Shift Left One Bit (Memory or Accumulator)
        /// </summary>
        /// <remarks>
        /// C ‹ [76543210] ‹ 0               N Z C I D V
        ///                                  + + + - - -
        /// addressing    assembler    opc  bytes  cycles
        /// --------------------------------------------
        /// accumulator   ASL A         0A    1     2
        /// zeropage      ASL oper      06    2     5
        /// zeropage,X    ASL oper,X    16    2     6
        /// absolute      ASL oper      0E    3     6
        /// absolute,X    ASL oper,X    1E    3     7
        /// </remarks>
        void ASL()
        {
            if (_currentInstruction.AddressMode != ACC) Fetch();
            var value = _fetched;

            SetFlag(C, (value & 0x80) > 0);

            if (_currentInstruction.AddressMode != ACC)
                Write(_effectiveAddress, value);

            value = (byte)(value << 1);

            if (_currentInstruction.AddressMode == ACC)
                _registers.A = value;
            else
                Write(_effectiveAddress, value);

            SetFlag(N, (value & 0x80) > 0);
            SetFlag(Z, value == 0);
        }

        /// <summary>
        /// Branch on Carry Clear
        /// </summary>
        /// <remarks>
        /// branch on C = 0                  N Z C I D V
        ///                                  - - - - - -
        /// addressing    assembler    opc  bytes  cycles
        /// --------------------------------------------
        /// relative      BCC oper      90    2     2**
        /// </remarks>
        void BCC()
        {
            Branch(GetFlag(C) == false);
        }

        /// <summary>
        /// Branch on Carry Set
        /// </summary>
        /// <remarks>
        /// branch on C = 1                  N Z C I D V
        ///                                  - - - - - -
        /// addressing    assembler    opc  bytes  cycles
        /// --------------------------------------------
        /// relative      BCS oper      B0    2     2**
        /// </remarks>
        void BCS()
        {
            Branch(GetFlag(C));
        }

        /// <summary>
        /// Branch on Result Zero
        /// </summary>
        /// <remarks>
        /// branch on Z = 1                  N Z C I D V
        ///                                  - - - - - -
        /// addressing    assembler    opc  bytes  cycles
        /// --------------------------------------------
        /// relative      BEQ oper      F0    2     2**
        /// </remarks>
        void BEQ()
        {
            Branch(GetFlag(Z));
        }

        /// <summary>
        /// Test Bits in Memory with Accumulator
        /// </summary>
        /// <remarks>
        /// -- bits 7 and 6 of operand are transferred to bit 7 and 6 of SR (N,V);
        ///    the zero flag is set to the result of operand AND accumulator.
        /// A AND M, M7 → N, M6 → V          N Z C I D V
        ///                                 M7 + - - - M6
        /// addressing    assembler    opc  bytes  cycles
        /// --------------------------------------------
        /// zeropage      BIT oper      24    2     3
        /// absolute      BIT oper      2C    3     4
        /// </remarks>
        void BIT()
        {
            var value = Fetch();

            SetFlag(N, (value & 0x80) > 0);
            SetFlag(V, (value & 0x40) > 0);
            value &= _registers.A;
            SetFlag(Z, value == 0);
        }

        /// <summary>
        /// Branch on Result Minus
        /// </summary>
        /// <remarks>
        /// branch on N = 1                  N Z C I D V
        ///                                  - - - - - -
        /// addressing    assembler    opc  bytes  cycles
        /// --------------------------------------------
        /// relative      BMI oper      30    2     2**
        /// </remarks>
        void BMI()
        {
            Branch(GetFlag(N));
        }

        /// <summary>
        /// Branch on Result not Zero
        /// </summary>
        /// <remarks>
        /// branch on Z = 0                  N Z C I D V
        ///                                  - - - - - -
        /// addressing    assembler    opc  bytes  cycles
        /// --------------------------------------------
        /// relative      BNE oper      D0    2     2**
        /// </remarks>
        void BNE()
        {
            Branch(GetFlag(Z) == false);
        }

        /// <summary>
        /// Branch on Result Plus
        /// </summary>
        /// <remarks>
        /// branch on N = 0                  N Z C I D V
        ///                                  - - - - - -
        /// addressing    assembler    opc  bytes  cycles
        /// --------------------------------------------
        /// relative      BPL oper      10    2     2**
        /// </remarks>
        void BPL()
        {
            Branch(GetFlag(N) == false);
        }

        /// <summary>
        /// Force Break
        /// </summary>
        /// <remarks>
        /// interrupt,                       N Z C I D V
        /// push PC+2, push SR               - - - 1 - -
        /// addressing    assembler    opc  bytes  cycles
        /// --------------------------------------------
        /// implied       BRK           00    1     7
        /// </remarks>
        void BRK()
        {
            // Cycle 2
            _registers.PC++; // "read" next byte, throw away, inc PC
            _cycles++;

            // Cycle 3
            Push((byte)(_registers.PC >> 8));

            // Cycle 4
            Push((byte)(_registers.PC & 0xFF));

            // Cycle 5
            Push((byte)(_registers.P | 0b00110000));

            // Cycle 6
            var PCL = Read(0xFFFE);

            // Cycle 7
            var PCH = Read(0xFFFF);

            _registers.PC = (ushort)((PCH << 8) + PCL);

            SetFlag(I, true);
        }

        /// <summary>
        /// Branch on Overflow Clear
        /// </summary>
        /// <remarks>
        /// branch on V = 0                  N Z C I D V
        ///                                  - - - - - -
        /// addressing    assembler    opc  bytes  cycles
        /// --------------------------------------------
        /// relative      BVC oper      50    2     2**
        /// </remarks>
        void BVC()
        {
            Branch(GetFlag(V) == false);
        }

        /// <summary>
        /// Branch on Overflow Set
        /// </summary>
        /// <remarks>
        /// branch on V = 1                  N Z C I D V
        ///                                  - - - - - -
        /// addressing    assembler    opc  bytes  cycles
        /// --------------------------------------------
        /// relative      BVC oper      70    2     2**
        /// </remarks>
        void BVS()
        {
            Branch(GetFlag(V));
        }

        /// <summary>
        /// Clear Carry Flag
        /// </summary>
        /// <remarks>
        /// 0 → C                            N Z C I D V
        ///                                  - - 0 - - -
        /// addressing    assembler    opc  bytes  cycles
        /// --------------------------------------------
        /// implied       CLC           18    1     2
        /// </remarks>
        void CLC()
        {
            SetFlag(C, false);
        }

        /// <summary>
        /// Clear Decimal Mode
        /// </summary>
        /// <remarks>
        /// 0 → D                            N Z C I D V
        ///                                  - - - - 0 -
        /// addressing    assembler    opc  bytes  cycles
        /// --------------------------------------------
        /// implied       CLD           D8    1     2
        /// </remarks>
        void CLD()
        {
            SetFlag(D, false);
        }

        /// <summary>
        /// Clear Interrupt Disable Bit
        /// </summary>
        /// <remarks>
        /// 0 → I                            N Z C I D V
        ///                                  - - - 0 - -
        /// addressing    assembler    opc  bytes  cycles
        /// --------------------------------------------
        /// implied       CLI           58    1     2  
        /// </remarks>
        void CLI()
        {
            SetFlag(I, false);
        }

        /// <summary>
        /// Clear Overflow Flag
        /// </summary>
        /// <remarks>
        /// 0 → V                            N Z C I D V
        ///                                  - - - - - 0
        /// addressing    assembler    opc  bytes  cycles
        /// --------------------------------------------
        /// implied       CLV           B8    1     2 
        /// </remarks>
        void CLV()
        {
            SetFlag(V, false);
        }

        /// <summary>
        /// Compare Memory with Accumulator
        /// </summary>
        /// <remarks>
        /// A - M                            N Z C I D V
        ///                                  + + + - - -
        /// addressing    assembler    opc  bytes  cycles
        /// --------------------------------------------
        /// immediate     CMP #oper     C9    2     2
        /// zeropage      CMP oper      C5    2     3
        /// zeropage,X    CMP oper,X    D5    2     4
        /// absolute      CMP oper      CD    3     4
        /// absolute,X    CMP oper,X    DD    3     4*
        /// absolute,Y    CMP oper,Y    D9    3     4*
        /// (indirect,X)  CMP (oper,X)  C1    2     6
        /// (indirect),Y  CMP (oper),Y  D1    2     5*
        /// </remarks>
        void CMP()
        {
            var value = Fetch();
            var result = _registers.A - value;

            SetFlag(C, _registers.A >= value);
            SetFlag(Z, result == 0);
            SetFlag(N, (result & 0x80) > 0);

            if (_paged) _cycles++;
        }

        /// <summary>
        /// Compare Memory and Index X
        /// </summary>
        /// <remarks>
        /// X - M                            N Z C I D V
        ///                                  + + + - - -
        /// addressing    assembler    opc  bytes  cycles
        /// --------------------------------------------
        /// immediate     CPX #oper     E0    2     2
        /// zeropage      CPX oper      E4    2     3
        /// absolute      CPX oper      EC    3     4
        /// </remarks>
        void CPX()
        {
            var value = Fetch();
            var result = _registers.X - value;

            SetFlag(C, _registers.X >= value);
            SetFlag(Z, result == 0);
            SetFlag(N, (result & 0x80) > 0);
        }

        /// <summary>
        /// Compare Memory and Index Y
        /// </summary>
        /// <remarks>
        /// Y - M                            N Z C I D V
        ///                                  + + + - - -
        /// addressing    assembler    opc  bytes  cycles
        /// --------------------------------------------
        /// immediate     CPY #oper     C0    2     2
        /// zeropage      CPY oper      C4    2     3
        /// absolute      CPY oper      CC    3     4
        /// </remarks>
        void CPY()
        {
            var value = Fetch();
            var result = _registers.Y - value;

            SetFlag(C, _registers.Y >= value);
            SetFlag(Z, result == 0);
            SetFlag(N, (result & 0x80) > 0);
        }

        /// <summary>
        /// Decrement Memory by One
        /// </summary>
        /// <remarks>
        /// M - 1 → M                        N Z C I D V
        ///                                  + + - - - -
        /// addressing    assembler    opc  bytes  cycles
        /// --------------------------------------------
        /// zeropage      DEC oper      C6    2     5
        /// zeropage,X    DEC oper,X    D6    2     6
        /// absolute      DEC oper      CE    3     6
        /// absolute,X    DEC oper,X    DE    3     7
        /// </remarks>
        void DEC()
        {
            var value = Fetch();
            var result = (byte)(value - 1);
            Write(_effectiveAddress, value);
            Write(_effectiveAddress, result);
            SetFlag(N, (result & 0x80) > 0);
            SetFlag(Z, result == 0);
        }

        /// <summary>
        /// Decrement Index X by One
        /// </summary>
        /// <remarks>
        /// X - 1 → X                        N Z C I D V
        ///                                  + + - - - -
        /// addressing    assembler    opc  bytes  cycles
        /// --------------------------------------------
        /// implied       DEC           CA    1     2
        /// </remarks>
        void DEX()
        {
            _registers.X--;
            SetFlag(N, (_registers.X & 0x80) > 0);
            SetFlag(Z, _registers.X == 0);
        }

        /// <summary>
        /// Decrement Index Y by One
        /// </summary>
        /// <remarks>
        /// Y - 1 → Y                        N Z C I D V
        ///                                  + + - - - -
        /// addressing    assembler    opc  bytes  cycles
        /// --------------------------------------------
        /// implied       DEC           88    1     2
        /// </remarks>
        void DEY()
        {
            _registers.Y--;
            SetFlag(N, (_registers.Y & 0x80) > 0);
            SetFlag(Z, _registers.Y == 0);
        }

        /// <summary>
        /// Exclusive-OR Memory with Accumulator
        /// </summary>
        /// <remarks>
        /// A EOR M → A                      N Z C I D V
        ///                                  + + - - - -
        /// addressing    assembler    opc  bytes  cycles
        /// --------------------------------------------
        /// immediate     EOR #oper     49    2     2
        /// zeropage      EOR oper      45    2     3
        /// zeropage,X    EOR oper,X    55    2     4
        /// absolute      EOR oper      4D    3     4
        /// absolute,X    EOR oper,X    5D    3     4*
        /// absolute,Y    EOR oper,Y    59    3     4*
        /// (indirect,X)  EOR (oper,X)  41    2     6
        /// (indirect),Y  EOR (oper),Y  51    2     5*    
        /// </remarks>
        void EOR()
        {
            _registers.A ^= Fetch();
            SetFlag(N, (_registers.A & 0x80) > 0);
            SetFlag(Z, _registers.A == 0);

            if (_paged) _cycles++;
        }

        /// <summary>
        /// Increment Memory by One
        /// </summary>
        /// <remarks>
        /// M + 1 → M                        N Z C I D V
        ///                                  + + - - - -
        /// addressing    assembler    opc  bytes  cycles
        /// --------------------------------------------
        /// zeropage      INC oper      E6    2     5
        /// zeropage,X    INC oper,X    F6    2     6
        /// absolute      INC oper      EE    3     6
        /// absolute,X    INC oper,X    FE    3     7  
        /// </remarks>
        void INC()
        {
            var value = Fetch();
            var result = (byte)(value + 1);
            Write(_effectiveAddress, value);
            Write(_effectiveAddress, result);
            SetFlag(N, (result & 0x80) > 0);
            SetFlag(Z, result == 0);
        }

        /// <summary>
        /// Increment Index X by One
        /// </summary>
        /// <remarks>
        /// X + 1 → X                        N Z C I D V
        ///                                  + + - - - -
        /// addressing    assembler    opc  bytes  cycles
        /// --------------------------------------------
        /// implied       INX           E8    1     2
        /// </remarks>
        void INX()
        {
            _registers.X++;
            SetFlag(N, (_registers.X & 0x80) > 0);
            SetFlag(Z, _registers.X == 0);
        }

        /// <summary>
        /// Increment Index Y by One
        /// </summary>
        /// <remarks>
        /// Y + 1 → Y                        N Z C I D V
        ///                                  + + - - - -
        /// addressing    assembler    opc  bytes  cycles
        /// --------------------------------------------
        /// implied       INY           C8    1     2
        /// </remarks>
        void INY()
        {
            _registers.Y++;
            SetFlag(N, (_registers.Y & 0x80) > 0);
            SetFlag(Z, _registers.Y == 0);
        }

        /// <summary>
        /// Jump to New Location
        /// </summary>
        /// <remarks>
        /// (PC+1) → PCL                    N Z C I D V
        /// (PC+2) → PCH                    - - - - - -
        /// addressing    assembler    opc  bytes  cycles
        /// --------------------------------------------
        /// absolute      JMP oper      4C    3     3
        /// indirect      JMP (oper)    6C    3     5
        /// </remarks>
        void JMP()
        {
            _registers.PC = _effectiveAddress;
        }

        /// <summary>
        /// Jump to New Location Saving Return Address
        /// </summary>
        /// <remarks>
        /// push (PC+2),                     N Z C I D V
        /// (PC+1) → PCL                     - - - - - -
        /// (PC+2) → PCH
        /// addressing    assembler    opc  bytes  cycles
        /// --------------------------------------------
        /// absolute      JSR oper      20    3     6
        /// </remarks>
        void JSR()
        {
            var returnAddress = _registers.PC - 1;

            // Cycle 4
            _cycles++;

            var PCH = (byte)(returnAddress >> 8);
            var PCL = (byte)(returnAddress & 0xFF);

            // Cycle 5
            Push(PCH);

            // Cycle 6
            Push(PCL);

            _registers.PC = _effectiveAddress;
        }

        /// <summary>
        /// Load Accumulator with Memory
        /// </summary>
        /// <remarks>
        /// M -> A                           N Z C I D V
        ///                                  + + - - - -
        /// addressing    assembler    opc  bytes  cycles
        /// --------------------------------------------
        /// immediate     LDA #oper     A9    2     2
        /// zeropage      LDA oper      A5    2     3
        /// zeropage,X    LDA oper,X    B5    2     4
        /// absolute      LDA oper      AD    3     4
        /// absolute,X    LDA oper,X    BD    3     4*
        /// absolute,Y    LDA oper,Y    B9    3     4*
        /// (indirect,X)  LDA (oper,X)  A1    2     6
        /// (indirect),Y  LDA (oper),Y  B1    2     5*
        /// </remarks>
        void LDA()
        {
            _registers.A = Fetch();
            SetFlag(N, (_registers.A & 0x80) > 0);
            SetFlag(Z, _registers.A == 0);

            if (_paged) _cycles++;
        }

        /// <summary>
        /// Load Index X with Memory
        /// </summary>
        /// <remarks>
        /// M → X                            N Z C I D V
        ///                                  + + - - - -
        /// addressing    assembler    opc  bytes  cycles
        /// --------------------------------------------
        /// immediate     LDX #oper     A2    2     2
        /// zeropage      LDX oper      A6    2     3
        /// zeropage,Y    LDX oper,Y    B6    2     4
        /// absolute      LDX oper      AE    3     4
        /// absolute,Y    LDX oper,Y    BE    3     4*
        /// </remarks>
        void LDX()
        {
            _registers.X = Fetch();
            SetFlag(N, (_registers.X & 0x80) > 0);
            SetFlag(Z, _registers.X == 0);

            if (_paged) _cycles++;
        }

        /// <summary>
        /// Load Index Y with Memory
        /// </summary>
        /// <remarks>
        /// M → Y                            N Z C I D V
        ///                                  + + - - - -
        /// addressing    assembler    opc  bytes  cycles
        /// --------------------------------------------
        /// immediate     LDY #oper     A0    2     2
        /// zeropage      LDY oper      A4    2     3
        /// zeropage,X    LDY oper,X    B4    2     4
        /// absolute      LDY oper      AC    3     4
        /// absolute,X    LDY oper,X    BC    3     4*
        /// </remarks>
        void LDY()
        {
            _registers.Y = Fetch();
            SetFlag(N, (_registers.Y & 0x80) > 0);
            SetFlag(Z, _registers.Y == 0);

            if (_paged) _cycles++;
        }

        /// <summary>
        /// Shift One Bit Right (Memory or Accumulator)
        /// </summary>
        /// <remarks>
        /// 0 › [76543210] › C               N Z C I D V
        ///                                  0 + + - - -
        /// addressing    assembler    opc  bytes  cycles
        /// --------------------------------------------
        /// accumulator   LSR A         4A    1     2
        /// zeropage      LSR oper      46    2     5
        /// zeropage,X    LSR oper,X    56    2     6
        /// absolute      LSR oper      4E    3     6
        /// absolute,X    LSR oper,X    5E    3     7
        /// </remarks>
        void LSR()
        {
            if (_currentInstruction.AddressMode != ACC) Fetch();
            var value = _fetched;

            SetFlag(C, (value & 0x01) > 0);

            if (_currentInstruction.AddressMode != ACC)
                Write(_effectiveAddress, value);

            value = (byte)(value >> 1);

            if (_currentInstruction.AddressMode == ACC)
                _registers.A = value;
            else
                Write(_effectiveAddress, value);

            SetFlag(Z, value == 0);
            SetFlag(N, false);
        }

        /// <summary>
        /// No Operation
        /// </summary>
        /// <remarks>
        /// ---                              N Z C I D V
        ///                                  - - - - - -
        /// addressing    assembler    opc  bytes  cycles
        /// --------------------------------------------
        /// implied       NOP           EA    1     2
        /// </remarks>
        void NOP()
        {

        }

        /// <summary>
        /// OR Memory with Accumulator
        /// </summary>
        /// <remarks>
        /// A OR M → A                       N Z C I D V
        ///                                  + + - - - -
        /// addressing    assembler    opc  bytes  cycles
        /// --------------------------------------------
        /// immediate     ORA #oper     09    2     2
        /// zeropage      ORA oper      05    2     3
        /// zeropage,X    ORA oper,X    15    2     4
        /// absolute      ORA oper      0D    3     4
        /// absolute,X    ORA oper,X    1D    3     4*
        /// absolute,Y    ORA oper,Y    19    3     4*
        /// (indirect,X)  ORA (oper,X)  01    2     6
        /// (indirect),Y  ORA (oper),Y  11    2     5*
        /// </remarks>
        void ORA()
        {
            var value = Fetch();
            _registers.A = (byte)(_registers.A | value);
            SetFlag(N, (_registers.A & 0x80) > 0);
            SetFlag(Z, _registers.A == 0);

            if (_paged) _cycles++;
        }

        /// <summary>
        /// Push Accumulator on Stack
        /// </summary>
        /// <remarks>
        /// push A                           N Z C I D V
        ///                                  - - - - - -
        /// addressing    assembler    opc  bytes  cycles
        /// --------------------------------------------
        /// implied       PHA           48    1     3  
        /// </remarks>
        void PHA()
        {
            Push(_registers.A);
        }

        /// <summary>
        /// Push Processor Status on Stack
        /// </summary>
        /// <remarks>
        /// push SR                          N Z C I D V
        ///                                  - - - - - -
        /// addressing    assembler    opc  bytes  cycles
        /// --------------------------------------------
        /// implied       PHP           08    1     3
        /// </remarks>
        void PHP()
        {
            var status = (byte)(_registers.P | 0x30); // Set "B" flag
            Push(status);
        }

        /// <summary>
        /// Pull Accumulator from Stack
        /// </summary>
        /// <remarks>
        /// pull A                           N Z C I D V
        ///                                  + + - - - -
        /// addressing    assembler    opc  bytes  cycles
        /// --------------------------------------------
        /// implied       PLA           68    1     4
        /// </remarks>
        void PLA()
        {
            _cycles++;

            _registers.A = Pull();

            SetFlag(N, (_registers.A & 0x80) > 0);
            SetFlag(Z, _registers.A == 0);
        }

        /// <summary>
        /// Pull Processor Status from Stack
        /// </summary>
        /// <remarks>
        /// pull SR                          N Z C I D V
        ///                                  from stack
        /// addressing    assembler    opc  bytes  cycles
        /// --------------------------------------------
        /// implied       PHP           28    1     4 
        /// </remarks>
        void PLP()
        {
            _cycles++;

            _registers.P = Pull();
            _registers.P = (byte)(_registers.P & ~0x30);
            _registers.P |= 0x20;
        }

        /// <summary>
        /// Rotate One Bit Left (Memory or Accumulator)
        /// </summary>
        /// <remarks>
        /// C ‹ [76543210] ‹ C               N Z C I D V
        ///                                  + + + - - -
        /// addressing    assembler    opc  bytes  cycles
        /// --------------------------------------------
        /// accumulator   ROL A         2A    1     2
        /// zeropage      ROL oper      26    2     5
        /// zeropage,X    ROL oper,X    36    2     6
        /// absolute      ROL oper      2E    3     6
        /// absolute,X    ROL oper,X    3E    3     7
        /// </remarks>
        void ROL()
        {
            if (_currentInstruction.AddressMode != ACC) Fetch();
            var value = _fetched;

            var carryOut = value & 0x80;
            var carryIn = GetFlag(C) ? 1 : 0;

            if (_currentInstruction.AddressMode != ACC)
                Write(_effectiveAddress, value);

            value <<= 1;
            value |= (byte)carryIn;

            SetFlag(C, carryOut > 0);

            if (_currentInstruction.AddressMode == ACC)
                _registers.A = value;
            else
                Write(_effectiveAddress, value);

            SetFlag(N, (value & 0x80) > 0);
            SetFlag(Z, value == 0);
        }

        /// <summary>
        /// Rotate One Bit Right (Memory or Accumulator)
        /// </summary>
        /// <remarks>
        /// C › [76543210] › C               N Z C I D V
        ///                                  + + + - - -
        /// addressing    assembler    opc  bytes  cycles
        /// --------------------------------------------
        /// accumulator   ROR A         6A    1     2
        /// zeropage      ROR oper      66    2     5
        /// zeropage,X    ROR oper,X    76    2     6
        /// absolute      ROR oper      6E    3     6
        /// absolute,X    ROR oper,X    7E    3     7
        /// </remarks>
        void ROR()
        {
            if (_currentInstruction.AddressMode != ACC) Fetch();
            var value = _fetched;

            var carryOut = value & 0x01;
            var carryIn = GetFlag(C) ? 1 : 0;

            if (_currentInstruction.AddressMode != ACC)
                Write(_effectiveAddress, value);

            value >>= 1;
            value |= (byte)(carryIn << 7);

            SetFlag(C, carryOut > 0);

            if (_currentInstruction.AddressMode == ACC)
                _registers.A = value;
            else
                Write(_effectiveAddress, value);

            SetFlag(N, (value & 0x80) > 0);
            SetFlag(Z, value == 0);
        }

        /// <summary>
        /// Return from Interrupt
        /// </summary>
        /// <remarks>
        /// pull SR, pull PC                 N Z C I D V
        ///                                  from stack
        /// addressing    assembler    opc  bytes  cycles
        /// --------------------------------------------
        /// implied       RTI           40    1     6
        /// </remarks>
        void RTI()
        {
            _cycles++;

            _registers.P = Pull();
            _registers.P = (byte)(_registers.P & ~0x30);
            _registers.P |= 0x20;
            var PCL = Pull();
            var PCH = Pull();
            _registers.PC = (ushort)((PCH << 8) + PCL);
        }

        /// <summary>
        /// Return from Subroutine
        /// </summary>
        /// <remarks>
        /// pull PC, PC+1 → PC               N Z C I D V
        ///                                  - - - - - -
        /// addressing    assembler    opc  bytes  cycles
        /// --------------------------------------------
        /// implied       RTS           60    1     6 
        /// </remarks>
        void RTS()
        {
            _cycles++;

            var PCL = Pull();
            var PCH = Pull();
            _registers.PC = (ushort)((PCH << 8) + PCL);

            _registers.PC++;
            _cycles++;
        }

        /// <summary>
        /// Subtract Memory from Accumulator with Borrow
        /// </summary>
        /// <remarks>
        /// A - M - C → A                    N Z C I D V
        ///                                  + + + - - +
        /// addressing    assembler    opc  bytes  cycles
        /// --------------------------------------------
        /// immediate     SBC #oper     E9    2     2
        /// zeropage      SBC oper      E5    2     3
        /// zeropage,X    SBC oper,X    F5    2     4
        /// absolute      SBC oper      ED    3     4
        /// absolute,X    SBC oper,X    FD    3     4*
        /// absolute,Y    SBC oper,Y    F9    3     4*
        /// (indirect,X)  SBC (oper,X)  E1    2     6
        /// (indirect),Y  SBC (oper),Y  F1    2     5*
        /// </remarks>
        void SBC()
        {
            var value = Fetch();
            value ^= 0xFF;

            var mod = _registers.A + (GetFlag(C) ? 1 : 0);
            var result = Adder(value, (byte)mod, out var carry);

            SetFlag(C, carry);
            SetFlag(V, (~(_registers.A ^ value) & (_registers.A ^ result) & 0x80) > 0);
            SetFlag(N, result >> 7 > 0);
            SetFlag(Z, result == 0);

            _registers.A = result;

            if (_paged) _cycles++;
        }

        /// <summary>
        /// Set Carry Flag
        /// </summary>
        /// <remarks>
        /// 1 → C                            N Z C I D V
        ///                                  - - 1 - - -
        /// addressing    assembler    opc  bytes  cycles
        /// --------------------------------------------
        /// implied       SEC           38    1     2
        /// </remarks>
        void SEC()
        {
            SetFlag(C, true);
        }

        /// <summary>
        /// Set Decimal Flag
        /// </summary>
        /// <remarks>
        /// 1 → D                            N Z C I D V
        ///                                  - - - - 1 -
        /// addressing    assembler    opc  bytes  cycles
        /// --------------------------------------------
        /// implied       SED           F8    1     2
        /// </remarks>
        void SED()
        {
            SetFlag(D, true);
        }

        /// <summary>
        /// Set Interrupt Disable Status
        /// </summary>
        /// <remarks>
        /// 1 → I                            N Z C I D V
        ///                                  - - - 1 - -
        /// addressing    assembler    opc  bytes  cycles
        /// --------------------------------------------
        /// implied       SEI           78    1     2
        /// </remarks>
        void SEI()
        {
            SetFlag(I, true);
        }

        /// <summary>
        /// Store Accumulator in Memory
        /// </summary>
        /// <remarks>
        /// A → M                            N Z C I D V
        ///                                  - - - - - -
        /// addressing    assembler    opc  bytes  cycles
        /// --------------------------------------------
        /// zeropage      STA oper      85    2     3
        /// zeropage,X    STA oper,X    95    2     4
        /// absolute      STA oper      8D    3     4
        /// absolute,X    STA oper,X    9D    3     5
        /// absolute,Y    STA oper,Y    99    3     5
        /// (indirect,X)  STA (oper,X)  81    2     6
        /// (indirect),Y  STA (oper),Y  91    2     6
        /// </remarks>
        void STA()
        {
            Write(_effectiveAddress, _registers.A);
        }

        /// <summary>
        /// Store Index X in Memory
        /// </summary>
        /// <remarks>
        /// X → M                           N Z C I D V
        ///                                 - - - - - -
        /// addressing    assembler    opc  bytes  cycles
        /// --------------------------------------------
        /// zeropage      STX oper      86    2     3
        /// zeropage,Y    STX oper,Y    96    2     4
        /// absolute      STX oper      8E    3     4
        /// </remarks>
        void STX()
        {
            Write(_effectiveAddress, _registers.X);
        }

        /// <summary>
        /// Store Index Y in Memory
        /// </summary>
        /// <remarks>
        /// Y → M                            N Z C I D V
        ///                                  - - - - - -
        /// addressing    assembler    opc  bytes  cycles
        /// --------------------------------------------
        /// zeropage      STY oper      84    2     3
        /// zeropage,X    STY oper,X    94    2     4
        /// absolute      STY oper      8C    3     4
        /// </remarks>
        void STY()
        {
            Write(_effectiveAddress, _registers.Y);
        }

        /// <summary>
        /// Transfer Accumulator to Index X
        /// </summary>
        /// <remarks>
        /// A → X                            N Z C I D V
        ///                                  + + - - - -
        /// addressing    assembler    opc  bytes  cycles
        /// --------------------------------------------
        /// implied       TAX           AA    1     2
        /// </remarks>
        void TAX()
        {
            _registers.X = _registers.A;

            SetFlag(N, (_registers.X & 0x80) > 0);
            SetFlag(Z, _registers.X == 0);
        }

        /// <summary>
        /// Transfer Accumulator to Index Y
        /// </summary>
        /// <remarks>
        /// A → Y                            N Z C I D V
        ///                                  + + - - - -
        /// addressing    assembler    opc  bytes  cycles
        /// --------------------------------------------
        /// implied       TAY           A8    1     2
        /// </remarks>
        void TAY()
        {
            _registers.Y = _registers.A;

            SetFlag(N, (_registers.Y & 0x80) > 0);
            SetFlag(Z, _registers.Y == 0);
        }

        /// <summary>
        /// Transfer Stack Pointer to Index X
        /// </summary>
        /// <remarks>
        /// SP → X                           N Z C I D V
        ///                                  + + - - - -
        /// addressing    assembler    opc  bytes  cycles
        /// --------------------------------------------
        /// implied       TSX           BA    1     2
        /// </remarks>
        void TSX()
        {
            _registers.X = _registers.S;

            SetFlag(N, (_registers.X & 0x80) > 0);
            SetFlag(Z, _registers.X == 0);
        }

        /// <summary>
        /// Transfer Index X to Accumulator
        /// </summary>
        /// <remarks>
        /// X → A                            N Z C I D V
        ///                                  + + - - - -
        /// addressing    assembler    opc  bytes  cycles
        /// --------------------------------------------
        /// implied       TXA           8A    1     2
        /// </remarks>
        void TXA()
        {
            _registers.A = _registers.X;

            SetFlag(N, (_registers.A & 0x80) > 0);
            SetFlag(Z, _registers.A == 0);
        }

        /// <summary>
        /// Transfer Index X to Stack Register
        /// </summary>
        /// <remarks>
        /// X → SP                           N Z C I D V
        ///                                  - - - - - -
        /// addressing    assembler    opc  bytes  cycles
        /// --------------------------------------------
        /// implied       TXS           9A    1     2
        /// </remarks>
        void TXS()
        {
            _registers.S = _registers.X;
        }

        /// <summary>
        /// Transfer Index Y to Accumulator
        /// </summary>
        /// <remarks>
        /// Y → A                            N Z C I D V
        ///                                  + + - - - -
        /// addressing    assembler    opc  bytes  cycles
        /// --------------------------------------------
        /// implied       TYA           98    1     2
        /// </remarks>
        void TYA()
        {
            _registers.A = _registers.Y;

            SetFlag(N, (_registers.A & 0x80) > 0);
            SetFlag(Z, _registers.A == 0);
        }
        #endregion
    }
}