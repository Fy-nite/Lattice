# Lattice Error Codes

This document catalogs the various error codes and exceptions used in the Lattice runtime. Errors are designed to be descriptive and helpful, following a style similar to Rust's compiler errors.

## Error Code Categories

- **L**: Core / Generic Errors
- **R**: Runtime Execution Errors
- **E**: Entrypoint and Loading Errors
- **C**: CPU and Instruction Errors

---

## Core Errors (L)

### `L000`: Generic Lattice Error
- **Description**: The base class for all Lattice-specific exceptions.
- **Cause**: Typically used when a more specific error category hasn't been defined or for general system failures.
- **Help**: Check the error message for specific details on what went wrong.

---

## Runtime Errors (R)

### `R001`: General Runtime Exception
- **Description**: A generic error that occurs during the execution of a program.
- **Cause**: Unexpected state or operation during runtime.
- **Help**: This is a catch-all for runtime issues. Check the stack trace and location.

### `R002`: OpCode Not Found
- **Description**: The CPU encountered an instruction opcode it does not recognize.
- **Cause**: The program contains an instruction that is not implemented in the current version of the Lattice CPU.
- **Help**: The opcode '{opCode}' is not supported. Check for typos in the IR or ensure you are using a compatible version of the compiler and runtime.

### `R003`: Method Resolution Failure
- **Description**: The runtime could not find the target method for a call instruction.
- **Cause**: A `call` instruction refers to a method that does not exist in the loaded module or its classes.
- **Help**: Ensure that the method is defined and that the class name and method signature are correct.

---

## Entrypoint Errors (E)

### `E001`: Entrypoint Not Found
- **Description**: The runtime could not find a suitable entry point to begin execution.
- **Cause**:
    - The `Program` class is missing.
    - The `Main` method is missing within the `Program` class.
    - The `Main` method is not marked as `static`.
- **Help**: Create a `Program` class with a `static method Main()` method.

---

## CPU Errors (C)

### `C001`: CPU Instruction Exception
- **Description**: A specialized error related to the internal state of the CPU during instruction execution.
- **Cause**: Internal CPU logic errors or invalid state transitions.
- **Help**: Usually indicates a bug in the runtime's instruction handling logic.

---

## File System Errors

### Standard `FileNotFoundException`
- **Description**: The runtime could not locate the `.oir` file specified.
- **Cause**: The path provided to `LoadProgram` is invalid or the file is missing.
- **Help**: Verify that the file exists at the specified path.
