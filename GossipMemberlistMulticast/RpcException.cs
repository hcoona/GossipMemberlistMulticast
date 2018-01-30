using System;

namespace GossipMemberlistMulticast
{
    public class RpcException : Exception
    {
        public RpcException()
        {
        }

        public RpcException(string message) : base(message)
        {
        }

        public RpcException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}
