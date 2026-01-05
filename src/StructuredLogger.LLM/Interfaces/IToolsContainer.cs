using Microsoft.Extensions.AI;
using System.Collections.Generic;

namespace StructuredLogger.LLM
{
    /// <summary>
    /// Defines a container for AI tools that can be registered with chat services.
    /// Tools containers expose a collection of delegates that can be invoked by the LLM.
    /// </summary>
    public interface IToolsContainer
    {
        /// <summary>
        /// Gets whether this container provides GUI manipulation tools.
        /// GUI manipulation tools are those that can interact with the UI to show, highlight, or navigate to specific content.
        /// </summary>
        bool HasGuiTools { get; }

        /// <summary>
        /// Gets all tools provided by this container with their applicable phases.
        /// </summary>
        /// <returns>Enumerable of tuples containing AIFunction and the phases where it's applicable.</returns>
        IEnumerable<(AIFunction Function, AgentPhase ApplicablePhases)> GetTools();
    }
}
