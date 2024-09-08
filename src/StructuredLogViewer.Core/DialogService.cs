namespace StructuredLogViewer
{
    public static class DialogService
    {
        public static void ShowMessageBox(string message)
        {
            ShowMessageBoxEvent?.Invoke(message);
        }

        public static event Action<string> ShowMessageBoxEvent;
    }
}
