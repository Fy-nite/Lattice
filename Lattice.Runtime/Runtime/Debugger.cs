using System;
using System.Linq;
using lattice.Core;
using ObjectIR.Core.AST;
using System.Collections.Generic;

namespace lattice.Runtime.Debugging
{
    public class Debugger
    {
        private bool _continue = false;
        private int _stepCount = 0;
        private Dictionary<string, object> _watches = new();

        public void Step(CPU cpu, Statement instruction)
        {
            if (_stepCount > 0)
            {
                _stepCount--;
                return;
            }

            if (!_continue)
            {
                DisplayDebuggerStatus(cpu, instruction);

                while (true)
                {
                    Console.Write("\nCommands: (s)tep, (n)ext [steps], (c)ontinue, (i)nspect [var], (w)atch [var], (p)rint context: ");
                    var input = Console.ReadLine()?.Split(' ');
                    var cmd = input?[0];

                    if (cmd == "s") break;
                    if (cmd == "n")
                    {
                        if (input?.Length > 1 && int.TryParse(input[1], out int steps)) _stepCount = steps - 1;
                        break;
                    }
                    if (cmd == "c")
                    {
                        _continue = true;
                        break;
                    }
                    if (cmd == "i")
                    {
                        if (input?.Length > 1)
                        {
                            var varName = input[1];
                            var val = cpu.CurrentFrame?.Locals.GetValueOrDefault(varName);
                            Console.WriteLine($"  {varName} = {val ?? "null"}");
                        }
                    }
                    if (cmd == "w")
                    {
                        if (input?.Length > 1)
                        {
                            var varName = input[1];
                            _watches[varName] = cpu.CurrentFrame?.Locals.GetValueOrDefault(varName);
                            Console.WriteLine($"  Watching {varName}");
                        }
                    }
                    if (cmd == "p")
                    {
                        DisplayDebuggerStatus(cpu, instruction);
                    }
                }
            }
            CheckWatches(cpu);
        }

        private void DisplayDebuggerStatus(CPU cpu, Statement instruction)
        {
            Console.WriteLine("\n--- Lattice Debugger ---");
            if (instruction.Location != null)
            {
                var info = instruction.Location;
                Console.WriteLine($"Source: {info.Line.ToString() ?? "unknown"}:{info.Line}");
            }
            Console.WriteLine($"Next: {instruction}");
            
            Console.WriteLine("\nStack:");
            foreach (var item in cpu.CurrentFrame?.EvaluationStack.Reverse() ?? Enumerable.Empty<object>())
            {
                Console.WriteLine($"  {item}");
            }

            Console.WriteLine("\nLocals:");
            foreach (var kv in cpu.CurrentFrame?.Locals ?? new Dictionary<string, object>())
            {
                Console.WriteLine($"  {kv.Key}: {kv.Value}");
            }
        }

        private void CheckWatches(CPU cpu)
        {
            foreach (var watch in _watches)
            {
                var currentVal = cpu.CurrentFrame?.Locals.GetValueOrDefault(watch.Key);
                if (currentVal != watch.Value)
                {
                    Console.WriteLine($"\n[Watch] {watch.Key} changed: {watch.Value} -> {currentVal}");
                    _watches[watch.Key] = currentVal;
                }
            }
        }
    }
}
