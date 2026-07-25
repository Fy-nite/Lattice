using CommandLine;
using ObjectIR.Core;
using ObjectIR.Core.AST;
using ObjectIR.Core.Fob;
using ObjectIR.Core.Serialization;
using ObjectIR.Stdlib.System;
using lattice;
using lattice.Runtime.Compiler;
using lattice.Throwables;
using MongoDB.Bson;
using System.Reflection;
using ObjectIR.StdLib.Core.Memory;

namespace lattice.Runtime;

class Program
{
    public class Options
    {
        [Option('d', "debug", Required = false, HelpText = "Enable debug mode.")]
        public bool Debug { get; set; }

        [Option('c', "compile", Required = false, HelpText = "Compile the module to BIR/JIR.")]
        public bool Compile { get; set; }

        [Option('i', "dump-ir", Required = false, HelpText = "Dump the module IR code to the console and exit.")]
        public bool DumpIR { get; set; }

        [Option('m', "module-info", Required = false, HelpText = "Print a summary of the module (methods and instruction counts).")]
        public bool ModuleInfo { get; set; }

        [Option("summary", Required = false, HelpText = "Print a detailed summary report of the module.")]
        public bool Summary { get; set; }

        [Option("markdown", Required = false, HelpText = "Print module info in Markdown format.")]
        public bool Markdown { get; set; }

        [Option('q', "quiet", Required = false, HelpText = "Suppress the runtime header and version info.")]
        public bool Quiet { get; set; }

        [Option("experimental", Required = false, HelpText = "Enable experimental features (space-separated): jit, heap, manual-malloc, generalized-jit, native-transpile")]
        public IEnumerable<string> Experimental { get; set; } = Enumerable.Empty<string>();

        [Option('o', "output", Required = false, HelpText = "Specify the base path for output files (used with --compile).")]
        public string? OutputPath { get; set; }

        [Value(0, MetaName = "input path", Required = true, HelpText = "Path to the .oir, .bir, or .fob file.")]
        public string InputPath { get; set; } = string.Empty;

        [Value(1, MetaName = "program args", HelpText = "Arguments passed to the Lattice program.")]
        public IEnumerable<string> ProgramArgs { get; set; } = Enumerable.Empty<string>();
    }

    public static Scheduler scheduler = new();
    public static ModuleNode RootModule = default!;

    static void Main(string[] args)
    {
        Parser.Default.ParseArguments<Options>(args)
            .WithParsed(RunOptions)
            .WithNotParsed(HandleParseError);
    }

    static void RunOptions(Options opts)
    {
        if (!opts.Quiet)
        {
            Console.WriteLine(new string('-', 15));
            Console.WriteLine("Lattice Runtime");
            Console.WriteLine($"V: {Assembly.GetExecutingAssembly().GetName().Version}");
            Console.WriteLine($"Hash: {Assembly.GetExecutingAssembly().GetHashCode().ToString()}");
            Console.WriteLine(new string('-', 15));
        }

        if (!File.Exists(opts.InputPath))
        {
            Console.Error.WriteLine($"Error: File not found: {opts.InputPath}");
            Environment.Exit(1);
        }

        var ext = Path.GetExtension(opts.InputPath);
        if (ext.Contains(".fob"))
        {
            var fobBinary = FobIrReader.ReadFromFile(opts.InputPath);
            RootModule = ModuleBinaryReader.Read(fobBinary.Payload);
        }
        else if (ext.Contains(".bir"))
        {
            RootModule = ModuleSerializer.LoadFromBson(File.ReadAllBytes(opts.InputPath));
        }
        else
        {
            RootModule = TextIrParser.ParseModule(File.ReadAllText(opts.InputPath));
        }

        if (opts.DumpIR)
        {
            Console.WriteLine(RootModule.DumpIRCode());
            Environment.Exit(0);
        }

        if (opts.ModuleInfo)
        {
            Console.WriteLine(RootModule.DumpText());
            Environment.Exit(0);
        }

        if (opts.Summary)
        {
            Console.WriteLine(RootModule.GenerateSummaryReport());
            Environment.Exit(0);
        }

        if (opts.Markdown)
        {
            Console.WriteLine(RootModule.DumpMarkdown());
            Environment.Exit(0);
        }

        if (opts.Compile)
        {
            string basePath = opts.OutputPath ?? opts.InputPath;

            // Strip existing extension for output paths
            if (Path.HasExtension(basePath))
                basePath = Path.ChangeExtension(basePath, null);

            // BIR/BSON binary — fast to load, no parsing
            File.WriteAllBytes(basePath + ".bir", RootModule.DumpBson());
            Console.WriteLine($"  -> {basePath}.bir");

            // JSON interchange format
            File.WriteAllText(basePath + ".jir", RootModule.DumpJson());
            Console.WriteLine($"  -> {basePath}.jir");

            // FOB/IR v3 — production binary with includes table
            var payload = ModuleBinaryWriter.Write(RootModule);
            var includes = FobIrCompiler.CollectIncludes(RootModule);
            var fobBytes = new FobIrCompiler().CompileFromPayload(payload, includes);
            File.WriteAllBytes(basePath + ".fob", fobBytes);
            Console.WriteLine($"  -> {basePath}.fob");

            // Module info summary
            File.WriteAllText(basePath + ".moduleinfo.txt", RootModule.DumpText());
            Console.WriteLine($"  -> {basePath}.moduleinfo.txt");

            Environment.Exit(0);
        }

        try
        {
            var features = Experimental.Parse(opts.Experimental);
            Experimental.Set(features);

            if (!opts.Quiet && features != ExperimentalFeature.None)
            {
                Console.WriteLine($"[experimental] Active features: {features}");
            }

            // Scan for native hooks in the standard library
            NativeRegistry.RegisterFromAssembly(typeof(ObjectIR.Stdlib.System.IO).Assembly);
            NativeRegistry.RegisterFromAssembly(typeof(Program).Assembly);

            CPU mainCpu = new(features);
            mainCpu.Scheduler = scheduler;
            mainCpu.Debug = opts.Debug;

            if (Experimental.IsEnabled(ExperimentalFeature.Heap) || Experimental.IsEnabled(ExperimentalFeature.ManualMalloc))
            {
                mainCpu.Heap = new lattice.Runtime.Memory.HeapAllocator();
            }

            mainCpu.LoadModule(RootModule);
            
            // We need to initialize the main thread in the CPU
            // instead of calling Run() which is blocking.
            // We'll add a Prepare() or similar to CPU.
            mainCpu.InitializeMain(opts.ProgramArgs.ToArray());
            
            scheduler.AddThread(mainCpu);
            scheduler.Run();
        }
        catch (LatticeStackOverflowException st)
        {
            Console.WriteLine($"{st.Message}");
            Console.WriteLine(st.StackTrace.ToJson());
        }
        catch (LatticeException ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.Error.WriteLine(ex.Message);
            Console.ResetColor();
            Environment.Exit(1);
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.DarkRed;
            Console.Error.WriteLine("An unexpected error occurred:");
            Console.Error.WriteLine(ex.ToString());
            Console.ResetColor();
            Environment.Exit(1);
        }
    }

    static void HandleParseError(IEnumerable<Error> errs)
    {
        // Default help text will be displayed by the library
        Environment.Exit(1);
    }
}
