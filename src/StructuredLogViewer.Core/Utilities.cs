﻿using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace StructuredLogViewer.Core
{
    public static class Utilities
    {
        public static string QuoteIfNeeded(this string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return text;
            }

            if (text.Contains(" "))
            {
                text = "\"" + text + "\"";
            }

            return text;
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
    }
}
