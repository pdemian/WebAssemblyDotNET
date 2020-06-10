using System;
using System.IO;

namespace WebAssemblyDotNET
{
    namespace Components
    {
        // https://webassembly.github.io/spec/core/binary/types.html#limits
        public class ResizeLimit : WebAssemblyComponent
        {
            public readonly uint initial;
            public readonly uint? maximum;

            public ResizeLimit(uint initial, uint? maximum)
            {
                if (maximum != null && initial > maximum) throw new ArgumentException(nameof(maximum));

                this.initial = initial;
                this.maximum = maximum;
            }

            public ResizeLimit(BinaryReader reader)
            {
                byte flags = LEB128.ReadUInt7(reader);
                if(flags != 0 && flags != 1) throw new Exception($"File is invalid. Expected 0 or 1, received '{flags}'.");

                initial = LEB128.ReadUInt32(reader);
                if (flags != 0)
                {
                    maximum = LEB128.ReadUInt32(reader);
                    if (maximum < initial) throw new Exception($"File is invalid. Initial memory size greater than maximum.");
                }
            }

            internal override void SaveAsWASM(BinaryWriter writer)
            {
                LEB128.WriteUInt7(writer, (byte)(maximum != null ? 1 : 0));
                LEB128.WriteUInt32(writer, initial);
                if (maximum != null)
                {
                    LEB128.WriteUInt32(writer, (uint)maximum);
                }
            }

            internal override void SaveAsWAT(BinaryWriter writer)
            {
                writer.Write(initial);
                if(maximum != null)
                {
                    writer.Write(' ');
                    writer.Write((uint)maximum);
                }
            }

            internal override uint BinarySize()
            {
                return sizeof(byte) + LEB128.SizeOf(initial) + (maximum != null ? LEB128.SizeOf((uint)maximum) : 0);
            }

            public override string ToString()
            {
                return $"({GetType().Name} (initial {initial}) (maximum {maximum ?? 0}))";
            }
        }
    }
}