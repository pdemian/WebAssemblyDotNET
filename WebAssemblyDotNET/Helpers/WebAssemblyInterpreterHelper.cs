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

        public override string ToString()
        {
            return $"({GetType().Name} (current {current}) (maximum {maximum}))";
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

                if (function.parameters.Length == parameters.Length)
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
                switch (function.locals[i])
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

        public override string ToString()
        {
            return $"({GetType().Name} (pc {pc}) {function} (parameters {WebAssemblyHelper.ToString(parameters)}) (locals {WebAssemblyHelper.ToString(locals)}))";
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

        public override string ToString()
        {
            return $"({GetType().Name} (arity {arity}) (branch_target {branch_target}))";
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
            return $"({GetType().Name} (type {type}) (value {value}))";
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

        public override string ToString()
        {
            return $"({GetType().Name} (is_in_module {is_in_module}) (is_export {is_export}) (module {module}) (name {name}) (return {return_type?.ToString() ?? "void"}) (parameters {WebAssemblyHelper.ToString(parameters)}) (locals {WebAssemblyHelper.ToString(locals)}))";
        }
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

        public override string ToString()
        {
            return $"({GetType().Name} {mem})";
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

        public override string ToString()
        {
            return $"({GetType().Name} (is_mutable {is_mutable}) {type} {value})";
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

        public override string ToString()
        {
            return $"({GetType().Name} {type} {mem})";
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

                logger.ConditionalTrace($"Copying: {BitConverter.ToString(entry.data).Replace("-", "")} to [{offset},{offset + entry.data.Length}].");

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

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();

            sb.AppendFormat("({0}, ", GetType().Name);

            sb.Append("(functions ");
            foreach(var func in functions.Keys)
            {
                sb.AppendFormat("({0} {1})", func, functions[func]);
            }
            sb.Append(") (memory ");
            foreach(var mem in linear_memory.Keys)
            {
                sb.AppendFormat("({0} {1})", mem, linear_memory[mem]);
            }
            sb.Append(") (globals ");
            foreach(var global in globals.Keys)
            {
                sb.AppendFormat("({0} {1})", global, globals[global]);
            }
            sb.Append(") (tables ");
            foreach(var table in tables.Keys)
            {
                sb.AppendFormat("({0} {1})", table, tables[table]);
            }

            var callstack_arr = callstack.ToArray();
            var labels_arr = labels.ToArray();
            var values_arr = values.ToArray();
            var objects_arr = stack_objects.ToArray();
            int callstack_i = 0, labels_i = 0, values_i = 0;

            sb.Append(") (stack ");
            foreach(var stack_obj in objects_arr)
            {
                switch(stack_obj)
                {
                    case WebAssemblyStackObjectType.ActivationObject:
                        sb.Append(callstack_arr[callstack_i++]);
                        break;
                    case WebAssemblyStackObjectType.LabelObject:
                        sb.Append(labels_arr[labels_i++]);
                        break;
                    case WebAssemblyStackObjectType.ValueObject:
                        sb.Append(values_arr[values_i++]);
                        break;
                }
                sb.Append(' ');
            }
            sb.Append("))");
            return sb.ToString();
        }
    }

    internal class WebAssemblyUnsafeNativeMethods
    {
        [DllImport("msvcrt.dll", CallingConvention = CallingConvention.Cdecl)]
        public static unsafe extern int puts(byte* str);
    }
}
