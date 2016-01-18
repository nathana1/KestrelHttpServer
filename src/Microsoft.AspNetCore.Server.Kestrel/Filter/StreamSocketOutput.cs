// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Server.Kestrel.Http;
using Microsoft.AspNetCore.Server.Kestrel.Infrastructure;
using Microsoft.Extensions.Logging;

namespace Microsoft.AspNetCore.Server.Kestrel.Filter
{
    public class StreamSocketOutput : ISocketOutput
    {
        private static readonly byte[] _endChunkBytes = Encoding.ASCII.GetBytes("\r\n");
        private static readonly byte[] _nullBuffer = new byte[0];
        private static readonly Action<Task, object> _producingCompleteError = (t, o) => { ((ILogger)o).LogError(t.Exception.Message, t.Exception); };

        private readonly Stream _outputStream;
        private readonly MemoryPool2 _memory;
        private readonly ILogger _logger;
        private MemoryPoolBlock2 _producingBlock;

        private object _writeLock = new object();

        public StreamSocketOutput(Stream outputStream, MemoryPool2 memory, ILogger logger)
        {
            _outputStream = outputStream;
            _memory = memory;
            _logger = logger;
        }

        public void Write(ArraySegment<byte> buffer, bool immediate, bool chunk)
        {
            lock (_writeLock)
            {
                if (chunk && buffer.Array != null)
                {
                    var beginChunkBytes = ChunkWriter.BeginChunkBytes(buffer.Count);
                    _outputStream.Write(beginChunkBytes.Array, beginChunkBytes.Offset, beginChunkBytes.Count);
                }

                _outputStream.Write(buffer.Array ?? _nullBuffer, buffer.Offset, buffer.Count);

                if (chunk && buffer.Array != null)
                {
                    _outputStream.Write(_endChunkBytes, 0, _endChunkBytes.Length);
                }
            }
        }

        public Task WriteAsync(ArraySegment<byte> buffer, bool immediate, bool chunk, CancellationToken cancellationToken)
        {
            lock (_writeLock)
            {
                if (chunk && buffer.Array != null)
                {
                    return WriteAsyncChunked(buffer, cancellationToken);
                }

                return _outputStream.WriteAsync(buffer.Array ?? _nullBuffer, buffer.Offset, buffer.Count, cancellationToken);
            }
        }

        private async Task WriteAsyncChunked(ArraySegment<byte> buffer, CancellationToken cancellationToken)
        {
            var beginChunkBytes = ChunkWriter.BeginChunkBytes(buffer.Count);

            await _outputStream.WriteAsync(beginChunkBytes.Array, beginChunkBytes.Offset, beginChunkBytes.Count, cancellationToken);
            await _outputStream.WriteAsync(buffer.Array ?? _nullBuffer, buffer.Offset, buffer.Count, cancellationToken);
            await _outputStream.WriteAsync(_endChunkBytes, 0, _endChunkBytes.Length, cancellationToken);
        }

        public MemoryPoolIterator2 ProducingStart()
        {
            _producingBlock = _memory.Lease();
            return new MemoryPoolIterator2(_producingBlock);
        }

        public void ProducingComplete(MemoryPoolIterator2 end)
        {
            lock (_writeLock)
            {
                ProducingCompleteAsync(end).GetAwaiter().GetResult();
            }
        }

        private async Task ProducingCompleteAsync(MemoryPoolIterator2 end)
        {
            MemoryPoolBlock2 block;
            try
            {
                block = _producingBlock;
                while (block != end.Block)
                {
                    await _outputStream.WriteAsync(block.Data.Array, block.Data.Offset, block.Data.Count, CancellationToken.None);
                    block = block.Next;
                }

                await _outputStream.WriteAsync(end.Block.Array, end.Block.Data.Offset, end.Index - end.Block.Data.Offset, CancellationToken.None);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.Message, ex);
            }

            block = _producingBlock;
            while (block != end.Block)
            {
                var returnBlock = block;
                block = block.Next;
                returnBlock.Pool.Return(returnBlock);
            }

            end.Block.Pool.Return(end.Block);
        }
    }
}
