using System;
using System.Threading.Tasks;
using Grpc.Core;
using Microsoft.Extensions.Logging;

namespace GossipMemberlistMulticast
{
    public class GossiperImpl : Gossiper.GossiperBase
    {
        private readonly ILogger<GossiperImpl> logger;
        private readonly Func<string, Gossiper.GossiperClient> clientFactory;
        private readonly Node node;

        public GossiperImpl(
            ILogger<GossiperImpl> logger,
            Func<string, Gossiper.GossiperClient> clientFactory,
            Node node)
        {
            this.logger = logger;
            this.clientFactory = clientFactory;
            this.node = node;
        }

        public override Task<ResponseMessage> Ping1(RequestMessage request, ServerCallContext context)
        {
            return Task.FromResult(new ResponseMessage
            {
                NodeId = node.Id,
                Ping1Response = node.Syn(request.Ping1Request)
            });
        }

        public override Task<ResponseMessage> Ping2(RequestMessage request, ServerCallContext context)
        {
            return Task.FromResult(new ResponseMessage
            {
                NodeId = node.Id,
                Ping2Response = node.Ack2(request.Ping2Request)
            });
        }

        public override async Task<ForwardResponse> Forward(ForwardRequest request, ServerCallContext context)
        {
            logger.LogDebug("Forward to {0} with method {1}", request.TargetEndpoint, request.TargetMethod);
            var client = clientFactory(request.TargetEndpoint);

            AsyncUnaryCall<ResponseMessage> forwardTask;
            switch (request.TargetMethod)
            {
                case "Ping1":
                    forwardTask = client.Ping1Async(
                        request.RequestMessage,
                        deadline: context.Deadline,
                        cancellationToken: context.CancellationToken);
                    break;
                case "Ping2":
                    forwardTask = client.Ping2Async(
                        request.RequestMessage,
                        deadline: context.Deadline,
                        cancellationToken: context.CancellationToken);
                    break;
                default:
                    logger.LogError("Unrecongnized forwarding method {0}", request.TargetMethod);
                    return new ForwardResponse
                    {
                        ErrorMessage = string.Format("Unrecongnized forwarding method {0}", request.TargetMethod)
                    };
            }

            return new ForwardResponse
            {
                ResponseMessage = await forwardTask
            };
        }
    }
}
