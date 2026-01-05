using System;
using System.Collections.Generic;
using System.ComponentModel;
using Microsoft.Extensions.AI;

namespace StructuredLogger.LLM
{
    /// <summary>
    /// Tool executor that allows the LLM to ask clarifying questions to the user.
    /// Should be used sparingly and only when absolutely necessary to resolve ambiguous requirements.
    /// </summary>
    public class AskUserToolExecutor : IToolsContainer
    {
        private readonly IUserInteraction userInteraction;

        public AskUserToolExecutor(IUserInteraction userInteraction)
        {
            this.userInteraction = userInteraction ?? throw new ArgumentNullException(nameof(userInteraction));
        }

        public bool HasGuiTools => false;

        public IEnumerable<(AIFunction Function, AgentPhase ApplicablePhases)> GetTools()
        {
            // AskUser is available in all phases, but should be used judiciously
            yield return (AIFunctionFactory.Create(AskUserAsync), AgentPhase.All);
        }

        [Description(@"Asks the user a clarifying question when requirements are genuinely unclear or ambiguous.

IMPORTANT GUIDELINES:
- Use SPARINGLY and only when absolutely necessary
- Try to infer reasonable defaults before asking
- Provide sensible default options when possible
- Combine multiple related questions into one if feasible
- Avoid asking for information that can be determined from the build data

WHEN TO USE:
- Truly ambiguous requirements (e.g., user says 'analyze the issue' but multiple distinct issues exist)
- User provides unclear references (e.g., 'that project' when multiple projects match)
- Conflicting or contradictory instructions
- Missing critical information that cannot be inferred

WHEN NOT TO USE:
- Information available in the build log
- Standard interpretations (e.g., 'build time' means total duration)
- Common defaults (e.g., 'errors' means all errors unless specified)

EXAMPLES OF GOOD USE:
- User asks about 'the slow project' but 5 projects have similar durations
- User says 'fix the configuration' but multiple configuration issues exist
- User requests analysis of 'the problematic target' when several targets failed

EXAMPLES OF BAD USE:
- Asking which project when user clearly stated the name
- Asking about format preferences for standard output
- Requesting clarification on well-defined terms")]
        public async System.Threading.Tasks.Task<string> AskUserAsync(
            [Description("The question to ask the user. Be clear, specific, and provide context.")] string question,
            [Description("Optional array of default options to present to the user as numbered choices (e.g., ['Option 1', 'Option 2']). Leave null if asking an open-ended question.")] string[]? options = null)
        {
            try
            {
                var response = await userInteraction.AskUser(question, options);
                return $"User responded: {response}";
            }
            catch (Exception ex)
            {
                return $"Error: Unable to get user input - {ex.Message}";
            }
        }
    }
}
