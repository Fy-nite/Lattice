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
        private readonly ManualResetEventSlim _allDone = new ManualResetEventSlim(false);

        public int ThreadCount
        {
            get { lock (_lock) { return _threads.Count; } }
        }

        public void AddThread(CPU cpu)
        {
            lock (_lock)
            {
                _threads.Add(cpu);
                _allDone.Reset();
            }

            Thread t = new Thread(() =>
            {
                Exception? threadException = null;
                try
                {
                    while (cpu.Step())
                    {
                        Thread.Yield();
                    }
                }
                catch (Exception ex)
                {
                    threadException = ex;
                }
                finally
                {
                    lock (_lock)
                    {
                        _threads.Remove(cpu);
                        if (_threads.Count == 0)
                            _allDone.Set();
                    }

                    if (threadException != null)
                        Console.Error.WriteLine($"[Scheduler] Thread crashed: {threadException}");
                }
            });
            t.IsBackground = true;
            t.Start();
        }

        public void Run()
        {
            _allDone.Wait();
        }

        public bool Run(int timeoutMs)
        {
            return _allDone.Wait(timeoutMs);
        }

        public bool Run(TimeSpan timeout)
        {
            return _allDone.Wait(timeout);
        }
    }
}
