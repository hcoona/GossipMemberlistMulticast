using System;

namespace GossipMemberlistMulticast
{
    public class NodeStateChangedEventArgs : EventArgs
    {
        public string EndPoint { get; set; }

        public NodeState PreviousNodeState { get; set; }

        public NodeState CurrentNodeState { get; set; }
    }
}
