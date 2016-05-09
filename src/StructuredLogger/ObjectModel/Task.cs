namespace Microsoft.Build.Logging.StructuredLogger
{
    public class Task : TimedNode
    {
        public string FromAssembly { get; set; }
        public string CommandLineArguments { get; set; }

        public override string ToString()
        {
            return $"Task: Id={Id} Name={Name}";
        }
    }
}
