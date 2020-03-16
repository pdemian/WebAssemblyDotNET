using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using WebAssemblyDotNET.Components;
using NLog;

namespace WebAssemblyDotNET
{
    internal class WebAssemblyMemoryManager
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();

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

        public WebAssemblyMemoryManager(long current, long maximum, long page_size = 64 * 1024)
        {
            this.current = current;
            this.maximum = maximum;
            this.page_size = page_size;
            memory = new byte[current * page_size];
        }

        public bool grow_memory(uint num_pages)
        {
            logger.Debug("Growing memory.");
            logger.ConditionalTrace($"Current num_pages = {current}, new is {num_pages}.");

            long ideal_size = current + num_pages * page_size;

            logger.ConditionalTrace($"{ideal_size} > {maximum} = {ideal_size > maximum}.");

            if (ideal_size > maximum) return false;

            byte[] new_mem = null;
            try
            {
                new_mem = new byte[ideal_size];
            }
            catch (Exception)
            {
                logger.Debug("Out of memory.");
                return false;
            }

            Array.Copy(memory, new_mem, ideal_size);

            memory = new_mem;

            logger.ConditionalTrace("Successfully grown memory.");
            return true;
        }
    }

    internal enum WebAssemblyStackObjectType
    {
        Empty,
        ActivationObject,
        LabelObject,
        ValueObject
    }

    internal class ActivationObject
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();

        public uint pc;
        public FunctionInstance function;
        public ValueObject[] parameters;
        public ValueObject[] locals;

        public ActivationObject(FunctionInstance function, ValueObject[] parameters)
        {
            logger.Debug($"Expected parameters: {function.parameters.Length}, Actual: {(parameters == null ? 0 : parameters.Length)}.");

            if (parameters == null)
            {
                if (function.parameters.Length != 0) throw new Exception("Corrupt Code");
            }
            else if (!Enumerable.SequenceEqual(parameters.Select(x => x.type), function.parameters))
            {
                logger.Debug("Sequence of parameters do not match.");

                if(function.parameters.Length == parameters.Length)
                {
                    for (int i = 0; i < parameters.Length; i++)
                    {
                        logger.ConditionalTrace($"{i}th parameter type expected: {function.parameters[i]}, actual: {parameters[i].type}.");
                    }
                }

                throw new Exception("Corrupt Code");
            }

            this.function = function;
            this.parameters = parameters;
            locals = new ValueObject[function.locals?.Length ?? 0];

            for (uint i = 0; i < (locals?.Length ?? 0); i++)
            {
                switch(function.locals[i])
                {
                    case WebAssemblyType.i32:
                        locals[i] = new ValueObject(default(uint));
                        break;
                    case WebAssemblyType.i64:
                        locals[i] = new ValueObject(default(ulong));
                        break;
                    case WebAssemblyType.f32:
                        locals[i] = new ValueObject(default(float));
                        break;
                    case WebAssemblyType.f64:
                        locals[i] = new ValueObject(default(double));
                        break;
                    default:
                        throw new Exception("Corrupt Code");
                }
            }

            pc = 0;
        }
    }

    internal class LabelObject
    {
        public uint arity;
        public uint branch_target;

        public LabelObject(uint arity, uint branch_target)
        {
            this.arity = arity;
            this.branch_target = branch_target;
        }
    }

    internal class ValueObject 
    {
        public WebAssemblyType type;
        public object value;

        public ValueObject(uint value)
        {
            type = WebAssemblyType.i32;
            this.value = value;
        }

        public ValueObject(ulong value)
        {
            type = WebAssemblyType.i64;
            this.value = value;
        }

        public ValueObject(float value)
        {
            type = WebAssemblyType.f32;
            this.value = value;
        }

        public ValueObject(double value)
        {
            type = WebAssemblyType.f64;
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

        public override string ToString()
        {
            return $"({type},{value})";
        }
    }

    internal class FunctionInstance
    {
        public bool is_in_module = false;
        public bool is_export = false;
        public string module = null;
        public string name = null;
        public WebAssemblyType? return_type = null;
        public WebAssemblyType[] parameters = null;
        public WebAssemblyType[] locals = null;
        public byte[] code = null;
        public Action<ValueObject[]> host_code;
    }

    internal class MemoryInstance
    {
        private WebAssemblyMemoryManager mem;

        public byte[] memory => mem.memory;
        public long size => mem.actual_size;
        public long pages => mem.current;

        public bool GrowMemory(uint num_pages) => mem.grow_memory(num_pages);

        public MemoryInstance(uint initial, uint? maximum)
        {
            mem = new WebAssemblyMemoryManager(initial, maximum ?? uint.MaxValue);
        }
    }

    internal class GlobalInstance
    {
        public bool is_mutable;
        public WebAssemblyType type;
        public ValueObject value;

        public GlobalInstance(bool is_mutable, WebAssemblyType type, ValueObject value)
        {
            this.is_mutable = is_mutable;
            this.type = type;
            this.value = value;
        }
    }

    internal class TableInstance
    {
        public WebAssemblyType type;
        private WebAssemblyMemoryManager mem;

        public byte[] memory => mem.memory;
        public long size => mem.actual_size;

        public bool GrowMemory(uint num_pages) => mem.grow_memory(num_pages);

        public TableInstance(WebAssemblyType type, uint initial, uint? maximum)
        {
            this.type = type;
            mem = new WebAssemblyMemoryManager(initial, maximum ?? uint.MaxValue);
        }
    }

    internal class WebAssemblyExecutionContext
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();

        // https://webassembly.github.io/spec/core/exec/runtime.html#store
        public readonly Dictionary<uint, FunctionInstance> functions = new Dictionary<uint, FunctionInstance>();
        public readonly Dictionary<uint, MemoryInstance> linear_memory = new Dictionary<uint, MemoryInstance>();
        public readonly Dictionary<uint, GlobalInstance> globals = new Dictionary<uint, GlobalInstance>();
        public readonly Dictionary<uint, TableInstance> tables = new Dictionary<uint, TableInstance>();

        // A stack can contain the following 3 types of objects
        public readonly Stack<ActivationObject> callstack = new Stack<ActivationObject>();
        public readonly Stack<LabelObject> labels = new Stack<LabelObject>();
        public readonly Stack<ValueObject> values = new Stack<ValueObject>();
        // For performance reasons, store their types rather than cast the top of the stack
        public readonly Stack<WebAssemblyStackObjectType> stack_objects = new Stack<WebAssemblyStackObjectType>();

        public WebAssemblyExecutionContext(WebAssemblyFile file)
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

        private void InitTable(WebAssemblyFile file)
        {
            logger.Debug("Instanciating Table.");

            for (uint i = 0; i < (uint)file.table.entries.Length; i++)
            {
                var entry = file.table.entries[i];

                logger.ConditionalTrace($"Instanciating TableInstance({entry.element_type}, {entry.limits.initial}, {entry.limits.maximum ?? 0}).");

                tables[i] = new TableInstance(entry.element_type, entry.limits.initial, entry.limits.maximum);
            }

            logger.Debug("Done instanciating Table.");
        }
        private void InitGlobal(WebAssemblyFile file)
        {
            logger.Debug("Instanciating Globals.");

            List<GlobalInstance> finalized_globals = new List<GlobalInstance>();

            for (uint i = 0; i < (uint)file.global.globals.Length; i++)
            {
                var global = file.global.globals[i];

                var value = WebAssemblyHelper.GetInitExpr(global.init, finalized_globals);

                logger.ConditionalTrace($"Instanciating GlobalInstance({global.type.mutability}, {global.type.content_type}, {value}).");

                globals[i] = new GlobalInstance(global.type.mutability, global.type.content_type, value);

                finalized_globals.Add(globals[i]);
            }

            logger.Debug("Done instanciating Globals.");
        }
        private void InitMemory(WebAssemblyFile file)
        {
            logger.Debug("Instanciating Memory.");

            for (uint i = 0; i < (uint)file.memory.entries.Length; i++)
            {
                var limits = file.memory.entries[i].limits;

                logger.ConditionalTrace($"Instanciating MemoryInstance({limits.initial}, {limits.maximum}).");

                linear_memory[i] = new MemoryInstance(limits.initial, limits.maximum);
            }

            logger.Debug("Done instanciating Memory.");
        }
        private void InitData(WebAssemblyFile file)
        {
            logger.Debug("Instanciating Data.");

            foreach (var entry in file.data.entries)
            {
                var offset = WebAssemblyHelper.GetOffset(entry.offset);

                logger.ConditionalTrace($"Copying: {BitConverter.ToString(entry.data).Replace("-", "")} to [{offset},{offset+entry.data.Length}].");

                Array.Copy(entry.data, 0, linear_memory[entry.memory_index].memory, offset, entry.data.Length);
            }

            logger.Debug("Done instanciating Data.");
        }
        private void InitExport(WebAssemblyFile file)
        {
            logger.Debug("Instanciating Export.");

            for (uint i = 0; i < (uint)file.export.entries.Length; i++)
            {
                var entry = file.export.entries[i];

                logger.ConditionalTrace($"Export is {entry.index}: {entry.kind} {entry.field_str}.");

                switch (entry.kind)
                {
                    case WebAssemblyExternalKind.Function:
                        var index = entry.index;
                        functions[index].is_export = true;
                        functions[index].name = entry.field_str;
                        break;
                }
            }

            logger.Debug("Done instanciating Export.");
        }
        private void InitImport(WebAssemblyFile file)
        {
            logger.Debug("Instanciating Export.");

            Dictionary<uint, FuncType> type_info = new Dictionary<uint, FuncType>();

            for (uint i = 0; i < (uint)file.type.entries.Length; i++)
            {
                type_info[i] = file.type.entries[i];
                logger.ConditionalTrace($"Type {i} = {file.type.entries[i]}.");
            }

            for (uint i = 0; i < (uint)file.import.entries.Length; i++)
            {
                var entry = file.import.entries[i];

                logger.Debug($"Entry = {entry}.");

                switch (entry.kind)
                {
                    case WebAssemblyExternalKind.Function:
                        uint index = (uint)entry.type;

                        functions[i] = new FunctionInstance
                        {
                            module = entry.module_str,
                            name = entry.field_str,
                            parameters = type_info[index].param_types,
                            return_type = type_info[index].return_type
                        };

                        logger.ConditionalTrace($"Function {i} = {functions[i]}.");
                        break;
                }
            }

            logger.Debug("Done instanciating Export.");
        }
        private void InitFunctions(WebAssemblyFile file)
        {
            logger.Debug("Instanciating Functions.");

            Dictionary<uint, FuncType> type_info = new Dictionary<uint, FuncType>();

            for (uint i = 0; i < (uint)file.type.entries.Length; i++)
            {
                type_info[i] = file.type.entries[i];
                logger.ConditionalTrace($"Type {i} = {file.type.entries[i]}.");
            }

            logger.ConditionalTrace($"file.function.types.Length = {file.function.types.Length} and file.code.bodies.Length = {file.code.bodies.Length}.");
            if (file.function.types.Length != file.code.bodies.Length) throw new Exception("Invalid file");

            uint import_count = (uint)functions.Count;
            logger.ConditionalTrace($"Import count = {import_count}.");

            for (uint i = 0; i < (uint)file.code.bodies.Length; i++)
            {
                uint index = file.function.types[i];
                FunctionBody body = file.code.bodies[i];

                uint func_indx = i + import_count;

                logger.ConditionalTrace($"Function {func_indx} = {body}.");

                functions[func_indx] = new FunctionInstance
                {
                    module = "this",
                    is_in_module = true,
                    code = body.code,
                    parameters = type_info[index].param_types,
                    return_type = type_info[index].return_type,
                };

                List<WebAssemblyType> locals_unwrapped = new List<WebAssemblyType>();

                foreach (var local in body.locals)
                {
                    locals_unwrapped.AddRange(Enumerable.Repeat(local.type, (int)local.count));
                }

                functions[func_indx].locals = locals_unwrapped.ToArray();

                logger.ConditionalTrace($"Final object = {functions[func_indx]}.");
            }

            logger.Debug("Done instanciating Functions.");
        }

        public void Push(ActivationObject obj)
        {
            stack_objects.Push(WebAssemblyStackObjectType.ActivationObject);
            callstack.Push(obj);
            logger.ConditionalTrace($"Pushed Activation Object: {obj}.");
        }
        public void Push(LabelObject obj)
        {
            stack_objects.Push(WebAssemblyStackObjectType.LabelObject);
            labels.Push(obj);
            logger.ConditionalTrace($"Pushed Label: {obj}.");
        }
        public void Push(ValueObject obj)
        {
            stack_objects.Push(WebAssemblyStackObjectType.ValueObject);
            values.Push(obj);
            logger.ConditionalTrace($"Pushed Value: {obj}.");
        }

        public ActivationObject GetCurrentFunction()
        {
            logger.ConditionalTrace($"Current function = {callstack.Peek()}.");

            return callstack.Peek();
        }

        public WebAssemblyStackObjectType Peek()
        {
            if (stack_objects.Count == 0) return WebAssemblyStackObjectType.Empty;
            var ret = stack_objects.Peek();
            logger.ConditionalTrace($"Peeking {ret}.");
            return ret;
        }
        public ActivationObject PopActivation()
        {
            if (stack_objects.Pop() != WebAssemblyStackObjectType.ActivationObject) throw new Exception("Library error!");
            var ret = callstack.Pop();
            logger.ConditionalTrace($"Popping Activation: {ret}.");
            return ret;
        }
        public LabelObject PopLabel()
        {
            if (stack_objects.Pop() != WebAssemblyStackObjectType.LabelObject) throw new Exception("Library error!");
            var ret = labels.Pop();
            logger.ConditionalTrace($"Popping Label: {ret}.");
            return ret;
        }
        public ValueObject PopValue()
        {
            if (stack_objects.Pop() != WebAssemblyStackObjectType.ValueObject) throw new Exception("Library error!");
            var ret = values.Pop();
            logger.ConditionalTrace($"Popping Value: {ret}.");
            return ret;
        }
    }

    internal class WebAssemblyEnvironmentCalls
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
        private static Logger logger = LogManager.GetCurrentClassLogger();

        public readonly WebAssemblyFile file;
        private readonly WebAssemblyExecutionContext ctx;

        public WebAssemblyInterpreter(WebAssemblyFile file)
        {
            this.file = file;

            if (file.type == null) throw new Exception("No type information!");
            if (file.function == null) throw new Exception("No type information!");
            if (file.code == null) throw new Exception("No code to execute!");
            if (file.start == null) throw new Exception("No start function!");

            ctx = new WebAssemblyExecutionContext(file);

            ResolveExternalFunctions();

            uint start_func = file.start.index;
            logger.Debug($"Start function index is {start_func}. This index does {(ctx.functions.ContainsKey(start_func) ? "" : "not")} exist.");
            if (!ctx.functions.ContainsKey(start_func)) throw new Exception("Corrupt file.");  
        }

        private void ResolveExternalFunctions()
        {
            logger.Debug("Begin Resolving External Functions.");

            foreach (var func in ctx.functions)
            {
                if (!func.Value.is_in_module)
                {
                    switch (func.Value.name)
                    {
                        case "puts":
                            logger.Debug("Binding environment call 'puts'.");

                            func.Value.host_code = (args) => {
                                logger.Trace("Calling host_code for puts.");
                                unsafe
                                {
                                    fixed (byte* arr = ctx.linear_memory[0].memory)
                                        WebAssemblyEnvironmentCalls.puts(arr + (int)args[0].value);
                                }
                                ctx.Push(new ValueObject(0));
                            };
                            break;
                        default:
                            throw new Exception("Failed to bind environment function '" + func.Value.name + "'");
                    }
                }
            }

            logger.Debug("Done Resolving External Functions.");
        }

        public int Run()
        {
            // Execute starting from the start_func
            var result = Invoke(file.start.index);

            logger.Debug($"Final result is {result}.");

            // I'm not sure what valid result types are for main
            // What about void? Is that valid or is it simply 0?
            switch (result.type)
            {
                case WebAssemblyType.i32:
                    return (int)result.AsI32();
                case WebAssemblyType.i64:
                    return (int)(long)result.AsI64();
                default:
                    throw new Exception("Corrupt Return Value.");
            }
        }

        private ValueObject GetValueOrFail()
        {
            if (ctx.Peek() != WebAssemblyStackObjectType.ValueObject)
            {
                logger.Debug("Top of stack is not a value object!");
                Trap("Function stack corrupt.");
            }
            return ctx.PopValue();
        }

        private ValueObject GetValueOfTypeOrFail(WebAssemblyType expected_type)
        {
            ValueObject obj = GetValueOrFail();

            if(obj.type != expected_type)
            {
                logger.Debug($"Expected type of {expected_type} but got {obj.type}.");

                Trap("Function stack corrupt.");
            }

            return obj;
        }

        private (byte[] memory, int location) LoadOrFail(byte[] code, uint size, ref uint pc)
        {
            // align is in bytes
            // it tells us how this value is aligned
            // in our implementation, we can disregard alignment
            int align = 1 << (int)LEB128.ReadUInt32(code, ref pc);
            uint offset = LEB128.ReadUInt32(code, ref pc);

            ValueObject i = GetValueOfTypeOrFail(WebAssemblyType.i32);
            long ea = i.AsI32() + offset;
            uint N = size/8;

            if ((ea + N) > ctx.linear_memory[0].size)
            {
                Trap("Illegal memory access");
            }

            logger.ConditionalTrace($"Memory location calculated = {ea}.");

            return (ctx.linear_memory[0].memory, (int)ea);
        }

        private (ValueObject value, byte[] memory, int location) StoreOrFail(byte[] code, uint size, WebAssemblyType type, ref uint pc)
        {
            // align is in bytes
            // it tells us how this value is aligned
            // in our implementation, we can disregard alignment
            int align = 1 << (int)LEB128.ReadUInt32(code, ref pc);
            uint offset = LEB128.ReadUInt32(code, ref pc);

            ValueObject c = GetValueOfTypeOrFail(type);
            ValueObject i = GetValueOfTypeOrFail(WebAssemblyType.i32);
            long ea = i.AsI32() + offset;
            uint N = size / 8;

            if ((ea + N) > ctx.linear_memory[0].size)
            {
                Trap("Illegal memory access");
            }

            logger.ConditionalTrace($"Memory location calculated = {ea}.");

            return (c, ctx.linear_memory[0].memory, (int)ea);
        }

        private void Enter(LabelObject obj)
        {
            ctx.Push(obj);
        }

        private void Exit()
        {
            // exit, no label required
        }

        private void Exit(uint l)
        {
            if(ctx.labels.Count() < l + 1)
            {
                Trap("Function corrupt");
            }

            // Stack appears to consider ElementAt from the front of the list rather than the back
            // So paradoxically, it actually counts up (0th element = latest, 1st = second, etc)
            LabelObject label = ctx.labels.ElementAt((int)l);

            uint n = label.arity;

            logger.ConditionalTrace($"Exiting to label: {label}.");

            /*
                Pop the values val^n from the stack.
                Repeat l+1 times:
                    While the top of the stack is a value, do:
                        Pop the value from the stack.
                    Assert: due to validation, the top of the stack now is a label.
                    Pop the label from the stack.
                Push the values val^n to the stack.
             */

            Stack<ValueObject> values = new Stack<ValueObject>();
            for(uint i = 0; i < n; i++)
            {
                values.Push(GetValueOrFail());
            }

            for(uint i = 0; i < l+1; i++)
            {
                while(ctx.Peek() == WebAssemblyStackObjectType.ValueObject)
                {
                    ctx.PopValue();
                }

                if(ctx.Peek() != WebAssemblyStackObjectType.LabelObject)
                {
                    Trap("Corrupt function");
                }

                ctx.PopLabel();
            }

            for(uint i = 0; i < n; i++)
            {
                ctx.Push(values.Pop());
            }

            logger.ConditionalTrace($"Jumping to {label.branch_target}.");

            ctx.GetCurrentFunction().pc = label.branch_target;
        }

        private ValueObject Invoke(uint func_id)
        {
            logger.Debug($"Invoking {func_id}.");

            FunctionInstance func = ctx.functions[func_id];
            ValueObject[] parameters = new ValueObject[func.parameters.Length];

            // Gather parameters from stack
            for (int i = 0; i < parameters.Length; i++)
            {
                parameters[i] = GetValueOrFail();
            }

            // Create our activation object and push on to the stack
            ActivationObject activation_object = new ActivationObject(ctx.functions[func_id], parameters);
            ctx.Push(activation_object);

            // If we're external, run host code
            if (!func.is_in_module)
            {
                logger.Debug("Executing host code.");
                func.host_code(activation_object.parameters);
            }
            else
            {
                Execute();             
            }

            ValueObject result = GetValueOrFail();

            if(result.type != func.return_type)
            {
                logger.Debug($"Result of function was {result.type}, expected {func.return_type}.");
                Trap("Function corrupt.");
            }

            // Remember to pop our own activation instance off the record!
            // Double check as well that it is in fact us
            if (ctx.Peek() != WebAssemblyStackObjectType.ActivationObject || activation_object != ctx.PopActivation())
            {
                Trap("Function stack corrupt.");
            }

            return result;
        }

        private void Trap(string error)
        {
            ActivationObject activation_object = ctx.GetCurrentFunction();
            FunctionInstance func = activation_object.function;

            throw new WebAssemblyTrap($"Trap occured at {func.module}.{func.name}@{activation_object.pc} with message: {error}");
        }

        // TODO: Can now transform Execute from a recursive function into an iterative function 
        // Each time a new "frame" is added, we just update activation_object and func
        // ctx records the current pc so when we go back
        // Simply need to inline the Invoke method into Execute
        private void Execute()
        {
            try
            {
                ActivationObject activation_object = ctx.GetCurrentFunction();
                FunctionInstance func = activation_object.function;
                byte[] code = func.code;

                logger.Info($"Entering Execute with function {func.module}.{func.name}.");
                logger.ConditionalTrace($"Function returns: {func.return_type}.");
                logger.ConditionalTrace("Function parameters: {0}.", string.Join(", ", func.parameters.Select(x => x.ToString())));
                logger.ConditionalTrace("Function arguments: {0}.", string.Join(", ", activation_object.parameters.Select(x => x.ToString())));
                logger.ConditionalTrace("Function locals: {0}.", string.Join(", ", func.locals.Select(x => x.ToString())));
                
                //WebAssemblyHelper.ReinterpretHelper reinterpret_helper = new WebAssemblyHelper.ReinterpretHelper();

                bool continue_executing = true;

                // https://webassembly.github.io/spec/core/exec/instructions.html
                while (continue_executing)
                {
                    logger.ConditionalTrace($"PC = {activation_object.pc}.");
                    logger.Debug($"Executing: {(WebAssemblyOpcode)code[activation_object.pc]}.");

                    switch ((WebAssemblyOpcode)code[activation_object.pc])
                    {
                        case WebAssemblyOpcode.UNREACHABLE:
                            {
                                Trap("Unreachable reached!");

                                break;
                            }
                        case WebAssemblyOpcode.NOP:
                            {
                                break;
                            }
                        case WebAssemblyOpcode.END:
                            {
                                continue_executing = false;

                                break;
                            }
                        case WebAssemblyOpcode.LOOP:
                            {
                                // ignore result for now
                                activation_object.pc++;
                                var result = (WebAssemblyType)code[activation_object.pc];

                                Enter(new LabelObject(0, activation_object.pc-2));

                                break;
                            }
                        case WebAssemblyOpcode.BR:
                            { 
                                uint l = LEB128.ReadUInt32(code, ref activation_object.pc);

                                Exit(l);

                                break;
                            }
                        case WebAssemblyOpcode.BR_IF:
                            {
                                ValueObject c = GetValueOfTypeOrFail(WebAssemblyType.i32);
                                if(c.AsI32() != 0)
                                {
                                    goto case WebAssemblyOpcode.BR;
                                }

                                break;
                            }
                        case WebAssemblyOpcode.LOCAL_GET:
                            {
                                uint local = LEB128.ReadUInt32(code, ref activation_object.pc);
                                if (local > func.locals.Length) Trap("Function corrupt.");

                                ctx.Push(activation_object.locals[local]);

                                break;
                            }
                        case WebAssemblyOpcode.LOCAL_SET:
                            {
                                uint local = LEB128.ReadUInt32(code, ref activation_object.pc);
                                if (local > func.locals.Length) Trap("Function corrupt.");

                                ValueObject val = GetValueOrFail();

                                activation_object.locals[local] = val;

                                break;
                            }
                        case WebAssemblyOpcode.LOCAL_TEE:
                            {
                                ValueObject val = GetValueOrFail();
                                ctx.Push(val);
                                ctx.Push(val);
                                goto case WebAssemblyOpcode.LOCAL_SET;
                            }
                        case WebAssemblyOpcode.I32_CONST:
                            {
                                ctx.Push(new ValueObject(LEB128.ReadUInt32(code, ref activation_object.pc)));
                                break;
                            }
                        case WebAssemblyOpcode.I64_CONST:
                            {
                                ctx.Push(new ValueObject(LEB128.ReadUInt64(code, ref activation_object.pc)));
                                break;
                            }
                        case WebAssemblyOpcode.F32_CONST:
                            {
                                ctx.Push(new ValueObject(BitConverter.ToSingle(code, (int)activation_object.pc + 1)));
                                activation_object.pc += 4;
                                break;
                            }
                        case WebAssemblyOpcode.F64_CONST:
                            {
                                ctx.Push(new ValueObject(BitConverter.ToDouble(code, (int)activation_object.pc + 1)));
                                activation_object.pc += 7;
                                break;
                            }
                        case WebAssemblyOpcode.I32_LOAD:
                            {
                                var (memory, location) = LoadOrFail(code, 32, ref activation_object.pc);

                                ctx.Push(new ValueObject(BitConverter.ToUInt32(memory, location)));

                                break;
                            }
                        case WebAssemblyOpcode.I64_LOAD:
                            {
                                var (memory, location) = LoadOrFail(code, 64, ref activation_object.pc);

                                ctx.Push(new ValueObject(BitConverter.ToUInt64(memory, location)));

                                break;
                            }
                        case WebAssemblyOpcode.F32_LOAD:
                            {
                                var (memory, location) = LoadOrFail(code, 32, ref activation_object.pc);

                                ctx.Push(new ValueObject(BitConverter.ToSingle(memory, location)));

                                break;
                            }
                        case WebAssemblyOpcode.F64_LOAD:
                            {
                                var (memory, location) = LoadOrFail(code, 64, ref activation_object.pc);

                                ctx.Push(new ValueObject(BitConverter.ToDouble(memory, location)));

                                break;
                            }
                        case WebAssemblyOpcode.I32_LOAD8_S:
                            {
                                var (memory, location) = LoadOrFail(code, 8, ref activation_object.pc);

                                ctx.Push(new ValueObject((uint)(sbyte)memory[location]));

                                break;
                            }
                        case WebAssemblyOpcode.I32_LOAD8_U:
                            {
                                var (memory, location) = LoadOrFail(code, 8, ref activation_object.pc);

                                ctx.Push(new ValueObject((uint)memory[location]));

                                break;
                            }
                        case WebAssemblyOpcode.I32_LOAD16_S:
                            {
                                var (memory, location) = LoadOrFail(code, 16, ref activation_object.pc);

                                ctx.Push(new ValueObject((uint)BitConverter.ToInt16(memory, (int)activation_object.pc)));

                                break;
                            }
                        case WebAssemblyOpcode.I32_LOAD16_U:
                            {
                                var (memory, location) = LoadOrFail(code, 16, ref activation_object.pc);

                                ctx.Push(new ValueObject(BitConverter.ToUInt16(memory, (int)activation_object.pc)));

                                break;
                            }
                        case WebAssemblyOpcode.I64_LOAD8_S:
                            {
                                var (memory, location) = LoadOrFail(code, 8, ref activation_object.pc);

                                ctx.Push(new ValueObject((ulong)(sbyte)memory[location]));

                                break;
                            }
                        case WebAssemblyOpcode.I64_LOAD8_U:
                            {
                                var (memory, location) = LoadOrFail(code, 8, ref activation_object.pc);

                                ctx.Push(new ValueObject((ulong)memory[location]));

                                break;
                            }
                        case WebAssemblyOpcode.I64_LOAD16_S:
                            {
                                var (memory, location) = LoadOrFail(code, 16, ref activation_object.pc);

                                ctx.Push(new ValueObject((ulong)BitConverter.ToInt16(memory, (int)activation_object.pc)));

                                break;
                            }
                        case WebAssemblyOpcode.I64_LOAD16_U:
                            {
                                var (memory, location) = LoadOrFail(code, 16, ref activation_object.pc);

                                ctx.Push(new ValueObject((ulong)BitConverter.ToUInt16(memory, (int)activation_object.pc)));

                                break;
                            }
                        case WebAssemblyOpcode.I64_LOAD32_S:
                            {
                                var (memory, location) = LoadOrFail(code, 32, ref activation_object.pc);

                                ctx.Push(new ValueObject((ulong)BitConverter.ToInt32(memory, (int)activation_object.pc)));

                                break;
                            }
                        case WebAssemblyOpcode.I64_LOAD32_U:
                            {
                                var (memory, location) = LoadOrFail(code, 32, ref activation_object.pc);

                                ctx.Push(new ValueObject((ulong)BitConverter.ToUInt32(memory, (int)activation_object.pc)));

                                break;
                            }
                        case WebAssemblyOpcode.I32_STORE:
                            {
                                var (value, memory, location) = StoreOrFail(code, 32, WebAssemblyType.i32, ref activation_object.pc);

                                Array.Copy(BitConverter.GetBytes(value.AsI32()), 0, memory, location, 32/8);

                                break;
                            }
                        case WebAssemblyOpcode.I64_STORE:
                            {
                                var (value, memory, location) = StoreOrFail(code, 64, WebAssemblyType.i64, ref activation_object.pc);

                                Array.Copy(BitConverter.GetBytes(value.AsI64()), 0, memory, location, 64/8);

                                break;
                            }
                        case WebAssemblyOpcode.F32_STORE:
                            {
                                var (value, memory, location) = StoreOrFail(code, 32, WebAssemblyType.f32, ref activation_object.pc);

                                Array.Copy(BitConverter.GetBytes(value.AsF32()), 0, memory, location, 32/8);

                                break;
                            }
                        case WebAssemblyOpcode.F64_STORE:
                            {
                                var (value, memory, location) = StoreOrFail(code, 64, WebAssemblyType.f64, ref activation_object.pc);

                                Array.Copy(BitConverter.GetBytes(value.AsF64()), 0, memory, location, 64/8);

                                break;
                            }
                        case WebAssemblyOpcode.I32_STORE8:
                            {
                                var (value, memory, location) = StoreOrFail(code, 32, WebAssemblyType.i32, ref activation_object.pc);

                                memory[location] = (byte)value.AsI32();

                                break;
                            }
                        case WebAssemblyOpcode.I32_STORE16:
                            {
                                var (value, memory, location) = StoreOrFail(code, 32, WebAssemblyType.i32, ref activation_object.pc);

                                Array.Copy(BitConverter.GetBytes((ushort)value.AsI32()), 0, memory, location, 16/8);

                                break;
                            }
                        case WebAssemblyOpcode.I64_STORE8:
                            {
                                var (value, memory, location) = StoreOrFail(code, 64, WebAssemblyType.i64, ref activation_object.pc);

                                memory[location] = (byte)value.AsI64();

                                break;
                            }
                        case WebAssemblyOpcode.I64_STORE16:
                            {
                                var (value, memory, location) = StoreOrFail(code, 64, WebAssemblyType.i64, ref activation_object.pc);

                                Array.Copy(BitConverter.GetBytes((ushort)value.AsI64()), 0, memory, location, 16/8);

                                break;
                            }
                        case WebAssemblyOpcode.I64_STORE32:
                            {
                                var (value, memory, location) = StoreOrFail(code, 64, WebAssemblyType.i64, ref activation_object.pc);

                                Array.Copy(BitConverter.GetBytes((uint)value.AsI64()), 0, memory, location, 32/8);

                                break;
                            }
                        case WebAssemblyOpcode.MEMORY_SIZE:
                            {
                                ctx.Push(new ValueObject((uint)ctx.linear_memory[0].pages));

                                break;
                            }
                        case WebAssemblyOpcode.MEMORY_GROW:
                            {
                                ValueObject n = GetValueOfTypeOrFail(WebAssemblyType.i32);

                                if (ctx.linear_memory[0].GrowMemory(n.AsI32()))
                                {
                                    ctx.Push(n);
                                }
                                else
                                {
                                    ctx.Push(new ValueObject(unchecked((uint)-1)));
                                }

                                break;
                            } 
                        case WebAssemblyOpcode.CALL:
                            {
                                uint new_func = LEB128.ReadUInt32(code, ref activation_object.pc);

                                ctx.Push(Invoke(new_func));

                                break;
                            }
                        case WebAssemblyOpcode.DROP:
                            {
                                GetValueOrFail();

                                break;
                            }
                        case WebAssemblyOpcode.SELECT:
                            {
                                ValueObject c = GetValueOfTypeOrFail(WebAssemblyType.i32);
                                ValueObject val2 = GetValueOrFail();
                                ValueObject val1 = GetValueOrFail();

                                ctx.Push(c.AsI32() != 0 ? val1 : val2);

                                break;
                            }
                        default:
                            {
                                Trap($"Unknown Opcode {code[activation_object.pc]}.");

                                break;
                            }
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
                logger.Debug(ex.StackTrace);

                Trap("Unexpected Error: " + ex.Message);
            }
        }
    }
}
