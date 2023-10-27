﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Buffers;
using System.Diagnostics;
using System.IO;
using Microsoft.Build.Shared;
using StructuredLogger.BinaryLogger.Postprocessing;

namespace Microsoft.Build.Logging
{
    internal static class StreamExtensions
    {
        private static bool CheckIsSkipNeeded(long bytesCount)
        {
            if(bytesCount is < 0 or > int.MaxValue)
            {
                throw new ArgumentOutOfRangeException(nameof(bytesCount), $"Attempt to skip {bytesCount} bytes, only non-negative offset up to int.MaxValue is allowed.");
            }

            return bytesCount > 0;
        }

        public static long SkipBytes(this Stream stream)
            => SkipBytes(stream, stream.Length, true);

        public static long SkipBytes(this Stream stream, long bytesCount)
            => SkipBytes(stream, bytesCount, true);

        public static long SkipBytes(this Stream stream, long bytesCount, bool throwOnEndOfStream)
        {
            if (!CheckIsSkipNeeded(bytesCount))
            {
                return 0;
            }

            byte[] buffer = ArrayPool<byte>.Shared.Rent(4096);
            using var _ = new CleanupScope(() => ArrayPool<byte>.Shared.Return(buffer));
            return SkipBytes(stream, bytesCount, throwOnEndOfStream, buffer);
        }

        public static long SkipBytes(this Stream stream, long bytesCount, bool throwOnEndOfStream, byte[] buffer)
        {
            if (!CheckIsSkipNeeded(bytesCount))
            {
                return 0;
            }

            long totalRead = 0;
            while (totalRead < bytesCount)
            {
                int read = stream.Read(buffer, 0, (int)Math.Min(bytesCount - totalRead, buffer.Length));
                if (read == 0)
                {
                    if (throwOnEndOfStream)
                    {
                        throw new InvalidDataException("Unexpected end of stream.");
                    }

                    return totalRead;
                }

                totalRead += read;
            }

            return totalRead;
        }

        public static byte[] ReadToEnd(this Stream stream)
        {
            if (stream.TryGetLength(out long length))
            {
                BinaryReader reader = new(stream);
                return reader.ReadBytes((int)length);
            }

            using var ms = new MemoryStream();
            stream.CopyTo(ms);
            return ms.ToArray();
        }

        public static bool TryGetLength(this Stream stream, out long length)
        {
            try
            {
                length = stream.Length;
                return true;
            }
            catch (NotSupportedException)
            {
                length = 0;
                return false;
            }
        }

        public static Stream ToReadableSeekableStream(this Stream stream)
        {
            return TransparentReadStream.EnsureSeekableStream(stream);
        }

        /// <summary>
        /// Creates bounded read-only, forward-only view over an underlying stream.
        /// </summary>
        /// <param name="stream"></param>
        /// <param name="length"></param>
        /// <returns></returns>
        public static Stream Slice(this Stream stream, long length)
        {
            return new SubStream(stream, length);
        }

        public static Stream Concat(this Stream stream, Stream other)
        {
            return new ConcatenatedReadStream(stream, other);
        }
    }
}
