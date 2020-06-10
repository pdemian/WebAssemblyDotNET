using System;
using System.Linq;
using System.IO;

namespace WebAssemblyDotNET
{
    namespace Components
    {
        // https://webassembly.github.io/spec/core/binary/types.html#function-types
        public class FuncType : WebAssemblyComponent
        {
            public readonly WebAssemblyType[] param_types;
            public readonly WebAssemblyType? return_type;

            public FuncType(WebAssemblyType[] param_types, WebAssemblyType? return_type)
            {
                this.param_types = param_types ?? throw new ArgumentException(nameof(param_types));
                this.return_type = return_type;

                if (param_types.Any(x => !WebAssemblyHelper.IsValueType(x))) throw new ArgumentException(nameof(param_types));

                if (return_type != null && !WebAssemblyHelper.IsValueType((WebAssemblyType)this.return_type)) throw new ArgumentException(nameof(return_type));
            }

            public FuncType(BinaryReader reader)
            {
                int form = LEB128.ReadInt7(reader);
                if (form != (int)WebAssemblyType.func) throw new Exception($"File is invalid. Expected byte '{WebAssemblyType.func}', received '{form}'.");

                uint param_count = LEB128.ReadUInt32(reader);
                if (param_count > int.MaxValue) throw new NotImplementedException($"Count larger than {int.MaxValue} bytes not supported.");
                param_types = new WebAssemblyType[param_count];

                for (uint i = 0; i < param_count; i++)
                {
                    param_types[i] = (WebAssemblyType)LEB128.ReadInt7(reader);
                    if (!WebAssemblyHelper.IsValueType(param_types[i])) throw new Exception($"File is invalid. Expected valid value type, received '{param_types[i]}'.");
                }

                int return_count = LEB128.ReadInt7(reader);
                if (return_count != 0 && return_count != 1) throw new Exception($"File is invalid. Expected byte 0 or 1, received {return_count}.");

                if (return_count == 1)
                {
                    return_type = (WebAssemblyType)LEB128.ReadInt7(reader);
                    if (!WebAssemblyHelper.IsValueType((WebAssemblyType)return_type)) throw new Exception($"File is invalid. Expected valid value type, received '{return_type}'.");
                }
            }

            internal override void SaveAsWASM(BinaryWriter writer)
            {
                LEB128.WriteInt7(writer, (sbyte)WebAssemblyType.func);
                LEB128.WriteUInt32(writer, (uint)param_types.Length);
                for (uint i = 0; i < param_types.Length; i++)
                {
                    LEB128.WriteInt7(writer, (sbyte)param_types[i]);
                }

                if (return_type != null)
                {
                    LEB128.WriteInt7(writer, 1);
                    LEB128.WriteInt7(writer, (sbyte)return_type);
                }
                else
                {
                    LEB128.WriteInt7(writer, 0);
                }
            }

            internal override void SaveAsWAT(BinaryWriter writer)
            {
                writer.Write("(func ");

                if(param_types.Length > 0)
                {
                    writer.Write($"(param {WebAssemblyHelper.ToString(param_types)}) ");
                }

                if(return_type != null)
                {
                    writer.Write($"(result {return_type.ToString()})");
                }

                writer.Write(')');
            }

            internal override uint BinarySize()
            {
                return sizeof(byte) + LEB128.SizeOf((uint)param_types.Length) + (uint)param_types.Length + sizeof(byte) * (return_type == null ? 1u : 2u);
            }

            public override string ToString()
            {
                return $"({GetType().Name} (return {return_type?.ToString() ?? "void"}) (params {WebAssemblyHelper.ToString(param_types)}))";
            }
        }
    }
}