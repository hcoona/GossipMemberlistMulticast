using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace GossipMemberlistMulticast
{
    public class ClusterBootstrapper
    {
        private readonly IServiceProvider serviceProvider;
        private readonly ILogger<ClusterBootstrapper> logger;
        private readonly Func<IEnumerable<string>> seedsProvider;
        private readonly NodeInformation selfNodeInformation;

        public ClusterBootstrapper(
            IServiceProvider serviceProvider,
            ILogger<ClusterBootstrapper> logger,
            Func<IEnumerable<string>> seedsProvider,
            NodeInformation selfNodeInformation)
        {
            this.serviceProvider = serviceProvider;
            this.logger = logger;
            this.seedsProvider = seedsProvider;
            this.selfNodeInformation = selfNodeInformation;
        }

        public async Task<Node> Bootstrap(
            Func<string, Gossiper.GossiperClient> clientFactory,
            CancellationToken cancellationToken = default)
        {
            var node = new Node(
                serviceProvider.GetRequiredService<ILogger<Node>>(),
                selfNodeInformation,
                new Dictionary<string, NodeInformation>(StringComparer.InvariantCultureIgnoreCase)
                {
                    { selfNodeInformation.EndPoint, selfNodeInformation }
                });

            while (!cancellationToken.IsCancellationRequested)
            {
                foreach (var seed in seedsProvider())
                {
                    try
                    {
                        var client = clientFactory(seed);

                        var synResponse = await client.Ping1Async(new RequestMessage
                        {
                            NodeEndpoint = selfNodeInformation.EndPoint,
                            Ping1Request = new Ping1Request
                            {
                                NodePropertyVersions = selfNodeInformation.GetNodePropertyVersions()
                            }
                        });

                        var ack2Request = node.Ack1(synResponse.Ping1Response);

                        await client.Ping2Async(new RequestMessage
                        {
                            NodeEndpoint = selfNodeInformation.EndPoint,
                            Ping2Request = ack2Request
                        });

                        logger.LogInformation("Bootstrapping succeed with seed node {0}", seed);
                        return node;
                    }
                    catch (Exception ex)
                    {
                        logger.LogWarning(default, ex, "Failed to connect to seed {0}, because {1}", seed, ex);
                    }
                }
            }
            throw new TaskCanceledException();
        }

        public async Task<Node> BootstrapSeed(
            CancellationToken cancellationToken = default)
        {
            seedsProvider().Select(v => new NodeInformation(
                serviceProvider.GetRequiredService<ILogger<NodeInformation>>(),
                v,

                ))
            return new Node(
                serviceProvider.GetRequiredService<ILogger<Node>>(),
                selfNodeInformation,
                new Dictionary<string, NodeInformation>(StringComparer.InvariantCultureIgnoreCase)
                {
                    { selfNodeInformation.EndPoint, selfNodeInformation }
                });
        }
    }
}
