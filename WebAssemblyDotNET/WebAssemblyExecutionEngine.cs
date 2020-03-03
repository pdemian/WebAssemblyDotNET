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

        public long actual_size
        {
            get
            {
                return current * page_size;
            }
        }

        public WASMMemoryManager(long current, long maximum, long page_size = 64 * 1024)
        {
            this.current = current;
            this.maximum = maximum;
            this.page_size = page_size;
            memory = new byte[current * page_size];
        }

        public bool grow_memory(uint num_pages)
        {
            long ideal_size = current + num_pages * page_size;

            if (ideal_size > maximum) return false;

            byte[] new_mem = null;
            try
            {
                new_mem = new byte[ideal_size];
            }
            catch (Exception)
            {
                return false;
            }

            Array.Copy(memory, new_mem, ideal_size);

            memory = new_mem;

            return true;
        }
    }

    public enum WASMStackObjectType
    {
        Empty,
        ActivationObject,
        LabelObject,
        ValueObject
    }

    public class WASMActivationObject
    {
        public uint pc;
        public FunctionInstance function;
        public WASMValueObject[] parameters;
        public WASMValueObject[] locals;

        public WASMActivationObject(FunctionInstance function, WASMValueObject[] parameters)
        {
            if(parameters == null)
            {
                if (function.parameters.Length != 0) throw new Exception("Corrupt Code");
            }
            else if (!Enumerable.SequenceEqual(parameters.Select(x => x.type), function.parameters))
            {
                throw new Exception("Corrupt Code");
            }

            this.function = function;
            this.parameters = parameters;
            locals = new WASMValueObject[function.locals?.Length ?? 0];

            for (uint i = 0; i < (locals?.Length ?? 0); i++)
            {
                switch(function.locals[i])
                {
                    case WASMType.i32:
                        locals[i] = new WASMValueObject(default(uint));
                        break;
                    case WASMType.i64:
                        locals[i] = new WASMValueObject(default(ulong));
                        break;
                    case WASMType.f32:
                        locals[i] = new WASMValueObject(default(float));
                        break;
                    case WASMType.f64:
                        locals[i] = new WASMValueObject(default(double));
                        break;
                    default:
                        throw new Exception("Corrupt Code");
                }
            }

            pc = 0;
        }
    }

    public class WASMLabelObject
    {
        public uint arity;
        public uint branch_target;

        public WASMLabelObject(uint arity, uint branch_target)
        {
            this.arity = arity;
            this.branch_target = branch_target;
        }
    }

    public class WASMValueObject 
    {
        public WASMType type;
        public object value;

        public WASMValueObject(uint value)
        {
            type = WASMType.i32;
            this.value = value;
        }

        public WASMValueObject(ulong value)
        {
            type = WASMType.i64;
            this.value = value;
        }

        public WASMValueObject(float value)
        {
            type = WASMType.f32;
            this.value = value;
        }

        public WASMValueObject(double value)
        {
            type = WASMType.f64;
            this.value = value;
        }

        public uint AsI32()
        {
            return (uint)value;
        }

        public ulong AsI64()
        {
            return (ulong)value;
        }

        public float AsF32()
        {
            return (float)value;
        }

        public double AsF64()
        {
            return (double)value;
        }
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
        public long size => mem.actual_size;
        public long pages => mem.current;

        public bool GrowMemory(uint num_pages) => mem.grow_memory(num_pages);

        public MemoryInstance(uint initial, uint? maximum)
        {
            mem = new WASMMemoryManager(initial, maximum ?? uint.MaxValue);
        }
    }

    public class GlobalInstance
    {
        public bool is_mutable;
        public WASMType type;
        public WASMValueObject value;

        public GlobalInstance(bool is_mutable, WASMType type, WASMValueObject value)
        {
            this.is_mutable = is_mutable;
            this.type = type;
            this.value = value;
        }
    }

    public class TableInstance
    {
        public WASMType type;
        private WASMMemoryManager mem;

        public byte[] memory => mem.memory;
        public long size => mem.actual_size;

        public bool GrowMemory(uint num_pages) => mem.grow_memory(num_pages);

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

        public WebAssemblyExecutionContext(WASMFile file)
        {
            if (file.table != null) InitTable(file);
            if (file.global != null) InitGlobal(file);
            if (file.memory != null) InitMemory(file);
            if (file.data != null) InitData(file);
            if (file.import != null) InitImport(file);
            InitFunctions(file);
            if (file.export != null) InitExport(file);
            
            // TODO: InitElement(ctx);
        }

        private void InitTable(WASMFile file)
        {
            for (uint i = 0; i < (uint)file.table.entries.Length; i++)
            {
                var entry = file.table.entries[i];

                tables[i] = new TableInstance(entry.element_type, entry.limits.initial, entry.limits.maximum);
            }
        }
        private void InitGlobal(WASMFile file)
        {
            for (uint i = 0; i < (uint)file.global.globals.Length; i++)
            {
                var global = file.global.globals[i];

                globals[i] = new GlobalInstance(global.type.mutability, global.type.content_type, null);
            }
        }
        private void InitMemory(WASMFile file)
        {
            for (uint i = 0; i < (uint)file.memory.entries.Length; i++)
            {
                var limits = file.memory.entries[i].limits;

                linear_memory[i] = new MemoryInstance(limits.initial, limits.maximum);
            }
        }
        private void InitData(WASMFile file)
        {
            foreach (var entry in file.data.entries)
            {
                Array.Copy(entry.data, 0, linear_memory[entry.memory_index].memory, WebAssemblyHelper.GetOffset(entry.offset), entry.data.Length);
            }
        }
        private void InitExport(WASMFile file)
        {
            for (uint i = 0; i < (uint)file.export.entries.Length; i++)
            {
                var entry = file.export.entries[i];

                switch (entry.kind)
                {
                    case WASMExternalKind.Function:
                        var index = entry.index;
                        functions[index].is_export = true;
                        functions[index].name = entry.field_str;
                        break;
                }
            }
        }
        private void InitImport(WASMFile file)
        {
            Dictionary<uint, FuncType> type_info = new Dictionary<uint, FuncType>();

            for (uint i = 0; i < (uint)file.type.entries.Length; i++)
            {
                type_info[i] = file.type.entries[i];
            }

            for (uint i = 0; i < (uint)file.import.entries.Length; i++)
            {
                var entry = file.import.entries[i];

                switch (entry.kind)
                {
                    case WASMExternalKind.Function:
                        uint index = (uint)entry.type;

                        functions[i] = new FunctionInstance
                        {
                            module = entry.module_str,
                            name = entry.field_str,
                            parameters = type_info[index].param_types,
                            return_type = type_info[index].return_type
                        };
                        break;
                }
            }
        }
        private void InitFunctions(WASMFile file)
        {
            Dictionary<uint, FuncType> type_info = new Dictionary<uint, FuncType>();

            for (uint i = 0; i < (uint)file.type.entries.Length; i++)
            {
                type_info[i] = file.type.entries[i];
            }

            if (file.function.types.Length != file.code.bodies.Length) throw new Exception("Invalid file");

            uint import_count = (uint)functions.Count;

            for (uint i = 0; i < (uint)file.code.bodies.Length; i++)
            {
                uint index = file.function.types[i];
                FunctionBody body = file.code.bodies[i];

                uint func_indx = i + import_count;

                functions[func_indx] = new FunctionInstance
                {
                    module = "this",
                    is_in_module = true,
                    code = body.code,
                    parameters = type_info[index].param_types,
                    return_type = type_info[index].return_type,
                };

                List<WASMType> locals_unwrapped = new List<WASMType>();

                foreach (var local in body.locals)
                {
                    locals_unwrapped.AddRange(Enumerable.Repeat(local.type, (int)local.count));
                }

                functions[i].locals = locals_unwrapped.ToArray();
            }
        }

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

        public WASMActivationObject GetCurrentFunction()
        {
            return callstack.Peek();
        }

        public WASMStackObjectType Peek()
        {
            if (stack_objects.Count == 0) return WASMStackObjectType.Empty;
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

    public class WebAssemblyTrap : Exception
    {
        public WebAssemblyTrap(string message) : base(message)
        {

        }

        public WebAssemblyTrap(string message, Exception innerException) : base(message, innerException)
        {

        }
    }

    public class WebAssemblyInterpreter
    {
        public readonly WASMFile file;

        public WebAssemblyInterpreter(WASMFile file)
        {
            this.file = file;
        }

        private void InitVariables(WebAssemblyExecutionContext ctx)
        {
            if(file.data != null)
            {
                // TODO: Should data be initialized here?
                // I don't think so as it's only supposed to be an i32.const
            }

            // TODO: Element

            if (file.global != null)
            {
                for (uint i = 0; i < (uint)file.global.globals.Length; i++)
                {
                    var global = file.global.globals[i];

                    ctx.globals[i].value = WebAssemblyHelper.GetInitExpr(global.init, ctx.globals.Values.ToArray());
                }
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
                                ctx.Push(new WASMValueObject(0));
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

            WebAssemblyExecutionContext ctx = new WebAssemblyExecutionContext(file);

            ResolveExternalFunctions(ctx);
            InitVariables(ctx);

            uint start_func = file.start.index;
            if (!ctx.functions.ContainsKey(start_func)) throw new Exception("Corrupt file.");

            // Execute starting from the start_func
            var result = Invoke(ctx, start_func);

            // I'm not sure what valid result types are for main
            // What about void? Is that valid or is it simply 0?
            switch (result.type)
            {
                case WASMType.i32:
                    return (int)result.AsI32();
                case WASMType.i64:
                    return (int)(long)result.AsI64();
                default:
                    throw new Exception("Corrupt Return Value.");
            }
        }

        private WASMValueObject GetValueOrFail(WebAssemblyExecutionContext ctx)
        {
            if (ctx.Peek() != WASMStackObjectType.ValueObject)
            {
                Trap(ctx, "Function stack corrupt.");
            }
            return ctx.PopValue();
        }

        private WASMValueObject GetValueOfTypeOrFail(WebAssemblyExecutionContext ctx, WASMType expected_type)
        {
            WASMValueObject obj = GetValueOrFail(ctx);

            if(obj.type != expected_type)
            {
                Trap(ctx, "Function stack corrupt.");
            }

            return obj;
        }

        private (byte[] memory, int location) LoadOrFail(WebAssemblyExecutionContext ctx, byte[] code, uint size, ref uint pc)
        {
            // align is in bytes
            // it tells us how this value is aligned
            // in our implementation, we can disregard alignment
            int align = 1 << (int)LEB128.ReadUInt32(code, ref pc);
            uint offset = LEB128.ReadUInt32(code, ref pc);

            WASMValueObject i = GetValueOfTypeOrFail(ctx, WASMType.i32);
            long ea = i.AsI32() + offset;
            uint N = size/8;

            if ((ea + N) > ctx.linear_memory[0].size)
            {
                Trap(ctx, "Illegal memory access");
            }

            return (ctx.linear_memory[0].memory, (int)ea);
        }

        private (WASMValueObject value, byte[] memory, int location) StoreOrFail(WebAssemblyExecutionContext ctx, byte[] code, uint size, WASMType type, ref uint pc)
        {
            // align is in bytes
            // it tells us how this value is aligned
            // in our implementation, we can disregard alignment
            int align = 1 << (int)LEB128.ReadUInt32(code, ref pc);
            uint offset = LEB128.ReadUInt32(code, ref pc);

            WASMValueObject c = GetValueOfTypeOrFail(ctx, type);
            WASMValueObject i = GetValueOfTypeOrFail(ctx, WASMType.i32);
            long ea = i.AsI32() + offset;
            uint N = size / 8;

            if ((ea + N) > ctx.linear_memory[0].size)
            {
                Trap(ctx, "Illegal memory access");
            }

            return (c, ctx.linear_memory[0].memory, (int)ea);
        }

        private void Enter(WebAssemblyExecutionContext ctx, WASMLabelObject obj)
        {
            ctx.Push(obj);
        }

        private void Exit(WebAssemblyExecutionContext ctx)
        {
            // exit, no label required
        }

        private void Exit(WebAssemblyExecutionContext ctx, uint l)
        {
            if(ctx.labels.Count() < l + 1)
            {
                Trap(ctx, "Function corrupt");
            }

            // Stack appears to consider ElementAt from the front of the list rather than the back
            // So paradoxically, it actually counts up (0th element = latest, 1st = second, etc)
            WASMLabelObject label = ctx.labels.ElementAt((int)l);

            uint n = label.arity;

            /*
                Pop the values val^n from the stack.
                Repeat l+1 times:
                    While the top of the stack is a value, do:
                        Pop the value from the stack.
                    Assert: due to validation, the top of the stack now is a label.
                    Pop the label from the stack.
                Push the values val^n to the stack.
             */

            Stack<WASMValueObject> values = new Stack<WASMValueObject>();
            for(uint i = 0; i < n; i++)
            {
                values.Push(GetValueOrFail(ctx));
            }

            for(uint i = 0; i < l+1; i++)
            {
                while(ctx.Peek() == WASMStackObjectType.ValueObject)
                {
                    ctx.PopValue();
                }

                if(ctx.Peek() != WASMStackObjectType.LabelObject)
                {
                    Trap(ctx, "Corrupt function");
                }

                ctx.PopLabel();
            }

            for(uint i = 0; i < n; i++)
            {
                ctx.Push(values.Pop());
            }

            ctx.GetCurrentFunction().pc = label.branch_target;
        }

        private WASMValueObject Invoke(WebAssemblyExecutionContext ctx, uint func_id)
        {
            FunctionInstance func = ctx.functions[func_id];
            WASMValueObject[] parameters = new WASMValueObject[func.parameters.Length];

            // Gather parameters from stack
            for (int i = 0; i < parameters.Length; i++)
            {
                parameters[i] = GetValueOrFail(ctx);
            }

            // Create our activation object and push on to the stack
            WASMActivationObject activation_object = new WASMActivationObject(ctx.functions[func_id], parameters);
            ctx.Push(activation_object);

            // If we're external, run host code
            if (!func.is_in_module)
            {
                func.host_code(activation_object.parameters);
            }
            else
            {
                Execute(ctx);             
            }

            WASMValueObject result = GetValueOrFail(ctx);

            if(result.type != func.return_type)
            {
                Trap(ctx, "Function corrupt.");
            }

            // Remember to pop our own activation instance off the record!
            // Double check as well that it is in fact us
            if (ctx.Peek() != WASMStackObjectType.ActivationObject || activation_object != ctx.PopActivation())
            {
                Trap(ctx, "Function stack corrupt.");
            }

            return result;
        }

        private void Trap(WebAssemblyExecutionContext ctx, string error)
        {
            WASMActivationObject activation_object = ctx.GetCurrentFunction();
            FunctionInstance func = activation_object.function;

            throw new WebAssemblyTrap($"Trap occured at {func.module}.{func.name}@{activation_object.pc} with message: {error}");
        }

        // TODO: Can now transform Execute from a recursive function into an iterative function 
        // Each time a new "frame" is added, we just update activation_object and func
        // ctx records the current pc so when we go back
        // Simply need to inline the Invoke method into Execute
        private void Execute(WebAssemblyExecutionContext ctx)
        {
            try
            {
                WASMActivationObject activation_object = ctx.GetCurrentFunction();
                FunctionInstance func = activation_object.function;
                byte[] code = func.code;

                //WebAssemblyHelper.ReinterpretHelper reinterpret_helper = new WebAssemblyHelper.ReinterpretHelper();

                bool continue_executing = true;

                // https://webassembly.github.io/spec/core/exec/instructions.html
                while (continue_executing)
                {
                    switch ((WASMOpcodes)code[activation_object.pc])
                    {
                        case WASMOpcodes.UNREACHABLE:
                            Trap(ctx, "Unreachable reached!");
                            break;
                        case WASMOpcodes.NOP:
                            break;
                        case WASMOpcodes.END:
                            continue_executing = false;
                            break;
                        case WASMOpcodes.LOOP:
                            {
                                // ignore result for now
                                activation_object.pc++;
                                var result = (WASMType)code[activation_object.pc];

                                Enter(ctx, new WASMLabelObject(0, activation_object.pc-2));
                            }
                            break;
                        case WASMOpcodes.BR:
                            { 
                                uint l = LEB128.ReadUInt32(code, ref activation_object.pc);

                                Exit(ctx, l);
                            }
                            break;
                        case WASMOpcodes.BR_IF:
                            {
                                WASMValueObject c = GetValueOfTypeOrFail(ctx, WASMType.i32);
                                if(c.AsI32() != 0)
                                {
                                    goto case WASMOpcodes.BR;
                                }
                            }
                            break;
                        case WASMOpcodes.LOCAL_GET:
                            {
                                uint local = LEB128.ReadUInt32(code, ref activation_object.pc);
                                if (local > func.locals.Length) Trap(ctx, "Function corrupt.");

                                ctx.Push(activation_object.locals[local]);
                            }
                            break;
                        case WASMOpcodes.LOCAL_SET:
                            {
                                uint local = LEB128.ReadUInt32(code, ref activation_object.pc);
                                if (local > func.locals.Length) Trap(ctx, "Function corrupt.");

                                WASMValueObject val = GetValueOrFail(ctx);

                                activation_object.locals[local] = val;
                            }
                            break;
                        case WASMOpcodes.LOCAL_TEE:
                            {
                                WASMValueObject val = GetValueOrFail(ctx);
                                ctx.Push(val);
                                ctx.Push(val);
                            }
                            goto case WASMOpcodes.LOCAL_SET;
                        case WASMOpcodes.I32_CONST:
                            ctx.Push(new WASMValueObject(LEB128.ReadUInt32(code, ref activation_object.pc)));
                            break;
                        case WASMOpcodes.I64_CONST:
                            ctx.Push(new WASMValueObject(LEB128.ReadUInt64(code, ref activation_object.pc)));
                            break;
                        case WASMOpcodes.F32_CONST:
                            ctx.Push(new WASMValueObject(BitConverter.ToSingle(code, (int)activation_object.pc+1)));
                            activation_object.pc += 4;
                            break;
                        case WASMOpcodes.F64_CONST:
                            ctx.Push(new WASMValueObject(BitConverter.ToDouble(code, (int)activation_object.pc+1)));
                            activation_object.pc += 7;
                            break;
                        case WASMOpcodes.I32_LOAD:
                            {
                                var (memory, location) = LoadOrFail(ctx, code, 32, ref activation_object.pc);

                                ctx.Push(new WASMValueObject(BitConverter.ToUInt32(memory, location)));
                            }
                            break;
                        case WASMOpcodes.I64_LOAD:
                            {
                                var (memory, location) = LoadOrFail(ctx, code, 64, ref activation_object.pc);

                                ctx.Push(new WASMValueObject(BitConverter.ToUInt64(memory, location)));
                            }
                            break;
                        case WASMOpcodes.F32_LOAD:
                            {
                                var (memory, location) = LoadOrFail(ctx, code, 32, ref activation_object.pc);

                                ctx.Push(new WASMValueObject(BitConverter.ToSingle(memory, location)));
                            }
                            break;
                        case WASMOpcodes.F64_LOAD:
                            {
                                var (memory, location) = LoadOrFail(ctx, code, 64, ref activation_object.pc);

                                ctx.Push(new WASMValueObject(BitConverter.ToDouble(memory, location)));
                            }
                            break;
                        case WASMOpcodes.I32_LOAD8_S:
                            {
                                var (memory, location) = LoadOrFail(ctx, code, 8, ref activation_object.pc);

                                ctx.Push(new WASMValueObject((uint)(sbyte)memory[location]));
                            }
                            break;
                        case WASMOpcodes.I32_LOAD8_U:
                            {
                                var (memory, location) = LoadOrFail(ctx, code, 8, ref activation_object.pc);

                                ctx.Push(new WASMValueObject((uint)memory[location]));
                            }
                            break;
                        case WASMOpcodes.I32_LOAD16_S:
                            {
                                var (memory, location) = LoadOrFail(ctx, code, 16, ref activation_object.pc);

                                ctx.Push(new WASMValueObject((uint)BitConverter.ToInt16(memory, (int)activation_object.pc)));
                            }
                            break;
                        case WASMOpcodes.I32_LOAD16_U:
                            {
                                var (memory, location) = LoadOrFail(ctx, code, 16, ref activation_object.pc);

                                ctx.Push(new WASMValueObject(BitConverter.ToUInt16(memory, (int)activation_object.pc)));
                            }
                            break;
                        case WASMOpcodes.I64_LOAD8_S:
                            {
                                var (memory, location) = LoadOrFail(ctx, code, 8, ref activation_object.pc);

                                ctx.Push(new WASMValueObject((ulong)(sbyte)memory[location]));
                            }
                            break;
                        case WASMOpcodes.I64_LOAD8_U:
                            {
                                var (memory, location) = LoadOrFail(ctx, code, 8, ref activation_object.pc);

                                ctx.Push(new WASMValueObject((ulong)memory[location]));
                            }
                            break;
                        case WASMOpcodes.I64_LOAD16_S:
                            {
                                var (memory, location) = LoadOrFail(ctx, code, 16, ref activation_object.pc);

                                ctx.Push(new WASMValueObject((ulong)BitConverter.ToInt16(memory, (int)activation_object.pc)));
                            }
                            break;
                        case WASMOpcodes.I64_LOAD16_U:
                            {
                                var (memory, location) = LoadOrFail(ctx, code, 16, ref activation_object.pc);

                                ctx.Push(new WASMValueObject((ulong)BitConverter.ToUInt16(memory, (int)activation_object.pc)));
                            }
                            break;
                        case WASMOpcodes.I64_LOAD32_S:
                            {
                                var (memory, location) = LoadOrFail(ctx, code, 32, ref activation_object.pc);

                                ctx.Push(new WASMValueObject((ulong)BitConverter.ToInt32(memory, (int)activation_object.pc)));
                            }
                            break;
                        case WASMOpcodes.I64_LOAD32_U:
                            {
                                var (memory, location) = LoadOrFail(ctx, code, 32, ref activation_object.pc);

                                ctx.Push(new WASMValueObject((ulong)BitConverter.ToUInt32(memory, (int)activation_object.pc)));
                            }
                            break;
                        case WASMOpcodes.I32_STORE:
                            {
                                var (value, memory, location) = StoreOrFail(ctx, code, 32, WASMType.i32, ref activation_object.pc);

                                Array.Copy(BitConverter.GetBytes(value.AsI32()), 0, memory, location, 32/8);
                            }
                            break;
                        case WASMOpcodes.I64_STORE:
                            {
                                var (value, memory, location) = StoreOrFail(ctx, code, 64, WASMType.i64, ref activation_object.pc);

                                Array.Copy(BitConverter.GetBytes(value.AsI64()), 0, memory, location, 64/8);
                            }
                            break;
                        case WASMOpcodes.F32_STORE:
                            {
                                var (value, memory, location) = StoreOrFail(ctx, code, 32, WASMType.f32, ref activation_object.pc);

                                Array.Copy(BitConverter.GetBytes(value.AsF32()), 0, memory, location, 32/8);
                            }
                            break;
                        case WASMOpcodes.F64_STORE:
                            {
                                var (value, memory, location) = StoreOrFail(ctx, code, 64, WASMType.f64, ref activation_object.pc);

                                Array.Copy(BitConverter.GetBytes(value.AsF64()), 0, memory, location, 64/8);
                            }
                            break;
                        case WASMOpcodes.I32_STORE8:
                            {
                                var (value, memory, location) = StoreOrFail(ctx, code, 32, WASMType.i32, ref activation_object.pc);

                                memory[location] = (byte)value.AsI32();
                            }
                            break;
                        case WASMOpcodes.I32_STORE16:
                            {
                                var (value, memory, location) = StoreOrFail(ctx, code, 32, WASMType.i32, ref activation_object.pc);

                                Array.Copy(BitConverter.GetBytes((ushort)value.AsI32()), 0, memory, location, 16/8);
                            }
                            break;
                        case WASMOpcodes.I64_STORE8:
                            {
                                var (value, memory, location) = StoreOrFail(ctx, code, 64, WASMType.i64, ref activation_object.pc);

                                memory[location] = (byte)value.AsI64();
                            }
                            break;
                        case WASMOpcodes.I64_STORE16:
                            {
                                var (value, memory, location) = StoreOrFail(ctx, code, 64, WASMType.i64, ref activation_object.pc);

                                Array.Copy(BitConverter.GetBytes((ushort)value.AsI64()), 0, memory, location, 16/8);
                            }
                            break;
                        case WASMOpcodes.I64_STORE32:
                            {
                                var (value, memory, location) = StoreOrFail(ctx, code, 64, WASMType.i64, ref activation_object.pc);

                                Array.Copy(BitConverter.GetBytes((uint)value.AsI64()), 0, memory, location, 32/8);
                            }
                            break;
                        case WASMOpcodes.MEMORY_SIZE:
                            ctx.Push(new WASMValueObject((uint)ctx.linear_memory[0].pages));
                            break;
                        case WASMOpcodes.MEMORY_GROW:
                            {
                                WASMValueObject n = GetValueOfTypeOrFail(ctx, WASMType.i32);

                                if (ctx.linear_memory[0].GrowMemory(n.AsI32()))
                                {
                                    ctx.Push(n);
                                }
                                else
                                {
                                    ctx.Push(new WASMValueObject(unchecked((uint)-1)));
                                }
                            }
                            break;
                        case WASMOpcodes.CALL:
                            {
                                uint new_func = LEB128.ReadUInt32(code, ref activation_object.pc);

                                ctx.Push(Invoke(ctx, new_func));
                            }
                            break;
                        case WASMOpcodes.DROP:
                            GetValueOrFail(ctx);
                            break;
                        case WASMOpcodes.SELECT:
                            {
                                WASMValueObject c = GetValueOfTypeOrFail(ctx, WASMType.i32);
                                WASMValueObject val2 = GetValueOrFail(ctx);
                                WASMValueObject val1 = GetValueOrFail(ctx);

                                ctx.Push(c.AsI32() != 0 ? val1 : val2);
                            }
                            break;
                        default:
                            Trap(ctx, $"Unknown Opcode {code[activation_object.pc]}.");
                            break;
                    }
                    activation_object.pc++;
                }
            }
            catch(WebAssemblyTrap)
            {
                throw;
            }
            catch(Exception ex)
            {
                Trap(ctx, "Unexpected Error: " + ex.Message);
            }
        }
    }
}
