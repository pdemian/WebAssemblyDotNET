using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WebAssemblyDotNET.Components;
using System.Runtime.InteropServices;

namespace WebAssemblyDotNET
{
    internal static class WebAssemblyHelper
    {
        [StructLayout(LayoutKind.Explicit)]
        internal struct ReinterpretHelper
        {
            [FieldOffset(0)] public sbyte i8;
            [FieldOffset(0)] public short i16;
            [FieldOffset(0)] public int i32;
            [FieldOffset(0)] public long i64;
            [FieldOffset(0)] public byte ui8;
            [FieldOffset(0)] public ushort ui16;
            [FieldOffset(0)] public uint ui32;
            [FieldOffset(0)] public ulong ui64;
            [FieldOffset(0)] public float f32;
            [FieldOffset(0)] public double f64;
        }

        internal static byte popcount(ulong value)
        {
            ulong result = value - ((value >> 1) & 0x5555555555555555UL);
            result = (result & 0x3333333333333333UL) + ((result >> 2) & 0x3333333333333333UL);
            return (byte)(unchecked(((result + (result >> 4)) & 0xF0F0F0F0F0F0F0FUL) * 0x101010101010101UL) >> 56);
        }

        internal static byte popcount(uint value)
        {
            uint result = value - ((value >> 1) & 0x55555555U);
            result = (result & 0x33333333U) + ((result >> 2) & 0x33333333U);
            return (byte)(unchecked(((result + (result >> 4)) & 0x0F0F0F0FU) * 0x01010101U) >> 24);
        }

        internal static bool IsValueType(WebAssemblyType type)
        {
            switch (type)
            {
                case WebAssemblyType.i32:
                case WebAssemblyType.i64:
                case WebAssemblyType.f32:
                case WebAssemblyType.f64:
                    return true;
                default:
                    return false;
            }
        }

        internal static WebAssemblyType GetInitExprType(InitExpr init)
        {
            if (init.expr.Length < 1) throw new Exception("Unexpected init expression.");

            switch (init.expr[0])
            {
                case (byte)WebAssemblyOpcode.I32_CONST:
                    return WebAssemblyType.i32;
                case (byte)WebAssemblyOpcode.I64_CONST:
                    return WebAssemblyType.i64;
                case (byte)WebAssemblyOpcode.F32_CONST:
                    return WebAssemblyType.f32;
                case (byte)WebAssemblyOpcode.F64_CONST:
                    return WebAssemblyType.f64;
                default:
                    throw new Exception("Invalid init expression. Expected only simple constant load instruction.");
            }
        }

        internal static uint GetOffset(InitExpr init)
        {
            if (init.expr.Length < 3 || init.expr.Length > 6 || init.expr[0] != (byte)WebAssemblyOpcode.I32_CONST || init.expr[init.expr.Length - 1] != (byte)WebAssemblyOpcode.END) throw new Exception("Unexpected init expression.");

            uint value = 0;
            for (int i = init.expr.Length - 2; i > 0; i--)
            {
                value = value << 8 | init.expr[i];
            }

            return value;
        }

        internal static ValueObject GetInitExpr(InitExpr init, List<GlobalInstance> initialized_globals)
        {
            if (init.expr.Length < 3) throw new Exception("Unexpected init expression.");

            uint pc = 0;

            ValueObject ret = null;

            switch (init.expr[0])
            {
                case (byte)WebAssemblyOpcode.I32_CONST:
                    ret = new ValueObject(LEB128.ReadUInt32(init.expr, ref pc));
                    break;
                case (byte)WebAssemblyOpcode.I64_CONST:
                    ret = new ValueObject(LEB128.ReadUInt64(init.expr, ref pc));
                    break;
                case (byte)WebAssemblyOpcode.F32_CONST:
                    ret = new ValueObject(BitConverter.ToSingle(init.expr, (int)pc));
                    break;
                case (byte)WebAssemblyOpcode.F64_CONST:
                    ret = new ValueObject(BitConverter.ToDouble(init.expr, (int)pc));
                    break;
                case (byte)WebAssemblyOpcode.GLOBAL_GET:
                    GlobalInstance gi = initialized_globals[LEB128.ReadInt32(init.expr, ref pc)];
                    if (!gi.is_mutable) throw new Exception("Unexpected init expression.");
                    ret = gi.value;
                    break;
                default:
                    throw new Exception("Invalid init expression. Expected only simple constant load instruction or global get.");
            }

            pc++;

            if (init.expr[pc] != (byte)WebAssemblyOpcode.END || init.expr.Length > pc)
            {
                throw new Exception("Invalid init expression.");
            }

            return ret;
        }
    }
}
