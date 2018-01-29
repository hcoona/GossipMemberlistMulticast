using System;
using System.Linq;
using Microsoft.Extensions.Logging;

namespace GossipMemberlistMulticast
{
    public class NodeInformation
    {
        private static readonly string NodeStateKey = "node_state";

        private readonly ILogger<NodeInformation> logger;

        public NodeInformation(
            ILogger<NodeInformation> logger,
            string endpoint,
            NodeState nodeState)
        {
            this.logger = logger;
            this.EndPoint = endpoint;

            Properties.NodeProperties_[NodeStateKey].StateProperty = nodeState;
        }

        public string EndPoint { get; }

        public NodeProperties Properties { get; } = new NodeProperties();

        public long Version
        {
            get { return Properties.NodeProperties_.Max(p => p.Value.Version.Version_); }
            set
            {
                Properties.NodeProperties_[NodeStateKey].Version.Version_ = value;
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
