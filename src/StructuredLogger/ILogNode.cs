using System.Xml.Linq;

namespace Microsoft.Build.Logging.StructuredLogger
{
    /// <summary>
    /// Interface class for an execution MSBuild log node to be represented in XML
    /// </summary>
    public interface ILogNode
    {
        /// <summary>
        /// Writes the node to XML XElement representation.
        /// </summary>
        /// <param name="parentElement">The parent element.</param>
        void SaveToElement(XElement parentElement);
    }
}
