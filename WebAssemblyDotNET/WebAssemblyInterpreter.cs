using System;
using System.Collections.Generic;
using System.Linq;
using NLog;

namespace WebAssemblyDotNET
{
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
                                        WebAssemblyUnsafeNativeMethods.puts(arr + (int)args[0].value);
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
                logger.ConditionalTrace("Function parameters: {0}.", WebAssemblyHelper.ToString(func.parameters, ", "));
                logger.ConditionalTrace("Function arguments: {0}.", WebAssemblyHelper.ToString(activation_object.parameters, ", "));
                logger.ConditionalTrace("Function locals: {0}.", WebAssemblyHelper.ToString(func.locals, ", "));
                
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

        public override string ToString()
        {
            return $"({GetType().Name} {ctx})";
        }
    }
}
