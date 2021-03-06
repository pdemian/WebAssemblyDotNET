﻿using System;
using System.IO;

namespace WebAssemblyDotNET
{
    namespace Components
    {
        // https://webassembly.github.io/spec/core/binary/types.html#table-types
        public class TableType : WebAssemblyComponent
        {
            public readonly WebAssemblyType element_type;
            public readonly ResizeLimit limits;

            public TableType(WebAssemblyType element_type, ResizeLimit limits)
            {
                if (element_type != WebAssemblyType.anyfunc) throw new ArgumentException(nameof(element_type));
                this.element_type = element_type;
                this.limits = limits ?? throw new ArgumentException(nameof(limits));
            }

            public TableType(BinaryReader reader)
            {
                sbyte type = LEB128.ReadInt7(reader);
                if(type != (int)WebAssemblyType.anyfunc) throw new Exception($"File is invalid. Expected byte '{WebAssemblyType.anyfunc}', received '{type}'.");
                element_type = WebAssemblyType.anyfunc;
                limits = new ResizeLimit(reader);
            }

            internal override void SaveAsWASM(BinaryWriter writer)
            {
                LEB128.WriteInt7(writer, (sbyte)element_type);
                limits.SaveAsWASM(writer);
            }

            internal override void SaveAsWAT(BinaryWriter writer)
            {
                writer.Write(element_type.ToString());
            }

            internal override uint BinarySize()
            {
                return sizeof(sbyte) + limits.BinarySize();
            }

            public override string ToString()
            {
                return $"({GetType().Name} (element_type {element_type}) {limits})";
            }
        }
    }
}