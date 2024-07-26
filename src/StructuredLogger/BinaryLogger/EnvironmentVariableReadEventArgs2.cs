using Microsoft.Build.Framework;

namespace StructuredLogger.BinaryLogger
{
    public class EnvironmentVariableReadEventArgs2 : EnvironmentVariableReadEventArgs
    {
        /// <summary>
        /// Initializes an instance of the EnvironmentVariableReadEventArgs class.
        /// </summary>
        /// <param name="environmentVarName">The name of the environment variable that was read.</param>
        /// <param name="environmentVarValue">The value of the environment variable that was read.</param>
        /// <param name="file">file associated with the event</param>
        /// <param name="line">line number (0 if not applicable)</param>
        /// <param name="column">column number (0 if not applicable)</param>
        public EnvironmentVariableReadEventArgs2(
            string environmentVarName,
            string environmentVarValue,
            string file,
            int line,
            int column)
            : base(environmentVarName, environmentVarValue)
        {
            LineNumber = line;
            ColumnNumber = column;
            File = file;
        }

        // the properties in the base class have an internal setter
        public new int LineNumber { get; set; }
        public new int ColumnNumber { get; set; }
        public new string File { get; set; }
    }
}
