using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using TPLTask = System.Threading.Tasks.Task;

namespace Microsoft.Build.Logging.StructuredLogger
{
    public static class Utilities
    {
        public static int BinarySearch<T, C>(this IList<T> list, C item, Func<T, C> comparableSelector)
            where C : IComparable<C>
        {
            int count = list.Count;
            int lo = 0;
            int hi = count - 1;

            while (lo <= hi)
            {
                int i = lo + ((hi - lo) >> 1);
                var comparable = comparableSelector(list[i]);
                int order = comparable.CompareTo(item);

                if (order == 0)
                {
                    return i;
                }

                if (order < 0)
                {
                    lo = i + 1;
                }
                else
                {
                    hi = i - 1;
                }
            }

            return ~lo;
        }
    }

    public class BatchBlockingCollection<T>
    {
        private readonly Rental<Batch> rental;
        private Batch currentBatch;
        private BlockingCollection<Batch> queue;
        private int BatchSize;

        public BatchBlockingCollection() : this(8192, 0)
        {
        }

        public event Action<T> ProcessItem;

        public TPLTask Completion;

        public BatchBlockingCollection(int batchSize = 8192, int boundedCapacity = 0)
        {
            BatchSize = batchSize;
            rental = new Rental<Batch>(() => new(batchSize));
            currentBatch = rental.Get();

            if (boundedCapacity == 0)
            {
                queue = new BlockingCollection<Batch>();
            }
            else
            {
                queue = new BlockingCollection<Batch>(boundedCapacity);
            }

            Completion = TPLTask.Run(() =>
            {
                foreach (var batch in queue.GetConsumingEnumerable())
                {
                    foreach (var item in batch)
                    {
                        ProcessItem?.Invoke(item);
                    }

                    batch.Clear();
                    rental.Return(batch);
                }
            });
        }

        public int Count => currentBatch.Count + queue.Count * BatchSize;

        public void Add(T item)
        {
            if (currentBatch.Count < BatchSize)
            {
                currentBatch.Add(item);
            }
            else
            {
                queue.Add(currentBatch);
                currentBatch = rental.Get();
                currentBatch.Add(item);
            }
        }

        public void CompleteAdding()
        {
            queue.Add(currentBatch);
            queue.CompleteAdding();
        }

        public class Batch : List<T>
        {
            public Batch(int capacity) : base(capacity)
            {
            }
        }
    }

    public class Rental<T>
    {
        private readonly Queue<T> queue = new Queue<T>();

        private Func<T> Factory;

        public Rental(Func<T> factory)
        {
            Factory = factory;
        }

        public T Get()
        {
            lock (queue)
            {
                if (queue.Count > 0)
                {
                    return queue.Dequeue();
                }

                return Factory();
            }
        }

        public void Return(T item)
        {
            lock (queue)
            {
                queue.Enqueue(item);
            }
        }
    }

    public class ChunkedList<T>
    {
        public int ChunkSize { get; }

        private List<List<T>> chunks = new List<List<T>>();

        public ChunkedList() : this(1048576)
        {
        }

        public ChunkedList(int chunkSize)
        {
            ChunkSize = chunkSize;
        }

        public void Add(T item)
        {
            AddChunk();
            List<T> chunk = chunks[chunks.Count - 1];
            chunk.Add(item);
        }

        private void AddChunk()
        {
            if (chunks.Count == 0 || chunks[chunks.Count - 1].Count >= ChunkSize)
            {
                chunks.Add(new List<T>(ChunkSize));
            }
        }

        public int Count
        {
            get
            {
                int count = 0;
                foreach (var chunk in chunks)
                {
                    count += chunk.Count;
                }

                return count;
            }
        }

        public IList<List<T>> Chunks => chunks;

        public override string ToString()
        {
            return $"Count = {Count}";
        }
    }
}