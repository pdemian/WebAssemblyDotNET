using System;
using System.Linq;
using System.IO;
using WebAssemblyDotNET.Components;
using System.Diagnostics.CodeAnalysis;

namespace WebAssemblyDotNET
{
    namespace Sections
    {
        // https://webassembly.github.io/spec/core/binary/modules.html#type-section
        public class TypeSection : WebAssemblySection
        {
            public readonly FuncType[] entries;

            [SuppressMessage("Microsoft.Usage", "CA2214:DoNotCallOverridableMethodsInConstructors", Justification = "Cleaner code by calling SizeOf()")]
            public TypeSection(FuncType[] entries) : base(WebAssemblyModuleID.Type)
            {
                this.entries = entries ?? throw new ArgumentException(nameof(entries));
                payload_len = SizeOf() - base.SizeOf();
            }

            public TypeSection(BinaryReader reader) : base(reader)
            {
                uint count = LEB128.ReadUInt32(reader);
                if(count > int.MaxValue) throw new NotImplementedException($"Count larger than {int.MaxValue} bytes not supported.");
                entries = new FuncType[count];

                for (uint i = 0; i < count; i++)
                {
                    entries[i] = new FuncType(reader);
                }
            }

            public override void Save(BinaryWriter writer)
            {
                base.Save(writer);

                LEB128.WriteUInt32(writer, (uint)entries.Length);
                foreach (FuncType entry in entries)
                {
                    entry.Save(writer);
                }
            }

            public override uint SizeOf()
            {
                return base.SizeOf() + LEB128.SizeOf((uint)entries.Length) + (uint)entries.Select(x => (long)x.SizeOf()).Sum();
            }

            public override string ToString()
            {
                return $"({GetType().Name} (entries {WebAssemblyHelper.ToString(entries)}))";
            }
        }
    }
}