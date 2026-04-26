using System.Collections.Generic;
using System.Linq;
using ObjectIR.Core.AST;
using ObjectIR.Core.Builder;
using Xunit;

namespace Lattice.ObjectIR.Tests
{
    public class HelloWorldTests
    {
        private ModuleNode ConstructHelloWorld()
        {
            return new IRBuilder("HelloWorld")
                .Class("Program")
                    .Method("Main", TypeRef.Void)
                        .Static()
                        .Body()
                            .Ldstr("Hello world")
                            .Call(new MethodReference(
                                new TypeRef("IO"),
                                "WriteLine",
                                TypeRef.Void,
                                new List<TypeRef> { TypeRef.String }))
                        .EndBody()
                    .EndMethod()
                .EndClass()
                .Build();
        }

   

        // -------------------------
        // Tests
        // -------------------------

        [Fact]
        public void ModuleContainsProgramClass()
        {
            var module = ConstructHelloWorld();

            var clazz = Helpers.GetClass(module, "Program");

            Assert.Equal("Program", clazz.Name);
        }

        [Fact]
        public void ProgramContainsMainMethod()
        {
            var module = ConstructHelloWorld();

            var method = Helpers.GetMethod(Helpers.GetClass(module, "Program"), "Main");

            Assert.Equal(TypeRef.Void, method.ReturnType);
            Assert.True(method.IsStatic);
        }

        [Fact]
        public void MainContainsLdstrInstruction()
        {
            var method = Helpers.GetMethod(
                Helpers.GetClass(ConstructHelloWorld(), "Program"),
                "Main");

            var instr = Helpers.FindSimpleInstruction(
                method,
                "Ldstr",
                "Hello world");

            Assert.NotNull(instr);
        }
        [Fact]
        public void DebugInstructions()
        {
            var method = Helpers.GetMethod(
                Helpers.GetClass(ConstructHelloWorld(), "Program"),
                "Main");

            var instructions = Helpers.GetInstructions(method);

            foreach (var i in instructions)
            {
                Console.WriteLine(i);
            }
        }
        [Fact]
        public void MainContainsWriteLineCall()
        {
            var method = Helpers.GetMethod(
                Helpers.GetClass(ConstructHelloWorld(), "Program"),
                "Main");

            var call = Helpers.FindCall(method, "WriteLine");

            Assert.NotNull(call);
            Assert.Equal("IO", call!.Target.DeclaringType.Name);
        }

        [Fact]
        public void MainHasTwoInstructions()
        {
            var method = Helpers.GetMethod(
                Helpers.GetClass(ConstructHelloWorld(), "Program"),
                "Main");

            Assert.Equal(2, Helpers.GetInstructions(method).Count);
        }
    }
}
