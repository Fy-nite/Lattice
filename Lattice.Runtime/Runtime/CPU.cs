using System.Runtime.CompilerServices;
using lattice.Connectors;
using lattice.Core;
using lattice.Throwables;
using Microsoft.VisualBasic.CompilerServices;
using ObjectIR.Core;
using ObjectIR.Core.AST;
using ObjectIR.StdLib.Core.Generics;
using ObjectIR.StdLib.Core.Memory;

public class CPU : IProgramLoader
{
    public lattice.Runtime.Scheduler Scheduler { get; set; }
    public List<ModuleNode> Modules = new();

    public ModuleNode program;

    public CallStack CurrentFrame;

    public bool Debug { get; set; }

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
        
        if (Operators.CompareString(node.ReturnType.Name, "void", false) != 0 && CurrentFrame?.EvaluationStack.Count > 0)
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
        var cls = program.Classes.FirstOrDefault(c => Operators.CompareString(c.Name, typeRef.Name, false) == 0);
        if (cls != null) return cls;

        // 2. Search in other loaded modules
        foreach (var mod in Modules)
        {
            cls = mod.Classes.FirstOrDefault(c => Operators.CompareString(c.Name, typeRef.Name, false) == 0);
            if (cls != null) return cls;
        }

        // 3. Try to resolve via dynamic hooks if not found
        if (NativeRegistry.TryRegister(typeRef.Name, program))
        {
            return program.Classes.FirstOrDefault(c => Operators.CompareString(c.Name, typeRef.Name, false) == 0);
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
            Modules = new List<ModuleNode>(this.Modules), // Share loaded modules
            Debug = this.Debug,
            MaxStackDepth = this.MaxStackDepth,
            Scheduler = this.Scheduler
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

    public CPU()
    {
        Debug = false;
        MaxStackDepth = 1000;
    }

    public void InitializeMain(string[] args)
    {
        ClassNode programClass = program.Classes.FirstOrDefault([SpecialName] (ClassNode c) => Operators.CompareString(c.Name, "Program", TextCompare: false) == 0);
        MethodNode main = null;
        if ((object)programClass != null)
        {
            main = programClass.Methods.FirstOrDefault([SpecialName] (MethodNode m) => Operators.CompareString(m.Name, "Main", TextCompare: false) == 0);
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

                    if (Operators.CompareString(method.ReturnType.Name, "void", TextCompare: false) != 0 && result != null && CurrentFrame.Previous != null)
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
                switch (simple.OpCode.ToLower())
                {
                    case "ldstr":
                        {
                            string str = simple.Operand.ToString();
                            if (str.StartsWith("\"") && str.EndsWith("\""))
                            {
                                str = str.Substring(1, checked(str.Length - 2));
                            }
                            // Handle escape sequences
                            str = str.Replace("\\n", "\n").Replace("\\t", "\t").Replace("\\r", "\r");
                            
                            CurrentFrame.EvaluationStack.Push(new Value<string>(str));
                            break;
                        }
                    case "ldc.i4":
                        CurrentFrame.EvaluationStack.Push(new Value<int>(int.Parse(simple.Operand!)));
                        break;

                    case "ldc.r4":
                        CurrentFrame.EvaluationStack.Push(new Value<float>(float.Parse(simple.Operand!)));
                        break;

                    case "ldnull":
                        CurrentFrame.EvaluationStack.Push(null);
                        break;

                    case "ldloc":
                        CurrentFrame.EvaluationStack.Push(CurrentFrame.Locals[simple.Operand!]);
                        break;

                    case "stloc":
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

                    case "starg":
                        if (CurrentFrame.EvaluationStack.Count == 0)
                        {
                            var sx = ins.Location;
                            throw new RuntimeException($"starg '{simple.Operand}' requires value on stack, but it is empty at {sx.Line}: {sx.SourceLine}", CurrentFrame.GetStackTrace());
                        }
                        CurrentFrame.Args[simple.Operand!] = CurrentFrame.EvaluationStack.Pop();
                        break;

                    case "ldarg":
                        CurrentFrame.EvaluationStack.Push(CurrentFrame.Args[simple.Operand!]);
                        break;

                    case "dup":
                        CurrentFrame.EvaluationStack.Push(CurrentFrame.EvaluationStack.Peek());
                        break;

                    case "pop":
                        CurrentFrame.EvaluationStack.Pop();
                        break;

                    case "ldfld":
                        {
                            var instance = CurrentFrame.EvaluationStack.Pop() as ManagedObject;
                            if (instance == null) throw new RuntimeException("ldfld requires a managed object instance on stack", CurrentFrame.GetStackTrace());
                            
                            string fieldName = simple.Operand!;
                            if (fieldName.Contains(".")) fieldName = fieldName.Split('.')[1];
                            
                            CurrentFrame.EvaluationStack.Push(instance.GetField(fieldName));
                            break;
                        }

                    case "stfld":
                        {
                            var value = CurrentFrame.EvaluationStack.Pop();
                            var instance = CurrentFrame.EvaluationStack.Pop() as ManagedObject;
                            if (instance == null) throw new RuntimeException("stfld requires a managed object instance on stack", CurrentFrame.GetStackTrace());
                            
                            string fieldName = simple.Operand!;
                            if (fieldName.Contains(".")) fieldName = fieldName.Split('.')[1];
                            
                            instance.SetField(fieldName, value);
                            break;
                        }

                    case "ret":
                        CurrentFrame.IP = CurrentFrame.Method.Body.Statements.Count;
                        break;

                    case "add":
                        {
                            if (CurrentFrame.EvaluationStack.Count < 2) throw new RuntimeException("add requires 2 elements on stack", CurrentFrame.GetStackTrace());
                            var (a, b) = PopTwo();
                            CurrentFrame.EvaluationStack.Push(new Value<int>(Convert.ToInt32(Unwrap(a)) + Convert.ToInt32(Unwrap(b))));
                            break;
                        }
                    case "sub":
                        {
                            if (CurrentFrame.EvaluationStack.Count < 2) throw new RuntimeException("sub requires 2 elements on stack", CurrentFrame.GetStackTrace());
                            var (a, b) = PopTwo();
                            CurrentFrame.EvaluationStack.Push(new Value<int>(Convert.ToInt32(Unwrap(a)) - Convert.ToInt32(Unwrap(b))));
                            break;
                        }
                    case "mul":
                        {
                            if (CurrentFrame.EvaluationStack.Count < 2) throw new RuntimeException("mul requires 2 elements on stack", CurrentFrame.GetStackTrace());
                            var (a, b) = PopTwo();
                            CurrentFrame.EvaluationStack.Push(new Value<int>(Convert.ToInt32(Unwrap(a)) * Convert.ToInt32(Unwrap(b))));
                            break;
                        }
                    case "div":
                        {
                            if (CurrentFrame.EvaluationStack.Count < 2) throw new RuntimeException("div requires 2 elements on stack", CurrentFrame.GetStackTrace());
                            var (a, b) = PopTwo();
                            CurrentFrame.EvaluationStack.Push(new Value<int>(Convert.ToInt32(Unwrap(a)) / Convert.ToInt32(Unwrap(b))));
                            break;
                        }
                    
                    case "ceq":
                        {
                            if (CurrentFrame.EvaluationStack.Count < 2) throw new RuntimeException("ceq requires 2 elements on stack", CurrentFrame.GetStackTrace());
                            var (a, b) = PopTwo();
                            CurrentFrame.EvaluationStack.Push(new Value<bool>(Equals(Unwrap(a), Unwrap(b))));
                            break;
                        }
                    case "cne":
                        {
                            if (CurrentFrame.EvaluationStack.Count < 2) throw new RuntimeException("cne requires 2 elements on stack", CurrentFrame.GetStackTrace());
                            var (a, b) = PopTwo();
                            CurrentFrame.EvaluationStack.Push(new Value<bool>(!Equals(Unwrap(a), Unwrap(b))));
                            break;
                        }
                    case "cgt":
                        {
                            if (CurrentFrame.EvaluationStack.Count < 2) throw new RuntimeException("cgt requires 2 elements on stack", CurrentFrame.GetStackTrace());
                            var (a, b) = PopTwo();
                            CurrentFrame.EvaluationStack.Push(new Value<bool>(Compare(Unwrap(a), Unwrap(b)) > 0));
                            break;
                        }
                    case "clt":
                        {
                            if (CurrentFrame.EvaluationStack.Count < 2) throw new RuntimeException("clt requires 2 elements on stack", CurrentFrame.GetStackTrace());
                            var (a, b) = PopTwo();
                            CurrentFrame.EvaluationStack.Push(new Value<bool>(Compare(Unwrap(a), Unwrap(b)) < 0));
                            break;
                        }
                    case "cgt.un":
                        {
                            if (CurrentFrame.EvaluationStack.Count < 2) throw new RuntimeException("cgt.un requires 2 elements on stack", CurrentFrame.GetStackTrace());
                            var (a, b) = PopTwo();
                            CurrentFrame.EvaluationStack.Push(new Value<bool>(CompareUnsigned(Unwrap(a), Unwrap(b)) > 0));
                            break;
                        }
                    case "cge.un":
                        {
                            if (CurrentFrame.EvaluationStack.Count < 2) throw new RuntimeException("cge.un requires 2 elements on stack", CurrentFrame.GetStackTrace());
                            var (a, b) = PopTwo();
                            CurrentFrame.EvaluationStack.Push(new Value<bool>(CompareUnsigned(Unwrap(a), Unwrap(b)) >= 0));
                            break;
                        }
                    case "not":
                        {
                            var a = CurrentFrame.EvaluationStack.Pop();
                            if (Unwrap(a) is bool boolVal)
                            {
                                CurrentFrame.EvaluationStack.Push(new Value<bool>(!boolVal));
                            }
                            break;
                        }

                    default:
                        throw new OpCodeNotFoundException(simple.OpCode, CurrentFrame.GetStackTrace());
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
                    // 1. Allocate the managed object
                    ManagedObject instance = new ManagedObject(newObj.Type.Name);
                    
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
                
                // Rewind IP to the start of the condition so it gets re-executed
                CurrentFrame.IP = conditionStartIP - 1; 
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
        if (Operators.CompareString(expression, "stack", TextCompare: false) == 0)
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
        if (Operators.CompareString(condition, "stack", TextCompare: false) == 0)
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

    private MethodNode ResolveMethod(MethodReference target)
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

    private MethodNode ResolveInModule(ModuleNode mod, MethodReference target)
    {
        foreach (ClassNode cls in mod.Classes)
        {
            if (Operators.CompareString(cls.Name, target.DeclaringType.Name, TextCompare: false) != 0)
            {
                continue;
            }
            if (Debug)
            // Log matching class
                Console.WriteLine($"[CPU] Resolved class: {cls.Name}. Looking for method: {target.Name}");
            
            // Check methods
            foreach (MethodNode meth in cls.Methods)
            {
                if (Operators.CompareString(meth.Name, target.Name, TextCompare: false) == 0)
                {
                    return meth;
                }
            }
            
            // Check constructors
            if (Operators.CompareString(target.Name, "constructor", TextCompare: false) == 0 || 
                Operators.CompareString(target.Name, ".ctor", TextCompare: false) == 0)
            {
                // Find constructor with matching parameter count
                var ctor = cls.Constructors.FirstOrDefault();
                
                if (ctor != null)
                {
                    // Synthesize a MethodNode so ExecuteMethod can handle it
                    return new MethodNode("constructor", ctor.Parameters, TypeRef.Void, false, null, ctor.Body);
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
}
