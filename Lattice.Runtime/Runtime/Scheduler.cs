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
    private readonly AutoResetEvent _threadEvent = new AutoResetEvent(false);

    public void AddThread(CPU cpu)
    {
        lock (_lock)
        {
            _threads.Add(cpu);
            
            Thread t = new Thread(() => {
                while (cpu.Step())
                {
                    Thread.Yield();
                }
                lock (_lock)
                {
                    _threads.Remove(cpu);
                    if (_threads.Count == 0)
                        _threadEvent.Set();
                }
            });
            t.IsBackground = true;
            t.Start();
        }
    }

    public void Run()
    {
        _threadEvent.WaitOne();
    }
}
}