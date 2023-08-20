using System;
using Microsoft.Build.Logging.StructuredLogger;

namespace BinlogTool
{
    public class Log
    {
        private static readonly object consoleLock = new object();

        public static bool Quiet { get; internal set; }

        public static void Write(string message, ConsoleColor color = ConsoleColor.Gray)
        {
            if (Quiet)
            {
                return;
            }
            if (!PlatformUtilities.HasColor)
            {
                Console.Write(message);
                return;
            }

            lock (consoleLock)
            {
                var oldColor = Console.ForegroundColor;
                Console.ForegroundColor = color;
                Console.Write(message);
                if (color != oldColor)
                {
                    Console.ForegroundColor = oldColor;
                }
            }
        }

        public static void WriteLine(string message = "", ConsoleColor color = ConsoleColor.Gray)
        {
            if (Quiet)
            {
                return;
            }
            if (!PlatformUtilities.HasColor)
            {
                Console.WriteLine(message);
                return;
            }

            lock (consoleLock)
            {
                var oldColor = Console.ForegroundColor;
                Console.ForegroundColor = color;
                Console.WriteLine(message);
                if (color != oldColor)
                {
                    Console.ForegroundColor = oldColor;
                }
            }
        }

        public static void WriteError(string message)
        {
            if (!PlatformUtilities.HasColor)
            {
                Console.Error.WriteLine(message);
                return;
            }

            lock (consoleLock)
            {
                var oldColor = Console.ForegroundColor;
                Console.ForegroundColor = ConsoleColor.Red;
                Console.Error.WriteLine(message);
                if (oldColor != ConsoleColor.Red)
                {
                    Console.ForegroundColor = oldColor;
                }
            }
        }
    }
}
