using System.Reflection;

namespace StructuredLogger.Tests
{
    public class TestUtilities
    {
        public static string GetTestFile(string fileName)
        {
            return Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), fileName);
        }
    }
}
