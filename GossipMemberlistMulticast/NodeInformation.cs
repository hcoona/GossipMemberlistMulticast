using System;
using System.Linq;
using System.Net;
using Microsoft.Extensions.Logging;

namespace GossipMemberlistMulticast
{
    public class NodeInformation
    {
        private static readonly string NodeStateKey = "node_state";
        private static readonly string NodeEndPointKey = "node_endpoint";

        private readonly ILogger<NodeInformation> logger;

        public NodeInformation(
            ILogger<NodeInformation> logger,
            string id)
        {
            this.logger = logger;
            this.Id = id;
        }

        public string Id { get; }

        public NodeProperties Properties { get; } = new NodeProperties();

        public long Version
        {
            get { return Properties.NodeProperties_.Max(p => p.Value.Version.Version_); }
            set
            {
                Properties.NodeProperties_[NodeStateKey].Version.Version_ = value;
                Properties.NodeProperties_[NodeEndPointKey].Version.Version_ = value;
            }
        }

        public NodeState State
        {
            get
            {
                return Properties.NodeProperties_[NodeStateKey].StateProperty;
            }
            set
            {
                var state = Properties.NodeProperties_[NodeStateKey].StateProperty;
                if (state != value)
                {
                    Properties.NodeProperties_[NodeStateKey].StateProperty = value;
                    Properties.NodeProperties_[NodeStateKey].Version.Version_ = ++this.Version;
                    OnStateChanged?.Invoke(this, new NodeStateChangedEventArgs
                    {
                        PreviousNodeState = state,
                        CurrentNodeState = value
                    });
                }
            }
        }

        public event EventHandler<NodeStateChangedEventArgs> OnStateChanged;

        public IPEndPoint EndPoint
        {
            get
            {
                var g = Properties.NodeProperties_[NodeEndPointKey].StringProperty.Split(new[] { ':' }, 2);
                return new IPEndPoint(IPAddress.Parse(g[0]), int.Parse(g[1]));
            }
        }

        public NodePropertyVersions GetNodePropertyVersions()
        {
            var result = new NodePropertyVersions();
            foreach (var p in Properties.NodeProperties_)
            {
                result.NodePropertyVersions_.Add(p.Key, p.Value.Version);
            }
            return result;
        }

        public NodeProperties GetPropertiesAfterVersion(long version)
        {
            var deltaNodeProperties = new NodeProperties();
            foreach (var p in Properties.NodeProperties_.Where(p => p.Value.Version.Version_ > version))
            {
                deltaNodeProperties.NodeProperties_.Add(p.Key, p.Value);
            }
            return deltaNodeProperties;
        }

        public NodeProperties UpdateProperties(NodeProperties nodeProperties)
        {
            var deltaNodeProperties = new NodeProperties();
            foreach (var p in nodeProperties.NodeProperties_)
            {
                var myProperty = Properties.NodeProperties_[p.Key];
                var theirProperty = p.Value;

                if (myProperty.Version.Version_ < theirProperty.Version.Version_)
                {
                    myProperty = theirProperty;
                    if (p.Key == NodeStateKey && myProperty.StateProperty != theirProperty.StateProperty)
                    {
                        OnStateChanged?.Invoke(this, new NodeStateChangedEventArgs
                        {
                            PreviousNodeState = myProperty.StateProperty,
                            CurrentNodeState = theirProperty.StateProperty
                        });
                    }
                }
                else if (myProperty.Version.Version_ > theirProperty.Version.Version_)
                {
                    deltaNodeProperties.NodeProperties_.Add(p.Key, myProperty);
                }
            }
            return deltaNodeProperties;
        }
    }
}
