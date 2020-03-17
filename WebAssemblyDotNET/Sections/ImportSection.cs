using System;
using System.Linq;
using System.IO;
using WebAssemblyDotNET.Components;

namespace WebAssemblyDotNET
{
    namespace Sections
    {
        // https://webassembly.github.io/spec/core/binary/modules.html#import-section
        public class ImportSection : WebAssemblySection
        {
            public readonly ImportEntry[] entries;

            public ImportSection(ImportEntry[] entries) : base(WebAssemblyModuleID.Import)
            {
                this.entries = entries ?? throw new ArgumentException(nameof(entries));
                payload_len = SizeOf() - base.SizeOf();
            }

            public ImportSection(BinaryReader reader) : base(reader)
            {
                uint count = LEB128.ReadUInt32(reader);
                if (count > int.MaxValue) throw new NotImplementedException($"Count larger than {int.MaxValue} bytes not supported.");
                entries = new ImportEntry[count];

                for (uint i = 0; i < count; i++)
                {
                    entries[i] = new ImportEntry(reader);
                }
            }

            public override void Save(BinaryWriter writer)
            {
                base.Save(writer);
                foreach (var entry in entries)
                {
                    entry.Save(writer);
                }
            }

            public override uint SizeOf()
            {
                return base.SizeOf() + (uint)entries.Select(x => (long)x.SizeOf()).Sum() + LEB128.SizeOf((uint)entries.Length);
            }

            public override string ToString()
            {
                return $"({GetType().Name} (entries {WebAssemblyHelper.ToString(entries)}))";
            }
        }
    }
}