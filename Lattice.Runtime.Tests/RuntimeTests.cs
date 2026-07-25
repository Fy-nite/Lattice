using Xunit;
using Lattice.Runtime;
using lattice.Runtime;
using ObjectIR.Core.AST;
using System.IO;
using ObjectIR.StdLib.Core.Memory;
using AstOpCode = global::ObjectIR.Core.Ast.OpCode;

namespace Lattice.Runtime.Tests
{
    public class RuntimeTests
    {
        [Fact]
        public void TestThreadedDemoExecution()
        {
            string oirPath = Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", "..", "Lattice.Runtime", "demos", "demo_threads.oir");
            Assert.True(File.Exists(oirPath), $"Test OIR file not found at {Path.GetFullPath(oirPath)}");

            var cpu = new CPU();
            cpu.Scheduler = new Scheduler();
            NativeRegistry.RegisterFromAssembly(typeof(ObjectIR.Stdlib.System.IO).Assembly);

            var module = TextIrParser.ParseModule(File.ReadAllText(oirPath));
            cpu.LoadModule(module);

            var exception = Record.Exception(() => cpu.InitializeMain(new string[] { }));
            Assert.Null(exception);

            cpu.Scheduler.AddThread(cpu);
            bool finished = cpu.Scheduler.Run(30000);
            Assert.True(finished, "Scheduler timed out after 30 seconds");
            Assert.Null(cpu.CurrentFrame);
        }

        [Fact]
        public void TestSimpleMainCompletesViaScheduler()
        {
            var module = new ModuleNode("Test");
            var programClass = new ClassNode("Program");
            var mainMethod = new MethodNode(
                "Main", new List<ParameterNode>(),
                TypeRef.Void, isStatic: true, implements: null,
                body: new BlockStatement(new List<Statement>
                {
                    new InstructionStatement(new SimpleInstruction(AstOpCode.Ret)),
                }));
            programClass.Methods.Add(mainMethod);
            module.Classes.Add(programClass);

            var cpu = new CPU();
            cpu.Scheduler = new Scheduler();
            cpu.LoadModule(module);

            cpu.InitializeMain(new string[] { });
            cpu.Scheduler.AddThread(cpu);
            bool finished = cpu.Scheduler.Run(5000);
            Assert.True(finished, "Scheduler timed out");
            Assert.Null(cpu.CurrentFrame);
        }

        [Fact]
        public void TestSchedulerThreadCount()
        {
            var module = new ModuleNode("Test");
            var programClass = new ClassNode("Program");
            var mainMethod = new MethodNode(
                "Main", new List<ParameterNode>(),
                TypeRef.Void, isStatic: true, implements: null,
                body: new BlockStatement(new List<Statement>
                {
                    new InstructionStatement(new SimpleInstruction(AstOpCode.Ret)),
                }));
            programClass.Methods.Add(mainMethod);
            module.Classes.Add(programClass);

            var cpu = new CPU();
            cpu.Scheduler = new Scheduler();
            cpu.LoadModule(module);

            Assert.Equal(0, cpu.Scheduler.ThreadCount);

            cpu.InitializeMain(new string[] { });
            cpu.Scheduler.AddThread(cpu);
            Assert.Equal(1, cpu.Scheduler.ThreadCount);

            bool finished = cpu.Scheduler.Run(5000);
            Assert.True(finished);
            Assert.Equal(0, cpu.Scheduler.ThreadCount);
        }

        [Fact]
        public void TestMultipleCPUsComplete()
        {
            var module = new ModuleNode("Test");
            var programClass = new ClassNode("Program");

            var mainMethod = new MethodNode(
                "Main", new List<ParameterNode>(),
                TypeRef.Void, isStatic: true, implements: null,
                body: new BlockStatement(new List<Statement>
                {
                    new InstructionStatement(new SimpleInstruction(AstOpCode.LdcI4, "42")),
                    new InstructionStatement(new SimpleInstruction(AstOpCode.Pop)),
                    new InstructionStatement(new SimpleInstruction(AstOpCode.Ret)),
                }));
            programClass.Methods.Add(mainMethod);
            module.Classes.Add(programClass);

            var cpu = new CPU();
            cpu.Scheduler = new Scheduler();
            cpu.LoadModule(module);

            cpu.InitializeMain(new string[] { });
            cpu.Scheduler.AddThread(cpu);
            bool finished = cpu.Scheduler.Run(5000);
            Assert.True(finished, "Scheduler timed out");
            Assert.Null(cpu.CurrentFrame);
        }
    }
}
