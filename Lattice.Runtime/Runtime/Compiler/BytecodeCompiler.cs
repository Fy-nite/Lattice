using ObjectIR.Core;
using ObjectIR.Core.AST;
using ObjectIR.Core.Ast;

namespace lattice.Runtime.Compiler;

public static class BytecodeCompiler
{
    public static CompiledMethod Compile(MethodNode method)
    {
        var localNames = new List<string>();
        var localNameMap = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var l in method.Locals)
        {
            if (!localNameMap.ContainsKey(l.Name))
            {
                localNameMap[l.Name] = localNames.Count;
                localNames.Add(l.Name);
            }
        }

        var argNames = method.Parameters.Select(p => p.Name).ToArray();

        var stringTable = new List<string>();
        var floatTable = new List<float>();
        var doubleTable = new List<double>();
        var stringIndexMap = new Dictionary<string, int>(StringComparer.Ordinal);
        var floatIndexMap = new Dictionary<int, int>();
        var doubleIndexMap = new Dictionary<long, int>();

        var callTargets = new List<CallInstruction>();
        var newObjTargets = new List<NewObjInstruction>();

        Stack<(int loopStartIP, int endPatchIP)> loopStack = new();

        int GetStringIdx(string s)
        {
            if (stringIndexMap.TryGetValue(s, out var idx)) return idx;
            idx = stringTable.Count;
            stringTable.Add(s);
            stringIndexMap[s] = idx;
            return idx;
        }

        int GetFloatIdx(float f)
        {
            var bits = BitConverter.SingleToInt32Bits(f);
            if (floatIndexMap.TryGetValue(bits, out var idx)) return idx;
            idx = floatTable.Count;
            floatTable.Add(f);
            floatIndexMap[bits] = idx;
            return idx;
        }

        int GetDoubleIdx(double d)
        {
            var bits = BitConverter.DoubleToInt64Bits(d);
            if (doubleIndexMap.TryGetValue(bits, out var idx)) return idx;
            idx = doubleTable.Count;
            doubleTable.Add(d);
            doubleIndexMap[bits] = idx;
            return idx;
        }

        var code = new List<CompactInstr>();

        int Emit(OpCode op, int operand = 0)
        {
            int idx = code.Count;
            code.Add(new CompactInstr(op, operand));
            return idx;
        }

        void Patch(int emitIdx, int target)
        {
            code[emitIdx] = new CompactInstr(code[emitIdx].Opcode, target);
        }

        int ResolveLocal(string name)
        {
            if (localNameMap.TryGetValue(name, out var idx)) return idx;
            idx = localNames.Count;
            localNameMap[name] = idx;
            localNames.Add(name);
            return idx;
        }

        int ResolveArg(string name)
        {
            for (int i = 0; i < argNames.Length; i++)
                if (string.Equals(argNames[i], name, StringComparison.Ordinal))
                    return i;
            return -1;
        }

        void CompileInstructionStmt(InstructionStatement stmt)
        {
            var instr = stmt.Instruction;
            if (instr is SimpleInstruction simple)
            {
                switch (simple.OpCode)
                {
                    case OpCode.Ldstr:
                    {
                        var str = simple.Operand ?? "";
                        if (str.StartsWith("\"") && str.EndsWith("\""))
                            str = str.Substring(1, str.Length - 2);
                        str = str.Replace("\\n", "\n").Replace("\\t", "\t").Replace("\\r", "\r");
                        Emit(OpCode.Ldstr, GetStringIdx(str));
                        break;
                    }
                    case OpCode.LdcI4:
                        Emit(OpCode.LdcI4, int.Parse(simple.Operand!));
                        break;
                    case OpCode.LdcI8:
                        Emit(OpCode.LdcI8, (int)long.Parse(simple.Operand!));
                        break;
                    case OpCode.LdcR4:
                        Emit(OpCode.LdcR4, GetFloatIdx(float.Parse(simple.Operand!)));
                        break;
                    case OpCode.LdcR8:
                        Emit(OpCode.LdcR8, GetDoubleIdx(double.Parse(simple.Operand!)));
                        break;
                    case OpCode.Ldc:
                        if (simple.Operand != null && long.TryParse(simple.Operand, out var lval))
                            Emit(OpCode.LdcI8, (int)lval);
                        else
                            Emit(OpCode.Ldnull);
                        break;
                    case OpCode.Ldnull:       Emit(OpCode.Ldnull); break;
                    case OpCode.Ldloc:        Emit(OpCode.Ldloc, ResolveLocal(simple.Operand!)); break;
                    case OpCode.Stloc:        Emit(OpCode.Stloc, ResolveLocal(simple.Operand!)); break;
                    case OpCode.Ldarg:        Emit(OpCode.Ldarg, ResolveArg(simple.Operand!)); break;
                    case OpCode.Starg:        Emit(OpCode.Starg, ResolveArg(simple.Operand!)); break;
                    case OpCode.Dup:          Emit(OpCode.Dup); break;
                    case OpCode.Pop:          Emit(OpCode.Pop); break;
                    case OpCode.Nop:          Emit(OpCode.Nop); break;
                    case OpCode.Ret:          Emit(OpCode.Ret); break;
                    case OpCode.Ldfld:        Emit(OpCode.Ldfld, GetStringIdx(simple.Operand!)); break;
                    case OpCode.Stfld:        Emit(OpCode.Stfld, GetStringIdx(simple.Operand!)); break;
                    case OpCode.Ldsfld:       Emit(OpCode.Ldsfld, GetStringIdx(simple.Operand!)); break;
                    case OpCode.Stsfld:       Emit(OpCode.Stsfld, GetStringIdx(simple.Operand!)); break;
                    case OpCode.Newarr:       Emit(OpCode.Newarr); break;
                    case OpCode.Ldelem:       Emit(OpCode.Ldelem); break;
                    case OpCode.Stelem:       Emit(OpCode.Stelem); break;
                    case OpCode.Castclass:    Emit(OpCode.Castclass); break;
                    case OpCode.Isinst:       Emit(OpCode.Isinst); break;
                    case OpCode.Neg:          Emit(OpCode.Neg); break;
                    case OpCode.Not:          Emit(OpCode.Not); break;
                    case OpCode.Conv:         Emit(OpCode.Conv, GetStringIdx(simple.Operand ?? "int32")); break;
                    case OpCode.Throw:        Emit(OpCode.Throw); break;
                    case OpCode.Try:          Emit(OpCode.Try); break;

                    case OpCode.Break:
                        if (loopStack.Count > 0)
                        {
                            var (_, endPatchIP) = loopStack.Peek();
                            Emit(OpCode.Br, endPatchIP);
                        }
                        break;

                    case OpCode.Continue:
                        if (loopStack.Count > 0)
                        {
                            var (loopStartIP, _) = loopStack.Peek();
                            Emit(OpCode.Br, loopStartIP);
                        }
                        break;

                    default:
                        Emit(simple.OpCode);
                        break;
                }
            }
            else if (instr is CallInstruction callInstr)
            {
                Emit(OpCode.Call, callTargets.Count);
                callTargets.Add(callInstr);
            }
            else if (instr is NewObjInstruction newObjInstr)
            {
                Emit(OpCode.Newobj, newObjTargets.Count);
                newObjTargets.Add(newObjInstr);
            }
        }

        void CompileBlock(IReadOnlyList<Statement> statements)
        {
            int i = 0;
            while (i < statements.Count)
            {
                var stmt = statements[i];

                if (stmt is WhileStatement whileStmt)
                {
                    i = CompileWhile(whileStmt, i, statements);
                }
                else if (stmt is IfStatement ifStmt)
                {
                    i = CompileIf(ifStmt, i, statements);
                }
                else if (stmt is BlockStatement block)
                {
                    CompileBlock(block.Statements);
                    i++;
                }
                else if (stmt is LocalDeclarationStatement)
                {
                    i++;
                }
                else
                {
                    CompileInstructionStmt((InstructionStatement)stmt);
                    i++;
                }
            }
        }

        int FindConditionStart(int stmtIndex, IReadOnlyList<Statement> statements)
        {
            var loc = statements[stmtIndex].Location;
            if (loc == null) return stmtIndex;

            int line = loc.Line;
            int condStart = stmtIndex;
            while (condStart > 0)
            {
                var prev = statements[condStart - 1];
                if (prev is InstructionStatement && prev.Location?.Line == line)
                    condStart--;
                else
                    break;
            }
            return condStart;
        }

        int CompileWhile(WhileStatement whileStmt, int stmtIndex, IReadOnlyList<Statement> statements)
        {
            int condStart = FindConditionStart(stmtIndex, statements);

            int loopStart = code.Count;

            for (int j = condStart; j < stmtIndex; j++)
                CompileInstructionStmt((InstructionStatement)statements[j]);

            int brFalseIdx = Emit(OpCode.Brfalse, 0);
            int endPatchIP = 0; // will be patched later

            loopStack.Push((loopStart, endPatchIP));

            CompileBlock(whileStmt.Body.Statements);

            var popped = loopStack.Pop();
            endPatchIP = code.Count;

            Emit(OpCode.Br, loopStart);
            Patch(brFalseIdx, code.Count);

            // Patch any break instructions that referred to the initial endPatchIP (0)
            for (int i = brFalseIdx + 1; i < code.Count - 1; i++)
            {
                if (code[i].Opcode == OpCode.Br && code[i].Operand == 0)
                {
                    Patch(i, code.Count);
                }
            }

            return stmtIndex + 1;
        }

        int CompileIf(IfStatement ifStmt, int stmtIndex, IReadOnlyList<Statement> statements)
        {
            int condStart = FindConditionStart(stmtIndex, statements);

            for (int j = condStart; j < stmtIndex; j++)
                CompileInstructionStmt((InstructionStatement)statements[j]);

            int brFalseIdx = Emit(OpCode.Brfalse, 0);

            CompileBlock(ifStmt.Then.Statements);

            if (ifStmt.Else != null && ifStmt.Else.Statements.Count > 0)
            {
                int brEndIdx = Emit(OpCode.Br, 0);
                Patch(brFalseIdx, code.Count);
                CompileBlock(ifStmt.Else.Statements);
                Patch(brEndIdx, code.Count);
            }
            else
            {
                Patch(brFalseIdx, code.Count);
            }

            return stmtIndex + 1;
        }

        CompileBlock(method.Body.Statements);

        return new CompiledMethod
        {
            Name = method.Name,
            LocalCount = localNames.Count,
            ArgCount = method.Parameters.Count,
            ReturnsValue = !string.Equals(method.ReturnType.Name, "void", StringComparison.Ordinal),
            SourceMethod = method,
            Code = code.ToArray(),
            StringTable = stringTable.ToArray(),
            FloatTable = floatTable.ToArray(),
            DoubleTable = doubleTable.ToArray(),
            LocalNames = localNames.ToArray(),
            ArgNames = argNames,
            LocalNameToIndex = localNames.Select((_, i) => i).ToArray(),
            CallTargets = callTargets.ToArray(),
            NewObjTargets = newObjTargets.ToArray(),
        };
    }
}
