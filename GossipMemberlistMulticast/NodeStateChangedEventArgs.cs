using System;

namespace GossipMemberlistMulticast
{
    public class NodeStateChangedEventArgs : EventArgs
    {
        public NodeState PreviousNodeState { get; set; }

        public NodeState CurrentNodeState { get; set; }
    }
}
