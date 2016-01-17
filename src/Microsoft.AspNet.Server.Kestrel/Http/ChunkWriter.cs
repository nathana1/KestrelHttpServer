// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Text;
using Microsoft.AspNet.Server.Kestrel.Infrastructure;

namespace Microsoft.AspNet.Server.Kestrel.Http
{
    public static class ChunkWriter
    {
        private static readonly ArraySegment<byte> _endChunkBytes = CreateAsciiByteArraySegment("\r\n");
        private static readonly byte[] _hex = Encoding.ASCII.GetBytes("0123456789abcdef");

        private static ArraySegment<byte> CreateAsciiByteArraySegment(string text)
        {
            var bytes = Encoding.ASCII.GetBytes(text);
            return new ArraySegment<byte>(bytes);
        }

        public static int WriteBeginChunkBytes(ref MemoryPoolIterator2 start, int dataCount)
        {
            // Determine the most-significant non-zero nibble
            int total, shift, shiftedCount;
            shiftedCount = dataCount;
            total = (shiftedCount > 0xffff) ? 0x10 : 0x00;
            shiftedCount >>= total;
            shift = (shiftedCount > 0x00ff) ? 0x08 : 0x00;
            shiftedCount >>= shift;
            total |= shift;
            total |= (shiftedCount > 0x000f) ? 0x04 : 0x00;

            var block = start.Block;
            var blockIndex = start.Index;
            var segmentEnd = block.Data.Offset + block.Data.Count;
            var pool = block.Pool;

            var chars = (total >> 2) + 1;
            var shiftDistance = 4 * chars;
            for (int i = chars + 2; i > 0; i--)
            {
                if (blockIndex == segmentEnd)
                {
                    var nextBlock = pool.Lease();
                    block.End = blockIndex;
                    block.Next = nextBlock;
                    block = nextBlock;
                    blockIndex = block.Data.Offset;
                    segmentEnd = block.Data.Offset + block.Data.Count;
                }

                if (i > 2)
                {
                    shiftDistance -= 4;
                    block.Array[blockIndex++] = _hex[(dataCount >> shiftDistance) & 0x0f];
                }
                else if (i == 2)
                {
                    block.Array[blockIndex++] = (byte)'\r';
                }
                else if (i == 1)
                {
                    block.Array[blockIndex++] = (byte)'\n';
                }
                else
                {
                    throw new ArgumentOutOfRangeException();
                }
            }

            block.End = blockIndex;
            start.Block = block;
            start.Index = blockIndex;

            return chars + 2;
        }

        public static ArraySegment<byte> BeginChunkBytes(int dataCount)
        {
            var bytes = new byte[10]
            {
                _hex[((dataCount >> 0x1c) & 0x0f)],
                _hex[((dataCount >> 0x18) & 0x0f)],
                _hex[((dataCount >> 0x14) & 0x0f)],
                _hex[((dataCount >> 0x10) & 0x0f)],
                _hex[((dataCount >> 0x0c) & 0x0f)],
                _hex[((dataCount >> 0x08) & 0x0f)],
                _hex[((dataCount >> 0x04) & 0x0f)],
                _hex[((dataCount >> 0x00) & 0x0f)],
                (byte)'\r',
                (byte)'\n',
            };

            // Determine the most-significant non-zero nibble
            int total, shift;
            total = (dataCount > 0xffff) ? 0x10 : 0x00;
            dataCount >>= total;
            shift = (dataCount > 0x00ff) ? 0x08 : 0x00;
            dataCount >>= shift;
            total |= shift;
            total |= (dataCount > 0x000f) ? 0x04 : 0x00;

            var offset = 7 - (total >> 2);
            return new ArraySegment<byte>(bytes, offset, 10 - offset);
        }

        public static int WriteBeginChunkBytesOld(ref MemoryPoolIterator2 start, int dataCount)
        {
            var chunkSegment = BeginChunkBytes(dataCount);
            start.CopyFrom(chunkSegment);
            return chunkSegment.Count;
        }

        public static void WriteEndChunkBytes(ref MemoryPoolIterator2 start)
        {
            start.CopyFrom(_endChunkBytes);
        }
    }
}
