using System;
using System.Collections.Generic;
using ObjectIR.Core.IR;
using ObjectIR.Core.Serialization;

namespace IRSerializerTest
{
    class Program
    {
        static void Main(string[] args)
        {
            var method = new MethodDefinition("Test", TypeReference.Void);
            method.Instructions.Add(new LoadConstantInstruction("hello", TypeReference.String));
            var methodRef = new MethodReference(TypeReference.FromName("System.Console"), "WriteLine", TypeReference.Void, new List<TypeReference> { TypeReference.String });
            method.Instructions.Add(new CallInstruction(methodRef));

            var json = InstructionSerializer.SerializeInstructions(method.Instructions).GetRawText();
            Console.WriteLine(json);

            // Now try loading the demo IR file to reproduce the original behavior
            try
            {
                var loader = new ModuleLoader();
                var path = @"d:\git\Lattice\lattice.core\demos\test.oir";
                var text = System.IO.File.ReadAllText(path);
                var module = loader.LoadFromText(text);
                var ser = new ModuleSerializer(module);
                    foreach (var t in module.Types)
                    {
                        if (t is ObjectIR.Core.IR.ClassDefinition cd)
                        {
                            foreach (var m in cd.Methods)
                            {
                                Console.WriteLine($"DEBUG: Method '{m.Name}' return type -> '{m.ReturnType.GetQualifiedName()}'");
                            }
                        }
                    }
                Console.WriteLine("--- DumpToIRCode ---");
                Console.WriteLine(ser.DumpToIRCode());
            }
            catch (Exception ex)
            {
                Console.WriteLine("Exception while loading/dumping demo IR:");
                Console.WriteLine(ex.ToString());
            }
        }
    }
}
