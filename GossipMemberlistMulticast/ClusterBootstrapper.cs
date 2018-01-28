using System;
using System.Collections.Generic;
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

        public async Task<Node> Bootstrap(
            Func<string, Gossiper.GossiperClient> clientFactory,
            CancellationToken cancellationToken = default)
        {
            var node = new Node(
                serviceProvider.GetRequiredService<ILogger<Node>>(),
                selfNodeInformation,
                new Dictionary<string, NodeInformation>(StringComparer.InvariantCultureIgnoreCase));

            while (!cancellationToken.IsCancellationRequested)
            {
                foreach (var seed in seedsProvider())
                {
                    try
                    {
                        var client = clientFactory(seed);

                        var synResponse = await client.Ping1Async(new RequestMessage
                        {
                            NodeId = selfNodeInformation.Id,
                            Ping1Request = new Ping1Request
                            {
                                NodePropertyVersions = selfNodeInformation.GetNodePropertyVersions()
                            }
                        });

                        var ack2Request = node.Ack1(synResponse.Ping1Response);

                        await client.Ping2Async(new RequestMessage
                        {
                            NodeId = selfNodeInformation.Id,
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
    }
}
