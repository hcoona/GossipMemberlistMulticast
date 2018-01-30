using System;

namespace GossipMemberlistMulticast
{
    public class RpcInvalidResponseException : RpcException
    {
        public RpcInvalidResponseException()
        {
        }

        public RpcInvalidResponseException(string message) : base(message)
        {
        }

        public RpcInvalidResponseException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}
