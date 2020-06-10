using System;
using System.Text;
using System.IO;

namespace WebAssemblyDotNET
{
    namespace Components
    {
        // https://webassembly.github.io/spec/core/binary/modules.html#binary-import
        public class ImportEntry : WebAssemblyComponent
        {
            public readonly string module_str;
            public readonly string field_str;
            public readonly WebAssemblyExternalKind kind;
            public readonly object type;

            private ImportEntry(string module_str, string field_str, WebAssemblyExternalKind kind, object type)
            {
                this.module_str = module_str ?? throw new ArgumentException(nameof(module_str));
                this.field_str = field_str ?? throw new ArgumentException(nameof(field_str));
                this.kind = kind;
                this.type = type ?? throw new ArgumentException(nameof(type));
            }

            public ImportEntry(string module_str, string field_str, uint type) : this(module_str, field_str, WebAssemblyExternalKind.Function, type){}
            public ImportEntry(string module_str, string field_str, TableType type) : this(module_str, field_str, WebAssemblyExternalKind.Table, type){}
            public ImportEntry(string module_str, string field_str, MemoryType type) : this(module_str, field_str, WebAssemblyExternalKind.Memory, type){}
            public ImportEntry(string module_str, string field_str, GlobalType type) : this(module_str, field_str, WebAssemblyExternalKind.Global, type){}

            public ImportEntry(BinaryReader reader)
            {
                uint module_len = LEB128.ReadUInt32(reader);
                if (module_len > int.MaxValue) throw new NotImplementedException($"String longer than {int.MaxValue} bytes not supported.");
                module_str = Encoding.UTF8.GetString(reader.ReadBytes((int)module_len));

                uint field_len = LEB128.ReadUInt32(reader);
                if (field_len > int.MaxValue) throw new NotImplementedException($"String longer than {int.MaxValue} bytes not supported.");
                field_str = Encoding.UTF8.GetString(reader.ReadBytes((int)field_len));

                byte tmp_kind = reader.ReadByte();

                if (!Enum.IsDefined(typeof(WebAssemblyExternalKind), tmp_kind)) throw new Exception($"File is invalid. Expected 0, 1, 2, or 3, received '{tmp_kind}'.");

                kind = (WebAssemblyExternalKind)tmp_kind;

                switch (kind)
                {
                    case WebAssemblyExternalKind.Function:
                        type = LEB128.ReadUInt32(reader);
                        break;
                    case WebAssemblyExternalKind.Table:
                        var table_tmp = new TableType(reader);
                        type = table_tmp;
                        break;
                    case WebAssemblyExternalKind.Memory:
                        var memory_tmp = new MemoryType(reader);
                        type = memory_tmp;
                        break;
                    case WebAssemblyExternalKind.Global:
                        var global_tmp = new GlobalType(reader);
                        type = global_tmp;
                        break;
                }
            }

            internal override void SaveAsWASM(BinaryWriter writer)
            {
                var module = Encoding.UTF8.GetBytes(module_str);
                var field = Encoding.UTF8.GetBytes(field_str);

                LEB128.WriteUInt32(writer, (uint)module.Length);
                writer.Write(module);
                LEB128.WriteUInt32(writer, (uint)field.Length);
                writer.Write(field);
                writer.Write((byte)kind);

                switch (kind)
                {
                    case WebAssemblyExternalKind.Function:
                        LEB128.WriteUInt32(writer, (uint)type);
                        break;
                    case WebAssemblyExternalKind.Table:
                        ((TableType)type).SaveAsWASM(writer);
                        break;
                    case WebAssemblyExternalKind.Memory:
                        ((MemoryType)type).SaveAsWASM(writer);
                        break;
                    case WebAssemblyExternalKind.Global:
                        ((GlobalType)type).SaveAsWASM(writer);
                        break;
                }
            }

            internal override void SaveAsWAT(BinaryWriter writer)
            {
                writer.Write("(import \"");
                writer.Write(WebAssemblyHelper.EscapeString(module_str));
                writer.Write("\" \"");
                writer.Write(WebAssemblyHelper.EscapeString(field_str));
                writer.Write("\" (");

                switch(kind)
                {
                    case WebAssemblyExternalKind.Function:
                        writer.Write((uint)type);
                        break;
                    case WebAssemblyExternalKind.Table:
                        ((TableType)type).SaveAsWAT(writer);
                        break;
                    case WebAssemblyExternalKind.Memory:
                        ((MemoryType)type).SaveAsWAT(writer);
                        break;
                    case WebAssemblyExternalKind.Global:
                        ((GlobalType)type).SaveAsWAT(writer);
                        break;
                }

                writer.Write(')');
            }

            internal override uint BinarySize()
            {
                var module_len = Encoding.UTF8.GetByteCount(module_str);
                var field_len = Encoding.UTF8.GetByteCount(field_str);

                uint size = LEB128.SizeOf(module_len) + LEB128.SizeOf(field_len) + (uint)module_len + (uint)field_len + sizeof(byte);

                switch (kind)
                {
                    case WebAssemblyExternalKind.Function:
                        size += LEB128.SizeOf((uint)type);
                        break;
                    case WebAssemblyExternalKind.Table:
                        size += ((TableType)type).BinarySize();
                        break;
                    case WebAssemblyExternalKind.Memory:
                        size += ((MemoryType)type).BinarySize();
                        break;
                    case WebAssemblyExternalKind.Global:
                        size += ((GlobalType)type).BinarySize();
                        break;
                }

                return size;
            }

            public override string ToString()
            {
                return $"({GetType().Name} (module {module_str}) (field {field_str}) (kind {kind}) (type {type}))";
            }

        }
    }
}