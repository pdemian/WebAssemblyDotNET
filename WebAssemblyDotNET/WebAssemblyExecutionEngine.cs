using System;
using System.Collections.Generic;
using System.Linq;
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
        public Func<WASMValueObject[], WASMValueObject> host_code;
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
            stack_objects.Push(WASMStackObjectType.ActivationObject);
            labels.Push(obj);
        }
        public void Push(WASMValueObject obj)
        {
            stack_objects.Push(WASMStackObjectType.ActivationObject);
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
}
