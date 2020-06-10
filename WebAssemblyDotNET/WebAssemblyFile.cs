using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using WebAssemblyDotNET.Sections;
using NLog;

namespace WebAssemblyDotNET
{
    // https://webassembly.github.io/spec/core/binary/modules.html#binary-module
    public class WebAssemblyFile
    {
        private readonly Logger logger = LogManager.GetCurrentClassLogger();

        private const uint MAGIC = 0x6D_73_61_00;
        private const uint SUPPORTED_VERSION = 1;

        public CustomSection custom = null;
        public TypeSection type = null;
        public ImportSection import = null;
        public FunctionSection function = null;
        public TableSection table = null;
        public MemorySection memory = null;
        public GlobalSection global = null;
        public ExportSection export = null;
        public StartSection start = null;
        public ElementSection element = null;
        public CodeSection code = null;
        public DataSection data = null;

        public WebAssemblyFile() { }

        public void Parse(string filename, bool strict_parse = true)
        {
            switch(new FileInfo(filename).Extension.ToUpperInvariant())
            {
                case "TXT": //Maybe someone is using a .txt file to write the code in because .wat isn't exactly well known
                case "WAT":
                    ParseAsWAT(filename, strict_parse);
                    break;
                // default to WASM even if filename isn't specified as that's the primary purpose of this API
                default:
                    ParseAsWASM(filename, strict_parse);
                    break;
            }
        }

        public void ParseAsWAT(string filename, bool strict_parse = true)
        {
            throw new NotImplementedException();
        }

        // strict parse means all sections must come in order
        public void ParseAsWASM(string filename, bool strict_parse = true)
        {
            if (!BitConverter.IsLittleEndian) throw new NotImplementedException("LEB128 implementation only handles little endian systems");

            using (FileStream fs = new FileStream(filename, FileMode.Open, FileAccess.Read))
            {
                BinaryReader reader = new BinaryReader(fs);
                uint magic = reader.ReadUInt32();
                if (magic != MAGIC) throw new Exception("Not a compiled Web Assembly file.");

                uint version = reader.ReadUInt32();
                if (version > SUPPORTED_VERSION) throw new Exception($"Unsupported version. Expected version <= {SUPPORTED_VERSION}, received {version}.");

                int last_parsed_module = int.MinValue;

                /* Read in each module */

                while (true)
                {
                    int id = reader.PeekChar();

                    // EOF
                    if (id == -1) break;

                    if (strict_parse && id < last_parsed_module) throw new Exception("File contains out of order sections.");
                    last_parsed_module = id;

                    switch (id)
                    {
                        case (int)WebAssemblyModuleID.Custom:
                            if (strict_parse && custom != null) throw new Exception("File contains a duplicate custom section.");
                            custom = new CustomSection(reader);
                            break;
                        case (int)WebAssemblyModuleID.Type:
                            if (strict_parse && type != null) throw new Exception("File contains a duplicate type section.");
                            type = new TypeSection(reader);
                            break;
                        case (int)WebAssemblyModuleID.Import:
                            if (strict_parse && import != null) throw new Exception("File contains a duplicate import section.");
                            import = new ImportSection(reader);
                            break;
                        case (int)WebAssemblyModuleID.Function:
                            if (strict_parse && function != null) throw new Exception("File contains a duplicate function section.");
                            function = new FunctionSection(reader);
                            break;
                        case (int)WebAssemblyModuleID.Table:
                            if (strict_parse && table != null) throw new Exception("File contains a duplicate table section.");
                            table = new TableSection(reader);
                            break;
                        case (int)WebAssemblyModuleID.Memory:
                            if (strict_parse && memory != null) throw new Exception("File contains a duplicate memory section.");
                            memory = new MemorySection(reader);
                            break;
                        case (int)WebAssemblyModuleID.Global:
                            if (strict_parse && global != null) throw new Exception("File contains a duplicate global section.");
                            global = new GlobalSection(reader);
                            break;
                        case (int)WebAssemblyModuleID.Export:
                            if (strict_parse && export != null) throw new Exception("File contains a duplicate export section.");
                            export = new ExportSection(reader);
                            break;
                        case (int)WebAssemblyModuleID.Start:
                            if (strict_parse && start != null) throw new Exception("File contains a duplicate start section.");
                            start = new StartSection(reader);
                            break;
                        case (int)WebAssemblyModuleID.Element:
                            if (strict_parse && element != null) throw new Exception("File contains a duplicate element section.");
                            element = new ElementSection(reader);
                            break;
                        case (int)WebAssemblyModuleID.Code:
                            if (strict_parse && code != null) throw new Exception("File contains a duplicate code section.");
                            code = new CodeSection(reader);
                            break;
                        case (int)WebAssemblyModuleID.Data:
                            if (strict_parse && data != null) throw new Exception("File contains a duplicate data section.");
                            data = new DataSection(reader);
                            break;

                        // Error
                        default:
                            throw new Exception($"Unknown section {id}.");
                    }
                }

                /* Additional validation */

                // The lengths of vectors produced by the (possibly empty) function and code section must match up.
                if ((function != null && code == null) || (function == null && code != null)) throw new Exception("File corrupt. Must include both function and code sections.");
                if (function.types.Length != code.bodies.Length) throw new Exception("File corrupt. Function and code sections do not match up.");

                // TODO: I don't actually check if data overlaps

                // TODO: Validate everything in this list
                // https://webassembly.github.io/spec/core/valid/modules.html
            }
        }

        public void SaveAsWAT(Stream stream)
        {
            BinaryWriter writer = new BinaryWriter(stream);

            writer.Write("(module \n");

            custom?.SaveAsWAT(writer);
            type?.SaveAsWAT(writer);
            import?.SaveAsWAT(writer);
            function?.SaveAsWAT(writer);
            table?.SaveAsWAT(writer);
            memory?.SaveAsWAT(writer);
            global?.SaveAsWAT(writer);
            export?.SaveAsWAT(writer);
            start?.SaveAsWAT(writer);
            element?.SaveAsWAT(writer);
            code?.SaveAsWAT(writer);
            data?.SaveAsWAT(writer);

            writer.Write(')');
        }

        public void SaveAsWAT(string filename)
        {
            using (FileStream fs = new FileStream(filename, FileMode.Create, FileAccess.Write))
            {
                SaveAsWAT(fs);
            }
        }

        public void SaveAsWASM(Stream stream)
        {
            BinaryWriter writer = new BinaryWriter(stream);

            writer.Write(MAGIC);
            writer.Write(SUPPORTED_VERSION);

            custom?.SaveAsWASM(writer);
            type?.SaveAsWASM(writer);
            import?.SaveAsWASM(writer);
            function?.SaveAsWASM(writer);
            table?.SaveAsWASM(writer);
            memory?.SaveAsWASM(writer);
            global?.SaveAsWASM(writer);
            export?.SaveAsWASM(writer);
            start?.SaveAsWASM(writer);
            element?.SaveAsWASM(writer);
            code?.SaveAsWASM(writer);
            data?.SaveAsWASM(writer);
        }

        public void SaveAsWASM(string filename)
        {
            using (FileStream fs = new FileStream(filename, FileMode.Create, FileAccess.Write))
            {
                SaveAsWASM(fs);
            }
        }

        public uint BinarySize()
        {
            logger.ConditionalTrace($"Custom Size: {0}", custom?.BinarySize() ?? 0);
            logger.ConditionalTrace($"Type Size: {0}", type?.BinarySize() ?? 0);
            logger.ConditionalTrace($"Import Size: {0}", import?.BinarySize() ?? 0);
            logger.ConditionalTrace($"Function Size: {0}", function?.BinarySize() ?? 0);
            logger.ConditionalTrace($"Table Size: {0}", table?.BinarySize() ?? 0);
            logger.ConditionalTrace($"Memory Size: {0}", memory?.BinarySize() ?? 0);
            logger.ConditionalTrace($"Global Size: {0}", global?.BinarySize() ?? 0);
            logger.ConditionalTrace($"Export Size: {0}", export?.BinarySize() ?? 0);
            logger.ConditionalTrace($"Start Size: {0}", start?.BinarySize() ?? 0);
            logger.ConditionalTrace($"Element Size: {0}", element?.BinarySize() ?? 0);
            logger.ConditionalTrace($"Code Size: {0}", code?.BinarySize() ?? 0);
            logger.ConditionalTrace($"Data Size: {0}", data?.BinarySize() ?? 0);

            uint size = sizeof(uint) +
                        sizeof(uint) +
                        (custom?.BinarySize() ?? 0)   +
                        (type?.BinarySize() ?? 0)     +
                        (import?.BinarySize() ?? 0)   +
                        (function?.BinarySize() ?? 0) +
                        (table?.BinarySize() ?? 0)    +
                        (memory?.BinarySize() ?? 0)   +
                        (global?.BinarySize() ?? 0)   +
                        (export?.BinarySize() ?? 0)   +
                        (start?.BinarySize() ?? 0)    +
                        (element?.BinarySize() ?? 0)  +
                        (code?.BinarySize() ?? 0)     +
                        (data?.BinarySize() ?? 0);

            logger.Debug($"Binary Size = {0}", size);

            return size;
        }

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();

            sb.AppendFormat("({0} ", GetType().Name);
            if (custom != null) sb.AppendFormat("{0} ", custom);
            if (type != null) sb.AppendFormat("{0} ", type);
            if (import != null) sb.AppendFormat("{0} ", import);
            if (function != null) sb.AppendFormat("{0} ", function);
            if (table != null) sb.AppendFormat("{0} ", table);
            if (memory != null) sb.AppendFormat("{0} ", memory);
            if (global != null) sb.AppendFormat("{0} ", global);
            if (export != null) sb.AppendFormat("{0} ", export);
            if (start != null) sb.AppendFormat("{0} ", start);
            if (element != null) sb.AppendFormat("{0} ", element);
            if (code != null) sb.AppendFormat("{0} ", code);
            if (data != null) sb.AppendFormat("{0} ", data);
            sb.Append(')');

            return sb.ToString();
        }
    }
}
