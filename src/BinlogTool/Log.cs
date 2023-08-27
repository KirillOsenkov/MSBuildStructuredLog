using System;

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