using System;
using System.Text;
using System.IO;

namespace WebAssemblyDotNET
{
    namespace Components
    {
        // https://webassembly.github.io/spec/core/binary/modules.html#binary-export
        public class ExportEntry : WebAssemblyComponent
        {
            public readonly string field_str;
            public readonly WebAssemblyExternalKind kind;
            public readonly uint index;

            public ExportEntry(string field_str, WebAssemblyExternalKind kind, uint index)
            {
                this.field_str = field_str ?? throw new ArgumentException(nameof(field_str));
                this.index = index;
                if (!Enum.IsDefined(typeof(WebAssemblyExternalKind), kind)) throw new ArgumentException(nameof(kind));
                this.kind = kind;
            }

            public ExportEntry(BinaryReader reader)
            {
                uint field_len = LEB128.ReadUInt32(reader);
                if (field_len > int.MaxValue) throw new NotImplementedException($"String longer than {int.MaxValue} bytes not supported.");
                field_str = Encoding.UTF8.GetString(reader.ReadBytes((int)field_len));

                var tmp_kind = reader.ReadByte();

                if (!Enum.IsDefined(typeof(WebAssemblyExternalKind), tmp_kind)) throw new Exception($"File is invalid. Expected byte 0, 1, 2, or 3, received '{tmp_kind}'.");

                kind = (WebAssemblyExternalKind)tmp_kind;

                index = LEB128.ReadUInt32(reader);
            }

            public override void Save(BinaryWriter writer)
            {
                var field = Encoding.UTF8.GetBytes(field_str);

                LEB128.WriteUInt32(writer, (uint)field.Length);
                writer.Write(field);
                writer.Write((byte)kind);
                LEB128.WriteUInt32(writer, index);
            }

            public override uint SizeOf()
            {
                var field = Encoding.UTF8.GetByteCount(field_str);

                return LEB128.SizeOf((uint)field) + (uint)field + sizeof(byte) + LEB128.SizeOf(index);
            }

            public override string ToString()
            {
                return $"({GetType().Name} (field {field_str}) (kind {kind}) (index {index}))";
            }
        }
    }
}