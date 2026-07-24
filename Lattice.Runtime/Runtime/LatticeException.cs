namespace lattice.Throwables;

public class LatticeException : Exception
{
    public string ErrorCode { get; set; }
    public string HelpText { get; set; }
    public string Location { get; set; }
    public List<string> Notes { get; set; } = new();

    public LatticeException(string message, string errorCode = "L000", string helpText = "", string location = "", IEnumerable<string>? notes = null)
        : base(message)
    {
        ErrorCode = errorCode;
        HelpText = helpText;
        Location = location;
        if (notes != null) Notes.AddRange(notes);
    }

    public override string Message
    {
        get
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"error[{ErrorCode}]: {base.Message}");
            if (!string.IsNullOrEmpty(Location))
                sb.AppendLine($"  --> {Location}");
            foreach (var n in Notes)
                sb.AppendLine($"  = note: {n}");
            if (!string.IsNullOrEmpty(HelpText))
            {
                sb.AppendLine();
                sb.AppendLine($"  = help: {HelpText}");
            }
            return sb.ToString().TrimEnd();
        }
    }
}
