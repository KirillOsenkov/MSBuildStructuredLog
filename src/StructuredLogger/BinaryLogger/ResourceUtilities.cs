namespace Microsoft.Build.Shared
{
    internal class ResourceUtilities
    {
        internal static string FormatResourceString(out string errorCode, out string helpKeyword, string text, string filePath, string message)
        {
            errorCode = "MSB0001";
            helpKeyword = "";
            return message;
        }

        internal static string FormatResourceStringStripCodeAndKeyword(out string errorCode, out string helpKeyword, string text, string filePath, string message)
        {
            errorCode = "MSB0001";
            helpKeyword = "";
            return message;
        }

        internal static string FormatResourceString(string v1, string v2)
        {
            return v1;
        }

        internal static string FormatResourceStringStripCodeAndKeyword(string v1, string v2)
        {
            return v1;
        }

        internal static string GetResourceString(string s) => s;
    }
}
