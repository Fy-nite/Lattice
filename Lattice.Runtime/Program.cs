using ObjectIR.Core;
using ObjectIR.Core.AST;
using ObjectIR.Core.Serialization;
using ObjectIR.Stdlib.System;

namespace lattice.Runtime;
using lattice;
class Program
{
    public static CPU cpu = new();
    public static ModuleNode RootModule;
    public static bool DebugMode = false;
    public static string ProgramPath;
    static void Main(string[] args)
    {
        foreach (var arg in args)
        {
            if (!arg.Contains("-"))
            {
                ProgramPath = arg;
            }
            else if (arg == "--debug")
            {
                DebugMode = true;
            }
            else if (arg == "--compile")
            {
                CompileModule = true;
                RootModule = TextIrParser.ParseModule("");
            }
           
        }

        if (ProgramPath == null)
        {
            IO.Println(new Value<Object>("Usage: lattice [--debug, --compile] <program.oir/fob>"));
            Environment.Exit(1);
        }

        if (Path.GetExtension(ProgramPath).Contains(".oir"))
        {
            RootModule = ModuleSerializer.LoadFromBson(File.ReadAllBytes(ProgramPath));
        }
    }
}