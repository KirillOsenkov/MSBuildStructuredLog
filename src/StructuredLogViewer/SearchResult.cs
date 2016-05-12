namespace StructuredLogViewer
{
    public class SearchResult
    {
        public object Node { get; set; }

        public string Field { get; set; }
        public string Word { get; set; }
        public int Index { get; set; }

        public string Before { get; set; }
        public string Highlighted { get; set; }
        public string After { get; set; }

        public void AddMatch(string field, string word, int index)
        {
            if (Field != null && Word != null)
            {
                return;
            }

            Field = field;
            Word = word;
            Index = index;

            Before = field.Substring(0, index);
            Highlighted = field.Substring(index, word.Length);
            After = field.Substring(index + word.Length, field.Length - index - word.Length);
        }
    }
}
