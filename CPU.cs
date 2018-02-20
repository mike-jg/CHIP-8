using System;

namespace CHIP_8
{
    public class CPU
    {
        // current Op code to execute
        private OpCode currentOpCode;

        // graphics width and height
        public const byte width = 64;
        public const byte height = 32;

        // 4k memory
        //
        // 0x000-0x1FF - Chip 8 interpreter (contains font set in emu)
        // 0x050-0x0A0 - Used for the built in 4x5 pixel font set(0-F)
        // 0x200-0xFFF - Program ROM and work RAM
        //
        private byte[] memory;

        // CPU registers
        // VF or V[16] is the carry flag
        private byte[] V;

        private bool[] keys;

        // index register
        private ushort I;

        private ushort programCounter;

        private byte[,] graphics;
        
        private byte delayTimer;
        private byte soundTimer;

        private ushort[] stack;
        private ushort stackPointer;

        private byte[] keypad;

        private bool drawFlag;

        private Random random = new Random();

        public bool DrawFlag { get => drawFlag; set => drawFlag = value; }
        public byte[,] Graphics { get => graphics; set => graphics = value; }
        
        public void Init()
        {
            memory = new byte[4096];
            for (int i = 0; i < memory.Length; i++)
            {
                memory[i] = 0;
            }

            Array.Copy(FontSet.getFontSet(), 0, memory, 0, FontSet.getFontSet().Length);

            V = new byte[16];
            for (int i = 0; i < V.Length; i++)
            {
                V[i] = 0;
            }

            keys = new bool[16];
            for (int i = 0; i < keys.Length; i++)
            {
                keys[i] = false;
            }

            graphics = new byte[width, height];
            for (int w = 0; w < width; w++)
            {
                for (int h = 0; h < height; h++)
                {
                    graphics[w, h] = 0x0;
                }
            }

            stack = new ushort[16];
            for (int i = 0; i < stack.Length; i++)
            {
                stack[i] = 0;
            }

            keypad = new byte[16];
            for (int i = 0; i < keypad.Length; i++)
            {
                keypad[i] = 0;
            }

            drawFlag = false;
            programCounter = 0x200; // start at the address where the program is loaded into
            currentOpCode = null;
            stackPointer = 0;
            delayTimer = 0;
            soundTimer = 0;
        }

        public void LoadGame(string file)
        {
            byte[] buffer = System.IO.File.ReadAllBytes(file);

            // Read into memory starting at Program ROM location
            // 0x200 == 512

            Array.Copy(buffer, 0, memory, 0x200, buffer.Length);
        }

        public void KeyReleased(byte key)
        {
            keys[key] = false;
        }

        public void KeyPressed(byte key)
        {
            keys[key] = true;
        }

        public void Cycle()
        {
            // https://en.wikipedia.org/wiki/CHIP-8#Opcode_table
            // Shift opcode left 8 bits, then add the next opcode onto the end
            ushort opCode = (ushort)((memory[programCounter] << 8) | memory[programCounter + 1]);              

            currentOpCode = OpCode.CreateFromInstruction(opCode);
            
            // First four bits of an opcode will explain what it is
            switch (currentOpCode.FullOpCode & 0xF000)
            {
                case 0x0000:
                    Execute0x0000();
                    break;

                // 1NNN Jump to address NNN
                case 0x1000:
                    programCounter = currentOpCode.NNN;
                    break;

                // 2NNN Calls subroutine at NNN
                case 0x2000:
                    // push address to stack
                    stack[stackPointer] = programCounter;
                    stackPointer++;
                    programCounter = currentOpCode.NNN;
                    break;

                // 3XNN
                case 0x3000:
                    Execute3XNN();
                    break;

                // 4XNN
                case 0x4000:
                    Execute4XNN();
                    break;

                // 5YXY
                case 0x5000:
                    Execute5XY0();
                    break;

                // 6XNN Sets VX to NN
                case 0x6000:
                    V[currentOpCode.X] = currentOpCode.NN;
                    programCounter += 2;
                    break;

                // 7XNN Adds NN to VX
                case 0x7000:
                    V[currentOpCode.X] += currentOpCode.NN;
                    programCounter += 2;
                    break;

                case 0x8000:
                    Execute0x8000();
                    break;

                // 9XY0 Skips the next instruction if VX doesn't equal VY. (Usually the next instruction is a jump to skip a code block)
                case 0x9000:
                    if (V[currentOpCode.X] != V[currentOpCode.Y])
                    {
                        programCounter += 2;                        
                    }
                    programCounter += 2;
                    break;

                // ANNN Sets I to the address NNN
                case 0xA000:
                    I = currentOpCode.NNN;
                    programCounter += 2;
                    break;

                // CXNN Sets VX to the result of a bitwise and operation on a random number (Typically: 0 to 255) and NN
                case 0xC000:
                    V[currentOpCode.X] = (byte)(random.Next(0, 255) & currentOpCode.NN);
                    programCounter += 2;
                    break;

                // DXYN
                case 0xD000:
                    ExecuteDXYN();
                    break;

                case 0xE000:
                    Execute0xE000();
                    break;

                case 0xF000:
                    Execute0xF000();
                    break;

                default:
                    throw new ArgumentException("Invalid opcode: " + currentOpCode.FullOpCode.ToString("X"));
            }

            if (delayTimer > 0)
            {
                delayTimer--;
            }

            if (soundTimer > 0)
            {
                if (soundTimer == 1)
                {
                    // beep
                }
                soundTimer--;
            }
        }

        private void Execute0xE000()
        {
            switch (currentOpCode.FullOpCode & 0x00FF)
            {
                // EX9E Skips the next instruction if the key stored in VX isn't pressed. (Usually the next instruction is a jump to skip a code block)
                case 0x009E:
                    if (keys[V[currentOpCode.X]])
                    {
                        programCounter += 2;
                    }
                    programCounter += 2;
                    break;

                // EXA1 Skips the next instruction if the key stored in VX isn't pressed. (Usually the next instruction is a jump to skip a code block)
                case 0x00A1:
                    if (!keys[V[currentOpCode.X]])
                    {
                        programCounter += 2;
                    }
                    programCounter += 2;
                    break;

                default:
                    throw new ArgumentException("Invalid opcode [0xE000]: " + currentOpCode.FullOpCode.ToString("X"));
            }
        }

        private void Execute0x8000()
        {
            switch (currentOpCode.FullOpCode & 0x000F)
            {
                // 8XY0 Sets VX to the value of VY
                case 0x000:
                    V[currentOpCode.X] = V[currentOpCode.Y];
                    programCounter += 2;
                    break;

                // 8XY1 Sets VX to VX or VY. (Bitwise OR operation) VF is reset to 0
                case 0x0001:
                    V[currentOpCode.X] = (byte)(V[currentOpCode.X] | V[currentOpCode.Y]);
                    V[0xF] = 0;
                    programCounter += 2;
                    break;

                // 8XY2 Sets VX to VX and VY. (Bitwise AND operation) VF is reset to 0
                case 0x0002:
                    V[currentOpCode.X] = (byte)(V[currentOpCode.X] & V[currentOpCode.Y]);
                    V[0xF] = 0;
                    programCounter += 2;
                    break;

                // 8XY3 Sets VX to VX xor VY. VF is reset to 0.
                case 0x0003:
                    V[currentOpCode.X] = (byte)(V[currentOpCode.X] ^ V[currentOpCode.Y]);
                    V[0xF] = 0;
                    programCounter += 2;
                    break;

                // 8XY4 Adds VY to VX. VF is set to 1 when there's a carry, and to 0 when there isn't
                case 0x0004:
                    Execute8XY4();
                    break;

                // 8XY5 VY is subtracted from VX. VF is set to 0 when there's a borrow, and 1 when there isn't.
                case 0x0005:
                    if (V[currentOpCode.Y] > V[currentOpCode.X])
                    {
                        V[0xF] = 0; // borrow
                    }
                    else
                    {
                        V[0xF] = 1;
                    }

                    V[currentOpCode.X] -= V[currentOpCode.Y];
                    programCounter += 2;
                    break;

                // 8XY6 Shifts VX right by one. VF is set to the value of the least significant bit of VX before the shift
                case 0x0006:
                    V[0xF] = (byte)(V[currentOpCode.Y] | 0x1);
                    V[currentOpCode.Y] >>= 1;
                    programCounter += 2;
                    break;

                //8XY7 Sets VX to VY minus VX. VF is set to 0 when there's a borrow, and 1 when there isn't.
                case 0x0007:
                    if (V[currentOpCode.X] > V[currentOpCode.Y])
                    {
                        V[0xF] = 0; // borrow
                    }
                    else
                    {
                        V[0xF] = 1;
                    }

                    V[currentOpCode.X] = (byte)(V[currentOpCode.Y] - V[currentOpCode.X]);
                    programCounter += 2;
                    break;

                // 8XYE Shifts VX left by one. VF is set to the value of the most significant bit of VX before the shift.
                case 0x000E:
                    V[0xF] = (byte)(V[currentOpCode.X] >> 7);
                    V[currentOpCode.X] <<= 1;
                    programCounter += 2;
                    break;

                default:
                    throw new ArgumentException("Invalid opcode [0x8000]: " + currentOpCode.FullOpCode.ToString("X"));
            }
        }

        private void Execute0x0000()
        {
            switch (currentOpCode.FullOpCode & 0x00FF)
            {
                // 0x00E0 Clears the screen
                case 0x00E0:
                    for (int w = 0; w < width; w++)
                    {
                        for (int h = 0; h < height; h++)
                        {
                            graphics[w, h] = 0x0;
                        }
                    }

                    drawFlag = true;
                    programCounter += 2;
                    break;

                // 0x00EE Returns from subroutine 
                case 0x00EE:
                    Execute00EE();
                    break;
                default:
                    throw new ArgumentException("Invalid opcode [0x0000]: " + currentOpCode.FullOpCode.ToString("X"));
            }
        }

        private void Execute0xF000()
        {
            switch (currentOpCode.FullOpCode & 0x00FF)
            {
                // FX07 Sets VX to the value of the delay timer
                case 0x0007:
                    V[currentOpCode.X] = delayTimer;
                    programCounter += 2;
                    break;

                // FX0A A key press is awaited, and then stored in VX. (Blocking Operation. All instruction halted until next key event)
                case 0x000A:
                    for (int i = 0; i < keys.Length; i++)
                    {
                        if (keys[i])
                        {
                            programCounter += 2;
                        }
                    }
                    break;

                // FX15 Sets the delay timer to VX
                case 0x0015:
                    delayTimer = V[currentOpCode.X];
                    programCounter += 2;
                    break;

                // FX18 Sets the sound timer to VX
                case 0x0018:
                    soundTimer = V[currentOpCode.X];
                    programCounter += 2;
                    break;

                // FX1E Adds VX to I
                case 0x001E:
                    if (I + V[currentOpCode.X] > 0xFFF)
                    {
                        V[0xF] = 1;
                    }
                    else
                    {
                        V[0xF] = 0;
                    }
                    I += V[currentOpCode.X];
                    programCounter += 2;
                    break;

                // FX29 Sets I to the location of the sprite for the character in VX. Characters 0-F (in hexadecimal) are represented by a 4x5 font
                case 0x0029:
                    I = (ushort)(V[currentOpCode.X] * 0x5);
                    programCounter += 2;
                    break;

                // FX33 
                case 0x0033:
                    ExecuteFX33();
                    break;

                // FX55 Stores V0 to VX (including VX) in memory starting at address I
                case 0x0055:
                    for (int i = 0; i <= (currentOpCode.X); i++)
                    {
                        memory[I + i] = V[i];
                    }
                    programCounter += 2;
                    break;

                // FX65 Fills V0 to VX (including VX) with values from memory starting at address I
                case 0x0065:
                    for (int i = 0; i <= (currentOpCode.X); i++)
                    {
                        V[i] = memory[I + i];
                    }
                    programCounter += 2;
                    break;

                default:
                    throw new ArgumentException("Invalid opcode [0xF000]: " + currentOpCode.FullOpCode.ToString("X"));
            }
        }

        // 3XNN
        // Skips the next instruction if VX equals NN. (Usually the next instruction is a jump to skip a code block)
        private void Execute3XNN()
        {
            if (V[currentOpCode.X] == currentOpCode.NN)
            {
                programCounter += 2;
            }

            programCounter += 2;
        }

        // 4XNN
        // Skips the next instruction if VX doesn't equal NN. (Usually the next instruction is a jump to skip a code block)
        private void Execute4XNN()
        {
            if (V[currentOpCode.X] != currentOpCode.NN)
            {
                programCounter += 2;
            }

            programCounter += 2;
        }

        // 5XY0
        // Skips the next instruction if VX equals VY. (Usually the next instruction is a jump to skip a code block)
        private void Execute5XY0()
        {
            if (V[currentOpCode.X] == V[currentOpCode.Y])
            {
                programCounter += 2;
            }

            programCounter += 2;
        }

        // 00EE
        // Returns from a subroutine
        private void Execute00EE()
        {
            stackPointer--;
            programCounter = stack[stackPointer];
            programCounter += 2;
        }

        // 8XY4
        // Adds VY to VX. VF is set to 1 when there's a carry, and to 0 when there isn't
        private void Execute8XY4()
        {
            V[currentOpCode.X] += V[currentOpCode.Y];
            // check for carry
            if (V[currentOpCode.Y] > (0xFF - V[currentOpCode.X]))
            {
                V[0xF] = 1;
            }
            else
            {
                V[0xF] = 0;
            }

            programCounter += 2;
        }

        // DXYN
        // Draws a sprite at coordinate(VX, VY) that has a width of 8 pixels and a height of N pixels.
        // Each row of 8 pixels is read as bit-coded starting from memory location I; I value doesn’t change after the execution of this instruction. 
        // As described above, VF is set to 1 if any screen pixels are flipped from set to unset when the sprite is drawn, and to 0 if that doesn’t happen.
        // @todo this doesn't work
        private void ExecuteDXYN()
        {
            ushort startX = V[currentOpCode.X];
            ushort startY = V[(currentOpCode.FullOpCode & 0x00F0) >> 4];
            ushort drawHeight = currentOpCode.N;
            ushort pixels;

            V[0xF] = (byte)0x0;
            for (int yline = 0; yline < drawHeight; yline++)
            {
                pixels = memory[(I + yline)];
                for (int xline = 0; xline < 8; xline++)
                {
                    var x = (startX + xline) % width;
                    var y = (startY + yline) % height;
                    var spriteBit = ((pixels >> (7 - xline)) & 1);
                    var oldBit = graphics[x, y] == 1 ? 1 : 0;

                    if (oldBit != spriteBit)
                    {
                        drawFlag = true;
                    }

                    var newBit = oldBit ^ spriteBit;

                    if (newBit != 0)
                    {
                        graphics[x, y] = 1;
                    }
                    else
                    {
                        graphics[x, y] = 0;
                    }

                    if (oldBit != 0 && newBit == 0)
                    {
                        // collision
                        V[0xF] = 1;
                    }
                }
            }
            
            programCounter += 2;
        }

        // FX33
        // Stores the binary-coded decimal representation of VX, with the most significant of three digits at the address in I, the middle digit at I plus 1, and the least significant digit at I plus 2.
        // (In other words, take the decimal representation of VX, place the hundreds digit in memory at location in I, the tens digit at location I+1, and the ones digit at location I+2.)
        private void ExecuteFX33()
        {
            memory[I] = (byte)(V[currentOpCode.X] / 100);
            memory[I + 1] = (byte)((V[currentOpCode.X] / 10) % 10);
            memory[I + 2] = (byte)((V[currentOpCode.X] % 100) % 10);
            programCounter += 2;
        }

    }
}
