﻿using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace FeatureLoom.Extensions
{
    public static class StreamExtensions
    {
        public static string ReadToString(this Stream stream)
        {
            StreamReader reader = new StreamReader(stream);
            return reader.ReadToEnd();
        }

        public static Task<string> ReadToStringAsync(this Stream stream)
        {
            StreamReader reader = new StreamReader(stream);
            return reader.ReadToEndAsync();
        }

        public static Stream ToStream(this string str, Encoding encoding = null)
        {
            if (encoding == null) encoding = Encoding.UTF8;
            return new MemoryStream(encoding.GetBytes(str));
        }

        public static async Task<byte[]> ReadToByteArrayAsync(this Stream stream, int bufferSize)
        {
            byte[] buffer = new byte[bufferSize];
            int bytesRead = 0;
            bool finished = false;
            byte[] data = null;

            do
            {
                bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
                if (bytesRead < buffer.Length) finished = true;
                if (bytesRead > 0)
                {
                    if (data == null) data = new byte[bytesRead];
                    data = data.Combine(buffer, bytesRead);
                }
            }
            while (!finished);

            return data ?? Array.Empty<Byte>();
        }
    }
}