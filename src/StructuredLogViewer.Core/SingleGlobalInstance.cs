using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace StructuredLogViewer.Core
{
    public class SingleGlobalInstance : IDisposable
    {
        public bool HasHandle = false;
        private Mutex mutex;

        public static SingleGlobalInstance Acquire(string mutexName, int millisecondsTimeout = -1)
        {
            return new SingleGlobalInstance(mutexName, millisecondsTimeout);
        }

        private SingleGlobalInstance(string mutexName, int millisecondsTimeout = -1)
        {
            try
            {
                mutex = new Mutex(false, mutexName);
                HasHandle = mutex.WaitOne(millisecondsTimeout);
            }
            catch (AbandonedMutexException)
            {
                HasHandle = true;
            }
        }
 
        public void Dispose()
        {
            if (mutex != null)
            {
                if (HasHandle)
                {
                    mutex.ReleaseMutex();
                }
 
                mutex.Dispose();
                mutex = null;
            }
        }
    }
}
