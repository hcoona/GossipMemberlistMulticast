using System;
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;

namespace GossipMemberlistMulticast.Tests
{
    public class TestNodeInformation
    {
        private const string endpoint = "127.0.0.1:12251";

        private readonly ILogger logger;

        public TestNodeInformation(ITestOutputHelper testOutputHelper)
        {
            logger = new XUnitOutputLogger(testOutputHelper);
        }

        [Fact]
        public void TestSelfNodeInitialState()
        {
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
            var n = NodeInformation.CreateSeedNode(endpoint);
            Assert.Equal(endpoint, n.Endpoint);
            Assert.Equal(NodeState.Unknown, n.NodeState);
            Assert.Equal(0, n.NodeVersion);
            Assert.Equal(0, n.LastKnownPropertyVersion);
            Assert.Single(n.Properties);
        }

        [Fact]
        public void TestBumpVersion()
        {
            var n = NodeInformation.CreateSelfNode(endpoint);
            n.Properties.Add("test_key", new VersionedProperty
            {
                Version = 5,
                StringProperty = "test_value"
            });

            Assert.Equal(5, n.LastKnownPropertyVersion);

            n.BumpVersion();
            Assert.Equal(6, n.LastKnownPropertyVersion);

            n.Properties.Add("test_key2", new VersionedProperty
            {
                Version = 9,
                StringProperty = "test_value2"
            });

            Assert.Equal(9, n.LastKnownPropertyVersion);

            n.BumpVersion();
            Assert.Equal(10, n.LastKnownPropertyVersion);

            n.Properties["test_key"].Version = 7;
            Assert.Equal(10, n.LastKnownPropertyVersion);

            n.BumpVersion();
            Assert.Equal(11, n.LastKnownPropertyVersion);
        }

        [Fact]
        public void TestGetSynopsis()
        {
            var n = NodeInformation.CreateSelfNode(endpoint);
            n.Properties.Add(new Dictionary<string, VersionedProperty>
            {
                {
                    "test_key",
                    new VersionedProperty
                    {
                        Version = 5,
                        StringProperty = "test_value"
                    }
                },
                {
                    "test_key2",
                    new VersionedProperty
                    {
                        Version = 9,
                        StringProperty = "test_value2"
                    }
                }
            });

            var ns = n.GetSynopsis();
            Assert.Equal(endpoint, ns.Endpoint);
            Assert.Equal(Process.GetCurrentProcess().StartTime.ToFileTimeUtc(), ns.NodeVersion);
            Assert.Equal(9, ns.LastKnownPropertyVersion);
        }

        [Fact]
        public void TestUpdateInitialSeedNode()
        {
            var n = NodeInformation.CreateSeedNode(endpoint);
            var n2 = NodeInformation.CreateSelfNode(endpoint);

            n.Update(n2, logger);
            Assert.Equal(n2.NodeVersion, n.NodeVersion);
            Assert.Equal(n2.NodeState, n.NodeState);
            Assert.Equal(n2.LastKnownPropertyVersion, n.LastKnownPropertyVersion);
            Assert.Equal(n2.Properties.Count, n.Properties.Count);

            n2.Properties.Add(new Dictionary<string, VersionedProperty>
            {
                {
                    "test_key",
                    new VersionedProperty
                    {
                        Version = 5,
                        StringProperty = "test_value"
                    }
                },
                {
                    "test_key2",
                    new VersionedProperty
                    {
                        Version = 9,
                        StringProperty = "test_value2"
                    }
                }
            });
            n2.BumpVersion();

            n.Update(n2, logger);
            Assert.Equal(n2.NodeVersion, n.NodeVersion);
            Assert.Equal(n2.NodeState, n.NodeState);
            Assert.Equal(n2.LastKnownPropertyVersion, n.LastKnownPropertyVersion);
            Assert.Equal(n2.Properties, n.Properties);
        }
    }
}
