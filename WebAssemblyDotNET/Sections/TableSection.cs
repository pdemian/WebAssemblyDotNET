﻿using System;
using System.Linq;
using System.IO;
using WebAssemblyDotNET.Components;
using System.Diagnostics.CodeAnalysis;

namespace WebAssemblyDotNET
{
    namespace Sections
    {
        // https://webassembly.github.io/spec/core/binary/modules.html#table-section
        public class TableSection : WebAssemblySection
        {
            public readonly TableType[] entries;

            [SuppressMessage("Microsoft.Usage", "CA2214:DoNotCallOverridableMethodsInConstructors", Justification = "Cleaner code by calling SizeOf()")]
            public TableSection(TableType[] entries) : base(WebAssemblyModuleID.Table)
            {
                this.entries = entries ?? throw new ArgumentException(nameof(entries));
                payload_len = SizeOf() - base.SizeOf();
            }

            public TableSection(BinaryReader reader) : base(reader)
            {
                uint count = LEB128.ReadUInt32(reader);
                if (count > int.MaxValue) throw new NotImplementedException($"Count larger than {int.MaxValue} bytes not supported.");
                entries = new TableType[count];

                for (uint i = 0; i < count; i++)
                {
                    entries[i] = new TableType(reader);
                }
            }

            public override void Save(BinaryWriter writer)
            {
                base.Save(writer);
                LEB128.WriteUInt32(writer, (uint)entries.Length);
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