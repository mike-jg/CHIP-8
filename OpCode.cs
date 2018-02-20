
namespace CHIP_8
{
    class OpCode
    {
        public ushort FullOpCode;
        public ushort NNN;
        public byte NN, X, Y, N;

        public static OpCode CreateFromInstruction(ushort instruction)
        {
            OpCode oc = new OpCode()
            {
                FullOpCode = instruction,
                NNN = (ushort)(instruction & 0x0FFF),
                NN = (byte)(instruction & 0x00FF),
                N = (byte)(instruction & 0x000F),
                X = (byte)((instruction & 0x0F00) >> 8),
                Y = (byte)((instruction & 0x00F0) >> 4)
            };
            return oc;
        }
    }
}
