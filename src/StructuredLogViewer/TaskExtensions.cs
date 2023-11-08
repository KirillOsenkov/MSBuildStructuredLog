using System.Threading.Tasks;

namespace StructuredLogViewer
{
    internal static class TaskExtensions
    {
        /// <summary>
        /// Consumes a task and doesn't do anything with it.  Useful for fire-and-forget calls to async methods within sync methods.
        /// </summary>
        /// <param name="task">The task whose result is to be ignored.</param>
        public static void Ignore(this Task task)
        { /* this is it */ }
    }
}
