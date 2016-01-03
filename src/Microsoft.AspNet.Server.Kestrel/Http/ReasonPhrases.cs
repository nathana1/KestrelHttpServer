// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Globalization;
using System.Text;
using Microsoft.AspNet.Server.Kestrel.Infrastructure;

namespace Microsoft.AspNet.Server.Kestrel.Http
{
    public static class ReasonPhrases
    {
        public static string ToStatus(int statusCode, string reasonPhrase = null)
        {
            if (string.IsNullOrEmpty(reasonPhrase))
            {
                return ToStatusPhrase(statusCode);
            }
            return statusCode.ToString(CultureInfo.InvariantCulture) + " " + reasonPhrase;
        }

        public static byte[] ToStatusBytes(int statusCode, string reasonPhrase = null)
        {
            if (string.IsNullOrEmpty(reasonPhrase))
            {
                switch (statusCode)
                {
                    case 100:
                        return Constants.HeaderBytesStatus100;
                    case 101:
                        return Constants.HeaderBytesStatus101;
                    case 102:
                        return Constants.HeaderBytesStatus102;
                    case 200:
                        return Constants.HeaderBytesStatus200;
                    case 201:
                        return Constants.HeaderBytesStatus201;
                    case 202:
                        return Constants.HeaderBytesStatus202;
                    case 203:
                        return Constants.HeaderBytesStatus203;
                    case 204:
                        return Constants.HeaderBytesStatus204;
                    case 205:
                        return Constants.HeaderBytesStatus205;
                    case 206:
                        return Constants.HeaderBytesStatus206;
                    case 207:
                        return Constants.HeaderBytesStatus207;
                    case 226:
                        return Constants.HeaderBytesStatus226;
                    case 300:
                        return Constants.HeaderBytesStatus300;
                    case 301:
                        return Constants.HeaderBytesStatus301;
                    case 302:
                        return Constants.HeaderBytesStatus302;
                    case 303:
                        return Constants.HeaderBytesStatus303;
                    case 304:
                        return Constants.HeaderBytesStatus304;
                    case 305:
                        return Constants.HeaderBytesStatus305;
                    case 306:
                        return Constants.HeaderBytesStatus306;
                    case 307:
                        return Constants.HeaderBytesStatus307;
                    case 400:
                        return Constants.HeaderBytesStatus400;
                    case 401:
                        return Constants.HeaderBytesStatus401;
                    case 402:
                        return Constants.HeaderBytesStatus402;
                    case 403:
                        return Constants.HeaderBytesStatus403;
                    case 404:
                        return Constants.HeaderBytesStatus404;
                    case 405:
                        return Constants.HeaderBytesStatus405;
                    case 406:
                        return Constants.HeaderBytesStatus406;
                    case 407:
                        return Constants.HeaderBytesStatus407;
                    case 408:
                        return Constants.HeaderBytesStatus408;
                    case 409:
                        return Constants.HeaderBytesStatus409;
                    case 410:
                        return Constants.HeaderBytesStatus410;
                    case 411:
                        return Constants.HeaderBytesStatus411;
                    case 412:
                        return Constants.HeaderBytesStatus412;
                    case 413:
                        return Constants.HeaderBytesStatus413;
                    case 414:
                        return Constants.HeaderBytesStatus414;
                    case 415:
                        return Constants.HeaderBytesStatus415;
                    case 416:
                        return Constants.HeaderBytesStatus416;
                    case 417:
                        return Constants.HeaderBytesStatus417;
                    case 418:
                        return Constants.HeaderBytesStatus418;
                    case 422:
                        return Constants.HeaderBytesStatus422;
                    case 423:
                        return Constants.HeaderBytesStatus423;
                    case 424:
                        return Constants.HeaderBytesStatus424;
                    case 426:
                        return Constants.HeaderBytesStatus426;
                    case 500:
                        return Constants.HeaderBytesStatus500;
                    case 501:
                        return Constants.HeaderBytesStatus501;
                    case 502:
                        return Constants.HeaderBytesStatus502;
                    case 503:
                        return Constants.HeaderBytesStatus503;
                    case 504:
                        return Constants.HeaderBytesStatus504;
                    case 505:
                        return Constants.HeaderBytesStatus505;
                    case 506:
                        return Constants.HeaderBytesStatus506;
                    case 507:
                        return Constants.HeaderBytesStatus507;
                    case 510:
                        return Constants.HeaderBytesStatus510;
                    default:
                        return Encoding.ASCII.GetBytes(statusCode.ToString(CultureInfo.InvariantCulture) + " Unknown");
                }
            }
            return Encoding.ASCII.GetBytes(statusCode.ToString(CultureInfo.InvariantCulture) + " " + reasonPhrase);
        }

        public static string ToReasonPhrase(int statusCode)
        {
            switch (statusCode)
            {
                case 100:
                    return "Continue";
                case 101:
                    return "Switching Protocols";
                case 102:
                    return "Processing";
                case 200:
                    return "OK";
                case 201:
                    return "Created";
                case 202:
                    return "Accepted";
                case 203:
                    return "Non-Authoritative Information";
                case 204:
                    return "No Content";
                case 205:
                    return "Reset Content";
                case 206:
                    return "Partial Content";
                case 207:
                    return "Multi-Status";
                case 226:
                    return "IM Used";
                case 300:
                    return "Multiple Choices";
                case 301:
                    return "Moved Permanently";
                case 302:
                    return "Found";
                case 303:
                    return "See Other";
                case 304:
                    return "Not Modified";
                case 305:
                    return "Use Proxy";
                case 306:
                    return "Reserved";
                case 307:
                    return "Temporary Redirect";
                case 400:
                    return "Bad Request";
                case 401:
                    return "Unauthorized";
                case 402:
                    return "Payment Required";
                case 403:
                    return "Forbidden";
                case 404:
                    return "Not Found";
                case 405:
                    return "Method Not Allowed";
                case 406:
                    return "Not Acceptable";
                case 407:
                    return "Proxy Authentication Required";
                case 408:
                    return "Request Timeout";
                case 409:
                    return "Conflict";
                case 410:
                    return "Gone";
                case 411:
                    return "Length Required";
                case 412:
                    return "Precondition Failed";
                case 413:
                    return "Payload Too Large";
                case 414:
                    return "URI Too Long";
                case 415:
                    return "Unsupported Media Type";
                case 416:
                    return "Range Not Satisfiable";
                case 417:
                    return "Expectation Failed";
                case 418:
                    return "I'm a Teapot";
                case 422:
                    return "Unprocessable Entity";
                case 423:
                    return "Locked";
                case 424:
                    return "Failed Dependency";
                case 426:
                    return "Upgrade Required";
                case 500:
                    return "Internal Server Error";
                case 501:
                    return "Not Implemented";
                case 502:
                    return "Bad Gateway";
                case 503:
                    return "Service Unavailable";
                case 504:
                    return "Gateway Timeout";
                case 505:
                    return "HTTP Version Not Supported";
                case 506:
                    return "Variant Also Negotiates";
                case 507:
                    return "Insufficient Storage";
                case 510:
                    return "Not Extended";
                default:
                    return null;
            }
        }

        public static string ToStatusPhrase(int statusCode)
        {
            switch (statusCode)
            {
                case 100:
                    return "100 Continue";
                case 101:
                    return "101 Switching Protocols";
                case 102:
                    return "102 Processing";
                case 200:
                    return "200 OK";
                case 201:
                    return "201 Created";
                case 202:
                    return "202 Accepted";
                case 203:
                    return "203 Non-Authoritative Information";
                case 204:
                    return "204 No Content";
                case 205:
                    return "205 Reset Content";
                case 206:
                    return "206 Partial Content";
                case 207:
                    return "207 Multi-Status";
                case 226:
                    return "226 IM Used";
                case 300:
                    return "300 Multiple Choices";
                case 301:
                    return "301 Moved Permanently";
                case 302:
                    return "302 Found";
                case 303:
                    return "303 See Other";
                case 304:
                    return "304 Not Modified";
                case 305:
                    return "305 Use Proxy";
                case 306:
                    return "306 Reserved";
                case 307:
                    return "307 Temporary Redirect";
                case 400:
                    return "400 Bad Request";
                case 401:
                    return "401 Unauthorized";
                case 402:
                    return "402 Payment Required";
                case 403:
                    return "403 Forbidden";
                case 404:
                    return "404 Not Found";
                case 405:
                    return "405 Method Not Allowed";
                case 406:
                    return "406 Not Acceptable";
                case 407:
                    return "407 Proxy Authentication Required";
                case 408:
                    return "408 Request Timeout";
                case 409:
                    return "409 Conflict";
                case 410:
                    return "410 Gone";
                case 411:
                    return "411 Length Required";
                case 412:
                    return "412 Precondition Failed";
                case 413:
                    return "413 Request Entity Too Large";
                case 414:
                    return "414 Request-URI Too Long";
                case 415:
                    return "415 Unsupported Media Type";
                case 416:
                    return "416 Requested Range Not Satisfiable";
                case 417:
                    return "417 Expectation Failed";
                case 418:
                    return "418 I'm a Teapot";
                case 422:
                    return "422 Unprocessable Entity";
                case 423:
                    return "423 Locked";
                case 424:
                    return "424 Failed Dependency";
                case 426:
                    return "426 Upgrade Required";
                case 500:
                    return "500 Internal Server Error";
                case 501:
                    return "501 Not Implemented";
                case 502:
                    return "502 Bad Gateway";
                case 503:
                    return "503 Service Unavailable";
                case 504:
                    return "504 Gateway Timeout";
                case 505:
                    return "505 HTTP Version Not Supported";
                case 506:
                    return "506 Variant Also Negotiates";
                case 507:
                    return "507 Insufficient Storage";
                case 510:
                    return "510 Not Extended";
                default:
                    return statusCode.ToString(CultureInfo.InvariantCulture) + " Unknown";
            }
        }
    }
}