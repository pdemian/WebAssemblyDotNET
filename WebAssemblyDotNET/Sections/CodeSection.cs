using System;
using System.Linq;
using System.IO;
using WebAssemblyDotNET.Components;
using System.Diagnostics.CodeAnalysis;

namespace WebAssemblyDotNET
{
    namespace Sections
    {
        // https://webassembly.github.io/spec/core/binary/modules.html#code-section
        public class CodeSection : WebAssemblySection
        {
            public readonly FunctionBody[] bodies;

            [SuppressMessage("Microsoft.Usage", "CA2214:DoNotCallOverridableMethodsInConstructors", Justification = "Cleaner code by calling SizeOf()")]
            public CodeSection(FunctionBody[] bodies) : base(WebAssemblyModuleID.Code)
            {
                this.bodies = bodies ?? throw new ArgumentException(nameof(bodies));
                payload_len = SizeOf() - base.SizeOf();
            }

            public CodeSection(BinaryReader reader) : base(reader)
            {
                uint count = LEB128.ReadUInt32(reader);
                if (count > int.MaxValue) throw new NotImplementedException($"Count larger than {int.MaxValue} bytes not supported.");
                bodies = new FunctionBody[count];

                for (uint i = 0; i < count; i++)
                {
                    bodies[i] = new FunctionBody(reader);
                }
            }

            public override void Save(BinaryWriter writer)
            {
                base.Save(writer);
                foreach (var entry in bodies)
                {
                    entry.Save(writer);
                }
            }

            public override uint SizeOf()
            {
                return base.SizeOf() + (uint)bodies.Select(x => (long)x.SizeOf()).Sum() + LEB128.SizeOf((uint)bodies.Length);
            }

            public override string ToString()
            {
                return $"({GetType().Name} (bodies {WebAssemblyHelper.ToString(bodies)}))";
            }
        }
    }
}