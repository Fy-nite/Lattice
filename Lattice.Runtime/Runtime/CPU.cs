using System.Runtime.CompilerServices;
using lattice.Connectors;
using lattice.Core;
using lattice.Throwables;
using Microsoft.VisualBasic.CompilerServices;
using ObjectIR.Core;
using ObjectIR.Core.AST;

public class CPU
{
    public ModuleNode program;

    public CallStack CurrentFrame;

    public bool Debug { get; set; }

    public int MaxStackDepth { get; set; }

    public CPU()
    {
        Debug = false;
        MaxStackDepth = 1000;
    }

    public void Run(string[] args)
    {
        ClassNode programClass = program.Classes.FirstOrDefault([SpecialName] (ClassNode c) => Operators.CompareString(c.Name, "Program", TextCompare: false) == 0);
        MethodNode main = null;
        if ((object)programClass != null)
        {
            main = programClass.Methods.FirstOrDefault([SpecialName] (MethodNode m) => Operators.CompareString(m.Name, "Main", TextCompare: false) == 0);
            if ((object)main != null && !main.IsStatic)
            {
                throw new EntrypointNotFoundException("Program does not contain a static main suitable for entrypoint", "Ensure your 'Main' method in the 'Program' class is marked as static.");
            }
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
        ExecuteMethod(main, null, mainArgs.ToArray());
    }

    public void LoadProgram(string path)
    {
        program = TextIrParser.ParseModule(File.ReadAllText(path));
        if ((object)program == null)
        {
            throw new FileNotFoundException($"File not found: {path}, are you sure that the file exists?");
        }
        program.Classes.AddRange(new StdlibConnector().GetStdlib());
    }

    public void LoadModule(ModuleNode Modz)
    {
        program = Modz;
        program.Classes.AddRange(new StdlibConnector().GetStdlib());
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
        if (Debug)
        {
            Console.WriteLine($"[DEBUG] Executing method: {method.Name}");
        }
        int argCount = method.Parameters.Count;
        checked
        {
            object[] poppedArgs = new object[argCount - 1 + 1];
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
                if (Debug)
                {
                    Console.WriteLine($"[DEBUG] Calling native method: {method.Name}");
                }
                Value<object>[] nativeArgs = new Value<object>[argCount - 1 + 1];
                int num3 = argCount - 1;
                for (int k = 0; k <= num3; k++)
                {
                    object popped = RuntimeHelpers.GetObjectValue(poppedArgs[k]);
                    if (Debug)
                    {
                        Console.WriteLine($"[DEBUG]   Arg {k}: {RuntimeHelpers.GetObjectValue(popped)}");
                    }
                    if (popped is Value<object>)
                    {
                        nativeArgs[k] = (Value<object>)popped;
                    }
                    else
                    {
                        nativeArgs[k] = new Value<object>(RuntimeHelpers.GetObjectValue(popped));
                    }
                }
                Value<object> result = method.NativeImpl.Method(nativeArgs);
                if (Operators.CompareString(method.ReturnType.Name, "void", TextCompare: false) != 0 && result != null && CurrentFrame != null)
                {
                    CurrentFrame.EvaluationStack.Push(result);
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
            try
            {
                while (CurrentFrame.IP < CurrentFrame.Method.Body.Statements.Count)
                {
                    Statement Instruction = CurrentFrame.Method.Body.Statements[CurrentFrame.IP];
                    ExecuteInstruction(Instruction);
                    CurrentFrame.IP++;
                }
            }
            finally
            {
                CurrentFrame = oldFrame;
            }
        }
    }

    public void ExecuteInstruction(Statement ins)
    {
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
                            CurrentFrame.EvaluationStack.Push(new Value<object>(str));
                            break;
                        }
                    case "ldc.i4":
                        CurrentFrame.EvaluationStack.Push(new Value<object>(int.Parse(simple.Operand!)));
                        break;

                    case "ldc.r4":
                        CurrentFrame.EvaluationStack.Push(new Value<object>(float.Parse(simple.Operand!)));
                        break;

                    case "ldnull":
                        CurrentFrame.EvaluationStack.Push(null);
                        break;

                    case "ldloc":
                        CurrentFrame.EvaluationStack.Push(CurrentFrame.Locals[simple.Operand!]);
                        break;

                    case "stloc":
                        CurrentFrame.Locals[simple.Operand!] = CurrentFrame.EvaluationStack.Pop();
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

                    case "ret":
                        CurrentFrame.IP = CurrentFrame.Method.Body.Statements.Count;
                        break;

                    case "add":
                        {
                            var (a, b) = PopTwo();
                            CurrentFrame.EvaluationStack.Push(new Value<object>(Convert.ToInt32(a) + Convert.ToInt32(b)));
                            break;
                        }
                    case "sub":
                        {
                            var (a, b) = PopTwo();
                            CurrentFrame.EvaluationStack.Push(new Value<object>(Convert.ToInt32(a) - Convert.ToInt32(b)));
                            break;
                        }
                    case "ceq":
                        {
                            var (a, b) = PopTwo();
                            CurrentFrame.EvaluationStack.Push(new Value<object>(Equals(Unwrap(a), Unwrap(b))));
                            break;
                        }
                    case "cne":
                        {
                            var (a, b) = PopTwo();
                            CurrentFrame.EvaluationStack.Push(new Value<object>(!Equals(Unwrap(a), Unwrap(b))));
                            break;
                        }
                    case "cgt":
                        {
                            var (a, b) = PopTwo();
                            CurrentFrame.EvaluationStack.Push(new Value<object>(
                                Compare(Unwrap(a), Unwrap(b)) > 0));
                            break;
                        }
                    case "clt":
                        {
                            var (a, b) = PopTwo();
                            CurrentFrame.EvaluationStack.Push(new Value<object>(
                                Compare(Unwrap(a), Unwrap(b)) < 0));
                            break;
                        }
                    case "cgt.un":
                        {
                            var (a, b) = PopTwo();
                            CurrentFrame.EvaluationStack.Push(new Value<object>(
                                CompareUnsigned(Unwrap(a), Unwrap(b)) > 0));
                            break;
                        }
                    case "cge.un":
                        {
                            var (a, b) = PopTwo();
                            CurrentFrame.EvaluationStack.Push(new Value<object>(
                                CompareUnsigned(Unwrap(a), Unwrap(b)) >= 0));
                            break;
                        }

                    default:
                        Console.WriteLine(simple.OpCode, CurrentFrame.GetStackTrace());
                        break;

                }
                        //throw new OpCodeNotFoundException(simple.OpCode, CurrentFrame.GetStackTrace());
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
        }
        else if (ins is IfStatement)
        {
            IfStatement ifStmt = (IfStatement)ins;
            if (EvaluateCondition(ifStmt.Condition))
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
            while (EvaluateCondition(whileStmt.Condition))
            {
                ExecuteBlock(whileStmt.Body);
            }
        }
        else if (ins is BlockStatement)
        {
            ExecuteBlock((BlockStatement)ins);
        }
    }

    private void ExecuteBlock(BlockStatement block)
    {
        foreach (Statement stmt in block.Statements)
        {
            ExecuteInstruction(stmt);
        }
    }

    private bool EvaluateCondition(string condition)
    {
        if (Operators.CompareString(condition, "stack", TextCompare: false) == 0)
        {
            object val = RuntimeHelpers.GetObjectValue(CurrentFrame.EvaluationStack.Pop());
            object data = RuntimeHelpers.GetObjectValue(val);
            if (val is Value<object>)
            {
                data = RuntimeHelpers.GetObjectValue(((Value<object>)val).Data);
            }
            if (!(data is bool EvaluateCondition))
            {
                if (data is int)
                {
                    return (int)data != 0;
                }
                return data != null;
            }
            return EvaluateCondition;
        }
        return false;
    }

    private MethodNode ResolveMethod(MethodReference target)
    {
        foreach (ClassNode cls in program.Classes)
        {
            if (Operators.CompareString(cls.Name, target.DeclaringType.Name, TextCompare: false) != 0)
            {
                continue;
            }
            foreach (MethodNode meth in cls.Methods)
            {
                if (Operators.CompareString(meth.Name, target.Name, TextCompare: false) == 0)
                {
                    return meth;
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
        => val is Value<object> v ? v.Data : val;

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
