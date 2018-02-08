using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
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

        private static long GetNodeVersion() => Process.GetCurrentProcess().StartTime.ToFileTimeUtc();

        private readonly ILogger logger;
        private readonly ILogger<Node> nodeLogger;

        public TestNode(ITestOutputHelper testOutputHelper)
        {
            logger = new XUnitOutputLogger(testOutputHelper);
            nodeLogger = new Logger<Node>(new LoggerFactory(new[] { new XUnitOutputLoggerProvider(testOutputHelper) }));
        }

        private Node CreateNode(string selfEndpoint, IEnumerable<string> seedsEndpoint) =>
            Node.Create(selfEndpoint, seedsEndpoint, () => nodeLogger);

        [Fact]
        public void TestInitialState()
        {
            var node = CreateNode(SelfEndpoint, SeedsEndpoint);
            Assert.Equal(SelfEndpoint, node.EndPoint);
            Assert.Equal(SeedsEndpoint.Count() + 1, node.nodeInformationDictionary.Count);

            Assert.Empty(node.LiveEndpoints);
            Assert.Equal(SeedsEndpoint.Count(), node.NonLiveEndpoints.Count);

            var nodesSynposis = node.GetNodesSynposis();
            Assert.Equal(SeedsEndpoint.Count() + 1, nodesSynposis.Count);
            Assert.Equal(
                new NodeInformationSynopsis
                {
                    Endpoint = SelfEndpoint,
                    LastKnownPropertyVersion = 1,
                    NodeVersion = GetNodeVersion()
                },
                nodesSynposis.Single(v => v.Endpoint == SelfEndpoint));
            Assert.Equal(
                new NodeInformationSynopsis
                {
                    Endpoint = SeedsEndpoint.First(),
                    LastKnownPropertyVersion = 0,
                    NodeVersion = 0
                },
                nodesSynposis.Single(n => n.Endpoint == SeedsEndpoint.First()));
        }

        [Fact]
        public void TestAssignNodeState()
        {
            var node = CreateNode(SelfEndpoint, SeedsEndpoint);

            var peerNode = node.nodeInformationDictionary[SeedsEndpoint.First()];
            node.AssignNodeState(peerNode.Endpoint, NodeState.Live);
            Assert.Equal(NodeState.Live, peerNode.NodeState);
            Assert.Equal(1, peerNode.NodeStateProperty.Version);
            Assert.Equal(peerNode.Endpoint, node.LiveEndpoints.Single());
        }

        [Fact]
        public void TestSyn()
        {
            const string node1Endpoint = "192.168.1.1:18841";
            const string node2Endpoint = "192.168.1.2:18841";

            var node1 = CreateNode(node1Endpoint, new[] { node2Endpoint }.Concat(SeedsEndpoint));
            var node2 = CreateNode(node2Endpoint, new[] { node1Endpoint }.Concat(SeedsEndpoint));

            var synRequest = new Ping1Request();
            synRequest.NodesSynopsis.AddRange(node1.GetNodesSynposis());

            var synResponse = node2.Syn(synRequest);
            Assert.Equal(
                new NodeInformationSynopsis
                {
                    Endpoint = node1.EndPoint,
                    LastKnownPropertyVersion = 0,
                    NodeVersion = 0
                },
                synResponse.RequiredNodesSynopsis.Single());
            Assert.Equal(
                node2.nodeInformationDictionary[node2.EndPoint],
                synResponse.UpdatedNodes.Single());
        }

        [Fact]
        public void TestFullSyncBasic()
        {
            const string node1Endpoint = "192.168.1.1:18841";
            const string node2Endpoint = "192.168.1.2:18841";

            var node1 = CreateNode(node1Endpoint, new[] { node2Endpoint }.Concat(SeedsEndpoint));
            var node2 = CreateNode(node2Endpoint, new[] { node1Endpoint }.Concat(SeedsEndpoint));

            var synRequest = new Ping1Request();
            synRequest.NodesSynopsis.AddRange(node1.GetNodesSynposis());

            var synResponse = node2.Syn(synRequest);

            var ack2Request = node1.Ack1(synResponse);
            var ack2Response = node2.Ack2(ack2Request);

            Assert.Equal(new Ping2Response(), ack2Response);

            Assert.Equal(
                new NodeInformation
                {
                    Endpoint = node1Endpoint,
                    NodeStateProperty = new VersionedProperty
                    {
                        StateProperty = NodeState.Live,
                        Version = 2
                    },
                    NodeVersion = GetNodeVersion(),
                },
                node1.nodeInformationDictionary[node1Endpoint]);
            Assert.Equal(
                new NodeInformation
                {
                    Endpoint = node2Endpoint,
                    NodeStateProperty = new VersionedProperty
                    {
                        StateProperty = NodeState.Live,
                        Version = 2
                    },
                    NodeVersion = GetNodeVersion(),
                },
                node1.nodeInformationDictionary[node2Endpoint]);
            foreach (var e in SeedsEndpoint)
            {
                Assert.Equal(
                    NodeInformation.CreateSeedNode(e),
                    node1.nodeInformationDictionary[e]);
            }

            Assert.Equal(
                new NodeInformation
                {
                    Endpoint = node1Endpoint,
                    NodeStateProperty = new VersionedProperty
                    {
                        StateProperty = NodeState.Live,
                        Version = 2
                    },
                    NodeVersion = GetNodeVersion(),
                },
                node2.nodeInformationDictionary[node1Endpoint]);
            Assert.Equal(
                new NodeInformation
                {
                    Endpoint = node2Endpoint,
                    NodeStateProperty = new VersionedProperty
                    {
                        StateProperty = NodeState.Live,
                        Version = 2
                    },
                    NodeVersion = GetNodeVersion(),
                },
                node2.nodeInformationDictionary[node2Endpoint]);
            foreach (var e in SeedsEndpoint)
            {
                Assert.Equal(
                    NodeInformation.CreateSeedNode(e),
                    node2.nodeInformationDictionary[e]);
            }
        }

        [Fact]
        public void PrintMessageSizeForClusterSize()
        {
            var node = CreateNode(SelfEndpoint, SeedsEndpoint);
            var baseIpAddress = IPAddress.Parse("192.168.1.1");
            for (int i = 0; i < 10000; i++)
            {
#pragma warning disable CS0618
                baseIpAddress.Address++;
#pragma warning restore CS0618
                var endpoint = baseIpAddress.ToString() + ":19999";
                node.nodeInformationDictionary.Add(
                    endpoint,
                    NodeInformation.CreateSelfNode(endpoint));
            }

            var ping1Request = new Ping1Request
            {
                NodesSynopsis = { node.GetNodesSynposis() }
            };
            logger.LogInformation("Ping1 request size: {0}", ping1Request.CalculateSize());
        }
    }
}
