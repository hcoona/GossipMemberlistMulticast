using System.Collections.Generic;
using System.Diagnostics;
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

        [Fact]
        public void TestUpdateOldNodeInformation()
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

            var n2 = NodeInformation.CreateSelfNode(endpoint);
            n2.Properties.Add(new Dictionary<string, VersionedProperty>
            {
                {
                    "test_key",
                    new VersionedProperty
                    {
                        Version = 5,
                        StringProperty = "test_value"
                    }
                }
            });

            Assert.NotEqual(n2, n);

            var originN = n.Clone();
            n.Update(n2, logger);

            Assert.NotEqual(n2, n);
            Assert.Equal(originN, n);
        }

        [Fact]
        public void TestUpdateNewNodeInformation()
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

            var n2 = NodeInformation.CreateSelfNode(endpoint);
            n2.NodeVersion += 1;
            n2.Properties.Add(new Dictionary<string, VersionedProperty>
            {
                {
                    "test_key",
                    new VersionedProperty
                    {
                        Version = 2,
                        StringProperty = "test_value"
                    }
                }
            });

            Assert.NotEqual(n2, n);

            var originN = n.Clone();
            n.Update(n2, logger);

            Assert.Equal(n2, n);
            Assert.NotEqual(originN, n);
        }

        [Fact]
        public void TestUpdateDeltaNodeInformation()
        {
            var n = NodeInformation.CreateSelfNode(endpoint);
            n.Properties.Add(new Dictionary<string, VersionedProperty>
            {
                {
                    "test_key1",
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
                },
                {
                    "test_key3",
                    new VersionedProperty
                    {
                        Version = 10,
                        StringProperty = "test_value3"
                    }
                }
            });

            var n2 = n.Clone();
            n2.Properties["test_key1"] = new VersionedProperty
            {
                Version = 11,
                StringProperty = "test_value1"
            };
            n2.Properties.Remove("test_key2");
            n2.Properties["test_key3"] = new VersionedProperty
            {
                Version = 6,
                StringProperty = "test_value3_old"
            };
            n2.Properties.Add("test_key4", new VersionedProperty
            {
                Version = 12,
                StringProperty = "test_value4"
            });

            var originN = n.Clone();
            n.Update(n2, logger);
            Assert.Equal(originN.NodeVersion, n.NodeVersion);
            Assert.Equal(n2.LastKnownPropertyVersion, n.LastKnownPropertyVersion);
            Assert.Equal(5, n.Properties.Count);
            Assert.Equal(n2.NodeStateProperty, n.NodeStateProperty);
            Assert.Equal(n2.Properties["test_key1"], n.Properties["test_key1"]);
            Assert.Equal(originN.Properties["test_key2"], n.Properties["test_key2"]);
            Assert.Equal(originN.Properties["test_key3"], n.Properties["test_key3"]);
            Assert.Equal(n2.Properties["test_key4"], n.Properties["test_key4"]);
        }

        [Fact]
        public void TestGetDeltaFromOldNode()
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

            var n2 = new NodeInformationSynopsis
            {
                Endpoint = endpoint,
                LastKnownPropertyVersion = 100,
                NodeVersion = n.NodeVersion - 1
            };

            var deltaN = n.GetDelta(n2, logger);
            Assert.Equal(n, deltaN);
        }

        [Fact]
        public void TestGetDeltaFromPeerNode()
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

            var n2 = new NodeInformationSynopsis
            {
                Endpoint = endpoint,
                LastKnownPropertyVersion = 6,
                NodeVersion = n.NodeVersion
            };

            var deltaN = n.GetDelta(n2, logger);
            Assert.Single(deltaN.Properties);
            Assert.Equal(n.Properties["test_key2"], deltaN.Properties["test_key2"]);
        }
    }
}
