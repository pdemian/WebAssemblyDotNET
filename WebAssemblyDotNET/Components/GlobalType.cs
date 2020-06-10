using System;
using System.IO;

namespace WebAssemblyDotNET
{
    namespace Components
    {
        // https://webassembly.github.io/spec/core/binary/types.html#global-types
        public class GlobalType : WebAssemblyComponent
        {
            public readonly WebAssemblyType content_type;
            public readonly bool mutability;

            public GlobalType(WebAssemblyType content_type, bool mutability)
            {
                if (!WebAssemblyHelper.IsValueType(content_type)) throw new ArgumentException(nameof(content_type));

                this.content_type = content_type;
                this.mutability = mutability;
            }

            public GlobalType(BinaryReader reader)
            {
                content_type = (WebAssemblyType)LEB128.ReadUInt7(reader);

                if (!WebAssemblyHelper.IsValueType(content_type)) throw new Exception($"File is invalid. Expected value type, received '{content_type}'.");

                byte mut = LEB128.ReadUInt7(reader);

                if(mut != 0 && mut != 1) throw new Exception($"File is invalid. Expected 0 or 1, received '{mut}'.");

                mutability = mut != 0;
            }

            internal override void SaveAsWASM(BinaryWriter writer)
            {
                LEB128.WriteUInt7(writer, (byte)content_type);
                LEB128.WriteUInt7(writer, (byte)(mutability ? 1 : 0));
            }

            internal override void SaveAsWAT(BinaryWriter writer)
            {
                writer.Write('(');
                if(mutability)
                {
                    writer.Write("mut ");
                }
                writer.Write(content_type.ToString());
                writer.Write(')');
            }

            internal override uint BinarySize()
            {
                return sizeof(byte) + sizeof(byte);
            }

            public override string ToString()
            {
                return $"({GetType().Name} (mutability {mutability}) (content_type {content_type}))";
            }
        }
    }
}