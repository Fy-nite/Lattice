// lattice_native.h — C++ declarations for ObjectIR native functions and runtime types.
// Generated from the Lattice runtime. Use this to build tooling, tests, or a
// C++-based VM that is compatible with ObjectIR bytecode.
//
// Requires C++17 or later (std::variant, std::string_view).

#pragma once

#include <cstdint>
#include <string>
#include <vector>
#include <functional>
#include <variant>
#include <optional>

// ---------------------------------------------------------------------------
// 1. ObjectIR primitive type mapping
// ---------------------------------------------------------------------------

namespace lattice {

// ObjectIR has four primitive types plus a generic object reference.
// The runtime boxes everything through `object?` on the managed side;
// on the C++ side you can represent values with a tagged union or
// std::variant.

enum class TypeKind : uint8_t {
    Void,
    Bool,
    Int32,
    Float32,
    String,
    Object,   // managed object / class instance
};

struct Value {
    TypeKind kind = TypeKind::Void;
    std::variant<std::monostate, bool, int32_t, float, std::string, void*> data;

    Value() : data(std::monostate{}) {}
    static Value Void() { return Value{}; }
    static Value FromBool(bool v)   { Value val; val.kind = TypeKind::Bool;    val.data = v; return val; }
    static Value FromInt(int32_t v) { Value val; val.kind = TypeKind::Int32;   val.data = v; return val; }
    static Value FromFloat(float v) { Value val; val.kind = TypeKind::Float32; val.data = v; return val; }
    static Value FromString(const std::string& v) { Value val; val.kind = TypeKind::String; val.data = v; return val; }
    static Value FromObject(void* v) { Value val; val.kind = TypeKind::Object; val.data = v; return val; }
};

// ---------------------------------------------------------------------------
// 2. ObjectIR opcodes (mirrors ObjectIR.Core.Ast.OpCode)
// ---------------------------------------------------------------------------

enum class OpCode : uint16_t {
    // Load instructions
    Ldarg   = 0,
    Ldloc   = 1,
    Ldfld   = 2,
    Ldsfld  = 3,
    Ldelem  = 4,
    Ldlen   = 5,
    Ldnull  = 6,
    LdcI4   = 7,
    LdcI8   = 8,
    LdcR4   = 9,
    LdcR8   = 10,
    Ldstr   = 11,

    // Store instructions
    Starg   = 12,
    Stloc   = 13,
    Stfld   = 14,
    Stsfld  = 15,
    Stelem  = 16,

    // Arithmetic
    Add     = 17,
    Sub     = 18,
    Mul     = 19,
    Div     = 20,
    Rem     = 21,
    Neg     = 22,
    And     = 23,
    Or      = 24,
    Xor     = 25,
    Not     = 26,
    Shl     = 27,
    Shr     = 28,

    // Comparison
    Ceq     = 29,
    Cne     = 30,
    Cgt     = 31,
    CgtUn   = 32,
    CgeUn   = 33,
    Clt     = 34,

    // Control flow
    Br      = 35,
    Brtrue  = 36,
    Brfalse = 37,
    Beq     = 38,
    Bne     = 39,
    Bgt     = 40,
    Blt     = 41,
    Ret     = 42,

    // Calls
    Call    = 43,
    Callvirt= 44,
    Calli   = 45,
    Newobj  = 46,

    // Object operations
    Newarr  = 47,
    Castclass = 48,
    Isinst  = 49,
    Box     = 50,
    Unbox   = 51,

    // Stack manipulation
    Dup     = 52,
    Pop     = 53,

    // Conversions
    ConvI4  = 54,
    ConvI8  = 55,
    ConvR4  = 56,
    ConvR8  = 57,
    ConvU4  = 58,
    ConvU8  = 59,

    // Structured control flow (high-level)
    If      = 60,
    While   = 61,
    For     = 62,
    Switch  = 63,
    Try     = 64,
    Break   = 65,
    Continue= 66,
    Throw   = 67,
};

// ---------------------------------------------------------------------------
// 3. Compact instruction (matches CompactInstr in CompiledMethod.cs)
// ---------------------------------------------------------------------------

struct CompactInstr {
    OpCode opcode;
    int32_t operand = 0;
};

// ---------------------------------------------------------------------------
// 4. Compiled method (matches CompiledMethod in CompiledMethod.cs)
// ---------------------------------------------------------------------------

struct CallTarget {
    std::string qualified_name;   // e.g. "IO.Println"
    std::vector<TypeKind> param_types;
    TypeKind return_type = TypeKind::Void;
};

struct NewObjTarget {
    std::string type_name;
    std::string ctor_signature;
};

struct CompiledMethod {
    std::string name;
    int local_count  = 0;
    int arg_count    = 0;
    bool returns_value = false;

    std::vector<CompactInstr> code;
    std::vector<std::string>  string_table;
    std::vector<float>        float_table;
    std::vector<std::string>  local_names;
    std::vector<std::string>  arg_names;
    std::vector<CallTarget>   call_targets;
    std::vector<NewObjTarget> newobj_targets;
};

// ---------------------------------------------------------------------------
// 5. Native function signatures — the functions you can call from ObjectIR
//
//    These are the exact signatures that the VM must provide. Each one
//    corresponds to a [NativeHook] class in the ObjectIR Stdlib.
//    "value" parameters are generic object references (boxing on managed
//    side); in C++ you'd pass a Value* or equivalent.
// ---------------------------------------------------------------------------

// -- IO class (static) -----------------------------------------------------

// IO.Print(value: object) -> void
//   Writes the string representation of `value` to stdout (no newline).
using Fn_IOPrint = std::function<void(const Value& value)>;

// IO.Println(value: object) -> void
//   Writes the string representation of `value` to stdout, followed by '\n'.
using Fn_IOPrintln = std::function<void(const Value& value)>;

// IO.Readln() -> string
//   Reads one line from stdin. Returns empty string on EOF.
using Fn_IOReadln = std::function<std::string()>;


// -- Thread class (static) -------------------------------------------------

// Thread.Spawn(delegate: object) -> void
//   Spawns a new execution thread running the method referenced by the
//   delegate. The delegate must implement IDelagate (have Target, Method
//   fields or a DelegateId).
using Fn_ThreadSpawn = std::function<void(const Value& delegate)>;

// Thread.Sleep(ms: int32) -> void
//   Suspends the current execution context for `ms` milliseconds.
using Fn_ThreadSleep = std::function<void(int32_t ms)>;


// -- Action class (instance, implements IDelagate) -------------------------

// Action.constructor(instance: object, methodName: string) -> void
//   Binds `methodName` on `instance` as a void-returning delegate.
//   Stores a DelegateId in the instance's Fields.
using Fn_ActionCtor = std::function<void(Value& instance, const std::string& method_name)>;

// Action.Invoke() -> void
//   Invokes the bound method. Reads DelegateId from the instance.
//   `this` is the Action object itself.
using Fn_ActionInvoke = std::function<void(Value& self)>;


// -- Func class (instance, implements IDelagate) ---------------------------

// Func.constructor(instance: object, methodName: string) -> void
//   Binds `methodName` on `instance` as a value-returning delegate.
//   Registers in DelegateRegistry; stores ID in Metadata.
using Fn_FuncCtor = std::function<void(Value& instance, const std::string& method_name)>;

// Func.Invoke() -> object
//   Invokes the bound method and returns the result.
using Fn_FuncInvoke = std::function<Value(Value& self)>;


// -- Delegate class (instance, data-only) ----------------------------------

// Delegate.constructor(target: object, methodName: string) -> void
//   Stores `target` and `methodName` as fields. Does NOT register in
//   DelegateRegistry — meant to be resolved later by Thread.Spawn.
using Fn_DelegateCtor = std::function<void(Value& target, const std::string& method_name)>;


// ---------------------------------------------------------------------------
// 6. VM callback interface — the hooks your C++ VM must implement
//
//    Aggregate all native functions into a single struct that the VM
//    loads at startup. Default implementations can be no-ops or stubs.
// ---------------------------------------------------------------------------

struct NativeHooks {
    // IO
    Fn_IOPrint    io_print    = [](const Value&) {};
    Fn_IOPrintln  io_println  = [](const Value&) {};
    Fn_IOReadln   io_readln   = []() -> std::string { return ""; };

    // Threading
    Fn_ThreadSpawn thread_spawn = [](const Value&) {};
    Fn_ThreadSleep thread_sleep = [](int32_t) {};

    // Delegates
    Fn_ActionCtor   action_ctor   = [](Value&, const std::string&) {};
    Fn_ActionInvoke action_invoke = [](Value&) {};
    Fn_FuncCtor     func_ctor     = [](Value&, const std::string&) {};
    Fn_FuncInvoke   func_invoke   = [](Value&) -> Value { return Value::Void(); };
    Fn_DelegateCtor delegate_ctor = [](Value&, const std::string&) {};
};

// ---------------------------------------------------------------------------
// 7. Bytecode interpreter helper — decode a CompactInstr operand based on
//    the opcode. Useful when building a disassembler or test harness.
// ---------------------------------------------------------------------------

inline const char* opcode_name(OpCode op) {
    switch (op) {
        case OpCode::Ldarg:      return "ldarg";
        case OpCode::Ldloc:      return "ldloc";
        case OpCode::Ldfld:      return "ldfld";
        case OpCode::Ldsfld:     return "ldsfld";
        case OpCode::Ldelem:     return "ldelem";
        case OpCode::Ldlen:      return "ldlen";
        case OpCode::Ldnull:     return "ldnull";
        case OpCode::LdcI4:      return "ldc.i4";
        case OpCode::LdcI8:      return "ldc.i8";
        case OpCode::LdcR4:      return "ldc.r4";
        case OpCode::LdcR8:      return "ldc.r8";
        case OpCode::Ldstr:      return "ldstr";
        case OpCode::Starg:      return "starg";
        case OpCode::Stloc:      return "stloc";
        case OpCode::Stfld:      return "stfld";
        case OpCode::Stsfld:     return "stsfld";
        case OpCode::Stelem:     return "stelem";
        case OpCode::Add:        return "add";
        case OpCode::Sub:        return "sub";
        case OpCode::Mul:        return "mul";
        case OpCode::Div:        return "div";
        case OpCode::Rem:        return "rem";
        case OpCode::Neg:        return "neg";
        case OpCode::And:        return "and";
        case OpCode::Or:         return "or";
        case OpCode::Xor:        return "xor";
        case OpCode::Not:        return "not";
        case OpCode::Shl:        return "shl";
        case OpCode::Shr:        return "shr";
        case OpCode::Ceq:        return "ceq";
        case OpCode::Cne:        return "cne";
        case OpCode::Cgt:        return "cgt";
        case OpCode::CgtUn:      return "cgt.un";
        case OpCode::CgeUn:      return "cge.un";
        case OpCode::Clt:        return "clt";
        case OpCode::Br:         return "br";
        case OpCode::Brtrue:     return "brtrue";
        case OpCode::Brfalse:    return "brfalse";
        case OpCode::Beq:        return "beq";
        case OpCode::Bne:        return "bne";
        case OpCode::Bgt:        return "bgt";
        case OpCode::Blt:        return "blt";
        case OpCode::Ret:        return "ret";
        case OpCode::Call:       return "call";
        case OpCode::Callvirt:   return "callvirt";
        case OpCode::Calli:      return "calli";
        case OpCode::Newobj:     return "newobj";
        case OpCode::Newarr:     return "newarr";
        case OpCode::Castclass:  return "castclass";
        case OpCode::Isinst:     return "isinst";
        case OpCode::Box:        return "box";
        case OpCode::Unbox:      return "unbox";
        case OpCode::Dup:        return "dup";
        case OpCode::Pop:        return "pop";
        case OpCode::ConvI4:     return "conv.i4";
        case OpCode::ConvI8:     return "conv.i8";
        case OpCode::ConvR4:     return "conv.r4";
        case OpCode::ConvR8:     return "conv.r8";
        case OpCode::ConvU4:     return "conv.u4";
        case OpCode::ConvU8:     return "conv.u8";
        case OpCode::If:         return "if";
        case OpCode::While:      return "while";
        case OpCode::For:        return "for";
        case OpCode::Switch:     return "switch";
        case OpCode::Try:        return "try";
        case OpCode::Break:      return "break";
        case OpCode::Continue:   return "continue";
        case OpCode::Throw:      return "throw";
    }
    return "unknown";
}

// Returns true if the opcode pops exactly one value and pushes nothing
// (i.e. it is a side-effect-only instruction like Pop, Stloc, Starg,
// Stfld, etc.).
inline bool opcode_is_side_effect_only(OpCode op) {
    switch (op) {
        case OpCode::Stloc:
        case OpCode::Starg:
        case OpCode::Stfld:
        case OpCode::Stsfld:
        case OpCode::Stelem:
        case OpCode::Pop:
            return true;
        default:
            return false;
    }
}

// Returns true if the opcode is a branch/jump instruction.
inline bool opcode_is_branch(OpCode op) {
    switch (op) {
        case OpCode::Br:
        case OpCode::Brtrue:
        case OpCode::Brfalse:
        case OpCode::Beq:
        case OpCode::Bne:
        case OpCode::Bgt:
        case OpCode::Blt:
            return true;
        default:
            return false;
    }
}

// Returns true if the opcode is a comparison (pushes a bool result).
inline bool opcode_is_comparison(OpCode op) {
    switch (op) {
        case OpCode::Ceq:
        case OpCode::Cne:
        case OpCode::Cgt:
        case OpCode::CgtUn:
        case OpCode::CgeUn:
        case OpCode::Clt:
            return true;
        default:
            return false;
    }
}

} // namespace lattice
