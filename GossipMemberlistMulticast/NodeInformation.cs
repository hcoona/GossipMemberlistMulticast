using System.Diagnostics;
using System.Linq;
using Microsoft.Extensions.Logging;

namespace GossipMemberlistMulticast
{
    public partial class NodeInformation
    {
        private static readonly string NodeStateKey = "node_state";

        public static NodeInformation CreateSelfNode(string endpoint) =>
            new NodeInformation
            {
                Endpoint = endpoint,
                NodeVersion = Process.GetCurrentProcess().StartTime.ToFileTimeUtc(),
                NodeStateProperty = new VersionedProperty
                {
                    Version = 1,
                    StateProperty = NodeState.Live
                }
            };

        public static NodeInformation CreateSeedNode(string endpoint) =>
            new NodeInformation
            {
                Endpoint = endpoint,
                NodeVersion = 0,
                NodeStateProperty = new VersionedProperty
                {
                    Version = 0,
                    StateProperty = NodeState.Unknown
                }
            };

        public VersionedProperty NodeStateProperty
        {
            get { return this.Properties[NodeStateKey]; }
            set { this.Properties[NodeStateKey] = value; }
        }

        public NodeState NodeState
        {
            get { return this.NodeStateProperty.StateProperty; }
            set { this.NodeStateProperty.StateProperty = value; }
        }

        public long LastKnownPropertyVersion => this.Properties.Max(p => p.Value.Version);

        public void BumpVersion()
        {
            this.NodeStateProperty.Version = LastKnownPropertyVersion + 1;
        }

        public NodeInformationSynopsis GetSynopsis() => new NodeInformationSynopsis
        {
            Endpoint = this.Endpoint,
            NodeVersion = this.NodeVersion,
            LastKnownPropertyVersion = this.LastKnownPropertyVersion
        };

        public void Update(NodeInformation other, ILogger logger)
        {
            using (logger.BeginScope("EndPoint={0}", this.Endpoint))
            {
                if (this.NodeVersion == other.NodeVersion)
                {
                    foreach (var p in other.Properties)
                    {
                        if (this.Properties.ContainsKey(p.Key))
                        {
                            if (this.Properties[p.Key].Version < p.Value.Version)
                            {
                                this.Properties[p.Key] = p.Value;
                                logger.LogDebug("Property {0} updated to value {1}", p.Key, p.Value);
                            }
                            else if (this.Properties[p.Key].Version > p.Value.Version)
                            {
                                logger.LogInformation(
                                    "Discard incoming node property {0} because our version is higher ({1} > {2})",
                                    p.Key,
                                    this.Properties[p.Key].Version,
                                    p.Value.Version);
                            }
                            else
                            {
                                logger.LogInformation(
                                    "Discard incoming node property {0} because the version is same ({1})",
                                    p.Key,
                                    p.Value.Version);
                            }
                        }
                        else
                        {
                            this.Properties.Add(p.Key, p.Value);
                            logger.LogDebug("Property {0} added to value {1}", p.Key, p.Value);
                        }
                    }
                }
                else if (this.NodeVersion < other.NodeVersion)
                {
                    this.NodeVersion = other.NodeVersion;
                    this.Properties.Clear();
                    this.Properties.Add(other.Properties);
                }
                else
                {
                    logger.LogInformation(
                        "Discard incoming node information because our node version is higher ({0} > {1})",
                        this.NodeVersion,
                        other.NodeVersion);
                }
            }
        }

        public NodeInformation GetDelta(NodeInformationSynopsis otherSynopsis, ILogger logger)
        {
            var result = new NodeInformation
            {
                Endpoint = this.Endpoint,
                NodeVersion = this.NodeVersion
            };

            if (this.NodeVersion < otherSynopsis.NodeVersion)
            {
                // Should not get here.
            }
            else if (this.NodeVersion > otherSynopsis.NodeVersion)
            {
                result.Properties.Add(this.Properties);
            }
            else
            {
                foreach (var p in this.Properties)
                {
                    if (p.Value.Version > otherSynopsis.LastKnownPropertyVersion)
                    {
                        result.Properties.Add(p.Key, p.Value);
                    }
                }
            }
            return result;
        }
    }
}
