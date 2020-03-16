using System;
using System.Linq;
using System.Text;
using System.IO;
using WebAssemblyDotNET.Components;

namespace WebAssemblyDotNET
{
    namespace Sections
    {
        // https://webassembly.github.io/spec/core/binary/modules.html#custom-section
        public class CustomSection : WebAssemblySection
        {
            public readonly string name;
            public readonly byte[] payload_data;

            public CustomSection(BinaryReader reader) : base(reader)
            {
                uint name_len = LEB128.ReadUInt32(reader);
                name = Encoding.UTF8.GetString(reader.ReadBytes((int)name_len));

                uint payload_size = payload_len - (LEB128.SizeOf(name_len) + name_len);
                if (payload_size > int.MaxValue) throw new NotImplementedException($"Payload longer than {int.MaxValue} bytes not supported.");

                payload_data = reader.ReadBytes((int)payload_size);
            }

            public CustomSection(string name, byte[] payload_data) : base(WebAssemblyModuleID.Custom)
            {
                if(payload_data.LongLength > int.MaxValue) throw new NotImplementedException($"Payload longer than {int.MaxValue} bytes not supported.");

                this.name = name;
                this.payload_data = payload_data;

                uint name_bytes = (uint)Encoding.UTF8.GetByteCount(name);
                payload_len = LEB128.SizeOf(name_bytes) + name_bytes + (uint)payload_data.Length;
            }

            public override void Save(BinaryWriter writer)
            {
                base.Save(writer);

                byte[] name_bytes = Encoding.UTF8.GetBytes(name);

                LEB128.WriteUInt32(writer, (uint)name_bytes.Length);
                writer.Write(name_bytes);
                writer.Write(payload_data);
            }

            public override uint SizeOf()
            {
                int str_size = Encoding.UTF8.GetByteCount(name);

                return base.SizeOf() + LEB128.SizeOf(str_size) + (uint)str_size + (uint)payload_data.Length;
            }
        }

        // https://webassembly.github.io/spec/core/binary/modules.html#type-section
        public class TypeSection : WebAssemblySection
        {
            public readonly FuncType[] entries;

            public TypeSection(FuncType[] entries) : base(WebAssemblyModuleID.Type)
            {
                this.entries = entries ?? throw new ArgumentException(nameof(entries));
                payload_len = SizeOf() - base.SizeOf();
            }

            public TypeSection(BinaryReader reader) : base(reader)
            {
                uint count = LEB128.ReadUInt32(reader);
                if(count > int.MaxValue) throw new NotImplementedException($"Count larger than {int.MaxValue} bytes not supported.");
                entries = new FuncType[count];

                for (uint i = 0; i < count; i++)
                {
                    entries[i] = new FuncType(reader);
                }
            }

            public override void Save(BinaryWriter writer)
            {
                base.Save(writer);

                LEB128.WriteUInt32(writer, (uint)entries.Length);
                foreach (FuncType entry in entries)
                {
                    entry.Save(writer);
                }
            }

            public override uint SizeOf()
            {
                return base.SizeOf() + LEB128.SizeOf((uint)entries.Length) + (uint)entries.Select(x => (long)x.SizeOf()).Sum();
            }
        }

        // https://webassembly.github.io/spec/core/binary/modules.html#import-section
        public class ImportSection : WebAssemblySection
        {
            public readonly ImportEntry[] entries;

            public ImportSection(ImportEntry[] entries) : base(WebAssemblyModuleID.Import)
            {
                this.entries = entries ?? throw new ArgumentException(nameof(entries));
                payload_len = SizeOf() - base.SizeOf();
            }

            public ImportSection(BinaryReader reader) : base(reader)
            {
                uint count = LEB128.ReadUInt32(reader);
                if (count > int.MaxValue) throw new NotImplementedException($"Count larger than {int.MaxValue} bytes not supported.");
                entries = new ImportEntry[count];

                for (uint i = 0; i < count; i++)
                {
                    entries[i] = new ImportEntry(reader);
                }
            }

            public override void Save(BinaryWriter writer)
            {
                base.Save(writer);
                foreach (var entry in entries)
                {
                    entry.Save(writer);
                }
            }

            public override uint SizeOf()
            {
                return base.SizeOf() + (uint)entries.Select(x => (long)x.SizeOf()).Sum() + LEB128.SizeOf((uint)entries.Length);
            }
        }

        // https://webassembly.github.io/spec/core/binary/modules.html#binary-funcsec
        public class FunctionSection : WebAssemblySection
        {
            public readonly uint[] types;

            public FunctionSection(uint[] types) : base(WebAssemblyModuleID.Function)
            {
                this.types = types ?? throw new ArgumentException(nameof(types));
                payload_len = SizeOf() - base.SizeOf();
            }

            public FunctionSection(BinaryReader reader) : base(reader)
            {
                uint count = LEB128.ReadUInt32(reader);
                if (count > int.MaxValue) throw new NotImplementedException($"Count larger than {int.MaxValue} bytes not supported.");
                types = new uint[count];

                for (uint i = 0; i < count; i++)
                {
                    types[i] = LEB128.ReadUInt32(reader);
                }
            }

            public override void Save(BinaryWriter writer)
            {
                base.Save(writer);
                foreach (var entry in types)
                {
                    LEB128.WriteUInt32(writer, entry);
                }
            }

            public override uint SizeOf()
            {
                return base.SizeOf() + (uint)types.Select(x => (long)LEB128.SizeOf(x)).Sum() + LEB128.SizeOf((uint)types.Length);
            }
        }

        // https://webassembly.github.io/spec/core/binary/modules.html#table-section
        public class TableSection : WebAssemblySection
        {
            public readonly TableType[] entries;

            public TableSection(TableType[] entries) : base(WebAssemblyModuleID.Table)
            {
                this.entries = entries ?? throw new ArgumentException(nameof(entries));
                payload_len = SizeOf() - base.SizeOf();
            }

            public TableSection(BinaryReader reader) : base(reader)
            {
                uint count = LEB128.ReadUInt32(reader);
                if (count > int.MaxValue) throw new NotImplementedException($"Count larger than {int.MaxValue} bytes not supported.");
                entries = new TableType[count];

                for (uint i = 0; i < count; i++)
                {
                    entries[i] = new TableType(reader);
                }
            }

            public override void Save(BinaryWriter writer)
            {
                base.Save(writer);
                foreach (var entry in entries)
                {
                    entry.Save(writer);
                }
            }

            public override uint SizeOf()
            {
                return base.SizeOf() + (uint)entries.Select(x => (long)x.SizeOf()).Sum() + LEB128.SizeOf((uint)entries.Length);
            }
        }

        // https://webassembly.github.io/spec/core/binary/modules.html#memory-section
        public class MemorySection : WebAssemblySection
        {
            public readonly MemoryType[] entries;

            public MemorySection(MemoryType[] entries) : base(WebAssemblyModuleID.Memory)
            {
                this.entries = entries ?? throw new ArgumentException(nameof(entries));
                payload_len = SizeOf() - base.SizeOf();
            }

            public MemorySection(BinaryReader reader) : base(reader)
            {
                uint count = LEB128.ReadUInt32(reader);
                if (count > int.MaxValue) throw new NotImplementedException($"Count larger than {int.MaxValue} bytes not supported.");
                entries = new MemoryType[count];

                for (uint i = 0; i < count; i++)
                {
                    entries[i] = new MemoryType(reader);
                }
            }

            public override void Save(BinaryWriter writer)
            {
                base.Save(writer);
                foreach (var entry in entries)
                {
                    entry.Save(writer);
                }
            }

            public override uint SizeOf()
            {
                return base.SizeOf() + (uint)entries.Select(x => (long)x.SizeOf()).Sum() + LEB128.SizeOf((uint)entries.Length);
            }
        }

        // https://webassembly.github.io/spec/core/binary/modules.html#global-section
        public class GlobalSection : WebAssemblySection
        {
            public readonly GlobalVariable[] globals;

            public GlobalSection(GlobalVariable[] globals) : base(WebAssemblyModuleID.Global)
            {
                this.globals = globals ?? throw new ArgumentException(nameof(globals));
                payload_len = SizeOf() - base.SizeOf();
            }

            public GlobalSection(BinaryReader reader) : base(reader)
            {
                uint count = LEB128.ReadUInt32(reader);
                if (count > int.MaxValue) throw new NotImplementedException($"Count larger than {int.MaxValue} bytes not supported.");
                globals = new GlobalVariable[count];

                for (uint i = 0; i < count; i++)
                {
                    globals[i] = new GlobalVariable(reader);
                }
            }

            public override void Save(BinaryWriter writer)
            {
                base.Save(writer);
                foreach (var entry in globals)
                {
                    entry.Save(writer);
                }
            }

            public override uint SizeOf()
            {
                return base.SizeOf() + (uint)globals.Select(x => (long)x.SizeOf()).Sum() + LEB128.SizeOf((uint)globals.Length);
            }
        }

        // https://webassembly.github.io/spec/core/binary/modules.html#export-section
        public class ExportSection : WebAssemblySection
        {
            public readonly ExportEntry[] entries;

            public ExportSection(ExportEntry[] entries) : base(WebAssemblyModuleID.Export)
            {
                this.entries = entries ?? throw new ArgumentException(nameof(entries));
                payload_len = SizeOf() - base.SizeOf();
            }

            public ExportSection(BinaryReader reader) : base(reader)
            {
                uint count = LEB128.ReadUInt32(reader);
                if (count > int.MaxValue) throw new NotImplementedException($"Count larger than {int.MaxValue} bytes not supported.");
                entries = new ExportEntry[count];

                for (uint i = 0; i < count; i++)
                {
                    entries[i] = new ExportEntry(reader);
                }
            }

            public override void Save(BinaryWriter writer)
            {
                base.Save(writer);
                foreach (var entry in entries)
                {
                    entry.Save(writer);
                }
            }

            public override uint SizeOf()
            {
                return base.SizeOf() + (uint)entries.Select(x => (long)x.SizeOf()).Sum() + LEB128.SizeOf((uint)entries.Length);
            }
        }

        // https://webassembly.github.io/spec/core/binary/modules.html#start-section
        public class StartSection : WebAssemblySection
        {
            public readonly uint index;

            public StartSection(uint index) : base(WebAssemblyModuleID.Start)
            {
                this.index = index;
                payload_len = LEB128.SizeOf(index);
            }

            public StartSection(BinaryReader reader) : base(reader)
            {
                index = LEB128.ReadUInt32(reader);
            }

            public override void Save(BinaryWriter writer)
            {
                base.Save(writer);
                LEB128.WriteUInt32(writer, index);
            }

            public override uint SizeOf()
            {
                return base.SizeOf() + LEB128.SizeOf(index);
            }
        }

        // https://webassembly.github.io/spec/core/binary/modules.html#element-section
        public class ElementSection : WebAssemblySection
        {
            public readonly ElementSegment[] entries;

            public ElementSection(ElementSegment[] entries) : base(WebAssemblyModuleID.Element)
            {
                this.entries = entries ?? throw new ArgumentException(nameof(entries));
                payload_len = SizeOf() - base.SizeOf();
            }

            public ElementSection(BinaryReader reader) : base(reader)
            {
                uint count = LEB128.ReadUInt32(reader);
                if (count > int.MaxValue) throw new NotImplementedException($"Count larger than {int.MaxValue} bytes not supported.");
                entries = new ElementSegment[count];

                for (uint i = 0; i < count; i++)
                {
                    entries[i] = new ElementSegment(reader);
                }
            }

            public override void Save(BinaryWriter writer)
            {
                base.Save(writer);
                foreach (var entry in entries)
                {
                    entry.Save(writer);
                }
            }

            public override uint SizeOf()
            {
                return base.SizeOf() + (uint)entries.Select(x => (long)x.SizeOf()).Sum() + LEB128.SizeOf((uint)entries.Length);
            }
        }

        // https://webassembly.github.io/spec/core/binary/modules.html#code-section
        public class CodeSection : WebAssemblySection
        {
            public readonly FunctionBody[] bodies;

            public CodeSection(FunctionBody[] bodies) : base(WebAssemblyModuleID.Code)
            {
                this.bodies = bodies ?? throw new ArgumentException(nameof(bodies));
                payload_len = SizeOf() - base.SizeOf();
            }

            public CodeSection(BinaryReader reader) : base(reader)
            {
                uint count = LEB128.ReadUInt32(reader);
                if (count > int.MaxValue) throw new NotImplementedException($"Count larger than {int.MaxValue} bytes not supported.");
                bodies = new FunctionBody[count];

                for (uint i = 0; i < count; i++)
                {
                    bodies[i] = new FunctionBody(reader);
                }
            }

            public override void Save(BinaryWriter writer)
            {
                base.Save(writer);
                foreach (var entry in bodies)
                {
                    entry.Save(writer);
                }
            }

            public override uint SizeOf()
            {
                return base.SizeOf() + (uint)bodies.Select(x => (long)x.SizeOf()).Sum() + LEB128.SizeOf((uint)bodies.Length);
            }
        }

        // https://webassembly.github.io/spec/core/binary/modules.html#data-section
        public class DataSection : WebAssemblySection
        {
            public readonly DataSegment[] entries;

            public DataSection(DataSegment[] entries) : base(WebAssemblyModuleID.Data)
            {
                this.entries = entries ?? throw new ArgumentException(nameof(entries));
                payload_len = SizeOf() - base.SizeOf();
            }

            public DataSection(BinaryReader reader) : base(reader)
            {
                uint count = LEB128.ReadUInt32(reader);
                if (count > int.MaxValue) throw new NotImplementedException($"Count larger than {int.MaxValue} bytes not supported.");
                entries = new DataSegment[count];

                for (uint i = 0; i < count; i++)
                {
                    entries[i] = new DataSegment(reader);
                }
            }

            public override void Save(BinaryWriter writer)
            {
                base.Save(writer);
                foreach (var entry in entries)
                {
                    entry.Save(writer);
                }
            }

            public override uint SizeOf()
            {
                return base.SizeOf() + (uint)entries.Select(x => (long)x.SizeOf()).Sum() + LEB128.SizeOf((uint)entries.Length);
            }
        }
    }
}