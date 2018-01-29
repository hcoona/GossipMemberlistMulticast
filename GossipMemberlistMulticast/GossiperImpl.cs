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

        public override Task<Ping1Response> Ping1(Ping1Request request, ServerCallContext context)
        {
            return Task.FromResult(node.Syn(request));
        }

        public override Task<Ping2Response> Ping2(Ping2Request request, ServerCallContext context)
        {
            return Task.FromResult(node.Ack2(request));
        }

        public override async Task<ForwardResponse> Forward(ForwardRequest request, ServerCallContext context)
        {
            logger.LogDebug("Forward to {0} with method {1}", request.TargetEndpoint, request.TargetMethod);
            var client = clientFactory(request.TargetEndpoint);

            switch (request.TargetMethod)
            {
                case "Ping1":
                    return new ForwardResponse
                    {
                        Ping1Response = await client.Ping1Async(
                            request.Ping1Request,
                            deadline: context?.Deadline ?? DateTime.UtcNow + TimeSpan.FromMilliseconds(200),
                            cancellationToken: context?.CancellationToken ?? default)
                    };
                case "Ping2":
                    return new ForwardResponse
                    {
                        Ping2Response = await client.Ping2Async(
                            request.Ping2Request,
                            deadline: context?.Deadline ?? DateTime.UtcNow + TimeSpan.FromMilliseconds(200),
                            cancellationToken: context?.CancellationToken ?? default)
                    };
                default:
                    logger.LogError("Unrecongnized forwarding method {0}", request.TargetMethod);
                    return new ForwardResponse
                    {
                        ErrorMessage = string.Format("Unrecongnized forwarding method {0}", request.TargetMethod)
                    };
            }
        }
    }
}
