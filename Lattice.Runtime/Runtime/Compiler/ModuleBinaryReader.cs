using ObjectIR.Core.AST;
using ObjectIR.Core.Serialization;

namespace lattice.Runtime.Compiler;

/// <summary>
/// Deserializes a <see cref="ModuleNode"/> from a FOB/IR v3 binary payload that was
/// produced by <see cref="ModuleBinaryWriter.Write"/>.
/// </summary>
public static class ModuleBinaryReader
{
    /// <summary>
    /// Reads a <see cref="ModuleNode"/> from a FOB/IR v3 payload byte array
    /// (the raw payload extracted from a <c>FobIrBinary</c> container).
    /// </summary>
    public static ModuleNode Read(byte[] payload)
    {
        return ModuleSerializer.LoadFromBson(payload);
    }
}
