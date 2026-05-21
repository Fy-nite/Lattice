using ObjectIR.Core.AST;
using System;
using System.Collections.Generic;
using System.Text;
using Xunit;

namespace Lattice.ObjectIR.Tests
{
    public class Helpers
    {
        // -------------------------
        // Helpers
        // -------------------------

        public static ClassNode GetClass(ModuleNode module, string name)
            => Assert.Single(module.Classes.Where(x => x.Name == name));

        public static MethodNode GetMethod(ClassNode clazz, string name)
            => Assert.Single(clazz.Methods.Where(x => x.Name == name));

        public static List<Instruction> GetInstructions(MethodNode method)
            => method.Body.Statements
                .OfType<InstructionStatement>()
                .Select(x => x.Instruction)
                .ToList();

        public static SimpleInstruction? FindSimpleInstruction(
    MethodNode method,
    string opcode,
    string? operand = null)
        {
            return GetInstructions(method)
                .OfType<SimpleInstruction>()
                .FirstOrDefault(x =>
                    string.Equals(
                        x.OpCode,
                        opcode,
                        StringComparison.OrdinalIgnoreCase)
                    &&
                    (operand == null || x.Operand == operand));
        }


        public static CallInstruction? FindCall(
            MethodNode method,
            string targetName)
        {
            return GetInstructions(method)
                .OfType<CallInstruction>()
                .FirstOrDefault(x => x.Target.Name == targetName);
        }
    }
}
