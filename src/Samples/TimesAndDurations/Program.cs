using System;
using System.IO;
using System.Linq;
using Microsoft.Build.Logging.StructuredLogger;

namespace TimesAndDurations
{
    class Program
    {
        static void Main(string[] args)
        {
            if (args.Length != 1)
            {
                return;
            }

            var logFilePath = args[0];
            if (!File.Exists(logFilePath))
            {
                return;
            }

            var build = Serialization.Read(logFilePath);

            var targets = build
                .FindChildrenRecursive<Target>(t => t.StartTime > DateTime.MinValue && t.EndTime < DateTime.MaxValue)
                .OrderByDescending(t => t.Duration)
                .ToArray();

            foreach (var target in targets.Take(10))
            {
                Console.WriteLine($"Target {target.Name}: {TextUtilities.Display(target.StartTime, displayDate: false)} {TextUtilities.Display(target.EndTime, displayDate: false)} {target.DurationText}");
            }
        }
    }
}
