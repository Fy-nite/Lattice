using System.Text.Json;
using lattice.IR;
using lattice.Runtime;
using lattice.TextIR;
using ObjectIR;
using ObjectIR.Core.Builder;
using ObjectIR.Core.Composition;
using ObjectIR.Core.IR;
using ObjectIR.Core.Serialization;
using ObjectIR.FobCompiler;

namespace lattice
{
    public class Program
    {
        public static void Main(string[] args)
        {
            if (args.Length == 0)
            {
                PrintHelp();
                return;
            }

            // ── Sub-commands ──────────────────────────────────────────────
            switch (args[0].ToLowerInvariant())
            {
                case "compile":
                    RunCompile(args[1..]);
                    return;

                case "decompile":
                    RunDecompile(args[1..]);
                    return;

                case "run":
                    // explicit "run" sub-command: shift args so the path is args[0]
                    RunFile(args[1..]);
                    return;

                case "--help":
                case "-h":
                case "help":
                    PrintHelp();
                    return;
            }

            // ── Default: treat first arg as a file to run ─────────────────
            RunFile(args);
        }

        // ── run ───────────────────────────────────────────────────────────

        private static void RunFile(string[] args)
        {
            if (args.Length == 0)
            {
                Console.Error.WriteLine("Error: no file specified.");
                PrintHelp();
                return;
            }

            var path = args[0];
            if (!File.Exists(path))
            {
                Console.Error.WriteLine($"Error: file '{path}' does not exist.");
                return;
            }

            try
            {
                var ir = new IRRuntime();

                var ext = Path.GetExtension(path).ToLowerInvariant();
                if (ext == ".fobir")
                {
                    // FOB/IR binary — load from bytes
                    ir.LoadFobFile(path);
                }
                else
                {
                    // TextIR, JSON, or AST text file
                    ir.LoadModule(File.ReadAllText(path));
                }

                ir.Run();
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error: {ex.Message}");
            }
        }

        // ── compile ───────────────────────────────────────────────────────

        private static void RunCompile(string[] args)
        {
            if (args.Length == 0)
            {
                Console.Error.WriteLine("Usage: lattice compile <input.textir> [output.fobir]");
                return;
            }

            var inputPath  = args[0];
            var outputPath = args.Length > 1 ? args[1] : Path.ChangeExtension(inputPath, ".fobir");

            if (!File.Exists(inputPath))
            {
                Console.Error.WriteLine($"Error: file '{inputPath}' does not exist.");
                return;
            }

            try
            {
                // 1. Parse the TextIR source into an AST.
                var astModule = FobIrCompiler.ParseTextIrFile(inputPath);

                // 2. Lower the AST to a ModuleDto (AstLowering is internal to lattice).
                var moduleDto = AstLowering.Lower(astModule);

                // 3. Serialise the compiled ModuleDto to compact binary bytecode.
                var payload = ModuleBinaryWriter.Write(moduleDto);

                // 4. Collect include references from the AST.
                var includes = FobIrCompiler.CollectIncludes(astModule);

                // 5. Pack into a v3 FOB/IR binary and write to disk.
                var bytes = new FobIrCompiler().CompileFromPayload(payload, includes);
                File.WriteAllBytes(outputPath, bytes);

                Console.WriteLine($"  compiled  {inputPath}");
                Console.WriteLine($"       ->   {outputPath} ({bytes.Length:N0} bytes, v3 bytecode)");
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Compile error: {ex.Message}");
            }
        }

        // ── decompile ─────────────────────────────────────────────────────

        private static void RunDecompile(string[] args)
        {
            if (args.Length == 0)
            {
                Console.Error.WriteLine("Usage: lattice decompile <input.fobir> [output.textir]");
                return;
            }

            var inputPath  = args[0];
            var outputPath = args.Length > 1 ? args[1] : Path.ChangeExtension(inputPath, ".textir");

            if (!File.Exists(inputPath))
            {
                Console.Error.WriteLine($"Error: file '{inputPath}' does not exist.");
                return;
            }

            try
            {
                var decompiler = new FobIrDecompiler();
                decompiler.DecompileFile(inputPath, outputPath);
                Console.WriteLine($"  decompiled  {inputPath}");
                Console.WriteLine($"          ->  {outputPath}");
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Decompile error: {ex.Message}");
            }
        }

        // ── help ──────────────────────────────────────────────────────────

        private static void PrintHelp()
        {
            Console.WriteLine("""
                lattice — ObjectIR runtime

                Usage:
                  lattice <file>                              Run a TextIR / JSON / FOB/IR file
                  lattice run      <file>                     Run a TextIR / JSON / FOB/IR file
                  lattice compile  <input.textir> [out.fobir] Compile TextIR to FOB/IR binary
                  lattice decompile <input.fobir> [out.textir] Decompile FOB/IR binary to TextIR
                  lattice help                                Show this message

                Supported file types:
                  .oir / .textir   TextIR source
                  .fobir           FOB/IR binary
                  .json            JSON IR module
                """);
        }
    }
}
