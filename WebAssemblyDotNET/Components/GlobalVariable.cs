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

            public override void Save(BinaryWriter writer)
            {
                type.Save(writer);
                init.Save(writer);
            }

            public override uint SizeOf()
            {
                return type.SizeOf() + init.SizeOf();
            }

            public override string ToString()
            {
                return $"({GetType().Name} {type} {init})";
            }
        }
    }
}