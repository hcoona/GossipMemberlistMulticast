using System;
using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;

namespace GossipMemberlistMulticast.Tests
{
    public class TestNodeInformation
    {
        private readonly ITestOutputHelper testOutputHelper;
        private readonly IServiceProvider container;

        public TestNodeInformation(ITestOutputHelper testOutputHelper)
        {
            this.testOutputHelper = testOutputHelper;

            var services = new ServiceCollection();
            services.AddLogging(b => b.AddProvider(new XUnitOutputLoggerProvider(testOutputHelper)));
            this.container = services.BuildServiceProvider();
        }

        [Fact]
        public void TestSelfNodeInitialState()
        {
            const string endpoint = "127.0.0.1:12251";

            var n = NodeInformation.CreateSelfNode(endpoint);
            Assert.Equal(endpoint, n.Endpoint);
            Assert.Equal(NodeState.Live, n.NodeState);
            Assert.Equal(Process.GetCurrentProcess().StartTime.ToFileTimeUtc(), n.NodeVersion);
            Assert.Equal(1, n.LastKnownPropertyVersion);
            Assert.Single(n.Properties);
        }

        [Fact]
        public void TestSeedNodeInitialState()
        {
            const string endpoint = "127.0.0.1:12251";

            var n = NodeInformation.CreateSeedNode(endpoint);
            Assert.Equal(endpoint, n.Endpoint);
            Assert.Equal(NodeState.Unknown, n.NodeState);
            Assert.Equal(0, n.NodeVersion);
            Assert.Equal(0, n.LastKnownPropertyVersion);
            Assert.Single(n.Properties);
        }
    }
}
