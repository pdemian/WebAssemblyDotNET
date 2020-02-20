using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using WebAssemblyDotNET.Components;

namespace WebAssemblyDotNET
{
    internal class WASMMemoryManager
    {
        public long current;
        public long maximum;
        public long page_size;
        public byte[] memory;

        public WASMMemoryManager(long current, long maximum, long page_size = 64 * 1024)
        {
            this.current = current;
            this.maximum = maximum;
            this.page_size = page_size;
            memory = new byte[current * page_size];
        }

        public int grow_memory(uint num_pages)
        {
            long ideal_size = current + num_pages * page_size;

            if (ideal_size > maximum) return -1;

            byte[] new_mem = null;
            try
            {
                new_mem = new byte[ideal_size];
            }
            catch (Exception)
            {
                throw;
            }

            Array.Copy(memory, new_mem, ideal_size);

            memory = new_mem;

            return 0;
        }
    }

    public enum WASMStackObjectType
    {
        ActivationObject,
        LabelObject,
        ValueObject
    }

    public class WASMActivationObject
    {
        public FunctionInstance function;
        public WASMValueObject[] parameters;
        public WASMValueObject[] locals;

        public WASMActivationObject(FunctionInstance function, WASMValueObject[] parameters)
        {
            if (!Enumerable.SequenceEqual(parameters.Select(x => x.type), function.parameters))
            {
                throw new Exception("Corrupt Code");
            }

            this.function = function;
            this.parameters = parameters;
            locals = new WASMValueObject[function.locals?.Length ?? 0];

            for (uint i = 0; i < (locals?.Length ?? 0); i++)
            {
                locals[i] = new WASMValueObject { type = function.locals[i], value = null };
            }
        }
    }

    public class WASMLabelObject
    {
        // Labels carry an arity...for some reason
        // uint n;
        public uint branch_target;
    }

    public class WASMValueObject 
    {
        public WASMType type;
        public object value;
    }

    public class FunctionInstance
    {
        public bool is_in_module = false;
        public bool is_export = false;
        public string module = null;
        public string name = null;
        public WASMType? return_type = null;
        public WASMType[] parameters = null;
        public WASMType[] locals = null;
        public byte[] code = null;
        public Action<WASMValueObject[]> host_code;
    }
    
    public class MemoryInstance
    {
        private WASMMemoryManager mem;

        public byte[] memory => mem.memory;

        public int GrowMemory(uint num_pages) => mem.grow_memory(num_pages);

        public MemoryInstance(uint initial, uint? maximum)
        {
            mem = new WASMMemoryManager(initial, maximum ?? uint.MaxValue);
        }
    }

    public class GlobalInstance
    {
        public bool is_mutable;
        public WASMType type;
        public InitExpr init_expr;

        public GlobalInstance(bool is_mutable, WASMType type, InitExpr init_expr)
        {
            this.is_mutable = is_mutable;
            this.type = type;
            this.init_expr = init_expr;
        }
    }

    public class TableInstance
    {
        public WASMType type;
        private WASMMemoryManager mem;

        public byte[] memory => mem.memory;

        public int GrowMemory(uint num_pages) => mem.grow_memory(num_pages);

        public TableInstance(WASMType type, uint initial, uint? maximum)
        {
            this.type = type;
            mem = new WASMMemoryManager(initial, maximum ?? uint.MaxValue);
        }
    }

    public class WebAssemblyExecutionContext
    {
        // https://webassembly.github.io/spec/core/exec/runtime.html#store
        public readonly Dictionary<uint, FunctionInstance> functions = new Dictionary<uint, FunctionInstance>();
        public readonly Dictionary<uint, MemoryInstance> linear_memory = new Dictionary<uint, MemoryInstance>();
        public readonly Dictionary<uint, GlobalInstance> globals = new Dictionary<uint, GlobalInstance>();
        public readonly Dictionary<uint, TableInstance> tables = new Dictionary<uint, TableInstance>();

        // A stack can contain the following 3 types of objects
        public readonly Stack<WASMActivationObject> callstack = new Stack<WASMActivationObject>();
        public readonly Stack<WASMLabelObject> labels = new Stack<WASMLabelObject>();
        public readonly Stack<WASMValueObject> values = new Stack<WASMValueObject>();
        // For performance reasons, store their types rather than cast the top of the stack
        public readonly Stack<WASMStackObjectType> stack_objects = new Stack<WASMStackObjectType>();

        public void Push(WASMActivationObject obj)
        {
            stack_objects.Push(WASMStackObjectType.ActivationObject);
            callstack.Push(obj);
        }
        public void Push(WASMLabelObject obj)
        {
            stack_objects.Push(WASMStackObjectType.LabelObject);
            labels.Push(obj);
        }
        public void Push(WASMValueObject obj)
        {
            stack_objects.Push(WASMStackObjectType.ValueObject);
            values.Push(obj);
        }

        public WASMStackObjectType Peek()
        {
            return stack_objects.Peek();
        }
        public WASMActivationObject PopActivation()
        {
            if (stack_objects.Pop() != WASMStackObjectType.ActivationObject) throw new Exception("Library error!");
            return callstack.Pop();
        }
        public WASMLabelObject PopLabel()
        {
            if (stack_objects.Pop() != WASMStackObjectType.LabelObject) throw new Exception("Library error!");
            return labels.Pop();
        }
        public WASMValueObject PopValue()
        {
            if (stack_objects.Pop() != WASMStackObjectType.ValueObject) throw new Exception("Library error!");
            return values.Pop();
        }
    }

    internal class WASMEnvironmentCalls
    {
        [DllImport("msvcrt.dll", CallingConvention = CallingConvention.Cdecl)]
        public static unsafe extern int puts(byte* str);
    }

    public class WebAssemblyInterpreter
    {
        public readonly WASMFile file;

        public WebAssemblyInterpreter(WASMFile file)
        {
            this.file = file;
        }

        private void InitTable(WebAssemblyExecutionContext ctx)
        {
            for (uint i = 0; i < (uint)file.table.entries.Length; i++)
            {
                var entry = file.table.entries[i];

                ctx.tables[i] = new TableInstance(entry.element_type, entry.limits.initial, entry.limits.maximum);
            }
        }

        private void InitGlobal(WebAssemblyExecutionContext ctx)
        {
            for (uint i = 0; i < (uint)file.global.globals.Length; i++)
            {
                var global = file.global.globals[i];

                ctx.globals[i] = new GlobalInstance(global.type.mutability, global.type.content_type, global.init);
            }
        }

        private void InitMemory(WebAssemblyExecutionContext ctx)
        {
            for (uint i = 0; i < (uint)file.memory.entries.Length; i++)
            {
                var limits = file.memory.entries[i].limits;

                ctx.linear_memory[i] = new MemoryInstance(limits.initial, limits.maximum);
            }
        }

        private void InitData(WebAssemblyExecutionContext ctx)
        {
            foreach (var entry in file.data.entries)
            {
                Array.Copy(entry.data, 0, ctx.linear_memory[entry.memory_index].memory, WebAssemblyHelper.GetOffset(entry.offset), entry.data.Length);
            }
        }

        private void InitType(WebAssemblyExecutionContext ctx)
        {
            for (uint i = 0; i < (uint)file.type.entries.Length; i++)
            {
                var entry = file.type.entries[i];

                ctx.functions[i] = new FunctionInstance
                {
                    return_type = entry.return_type,
                    parameters = entry.param_types
                };
            }
        }

        private void InitExport(WebAssemblyExecutionContext ctx)
        {
            for (uint i = 0; i < (uint)file.export.entries.Length; i++)
            {
                var entry = file.export.entries[i];

                switch (entry.kind)
                {
                    case WASMExternalKind.Function:
                        var index = entry.index;
                        ctx.functions[index].is_export = true;
                        ctx.functions[index].name = entry.field_str;
                        break;
                }
            }
        }

        private void InitImport(WebAssemblyExecutionContext ctx)
        {
            for (uint i = 0; i < (uint)file.import.entries.Length; i++)
            {
                var entry = file.import.entries[i];

                switch (entry.kind)
                {
                    case WASMExternalKind.Function:
                        var index = (uint)entry.type;
                        ctx.functions[index].module = entry.module_str;
                        ctx.functions[index].name = entry.field_str;
                        break;
                }
            }
        }

        private void InitFunctions(WebAssemblyExecutionContext ctx)
        {
            if (file.function.types.Length != file.code.bodies.Length) throw new Exception("Invalid file");

            for (uint i = 0; i < (uint)file.code.bodies.Length; i++)
            {
                var index = file.function.types[i];
                var body = file.code.bodies[i];

                ctx.functions[index].module = string.Empty;
                ctx.functions[index].is_in_module = true;
                ctx.functions[index].code = body.code;

                var locals_unwrapped = new List<WASMType>();

                foreach (var local in body.locals)
                {
                    locals_unwrapped.AddRange(Enumerable.Repeat(local.type, (int)local.count));
                }

                ctx.functions[index].locals = locals_unwrapped.ToArray();
            }
        }

        private void ResolveExternalFunctions(WebAssemblyExecutionContext ctx)
        {
            foreach (var func in ctx.functions)
            {
                if (!func.Value.is_in_module)
                {
                    switch (func.Value.name)
                    {
                        case "puts":
                            func.Value.host_code = (args) => {
                                unsafe
                                {
                                    fixed (byte* arr = ctx.linear_memory[0].memory)
                                        WASMEnvironmentCalls.puts(arr + (int)args[0].value);
                                }
                                ctx.Push(new WASMValueObject { type = WASMType.i32, value = 0 });
                            };
                            break;
                        default:
                            throw new Exception("Failed to bind environment function '" + func.Value.name + "'");
                    }
                }
            }
        }

        public int Run()
        {
            if (file.type == null) throw new Exception("No type information!");
            if (file.function == null) throw new Exception("No type information!");
            if (file.code == null) throw new Exception("No code to execute!");
            if (file.start == null) throw new Exception("No start function!");

            WebAssemblyExecutionContext ctx = new WebAssemblyExecutionContext();

            if (file.table != null) InitTable(ctx);
            if (file.global != null) InitGlobal(ctx);
            if (file.memory != null) InitMemory(ctx);
            if (file.data != null) InitData(ctx);
            InitType(ctx);
            InitExport(ctx);
            InitImport(ctx);
            // TODO: InitElement(ctx);
            InitFunctions(ctx);
            ResolveExternalFunctions(ctx);

            uint start_func = file.start.index;
            if (!ctx.functions.ContainsKey(start_func)) throw new Exception("Corrupt file.");

            // Execute starting from the start_func
            var result = Execute(ctx, start_func);

            // I'm not sure what valid result types are for main
            // What about void? Is that valid or is it simply 0?
            switch (result.type)
            {
                case WASMType.i32:
                    return (int)result.value;
                case WASMType.i64:
                    return (int)(long)result.value;
                default:
                    throw new Exception("Corrupt Return Value.");
            }
        }

        private WASMValueObject Execute(WebAssemblyExecutionContext ctx, uint func_id)
        {
            var func = ctx.functions[func_id];
            var parameters = new WASMValueObject[func.parameters.Length];

            // Gather parameters from stack
            for (int i = 0; i < parameters.Length; i++)
            {
                if (ctx.Peek() != WASMStackObjectType.ValueObject)
                    throw new Exception("Function stack corrupt.");
                parameters[i] = ctx.PopValue();
            }

            // Create our activation object and push on to the stack
            var activation_object = new WASMActivationObject(ctx.functions[func_id], parameters);
            ctx.Push(activation_object);

            // If we're external, run host code
            if (!func.is_in_module)
            {
                func.host_code(parameters);
            }
            else
            {
                bool continue_executing = true;
                uint pc = 0;

                while (continue_executing)
                {
                    switch ((WASMOpcodes)func.code[pc])
                    {
                        case WASMOpcodes.UNREACHABLE:
                            throw new Exception("Unreachable reached!");
                        case WASMOpcodes.NOP:
                            break;
                        case WASMOpcodes.END:
                            continue_executing = false;
                            break;
                        case WASMOpcodes.I32_CONST:
                            // read var_int and push to stack
                            pc++;
                            ctx.Push(new WASMValueObject { type = WASMType.i32, value = LEB128.ReadInt32(func.code, ref pc) });
                            break;
                        case WASMOpcodes.CALL:
                            // read var_int and call with stack
                            pc++;
                            uint new_func = LEB128.ReadUInt32(func.code, ref pc);

                            ctx.Push(Execute(ctx, new_func));
                            break;
                        case WASMOpcodes.DROP:
                            // drop last value on stack
                            if (ctx.Peek() != WASMStackObjectType.ValueObject)
                            {
                                throw new Exception("Function stack corrupt.");
                            }
                            ctx.PopValue();
                            break;
                    }
                    pc++;
                }
            }

            if (ctx.Peek() != WASMStackObjectType.ValueObject)
            {
                throw new Exception("Function stack corrupt.");
            }

            var result = ctx.PopValue();

            // Remember to pop our own activation instance off the record!
            // Double check as well that it is in fact us
            if (ctx.Peek() != WASMStackObjectType.ActivationObject || activation_object != ctx.PopActivation())
            {
                throw new Exception("Function stack corrupt.");
            }

            return result;
        }
    }
}
