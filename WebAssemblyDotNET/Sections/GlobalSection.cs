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

            [SuppressMessage("Microsoft.Usage", "CA2214:DoNotCallOverridableMethodsInConstructors", Justification = "Cleaner code by calling SizeOf()")]
            public GlobalSection(GlobalVariable[] globals) : base(WebAssemblyModuleID.Global)
            {
                this.globals = globals ?? throw new ArgumentException(nameof(globals));
                payload_len = SizeOf() - base.SizeOf();
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

            public override void Save(BinaryWriter writer)
            {
                base.Save(writer);
                LEB128.WriteUInt32(writer, (uint)globals.Length);
                foreach (var entry in globals)
                {
                    entry.Save(writer);
                }
            }

            public override uint SizeOf()
            {
                return base.SizeOf() + (uint)globals.Select(x => (long)x.SizeOf()).Sum() + LEB128.SizeOf((uint)globals.Length);
            }

            public override string ToString()
            {
                return $"({GetType().Name} (globals {WebAssemblyHelper.ToString(globals)}))";
            }
        }
    }
}