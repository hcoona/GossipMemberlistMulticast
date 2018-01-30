using System;

namespace GossipMemberlistMulticast
{
    public class RpcRemoteException : RpcException
    {
        public RpcRemoteException()
        {
        }

        public RpcRemoteException(string message) : base(message)
        {
        }

        public RpcRemoteException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}
