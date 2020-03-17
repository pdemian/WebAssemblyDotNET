using System;
using System.IO;
using NLog;

namespace WebAssemblyDotNET
{
    public enum WebAssemblyModuleID : byte
    {
        Custom = 0,
        Type = 1,
        Import = 2,
        Function = 3,
        Table = 4,
        Memory = 5,
        Global = 6,
        Export = 7,
        Start = 8,
        Element = 9,
        Code = 10,
        Data = 11
    }

    public enum WebAssemblyExternalKind : byte
    {
        Function = 0,
        Table = 1,
        Memory = 2,
        Global = 3
    }

    public enum WebAssemblyType : byte
    {
        i32 = 0x7F,
        i64 = 0x7E,
        f32 = 0x7D,
        f64 = 0x7C,
        anyfunc = 0x70,
        func = 0x60,
        block = 0x40
    }

    public enum WebAssemblyOpcode : byte
    {
        // https://webassembly.github.io/spec/core/binary/instructions.html#control-instructions
        UNREACHABLE = 0x00,
        NOP = 0x01,
        BLOCK = 0x02, /* Not Implemented */
        LOOP = 0x03,
        IF = 0x04, /* Not Implemented */
        ELSE = 0x05, /* Not Implemented */
        BR = 0x0C,
        BR_IF = 0x0D,
        BR_TABLE = 0x0E, /* Not Implemented */
        RETURN = 0x0F, /* Not Implemented */
        END = 0x0B, /* Not Implemented */
        CALL = 0x10,
        CALL_INDIRECT = 0x11, /* Not Implemented */

        // https://webassembly.github.io/spec/core/binary/instructions.html#parametric-instructions
        DROP = 0x1A,
        SELECT = 0x1B,

        // https://webassembly.github.io/spec/core/binary/instructions.html#variable-instructions
        LOCAL_GET = 0x20,
        LOCAL_SET = 0x21,
        LOCAL_TEE = 0x22,
        GLOBAL_GET = 0x23, /* Not Implemented */
        GLOBAL_SET = 0x24, /* Not Implemented */

        // https://webassembly.github.io/spec/core/binary/instructions.html#memory-instructions
        I32_LOAD = 0x28,
        I64_LOAD = 0x29,
        F32_LOAD = 0x2A,
        F64_LOAD = 0x2B,
        I32_LOAD8_S = 0x2C,
        I32_LOAD8_U = 0x2D,
        I32_LOAD16_S = 0x2E,
        I32_LOAD16_U = 0x2F,
        I64_LOAD8_S = 0x30,
        I64_LOAD8_U = 0x31,
        I64_LOAD16_S = 0x32,
        I64_LOAD16_U = 0x33,
        I64_LOAD32_S = 0x34,
        I64_LOAD32_U = 0x35,
        I32_STORE = 0x36,
        I64_STORE = 0x37,
        F32_STORE = 0x38,
        F64_STORE = 0x39,
        I32_STORE8 = 0x3A,
        I32_STORE16 = 0x3B,
        I64_STORE8 = 0x3C,
        I64_STORE16 = 0x3D,
        I64_STORE32 = 0x3E,
        MEMORY_SIZE = 0x3F,
        MEMORY_GROW = 0x40,

        // https://webassembly.github.io/spec/core/binary/instructions.html#numeric-instructions
        I32_CONST = 0x41,
        I64_CONST = 0x42,
        F32_CONST = 0x43,
        F64_CONST = 0x44,

        /* Not implemented */
        I32_EQZ = 0x45,
        I32_EQ = 0x46,
        I32_NE = 0x47,
        I32_LT_S = 0x48,
        I32_LT_U = 0x49,
        I32_GT_S = 0x4A,
        I32_GT_U = 0x4B,
        I32_LE_S = 0x4C,
        I32_LE_U = 0x4D,
        I32_GE_S = 0x4E,
        I32_GE_U = 0x4F,
        I64_EQZ = 0x50,
        I64_EQ = 0x51,
        I64_NE = 0x52,
        I64_LT_S = 0x53,
        I64_LT_U = 0x54,
        I64_GT_S = 0x55,
        I64_GT_U = 0x56,
        I64_LE_S = 0x57,
        I64_LE_U = 0x58,
        I64_GE_S = 0x59,
        I64_GE_U = 0x5A,
        F32_EQ = 0x5B,
        F32_NE = 0x5C,
        F32_LT = 0x5D,
        F32_GT = 0x5E,
        F32_LE = 0x5F,
        F32_GE = 0x60,
        F64_EQ = 0x61,
        F64_NE = 0x62,
        F64_LT = 0x63,
        F64_GT = 0x64,
        F64_LE = 0x65,
        F64_GE = 0x66,
        I32_CLZ = 0x67,
        I32_CTZ = 0x68,
        I32_POPCNT = 0x69,
        I32_ADD = 0x6A,
        I32_SUB = 0x6B,
        I32_MUL = 0x6C,
        I32_DIV_S = 0x6D,
        I32_DIV_U = 0x6E,
        I32_REM_S = 0x6F,
        I32_REM_U = 0x70,
        I32_AND = 0x71,
        I32_OR = 0x72,
        I32_XOR = 0x73,
        I32_SHL = 0x74,
        I32_SHR_S = 0x75,
        I32_SHR_U = 0x76,
        I32_ROTL = 0x77,
        I32_ROTR = 0x78,
        I64_CLZ = 0x79,
        I64_CTZ = 0x7A,
        I64_POPCNT = 0x7B,
        I64_ADD = 0x7C,
        I64_SUB = 0x7D,
        I64_MUL = 0x7E,
        I64_DIV_S = 0x7F,
        I64_DIV_U = 0x80,
        I64_REM_S = 0x81,
        I64_REM_U = 0x82,
        I64_AND = 0x83,
        I64_OR = 0x84,
        I64_XOR = 0x85,
        I64_SHL = 0x86,
        I64_SHR_S = 0x87,
        I64_SHR_U = 0x88,
        I64_ROTL = 0x89,
        I64_ROTR = 0x8A,
        F32_ABS = 0x8B,
        F32_NEG = 0x8C,
        F32_CEIL = 0x8D,
        F32_FLOOR = 0x8E,
        F32_TRUNC = 0x8F,
        F32_NEAREST = 0x90,
        F32_SQRT = 0x91,
        F32_ADD = 0x92,
        F32_SUB = 0x93,
        F32_MUL = 0x94,
        F32_DIV = 0x95,
        F32_MIN = 0x96,
        F32_MAX = 0x97,
        F32_COPYSIGN = 0x98,
        F64_ABS = 0x99,
        F64_NEG = 0x9A,
        F64_CEIL = 0x9B,
        F64_FLOOR = 0x9C,
        F64_TRUNC = 0x9D,
        F64_NEAREST = 0x9E,
        F64_SQRT = 0x9F,
        F64_ADD = 0xA0,
        F64_SUB = 0xA1,
        F64_MUL = 0xA2,
        F64_DIV = 0xA3,
        F64_MIN = 0xA4,
        F64_MAX = 0xA5,
        F64_COPYSIGN = 0xA6,
        I32_WRAP_I64 = 0xA7,
        I32_TRUNC_F32_S = 0xA8,
        I32_TRUNC_F32_U = 0xA9,
        I32_TRUNC_F64_S = 0xAA,
        I32_TRUNC_F64_U = 0xAB,
        I64_EXTEND_I32_S = 0xAC,
        I64_EXTEND_I32_U = 0xAD,
        I64_TRUNC_F32_S = 0xAE,
        I64_TRUNC_F32_U = 0xAF,
        I64_TRUNC_F64_S = 0xB0,
        I64_TRUNC_F64_U = 0xB1,
        F32_CONVERT_I32_S = 0xB2,
        F32_CONVERT_I32_U = 0xB3,
        F32_CONVERT_I64_S = 0xB4,
        F32_CONVERT_I64_U = 0xB5,
        F32_DEMOTE_F64 = 0xB6,
        F64_CONVERT_I32_S = 0xB7,
        F64_CONVERT_I32_U = 0xB8,
        F64_CONVERT_I64_S = 0xB9,
        F64_CONVERT_I64_U = 0xBA,
        F64_PROMOTE_F32 = 0xBB,
        I32_REINTERPRET_F32 = 0xBC,
        I64_REINTERPRET_F64 = 0xBD,
        F32_REINTERPRET_I32 = 0xBE,
        F64_REINTERPRET_I64 = 0xBF,
    }

    public abstract class WebAssemblyComponent
    {
        private readonly Logger logger;

        public WebAssemblyComponent()
        {
            logger = LogManager.GetLogger(GetType().Name);
        }

        public abstract void Save(BinaryWriter writer);
        public abstract uint SizeOf();
        public abstract override string ToString();
    }

    public abstract class WebAssemblySection : WebAssemblyComponent
    {
        public readonly WebAssemblyModuleID module;
        public uint payload_len
        {
            get => data_size;
            protected set => data_size = value;
        }

        protected uint data_size;

        public WebAssemblySection(BinaryReader reader)
        {
            module = (WebAssemblyModuleID)LEB128.ReadUInt7(reader);
            data_size = LEB128.ReadUInt32(reader);
        }

        public WebAssemblySection(WebAssemblyModuleID module)
        {
            this.module = module;
        }

        public WebAssemblySection(WebAssemblyModuleID module, uint payload_len)
        {
            this.module = module;
            data_size = payload_len;
        }

        public override void Save(BinaryWriter writer)
        {
            LEB128.WriteUInt7(writer, (byte)module);
            LEB128.WriteUInt32(writer, payload_len);
        }

        public override uint SizeOf()
        {
            return sizeof(byte) + LEB128.SizeOf(payload_len);
        }
    }
}
