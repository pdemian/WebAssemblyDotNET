using System;
using System.Linq;
using System.IO;
using WebAssemblyDotNET.Components;
using System.Diagnostics.CodeAnalysis;

namespace WebAssemblyDotNET
{
    namespace Sections
    {
        // https://webassembly.github.io/spec/core/binary/modules.html#data-section
        
        public class DataSection : WebAssemblySection
        {
            public readonly DataSegment[] entries;

            [SuppressMessage("Microsoft.Usage", "CA2214:DoNotCallOverridableMethodsInConstructors", Justification = "Cleaner code by calling BinarySize()")]
            public DataSection(DataSegment[] entries) : base(WebAssemblyModuleID.Data)
            {
                this.entries = entries ?? throw new ArgumentException(nameof(entries));
                payload_len = BinarySize() - base.BinarySize();
            }

            public DataSection(BinaryReader reader) : base(reader)
            {
                uint count = LEB128.ReadUInt32(reader);
                if (count > int.MaxValue) throw new NotImplementedException($"Count larger than {int.MaxValue} bytes not supported.");
                entries = new DataSegment[count];

                for (uint i = 0; i < count; i++)
                {
                    entries[i] = new DataSegment(reader);
                }
            }

            internal override void SaveAsWASM(BinaryWriter writer)
            {
                base.SaveAsWASM(writer);
                LEB128.WriteUInt32(writer, (uint)entries.Length);
                foreach (var entry in entries)
                {
                    entry.SaveAsWASM(writer);
                }
            }

            internal override void SaveAsWAT(BinaryWriter writer)
            {
                foreach(var entry in entries.OrderBy(x => x.memory_index))
                {
                    writer.Write('\t');
                    entry.SaveAsWAT(writer);
                    writer.Write('\n');
                }
            }

            internal override uint BinarySize()
            {
                return base.BinarySize() + (uint)entries.Select(x => (long)x.BinarySize()).Sum() + LEB128.SizeOf((uint)entries.Length);
            }

            public override string ToString()
            {
                return $"({GetType().Name} (bodies {WebAssemblyHelper.ToString(entries)}))";
            }
        }
    }
}