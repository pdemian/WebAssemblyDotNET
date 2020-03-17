using System;
using System.Linq;
using System.IO;

namespace WebAssemblyDotNET
{
    namespace Components
    {
        // https://webassembly.github.io/spec/core/binary/modules.html#binary-elemsec
        public class ElementSegment : WebAssemblyComponent
        {
            public readonly uint table_index;
            public readonly InitExpr offset;
            public readonly uint[] function_index;

            public ElementSegment(uint table_index, InitExpr offset, uint[] function_index)
            {
                this.table_index = table_index;
                this.offset = offset ?? throw new ArgumentException(nameof(offset));
                this.function_index = function_index ?? throw new ArgumentException(nameof(function_index));
            }

            public ElementSegment(BinaryReader reader)
            {
                table_index = LEB128.ReadUInt32(reader);
                offset = new InitExpr(reader);
                uint num_elem = LEB128.ReadUInt32(reader);

                function_index = new uint[num_elem];

                for (uint i = 0; i < num_elem; i++)
                {
                    function_index[i] = LEB128.ReadUInt32(reader);
                }
            }

            public override void Save(BinaryWriter writer)
            {
                LEB128.WriteUInt32(writer, table_index);
                offset.Save(writer);
                LEB128.WriteUInt32(writer, (uint)function_index.Length);
                foreach (var elem in function_index)
                {
                    LEB128.WriteUInt32(writer, elem);
                }
            }

            public override uint SizeOf()
            {
                return LEB128.SizeOf(table_index) + offset.SizeOf() + (uint)function_index.Length + (uint)function_index.Select(x => (long)LEB128.SizeOf(x)).Sum();
            }

            public override string ToString()
            {
                return $"({GetType().Name} (table_index {table_index}) {offset} (function_index {WebAssemblyHelper.ToString(function_index)}))";
            }
        }
    }
}