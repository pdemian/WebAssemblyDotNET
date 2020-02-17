using System;
using System.IO;
using WebAssemblyDotNET.Sections;
using WebAssemblyDotNET.Components;

namespace WebAssemblyDotNET
{
    internal static class WebAssemblyHelper
    {
        internal static bool IsValueType(WASMType type)
        {
            switch (type)
            {
                case WASMType.i32:
                case WASMType.i64:
                case WASMType.f32:
                case WASMType.f64:
                    return true;
                default:
                    return false;
            }
        }

        internal static WASMType GetInitExprType(InitExpr init)
        {
            if (init.expr.Length < 1) throw new Exception("Unexpected init expression.");

            switch(init.expr[0])
            {
                case (byte)WASMOpcodes.I32_CONST:
                    return WASMType.i32;
                case (byte)WASMOpcodes.I64_CONST:
                    return WASMType.i64;
                case (byte)WASMOpcodes.F32_CONST:
                    return WASMType.f32;
                case (byte)WASMOpcodes.F64_CONST:
                    return WASMType.f64;
                default:
                    throw new Exception("Invalid init expression. Expected only simple constant load instruction.");
            }
        }

        internal static uint GetOffset(InitExpr init)
        {
            if (init.expr.Length < 3 || init.expr.Length > 6 || init.expr[0] != (byte)WASMOpcodes.I32_CONST || init.expr[init.expr.Length - 1] != (byte)WASMOpcodes.END) throw new Exception("Unexpected init expression.");

            uint value = 0;
            for (int i = init.expr.Length - 2; i > 0; i--)
            {
                value = value << 8 | init.expr[i];
            }

            return value;
        }
    }

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
    }

    public enum WASMModuleID : byte
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

    public enum WASMExternalKind : byte
    {
        Function = 0,
        Table = 1,
        Memory = 2,
        Global = 3
    }

    public enum WASMType : byte
    {
        i32 = 0x7F,
        i64 = 0x7E,
        f32 = 0x7D,
        f64 = 0x7C,
        anyfunc = 0x70,
        func = 0x60,
        block = 0x40
    }

    public abstract class WebAssemblyComponent
    {
        public abstract void Save(BinaryWriter writer);
        public abstract uint SizeOf();
    }

    public abstract class WASMSection : WebAssemblyComponent
    {
        public readonly WASMModuleID module;
        public uint payload_len
        {
            get => data_size;
            protected set => data_size = value;
        }

        protected uint data_size;

        public WASMSection(BinaryReader reader)
        {
            module = (WASMModuleID)LEB128.ReadUInt7(reader);
            data_size = LEB128.ReadUInt32(reader);
        }

        public WASMSection(WASMModuleID module)
        {
            this.module = module;
        }

        public WASMSection(WASMModuleID module, uint payload_len)
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

    public enum WASMOpcodes : byte
    {
        UNREACHABLE = 0x00,
        NOP = 0x01,
        END = 0x0B,
        CALL = 0x10,
        DROP = 0x1A,
        I32_CONST = 0x41,
        I64_CONST = 0x42,
        F32_CONST = 0x43,
        F64_CONST = 0x44,
    }

    // https://webassembly.github.io/spec/core/binary/modules.html#binary-module
    public class WASMFile
    {
        private const uint MAGIC = 0x6D736100;
        private const uint SUPPORTED_VERSION = 1;

        public CustomSection custom = null;
        public TypeSection type = null;
        public ImportSection import = null;
        public FunctionSection function = null;
        public TableSection table = null;
        public MemorySection memory = null;
        public GlobalSection global = null;
        public ExportSection export = null;
        public StartSection start = null;
        public ElementSection element = null;
        public CodeSection code = null;
        public DataSection data = null;

        public WASMFile() { }

        // strict parse means all sections must come in order
        public void Parse(string filename, bool strict_parse = true)
        {
            if (!BitConverter.IsLittleEndian) throw new NotImplementedException("LEB128 implementation only handles little endian systems");

            using (FileStream fs = new FileStream(filename, FileMode.Open, FileAccess.Read))
            {
                using (BinaryReader reader = new BinaryReader(fs))
                {
                    try
                    {
                        uint magic = reader.ReadUInt32();
                        if (magic != MAGIC) throw new Exception("Not a compiled Web Assembly file.");

                        uint version = reader.ReadUInt32();
                        if (version > SUPPORTED_VERSION) throw new Exception($"Unsupported version. Expected version <= {SUPPORTED_VERSION}, received {version}.");

                        int last_parsed_module = int.MinValue;

                        /* Read in each module */

                        while (true)
                        {
                            int id = reader.PeekChar();

                            // EOF
                            if (id == -1) break;

                            if (strict_parse && id < last_parsed_module) throw new Exception("File contains out of order sections.");
                            last_parsed_module = id;

                            switch (id)
                            {
                                case (int)WASMModuleID.Custom:
                                    if (strict_parse && custom != null) throw new Exception("File contains a duplicate custom section.");
                                    custom = new CustomSection(reader);
                                    break;
                                case (int)WASMModuleID.Type:
                                    if (strict_parse && type != null) throw new Exception("File contains a duplicate type section.");
                                    type = new TypeSection(reader);
                                    break;
                                case (int)WASMModuleID.Import:
                                    if (strict_parse && import != null) throw new Exception("File contains a duplicate import section.");
                                    import = new ImportSection(reader);
                                    break;
                                case (int)WASMModuleID.Function:
                                    if (strict_parse && function != null) throw new Exception("File contains a duplicate function section.");
                                    function = new FunctionSection(reader);
                                    break;
                                case (int)WASMModuleID.Table:
                                    if (strict_parse && table != null) throw new Exception("File contains a duplicate table section.");
                                    table = new TableSection(reader);
                                    break;
                                case (int)WASMModuleID.Memory:
                                    if (strict_parse && memory != null) throw new Exception("File contains a duplicate memory section.");
                                    memory = new MemorySection(reader);
                                    break;
                                case (int)WASMModuleID.Global:
                                    if (strict_parse && global != null) throw new Exception("File contains a duplicate global section.");
                                    global = new GlobalSection(reader);
                                    break;
                                case (int)WASMModuleID.Export:
                                    if (strict_parse && export != null) throw new Exception("File contains a duplicate export section.");
                                    export = new ExportSection(reader);
                                    break;
                                case (int)WASMModuleID.Start:
                                    if (strict_parse && start != null) throw new Exception("File contains a duplicate start section.");
                                    start = new StartSection(reader);
                                    break;
                                case (int)WASMModuleID.Element:
                                    if (strict_parse && element != null) throw new Exception("File contains a duplicate element section.");
                                    element = new ElementSection(reader);
                                    break;
                                case (int)WASMModuleID.Code:
                                    if (strict_parse && code != null) throw new Exception("File contains a duplicate code section.");
                                    code = new CodeSection(reader);
                                    break;
                                case (int)WASMModuleID.Data:
                                    if (strict_parse && data != null) throw new Exception("File contains a duplicate data section.");
                                    data = new DataSection(reader);
                                    break;

                                // Error
                                default:
                                    throw new Exception($"Unknown section {id}.");
                            }
                        }

                        /* Additional validation */

                        // The lengths of vectors produced by the (possibly empty) function and code section must match up.
                        if ((function != null && code == null) || (function == null && code != null)) throw new Exception("File corrupt. Must include both function and code sections.");
                        if (function.types.Length != code.bodies.Length) throw new Exception("File corrupt. Function and code sections do not match up.");


                        // TODO: Validate everything in this list
                        // https://webassembly.github.io/spec/core/valid/modules.html
                    }
                    catch (Exception)
                    {
                        // for (var b = fs.ReadByte(); b != -1; b = fs.ReadByte())
                        // {
                        //     Console.Write(" 0x" + b.ToString("X"));
                        // }
                        throw;
                    }
                }
            }
        }

        public void Save(string filename)
        {
            using (FileStream fs = new FileStream(filename, FileMode.Create, FileAccess.Write))
            {
                using (BinaryWriter writer = new BinaryWriter(fs))
                {
                    writer.Write(MAGIC);
                    writer.Write(SUPPORTED_VERSION);

                    custom?.Save(writer);
                    type?.Save(writer);
                    import?.Save(writer);
                    function?.Save(writer);
                    table?.Save(writer);
                    memory?.Save(writer);
                    global?.Save(writer);
                    export?.Save(writer);
                    start?.Save(writer);
                    element?.Save(writer);
                    code?.Save(writer);
                    data?.Save(writer);
                }
            }
        }

        public uint SizeOf()
        {
            return sizeof(uint) +
                   sizeof(uint) +
                   custom?.SizeOf() ?? 0 +
                   type?.SizeOf() ?? 0 +
                   import?.SizeOf() ?? 0 +
                   function?.SizeOf() ?? 0 +
                   table?.SizeOf() ?? 0 +
                   memory?.SizeOf() ?? 0 +
                   global?.SizeOf() ?? 0 +
                   export?.SizeOf() ?? 0 +
                   start?.SizeOf() ?? 0 +
                   element?.SizeOf() ?? 0 +
                   code?.SizeOf() ?? 0 +
                   data?.SizeOf() ?? 0;
        }
    }
}
