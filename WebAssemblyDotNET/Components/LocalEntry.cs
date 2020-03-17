using System;
using System.IO;

namespace WebAssemblyDotNET
{
    namespace Components
    {
        // https://webassembly.github.io/spec/core/binary/modules.html#binary-local
        public class LocalEntry : WebAssemblyComponent
        {
            public readonly uint count;
            public readonly WebAssemblyType type;

            public LocalEntry(uint count, WebAssemblyType type)
            {
                if (!Enum.IsDefined(typeof(WebAssemblyType), type)) throw new ArgumentException(nameof(type));

                this.count = count;
                this.type = type;
            }

            public LocalEntry(BinaryReader reader)
            {
                count = LEB128.ReadUInt32(reader);

                var tmp_type = reader.ReadByte();

                if (!Enum.IsDefined(typeof(WebAssemblyType), tmp_type)) throw new Exception($"File is invalid. Expected WebAssemblyType, received '{tmp_type}'.");

                type = (WebAssemblyType)tmp_type;
            }

            public override void Save(BinaryWriter writer)
            {
                LEB128.WriteUInt32(writer, count);
                writer.Write((byte)type);
            }

            public override uint SizeOf()
            {
                return LEB128.SizeOf(count) + sizeof(byte);
            }

            public override string ToString()
            {
                return $"({GetType().Name} (count {count}) (type {type}))";
            }
        }
    }
}