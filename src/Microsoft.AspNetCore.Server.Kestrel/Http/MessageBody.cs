// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.AspNetCore.Server.Kestrel.Http
{
    public abstract class MessageBody
    {
        private readonly FrameContext _context;
        private int _send100Continue = 1;

        protected MessageBody(FrameContext context)
        {
            _context = context;
        }

        public bool RequestKeepAlive { get; protected set; }

        public ValueTask<int> ReadAsync(ArraySegment<byte> buffer, CancellationToken cancellationToken = default(CancellationToken))
        {
            var send100Continue = 0;
            var result = ReadAsyncImplementation(buffer, cancellationToken);
            if (!result.IsCompleted)
            {
                send100Continue = Interlocked.Exchange(ref _send100Continue, 0);
            }
            if (send100Continue == 1)
            {
                _context.FrameControl.ProduceContinue();
            }
            return result;
        }

        public async Task Consume(CancellationToken cancellationToken = default(CancellationToken))
        {
            ValueTask<int> result;
            var send100checked = false;
            do
            {
                result = ReadAsyncImplementation(default(ArraySegment<byte>), cancellationToken);
                if (!result.IsCompleted)
                {
                    if (!send100checked)
                    {
                        if (Interlocked.Exchange(ref _send100Continue, 0) == 1)
                        {
                            _context.FrameControl.ProduceContinue();
                        }
                        send100checked = true;
                    }
                }
                // ValueTask uses .GetAwaiter().GetResult() if necessary
                else if (result.Result == 0) 
                {
                    // Completed Task, end of stream
                    return;
                }
                else
                {
                    // Completed Task, get next Task rather than await
                    continue;
                }
            } while (await result != 0);
        }

        public abstract ValueTask<int> ReadAsyncImplementation(ArraySegment<byte> buffer, CancellationToken cancellationToken);

        public static MessageBody For(
            string httpVersion,
            FrameRequestHeaders headers,
            FrameContext context)
        {
            // see also http://tools.ietf.org/html/rfc2616#section-4.4

            var keepAlive = httpVersion != "HTTP/1.0";

            var connection = headers.HeaderConnection.ToString();
            if (connection.Length > 0)
            {
                keepAlive = connection.Equals("keep-alive", StringComparison.OrdinalIgnoreCase);
            }

            var transferEncoding = headers.HeaderTransferEncoding.ToString();
            if (transferEncoding.Length > 0)
            {
                return new ForChunkedEncoding(keepAlive, context);
            }

            var contentLength = headers.HeaderContentLength.ToString();
            if (contentLength.Length > 0)
            {
                return new ForContentLength(keepAlive, int.Parse(contentLength), context);
            }

            if (keepAlive)
            {
                return new ForContentLength(true, 0, context);
            }

            return new ForRemainingData(context);
        }

        private class ForRemainingData : MessageBody
        {
            public ForRemainingData(FrameContext context)
                : base(context)
            {
            }

            public override ValueTask<int> ReadAsyncImplementation(ArraySegment<byte> buffer, CancellationToken cancellationToken)
            {
                return _context.SocketInput.ReadAsync(buffer.Array, buffer.Offset, buffer.Array == null ? 8192 : buffer.Count);
            }
        }

        private class ForContentLength : MessageBody
        {
            private readonly int _contentLength;
            private int _inputLength;

            public ForContentLength(bool keepAlive, int contentLength, FrameContext context)
                : base(context)
            {
                RequestKeepAlive = keepAlive;
                _contentLength = contentLength;
                _inputLength = _contentLength;
            }

            public override ValueTask<int> ReadAsyncImplementation(ArraySegment<byte> buffer, CancellationToken cancellationToken)
            {
                var input = _context.SocketInput;

                var limit = buffer.Array == null ? _inputLength : Math.Min(buffer.Count, _inputLength);
                if (limit == 0)
                {
                    return 0;
                }

                var task = _context.SocketInput.ReadAsync(buffer.Array, buffer.Offset, limit);

                if (task.IsCompleted)
                {
                    // .GetAwaiter().GetResult() done by ValueTask if needed
                    var actual = task.Result;
                    _inputLength -= actual;
                    if (actual == 0)
                    {
                        throw new InvalidDataException("Unexpected end of request content");
                    }
                    return actual;
                }
                else
                {
                    return ReadAsyncAwaited(task.AsTask());
                }
            }

            private async Task<int> ReadAsyncAwaited(Task<int> task)
            {
                var actual = await task;
                _inputLength -= actual;
                if (actual == 0)
                {
                    throw new InvalidDataException("Unexpected end of request content");
                }

                return actual;
            }
        }

        /// <summary>
        ///   http://tools.ietf.org/html/rfc2616#section-3.6.1
        /// </summary>
        private class ForChunkedEncoding : MessageBody
        {
            private int _inputLength;

            private Mode _mode = Mode.ChunkPrefix;

            public ForChunkedEncoding(bool keepAlive, FrameContext context)
                : base(context)
            {
                RequestKeepAlive = keepAlive;
            }
            public override ValueTask<int> ReadAsyncImplementation(ArraySegment<byte> buffer, CancellationToken cancellationToken)
            {
                return ReadAsyncAwaited(buffer, cancellationToken);
            }

            private async Task<int> ReadAsyncAwaited(ArraySegment<byte> buffer, CancellationToken cancellationToken)
            {
                var input = _context.SocketInput;

                while (_mode != Mode.Complete)
                {
                    while (_mode == Mode.ChunkPrefix)
                    {
                        var chunkSize = 0;
                        if (!TakeChunkedLine(input, ref chunkSize))
                        {
                            await input;
                        }
                        else if (chunkSize == 0)
                        {
                            _mode = Mode.Complete;
                        }
                        else
                        {
                            _mode = Mode.ChunkData;
                        }
                        _inputLength = chunkSize;
                    }
                    while (_mode == Mode.ChunkData)
                    {
                        var limit = buffer.Array == null ? _inputLength : Math.Min(buffer.Count, _inputLength);
                        if (limit != 0)
                        {
                            await input;
                        }

                        var begin = input.ConsumingStart();
                        int actual;
                        var end = begin.CopyTo(buffer.Array, buffer.Offset, limit, out actual);
                        _inputLength -= actual;
                        input.ConsumingComplete(end, end);

                        if (_inputLength == 0)
                        {
                            _mode = Mode.ChunkSuffix;
                        }
                        if (actual != 0)
                        {
                            return actual;
                        }
                    }
                    while (_mode == Mode.ChunkSuffix)
                    {
                        var scan = input.ConsumingStart();
                        var consumed = scan;
                        var ch1 = scan.Take();
                        var ch2 = scan.Take();
                        if (ch1 == -1 || ch2 == -1)
                        {
                            input.ConsumingComplete(consumed, scan);
                            await input;
                        }
                        else if (ch1 == '\r' && ch2 == '\n')
                        {
                            input.ConsumingComplete(scan, scan);
                            _mode = Mode.ChunkPrefix;
                        }
                        else
                        {
                            throw new NotImplementedException("INVALID REQUEST FORMAT");
                        }
                    }
                }

                return 0;
            }

            private static bool TakeChunkedLine(SocketInput baton, ref int chunkSizeOut)
            {
                var scan = baton.ConsumingStart();
                var consumed = scan;
                try
                {
                    var ch0 = scan.Take();
                    var chunkSize = 0;
                    var mode = 0;
                    while (ch0 != -1)
                    {
                        var ch1 = scan.Take();
                        if (ch1 == -1)
                        {
                            return false;
                        }

                        if (mode == 0)
                        {
                            if (ch0 >= '0' && ch0 <= '9')
                            {
                                chunkSize = chunkSize * 0x10 + (ch0 - '0');
                            }
                            else if (ch0 >= 'A' && ch0 <= 'F')
                            {
                                chunkSize = chunkSize * 0x10 + (ch0 - ('A' - 10));
                            }
                            else if (ch0 >= 'a' && ch0 <= 'f')
                            {
                                chunkSize = chunkSize * 0x10 + (ch0 - ('a' - 10));
                            }
                            else
                            {
                                throw new NotImplementedException("INVALID REQUEST FORMAT");
                            }
                            mode = 1;
                        }
                        else if (mode == 1)
                        {
                            if (ch0 >= '0' && ch0 <= '9')
                            {
                                chunkSize = chunkSize * 0x10 + (ch0 - '0');
                            }
                            else if (ch0 >= 'A' && ch0 <= 'F')
                            {
                                chunkSize = chunkSize * 0x10 + (ch0 - ('A' - 10));
                            }
                            else if (ch0 >= 'a' && ch0 <= 'f')
                            {
                                chunkSize = chunkSize * 0x10 + (ch0 - ('a' - 10));
                            }
                            else if (ch0 == ';')
                            {
                                mode = 2;
                            }
                            else if (ch0 == '\r' && ch1 == '\n')
                            {
                                consumed = scan;
                                chunkSizeOut = chunkSize;
                                return true;
                            }
                            else
                            {
                                throw new NotImplementedException("INVALID REQUEST FORMAT");
                            }
                        }
                        else if (mode == 2)
                        {
                            if (ch0 == '\r' && ch1 == '\n')
                            {
                                consumed = scan;
                                chunkSizeOut = chunkSize;
                                return true;
                            }
                            else
                            {
                                // chunk-extensions not currently parsed
                            }
                        }

                        ch0 = ch1;
                    }
                    return false;
                }
                finally
                {
                    baton.ConsumingComplete(consumed, scan);
                }
            }

            private enum Mode
            {
                ChunkPrefix,
                ChunkData,
                ChunkSuffix,
                Complete,
            };
        }
    }
}
