using System;
using System.Collections.Generic;
using System.IO;

namespace WebAssemblyDotNET
{
    namespace Components
    {
        // https://webassembly.github.io/spec/core/binary/instructions.html#binary-expr
        public class InitExpr : WebAssemblyComponent
        {
            public readonly byte[] expr;

            public InitExpr(byte[] expr)
            {
                this.expr = expr ?? throw new ArgumentException(nameof(expr));
                if (expr.Length < 1 || expr[expr.Length - 1] != (byte)WebAssemblyOpcode.END) throw new ArgumentException(nameof(expr));
            }

            public InitExpr(BinaryReader reader)
            {
                List<byte> code = new List<byte>();
                for (int b = reader.ReadByte(); ; b = reader.ReadByte())
                {
                    if (b == -1) throw new Exception($"File is invalid. Unexpected EOF.");
                    code.Add((byte)b);
                    if (b == (byte)WebAssemblyOpcode.END) break;
                }

                expr = code.ToArray();
            }

            internal override void SaveAsWASM(BinaryWriter writer)
            {
                writer.Write(expr);
            }

            internal override void SaveAsWAT(BinaryWriter writer)
            {
                writer.Write($"(i32.const {WebAssemblyHelper.GetOffset(this)})");
            }

            internal override uint BinarySize()
            {
                return (uint)expr.Length;
            }

            public override string ToString()
            {
                return $"({GetType().Name} (expr {BitConverter.ToString(expr).Replace("-", "")}))";
            }
        }
    }
}