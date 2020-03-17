using System;
using System.Text;
using System.IO;

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


            public override string ToString()
            {
                return $"({GetType().Name} (name {name}))";
            }
        }
    }
}