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
