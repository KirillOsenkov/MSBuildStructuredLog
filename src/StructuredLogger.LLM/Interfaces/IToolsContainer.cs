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
        /// Gets all tools provided by this container with their applicable phases.
        /// </summary>
        /// <returns>Enumerable of tuples containing AIFunction and the phases where it's applicable.</returns>
        IEnumerable<(AIFunction Function, AgentPhase ApplicablePhases)> GetTools();
    }
}
