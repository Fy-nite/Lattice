using ObjectIR.Core.AST;

namespace lattice.Core;

public class CallStack
{
    public MethodNode Method { get; set; }
    public int IP { get; set; }
    public Dictionary<string, object> Locals { get; set; } = new();
    public Dictionary<string, object> Args { get; set; } = new();
    public ManagedObject? This { get; set; }
    public CallStack? Previous { get; set; }
    public Stack<object> EvaluationStack { get; set; } = new();
    public bool BreakRequested { get; set; }
    public bool ContinueRequested { get; set; }

    public CallStack(MethodNode method, ManagedObject? thisObj = null)
    {
        Method = method;
        IP = 0;
        Locals = new Dictionary<string, object>();
        Args = new Dictionary<string, object>();
        This = thisObj;
        if (thisObj != null)
        {
            Args["this"] = thisObj;
        }
        Previous = null;
        EvaluationStack = new Stack<object>();
    }

    public CallStack PushFrame(MethodNode newMethod, ManagedObject? thisObj = null)
    {
        var frame = new CallStack(newMethod, thisObj);
        frame.Previous = this;
        return frame;
    }

    public CallStack? PopFrame() => Previous;

    public override string ToString()
    {
        var name = Method?.Name ?? "unknown";
        return $"at {name} @ {IP}";
    }

    public string GetStackTrace()
    {
        var sb = new System.Text.StringBuilder();
        var current = this;
        while (current != null)
        {
            sb.AppendLine(current.ToString());
            current = current.Previous;
        }
        return sb.ToString();
    }
}
