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
                // Console.WriteLine($"[SCHEDULER] Adding thread, count is now {_threads.Count + 1}");
                _threads.Add(cpu);
                
                // Spawn a dedicated native thread for this CPU
                Thread t = new Thread(() => {
                    while (cpu.Step())
                    {
                        // Small sleep to prevent 100% CPU usage
                        Thread.Sleep(1);
                    }
                    // Console.WriteLine("[SCHEDULER] Thread finished.");
                    lock (_lock)
                    {
                        _threads.Remove(cpu);
                    }
                });
                t.IsBackground = true;
                t.Start();
            }
        }

        public void Run()
        {
            // Console.WriteLine("[SCHEDULER] Starting...");
            // The scheduler now just waits for all threads to finish
            while (true)
            {
                lock (_lock)
                {
                    if (_threads.Count == 0) break;
                }
                Thread.Sleep(100);
            }
        }
    }
}