using Xunit;
using Lattice.Runtime;
using lattice.Runtime;
using ObjectIR.Core.AST;
using System.IO;
using ObjectIR.StdLib.Core.Memory;

namespace Lattice.Runtime.Tests
{
    public class RuntimeTests
    {
        [Fact]
        public void TestThreadedDemoExecution()
        {
            // Simulate running the demo_threads.oir file
            string oirPath = Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", "..", "Lattice.Runtime", "demos", "demo_threads.oir");
            Assert.True(File.Exists(oirPath), $"Test OIR file not found at {Path.GetFullPath(oirPath)}");

            var cpu = new CPU();
            cpu.Scheduler = new Scheduler();
            // Register native hooks
            NativeRegistry.RegisterFromAssembly(typeof(ObjectIR.Stdlib.System.IO).Assembly);
            
            // The OIR needs to be parsed, and the resulting ModuleNode used. 
            // In the real runtime, this is handled by ProgramLoader.
            // We'll manually parse it to mimic the runtime environment.
            var module = TextIrParser.ParseModule(File.ReadAllText(oirPath));
            cpu.LoadModule(module);
            
            // We expect the execution to complete without crashing
            var exception = Record.Exception(() => cpu.InitializeMain(new string[] { }));
            Assert.Null(exception);

            cpu.Scheduler.Run(); // Use scheduler to run
        }
    }
}
