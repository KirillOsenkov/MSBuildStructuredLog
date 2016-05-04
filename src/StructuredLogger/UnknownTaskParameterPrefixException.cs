using System;

namespace Microsoft.Build.Logging.StructuredLogger
{
    internal class UnknownTaskParameterPrefixException : Exception
    {
        public UnknownTaskParameterPrefixException(string prefix)
            : base(string.Format("Unknown task parameter type: {0}", prefix))
        {
        }
    }
}
