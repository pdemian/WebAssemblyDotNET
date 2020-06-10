using System;
using System.Linq;
using System.IO;
using WebAssemblyDotNET.Components;
using System.Diagnostics.CodeAnalysis;

namespace WebAssemblyDotNET
{
    namespace Sections
    {
        // https://webassembly.github.io/spec/core/binary/modules.html#global-section
        public class GlobalSection : WebAssemblySection
        {
            public readonly GlobalVariable[] globals;

            [SuppressMessage("Microsoft.Usage", "CA2214:DoNotCallOverridableMethodsInConstructors", Justification = "Cleaner code by calling BinarySize()")]
            public GlobalSection(GlobalVariable[] globals) : base(WebAssemblyModuleID.Global)
            {
                this.globals = globals ?? throw new ArgumentException(nameof(globals));
                payload_len = BinarySize() - base.BinarySize();
            }

            public GlobalSection(BinaryReader reader) : base(reader)
            {
                uint count = LEB128.ReadUInt32(reader);
                if (count > int.MaxValue) throw new NotImplementedException($"Count larger than {int.MaxValue} bytes not supported.");
                globals = new GlobalVariable[count];

                for (uint i = 0; i < count; i++)
                {
                    globals[i] = new GlobalVariable(reader);
                }
            }

            internal override void SaveAsWASM(BinaryWriter writer)
            {
                base.SaveAsWASM(writer);
                LEB128.WriteUInt32(writer, (uint)globals.Length);
                foreach (var entry in globals)
                {
                    entry.SaveAsWASM(writer);
                }
            }

            internal override void SaveAsWAT(BinaryWriter writer)
            {
                for (uint index = 0; index < globals.Length; index++)
                {
                    writer.Write($"\t(global {index} ");
                    globals[index].SaveAsWAT(writer);
                    writer.Write(")\n");
                }
            }

            internal override uint BinarySize()
            {
                return base.BinarySize() + (uint)globals.Select(x => (long)x.BinarySize()).Sum() + LEB128.SizeOf((uint)globals.Length);
            }

            public override string ToString()
            {
                return $"({GetType().Name} (globals {WebAssemblyHelper.ToString(globals)}))";
            }
        }
    }
}