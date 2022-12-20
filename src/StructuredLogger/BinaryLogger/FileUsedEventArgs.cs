using System;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.Build.Framework
{
    /// <summary>
    /// Arguments for the response file used event
    /// </summary>
    [Serializable]
    public class FileUsedEventArgs : BuildMessageEventArgs
    {
        public FileUsedEventArgs()
        {
        }

        /// <summary>
        /// Initialize a new instance of the ResponseFileUsedEventArgs class.
        /// </summary>
        public FileUsedEventArgs(string responseFilePath) : base()
        {
            FilePath = responseFilePath;
        }

        public string? FilePath { set; get; }
    }
}
