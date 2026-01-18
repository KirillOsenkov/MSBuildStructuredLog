using System;
using System.Threading.Tasks;
using StructuredLogger.LLM;

namespace BinlogTool
{
    /// <summary>
    /// Console-based implementation of IUserInteraction for BinlogTool CLI.
    /// </summary>
    public class ConsoleUserInteraction : IUserInteraction
    {
        public Task<string> AskUser(string question, string[]? options = null)
        {
            Console.WriteLine();
            Console.WriteLine("=== User Input Required ===");
            Console.WriteLine(question);

            if (options != null && options.Length > 0)
            {
                Console.WriteLine();
                Console.WriteLine("Available options:");
                for (int i = 0; i < options.Length; i++)
                {
                    Console.WriteLine($"  {i + 1}. {options[i]}");
                }
                Console.WriteLine();
                Console.Write("Enter your choice (number) or custom response: ");
            }
            else
            {
                Console.WriteLine();
                Console.Write("Your response: ");
            }

            var response = Console.ReadLine() ?? string.Empty;

            // If options were provided and user entered a number, return the corresponding option
            if (options != null && options.Length > 0 && int.TryParse(response.Trim(), out int choice))
            {
                if (choice >= 1 && choice <= options.Length)
                {
                    return Task.FromResult(options[choice - 1]);
                }
            }

            return Task.FromResult(response);
        }
    }
}
