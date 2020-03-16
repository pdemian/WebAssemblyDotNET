using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Threading.Tasks;

namespace WebAssemblyDotNET
{
    public static class LEB128
    {
        public static uint SizeOf(uint value)
        {
            return (value > 0xFFFFFFF) ? 5u :
                   (value > 0x1FFFFF) ? 4u :
                   (value > 0x3FFF) ? 3u :
                   (value > 0x7F) ? 2u : 1u;
        }
        public static uint SizeOf(int value)
        {
            return SizeOf(value > 0 ? (uint)value : (uint)(-value));
        }
        public static uint SizeOf(byte value)
        {
            return sizeof(byte);
        }
        public static uint SizeOf(sbyte value)
        {
            return sizeof(sbyte);
        }

        public static void WriteUInt32(BinaryWriter writer, uint value)
        {
            for (uint count = 1; count < 6; count++)
            {
                byte b = (byte)(value & 0b01111111);
                value >>= 7;

                if (value != 0)
                    b |= 0b10000000;

                writer.Write(b);

                if (value == 0) break;
            }
        }
        public static void WriteUInt7(BinaryWriter writer, byte value)
        {
            writer.Write((byte)(value & 0b01111111));
        }

        public static void WriteInt32(BinaryWriter writer, int value)
        {
            bool negative = value < 0;

            for (uint count = 1; count < 6; count++)
            {
                byte b = (byte)(value & 0b01111111);
                value >>= 7;

                if (negative)
                    value |= (~0 << (32 - 7));

                if ((value == 0 && (b & 0b01000000) == 0) || (value == -1 && (b & 0b01000000) != 0))
                {
                    writer.Write(b);
                    break;
                }
                else
                {
                    b |= 0b10000000;
                    writer.Write(b);
                }
            }
        }
        public static void WriteInt7(BinaryWriter writer, sbyte value)
        {
            if (value < 0)
            {
                value |= ~0;
            }

            writer.Write((byte)value);
        }

        public static uint ReadUInt32(BinaryReader reader)
        {
            int shift = 0;
            uint result = 0;

            // Guaranteed to be 5 bytes or less. Doing this might make the compiler unroll the loop
            for (uint count = 1; count < 6; count++)
            {
                byte next = reader.ReadByte();
                result |= (next & 0b01111111u) << shift;
                if ((next & 0b10000000u) == 0)
                {
                    break;
                }

                shift += 7;
            }

            return result;
        }
        public static byte ReadUInt7(BinaryReader reader)
        {
            return reader.ReadByte();
        }

        public static int ReadInt32(BinaryReader reader)
        {
            int shift = 0;
            int result = 0;
            byte next = 0;

            for (uint count = 1; count < 6; count++)
            {
                next = reader.ReadByte();
                result |= (next & 0b01111111) << shift;
                shift += 7;

                if ((next & 0b10000000u) == 0)
                {
                    break;
                }
            }

            if ((shift < 32) && (next & 0b01000000) != 0)
                result |= (~0 << shift);

            return result;
        }
        public static sbyte ReadInt7(BinaryReader reader)
        {
            sbyte val = reader.ReadSByte();

            // if ((val & 0b01000000) != 0) val |= ~0;

            return val;
        }

        public static int ReadInt32(byte[] code, ref uint pc)
        {
            int shift = 0;
            int result = 0;
            byte next = 0;

            pc++;

            for (uint count = 1; count < 6; count++)
            {
                next = code[pc];
                result |= (next & 0b01111111) << shift;
                shift += 7;

                if ((next & 0b10000000u) == 0)
                {
                    break;
                }

                pc++;
            }

            if ((shift < 32) && (next & 0b01000000) != 0)
                result |= (~0 << shift);

            return result;
        }
        public static uint ReadUInt32(byte[] code, ref uint pc)
        {
            int shift = 0;
            uint result = 0;

            pc++;

            // Guaranteed to be 5 bytes or less. Doing this might make the compiler unroll the loop
            for (uint count = 1; count < 6; count++)
            {
                byte next = code[pc];
                result |= (next & 0b01111111u) << shift;
                if ((next & 0b10000000u) == 0)
                {
                    break;
                }
                pc++;
                shift += 7;
            }

            return result;
        }

        public static long ReadInt64(byte[] code, ref uint pc)
        {
            int shift = 0;
            long result = 0;
            byte next = 0;

            pc++;

            for (uint count = 1; count < 11; count++)
            {
                next = code[pc];
                result |= (next & 0b01111111L) << shift;
                shift += 7;

                if ((next & 0b10000000U) == 0)
                {
                    break;
                }

                pc++;
            }

            if ((shift < 64) && (next & 0b01000000U) != 0)
                result |= (~0L << shift);

            return result;
        }
        public static ulong ReadUInt64(byte[] code, ref uint pc)
        {
            int shift = 0;
            ulong result = 0;

            pc++;

            // Guaranteed to be 10 bytes or less. Doing this might make the compiler unroll the loop
            for (uint count = 1; count < 11; count++)
            {
                byte next = code[pc];
                result |= (next & 0b01111111UL) << shift;
                if ((next & 0b10000000U) == 0)
                {
                    break;
                }
                pc++;
                shift += 7;
            }

            return result;
        }
    }
}
