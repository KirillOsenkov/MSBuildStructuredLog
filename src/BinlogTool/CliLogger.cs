using System;

namespace BinlogTool
{
    /// <summary>
    /// Logging abstraction for CLI output with verbosity levels.
    /// </summary>
    public class CliLogger
    {
        public enum Verbosity
        {
            Quiet,    // Only final output and errors
            Normal,   // Normal output (default)
            Verbose   // All details including debug info
        }

        private readonly Verbosity level;

        public CliLogger(Verbosity level = Verbosity.Normal)
        {
            this.level = level;
        }

        public bool IsQuiet => level == Verbosity.Quiet;
        public bool IsNormal => level == Verbosity.Normal;
        public bool IsVerbose => level == Verbosity.Verbose;

        public void LogSystem(string message)
        {
            if (level >= Verbosity.Normal)
            {
                WriteColored("[SYSTEM] ", ConsoleColor.Gray);
                Console.WriteLine(message);
            }
        }

        public void LogTool(string message)
        {
            if (level >= Verbosity.Normal)
            {
                WriteColored("[TOOL] ", ConsoleColor.Cyan);
                Console.WriteLine(message);
            }
        }

        public void LogAgent(string message)
        {
            if (level >= Verbosity.Normal)
            {
                WriteColored("[AGENT] ", ConsoleColor.Yellow);
                Console.WriteLine(message);
            }
        }

        public void LogRetry(string message)
        {
            if (level >= Verbosity.Normal)
            {
                WriteColored("[RETRY] ", ConsoleColor.Magenta);
                Console.WriteLine(message);
            }
        }

        public void LogError(string message)
        {
            WriteColored("[ERROR] ", ConsoleColor.Red);
            Console.Error.WriteLine(message);
        }

        public void LogWarning(string message)
        {
            if (level >= Verbosity.Normal)
            {
                WriteColored("[WARNING] ", ConsoleColor.DarkYellow);
                Console.WriteLine(message);
            }
        }

        public void LogResponse(string message)
        {
            Console.WriteLine(message);
        }

        public void LogVerbose(string message)
        {
            if (level >= Verbosity.Verbose)
            {
                WriteColored("[VERBOSE] ", ConsoleColor.DarkGray);
                Console.WriteLine(message);
            }
        }

        public void LogInfo(string message)
        {
            if (level >= Verbosity.Normal)
            {
                Console.WriteLine(message);
            }
        }

        private void WriteColored(string text, ConsoleColor color)
        {
            var oldColor = Console.ForegroundColor;
            Console.ForegroundColor = color;
            Console.Write(text);
            Console.ForegroundColor = oldColor;
        }
    }
}
