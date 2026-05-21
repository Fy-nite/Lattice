using System;
using System.Collections.Generic;
using System.Linq;
using ObjectIR.Core.AST;

namespace lattice.Runtime
{
    public class Scheduler
    {
        private readonly List<CPU> _threads = new List<CPU>();
        private readonly object _lock = new object();

        public void AddThread(CPU cpu)
        {
            lock (_lock)
            {
                _threads.Add(cpu);
            }
        }

        public void Run()
        {
            while (true)
            {
                List<CPU> toStep;
                lock (_lock)
                {
                    if (_threads.Count == 0) break;
                    toStep = _threads.ToList();
                }

                foreach (var cpu in toStep)
                {
                    bool canContinue = cpu.Step();
                    if (!canContinue)
                    {
                        lock (_lock)
                        {
                            _threads.Remove(cpu);
                        }
                    }
                }

                // Small sleep to prevent 100% CPU usage if all threads are yielding/waiting
             
                Thread.Sleep(1);
            }
        }

        /// <summary>
        /// Spawns a new thread (CPU instance) sharing the same program/modules.
        /// </summary>
        public CPU Spawn(CPU parent, MethodReference entryPoint)
        {
            var newCpu = new CPU
            {
                program = parent.program,
                Modules = parent.Modules,
                Debug = parent.Debug,
                MaxStackDepth = parent.MaxStackDepth
            };

            // We need to resolve the method and start it in the new CPU
            // This might require a specialized ExecuteMethod that doesn't run the loop immediately
            // or just rely on the Scheduler to pick it up via Step()
            
            // For now, let's assume we can resolve the method
            // Actually, we need to find the MethodNode
            // We'll use the parent's resolve logic or a shared resolver
            
            // This is a bit simplified, but demonstrates the concept
            return newCpu;
        }
    }
}