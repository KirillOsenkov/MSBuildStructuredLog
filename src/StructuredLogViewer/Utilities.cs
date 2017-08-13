using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace StructuredLogViewer
{
    public class Utilities
    {
        public static string DisplayDuration(TimeSpan span)
        {
            if (span.TotalMilliseconds < 1)
            {
                return "";
            }

            if (span.TotalSeconds > 3600)
            {
                return span.ToString(@"h\:mm\:ss");
            }

            if (span.TotalSeconds > 60)
            {
                return span.ToString(@"m\:ss\.fff");
            }

            if (span.TotalMilliseconds > 1000)
            {
                return span.ToString(@"s\.fff") + " s";
            }

            return span.Milliseconds + " ms";
        }

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

        public static string Display(DateTime time)
        {
            return time.ToString("HH:mm:ss.fff");
        }

        public static string GetSHA1HashOfFileContents(string filePath)
        {
            using (var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            using (var hash = new SHA1Managed())
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
    }
}
