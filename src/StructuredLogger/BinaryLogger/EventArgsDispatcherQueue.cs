using System;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using SystemTask = System.Threading.Tasks.Task;

namespace Microsoft.Build.Logging.StructuredLogger;

internal class EventArgsDispatcherQueue<TEventArgs> : EventArgs
{
    private readonly Action<TEventArgs> dispatch;
    private readonly BlockingCollection<TEventArgs> queue;
    private readonly SystemTask processingTask;

    public EventArgsDispatcherQueue(Action<TEventArgs> dispatch)
    {
        this.dispatch = dispatch;
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Create("BROWSER")))
        {
            // Use a producer-consumer queue so that IO can happen on one thread
            // while processing can happen on another thread decoupled. The speed
            // up is from 4.65 to 4.15 seconds.
            queue = new BlockingCollection<TEventArgs>(boundedCapacity: 5000);
            processingTask = SystemTask.Run(() =>
            {
                foreach (var args in queue.GetConsumingEnumerable())
                {
                    dispatch(args);
                }
            });
        }
    }

    public void CompleteAdding()
    {
        queue?.CompleteAdding();
    }

    public void Add(TEventArgs args)
    {
        if (queue is not null)
        {
            queue.Add(args);
        }
        else
        {
            dispatch(args);
        }
    }

    public void Wait()
    {
        processingTask?.Wait();
    }
}
