using System.Text;
using System.Text.Json;
using lattice.IR;

namespace lattice.Runtime;

/// <summary>
/// Deserialises a <see cref="ModuleDto"/> from the compact binary bytecode payload
/// produced by <see cref="ModuleBinaryWriter.Write"/>.
/// </summary>
internal static class ModuleBinaryReader
{
    private static readonly JsonSerializerOptions s_json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    // ── Entry point ───────────────────────────────────────────────────────────

    /// <summary>Deserialises a binary payload produced by <see cref="ModuleBinaryWriter.Write"/>.</summary>
    public static ModuleDto Read(byte[] payload)
    {
        using var ms = new MemoryStream(payload);
        using var r  = new BinaryReader(ms, Encoding.UTF8, leaveOpen: true);
        var strings  = ReadStringTable(r);
        return ReadModule(r, strings);
    }

    // ── String table ──────────────────────────────────────────────────────────

    private static string[] ReadStringTable(BinaryReader r)
    {
        var count   = r.ReadUInt32();
        var strings = new string[count];
        for (uint i = 0; i < count; i++)
        {
            var len   = r.ReadUInt16();
            var bytes = r.ReadBytes(len);
            strings[i] = Encoding.UTF8.GetString(bytes);
        }
        return strings;
    }

    // ── Module ────────────────────────────────────────────────────────────────

    private static ModuleDto ReadModule(BinaryReader r, string[] st)
    {
        var name      = st[r.ReadUInt16()];
        var version   = st[r.ReadUInt16()];
        var typeCount = r.ReadUInt16();
        var types     = new TypeDto[typeCount];
        for (int i = 0; i < typeCount; i++) types[i] = ReadType(r, st);

        return new ModuleDto
        {
            name      = name,
            version   = version,
            metadata  = JsonSerializer.SerializeToElement(new { }),
            functions = JsonSerializer.SerializeToElement(Array.Empty<object>()),
            types     = types
        };
    }

    // ── Type ──────────────────────────────────────────────────────────────────

    private static TypeDto ReadType(BinaryReader r, string[] st)
    {
        var kind     = st[r.ReadUInt16()];
        var name     = st[r.ReadUInt16()];
        var ns       = Nullable(st[r.ReadUInt16()]);
        var access   = Nullable(st[r.ReadUInt16()]);
        var baseType = Nullable(st[r.ReadUInt16()]);
        var flags    = r.ReadByte();

        var ifaceCount = r.ReadUInt16();
        var ifaces     = new string[ifaceCount];
        for (int i = 0; i < ifaceCount; i++) ifaces[i] = st[r.ReadUInt16()];

        var fieldCount = r.ReadUInt16();
        var fields     = new FieldDto[fieldCount];
        for (int i = 0; i < fieldCount; i++) fields[i] = ReadField(r, st);

        var methodCount = r.ReadUInt16();
        var methods     = new MethodDto[methodCount];
        for (int i = 0; i < methodCount; i++) methods[i] = ReadMethod(r, st);

        return new TypeDto
        {
            kind               = kind,
            name               = name,
            _namespace         = ns,
            access             = access,
            baseType           = baseType,
            isAbstract         = (flags & 1) != 0,
            isSealed           = (flags & 2) != 0,
            interfaces         = ifaces,
            fields             = fields,
            methods            = methods,
            attributes         = Array.Empty<AttributeDto>(),
            baseInterfaces     = JsonSerializer.SerializeToElement(Array.Empty<object>()),
            genericParameters  = JsonSerializer.SerializeToElement(Array.Empty<object>()),
            properties         = JsonSerializer.SerializeToElement(Array.Empty<object>())
        };
    }

    // ── Field ─────────────────────────────────────────────────────────────────

    private static FieldDto ReadField(BinaryReader r, string[] st)
    {
        var name   = st[r.ReadUInt16()];
        var type   = st[r.ReadUInt16()];
        var access = Nullable(st[r.ReadUInt16()]);
        var flags  = r.ReadByte();

        return new FieldDto
        {
            name       = name,
            type       = type,
            access     = access,
            isStatic   = (flags & 1) != 0,
            isReadOnly = (flags & 2) != 0,
            attributes = Array.Empty<AttributeDto>()
        };
    }

    // ── Method ────────────────────────────────────────────────────────────────

    private static MethodDto ReadMethod(BinaryReader r, string[] st)
    {
        var name       = st[r.ReadUInt16()];
        var returnType = st[r.ReadUInt16()];
        var access     = Nullable(st[r.ReadUInt16()]);
        var flags      = r.ReadByte();

        var paramCount = r.ReadUInt16();
        var parameters = new ParameterDto[paramCount];
        for (int i = 0; i < paramCount; i++)
            parameters[i] = new ParameterDto { name = st[r.ReadUInt16()], type = st[r.ReadUInt16()] };

        var localCount = r.ReadUInt16();
        var locals     = new LocalVariableDto[localCount];
        for (int i = 0; i < localCount; i++)
            locals[i] = new LocalVariableDto { name = st[r.ReadUInt16()], type = st[r.ReadUInt16()] };

        var instrCount = r.ReadUInt32();
        var instrs     = new InstructionDto[instrCount];
        for (uint i = 0; i < instrCount; i++) instrs[i] = ReadInstr(r, st);

        return new MethodDto
        {
            name             = name,
            returnType       = returnType,
            access           = access,
            isStatic         = (flags & 0x01) != 0,
            isVirtual        = (flags & 0x02) != 0,
            isOverride       = (flags & 0x04) != 0,
            isAbstract       = (flags & 0x08) != 0,
            isConstructor    = (flags & 0x10) != 0,
            parameters       = parameters,
            localVariables   = locals,
            instructions     = instrs,
            instructionCount = (int)instrCount,
            attributes       = Array.Empty<AttributeDto>()
        };
    }

    // ── Instruction block ─────────────────────────────────────────────────────

    private static InstructionDto[] ReadInstrBlock(BinaryReader r, string[] st)
    {
        var count  = r.ReadUInt32();
        var instrs = new InstructionDto[count];
        for (uint i = 0; i < count; i++) instrs[i] = ReadInstr(r, st);
        return instrs;
    }

    // ── Instruction ───────────────────────────────────────────────────────────

    private static InstructionDto ReadInstr(BinaryReader r, string[] st)
    {
        var op = r.ReadByte();

        string     opCode;
        JsonElement operand;

        switch (op)
        {
            // ── Zero-operand ──────────────────────────────────────────────────
            case BinOp.Nop:       opCode = "nop";      operand = Empty(); break;
            case BinOp.Dup:       opCode = "dup";      operand = Empty(); break;
            case BinOp.Pop:       opCode = "pop";      operand = Empty(); break;
            case BinOp.Ldnull:    opCode = "ldnull";   operand = Empty(); break;
            case BinOp.Add:       opCode = "add";      operand = Empty(); break;
            case BinOp.Sub:       opCode = "sub";      operand = Empty(); break;
            case BinOp.Mul:       opCode = "mul";      operand = Empty(); break;
            case BinOp.Div:       opCode = "div";      operand = Empty(); break;
            case BinOp.Rem:       opCode = "rem";      operand = Empty(); break;
            case BinOp.Neg:       opCode = "neg";      operand = Empty(); break;
            case BinOp.Not:       opCode = "not";      operand = Empty(); break;
            case BinOp.Ceq:       opCode = "ceq";      operand = Empty(); break;
            case BinOp.Cne:       opCode = "cne";      operand = Empty(); break;
            case BinOp.Cgt:       opCode = "cgt";      operand = Empty(); break;
            case BinOp.Cge:       opCode = "cge";      operand = Empty(); break;
            case BinOp.Clt:       opCode = "clt";      operand = Empty(); break;
            case BinOp.Cle:       opCode = "cle";      operand = Empty(); break;
            case BinOp.Ldelem:    opCode = "ldelem";   operand = Empty(); break;
            case BinOp.Stelem:    opCode = "stelem";   operand = Empty(); break;
            case BinOp.Ret:       opCode = "ret";      operand = Empty(); break;
            case BinOp.Throw:     opCode = "throw";    operand = Empty(); break;
            case BinOp.Break_:    opCode = "break";    operand = Empty(); break;
            case BinOp.Continue_: opCode = "continue"; operand = Empty(); break;

            // ── ldc ───────────────────────────────────────────────────────────
            case BinOp.Ldc:
            {
                var value = st[r.ReadUInt16()];
                var type  = st[r.ReadUInt16()];
                opCode  = "ldc";
                operand = JsonSerializer.SerializeToElement(new { value, type });
                break;
            }

            // ── ldstr ─────────────────────────────────────────────────────────
            case BinOp.Ldstr:
            {
                var value = st[r.ReadUInt16()];
                opCode  = "ldstr";
                operand = JsonSerializer.SerializeToElement(new { value });
                break;
            }

            // ── ldarg ─────────────────────────────────────────────────────────
            case BinOp.Ldarg:
            {
                var argumentName = st[r.ReadUInt16()];
                opCode  = "ldarg";
                operand = JsonSerializer.SerializeToElement(new { argumentName });
                break;
            }

            // ── starg ─────────────────────────────────────────────────────────
            case BinOp.Starg:
            {
                var argumentName = st[r.ReadUInt16()];
                opCode  = "starg";
                operand = JsonSerializer.SerializeToElement(new { argumentName });
                break;
            }

            // ── ldloc ─────────────────────────────────────────────────────────
            case BinOp.Ldloc:
            {
                var localName = st[r.ReadUInt16()];
                opCode  = "ldloc";
                operand = JsonSerializer.SerializeToElement(new { localName });
                break;
            }

            // ── stloc ─────────────────────────────────────────────────────────
            case BinOp.Stloc:
            {
                var localName = st[r.ReadUInt16()];
                opCode  = "stloc";
                operand = JsonSerializer.SerializeToElement(new { localName });
                break;
            }

            // ── newobj ────────────────────────────────────────────────────────
            case BinOp.Newobj:
            {
                var type = st[r.ReadUInt16()];
                opCode  = "newobj";
                operand = JsonSerializer.SerializeToElement(new { type });
                break;
            }

            // ── newarr ────────────────────────────────────────────────────────
            case BinOp.Newarr:
            {
                var elementType = st[r.ReadUInt16()];
                opCode  = "newarr";
                operand = JsonSerializer.SerializeToElement(new { elementType });
                break;
            }

            // ── ldfld ─────────────────────────────────────────────────────────
            case BinOp.Ldfld:
            {
                // Reconstruct as { field: "name" } — the form the runtime handles
                var field = st[r.ReadUInt16()];
                opCode  = "ldfld";
                operand = JsonSerializer.SerializeToElement(new { field });
                break;
            }

            // ── stfld ─────────────────────────────────────────────────────────
            case BinOp.Stfld:
            {
                var field = st[r.ReadUInt16()];
                opCode  = "stfld";
                operand = JsonSerializer.SerializeToElement(new { field });
                break;
            }

            // ── ldsfld ────────────────────────────────────────────────────────
            case BinOp.Ldsfld:
            {
                var declaringType = st[r.ReadUInt16()];
                var name          = st[r.ReadUInt16()];
                opCode  = "ldsfld";
                operand = JsonSerializer.SerializeToElement(
                    new { field = new { declaringType, name } });
                break;
            }

            // ── stsfld ────────────────────────────────────────────────────────
            case BinOp.Stsfld:
            {
                var declaringType = st[r.ReadUInt16()];
                var name          = st[r.ReadUInt16()];
                opCode  = "stsfld";
                operand = JsonSerializer.SerializeToElement(
                    new { field = new { declaringType, name } });
                break;
            }

            // ── conv ──────────────────────────────────────────────────────────
            case BinOp.Conv:
            {
                var targetType = st[r.ReadUInt16()];
                opCode  = "conv";
                operand = JsonSerializer.SerializeToElement(new { targetType });
                break;
            }

            // ── castclass ─────────────────────────────────────────────────────
            case BinOp.Castclass:
            {
                var targetType = st[r.ReadUInt16()];
                opCode  = "castclass";
                operand = JsonSerializer.SerializeToElement(new { targetType });
                break;
            }

            // ── isinst ────────────────────────────────────────────────────────
            case BinOp.Isinst:
            {
                var targetType = st[r.ReadUInt16()];
                opCode  = "isinst";
                operand = JsonSerializer.SerializeToElement(new { targetType });
                break;
            }

            // ── call / callvirt ───────────────────────────────────────────────
            case BinOp.Call:
            case BinOp.Callvirt:
            {
                opCode  = op == BinOp.Call ? "call" : "callvirt";
                operand = ReadCallTargetElement(r, st);
                break;
            }

            // ── if ────────────────────────────────────────────────────────────
            case BinOp.If:
            {
                opCode = "if";
                var condition = ReadConditionDto(r, st);
                var thenBlock = ReadInstrBlock(r, st);
                var hasElse   = r.ReadByte() != 0;
                if (hasElse)
                {
                    var elseBlock = ReadInstrBlock(r, st);
                    operand = JsonSerializer.SerializeToElement(
                        new { condition, thenBlock, elseBlock }, s_json);
                }
                else
                {
                    operand = JsonSerializer.SerializeToElement(
                        new { condition, thenBlock }, s_json);
                }
                break;
            }

            // ── while ─────────────────────────────────────────────────────────
            case BinOp.While:
            {
                opCode = "while";
                var condition = ReadConditionDto(r, st);
                var body      = ReadInstrBlock(r, st);
                operand = JsonSerializer.SerializeToElement(new { condition, body }, s_json);
                break;
            }

            // ── try ───────────────────────────────────────────────────────────
            case BinOp.Try:
            {
                opCode = "try";
                var tryBlock   = ReadInstrBlock(r, st);
                var catchCount = r.ReadUInt16();

                // Build catch block array as anonymous objects so they serialise correctly
                var catchBlocks = new object[catchCount];
                for (int i = 0; i < catchCount; i++)
                {
                    var exType = Nullable(st[r.ReadUInt16()]);
                    var block  = ReadInstrBlock(r, st);
                    catchBlocks[i] = exType != null
                        ? (object)new { exceptionType = exType, block }
                        : new { block };
                }

                var hasFinally = r.ReadByte() != 0;
                if (hasFinally)
                {
                    var finallyBlock = ReadInstrBlock(r, st);
                    operand = JsonSerializer.SerializeToElement(
                        new { tryBlock, catchBlocks, finallyBlock }, s_json);
                }
                else
                {
                    operand = JsonSerializer.SerializeToElement(
                        new { tryBlock, catchBlocks }, s_json);
                }
                break;
            }

            // ── raw JSON escape ───────────────────────────────────────────────
            case BinOp.RawJson:
            {
                var len  = r.ReadUInt32();
                var raw  = r.ReadBytes((int)len);
                var json = Encoding.UTF8.GetString(raw);
                return JsonSerializer.Deserialize<InstructionDto>(json)
                       ?? throw new InvalidDataException("Invalid raw-JSON instruction bytes.");
            }

            default:
                throw new InvalidDataException($"Unknown binary opcode 0x{op:X2}.");
        }

        return new InstructionDto { opCode = opCode, operand = operand };
    }

    // ── Call target ───────────────────────────────────────────────────────────

    private static JsonElement ReadCallTargetElement(BinaryReader r, string[] st)
    {
        var declaringType   = st[r.ReadUInt16()];
        var name            = st[r.ReadUInt16()];
        var returnType      = st[r.ReadUInt16()];
        var paramCount      = r.ReadUInt16();
        var parameterTypes  = new string[paramCount];
        for (int i = 0; i < paramCount; i++) parameterTypes[i] = st[r.ReadUInt16()];

        // Reconstruct operand in the exact shape the runtime's ExecuteCall expects:
        // { method: { declaringType, name, returnType, parameterTypes } }
        return JsonSerializer.SerializeToElement(
            new { method = new { declaringType, name, returnType, parameterTypes } });
    }

    // ── Condition ─────────────────────────────────────────────────────────────

    private static ConditionDto ReadConditionDto(BinaryReader r, string[] st)
    {
        var kind = r.ReadByte();
        switch (kind)
        {
            case BinOp.CondBinary:
            {
                var operation = st[r.ReadUInt16()];
                return new ConditionDto { kind = "binary", operation = operation };
            }

            case BinOp.CondExpression:
            {
                var expr = ReadInstr(r, st);
                return new ConditionDto { kind = "expression", expression = expr };
            }

            case BinOp.CondBlock:
            {
                var block = ReadInstrBlock(r, st);
                return new ConditionDto { kind = "block", block = block };
            }

            default: // CondStack
                return new ConditionDto { kind = "stack" };
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static JsonElement Empty() =>
        JsonSerializer.SerializeToElement(new { });

    private static string? Nullable(string s) =>
        string.IsNullOrEmpty(s) ? null : s;
}
