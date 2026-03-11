using System.Text;
using System.Text.Json;
using lattice.IR;

namespace lattice.Runtime;

/// <summary>
/// Serialises a <see cref="ModuleDto"/> into the compact binary bytecode payload
/// used by the FOB/IR v3 format.
/// </summary>
/// <remarks>
/// <para><b>Payload layout:</b></para>
/// <code>
/// [StringTable]
///   uint32   count          (number of interned strings, including slot-0 = "")
///   for each string:
///     uint16 byteLen        (UTF-8 byte length)
///     bytes  utf8           (no null terminator)
///
/// [Module]
///   uint16   nameIdx
///   uint16   versionIdx
///   uint16   typeCount
///   TypeBinary × typeCount
///
/// [TypeBinary]
///   uint16 kindIdx  uint16 nameIdx  uint16 nsIdx  uint16 accessIdx  uint16 baseTypeIdx
///   byte   flags    (bit 0 = isAbstract, bit 1 = isSealed)
///   uint16 ifaceCount  uint16[] ifaceIdx
///   uint16 fieldCount  FieldBinary[]
///   uint16 methodCount MethodBinary[]
///
/// [FieldBinary]
///   uint16 nameIdx  uint16 typeIdx  uint16 accessIdx  byte flags (bit0=isStatic, bit1=isReadOnly)
///
/// [MethodBinary]
///   uint16 nameIdx  uint16 retTypeIdx  uint16 accessIdx  byte flags
///   uint16 paramCount  (uint16 nameIdx + uint16 typeIdx) × paramCount
///   uint16 localCount  (uint16 nameIdx + uint16 typeIdx) × localCount
///   uint32 instrCount  InstrBinary × instrCount
///
/// [InstrBinary]
///   byte opcode  [operand — varies by opcode; see BinOp constants]
///
/// [InstrBlock]
///   uint32 count  InstrBinary × count
///
/// [Condition]
///   byte kind (0=stack, 1=binary, 2=expression, 3=block)
///   binary:     uint16 operationIdx
///   expression: InstrBinary
///   block:      InstrBlock
///
/// [CallTarget]
///   uint16 declTypeIdx  uint16 nameIdx  uint16 retTypeIdx
///   uint16 paramCount   uint16[] paramTypeIdx
/// </code>
/// </remarks>
internal static class ModuleBinaryWriter
{
    private static readonly JsonSerializerOptions s_json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    // ── Entry point ───────────────────────────────────────────────────────────

    /// <summary>Serialises <paramref name="module"/> to a compact binary payload.</summary>
    public static byte[] Write(ModuleDto module)
    {
        var st = new StringTableBuilder();
        CollectModuleStrings(st, module);      // pass 1 — intern all strings

        using var ms = new MemoryStream();
        using var w  = new BinaryWriter(ms, Encoding.UTF8, leaveOpen: true);

        st.WriteTo(w);                         // string table first
        WriteModule(w, st, module);            // then module body

        w.Flush();
        return ms.ToArray();
    }

    // ── String-collection pass ────────────────────────────────────────────────

    private static void CollectModuleStrings(StringTableBuilder st, ModuleDto m)
    {
        st.Intern(m.name);
        st.Intern(m.version);
        foreach (var t in m.types) CollectTypeStrings(st, t);
    }

    private static void CollectTypeStrings(StringTableBuilder st, TypeDto t)
    {
        st.Intern(t.kind); st.Intern(t.name); st.Intern(t._namespace);
        st.Intern(t.access); st.Intern(t.baseType);
        foreach (var i in t.interfaces) st.Intern(i);
        foreach (var f in t.fields)
        {
            st.Intern(f.name); st.Intern(f.type); st.Intern(f.access);
        }
        foreach (var m in t.methods) CollectMethodStrings(st, m);
    }

    private static void CollectMethodStrings(StringTableBuilder st, MethodDto m)
    {
        st.Intern(m.name); st.Intern(m.returnType); st.Intern(m.access);
        foreach (var p in m.parameters) { st.Intern(p.name); st.Intern(p.type); }
        foreach (var l in m.localVariables) { st.Intern(l.name); st.Intern(l.type); }
        foreach (var i in m.instructions) CollectInstrStrings(st, i);
    }

    private static void CollectInstrStrings(StringTableBuilder st, InstructionDto instr)
    {
        var e  = instr.operand;
        switch (instr.opCode)
        {
            case "ldc":
                st.Intern(GetStr(e, "value")); st.Intern(GetStr(e, "type")); break;
            case "ldstr":
                st.Intern(GetStr(e, "value")); break;
            case "ldarg":
                st.Intern(GetStr(e, "argumentName")); break;
            case "starg":
                st.Intern(GetStr(e, "argumentName")); break;
            case "ldloc": case "stloc":
                st.Intern(GetStr(e, "localName")); break;
            case "newobj":
                st.Intern(GetStr(e, "type")); break;
            case "newarr":
                st.Intern(GetStr(e, "elementType")); break;
            case "ldfld": case "stfld":
                st.Intern(GetFieldNameStr(e)); break;
            case "ldsfld": case "stsfld":
                CollectStaticFieldStrings(st, e); break;
            case "conv":
                st.Intern(GetStr(e, "targetType") ?? GetArg0(e)); break;
            case "castclass": case "isinst":
                st.Intern(GetStr(e, "targetType") ?? GetArg0(e)); break;
            case "call": case "callvirt":
                CollectCallStrings(st, e); break;
            case "if":
                CollectConditionStrings(st, e);
                CollectBlockStrings(st, e, "thenBlock");
                CollectBlockStrings(st, e, "elseBlock"); break;
            case "while":
                CollectConditionStrings(st, e);
                CollectBlockStrings(st, e, "body"); break;
            case "try":
                CollectBlockStrings(st, e, "tryBlock");
                CollectCatchBlockStrings(st, e);
                CollectBlockStrings(st, e, "finallyBlock"); break;
            // Zero-operand: nothing to collect
        }
    }

    private static void CollectStaticFieldStrings(StringTableBuilder st, JsonElement e)
    {
        if (e.ValueKind == JsonValueKind.Object
            && e.TryGetProperty("field", out var f)
            && f.ValueKind == JsonValueKind.Object)
        {
            st.Intern(f.TryGetProperty("declaringType", out var dt) ? dt.GetString() : null);
            st.Intern(f.TryGetProperty("name", out var n) ? n.GetString() : null);
        }
    }

    private static void CollectCallStrings(StringTableBuilder st, JsonElement e)
    {
        var method = e.ValueKind == JsonValueKind.Object
                     && e.TryGetProperty("method", out var m) ? m : e;
        if (method.ValueKind != JsonValueKind.Object) return;
        st.Intern(GetStr(method, "declaringType"));
        st.Intern(GetStr(method, "name"));
        st.Intern(GetStr(method, "returnType"));
        if (method.TryGetProperty("parameterTypes", out var pts)
            && pts.ValueKind == JsonValueKind.Array)
        {
            foreach (var pt in pts.EnumerateArray()) st.Intern(pt.GetString());
        }
    }

    private static void CollectConditionStrings(StringTableBuilder st, JsonElement e)
    {
        if (e.ValueKind != JsonValueKind.Object
            || !e.TryGetProperty("condition", out var c)) return;

        if (c.TryGetProperty("operation", out var op)) st.Intern(op.GetString());

        CollectBlockStrings(st, c, "block");

        if (c.TryGetProperty("expression", out var expr)
            && expr.ValueKind == JsonValueKind.Object)
        {
            var instrDto = JsonSerializer.Deserialize<InstructionDto>(expr.GetRawText());
            if (instrDto != null) CollectInstrStrings(st, instrDto);
        }
    }

    private static void CollectBlockStrings(StringTableBuilder st, JsonElement e, string name)
    {
        if (e.ValueKind != JsonValueKind.Object
            || !e.TryGetProperty(name, out var arr)
            || arr.ValueKind != JsonValueKind.Array) return;

        var instrs = JsonSerializer.Deserialize<InstructionDto[]>(arr.GetRawText());
        if (instrs == null) return;
        foreach (var i in instrs) CollectInstrStrings(st, i);
    }

    private static void CollectCatchBlockStrings(StringTableBuilder st, JsonElement e)
    {
        if (e.ValueKind != JsonValueKind.Object
            || !e.TryGetProperty("catchBlocks", out var arr)
            || arr.ValueKind != JsonValueKind.Array) return;

        foreach (var cb in arr.EnumerateArray())
        {
            if (cb.TryGetProperty("exceptionType", out var et))
                st.Intern(et.GetString());
            CollectBlockStrings(st, cb, "block");
        }
    }

    // ── Binary-writing pass ───────────────────────────────────────────────────

    private static void WriteModule(BinaryWriter w, StringTableBuilder st, ModuleDto m)
    {
        w.Write(st.Intern(m.name));
        w.Write(st.Intern(m.version));
        w.Write((ushort)m.types.Length);
        foreach (var t in m.types) WriteType(w, st, t);
    }

    private static void WriteType(BinaryWriter w, StringTableBuilder st, TypeDto t)
    {
        w.Write(st.Intern(t.kind));
        w.Write(st.Intern(t.name));
        w.Write(st.Intern(t._namespace));
        w.Write(st.Intern(t.access));
        w.Write(st.Intern(t.baseType));

        byte flags = 0;
        if (t.isAbstract) flags |= 1;
        if (t.isSealed)   flags |= 2;
        w.Write(flags);

        w.Write((ushort)t.interfaces.Length);
        foreach (var i in t.interfaces) w.Write(st.Intern(i));

        w.Write((ushort)t.fields.Length);
        foreach (var f in t.fields) WriteField(w, st, f);

        w.Write((ushort)t.methods.Length);
        foreach (var m in t.methods) WriteMethod(w, st, m);
    }

    private static void WriteField(BinaryWriter w, StringTableBuilder st, FieldDto f)
    {
        w.Write(st.Intern(f.name));
        w.Write(st.Intern(f.type));
        w.Write(st.Intern(f.access));
        byte flags = 0;
        if (f.isStatic)   flags |= 1;
        if (f.isReadOnly) flags |= 2;
        w.Write(flags);
    }

    private static void WriteMethod(BinaryWriter w, StringTableBuilder st, MethodDto m)
    {
        w.Write(st.Intern(m.name));
        w.Write(st.Intern(m.returnType));
        w.Write(st.Intern(m.access));

        byte flags = 0;
        if (m.isStatic)      flags |= 0x01;
        if (m.isVirtual)     flags |= 0x02;
        if (m.isOverride)    flags |= 0x04;
        if (m.isAbstract)    flags |= 0x08;
        if (m.isConstructor) flags |= 0x10;
        w.Write(flags);

        w.Write((ushort)m.parameters.Length);
        foreach (var p in m.parameters)
        {
            w.Write(st.Intern(p.name));
            w.Write(st.Intern(p.type));
        }

        w.Write((ushort)m.localVariables.Length);
        foreach (var l in m.localVariables)
        {
            w.Write(st.Intern(l.name));
            w.Write(st.Intern(l.type));
        }

        w.Write((uint)m.instructions.Length);
        foreach (var i in m.instructions) WriteInstr(w, st, i);
    }

    private static void WriteInstrBlock(BinaryWriter w, StringTableBuilder st, InstructionDto[] instrs)
    {
        w.Write((uint)instrs.Length);
        foreach (var i in instrs) WriteInstr(w, st, i);
    }

    private static void WriteInstr(BinaryWriter w, StringTableBuilder st, InstructionDto instr)
    {
        var e = instr.operand;
        switch (instr.opCode)
        {
            // ── Zero-operand ──────────────────────────────────────────────────
            case "nop":      w.Write(BinOp.Nop);      return;
            case "dup":      w.Write(BinOp.Dup);      return;
            case "pop":      w.Write(BinOp.Pop);      return;
            case "ldnull":   w.Write(BinOp.Ldnull);   return;
            case "add":      w.Write(BinOp.Add);      return;
            case "sub":      w.Write(BinOp.Sub);      return;
            case "mul":      w.Write(BinOp.Mul);      return;
            case "div":      w.Write(BinOp.Div);      return;
            case "rem":      w.Write(BinOp.Rem);      return;
            case "neg":      w.Write(BinOp.Neg);      return;
            case "not":      w.Write(BinOp.Not);      return;
            case "ceq":      w.Write(BinOp.Ceq);      return;
            case "cne":      w.Write(BinOp.Cne);      return;
            case "cgt":      w.Write(BinOp.Cgt);      return;
            case "cge":      w.Write(BinOp.Cge);      return;
            case "clt":      w.Write(BinOp.Clt);      return;
            case "cle":      w.Write(BinOp.Cle);      return;
            case "ldelem":   w.Write(BinOp.Ldelem);   return;
            case "stelem":   w.Write(BinOp.Stelem);   return;
            case "ret":      w.Write(BinOp.Ret);      return;
            case "throw":    w.Write(BinOp.Throw);    return;
            case "break":    w.Write(BinOp.Break_);   return;
            case "continue": w.Write(BinOp.Continue_); return;

            // ── ldc ───────────────────────────────────────────────────────────
            case "ldc":
                w.Write(BinOp.Ldc);
                w.Write(st.Intern(GetStr(e, "value")));
                w.Write(st.Intern(GetStr(e, "type")));
                return;

            // ── ldstr ─────────────────────────────────────────────────────────
            case "ldstr":
                w.Write(BinOp.Ldstr);
                w.Write(st.Intern(GetStr(e, "value")));
                return;

            // ── ldarg / starg ─────────────────────────────────────────────────
            case "ldarg":
                w.Write(BinOp.Ldarg);
                w.Write(st.Intern(GetStr(e, "argumentName")));
                return;

            case "starg":
                w.Write(BinOp.Starg);
                w.Write(st.Intern(GetStr(e, "argumentName")));
                return;

            // ── ldloc / stloc ─────────────────────────────────────────────────
            case "ldloc":
                w.Write(BinOp.Ldloc);
                w.Write(st.Intern(GetStr(e, "localName")));
                return;

            case "stloc":
                w.Write(BinOp.Stloc);
                w.Write(st.Intern(GetStr(e, "localName")));
                return;

            // ── newobj / newarr ───────────────────────────────────────────────
            case "newobj":
                w.Write(BinOp.Newobj);
                w.Write(st.Intern(GetStr(e, "type")));
                return;

            case "newarr":
                w.Write(BinOp.Newarr);
                w.Write(st.Intern(GetStr(e, "elementType")));
                return;

            // ── ldfld / stfld ─────────────────────────────────────────────────
            case "ldfld":
                w.Write(BinOp.Ldfld);
                w.Write(st.Intern(GetFieldNameStr(e)));
                return;

            case "stfld":
                w.Write(BinOp.Stfld);
                w.Write(st.Intern(GetFieldNameStr(e)));
                return;

            // ── ldsfld / stsfld ───────────────────────────────────────────────
            case "ldsfld":
                w.Write(BinOp.Ldsfld);
                WriteStaticField(w, st, e);
                return;

            case "stsfld":
                w.Write(BinOp.Stsfld);
                WriteStaticField(w, st, e);
                return;

            // ── conv / castclass / isinst ─────────────────────────────────────
            case "conv":
                w.Write(BinOp.Conv);
                w.Write(st.Intern(GetStr(e, "targetType") ?? GetArg0(e)));
                return;

            case "castclass":
                w.Write(BinOp.Castclass);
                w.Write(st.Intern(GetStr(e, "targetType") ?? GetArg0(e)));
                return;

            case "isinst":
                w.Write(BinOp.Isinst);
                w.Write(st.Intern(GetStr(e, "targetType") ?? GetArg0(e)));
                return;

            // ── call / callvirt ───────────────────────────────────────────────
            case "call":
                w.Write(BinOp.Call);
                WriteCallTarget(w, st, e);
                return;

            case "callvirt":
                w.Write(BinOp.Callvirt);
                WriteCallTarget(w, st, e);
                return;

            // ── if ────────────────────────────────────────────────────────────
            case "if":
            {
                w.Write(BinOp.If);
                WriteCondition(w, st, e);
                WriteInstrBlock(w, st, GetInstrArray(e, "thenBlock"));
                var elseBlock = GetInstrArray(e, "elseBlock");
                w.Write((byte)(elseBlock.Length > 0 ? 1 : 0));
                if (elseBlock.Length > 0) WriteInstrBlock(w, st, elseBlock);
                return;
            }

            // ── while ─────────────────────────────────────────────────────────
            case "while":
                w.Write(BinOp.While);
                WriteCondition(w, st, e);
                WriteInstrBlock(w, st, GetInstrArray(e, "body"));
                return;

            // ── try ───────────────────────────────────────────────────────────
            case "try":
            {
                w.Write(BinOp.Try);
                WriteInstrBlock(w, st, GetInstrArray(e, "tryBlock"));

                var catchData = GetCatchBlocksData(e);
                w.Write((ushort)catchData.Count);
                foreach (var (exType, block) in catchData)
                {
                    w.Write(st.Intern(exType));
                    WriteInstrBlock(w, st, block);
                }

                var finallyBlock = GetInstrArray(e, "finallyBlock");
                w.Write((byte)(finallyBlock.Length > 0 ? 1 : 0));
                if (finallyBlock.Length > 0) WriteInstrBlock(w, st, finallyBlock);
                return;
            }

            // ── escape hatch ──────────────────────────────────────────────────
            default:
            {
                w.Write(BinOp.RawJson);
                var raw = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(instr, s_json));
                w.Write((uint)raw.Length);
                w.Write(raw);
                return;
            }
        }
    }

    private static void WriteStaticField(BinaryWriter w, StringTableBuilder st, JsonElement e)
    {
        string declType = "", name = "";
        if (e.ValueKind == JsonValueKind.Object
            && e.TryGetProperty("field", out var f)
            && f.ValueKind == JsonValueKind.Object)
        {
            declType = f.TryGetProperty("declaringType", out var dt) ? dt.GetString() ?? "" : "";
            name     = f.TryGetProperty("name",          out var n)  ? n.GetString()  ?? "" : "";
        }
        w.Write(st.Intern(declType));
        w.Write(st.Intern(name));
    }

    private static void WriteCallTarget(BinaryWriter w, StringTableBuilder st, JsonElement e)
    {
        // AstLowering wraps the target in a "method" property
        var method = e.ValueKind == JsonValueKind.Object
                     && e.TryGetProperty("method", out var m) ? m : e;

        w.Write(st.Intern(GetStr(method, "declaringType")));
        w.Write(st.Intern(GetStr(method, "name")));
        w.Write(st.Intern(GetStr(method, "returnType")));

        string[] paramTypes = [];
        if (method.ValueKind == JsonValueKind.Object
            && method.TryGetProperty("parameterTypes", out var pts)
            && pts.ValueKind == JsonValueKind.Array)
        {
            paramTypes = pts.EnumerateArray()
                            .Select(p => p.GetString() ?? "")
                            .ToArray();
        }
        w.Write((ushort)paramTypes.Length);
        foreach (var pt in paramTypes) w.Write(st.Intern(pt));
    }

    private static void WriteCondition(BinaryWriter w, StringTableBuilder st, JsonElement e)
    {
        if (e.ValueKind != JsonValueKind.Object
            || !e.TryGetProperty("condition", out var c))
        {
            w.Write(BinOp.CondStack);
            return;
        }

        var kind = c.TryGetProperty("kind", out var k) ? k.GetString() : "stack";
        switch (kind)
        {
            case "binary":
                w.Write(BinOp.CondBinary);
                w.Write(st.Intern(c.TryGetProperty("operation", out var op) ? op.GetString() : null));
                break;

            case "expression":
            {
                w.Write(BinOp.CondExpression);
                InstructionDto? exprInstr = null;
                if (c.TryGetProperty("expression", out var expr)
                    && expr.ValueKind == JsonValueKind.Object)
                {
                    exprInstr = JsonSerializer.Deserialize<InstructionDto>(expr.GetRawText());
                }
                WriteInstr(w, st, exprInstr ?? new InstructionDto { opCode = "nop", operand = EmptyElement() });
                break;
            }

            case "block":
            {
                w.Write(BinOp.CondBlock);
                InstructionDto[] blockInstrs = [];
                if (c.TryGetProperty("block", out var blk) && blk.ValueKind == JsonValueKind.Array)
                    blockInstrs = JsonSerializer.Deserialize<InstructionDto[]>(blk.GetRawText()) ?? [];
                WriteInstrBlock(w, st, blockInstrs);
                break;
            }

            default:
                w.Write(BinOp.CondStack);
                break;
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static string? GetStr(JsonElement e, string property)
    {
        if (e.ValueKind != JsonValueKind.Object) return null;
        return e.TryGetProperty(property, out var p) && p.ValueKind == JsonValueKind.String
            ? p.GetString()
            : null;
    }

    // Handles both { field: "name" } (AstLowering) and { field: { name: "..." } } forms.
    private static string? GetFieldNameStr(JsonElement e)
    {
        if (e.ValueKind != JsonValueKind.Object || !e.TryGetProperty("field", out var f)) return null;
        if (f.ValueKind == JsonValueKind.String) return f.GetString();
        if (f.ValueKind == JsonValueKind.Object && f.TryGetProperty("name", out var n)) return n.GetString();
        return null;
    }

    private static string? GetArg0(JsonElement e)
    {
        if (e.ValueKind != JsonValueKind.Object
            || !e.TryGetProperty("arguments", out var arr)
            || arr.ValueKind != JsonValueKind.Array) return null;
        foreach (var item in arr.EnumerateArray()) return item.GetString();
        return null;
    }

    private static InstructionDto[] GetInstrArray(JsonElement e, string name)
    {
        if (e.ValueKind != JsonValueKind.Object
            || !e.TryGetProperty(name, out var arr)
            || arr.ValueKind != JsonValueKind.Array) return [];
        return JsonSerializer.Deserialize<InstructionDto[]>(arr.GetRawText()) ?? [];
    }

    private static List<(string? exType, InstructionDto[] block)> GetCatchBlocksData(JsonElement e)
    {
        var result = new List<(string?, InstructionDto[])>();
        if (e.ValueKind != JsonValueKind.Object
            || !e.TryGetProperty("catchBlocks", out var arr)
            || arr.ValueKind != JsonValueKind.Array) return result;

        foreach (var cb in arr.EnumerateArray())
        {
            var exType = cb.TryGetProperty("exceptionType", out var et)
                         && et.ValueKind == JsonValueKind.String
                         ? et.GetString() : null;
            var block  = cb.TryGetProperty("block", out var blk) && blk.ValueKind == JsonValueKind.Array
                         ? JsonSerializer.Deserialize<InstructionDto[]>(blk.GetRawText()) ?? []
                         : Array.Empty<InstructionDto>();
            result.Add((exType, block));
        }
        return result;
    }

    private static JsonElement EmptyElement() =>
        JsonSerializer.SerializeToElement(new { });

    // ── StringTableBuilder ────────────────────────────────────────────────────

    private sealed class StringTableBuilder
    {
        private readonly Dictionary<string, ushort> _map  = new(StringComparer.Ordinal) { { "", 0 } };
        private readonly List<string>               _list = [""];

        /// <summary>Returns the interned index for <paramref name="s"/> (0 = empty/null).</summary>
        public ushort Intern(string? s)
        {
            if (string.IsNullOrEmpty(s)) return 0;
            if (_map.TryGetValue(s, out var idx)) return idx;
            var newIdx = checked((ushort)_list.Count);
            _list.Add(s);
            _map[s] = newIdx;
            return newIdx;
        }

        /// <summary>Writes the string table header to <paramref name="w"/>.</summary>
        public void WriteTo(BinaryWriter w)
        {
            w.Write((uint)_list.Count);
            foreach (var s in _list)
            {
                var bytes = Encoding.UTF8.GetBytes(s);
                w.Write((ushort)bytes.Length);
                w.Write(bytes);
            }
        }
    }
}
