namespace lattice.Throwables;

public class RuntimeException : LatticeException
{
    public RuntimeException(string message, string errorCode = "R001", string helpText = "", string location = "", IEnumerable<string>? notes = null)
        : base(message, errorCode, helpText, location, notes)
    {
    }
}
