// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.AspNetCore.Server.Kestrel.Http;

namespace Microsoft.AspNetCore.Server.Kestrel.Infrastructure
{
    public class HeaderFactory : DefaultComponentFactory<Headers>
    {
        // https://github.com/dotnet/coreclr/pull/4468#issuecomment-212931043
        // 12x Faster than new T() which uses System.Activator 
        protected override Headers CreateNew() => new Headers();
    }
}
