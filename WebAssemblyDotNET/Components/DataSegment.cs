using System;
using System.IO;

namespace WebAssemblyDotNET
{
    namespace Components
    {
        // https://webassembly.github.io/spec/core/binary/modules.html#binary-data
        public class DataSegment : WebAssemblyComponent
        {
            public readonly uint memory_index;
            public readonly InitExpr offset;
            public readonly byte[] data;

            public DataSegment(uint memory_index, InitExpr offset, byte[] data)
            {
                this.memory_index = memory_index;
                this.offset = offset ?? throw new ArgumentException(nameof(offset));
                this.data = data ?? throw new ArgumentException(nameof(data));
            }

            public DataSegment(BinaryReader reader)
            {
                memory_index = LEB128.ReadUInt32(reader);
                offset = new InitExpr(reader);
                uint count = LEB128.ReadUInt32(reader);
                if (count > int.MaxValue) throw new NotImplementedException($"Count larger than {int.MaxValue} bytes not supported.");
                data = reader.ReadBytes((int)count);
            }

            public override void Save(BinaryWriter writer)
            {
                LEB128.WriteUInt32(writer, memory_index);
                offset.Save(writer);
                LEB128.WriteUInt32(writer, (uint)data.Length);
                writer.Write(data);
            }

            public override uint SizeOf()
            {
                return LEB128.SizeOf(memory_index) + LEB128.SizeOf((uint)data.Length) + (uint)data.Length + offset.SizeOf();
            }

            public override string ToString()
            {
                return $"({GetType().Name} (memory_index {memory_index}) {offset} (data {BitConverter.ToString(data).Replace("-","")}))";
            }
        }
    }
}