using ObjectIR.Core;
using ObjectIR.Stdlib.System;

namespace lattice.Runtime;
using lattice;
class Program
{
    public static CPU cpu = new();
    public static bool DebugMode = false;
    public static string ProgramPath;
    static void Main(string[] args)
    {
        foreach (var arg in args)
        {
            if (arg == "--debug")
            {
                DebugMode = true;
            }
            else
            {
                ProgramPath = arg;
            }
        }

        if (ProgramPath == null)
        {
            IO.Println(new Value<Object>("Usage: lattice [--debug] <program.oir/fob>"));
        }
    }
}