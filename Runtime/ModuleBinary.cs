namespace lattice.Runtime;

/// <summary>
/// Opcode byte values for the FOB/IR v3 binary payload format.
/// Each instruction in a method body is encoded as one of these bytes
/// followed by a fixed operand encoding determined by the opcode.
/// </summary>
internal static class BinOp
{
    // ── Zero-operand ──────────────────────────────────────────────────────────
    public const byte Nop       = 0x00;
    public const byte Dup       = 0x01;
    public const byte Pop       = 0x02;
    public const byte Ldnull    = 0x03;

    // ── Arithmetic / bitwise ──────────────────────────────────────────────────
    public const byte Add       = 0x0A;
    public const byte Sub       = 0x0B;
    public const byte Mul       = 0x0C;
    public const byte Div       = 0x0D;
    public const byte Rem       = 0x0E;
    public const byte Neg       = 0x0F;
    public const byte Not       = 0x10;

    // ── Comparison ────────────────────────────────────────────────────────────
    public const byte Ceq       = 0x11;
    public const byte Cne       = 0x12;
    public const byte Cgt       = 0x13;
    public const byte Cge       = 0x14;
    public const byte Clt       = 0x15;
    public const byte Cle       = 0x16;

    // ── Array ─────────────────────────────────────────────────────────────────
    public const byte Ldelem    = 0x17;
    public const byte Stelem    = 0x18;

    // ── Control (zero-operand) ────────────────────────────────────────────────
    public const byte Ret       = 0x19;
    public const byte Throw     = 0x1A;
    public const byte Break_    = 0x1B;   // 'Break' is a C# keyword
    public const byte Continue_ = 0x1C;   // 'Continue' is a C# keyword

    // ── One string-index operand  [uint16 si] ─────────────────────────────────
    public const byte Ldstr     = 0x20;   // [si value]
    public const byte Ldarg     = 0x21;   // [si argumentName]  ("this" is representable)
    public const byte Starg     = 0x22;   // [si argumentName]
    public const byte Ldloc     = 0x23;   // [si localName]
    public const byte Stloc     = 0x24;   // [si localName]
    public const byte Newobj    = 0x25;   // [si type]
    public const byte Newarr    = 0x26;   // [si elementType]
    public const byte Ldfld     = 0x27;   // [si fieldName]   — from AstLowering: operand.field is a string
    public const byte Stfld     = 0x28;   // [si fieldName]
    public const byte Conv      = 0x29;   // [si targetType]

    // ── Two string-index operands  [uint16 si][uint16 si] ────────────────────
    public const byte Ldc       = 0x30;   // [si value][si type]
    public const byte Ldsfld    = 0x31;   // [si declaringType][si fieldName]
    public const byte Stsfld    = 0x32;   // [si declaringType][si fieldName]
    public const byte Castclass = 0x33;   // [si targetType]  (also single-si, but in this group)
    public const byte Isinst    = 0x34;   // [si targetType]

    // ── Call target  [si declType][si name][si retType][uint16 pCount][si…] ──
    public const byte Call      = 0x40;
    public const byte Callvirt  = 0x41;

    // ── Nested block instructions ─────────────────────────────────────────────
    public const byte If        = 0x50;   // Condition InstrBlock [byte hasElse InstrBlock]
    public const byte While     = 0x51;   // Condition InstrBlock
    public const byte Try       = 0x52;   // InstrBlock uint16-catchCount [si exType InstrBlock]… byte-hasFinally [InstrBlock]

    // ── Condition kind bytes (used inside Condition encoding) ─────────────────
    public const byte CondStack      = 0x00;  // value already on eval stack
    public const byte CondBinary     = 0x01;  // [si operation]
    public const byte CondExpression = 0x02;  // single Instruction
    public const byte CondBlock      = 0x03;  // InstrBlock

    // ── Escape hatch ─────────────────────────────────────────────────────────
    /// <summary>
    /// Raw JSON escape: the next 4 bytes are a uint32 length, followed by that
    /// many bytes of UTF-8–encoded JSON representing the full <c>InstructionDto</c>.
    /// Used for opcodes whose operand shape is unknown or non-standard.
    /// </summary>
    public const byte RawJson   = 0xFF;
}
