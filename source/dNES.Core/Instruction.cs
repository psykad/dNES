using System;

namespace dNES.Core
{
    class Instruction
    {
        public byte OpCode { get; }
        public Action Operation { get; }
        public Action AddressMode { get; }

        public Instruction(byte opCode, Action operation, Action addressMode)
        {
            OpCode = opCode;
            Operation = operation;
            AddressMode = addressMode;
        }
    }
}