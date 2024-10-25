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

        /// <summary>
        /// Node in the tree where the copy operation happened
        /// (usually the Message under a Copy task)
        /// </summary>
        public TreeNode Node { get; set; }

        public override string ToString()
        {
            return $"{Source} ➔ {Destination}";
        }
    }
}
