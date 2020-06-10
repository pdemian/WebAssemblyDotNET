using System;
using System.IO;

namespace WebAssemblyDotNET
{
    namespace Components
    {
        // https://webassembly.github.io/spec/core/binary/types.html#memory-types
        public class MemoryType : WebAssemblyComponent
        {
            public readonly ResizeLimit limits;

            public MemoryType(ResizeLimit limits)
            {
                this.limits = limits ?? throw new ArgumentException(nameof(limits));
            }

            public MemoryType(BinaryReader reader)
            {
                limits = new ResizeLimit(reader);
            }

            internal override void SaveAsWASM(BinaryWriter writer)
            {
                limits.SaveAsWASM(writer);
            }

            internal override void SaveAsWAT(BinaryWriter writer)
            {
                limits.SaveAsWAT(writer);
            }

            internal override uint BinarySize()
            {
                return limits.BinarySize();
            }

            public override string ToString()
            {
                return $"({GetType().Name} {limits})";
            }
        }
    }
}