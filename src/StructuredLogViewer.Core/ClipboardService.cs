namespace StructuredLogViewer
{
    public static class ClipboardService
    {
        public static void SetText(string text)
        {
            Set?.Invoke(text);
        }

        public static event Action<string> Set;
    }
}
