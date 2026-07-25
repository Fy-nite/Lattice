using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using lattice.Core;
using lattice.Throwables;
using ObjectIR.Core;
using ObjectIR.Core.AST;
using ObjectIR.Core.Ast;
using ObjectIR.StdLib.Core.Generics;
using ObjectIR.StdLib.Core.Memory;
using lattice.Runtime.Compiler;
using lattice.Runtime.Memory;
using lattice.Runtime;

public class CPU : IProgramLoader
{
    public lattice.Runtime.Scheduler Scheduler { get; set; }
    public List<ModuleNode> Modules = new();
    public CompilationCache Cache { get; set; } = new();
    public HeapAllocator? Heap { get; set; }
    public ModuleNode program;

    public CallStack CurrentFrame;

    public bool Debug { get; set; }
    public ExperimentalFeature Features { get; set; }

    private lattice.Runtime.Debugging.Debugger _debugger = new lattice.Runtime.Debugging.Debugger();

    public int MaxStackDepth { get; set; }

    // --- IProgramLoader Implementation ---

    ModuleNode? IProgramLoader.MainModule => program;

    MethodNode? IProgramLoader.GetCurrentMethod() => CurrentFrame?.Method;

    object? IProgramLoader.GetCurrentThis() => CurrentFrame?.This;
    void IProgramLoader.Yield(int milliseconds)
    {
        System.Threading.Thread.Sleep(milliseconds);
    }
    Value<object> IProgramLoader.ExecuteMethod(MethodReference method, object? thisObj, params object[] args)
    {
        MethodNode node = ResolveMethod(method);
        if (node == null) throw new MethodResolutionException(method.Name, CurrentFrame?.GetStackTrace());

        ExecuteMethod(node, thisObj as ManagedObject, args);
        
        if (!string.Equals(node.ReturnType.Name, "void", StringComparison.Ordinal) && CurrentFrame?.EvaluationStack.Count > 0)
        {
            var result = CurrentFrame.EvaluationStack.Pop();
            return (result as Value<object>) ?? new Value<object>(result);
        }

        return new Value<object>(null);
    }

    ClassNode? IProgramLoader.ResolveType(TypeRef typeRef)
    {
        if (program == null) return null;

        // 1. Search in main program module
        var cls = program.Classes.FirstOrDefault(c => string.Equals(c.Name, typeRef.Name, StringComparison.Ordinal));
        if (cls != null) return cls;

        // 2. Search in other loaded modules
        foreach (var mod in Modules)
        {
            cls = mod.Classes.FirstOrDefault(c => string.Equals(c.Name, typeRef.Name, StringComparison.Ordinal));
            if (cls != null) return cls;
        }

        // 3. Try to resolve via dynamic hooks if not found
        if (NativeRegistry.TryRegister(typeRef.Name, program))
        {
            return program.Classes.FirstOrDefault(c => string.Equals(c.Name, typeRef.Name, StringComparison.Ordinal));
        }

        return null;
    }

    void IProgramLoader.SpawnThread(IDelagate entryPoint)
    {
        if (Scheduler == null) {
            Console.WriteLine("[CPU] SpawnThread failed: Scheduler is null!");
            return;
        }
        if (Debug)
            Console.WriteLine($"[CPU] Spawning thread on CPU instance {this.GetHashCode()}");
        
        var newCpu = new CPU
        {
            program = this.program,
            Modules = new List<ModuleNode>(this.Modules),
            Debug = this.Debug,
            Features = this.Features,
            MaxStackDepth = this.MaxStackDepth,
            Scheduler = this.Scheduler,
            Cache = this.Cache,
            Heap = this.Heap
        };

        // Resolve method from delegate
        MethodNode method = ResolveMethod(entryPoint.Method);
        if (method != null)
        {
            // Use the Target from the delegate as the 'this' pointer
            ManagedObject targetObj = entryPoint.Target as ManagedObject;
            newCpu.PushFrame(method, targetObj);
            Scheduler.AddThread(newCpu);
        }
        else {
            Console.WriteLine($"[CPU] Failed to resolve method {entryPoint.Method.Name}");
        }
    }
    // --- End IProgramLoader Implementation ---

    

    // --- End IProgramLoader Implementation ---

    public CPU(ExperimentalFeature features = ExperimentalFeature.None)
    {
        Debug = false;
        MaxStackDepth = 1000;
        Features = features;
    }

    public void InitializeMain(string[] args)
    {
        ClassNode programClass = program.Classes.FirstOrDefault(c => string.Equals(c.Name, "Program", StringComparison.Ordinal));
        MethodNode main = null;
        if (programClass != null)
        {
            main = programClass.Methods.FirstOrDefault(m => string.Equals(m.Name, "Main", StringComparison.Ordinal));
        }
        
        if ((object)main == null)
        {
            throw new EntrypointNotFoundException("Entrypoint 'Program.Main' not found", "Create a 'Program' class with a 'Main' method to serve as the entry point.");
        }

        List<object> mainArgs = new List<object>();
        if (main.Parameters.Count > 0)
        {
            mainArgs.Add(args);
        }

        // Setup the initial frame
        CurrentFrame = new CallStack(main, null);
        if (main.Parameters.Count > 0)
        {
            CurrentFrame.Args[main.Parameters[0].Name] = args;
        }
    }

    public void PushFrame(MethodNode method, ManagedObject thisObj = null, object[] args = null)
    {
        CallStack newFrame = ((CurrentFrame != null) ? CurrentFrame.PushFrame(method, thisObj) : new CallStack(method, thisObj));
        if (args != null)
        {
            for (int i = 0; i < Math.Min(args.Length, method.Parameters.Count); i++)
            {
                newFrame.Args[method.Parameters[i].Name] = args[i];
            }
        }
        CurrentFrame = newFrame;
    }

    /// <summary>
    /// Executes a single instruction from the current frame.
    /// Returns true if execution can continue, false if the thread has finished or is blocked.
    /// </summary>
    public bool Step()
    {
        if (CurrentFrame == null) return false;

        using (ProgramLoader.Activate(this))
        {
            if (CurrentFrame.IP < CurrentFrame.Method.Body.Statements.Count)
            {
                Statement instruction = CurrentFrame.Method.Body.Statements[CurrentFrame.IP];
                ExecuteInstruction(instruction);
                CurrentFrame.IP++;
                return true;
            }

            // Current method finished, pop frame
            CurrentFrame = CurrentFrame.Previous;
            return CurrentFrame != null;
        }
    }

    public void LoadProgram(string path)
    {
        program = TextIrParser.ParseModule(File.ReadAllText(path));
        if ((object)program == null)
        {
            throw new FileNotFoundException($"File not found: {path}, are you sure that the file exists?");
        }
    }

    public void LoadModule(ModuleNode Modz)
    {
        program = Modz;
        CompileAll();
    }


    private int GetStackDepth()
    {
        int depth = 0;
        for (CallStack current = CurrentFrame; current != null; current = current.Previous)
        {
            depth = checked(depth + 1);
        }
        return depth;
    }

    public void ExecuteMethod(MethodNode method, ManagedObject thisObj = null, object[] providedArgs = null)
    {
        if (Debug == true) // Temporarily force logging
        {
            Console.WriteLine($"[CPU] Executing method: {method.Name} on {thisObj?.TypeName ?? "static"}");
        }
        int argCount = method.Parameters.Count;
        checked
        {
            object[] poppedArgs = new object[argCount];
            // ... (rest of the logic for popping args)
            if (providedArgs != null)
            {
                int num = Math.Min(providedArgs.Length, argCount) - 1;
                for (int i = 0; i <= num; i++)
                {
                    poppedArgs[i] = RuntimeHelpers.GetObjectValue(providedArgs[i]);
                }
            }
            else if (CurrentFrame != null)
            {
                int num2 = argCount - 1;
                for (int j = num2; j >= 0; j += -1)
                {
                    poppedArgs[j] = RuntimeHelpers.GetObjectValue(CurrentFrame.EvaluationStack.Pop());
                }
            }
            
            if (method.NativeImpl != null)
            {
                if (Debug == true)
                {
                    Console.WriteLine($"[CPU] Calling native method: {method.Name}");
                }

                // Push a temporary frame to provide context (like 'this') for the native call
                PushFrame(method, thisObj);

                try 
                {
                    Value<object>[] nativeArgs = new Value<object>[argCount];
                    int num3 = argCount - 1;
                    for (int k = 0; k <= num3; k++)
                    {
                        object popped = RuntimeHelpers.GetObjectValue(poppedArgs[k]);
                        // Always unwrap so the native method gets the raw data in Value<object>.Data
                        object rawData = Unwrap(popped);
                        nativeArgs[k] = new Value<object>(rawData);
                    }

                    // Native calls also need context
                    Value<object> result;
                    using (ProgramLoader.Activate(this))
                    {
                        result = method.NativeImpl.Method(nativeArgs);
                    }

                    if (!string.Equals(method.ReturnType.Name, "void", StringComparison.Ordinal) && result != null && CurrentFrame.Previous != null)
                    {
                        CurrentFrame.Previous.EvaluationStack.Push(result);
                    }
                }
                finally 
                {
                    // Pop the temporary frame
                    CurrentFrame = CurrentFrame.Previous;
                }
                return;
            }

            // Fast path: if the method has been compiled or JIT'd, use that instead of AST interpretation
            var compiled = Cache.GetCompiled(method);
            if (compiled != null)
            {
                if (Features.HasFlag(ExperimentalFeature.Jit))
                {
                    var jitDel = Cache.GetJit(method);
                    if (jitDel != null)
                    {
                        var jitArgs = new object?[argCount];
                        for (int i = 0; i < argCount; i++)
                            jitArgs[i] = poppedArgs[i];
                        var jitResult = jitDel(jitArgs, this, compiled);
                        if (compiled.ReturnsValue && CurrentFrame?.Previous != null)
                        {
                            CurrentFrame.Previous.EvaluationStack.Push(jitResult);
                        }
                        return;
                    }

                    int count = Cache.IncrementExecutionCount(method);
                    if (count == 1000)
                        QueueJitCompile(method, compiled);
                }

                var rawArgs = new StackValue[argCount];
                for (int i = 0; i < argCount; i++)
                    rawArgs[i] = CompiledExecutor.RawToStackValue(poppedArgs[i]);
                var compiledResult = CompiledExecutor.Execute(compiled, rawArgs, this);
                if (compiled.ReturnsValue && CurrentFrame != null)
                {
                    CurrentFrame.EvaluationStack.Push(compiledResult.ToObject());
                }
                return;
            }

            if (GetStackDepth() >= MaxStackDepth)
            {
                throw new LatticeStackOverflowException((CurrentFrame != null) ? CurrentFrame.GetStackTrace() : ("at " + method.Name));
            }
            
            CallStack newFrame = ((CurrentFrame != null) ? CurrentFrame.PushFrame(method, thisObj) : new CallStack(method, thisObj));
            int num4 = argCount - 1;
            for (int l = 0; l <= num4; l++)
            {
                newFrame.Args[method.Parameters[l].Name] = RuntimeHelpers.GetObjectValue(poppedArgs[l]);
            }
            
            CallStack oldFrame = CurrentFrame;
            CurrentFrame = newFrame;
            
            // If we are not in a granular "Step" mode, we run to completion
            // This maintains backward compatibility for Run()
            try
            {
                while (CurrentFrame == newFrame && CurrentFrame.IP < CurrentFrame.Method.Body.Statements.Count)
                {
                    Statement Instruction = CurrentFrame.Method.Body.Statements[CurrentFrame.IP];
                    ExecuteInstruction(Instruction);
                    CurrentFrame.IP++;
                }
                
                // If the frame finished, pop it
                if (CurrentFrame == newFrame && CurrentFrame.IP >= CurrentFrame.Method.Body.Statements.Count)
                {
                    CurrentFrame = oldFrame;
                }
            }
            finally
            {
                // In a recursive ExecuteMethod, we want to restore the frame if it didn't finish
                // but usually it finishes here.
            }
        }
    }

    public void ExecuteInstruction(Statement ins)
    {
        if (Debug)
        {
            _debugger.Step(this, ins);
        }

        if (ins is InstructionStatement)
        {
            Instruction instr = ((InstructionStatement)ins).Instruction;
            if (instr is SimpleInstruction)
            {
                SimpleInstruction simple = (SimpleInstruction)instr;
                switch (simple.OpCode)
                {
                    case OpCode.Ldstr:
                        {
                            string str = simple.Operand.ToString();
                            if (str.StartsWith("\"") && str.EndsWith("\""))
                            {
                                str = str.Substring(1, checked(str.Length - 2));
                            }
                            str = str.Replace("\\n", "\n").Replace("\\t", "\t").Replace("\\r", "\r");
                            
                            CurrentFrame.EvaluationStack.Push(new Value<string>(str));
                            break;
                        }
                    case OpCode.LdcI4:
                        CurrentFrame.EvaluationStack.Push(new Value<int>(int.Parse(simple.Operand!)));
                        break;

                    case OpCode.LdcR4:
                        CurrentFrame.EvaluationStack.Push(new Value<float>(float.Parse(simple.Operand!)));
                        break;

                    case OpCode.Ldnull:
                        CurrentFrame.EvaluationStack.Push(null);
                        break;

                    case OpCode.Ldloc:
                        CurrentFrame.EvaluationStack.Push(CurrentFrame.Locals[simple.Operand!]);
                        break;

                    case OpCode.Stloc:
                        if (CurrentFrame.EvaluationStack.Count == 0)
                        {
                            var sz = ins.Location;
                            if (sz != null)
                            {
                                throw new RuntimeException($"stloc '{simple.Operand}' requires value on stack, but it is empty at {sz.Line}: {sz.SourceLine}", CurrentFrame.GetStackTrace());
                            }
                            else
                            {
                                throw new RuntimeException($"stloc '{simple.Operand}' requires value on stack, but it is empty", CurrentFrame.GetStackTrace());
                            }
                        }
                        CurrentFrame.Locals[simple.Operand!] = CurrentFrame.EvaluationStack.Pop();
                        break;

                    case OpCode.Starg:
                        if (CurrentFrame.EvaluationStack.Count == 0)
                        {
                            var sx = ins.Location;
                            throw new RuntimeException($"starg '{simple.Operand}' requires value on stack, but it is empty at {sx.Line}: {sx.SourceLine}", CurrentFrame.GetStackTrace());
                        }
                        CurrentFrame.Args[simple.Operand!] = CurrentFrame.EvaluationStack.Pop();
                        break;

                    case OpCode.Ldarg:
                        CurrentFrame.EvaluationStack.Push(CurrentFrame.Args[simple.Operand!]);
                        break;

                    case OpCode.Dup:
                        CurrentFrame.EvaluationStack.Push(CurrentFrame.EvaluationStack.Peek());
                        break;

                    case OpCode.Pop:
                        CurrentFrame.EvaluationStack.Pop();
                        break;

                    case OpCode.Ldfld:
                        {
                            var instance = CurrentFrame.EvaluationStack.Pop() as ManagedObject;
                            if (instance == null) throw new RuntimeException("ldfld requires a managed object instance on stack", CurrentFrame.GetStackTrace());
                            
                            string fieldName = simple.Operand!;
                            if (fieldName.Contains(".")) fieldName = fieldName.Split('.')[1];
                            
                            CurrentFrame.EvaluationStack.Push(instance.GetField(fieldName));
                            break;
                        }

                    case OpCode.Stfld:
                        {
                            var value = CurrentFrame.EvaluationStack.Pop();
                            var instance = CurrentFrame.EvaluationStack.Pop() as ManagedObject;
                            if (instance == null) throw new RuntimeException("stfld requires a managed object instance on stack", CurrentFrame.GetStackTrace());
                            
                            string fieldName = simple.Operand!;
                            if (fieldName.Contains(".")) fieldName = fieldName.Split('.')[1];
                            
                            instance.SetField(fieldName, value);
                            break;
                        }

                    case OpCode.Ret:
                        if (CurrentFrame.Previous != null && !string.Equals(CurrentFrame.Method.ReturnType.Name, "void", StringComparison.Ordinal) && CurrentFrame.EvaluationStack.Count > 0)
                        {
                            var returnValue = CurrentFrame.EvaluationStack.Pop();
                            CurrentFrame.Previous.EvaluationStack.Push(returnValue);
                        }
                        CurrentFrame.IP = CurrentFrame.Method.Body.Statements.Count;
                        break;

                    case OpCode.Add:
                        {
                            if (CurrentFrame.EvaluationStack.Count < 2) throw new RuntimeException("add requires 2 elements on stack", CurrentFrame.GetStackTrace());
                            var (a, b) = PopTwo();
                            CurrentFrame.EvaluationStack.Push(DoBinaryArith(a, b, (x, y) => x + y, (x, y) => x + y));
                            break;
                        }
                    case OpCode.Sub:
                        {
                            if (CurrentFrame.EvaluationStack.Count < 2) throw new RuntimeException("sub requires 2 elements on stack", CurrentFrame.GetStackTrace());
                            var (a, b) = PopTwo();
                            CurrentFrame.EvaluationStack.Push(DoBinaryArith(a, b, (x, y) => x - y, (x, y) => x - y));
                            break;
                        }
                    case OpCode.Mul:
                        {
                            if (CurrentFrame.EvaluationStack.Count < 2) throw new RuntimeException("mul requires 2 elements on stack", CurrentFrame.GetStackTrace());
                            var (a, b) = PopTwo();
                            CurrentFrame.EvaluationStack.Push(DoBinaryArith(a, b, (x, y) => x * y, (x, y) => x * y));
                            break;
                        }
                        case OpCode.Div:
                        {
                            if (CurrentFrame.EvaluationStack.Count < 2) throw new RuntimeException("div requires 2 elements on stack", CurrentFrame.GetStackTrace());
                            var (a, b) = PopTwo();
                            CurrentFrame.EvaluationStack.Push(DoBinaryArith(a, b, (x, y) => x / y, (x, y) => x / y));
                            break;
                        }
                    case OpCode.Rem:
                        {
                            if (CurrentFrame.EvaluationStack.Count < 2) throw new RuntimeException("rem requires 2 elements on stack", CurrentFrame.GetStackTrace());
                            var (a, b) = PopTwo();
                            CurrentFrame.EvaluationStack.Push(DoBinaryArith(a, b, (x, y) => x % y, (x, y) => x % y));
                            break;
                        }
                    
                    case OpCode.Ceq:
                        {
                            if (CurrentFrame.EvaluationStack.Count < 2) throw new RuntimeException("ceq requires 2 elements on stack", CurrentFrame.GetStackTrace());
                            var (a, b) = PopTwo();
                            CurrentFrame.EvaluationStack.Push(new Value<bool>(Equals(Unwrap(a), Unwrap(b))));
                            break;
                        }
                    case OpCode.Cne:
                        {
                            if (CurrentFrame.EvaluationStack.Count < 2) throw new RuntimeException("cne requires 2 elements on stack", CurrentFrame.GetStackTrace());
                            var (a, b) = PopTwo();
                            CurrentFrame.EvaluationStack.Push(new Value<bool>(!Equals(Unwrap(a), Unwrap(b))));
                            break;
                        }
                    case OpCode.Cgt:
                        {
                            if (CurrentFrame.EvaluationStack.Count < 2) throw new RuntimeException("cgt requires 2 elements on stack", CurrentFrame.GetStackTrace());
                            var (a, b) = PopTwo();
                            CurrentFrame.EvaluationStack.Push(new Value<bool>(Compare(Unwrap(a), Unwrap(b)) > 0));
                            break;
                        }
                    case OpCode.Clt:
                        {
                            if (CurrentFrame.EvaluationStack.Count < 2) throw new RuntimeException("clt requires 2 elements on stack", CurrentFrame.GetStackTrace());
                            var (a, b) = PopTwo();
                            CurrentFrame.EvaluationStack.Push(new Value<bool>(Compare(Unwrap(a), Unwrap(b)) < 0));
                            break;
                        }
                    case OpCode.CgtUn:
                        {
                            if (CurrentFrame.EvaluationStack.Count < 2) throw new RuntimeException("cgt.un requires 2 elements on stack", CurrentFrame.GetStackTrace());
                            var (a, b) = PopTwo();
                            CurrentFrame.EvaluationStack.Push(new Value<bool>(CompareUnsigned(Unwrap(a), Unwrap(b)) > 0));
                            break;
                        }
                    case OpCode.CgeUn:
                        {
                            if (CurrentFrame.EvaluationStack.Count < 2) throw new RuntimeException("cge.un requires 2 elements on stack", CurrentFrame.GetStackTrace());
                            var (a, b) = PopTwo();
                            CurrentFrame.EvaluationStack.Push(new Value<bool>(CompareUnsigned(Unwrap(a), Unwrap(b)) >= 0));
                            break;
                        }
                    case OpCode.Not:
                        {
                            var a = CurrentFrame.EvaluationStack.Pop();
                            if (Unwrap(a) is bool boolVal)
                            {
                                CurrentFrame.EvaluationStack.Push(new Value<bool>(!boolVal));
                            }
                            break;
                        }

                    default:
                        throw new OpCodeNotFoundException(OpCodeConverter.ToString(simple.OpCode), CurrentFrame.GetStackTrace());
                        //Console.WriteLine(simple.OpCode, CurrentFrame.GetStackTrace());
                        //break;

                }
            }
            else if (instr is CallInstruction)
            {
                CallInstruction callInstr = (CallInstruction)instr;
                MethodNode targetMethod = ResolveMethod(callInstr.Target);
                if ((object)targetMethod == null)
                {
                    throw new MethodResolutionException(callInstr.Target.Name, CurrentFrame.GetStackTrace());
                }
                ExecuteMethod(targetMethod);
            }
            else if (instr is NewObjInstruction)
            {
                NewObjInstruction newObj = (NewObjInstruction)instr;
                try
                {
                    ManagedObject instance;
                    if (Heap != null)
                    {
                        int handle = Heap.NewObject(newObj.Type.Name).Handle;
                        instance = new ManagedObject(newObj.Type.Name)
                        {
                            Heap = Heap,
                            HeapHandle = handle
                        };
                    }
                    else
                    {
                        instance = new ManagedObject(newObj.Type.Name);
                    }
                    
                    // 2. Resolve and execute constructor if specified
                    if (newObj.Constructor != null)
                    {
                        var ctor = ResolveMethod(newObj.Constructor);
                        if ((object)ctor == null)
                        {
                            throw new MethodResolutionException(newObj.Constructor.Name, CurrentFrame.GetStackTrace());
                        }
                        ExecuteMethod(ctor, instance);
                    }
                    
                    // 3. Push the new instance onto the evaluation stack
                    CurrentFrame.EvaluationStack.Push(instance);
                }
                catch (Exception ex)
                {
                    throw new RuntimeException($"Failed to create object for type {newObj.Type.Name}: {ex.Message}", CurrentFrame.GetStackTrace());
                }
            }
        }
        else if (ins is IfStatement)
        {
            IfStatement ifStmt = (IfStatement)ins;
            if (EvaluateCondition(ifStmt.Condition, ifStmt.Location))
            {
                ExecuteBlock(ifStmt.Then);
            }
            else if ((object)ifStmt.Else != null)
            {
                ExecuteBlock(ifStmt.Else);
            }
        }
        else if (ins is WhileStatement)
        {
            WhileStatement whileStmt = (WhileStatement)ins;
            // The OIR "while (stack) { ... }" structure implies the condition is evaluated
            // *before* the loop body. 
            
            // 1. Identify start of condition instructions
            int conditionStartIP = CurrentFrame.IP - 1;
            var currentLine = whileStmt.Location?.Line;
            if (currentLine.HasValue)
            {
                while (conditionStartIP >= 0)
                {
                    var prevIns = CurrentFrame.Method.Body.Statements[conditionStartIP];
                    if (prevIns.Location?.Line != currentLine) break;
                    conditionStartIP--;
                }
            }
            // conditionStartIP is now the index of the first condition instruction

            // 2. Evaluate the condition (only if stack is not empty)
            if (CurrentFrame.EvaluationStack.Count > 0 && EvaluateCondition(whileStmt.Condition, whileStmt.Location))
            {
                // Execute the body
                ExecuteBlock(whileStmt.Body);
                
                CurrentFrame.IP = conditionStartIP;
            }
            else
            {
                // Loop ended, clean up condition results if necessary.
                // If the loop finished naturally, the condition result is popped by EvaluateCondition.
                // If we fall through, we might need to pop it if it was evaluated just now.
                if (CurrentFrame.EvaluationStack.Count > 0)
                {
                    // This is a safety check: if we're here, the condition evaluated to false.
                    // We might need to pop the condition result.
                    // CurrentFrame.EvaluationStack.Pop(); 
                }
            }
        }
        else if (ins is SwitchStatement)
        {
            ExecuteSwitch((SwitchStatement)ins);
        }
        else if (ins is BlockStatement)
        {
            ExecuteBlock((BlockStatement)ins);
        }
    }

    private void ExecuteSwitch(SwitchStatement stmt)
    {
        object val = EvaluateExpression(stmt.Expression);
        int intVal = Convert.ToInt32(val);

        foreach (var switchCase in stmt.Cases)
        {
            if (switchCase.Value.HasValue && switchCase.Value.Value == intVal)
            {
                ExecuteBlock(switchCase.Body);
                return;
            }
        }

        // Find and execute 'default' case (value is null)
        foreach (var switchCase in stmt.Cases)
        {
            if (!switchCase.Value.HasValue)
            {
                ExecuteBlock(switchCase.Body);
                return;
            }
        }
    }

    private object EvaluateExpression(string expression)
    {
        if (string.Equals(expression, "stack", StringComparison.Ordinal))
        {
            if (CurrentFrame.EvaluationStack.Count == 0)
            {
                throw new RuntimeException("switch expression requires value on stack, but it is empty.", CurrentFrame.GetStackTrace());
            }
            object val = CurrentFrame.EvaluationStack.Pop();
            return Unwrap(val) ?? val;
        }
        
        // Assume it's a local or something else if not "stack"
        if (CurrentFrame.Locals.ContainsKey(expression))
        {
            var val = CurrentFrame.Locals[expression];
            return Unwrap(val) ?? val;
        }

        return expression; // literal or other
    }

    private void ExecuteBlock(BlockStatement block)
    {
        foreach (Statement stmt in block.Statements)
        {
            ExecuteInstruction(stmt);
        }
    }

    private bool EvaluateCondition(string condition, SourceLocation ins)
    {
        if (string.Equals(condition, "stack", StringComparison.Ordinal))
        {
            if (CurrentFrame.EvaluationStack.Count == 0)
            {
                var locInfo = (ins != null) ? $"\n at {ins.Line}: {ins.SourceLine}" : "";
                throw new RuntimeException($"condition requires value on stack, but it is empty.{locInfo}", CurrentFrame.GetStackTrace());
            }
            object val = CurrentFrame.EvaluationStack.Pop();
            object? data = Unwrap(val);
            
            if (data is bool boolVal)
            {
                return boolVal;
            }
            if (data is int intVal)
            {
                return intVal != 0;
            }
            return data != null;
        }
        return false;
    }

    public MethodNode? ResolveMethod(MethodReference target)
    {
        // 1. Local resolution (already loaded in AST)
        var method = ResolveLocalMethod(target);
        if (method != null) return method;

        // 2. On-demand dynamic registration from hooks
        if (NativeRegistry.TryRegister(target.DeclaringType.Name, program))
        {
            // Now that it's registered in the AST, resolve it locally
            return ResolveLocalMethod(target);
        }

        return null;
    }

    private MethodNode ResolveLocalMethod(MethodReference target)
    {
        // Handle "this" prefix: resolve against the current class context
        if (string.Equals(target.DeclaringType.Name, "this", StringComparison.Ordinal))
        {
            return ResolveThisMethod(target);
        }

        // 1. Search in main program module
        var node = ResolveInModule(program, target);
        if (node != null) return node;

        // 2. Search in other loaded modules
        foreach (var mod in Modules)
        {
            node = ResolveInModule(mod, target);
            if (node != null) return node;
        }

        return null;
    }

    private MethodNode? ResolveThisMethod(MethodReference target)
    {
        var currentMethod = CurrentFrame?.Method;
        if (currentMethod != null)
        {
            // Find which class contains the current method and search there first
            var cls = FindClassByMethod(currentMethod);
            if (cls != null)
            {
                var node = FindMethodInClass(cls, target.Name);
                if (node != null) return node;
            }
        }

        // Fallback: search all classes in all modules by method name
        foreach (var cls in program.Classes)
        {
            var node = FindMethodInClass(cls, target.Name);
            if (node != null) return node;
        }
        foreach (var mod in Modules)
        {
            foreach (var cls in mod.Classes)
            {
                var node = FindMethodInClass(cls, target.Name);
                if (node != null) return node;
            }
        }

        return null;
    }

    private ClassNode? FindClassByMethod(MethodNode method)
    {
        foreach (var cls in program.Classes)
        {
            if (cls.Methods.Contains(method))
                return cls;
        }
        foreach (var mod in Modules)
        {
            foreach (var cls in mod.Classes)
            {
                if (cls.Methods.Contains(method))
                    return cls;
            }
        }
        return null;
    }

    private MethodNode? FindMethodInClass(ClassNode cls, string methodName)
    {
        foreach (MethodNode meth in cls.Methods)
        {
            if (string.Equals(meth.Name, methodName, StringComparison.Ordinal))
            {
                return meth;
            }
        }
        return null;
    }

    private MethodNode ResolveInModule(ModuleNode mod, MethodReference target)
    {
        foreach (ClassNode cls in mod.Classes)
        {
            if (!string.Equals(cls.Name, target.DeclaringType.Name, StringComparison.Ordinal))
            {
                continue;
            }
            if (Debug)
            // Log matching class
                Console.WriteLine($"[CPU] Resolved class: {cls.Name}. Looking for method: {target.Name}");
            
            // Check methods
            foreach (MethodNode meth in cls.Methods)
            {
                if (string.Equals(meth.Name, target.Name, StringComparison.Ordinal))
                {
                    return meth;
                }
            }
            
            // Check constructors
            if (string.Equals(target.Name, "constructor", StringComparison.Ordinal) || 
                string.Equals(target.Name, ".ctor", StringComparison.Ordinal))
            {
                var ctor = cls.Constructors.FirstOrDefault();
                if (ctor != null)
                {
                    return Cache.GetOrCreateCtorMethodNode(ctor);
                }
            }
        }
        return null;
    }
    private (object? a, object? b) PopTwo()
    {
        var b = CurrentFrame!.EvaluationStack.Pop();
        var a = CurrentFrame!.EvaluationStack.Pop();
        return (a, b);
    }

    private static object DoBinaryArith(object? a, object? b, Func<int, int, int> intOp, Func<double, double, double> floatOp)
    {
        var ua = Unwrap(a);
        var ub = Unwrap(b);
        if (ua is float || ua is double || ub is float || ub is double)
        {
            return new Value<double>(floatOp(Convert.ToDouble(ua), Convert.ToDouble(ub)));
        }
        return new Value<int>(intOp(Convert.ToInt32(ua), Convert.ToInt32(ub)));
    }

    private static object? Unwrap(object? val)
        => val is IValue v ? v.GetObjectData() : val;

    private static int Compare(object? a, object? b)
    {
        if (a is IComparable ca) return ca.CompareTo(b);
        return 0;
    }

    // Handles null-check pattern (ldnull + cgt.un) and float NaN semantics
    private static int CompareUnsigned(object? a, object? b)
    {
        // reference null check: any ref > null
        if (b is null) return a is null ? 0 : 1;
        if (a is null) return -1;

        // float unordered: NaN makes the comparison return "greater"
        if (a is double da && b is double db)
        {
            if (double.IsNaN(da) || double.IsNaN(db)) return 1;
            return da.CompareTo(db);
        }
        if (a is float fa && b is float fb)
        {
            if (float.IsNaN(fa) || float.IsNaN(fb)) return 1;
            return fa.CompareTo(fb);
        }

        // unsigned integers
        if (a is int ia && b is int ib)
            return ((uint)ia).CompareTo((uint)ib);

        if (a is IComparable ca) return ca.CompareTo(b);
        return 0;
    }

    public void CompileAll()
    {
        var classes = program.Classes.ToArray();
        foreach (var cls in classes)
        {
            var methods = cls.Methods.ToArray();
            foreach (var method in methods)
            {
                if (Cache.GetCompiled(method) == null && method.Body?.Statements.Count > 0)
                {
                    Cache.CompileAndStore(method);
                }
            }
        }

        // Eagerly resolve all targets in compiled methods (call targets, ctor targets, field names)
        ResolveAllTargets();
    }

    private void ResolveAllTargets()
    {
        var classes = program.Classes.ToArray();
        foreach (var cls in classes)
        {
            var methods = cls.Methods.ToArray();
            foreach (var method in methods)
            {
                var cm = Cache.GetCompiled(method);
                if (cm != null) cm.ResolveTargets(this);
            }
        }
    }

    public CompiledMethod? GetCompiled(MethodNode method)
    {
        return Cache.GetCompiled(method);
    }

    private void QueueJitCompile(MethodNode method, CompiledMethod cm)
    {
        if (!Cache.TryAddJit(method)) return;

        ThreadPool.QueueUserWorkItem(_ =>
        {
            try
            {
                var jit = JitCompiler.GetOrCompile(cm);
                Cache.SetJit(method, jit);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[JIT] Background compile failed for {method.Name}: {ex.Message}");
                Cache.RemoveJit(method);
            }
        });
    }

    public void ForceJit()
    {
        var allMethods = new List<MethodNode>();
        void CollectClass(ClassNode cls)
        {
            foreach (var m in cls.Methods)
                if (m.NativeImpl == null)
                    allMethods.Add(m);
        }
        void CollectModule(ModuleNode mod)
        {
            foreach (var cls in mod.Classes)
                CollectClass(cls);
        }

        CollectModule(program);
        foreach (var mod in Modules)
            CollectModule(mod);

        foreach (var method in allMethods)
        {
            var cm = Cache.GetCompiled(method) ?? Cache.CompileAndStore(method);
            cm.ResolveTargets(this);
            var jit = JitCompiler.GetOrCompile(cm);
            Cache.SetJit(method, jit);
        }
    }

    public T CallMethod<T>(string methodPath, params object[] args)
    {
        var parts = methodPath.Split('.');
        if (parts.Length < 2)
            throw new ArgumentException($"Expected 'ClassName.MethodName', got '{methodPath}'");

        string className = parts[parts.Length - 2];
        string methodName = parts[parts.Length - 1];

        var cls = FindClass(className);
        if (cls == null)
            throw new MethodResolutionException(methodPath, CurrentFrame?.GetStackTrace() ?? "");

        var method = cls.Methods.FirstOrDefault(m => string.Equals(m.Name, methodName, StringComparison.Ordinal));
        if (method == null)
            throw new MethodResolutionException(methodPath, CurrentFrame?.GetStackTrace() ?? "");

        // Try JIT native path
        if (Features.HasFlag(ExperimentalFeature.Jit))
        {
            var cmJit = Cache.GetCompiled(method);
            var jitDel = cmJit != null ? Cache.GetJit(method) : null;
            if (cmJit != null && jitDel != null)
            {
                var jitArgs = new object?[method.Parameters.Count];
                for (int i = 0; i < jitArgs.Length; i++)
                    jitArgs[i] = i < args.Length ? args[i] : null;
                var jitResult = jitDel(jitArgs, this, cmJit);
                if (cmJit.ReturnsValue)
                {
                    var unwrapped = jitResult;
                    if (unwrapped != null && typeof(T) != unwrapped.GetType())
                        return (T)Convert.ChangeType(unwrapped, typeof(T));
                    return (T)(unwrapped ?? default(T?)!);
                }
                return default;
            }
        }

        // Try compiled interpreter path
        var cm = Cache.GetCompiled(method);
        if (cm != null)
        {
            // Count execution for tiered compilation
            int count = Cache.IncrementExecutionCount(method);
            if (count == 1000)
                QueueJitCompile(method, cm);
            var rawArgs = new StackValue[method.Parameters.Count];
            for (int i = 0; i < rawArgs.Length; i++)
                rawArgs[i] = i < args.Length
                    ? CompiledExecutor.RawToStackValue(args[i])
                    : default;

            var compiledResult = CompiledExecutor.Execute(cm, rawArgs, this);

            if (cm.ReturnsValue)
            {
                var unwrapped = compiledResult.ToObject();
                if (unwrapped != null && typeof(T) != unwrapped.GetType())
                    return (T)Convert.ChangeType(unwrapped, typeof(T));
                return (T)(unwrapped ?? default(T?)!);
            }
            return default;
        }

        // Fallback to AST path
        bool hadFrame = CurrentFrame != null;
        if (!hadFrame)
            CurrentFrame = new CallStack(method, null);

        ExecuteMethod(method, null, args);

        T result = default;
        if (CurrentFrame?.EvaluationStack.Count > 0)
        {
            var val = CurrentFrame.EvaluationStack.Pop();
            var unwrapped = Unwrap(val);
            if (unwrapped != null && typeof(T) != unwrapped.GetType())
                result = (T)Convert.ChangeType(unwrapped, typeof(T));
            else
                result = (T)(unwrapped ?? default(T?)!);
        }

        if (!hadFrame)
            CurrentFrame = null;

        return result;
    }

    private ClassNode? FindClass(string className)
    {
        var cls = program.Classes.FirstOrDefault(c => string.Equals(c.Name, className, StringComparison.Ordinal));
        if (cls != null) return cls;

        foreach (var mod in Modules)
        {
            cls = mod.Classes.FirstOrDefault(c => string.Equals(c.Name, className, StringComparison.Ordinal));
            if (cls != null) return cls;
        }

        return null;
    }
}
