using System;
using System.Linq;
using System.IO;
using System.Diagnostics.CodeAnalysis;

namespace WebAssemblyDotNET
{
    namespace Sections
    {
        // https://webassembly.github.io/spec/core/binary/modules.html#binary-funcsec
        public class FunctionSection : WebAssemblySection
        {
            public readonly uint[] types;

            [SuppressMessage("Microsoft.Usage", "CA2214:DoNotCallOverridableMethodsInConstructors", Justification = "Cleaner code by calling BinarySize()")]
            public FunctionSection(uint[] types) : base(WebAssemblyModuleID.Function)
            {
                this.types = types ?? throw new ArgumentException(nameof(types));
                payload_len = BinarySize() - base.BinarySize();
            }

            public FunctionSection(BinaryReader reader) : base(reader)
            {
                uint count = LEB128.ReadUInt32(reader);
                if (count > int.MaxValue) throw new NotImplementedException($"Count larger than {int.MaxValue} bytes not supported.");
                types = new uint[count];

                for (uint i = 0; i < count; i++)
                {
                    types[i] = LEB128.ReadUInt32(reader);
                }
            }

            internal override void SaveAsWASM(BinaryWriter writer)
            {
                base.SaveAsWASM(writer);
                LEB128.WriteUInt32(writer, (uint)types.Length);
                foreach (var entry in types)
                {
                    LEB128.WriteUInt32(writer, entry);
                }
            }

            internal override void SaveAsWAT(BinaryWriter writer)
            {
                throw new NotImplementedException();
            }

            internal override uint BinarySize()
            {
                return base.BinarySize() + (uint)types.Select(x => (long)LEB128.SizeOf(x)).Sum() + LEB128.SizeOf((uint)types.Length);
            }

            public override string ToString()
            {
                return $"({GetType().Name} (types {WebAssemblyHelper.ToString(types)}))";
            }
        }
    }
}