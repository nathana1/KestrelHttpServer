// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Numerics;
using System.Text;

namespace Microsoft.AspNet.Server.Kestrel.Infrastructure
{
    internal class Constants
    {
        public const int ListenBacklog = 128;

        public const int EOF = -4095;
        public const int ECONNRESET = -4077;

        /// <summary>
        /// Prefix of host name used to specify Unix sockets in the configuration.
        /// </summary>
        public const string UnixPipeHostPrefix = "unix:/";

        /// <summary>
        /// DateTime format string for RFC1123. See  https://msdn.microsoft.com/en-us/library/az4se3k1(v=vs.110).aspx#RFC1123
        /// for info on the format.
        /// </summary>
        public const string RFC1123DateFormat = "r";

        public readonly static int VectorSpan = Vector<byte>.Count;
        private readonly static bool IsLittleEndian = BitConverter.IsLittleEndian;

        internal static readonly byte[] HeaderBytesStatus100 = Encoding.ASCII.GetBytes("100 Continue");
        internal static readonly byte[] HeaderBytesStatus101 = Encoding.ASCII.GetBytes("101 Switching Protocols");
        internal static readonly byte[] HeaderBytesStatus102 = Encoding.ASCII.GetBytes("102 Processing");
        internal static readonly byte[] HeaderBytesStatus200 = Encoding.ASCII.GetBytes("200 OK");
        internal static readonly byte[] HeaderBytesStatus201 = Encoding.ASCII.GetBytes("201 Created");
        internal static readonly byte[] HeaderBytesStatus202 = Encoding.ASCII.GetBytes("202 Accepted");
        internal static readonly byte[] HeaderBytesStatus203 = Encoding.ASCII.GetBytes("203 Non-Authoritative Information");
        internal static readonly byte[] HeaderBytesStatus204 = Encoding.ASCII.GetBytes("204 No Content");
        internal static readonly byte[] HeaderBytesStatus205 = Encoding.ASCII.GetBytes("205 Reset Content");
        internal static readonly byte[] HeaderBytesStatus206 = Encoding.ASCII.GetBytes("206 Partial Content");
        internal static readonly byte[] HeaderBytesStatus207 = Encoding.ASCII.GetBytes("207 Multi-Status");
        internal static readonly byte[] HeaderBytesStatus226 = Encoding.ASCII.GetBytes("226 IM Used");
        internal static readonly byte[] HeaderBytesStatus300 = Encoding.ASCII.GetBytes("300 Multiple Choices");
        internal static readonly byte[] HeaderBytesStatus301 = Encoding.ASCII.GetBytes("301 Moved Permanently");
        internal static readonly byte[] HeaderBytesStatus302 = Encoding.ASCII.GetBytes("302 Found");
        internal static readonly byte[] HeaderBytesStatus303 = Encoding.ASCII.GetBytes("303 See Other");
        internal static readonly byte[] HeaderBytesStatus304 = Encoding.ASCII.GetBytes("304 Not Modified");
        internal static readonly byte[] HeaderBytesStatus305 = Encoding.ASCII.GetBytes("305 Use Proxy");
        internal static readonly byte[] HeaderBytesStatus306 = Encoding.ASCII.GetBytes("306 Reserved");
        internal static readonly byte[] HeaderBytesStatus307 = Encoding.ASCII.GetBytes("307 Temporary Redirect");
        internal static readonly byte[] HeaderBytesStatus400 = Encoding.ASCII.GetBytes("400 Bad Request");
        internal static readonly byte[] HeaderBytesStatus401 = Encoding.ASCII.GetBytes("401 Unauthorized");
        internal static readonly byte[] HeaderBytesStatus402 = Encoding.ASCII.GetBytes("402 Payment Required");
        internal static readonly byte[] HeaderBytesStatus403 = Encoding.ASCII.GetBytes("403 Forbidden");
        internal static readonly byte[] HeaderBytesStatus404 = Encoding.ASCII.GetBytes("404 Not Found");
        internal static readonly byte[] HeaderBytesStatus405 = Encoding.ASCII.GetBytes("405 Method Not Allowed");
        internal static readonly byte[] HeaderBytesStatus406 = Encoding.ASCII.GetBytes("406 Not Acceptable");
        internal static readonly byte[] HeaderBytesStatus407 = Encoding.ASCII.GetBytes("407 Proxy Authentication Required");
        internal static readonly byte[] HeaderBytesStatus408 = Encoding.ASCII.GetBytes("408 Request Timeout");
        internal static readonly byte[] HeaderBytesStatus409 = Encoding.ASCII.GetBytes("409 Conflict");
        internal static readonly byte[] HeaderBytesStatus410 = Encoding.ASCII.GetBytes("410 Gone");
        internal static readonly byte[] HeaderBytesStatus411 = Encoding.ASCII.GetBytes("411 Length Required");
        internal static readonly byte[] HeaderBytesStatus412 = Encoding.ASCII.GetBytes("412 Precondition Failed");
        internal static readonly byte[] HeaderBytesStatus413 = Encoding.ASCII.GetBytes("413 Payload Too Large");
        internal static readonly byte[] HeaderBytesStatus414 = Encoding.ASCII.GetBytes("414 URI Too Long");
        internal static readonly byte[] HeaderBytesStatus415 = Encoding.ASCII.GetBytes("415 Unsupported Media Type");
        internal static readonly byte[] HeaderBytesStatus416 = Encoding.ASCII.GetBytes("416 Range Not Satisfiable");
        internal static readonly byte[] HeaderBytesStatus417 = Encoding.ASCII.GetBytes("417 Expectation Failed");
        internal static readonly byte[] HeaderBytesStatus418 = Encoding.ASCII.GetBytes("418 I'm a Teapot");
        internal static readonly byte[] HeaderBytesStatus422 = Encoding.ASCII.GetBytes("422 Unprocessable Entity");
        internal static readonly byte[] HeaderBytesStatus423 = Encoding.ASCII.GetBytes("423 Locked");
        internal static readonly byte[] HeaderBytesStatus424 = Encoding.ASCII.GetBytes("424 Failed Dependency");
        internal static readonly byte[] HeaderBytesStatus426 = Encoding.ASCII.GetBytes("426 Upgrade Required");
        internal static readonly byte[] HeaderBytesStatus500 = Encoding.ASCII.GetBytes("500 Internal Server Error");
        internal static readonly byte[] HeaderBytesStatus501 = Encoding.ASCII.GetBytes("501 Not Implemented");
        internal static readonly byte[] HeaderBytesStatus502 = Encoding.ASCII.GetBytes("502 Bad Gateway");
        internal static readonly byte[] HeaderBytesStatus503 = Encoding.ASCII.GetBytes("503 Service Unavailable");
        internal static readonly byte[] HeaderBytesStatus504 = Encoding.ASCII.GetBytes("504 Gateway Timeout");
        internal static readonly byte[] HeaderBytesStatus505 = Encoding.ASCII.GetBytes("505 HTTP Version Not Supported");
        internal static readonly byte[] HeaderBytesStatus506 = Encoding.ASCII.GetBytes("506 Variant Also Negotiates");
        internal static readonly byte[] HeaderBytesStatus507 = Encoding.ASCII.GetBytes("507 Insufficient Storage");
        internal static readonly byte[] HeaderBytesStatus510 = Encoding.ASCII.GetBytes("510 Not Extended");

        internal static readonly byte[] HeaderBytesConnectionClose = Encoding.ASCII.GetBytes("\r\nConnection: close");
        internal static readonly byte[] HeaderBytesConnectionKeepAlive = Encoding.ASCII.GetBytes("\r\nConnection: keep-alive");
        internal static readonly byte[] HeaderBytesTransferEncodingChunked = Encoding.ASCII.GetBytes("\r\nTransfer-Encoding: chunked");
        internal static readonly byte[] HeaderBytesHttpVersion1_0 = Encoding.ASCII.GetBytes("HTTP/1.0 ");
        internal static readonly byte[] HeaderBytesHttpVersion1_1 = Encoding.ASCII.GetBytes("HTTP/1.1 ");
        internal static readonly byte[] HeaderBytesContentLengthZero = Encoding.ASCII.GetBytes("\r\nContent-Length: 0");
        internal static readonly byte[] HeaderBytesSpace = Encoding.ASCII.GetBytes(" ");
        internal static readonly byte[] HeaderBytesServer = Encoding.ASCII.GetBytes("\r\nServer: Kestrel");
        internal static readonly byte[] HeaderBytesDate = Encoding.ASCII.GetBytes("Date: ");
        internal static readonly byte[] HeaderBytesEndHeaders = Encoding.ASCII.GetBytes("\r\n\r\n");

        internal static readonly byte[] HexBytes = Encoding.ASCII.GetBytes("0123456789abcdef");

        internal static readonly byte[] HeaderEndChunkBytes = Encoding.ASCII.GetBytes("\r\n");
        internal static readonly byte[] HeaderEndChunkedResponseBytes = Encoding.ASCII.GetBytes("0\r\n\r\n");
        internal static readonly byte[] HeaderContinueBytes = Encoding.ASCII.GetBytes("HTTP/1.1 100 Continue\r\n\r\n");
        internal static readonly byte[] HeaderEmptyDataBytes = new byte[0];

        // method needed while this bug is active https://github.com/dotnet/coreclr/issues/2526
        public static void InitalizeJitConstants()
        {
            // Pre-initalize static readonly fields backed by functions

            if (Constants.VectorSpan != Constants.VectorSpan - 1
                && (Vector.IsHardwareAccelerated != Vector.IsHardwareAccelerated)
                && (Constants.IsLittleEndian != !Constants.IsLittleEndian))
            {
                // Pre-initalize static readonly fields
            }

            var headerByteLengths =
            HeaderBytesStatus100.Length +
            HeaderBytesStatus101.Length +
            HeaderBytesStatus102.Length +
            HeaderBytesStatus200.Length +
            HeaderBytesStatus201.Length +
            HeaderBytesStatus202.Length +
            HeaderBytesStatus203.Length +
            HeaderBytesStatus204.Length +
            HeaderBytesStatus205.Length +
            HeaderBytesStatus206.Length +
            HeaderBytesStatus207.Length +
            HeaderBytesStatus226.Length +
            HeaderBytesStatus300.Length +
            HeaderBytesStatus301.Length +
            HeaderBytesStatus302.Length +
            HeaderBytesStatus303.Length +
            HeaderBytesStatus304.Length +
            HeaderBytesStatus305.Length +
            HeaderBytesStatus306.Length +
            HeaderBytesStatus307.Length +
            HeaderBytesStatus400.Length +
            HeaderBytesStatus401.Length +
            HeaderBytesStatus402.Length +
            HeaderBytesStatus403.Length +
            HeaderBytesStatus404.Length +
            HeaderBytesStatus405.Length +
            HeaderBytesStatus406.Length +
            HeaderBytesStatus407.Length +
            HeaderBytesStatus408.Length +
            HeaderBytesStatus409.Length +
            HeaderBytesStatus410.Length +
            HeaderBytesStatus411.Length +
            HeaderBytesStatus412.Length +
            HeaderBytesStatus413.Length +
            HeaderBytesStatus414.Length +
            HeaderBytesStatus415.Length +
            HeaderBytesStatus416.Length +
            HeaderBytesStatus417.Length +
            HeaderBytesStatus418.Length +
            HeaderBytesStatus422.Length +
            HeaderBytesStatus423.Length +
            HeaderBytesStatus424.Length +
            HeaderBytesStatus426.Length +
            HeaderBytesStatus500.Length +
            HeaderBytesStatus501.Length +
            HeaderBytesStatus502.Length +
            HeaderBytesStatus503.Length +
            HeaderBytesStatus504.Length +
            HeaderBytesStatus505.Length +
            HeaderBytesStatus506.Length +
            HeaderBytesStatus507.Length +
            HeaderBytesStatus510.Length +

            HeaderBytesConnectionClose.Length +
            HeaderBytesConnectionKeepAlive.Length +
            HeaderBytesTransferEncodingChunked.Length +
            HeaderBytesHttpVersion1_0.Length +
            HeaderBytesHttpVersion1_1.Length +
            HeaderBytesContentLengthZero.Length +
            HeaderBytesSpace.Length +
            HeaderBytesServer.Length +
            HeaderBytesDate.Length +
            HeaderBytesEndHeaders.Length +

            HexBytes.Length +

            HeaderEndChunkBytes.Length + 
            HeaderEndChunkedResponseBytes.Length + 
            HeaderContinueBytes.Length + 
            HeaderEmptyDataBytes.Length;
        }
    }
}
