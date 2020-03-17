using System.IO;

namespace WebAssemblyDotNET
{
    namespace Sections
    {
        // https://webassembly.github.io/spec/core/binary/modules.html#start-section
        public class StartSection : WebAssemblySection
        {
            public readonly uint index;

            public StartSection(uint index) : base(WebAssemblyModuleID.Start)
            {
                this.index = index;
                payload_len = LEB128.SizeOf(index);
            }

            public StartSection(BinaryReader reader) : base(reader)
            {
                index = LEB128.ReadUInt32(reader);
            }

            public override void Save(BinaryWriter writer)
            {
                base.Save(writer);
                LEB128.WriteUInt32(writer, index);
            }

            public override uint SizeOf()
            {
                return base.SizeOf() + LEB128.SizeOf(index);
            }

            public override string ToString()
            {
                return $"({GetType().Name} (index {index}))";
            }
        }
    }
}