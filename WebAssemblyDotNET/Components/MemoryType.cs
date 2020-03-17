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

            public override void Save(BinaryWriter writer)
            {
                limits.Save(writer);
            }

            public override uint SizeOf()
            {
                return limits.SizeOf();
            }

            public override string ToString()
            {
                return $"({GetType().Name} {limits})";
            }
        }
    }
}