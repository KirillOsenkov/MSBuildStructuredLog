namespace StructuredLogViewer
{
    public class Classifier
    {
        public static bool LooksLikeXml(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return false;
            }

            if (text.Length > 10)
            {
                if (text.StartsWith("<?xml"))
                {
                    return true;
                }
                else
                {
                    for (int i = 0; i < text.Length; i++)
                    {
                        var ch = text[i];
                        if (ch == '<')
                        {
                            for (int j = text.Length - 1; j >= 0; j--)
                            {
                                ch = text[j];

                                if (ch == '>')
                                {
                                    return true;
                                }
                                else if (!char.IsWhiteSpace(ch))
                                {
                                    return false;
                                }
                            }

                            return false;
                        }
                        else if (!char.IsWhiteSpace(ch))
                        {
                            return false;
                        }
                    }
                }
            }

            return false;
        }
    }
}
