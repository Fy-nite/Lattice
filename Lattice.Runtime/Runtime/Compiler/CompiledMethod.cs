using ObjectIR.Core.AST;
using ObjectIR.Core.Ast;

namespace lattice.Runtime.Compiler;

public sealed class CompiledMethod
{
    public string Name { get; init; }
    public int LocalCount { get; init; }
    public int ArgCount { get; init; }
    public bool ReturnsValue { get; init; }
    public MethodNode SourceMethod { get; init; }

    public CompactInstr[] Code { get; set; } = [];
    public string[] StringTable { get; set; } = [];
    public float[] FloatTable { get; set; } = [];

    public int[] LocalNameToIndex { get; set; } = [];
    public string[] LocalNames { get; set; } = [];
    public string[] ArgNames { get; set; } = [];

    // Indexed by the Call instruction's position in Code[].
    // Each Call instruction stores its index into this array as the operand.
    public CallInstruction?[] CallTargets { get; set; } = [];
    public NewObjInstruction?[] NewObjTargets { get; set; } = [];

    // Pre-resolved MethodNode references (populated by ResolveTargets).
    // Avoids string-based ResolveMethod lookup on every Call/Newobj at runtime.
    public MethodNode?[] ResolvedCallTargets { get; set; } = [];
    public MethodNode?[] ResolvedCtorTargets { get; set; } = [];

    // Pre-split field names: "BenchObj.X" -> "X" at compile time.
    // Indexed the same way as StringTable — uses the same operand index.
    public string[] FieldNames { get; set; } = [];

    /// <summary>
    /// Pre-resolve all Call and Newobj targets to MethodNode references,
    /// eliminating the per-call string-based ResolveMethod scan.
    /// Also pre-split field names (strip "ClassName." prefix).
    /// </summary>
    public void ResolveTargets(CPU cpu)
    {
        ResolvedCallTargets = new MethodNode?[CallTargets.Length];
        for (int i = 0; i < CallTargets.Length; i++)
        {
            var callInstr = CallTargets[i];
            if (callInstr?.Target != null)
                ResolvedCallTargets[i] = cpu.ResolveMethod(callInstr.Target);
        }

        ResolvedCtorTargets = new MethodNode?[NewObjTargets.Length];
        for (int i = 0; i < NewObjTargets.Length; i++)
        {
            var newObj = NewObjTargets[i];
            if (newObj?.Constructor != null)
                ResolvedCtorTargets[i] = cpu.ResolveMethod(newObj.Constructor);
        }

        FieldNames = new string[StringTable.Length];
        for (int i = 0; i < StringTable.Length; i++)
        {
            var name = StringTable[i];
            int dot = name.IndexOf('.');
            FieldNames[i] = dot >= 0 ? name[(dot + 1)..] : name;
        }
    }
}

public struct CompactInstr
{
    public OpCode Opcode;
    public int Operand;

    public CompactInstr(OpCode opcode, int operand = 0)
    {
        Opcode = opcode;
        Operand = operand;
    }
}
