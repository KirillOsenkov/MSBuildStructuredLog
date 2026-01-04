using System;

namespace StructuredLogger.LLM
{
    /// <summary>
    /// Defines the phases of agent execution. Tool executors can declare which phases they support.
    /// </summary>
    [Flags]
    public enum AgentPhase
    {
        /// <summary>
        /// No phase specified
        /// </summary>
        None = 0,

        /// <summary>
        /// Planning phase - agent is creating a plan or decomposing tasks
        /// </summary>
        Planning = 1 << 0,

        /// <summary>
        /// Research phase - agent is gathering information and context
        /// </summary>
        Research = 1 << 1,

        /// <summary>
        /// Summarization phase - agent is synthesizing results and generating summaries
        /// </summary>
        Summarization = 1 << 2,

        /// <summary>
        /// All phases - tool is applicable in any phase
        /// </summary>
        All = Planning | Research | Summarization
    }
}
