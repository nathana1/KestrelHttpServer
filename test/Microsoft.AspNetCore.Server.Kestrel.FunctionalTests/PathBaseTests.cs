// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Testing.xunit;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Microsoft.AspNetCore.Server.Kestrel.FunctionalTests
{
    public class PathBaseTests
    {
        [ConditionalTheory]
        [InlineData("http://localhost:8791/base", "http://localhost:8791/base", "/base", "")]
        [InlineData("http://localhost:8791/base", "http://localhost:8791/base/", "/base", "/")]
        [InlineData("http://localhost:8791/base", "http://localhost:8791/base/something", "/base", "/something")]
        [InlineData("http://localhost:8791/base", "http://localhost:8791/base/something/", "/base", "/something/")]
        [InlineData("http://localhost:8791/base/more", "http://localhost:8791/base/more", "/base/more", "")]
        [InlineData("http://localhost:8791/base/more", "http://localhost:8791/base/more/something", "/base/more", "/something")]
        [InlineData("http://localhost:8791/base/more", "http://localhost:8791/base/more/something/", "/base/more", "/something/")]
        [FrameworkSkipCondition(RuntimeFrameworks.Mono, SkipReason = "Test hangs after execution on Mono.")]
        public Task RequestPathBaseIsServerPathBase(string registerAddress, string requestAddress, string expectedPathBase, string expectedPath)
        {
            return TestPathBase(registerAddress, requestAddress, expectedPathBase, expectedPath);
        }

        [ConditionalTheory]
        [InlineData("http://localhost:8791", "http://localhost:8791/", "", "/")]
        [InlineData("http://localhost:8791", "http://localhost:8791/something", "", "/something")]
        [InlineData("http://localhost:8791/", "http://localhost:8791/", "", "/")]
        [InlineData("http://localhost:8791/base", "http://localhost:8791/", "", "/")]
        [InlineData("http://localhost:8791/base", "http://localhost:8791/something", "", "/something")]
        [InlineData("http://localhost:8791/base", "http://localhost:8791/baseandsomething", "", "/baseandsomething")]
        [InlineData("http://localhost:8791/base", "http://localhost:8791/ba", "", "/ba")]
        [InlineData("http://localhost:8791/base", "http://localhost:8791/ba/se", "", "/ba/se")]
        [FrameworkSkipCondition(RuntimeFrameworks.Mono, SkipReason = "Test hangs after execution on Mono.")]
        public Task DefaultPathBaseIsEmpty(string registerAddress, string requestAddress, string expectedPathBase, string expectedPath)
        {
            return TestPathBase(registerAddress, requestAddress, expectedPathBase, expectedPath);
        }

        [ConditionalTheory]
        [InlineData("http://localhost:8791", "http://localhost:8791/", "", "/")]
        [InlineData("http://localhost:8791/", "http://localhost:8791/", "", "/")]
        [InlineData("http://localhost:8791/base", "http://localhost:8791/base/", "/base", "/")]
        [InlineData("http://localhost:8791/base/", "http://localhost:8791/base", "/base", "")]
        [InlineData("http://localhost:8791/base/", "http://localhost:8791/base/", "/base", "/")]
        [FrameworkSkipCondition(RuntimeFrameworks.Mono, SkipReason = "Test hangs after execution on Mono.")]
        public Task PathBaseNeverEndsWithSlash(string registerAddress, string requestAddress, string expectedPathBase, string expectedPath)
        {
            return TestPathBase(registerAddress, requestAddress, expectedPathBase, expectedPath);
        }

        [ConditionalFact]
        [FrameworkSkipCondition(RuntimeFrameworks.Mono, SkipReason = "Test hangs after execution on Mono.")]
        public Task PathBaseAndPathPreserveRequestCasing()
        {
            return TestPathBase("http://localhost:8791/base", "http://localhost:8791/Base/Something", "/Base", "/Something");
        }

        [ConditionalFact]
        [FrameworkSkipCondition(RuntimeFrameworks.Mono, SkipReason = "Test hangs after execution on Mono.")]
        public Task PathBaseCanHaveUTF8Characters()
        {
            return TestPathBase("http://localhost:8791/b♫se", "http://localhost:8791/b♫se/something", "/b♫se", "/something");
        }

        private async Task TestPathBase(string registerAddress, string requestAddress, string expectedPathBase, string expectedPath)
        {
            var config = new ConfigurationBuilder().AddInMemoryCollection(
                new Dictionary<string, string> {
                    { "server.urls", registerAddress }
                }).Build();

            var builder = new WebHostBuilder()
                .UseConfiguration(config)
                .UseServer("Microsoft.AspNetCore.Server.Kestrel")
                .Configure(app =>
                {
                    app.Run(async context =>
                    {
                        await context.Response.WriteAsync(JsonConvert.SerializeObject(new
                        {
                            PathBase = context.Request.PathBase.Value,
                            Path = context.Request.Path.Value
                        }));
                    });
                });

            using (var host = builder.Build())
            {
                host.Start();

                using (var client = new HttpClient())
                {
                    var response = await client.GetAsync(requestAddress);
                    response.EnsureSuccessStatusCode();

                    var responseText = await response.Content.ReadAsStringAsync();
                    Assert.NotEmpty(responseText);

                    var pathFacts = JsonConvert.DeserializeObject<JObject>(responseText);
                    Assert.Equal(expectedPathBase, pathFacts["PathBase"].Value<string>());
                    Assert.Equal(expectedPath, pathFacts["Path"].Value<string>());
                }
            }
        }
    }
}
