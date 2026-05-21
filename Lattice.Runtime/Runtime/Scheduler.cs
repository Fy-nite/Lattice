using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
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
                    try
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
                    catch (Exception ex)
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine($"[SCHEDULER ERROR] Thread crashed: {ex.Message}");
                        Console.ResetColor();
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
    }
}