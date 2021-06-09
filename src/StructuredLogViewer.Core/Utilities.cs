using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace StructuredLogViewer
{
    public static class Utilities
    {
        public static string ByteArrayToHexString(byte[] bytes, int digits = 0)
        {
            if (digits == 0)
            {
                digits = bytes.Length * 2;
            }

            char[] c = new char[digits];
            byte b;
            for (int i = 0; i < digits / 2; i++)
            {
                b = ((byte)(bytes[i] >> 4));
                c[i * 2] = (char)(b > 9 ? b + 87 : b + 0x30);
                b = ((byte)(bytes[i] & 0xF));
                c[i * 2 + 1] = (char)(b > 9 ? b + 87 : b + 0x30);
            }

            return new string(c);
        }

        public static string GetSHA1HashOfFileContents(string filePath)
        {
            using (var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            using (var hash = SHA1.Create())
            {
                var result = hash.ComputeHash(stream);
                return ByteArrayToHexString(result);
            }
        }

        public static string GetMD5Hash(string input, int digits)
        {
            using (var md5 = MD5.Create())
            {
                var bytes = Encoding.UTF8.GetBytes(input);
                var hashBytes = md5.ComputeHash(bytes);
                return ByteArrayToHexString(hashBytes, digits);
            }
        }

        public static string InsertMissingDriveSeparator(string sourceFilePath)
        {
            // the zip archive has the ':' stripped from paths
            // try to restore it to match the original path
            var preprocessableFilePath = sourceFilePath;
            if (preprocessableFilePath.Length > 3 && preprocessableFilePath[1] == '\\')
            {
                preprocessableFilePath = preprocessableFilePath.Insert(1, ":");
            }

            return preprocessableFilePath;
        }

        public static bool LooksLikeXml(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
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
