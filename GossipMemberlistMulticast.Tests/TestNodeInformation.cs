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

        private NodeInformation CreateNodeInformation(string endpoint, NodeState nodeState)
        {
            return new NodeInformation
            {
                Endpoint = endpoint,
                NodeVersion = Process.GetCurrentProcess().StartTime.ToFileTimeUtc(),
                NodeStateProperty = new VersionedProperty
                {
                    Version = 1,
                    StateProperty = nodeState
                }
            };
        }

        [Fact]
        public void TestInitialState()
        {
            var endpoint = "127.0.0.1:1080";
            var nodeState = NodeState.Live;

            var n = CreateNodeInformation(endpoint, nodeState);
            Assert.Equal(endpoint, n.Endpoint);
            Assert.Equal(nodeState, n.NodeState);
            Assert.Equal(Process.GetCurrentProcess().StartTime.ToFileTimeUtc(), n.NodeVersion);
            Assert.Equal(1, n.LastKnownPropertyVersion);
            Assert.Single(n.Properties);
        }
    }
}
