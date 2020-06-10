using System;
using System.IO;

namespace WebAssemblyDotNET
{
    namespace Components
    {
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

            internal override void SaveAsWASM(BinaryWriter writer)
            {
                type.SaveAsWASM(writer);
                init.SaveAsWASM(writer);
            }

            internal override void SaveAsWAT(BinaryWriter writer)
            {
                type.SaveAsWAT(writer);
                writer.Write(' ');
                type.SaveAsWAT(writer);
            }

            internal override uint BinarySize()
            {
                return type.BinarySize() + init.BinarySize();
            }

            public override string ToString()
            {
                return $"({GetType().Name} {type} {init})";
            }
        }
    }
}