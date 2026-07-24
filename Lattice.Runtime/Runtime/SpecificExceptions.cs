namespace lattice.Throwables;

public class OpCodeNotFoundException : RuntimeException
{
    public OpCodeNotFoundException(string opCode, string location)
        : base($"Unknown opcode: {opCode}", "R002",
              $"The opcode '{opCode}' is not supported by the current CPU implementation. Check for typos or unsupported instructions.",
              location)
    {
    }
}

public class MethodResolutionException : RuntimeException
{
    public MethodResolutionException(string methodName, string location)
        : base($"Could not resolve method: {methodName}", "R003",
              $"Ensure that the method '{methodName}' is defined and accessible.",
              location)
    {
    }
}

public class EntrypointNotFoundException : RuntimeException
{
    public EntrypointNotFoundException(string message, string help)
        : base(message, "E001", help)
    {
    }
}

public class LatticeStackOverflowException : RuntimeException
{
    public LatticeStackOverflowException(string location)
        : base("Stack overflow", "R005",
              "The virtual machine has reached its maximum recursion depth. Check for infinite recursion in your code.",
              location)
    {
    }
}
