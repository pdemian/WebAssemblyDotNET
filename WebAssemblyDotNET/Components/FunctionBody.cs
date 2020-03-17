using System;
using System.Linq;
using System.IO;

namespace WebAssemblyDotNET
{
    namespace Components
    {
        // https://webassembly.github.io/spec/core/binary/modules.html#binary-func
        public class FunctionBody : WebAssemblyComponent
        {
            public readonly LocalEntry[] locals;
            public readonly byte[] code;

            public FunctionBody(uint local_count, LocalEntry[] locals, byte[] code)
            {
                this.locals = locals ?? throw new ArgumentException(nameof(locals));
                this.code = code ?? throw new ArgumentException(nameof(code));

                if (code.Length == 0 || code[code.Length - 1] != (int)WebAssemblyOpcode.END) throw new ArgumentException(nameof(code));
            }

            public FunctionBody(BinaryReader reader)
            {
                // Bit of a hack, but we use positions in this method, so we need to ensure it works
                if (!reader.BaseStream.CanSeek) throw new NotSupportedException("Stream passed does not support seeking.");

                uint body_size = LEB128.ReadUInt32(reader);

                var before_pos = reader.BaseStream.Position;

                uint local_count = LEB128.ReadUInt32(reader);
                if (local_count > int.MaxValue) throw new NotImplementedException($"Count larger than {int.MaxValue} bytes not supported.");
                locals = new LocalEntry[local_count];

                for (uint i = 0; i < local_count; i++)
                {
                    locals[i] = new LocalEntry(reader);
                }

                var after_pos = reader.BaseStream.Position;

                code = reader.ReadBytes((int)(body_size - (after_pos - before_pos)));

                if (code[code.Length - 1] != (byte)WebAssemblyOpcode.END) throw new Exception($"File is invalid. Expected byte {WebAssemblyOpcode.END}, received {code[code.Length-1]}.");
            }

            public override void Save(BinaryWriter writer)
            {
                uint local_count = (uint)locals.Length;
                uint body_size = LEB128.SizeOf(local_count) + (uint)locals.Select(x => (long)x.SizeOf()).Sum() + (uint)code.Length;

                LEB128.WriteUInt32(writer, body_size);
                LEB128.WriteUInt32(writer, local_count);
                foreach (var local in locals)
                {
                    local.Save(writer);
                }
                writer.Write(code);
            }

            public override uint SizeOf()
            {
                uint local_count = (uint)locals.Length;
                long body_size = LEB128.SizeOf(local_count) + (uint)locals.Select(x => (long)x.SizeOf()).Sum() + code.Length;

                return LEB128.SizeOf((uint)body_size) + LEB128.SizeOf(local_count) + (uint)locals.Select(x => (long)x.SizeOf()).Sum() + (uint)code.Length;
            }

            public override string ToString()
            {
                return $"({GetType().Name} (locals {WebAssemblyHelper.ToString(locals)}) (code {BitConverter.ToString(code).Replace("-", "")}))";
            }
        }
    }
}