using System.Text;
using System.Text.Json;
using lattice.IR;

namespace Lattice.Decompiler.Decompiler;

/// <summary>
/// Converts a <see cref="ModuleDto"/> (and its parts) back into human-readable TextIR source.
/// </summary>
public static class IrPrettyPrinter
{
    public static string PrintModule(ModuleDto module)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"module {module.name} version {module.version}");
        sb.AppendLine();
        foreach (var type in module.types)
        {
            sb.AppendLine(PrintType(type));
        }
        return sb.ToString();
    }

    public static string PrintType(TypeDto type)
    {
        var sb = new StringBuilder();

        // Attributes
        foreach (var attr in type.attributes)
            sb.AppendLine(PrintAttribute(attr));

        // Modifiers
        var mods = new List<string>();
        if (!string.IsNullOrWhiteSpace(type.access)) mods.Add(type.access);
        if (type.isAbstract) mods.Add("abstract");
        if (type.isSealed)   mods.Add("sealed");
        if (mods.Count > 0)
            sb.Append(string.Join(" ", mods) + " ");

        sb.Append($"{type.kind} {type.name}");

        // Inheritance
        var bases = new List<string>();
        if (!string.IsNullOrWhiteSpace(type.baseType)) bases.Add(type.baseType);
        if (type.interfaces?.Length > 0) bases.AddRange(type.interfaces);
        if (bases.Count > 0)
            sb.Append(" : " + string.Join(", ", bases));

        sb.AppendLine(" {");

        foreach (var field in type.fields)
            sb.AppendLine("    " + PrintField(field));

        foreach (var method in type.methods)
        {
            sb.AppendLine();
            foreach (var line in PrintMethod(method, type.name).Split('\n'))
                sb.AppendLine("    " + line.TrimEnd());
        }

        sb.AppendLine("}");
        return sb.ToString();
    }

    public static string PrintField(FieldDto field)
    {
        var sb = new StringBuilder();
        foreach (var attr in field.attributes)
            sb.AppendLine(PrintAttribute(attr));

        var mods = new List<string>();
        if (!string.IsNullOrWhiteSpace(field.access)) mods.Add(field.access);
        if (field.isStatic) mods.Add("static");
        if (mods.Count > 0) sb.Append(string.Join(" ", mods) + " ");

        sb.Append($"field {field.name}: {field.type}");
        return sb.ToString().TrimEnd();
    }

    public static string PrintMethod(MethodDto method, string declaringType = "")
    {
        var sb = new StringBuilder();

        foreach (var attr in method.attributes)
            sb.AppendLine(PrintAttribute(attr));

        // Signature
        var mods = new List<string>();
        if (!string.IsNullOrWhiteSpace(method.access)) mods.Add(method.access);
        if (method.isStatic)   mods.Add("static");
        if (method.isVirtual)  mods.Add("virtual");
        if (method.isOverride) mods.Add("override");
        if (method.isAbstract) mods.Add("abstract");
        if (mods.Count > 0) sb.Append(string.Join(" ", mods) + " ");

        var paramList = string.Join(", ", method.parameters.Select(p => $"{p.name}: {p.type}"));

        if (method.isConstructor)
            sb.Append($"constructor({paramList})");
        else
            sb.Append($"method {method.name}({paramList}) -> {method.returnType}");

        sb.AppendLine(" {");

        // Locals
        foreach (var local in method.localVariables)
            sb.AppendLine($"    local {local.name}: {local.type}");
        if (method.localVariables.Length > 0) sb.AppendLine();

        // Instructions
        foreach (var instr in method.instructions)
            sb.AppendLine("    " + PrintInstruction(instr));

        sb.Append("}");
        return sb.ToString();
    }

    private static string PrintInstruction(InstructionDto instr)
    {
        if (instr.operand.ValueKind == JsonValueKind.Undefined)
            return instr.opCode;

        return instr.opCode switch
        {
            "ldstr"  => $"ldstr \"{EscapeString(GetStr(instr.operand, "value") ?? "")}\"",
            "ldc" or "ldc.i4" or "ldc.i8" or "ldc.r4" or "ldc.r8"
                     => FormatLdc(instr),
            "ldarg"  => FormatNameOrIndex(instr.opCode, instr.operand, "argumentName", "index"),
            "starg"  => FormatNameOrIndex(instr.opCode, instr.operand, "argumentName", null),
            "ldloc"  => FormatNameOrIndex(instr.opCode, instr.operand, "localName", null),
            "stloc"  => FormatNameOrIndex(instr.opCode, instr.operand, "localName", null),
            "ldfld" or "stfld"
                     => $"{instr.opCode} {GetFieldRef(instr.operand)}",
            "ldsfld" or "stsfld"
                     => $"{instr.opCode} {GetStaticFieldRef(instr.operand)}",
            "newobj" => $"newobj {GetStr(instr.operand, "type") ?? "?"}",
            "newarr" => $"newarr {GetStr(instr.operand, "elementType") ?? "?"}",
            "conv"   => $"conv {GetStr(instr.operand, "targetType") ?? "?"}",
            "castclass" or "isinst"
                     => $"{instr.opCode} {GetStr(instr.operand, "targetType") ?? "?"}",
            "call" or "callvirt"
                     => FormatCall(instr),
            "if"     => FormatIf(instr),
            "while"  => FormatWhile(instr),
            "try"    => FormatTry(instr),
            _        => $"{instr.opCode} {instr.operand}"
        };
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static string FormatLdc(InstructionDto instr)
    {
        var val  = GetStr(instr.operand, "value") ?? "0";
        var type = GetStr(instr.operand, "type");
        return type is null ? $"{instr.opCode} {val}" : $"{instr.opCode} {val} // {type}";
    }

    private static string FormatNameOrIndex(string op, JsonElement operand, string nameProp, string? indexProp)
    {
        var name = GetStr(operand, nameProp);
        if (name != null) return $"{op} {name}";
        if (indexProp != null && operand.TryGetProperty(indexProp, out var idx))
            return $"{op} {idx}";
        return op;
    }

    private static string GetFieldRef(JsonElement operand)
    {
        if (operand.TryGetProperty("field", out var f))
        {
            if (f.ValueKind == JsonValueKind.String) return f.GetString() ?? "?";
            var dt = f.TryGetProperty("declaringType", out var d) ? d.GetString() : null;
            var fn = f.TryGetProperty("name",          out var n) ? n.GetString() : null;
            return dt != null ? $"{dt}.{fn}" : fn ?? "?";
        }
        return "?";
    }

    private static string GetStaticFieldRef(JsonElement operand)
    {
        if (operand.TryGetProperty("field", out var f) && f.ValueKind == JsonValueKind.Object)
        {
            var dt = f.TryGetProperty("declaringType", out var d) ? d.GetString() : null;
            var fn = f.TryGetProperty("name",          out var n) ? n.GetString() : null;
            return $"{dt}.{fn}";
        }
        return "?";
    }

    private static string FormatCall(InstructionDto instr)
    {
        if (instr.operand.TryGetProperty("method", out var m) && m.ValueKind == JsonValueKind.Object)
        {
            var dt   = GetStr(m, "declaringType") ?? "";
            var name = GetStr(m, "name") ?? "";
            var ret  = GetStr(m, "returnType") ?? "void";
            var parms = m.TryGetProperty("parameterTypes", out var pt) && pt.ValueKind == JsonValueKind.Array
                ? string.Join(", ", pt.EnumerateArray().Select(e => e.GetString() ?? ""))
                : "";
            return $"{instr.opCode} {dt}.{name}({parms}) -> {ret}";
        }
        return instr.opCode;
    }

    private static string FormatIf(InstructionDto instr)
    {
        var cond = FormatCondition(instr.operand);
        var sb   = new StringBuilder();
        sb.AppendLine($"if ({cond}) {{");
        if (instr.operand.TryGetProperty("thenBlock", out var then) && then.ValueKind == JsonValueKind.Array)
            foreach (var i in then.EnumerateArray())
                sb.AppendLine("    " + PrintInstruction(JsonSerializer.Deserialize<InstructionDto>(i.GetRawText())!));
        var hasElse = instr.operand.TryGetProperty("elseBlock", out var elseB) && elseB.ValueKind == JsonValueKind.Array;
        if (hasElse)
        {
            sb.AppendLine("} else {");
            foreach (var i in elseB.EnumerateArray())
                sb.AppendLine("    " + PrintInstruction(JsonSerializer.Deserialize<InstructionDto>(i.GetRawText())!));
        }
        sb.Append("}");
        return sb.ToString();
    }

    private static string FormatWhile(InstructionDto instr)
    {
        var cond = FormatCondition(instr.operand);
        var sb   = new StringBuilder();
        sb.AppendLine($"while ({cond}) {{");
        if (instr.operand.TryGetProperty("body", out var body) && body.ValueKind == JsonValueKind.Array)
            foreach (var i in body.EnumerateArray())
                sb.AppendLine("    " + PrintInstruction(JsonSerializer.Deserialize<InstructionDto>(i.GetRawText())!));
        sb.Append("}");
        return sb.ToString();
    }

    private static string FormatTry(InstructionDto instr)
    {
        var sb = new StringBuilder();
        sb.AppendLine("try {");
        if (instr.operand.TryGetProperty("tryBlock", out var tryB) && tryB.ValueKind == JsonValueKind.Array)
            foreach (var i in tryB.EnumerateArray())
                sb.AppendLine("    " + PrintInstruction(JsonSerializer.Deserialize<InstructionDto>(i.GetRawText())!));

        if (instr.operand.TryGetProperty("catchBlocks", out var catchArr) && catchArr.ValueKind == JsonValueKind.Array)
        {
            foreach (var cb in catchArr.EnumerateArray())
            {
                var et = cb.TryGetProperty("exceptionType", out var e) ? e.GetString() : null;
                sb.AppendLine(et is null ? "} catch {" : $"}} catch ({et}) {{");
                if (cb.TryGetProperty("block", out var bk) && bk.ValueKind == JsonValueKind.Array)
                    foreach (var i in bk.EnumerateArray())
                        sb.AppendLine("    " + PrintInstruction(JsonSerializer.Deserialize<InstructionDto>(i.GetRawText())!));
            }
        }

        if (instr.operand.TryGetProperty("finallyBlock", out var fin) && fin.ValueKind == JsonValueKind.Array)
        {
            sb.AppendLine("} finally {");
            foreach (var i in fin.EnumerateArray())
                sb.AppendLine("    " + PrintInstruction(JsonSerializer.Deserialize<InstructionDto>(i.GetRawText())!));
        }
        sb.Append("}");
        return sb.ToString();
    }

    private static string FormatCondition(JsonElement operand)
    {
        if (!operand.TryGetProperty("condition", out var cond) || cond.ValueKind != JsonValueKind.Object)
            return "stack";
        var kind = GetStr(cond, "kind") ?? "stack";
        if (kind == "binary")
        {
            var op = GetStr(cond, "operation") ?? "ceq";
            return op;
        }
        return kind;
    }

    private static string PrintAttribute(AttributeDto attr)
    {
        var args = attr.constructorArguments.Length > 0
            ? $"({string.Join(", ", attr.constructorArguments.Select(a => a?.ToString() ?? "null"))})"
            : "";
        return $"@{attr.type}{args}";
    }

    private static string? GetStr(JsonElement el, string prop)
        => el.ValueKind == JsonValueKind.Object && el.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.String
           ? v.GetString()
           : null;

    private static string EscapeString(string s)
        => s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "\\r").Replace("\t", "\\t");
}
