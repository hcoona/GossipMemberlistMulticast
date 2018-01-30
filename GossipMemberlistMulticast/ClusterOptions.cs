namespace GossipMemberlistMulticast
{
    public class ClusterOptions
    {
        public long GossipIntervalMilliseconds { get; set; }

        public double GossipNonLiveNodesPossibility { get; set; }

        public int PingTimeoutMilliseconds { get; set; }

        public int ForwardTimeoutMilliseconds { get; set; }
    }
}
