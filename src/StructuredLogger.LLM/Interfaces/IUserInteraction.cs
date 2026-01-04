using System.Threading.Tasks;

namespace StructuredLogger.LLM
{
    /// <summary>
    /// Interface for user interaction, allowing LLMs to ask clarifying questions.
    /// Implementations can use Console, GUI, or other mechanisms.
    /// </summary>
    public interface IUserInteraction
    {
        /// <summary>
        /// Asks the user a question and returns their response.
        /// </summary>
        /// <param name="question">The question to ask the user.</param>
        /// <param name="options">Optional list of suggested options. If provided, shows as numbered choices.</param>
        /// <returns>User's response as a string.</returns>
        Task<string> AskUser(string question, string[]? options = null);
    }
}
