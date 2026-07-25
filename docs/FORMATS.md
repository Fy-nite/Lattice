# ObjectIR Input Formats

Lattice accepts ObjectIR modules in three equivalent formats. All three express identical semantics; choose based on your use case.

## TextIR (recommended for authoring)

Human-readable, line-oriented syntax. The canonical format for writing and testing ObjectIR code.

### Syntax overview

```textir
module ModuleName version 1.0.0

class ClassName {
  field fieldName : fieldType
  
  constructor (param : paramType) {
    ldarg this
    ldarg param
    stfld fieldName
    ret
  }
  
  method MethodName (param : paramType) -> returnType {
    ldarg this
    ldfld fieldName
    ret
  }
  
  static method StaticMethod () -> void {
    ldstr "hello"
    call System.Console.WriteLine (string) -> void
    ret
  }
}
```

### Module header

Every TextIR file begins with:

```textir
module ModuleName version major.minor[.patch]
```

### Type declarations

```textir
class TypeName { ... }          // Reference type (heap-allocated)
interface InterfaceName { ... } // Contract type
struct StructName { ... }       // Value type (host-defined semantics)
enum EnumName { ... }           // Enumeration
```

Modifiers: `public`, `private`, `protected`, `internal`, `abstract`, `sealed`

Inheritance:

```textir
class Dog : Animal {
  override method Speak () -> void implements Animal.Speak {
    ldstr "Woof!"
    call System.Console.WriteLine (string) -> void
    ret
  }
}
```

### Fields

```textir
field name : type
static field staticField : type
```

### Methods

```textir
method Name (param1 : type1, param2 : type2) -> returnType {
  local localVar : type
  
  ldarg param1
  ldloc localVar
  add
  ret
}

static method StaticMethod () -> void { ... }
virtual method VirtualMethod () -> void { ... }
override method OverrideMethod () -> void implements Interface.Method { ... }
abstract method AbstractMethod () -> void
```

### Instructions

Instructions occupy one line and consume operands from the remainder of that line.

```textir
ldc.i4 42              // Load constant
ldarg paramName        // Load argument
ldloc varName          // Load local
ldstr "hello"          // Load string
ldfld ClassName.field  // Load field
stfld ClassName.field  // Store field
add                    // Arithmetic
ceq                    // Comparison
call ClassName.Method (int32, string) -> void  // Call
callvirt ClassName.Method () -> int32          // Virtual call
newobj ClassName       // Create object
newarr                 // Create array
if { ... }             // Conditional
while { ... }          // Loop
try { ... }            // Exception handling
throw                  // Raise exception
ret                    // Return
```

### Control flow

#### if statement

```textir
if {
  "kind": "stack",
  "thenBlock": [
    { "opCode": "ldstr", "operand": "then branch" },
    { "opCode": "call", "operand": { "method": { "declaringType": "System.Console", "name": "WriteLine", "returnType": "void", "parameterTypes": ["string"] } } }
  ],
  "elseBlock": [
    { "opCode": "ldstr", "operand": "else branch" },
    { "opCode": "call", "operand": { "method": { "declaringType": "System.Console", "name": "WriteLine", "returnType": "void", "parameterTypes": ["string"] } } }
  ]
}
```

#### while loop

```textir
while {
  "condition": { "kind": "stack" },
  "body": [
    { "opCode": "ldarg", "operand": "counter" },
    { "opCode": "ldc.i4", "operand": 1 },
    { "opCode": "add" },
    { "opCode": "starg", "operand": "counter" }
  ]
}
```

#### try/catch/finally

```textir
try {
  "tryBlock": [ ... ],
  "catchBlocks": [
    {
      "type": "MyError",
      "variable": "err",
      "body": [ ... ]
    },
    {
      "body": [ ... ]  // Catch-all (no type)
    }
  ],
  "finallyBlock": [ ... ]
}
```

### Attributes (annotations)

```textir
@Serializable
class DataClass { ... }

@Deprecated("Use NewMethod instead")
method OldMethod () -> void { ... }

@NonSerialized
field cachedValue : int32
```

### Comments

```textir
// Line comment
```

### String escapes

| Sequence | Meaning |
|----------|---------|
| `\n` | Line feed |
| `\t` | Tab |
| `\r` | Carriage return |
| `\\` | Backslash |
| `\"` | Double quote |

---

## JSON (recommended for interchange)

Machine-generated or exchanged between tools. Structure mirrors the module schema.

### Example

```json
{
  "name": "Arithmetic",
  "version": "1.0.0",
  "types": [
    {
      "kind": "class",
      "name": "Calc",
      "namespace": null,
      "baseType": null,
      "baseInterfaces": [],
      "fields": [],
      "methods": [
        {
          "name": "Add",
          "returnType": "int32",
          "isStatic": true,
          "isVirtual": false,
          "isOverride": false,
          "isAbstract": false,
          "parameters": [
            { "name": "a", "type": "int32" },
            { "name": "b", "type": "int32" }
          ],
          "locals": [],
          "instructions": [
            { "opCode": "ldarg", "operand": "a" },
            { "opCode": "ldarg", "operand": "b" },
            { "opCode": "add" },
            { "opCode": "ret" }
          ],
          "attributes": []
        }
      ]
    }
  ],
  "functions": null
}
```

### Structure

**Top-level object:**

```json
{
  "name": "string",
  "version": "string (major.minor.patch)",
  "types": [ /* TypeObject[] */ ],
  "functions": null
}
```

**Type object:**

```json
{
  "kind": "class|interface|struct|enum",
  "name": "string",
  "namespace": "string|null",
  "baseType": "string|null",
  "baseInterfaces": ["string"],
  "fields": [ /* FieldObject[] */ ],
  "methods": [ /* MethodObject[] */ ],
  "attributes": [ /* AttributeObject[] */ ]
}
```

**Field object:**

```json
{
  "name": "string",
  "type": "string",
  "isStatic": "boolean",
  "attributes": [ /* AttributeObject[] */ ]
}
```

**Method object:**

```json
{
  "name": "string",
  "returnType": "string",
  "isStatic": "boolean",
  "isVirtual": "boolean",
  "isOverride": "boolean",
  "isAbstract": "boolean",
  "parameters": [
    { "name": "string", "type": "string" }
  ],
  "locals": [
    { "name": "string", "type": "string" }
  ],
  "instructions": [ /* InstructionObject[] */ ],
  "attributes": [ /* AttributeObject[] */ ]
}
```

**Instruction object:**

```json
{
  "opCode": "string (mnemonic)",
  "operand": "any (opcode-specific)"
}
```

Common operand forms:

| Opcode(s) | Operand |
|-----------|---------|
| `ldstr` | `"string"` or `{"value": "..."}` |
| `ldc`, `ldc.*` | `number` or `{"value": ..., "type": "typename"}` |
| `ldarg`, `starg`, `ldloc`, `stloc` | `"name"` or `0` (index) |
| `ldfld`, `stfld` | `"FieldName"` |
| `ldsfld`, `stsfld` | `{"field": {"declaringType": "...", "name": "..."}}` |
| `call`, `callvirt` | `{"method": {"declaringType": "...", "name": "...", "returnType": "...", "parameterTypes": [...]}}` |
| `newobj`, `conv`, `castclass`, `isinst` | `"TypeName"` |
| `if` | `{"condition": {...}, "thenBlock": [...], "elseBlock": [...]}` |
| `while` | `{"condition": {...}, "body": [...]}` |
| `try` | `{"tryBlock": [...], "catchBlocks": [...], "finallyBlock": [...]}` |

**Condition object:**

```json
{
  "kind": "stack|binary|expression|block",
  "operation": "ceq|cne|cgt|cge|clt|cle",  // For "binary"
  "body": [ /* InstructionObject[] */ ]     // For "expression" or "block"
}
```

**Attribute object:**

```json
{
  "name": "string",
  "args": [ /* values */ ]
}
```

---

## BIR/BSON (compiled binary format)

Lattice's compiled binary format uses MongoDB BSON encoding of the module schema. Produced by the `--compile` flag on the CLI, it is faster to load than TextIR (no parsing) and more compact for distribution.

### Compiling

```powershell
lattice --compile program.oir
```

This produces:
- `program.bir` — BSON binary (loadable by `ModuleSerializer.LoadFromBson`)
- `program.jir` — equivalent JSON representation
- `program.moduleinfo.txt` — human-readable method/instruction summary

### Loading in Lattice

```csharp
using ObjectIR.Core.Serialization;

var bytes = File.ReadAllBytes("program.bir");
var module = ModuleSerializer.LoadFromBson(bytes);
```

### How it works

The `ModuleSerializer` converts the `ModuleNode` AST to a `ModuleData` DTO, then serializes it
to BSON via the MongoDB driver. The round-trip is:

```
ModuleNode (AST)
    ↓ DumpModule()
ModuleData (DTO)
    ↓ BsonSerializer.ModuleDataToBson()
BSON document
    ↓ .ToBson()
byte[] (.bir file)
```

Deserialization reverses the process:

```
byte[] (.bir file)
    ↓ BsonDocumentSerializer.Instance.Deserialize()
BSON document
    ↓ BsonSerializer.BsonToModuleData()
ModuleData (DTO)
    ↓ LoadModule()
ModuleNode (AST)
```

### Encoding details

The `ModuleData` DTO mirrors the JSON schema (see above). BSON maps naturally to JSON types:

| JSON type | BSON type |
|-----------|-----------|
| string | UTF-8 string |
| number | Int32 or Double |
| boolean | Boolean |
| array | Array |
| object | Document |
| null | Null |

This means `.jir` and `.bir` files are semantically identical — you can convert between them
without information loss.

---

## FOB/IR v3 (production binary format)

The FOB (Flat Object Binary) format is the **recommended distribution format** for production
ObjectIR modules. It wraps a binary payload (BSON-encoded module data) in a self-describing
container with magic bytes, versioning, an includes table, and a string pool.

`--compile` produces a `.fob` file alongside `.bir` and `.jir`.

### Container layout

```
[Header]            24 bytes
  6  – ASCII magic "FOB/IR"
  2  – ushort version (= 3)
  4  – uint includesOffset
  4  – uint stringDataOffset
  4  – uint payloadOffset
  4  – uint payloadLength

[Includes]          @ includesOffset
  4  – uint count
  count × 4  – uint offset into StringData blob

[StringData]        @ stringDataOffset
  4  – uint dataLength
  dataLength bytes – null-terminated UTF-8 strings

[Payload]           @ payloadOffset
  payloadLength bytes – BSON-encoded module data
```

The includes section lists external type names the module depends on at runtime
(e.g. `IO`, `Thread`, `System.Console`). These are resolved on-demand via the
native hook system (`NativeRegistry`).

### Compiling to FOB

```powershell
lattice --compile program.oir
```

This produces:
- `program.fob` — FOB/IR v3 binary (recommended for distribution)
- `program.bir` — raw BSON payload (fast loading, minimal overhead)
- `program.jir` — equivalent JSON representation
- `program.moduleinfo.txt` — method/instruction summary

### Loading in Lattice

```powershell
lattice program.fob
```

```csharp
using ObjectIR.Core.Fob;
using lattice.Runtime.Compiler;

var fobBinary = FobIrReader.ReadFromFile("program.fob");
var module = ModuleBinaryReader.Read(fobBinary.Payload);
```

### When to use which format

| Format | Use case |
|--------|----------|
| **TextIR (`.oir`)** | Authoring, debugging, compiler development — human-readable, no tooling needed |
| **JSON (`.jir`)** | Interchange between tools, web services, inspection |
| **BIR/BSON (`.bir`)** | Fast local loading during development — no container overhead |
| **FOB/IR v3 (`.fob`)** | **Production distribution** — self-describing, versioned, includes dependency metadata |
