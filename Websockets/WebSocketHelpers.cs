//
// Copyright (c) .NET Foundation and Contributors
// See LICENSE file in the project root for full license information.
//

using System.Collections;
using System.Text;

namespace System.Net.WebSockets
{
    internal static class WebSocketHelpers
    {
        internal static byte[] ReverseBytes(byte[] bytes)
        {
            byte[] returnBytes = new byte[bytes.Length];
            for (int i = 0; i < bytes.Length; i++)
            {
                returnBytes[bytes.Length - i - 1] = bytes[i];
            }

            return returnBytes;
        }

        public static IDictionary ParseHeaders(string handshake)
        {
            var headers = handshake.Split(new char[] { '\r', '\n' });
            IDictionary dic = new Hashtable();
            foreach (string header in headers)
            {
                var keyVal = header.Split(':');
                if (keyVal.Length == 2)
                {
                    dic[keyVal[0].Trim().ToLower()] = keyVal[1].Trim();
                }
            }

            return dic;
        }

        private static uint SHA1RotateLeft(uint x, int n)
        {
            return ((x << n) | (x >> (32 - n)));
        }

        public static byte[] ComputeHash(string msg)
        {
            byte[] data = Encoding.UTF8.GetBytes(msg);
            uint databytes = (uint)data.Length;
            byte[] digest = new byte[20];

            uint[] W = new uint[80];
            uint[] H = new uint[] { 0x67452301, 0xEFCDAB89, 0x98BADCFE, 0x10325476, 0xC3D2E1F0 };

            uint a, b, c, d, e;
            uint f = 0;
            uint k = 0;

            uint idx, lidx, widx;
            uint didx = 0;

            int wcount;
            uint temp;
            ulong databits = ((ulong)databytes) * 8;
            uint loopcount = (databytes + 8) / 64 + 1; //problem is 
            uint tailbytes = 64 * loopcount - databytes;
            byte[] datatail = new byte[128]; //{ 0 };


            if (data == null) return null;

            /* Pre-processing of data tail (includes padding to fill out 512-bit chunk):
           Add bit '1' to end of message (big-endian)
           Add 64-bit message length in bits at very end (big-endian) */
            datatail[0] = 0x80;
            datatail[tailbytes - 8] = (byte)(databits >> 56 & 0xFF);
            datatail[tailbytes - 7] = (byte)(databits >> 48 & 0xFF);
            datatail[tailbytes - 6] = (byte)(databits >> 40 & 0xFF);
            datatail[tailbytes - 5] = (byte)(databits >> 32 & 0xFF);
            datatail[tailbytes - 4] = (byte)(databits >> 24 & 0xFF);
            datatail[tailbytes - 3] = (byte)(databits >> 16 & 0xFF);
            datatail[tailbytes - 2] = (byte)(databits >> 8 & 0xFF);
            datatail[tailbytes - 1] = (byte)(databits >> 0 & 0xFF);

            for (lidx = 0; lidx < loopcount; lidx++)
            {
                W = new uint[80];
                /* Break 512-bit chunk into sixteen 32-bit, big endian words */
                for (widx = 0; widx <= 15; widx++)
                {
                    wcount = 24;

                    /* Copy byte-per byte from specified buffer */
                    while (didx < databytes && wcount >= 0)
                    {
                        W[widx] += (((uint)data[didx]) << wcount);
                        didx++;
                        wcount -= 8;
                    }
                    /* Fill out W with padding as needed */
                    while (wcount >= 0)
                    {
                        W[widx] += (((uint)datatail[didx - databytes]) << wcount);
                        didx++;
                        wcount -= 8;
                    }
                }

                for (widx = 16; widx <= 31; widx++)
                {
                    W[widx] = SHA1RotateLeft((W[widx - 3] ^ W[widx - 8] ^ W[widx - 14] ^ W[widx - 16]), 1);
                }
                for (widx = 32; widx <= 79; widx++)
                {
                    W[widx] = SHA1RotateLeft((W[widx - 6] ^ W[widx - 16] ^ W[widx - 28] ^ W[widx - 32]), 2);
                }

                /* Main loop */
                a = H[0];
                b = H[1];
                c = H[2];
                d = H[3];
                e = H[4];

                for (idx = 0; idx <= 79; idx++)
                {
                    if (idx <= 19)
                    {
                        f = (b & c) | ((~b) & d);
                        k = 0x5A827999;
                    }
                    else if (idx >= 20 && idx <= 39)
                    {
                        f = b ^ c ^ d;
                        k = 0x6ED9EBA1;
                    }
                    else if (idx >= 40 && idx <= 59)
                    {
                        f = (b & c) | (b & d) | (c & d);
                        k = 0x8F1BBCDC;
                    }
                    else if (idx >= 60 && idx <= 79)
                    {
                        f = b ^ c ^ d;
                        k = 0xCA62C1D6;
                    }
                    temp = SHA1RotateLeft(a, 5) + f + e + k + W[idx];
                    e = d;
                    d = c;
                    c = SHA1RotateLeft(b, 30);
                    b = a;
                    a = temp;
                }

                H[0] += a;
                H[1] += b;
                H[2] += c;
                H[3] += d;
                H[4] += e;
            }

            /* Store binary digest in supplied buffer */
            for (idx = 0; idx < 5; idx++)
            {
                digest[idx * 4 + 0] = (byte)(H[idx] >> 24);
                digest[idx * 4 + 1] = (byte)(H[idx] >> 16);
                digest[idx * 4 + 2] = (byte)(H[idx] >> 8);
                digest[idx * 4 + 3] = (byte)(H[idx]);
            }

            return digest;
        }
    }
}
