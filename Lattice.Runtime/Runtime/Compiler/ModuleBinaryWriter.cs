using ObjectIR.Core.AST;
using ObjectIR.Core.Serialization;

namespace lattice.Runtime.Compiler;

/// <summary>
/// Serializes a <see cref="ModuleNode"/> into the binary payload carried inside a
/// FOB/IR v3 container.  The payload uses BSON encoding via <see cref="ModuleSerializer"/>.
/// </summary>
public static class ModuleBinaryWriter
{
    /// <summary>
    /// Serializes <paramref name="module"/> to a binary payload that can be wrapped
    /// in a FOB/IR v3 container via <c>FobIrCompiler.CompileFromPayload</c>.
    /// </summary>
    public static byte[] Write(ModuleNode module)
    {
        return new ModuleSerializer(module).DumpToBson();
    }
}
