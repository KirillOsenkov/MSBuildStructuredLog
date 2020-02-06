using System.Diagnostics;
using System.IO;

namespace StructuredLogger.Tests
{
    public class Differ
    {
        public static bool AreDifferent(string file1, string file2)
        {
            var source = File.ReadAllText(file1);
            var destination = File.ReadAllText(file2);
            if (source != destination)
            {
                // Process.Start("devenv", $"/diff \"{Path.GetTestFile(file1)}\" \"{Path.GetTestFile(file2)}\"");
                return true;
            }
            else
            {
                return false;
            }
        }
    }
}
