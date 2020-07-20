using System;

namespace Blazor.FileReader
{
    /// <summary>
    /// Exception that is thrown if an exception occurs in the browser during file reader operations
    /// </summary>
    public class BrowserFileReaderException : Exception
    {
        internal BrowserFileReaderException(string message):base(message)
        {
        }
    }
}
