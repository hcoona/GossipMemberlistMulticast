using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;

namespace GossipMemberlistMulticast.Tests
{
    public class TestNode
    {
        private const string SelfEndpoint = "127.0.0.1:12251";
        private static readonly IEnumerable<string> SeedsEndpoint =
            Enumerable.Range(2, 4).Select(i => $"127.0.0.1:1225{i}").ToArray();

        private readonly ILogger logger;
        private readonly ILogger<Node> nodeLogger;

        public TestNode(ITestOutputHelper testOutputHelper)
        {
            logger = new XUnitOutputLogger(testOutputHelper);
            nodeLogger = new Logger<Node>(new LoggerFactory(new[] { new XUnitOutputLoggerProvider(testOutputHelper) }));
        }

        private Node CreateNode()
        {
            var selfNodeInformation = NodeInformation.CreateSelfNode(SelfEndpoint);
            var nodeInformationDictionary = new Dictionary<string, NodeInformation>
            {
                { SelfEndpoint, selfNodeInformation }
            };
            foreach (var e in SeedsEndpoint)
            {
                nodeInformationDictionary.Add(e, NodeInformation.CreateSeedNode(e));
            }
            return new Node(
                nodeLogger,
                selfNodeInformation,
                nodeInformationDictionary);
        }

        [Fact]
        public void TestInitialState()
        {
            var n = CreateNode();
            Assert.Equal(SelfEndpoint, n.EndPoint);
            Assert.Equal(SeedsEndpoint.Count() + 1, n.nodeInformationDictionary.Count);

            Assert.Empty(n.LiveEndpoints);
            Assert.Equal(SeedsEndpoint.Count(), n.NonLiveEndpoints.Count);

            var nodesSynposis = n.GetNodesSynposis();
            Assert.Equal(SeedsEndpoint.Count() + 1, nodesSynposis.Count);
            Assert.Equal(
                new NodeInformationSynopsis
                {
                    Endpoint = SelfEndpoint,
                    LastKnownPropertyVersion = 1,
                    NodeVersion = Process.GetCurrentProcess().StartTime.ToFileTimeUtc()
                },
                nodesSynposis.Single(v => v.Endpoint == SelfEndpoint));
            Assert.Equal(
                new NodeInformationSynopsis
                {
                    Endpoint = SeedsEndpoint.First(),
                    LastKnownPropertyVersion = 0,
                    NodeVersion = 0
                },
                nodesSynposis.Single(v => v.Endpoint == SeedsEndpoint.First()));
        }
    }
}
