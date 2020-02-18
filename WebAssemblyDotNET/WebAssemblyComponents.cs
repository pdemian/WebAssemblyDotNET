using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace WebAssemblyDotNET
{
    namespace Components
    {
        // https://webassembly.github.io/spec/core/binary/types.html#function-types
        public class FuncType : WebAssemblyComponent
        {
            public readonly WASMType[] param_types;
            public readonly WASMType? return_type;

            public FuncType(WASMType[] param_types, WASMType? return_type)
            {
                this.param_types = param_types ?? throw new ArgumentException(nameof(param_types));
                this.return_type = return_type;

                if (param_types.Any(x => !WebAssemblyHelper.IsValueType(x))) throw new ArgumentException(nameof(param_types));

                if (return_type != null && !WebAssemblyHelper.IsValueType((WASMType)this.return_type)) throw new ArgumentException(nameof(return_type));
            }

            public FuncType(BinaryReader reader)
            {
                int form = LEB128.ReadInt7(reader);
                if (form != (int)WASMType.func) throw new Exception($"File is invalid. Expected byte '{WASMType.func}', received '{form}'.");

                uint param_count = LEB128.ReadUInt32(reader);
                if (param_count > int.MaxValue) throw new NotImplementedException($"Count larger than {int.MaxValue} bytes not supported.");
                param_types = new WASMType[param_count];

                for (uint i = 0; i < param_count; i++)
                {
                    param_types[i] = (WASMType)LEB128.ReadInt7(reader);
                    if (!WebAssemblyHelper.IsValueType(param_types[i])) throw new Exception($"File is invalid. Expected valid value type, received '{param_types[i]}'.");
                }

                int return_count = LEB128.ReadInt7(reader);
                if (return_count != 0 && return_count != 1) throw new Exception($"File is invalid. Expected byte 0 or 1, received {return_count}.");

                if (return_count == 1)
                {
                    return_type = (WASMType)LEB128.ReadInt7(reader);
                    if (!WebAssemblyHelper.IsValueType((WASMType)return_type)) throw new Exception($"File is invalid. Expected valid value type, received '{return_type}'.");
                }
            }

            public override void Save(BinaryWriter writer)
            {
                LEB128.WriteInt7(writer, (sbyte)WASMType.func);
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

            public override uint SizeOf()
            {
                return sizeof(byte) + LEB128.SizeOf((uint)param_types.Length) + (uint)param_types.Length + sizeof(byte) * (return_type == null ? 1u : 2u);
            }
        }

        // https://webassembly.github.io/spec/core/binary/types.html#limits
        public class ResizeLimit : WebAssemblyComponent
        {
            public readonly uint initial;
            public readonly uint? maximum;

            public ResizeLimit(uint initial, uint? maximum)
            {
                if (maximum != null && initial > maximum) throw new ArgumentException(nameof(maximum));

                this.initial = initial;
                this.maximum = maximum;
            }

            public ResizeLimit(BinaryReader reader)
            {
                byte flags = LEB128.ReadUInt7(reader);
                if(flags != 0 && flags != 1) throw new Exception($"File is invalid. Expected 0 or 1, received '{flags}'.");

                initial = LEB128.ReadUInt32(reader);
                if (flags != 0)
                {
                    maximum = LEB128.ReadUInt32(reader);
                    if (maximum < initial) throw new Exception($"File is invalid. Initial memory size greater than maximum.");
                }
            }

            public override void Save(BinaryWriter writer)
            {
                LEB128.WriteUInt7(writer, (byte)(maximum != null ? 1 : 0));
                LEB128.WriteUInt32(writer, initial);
                if (maximum != null)
                {
                    LEB128.WriteUInt32(writer, (uint)maximum);
                }
            }

            public override uint SizeOf()
            {
                return sizeof(byte) + LEB128.SizeOf(initial) + (maximum != null ? LEB128.SizeOf((uint)maximum) : 0);
            }
        }

        // https://webassembly.github.io/spec/core/binary/types.html#table-types
        public class TableType : WebAssemblyComponent
        {
            public readonly WASMType element_type;
            public readonly ResizeLimit limits;

            public TableType(WASMType element_type, ResizeLimit limits)
            {
                if (element_type != WASMType.anyfunc) throw new ArgumentException(nameof(element_type));
                this.element_type = element_type;
                this.limits = limits ?? throw new ArgumentException(nameof(limits));
            }

            public TableType(BinaryReader reader)
            {
                sbyte type = LEB128.ReadInt7(reader);
                if(type != (int)WASMType.anyfunc) throw new Exception($"File is invalid. Expected byte '{WASMType.anyfunc}', received '{type}'.");
                element_type = WASMType.anyfunc;
                limits = new ResizeLimit(reader);
            }

            public override void Save(BinaryWriter writer)
            {
                LEB128.WriteInt7(writer, (sbyte)element_type);
                limits.Save(writer);
            }

            public override uint SizeOf()
            {
                return sizeof(sbyte) + limits.SizeOf();
            }
        }

        // https://webassembly.github.io/spec/core/binary/types.html#memory-types
        public class MemoryType : WebAssemblyComponent
        {
            public readonly ResizeLimit limits;

            public MemoryType(ResizeLimit limits)
            {
                this.limits = limits ?? throw new ArgumentException(nameof(limits));
            }

            public MemoryType(BinaryReader reader)
            {
                limits = new ResizeLimit(reader);
            }

            public override void Save(BinaryWriter writer)
            {
                limits.Save(writer);
            }

            public override uint SizeOf()
            {
                return limits.SizeOf();
            }
        }

        // https://webassembly.github.io/spec/core/binary/types.html#global-types
        public class GlobalType : WebAssemblyComponent
        {
            public readonly WASMType content_type;
            public readonly bool mutability;

            public GlobalType(WASMType content_type, bool mutability)
            {
                if (!WebAssemblyHelper.IsValueType(content_type)) throw new ArgumentException(nameof(content_type));

                this.content_type = content_type;
                this.mutability = mutability;
            }

            public GlobalType(BinaryReader reader)
            {
                content_type = (WASMType)LEB128.ReadUInt7(reader);

                if (!WebAssemblyHelper.IsValueType(content_type)) throw new Exception($"File is invalid. Expected value type, received '{content_type}'.");

                byte mut = LEB128.ReadUInt7(reader);

                if(mut != 0 && mut != 1) throw new Exception($"File is invalid. Expected 0 or 1, received '{mut}'.");

                mutability = mut != 0;
            }

            public override void Save(BinaryWriter writer)
            {
                LEB128.WriteUInt7(writer, (byte)content_type);
                LEB128.WriteUInt7(writer, (byte)(mutability ? 1 : 0));
            }

            public override uint SizeOf()
            {
                return sizeof(byte) + sizeof(byte);
            }
        }

        // https://webassembly.github.io/spec/core/binary/modules.html#binary-import
        public class ImportEntry : WebAssemblyComponent
        {
            public readonly string module_str;
            public readonly string field_str;
            public readonly WASMExternalKind kind;
            public readonly object type;

            private ImportEntry(string module_str, string field_str, WASMExternalKind kind, object type)
            {
                this.module_str = module_str ?? throw new ArgumentException(nameof(module_str));
                this.field_str = field_str ?? throw new ArgumentException(nameof(field_str));
                this.kind = kind;
                this.type = type ?? throw new ArgumentException(nameof(type));
            }

            public ImportEntry(string module_str, string field_str, uint type) : this(module_str, field_str, WASMExternalKind.Function, type){}
            public ImportEntry(string module_str, string field_str, TableType type) : this(module_str, field_str, WASMExternalKind.Table, type){}
            public ImportEntry(string module_str, string field_str, MemoryType type) : this(module_str, field_str, WASMExternalKind.Memory, type){}
            public ImportEntry(string module_str, string field_str, GlobalType type) : this(module_str, field_str, WASMExternalKind.Global, type){}

            public ImportEntry(BinaryReader reader)
            {
                uint module_len = LEB128.ReadUInt32(reader);
                if (module_len > int.MaxValue) throw new NotImplementedException($"String longer than {int.MaxValue} bytes not supported.");
                module_str = Encoding.UTF8.GetString(reader.ReadBytes((int)module_len));

                uint field_len = LEB128.ReadUInt32(reader);
                if (field_len > int.MaxValue) throw new NotImplementedException($"String longer than {int.MaxValue} bytes not supported.");
                field_str = Encoding.UTF8.GetString(reader.ReadBytes((int)field_len));

                byte tmp_kind = reader.ReadByte();

                if (!Enum.IsDefined(typeof(WASMExternalKind), tmp_kind)) throw new Exception($"File is invalid. Expected 0, 1, 2, or 3, received '{tmp_kind}'.");

                kind = (WASMExternalKind)tmp_kind;

                switch (kind)
                {
                    case WASMExternalKind.Function:
                        type = LEB128.ReadUInt32(reader);
                        break;
                    case WASMExternalKind.Table:
                        var table_tmp = new TableType(reader);
                        type = table_tmp;
                        break;
                    case WASMExternalKind.Memory:
                        var memory_tmp = new MemoryType(reader);
                        type = memory_tmp;
                        break;
                    case WASMExternalKind.Global:
                        var global_tmp = new GlobalType(reader);
                        type = global_tmp;
                        break;
                }
            }

            public override void Save(BinaryWriter writer)
            {
                var module = Encoding.UTF8.GetBytes(module_str);
                var field = Encoding.UTF8.GetBytes(field_str);

                LEB128.WriteUInt32(writer, (uint)module.Length);
                writer.Write(module);
                LEB128.WriteUInt32(writer, (uint)field.Length);
                writer.Write(field);
                writer.Write((byte)kind);

                switch (kind)
                {
                    case WASMExternalKind.Function:
                        LEB128.WriteUInt32(writer, (uint)type);
                        break;
                    case WASMExternalKind.Table:
                        ((TableType)type).Save(writer);
                        break;
                    case WASMExternalKind.Memory:
                        ((MemoryType)type).Save(writer);
                        break;
                    case WASMExternalKind.Global:
                        ((GlobalType)type).Save(writer);
                        break;
                }
            }

            public override uint SizeOf()
            {
                var module_len = Encoding.UTF8.GetByteCount(module_str);
                var field_len = Encoding.UTF8.GetByteCount(field_str);

                uint size = LEB128.SizeOf(module_len) + LEB128.SizeOf(field_len) + (uint)module_len + (uint)field_len + sizeof(byte);

                switch (kind)
                {
                    case WASMExternalKind.Function:
                        size += LEB128.SizeOf((uint)type);
                        break;
                    case WASMExternalKind.Table:
                        size += ((TableType)type).SizeOf();
                        break;
                    case WASMExternalKind.Memory:
                        size += ((MemoryType)type).SizeOf();
                        break;
                    case WASMExternalKind.Global:
                        size += ((GlobalType)type).SizeOf();
                        break;
                }

                return size;
            }
        }

        // https://webassembly.github.io/spec/core/binary/modules.html#global-section
        public class GlobalVariable : WebAssemblyComponent
        {
            public readonly GlobalType type;
            public readonly InitExpr init;

            public GlobalVariable(GlobalType type, InitExpr init)
            {
                this.type = type ?? throw new ArgumentException(nameof(type));
                this.init = init ?? throw new ArgumentException(nameof(init));

                if (type.content_type != WebAssemblyHelper.GetInitExprType(init)) throw new Exception("Global variable type and expression mismatch.");
            }

            public GlobalVariable(BinaryReader reader)
            {
                type = new GlobalType(reader);
                init = new InitExpr(reader);

                if (type.content_type != WebAssemblyHelper.GetInitExprType(init)) throw new Exception("Global variable type and expression mismatch.");
            }

            public override void Save(BinaryWriter writer)
            {
                type.Save(writer);
                init.Save(writer);
            }

            public override uint SizeOf()
            {
                return type.SizeOf() + init.SizeOf();
            }
        }

        // https://webassembly.github.io/spec/core/binary/modules.html#binary-export
        public class ExportEntry : WebAssemblyComponent
        {
            public readonly string field_str;
            public readonly WASMExternalKind kind;
            public readonly uint index;

            public ExportEntry(string field_str, WASMExternalKind kind, uint index)
            {
                this.field_str = field_str ?? throw new ArgumentException(nameof(field_str));
                this.index = index;
                if (!Enum.IsDefined(typeof(WASMExternalKind), kind)) throw new ArgumentException(nameof(kind));
                this.kind = kind;
            }

            public ExportEntry(BinaryReader reader)
            {
                uint field_len = LEB128.ReadUInt32(reader);
                if (field_len > int.MaxValue) throw new NotImplementedException($"String longer than {int.MaxValue} bytes not supported.");
                field_str = Encoding.UTF8.GetString(reader.ReadBytes((int)field_len));

                var tmp_kind = reader.ReadByte();

                if (!Enum.IsDefined(typeof(WASMExternalKind), tmp_kind)) throw new Exception($"File is invalid. Expected byte 0, 1, 2, or 3, received '{tmp_kind}'.");

                kind = (WASMExternalKind)tmp_kind;

                index = LEB128.ReadUInt32(reader);
            }

            public override void Save(BinaryWriter writer)
            {
                var field = Encoding.UTF8.GetBytes(field_str);

                LEB128.WriteUInt32(writer, (uint)field.Length);
                writer.Write(field);
                writer.Write((byte)kind);
                LEB128.WriteUInt32(writer, index);
            }

            public override uint SizeOf()
            {
                var field = Encoding.UTF8.GetByteCount(field_str);

                return LEB128.SizeOf((uint)field) + (uint)field + sizeof(byte) + LEB128.SizeOf(index);
            }
        }

        // https://webassembly.github.io/spec/core/binary/modules.html#binary-elemsec
        public class ElementSegment : WebAssemblyComponent
        {
            public readonly uint table_index;
            public readonly InitExpr offset;
            public readonly uint[] function_index;

            public ElementSegment(uint table_index, InitExpr offset, uint[] function_index)
            {
                this.table_index = table_index;
                this.offset = offset ?? throw new ArgumentException(nameof(offset));
                this.function_index = function_index ?? throw new ArgumentException(nameof(function_index));
            }

            public ElementSegment(BinaryReader reader)
            {
                table_index = LEB128.ReadUInt32(reader);
                offset = new InitExpr(reader);
                uint num_elem = LEB128.ReadUInt32(reader);

                function_index = new uint[num_elem];

                for (uint i = 0; i < num_elem; i++)
                {
                    function_index[i] = LEB128.ReadUInt32(reader);
                }
            }

            public override void Save(BinaryWriter writer)
            {
                LEB128.WriteUInt32(writer, table_index);
                offset.Save(writer);
                LEB128.WriteUInt32(writer, (uint)function_index.Length);
                foreach (var elem in function_index)
                {
                    LEB128.WriteUInt32(writer, elem);
                }
            }

            public override uint SizeOf()
            {
                return LEB128.SizeOf(table_index) + offset.SizeOf() + (uint)function_index.Length + (uint)function_index.Select(x => (long)LEB128.SizeOf(x)).Sum();
            }
        }

        // TODO: Validate code
        // https://webassembly.github.io/spec/core/binary/modules.html#binary-func
        public class FunctionBody : WebAssemblyComponent
        {
            public readonly LocalEntry[] locals;
            public readonly byte[] code;

            public FunctionBody(uint local_count, LocalEntry[] locals, byte[] code)
            {
                this.locals = locals ?? throw new ArgumentException(nameof(locals));
                this.code = code ?? throw new ArgumentException(nameof(code));

                if (code.Length == 0 || code[code.Length - 1] != (int)WASMOpcodes.END) throw new ArgumentException(nameof(code));
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

                if (code[code.Length - 1] != (byte)WASMOpcodes.END) throw new Exception($"File is invalid. Expected byte {WASMOpcodes.END}, received {code[code.Length-1]}.");
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
        }

        // https://webassembly.github.io/spec/core/binary/modules.html#binary-local
        public class LocalEntry : WebAssemblyComponent
        {
            public readonly uint count;
            public readonly WASMType type;

            public LocalEntry(uint count, WASMType type)
            {
                if (!Enum.IsDefined(typeof(WASMType), type)) throw new ArgumentException(nameof(type));

                this.count = count;
                this.type = type;
            }

            public LocalEntry(BinaryReader reader)
            {
                count = LEB128.ReadUInt32(reader);

                var tmp_type = reader.ReadByte();

                if (!Enum.IsDefined(typeof(WASMType), tmp_type)) throw new Exception($"File is invalid. Expected WASMType, received '{tmp_type}'.");

                type = (WASMType)tmp_type;
            }

            public override void Save(BinaryWriter writer)
            {
                LEB128.WriteUInt32(writer, count);
                writer.Write((byte)type);
            }

            public override uint SizeOf()
            {
                return LEB128.SizeOf(count) + sizeof(byte);
            }
        }

        // https://webassembly.github.io/spec/core/binary/modules.html#binary-data
        public class DataSegment : WebAssemblyComponent
        {
            public readonly uint memory_index;
            public readonly InitExpr offset;
            public readonly byte[] data;

            public DataSegment(uint memory_index, InitExpr offset, byte[] data)
            {
                this.memory_index = memory_index;
                this.offset = offset ?? throw new ArgumentException(nameof(offset));
                this.data = data ?? throw new ArgumentException(nameof(data));
            }

            public DataSegment(BinaryReader reader)
            {
                memory_index = LEB128.ReadUInt32(reader);
                offset = new InitExpr(reader);
                uint count = LEB128.ReadUInt32(reader);
                if (count > int.MaxValue) throw new NotImplementedException($"Count larger than {int.MaxValue} bytes not supported.");
                data = reader.ReadBytes((int)count);
            }

            public override void Save(BinaryWriter writer)
            {
                LEB128.WriteUInt32(writer, memory_index);
                offset.Save(writer);
                LEB128.WriteUInt32(writer, (uint)data.Length);
                writer.Write(data);
            }

            public override uint SizeOf()
            {
                return LEB128.SizeOf(memory_index) + LEB128.SizeOf((uint)data.Length) + (uint)data.Length + offset.SizeOf();
            }
        }

        // TODO: Validate code
        // https://webassembly.github.io/spec/core/binary/instructions.html#binary-expr
        public class InitExpr : WebAssemblyComponent
        {
            public readonly byte[] expr;

            public InitExpr(byte[] expr)
            {
                this.expr = expr ?? throw new ArgumentException(nameof(expr));
                if (expr.Length < 1 || expr[expr.Length - 1] != (byte)WASMOpcodes.END) throw new ArgumentException(nameof(expr));
            }

            public InitExpr(BinaryReader reader)
            {
                List<byte> code = new List<byte>();
                for (int b = reader.ReadByte(); ; b = reader.ReadByte())
                {
                    if (b == -1) throw new Exception($"File is invalid. Unexpected EOF.");
                    code.Add((byte)b);
                    if (b == (byte)WASMOpcodes.END) break;
                }

                expr = code.ToArray();
            }

            public override void Save(BinaryWriter writer)
            {
                writer.Write(expr);
            }

            public override uint SizeOf()
            {
                return (uint)expr.Length;
            }
        }
    }
}