namespace Microsoft.Build.Logging.StructuredLogger
{
    public class FileCopyOperation
    {
        public string Source { get; set; }
        public string Destination { get; set; }

        /// <summary>
        /// We need to represent both "Copied" and "Did not copy" cases
        /// </summary>
        public bool Copied { get; set; }

        public Message Message { get; set; }

        public override string ToString()
        {
            return $"{Source} ➔ {Destination}";
        }
    }
}
