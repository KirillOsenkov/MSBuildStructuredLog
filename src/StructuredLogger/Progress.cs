using System;
using System.Threading;

namespace Microsoft.Build.Logging.StructuredLogger
{
    public class Progress : IProgress<ProgressUpdate>
    {
        public virtual CancellationToken CancellationToken { get; set; } = CancellationToken.None;

        public event Action<ProgressUpdate> Updated;

        public virtual void Report(double ratio)
        {
            Report(new ProgressUpdate { Ratio = ratio });
        }

        public virtual void Report(ProgressUpdate progressUpdate) 
        {
            Updated?.Invoke(progressUpdate);
        }
    }

    public struct ProgressUpdate
    {
        public double Ratio;
        public int BufferLength;
    }
}
