using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace GossipMemberlistMulticast
{
    public class Cluster
    {
        private readonly IServiceProvider serviceProvider;
        private readonly ILogger<Cluster> logger;
        private readonly string selfNodeEndPoint;
        private readonly Func<IEnumerable<string>> seedsProvider;
        private readonly Func<string, Gossiper.GossiperClient> clientFactory;

        public Cluster(
            IServiceProvider serviceProvider,
            ILogger<Cluster> logger,
            string selfNodeEndPoint,
            Func<IEnumerable<string>> seedsProvider,
            Func<string, Gossiper.GossiperClient> clientFactory)
        {
            this.serviceProvider = serviceProvider;
            this.logger = logger;
            this.selfNodeEndPoint = selfNodeEndPoint;
            this.seedsProvider = seedsProvider;
            this.clientFactory = clientFactory;
        }

        private Node node;
        private CancellationTokenSource backgroundLoopCancellationTokenSource;
        private Task backgroundLoopTask;

        public Task StartAsync(CancellationToken cancellationToken = default)
        {
            var selfNodeInformation = new NodeInformation(
                serviceProvider.GetRequiredService<ILogger<NodeInformation>>(),
                selfNodeEndPoint,
                NodeState.Live);
            var seedsNodeInformation = seedsProvider()
                .Select(endpoint => new NodeInformation(
                    serviceProvider.GetRequiredService<ILogger<NodeInformation>>(),
                    endpoint,
                    NodeState.Unknown))
                .ToArray();

            var nodeInformationDictionary = new Dictionary<string, NodeInformation>(StringComparer.InvariantCultureIgnoreCase)
            {
                { selfNodeInformation.EndPoint, selfNodeInformation }
            };
            foreach (var n in seedsNodeInformation)
            {
                nodeInformationDictionary.Add(n.EndPoint, n);
            }

            node = new Node(
                serviceProvider.GetRequiredService<ILogger<Node>>(),
                selfNodeInformation,
                nodeInformationDictionary);

            backgroundLoopCancellationTokenSource = new CancellationTokenSource();
            backgroundLoopTask = StartBackgroundLoopAsync(backgroundLoopCancellationTokenSource.Token);

            // TODO: remove dead nodes

            return Task.FromResult<object>(null);
        }

        public Task StopAsync(CancellationToken cancellationToken = default)
        {
            backgroundLoopCancellationTokenSource.Cancel();
            backgroundLoopTask.Wait(cancellationToken);
            backgroundLoopTask.Dispose();
            return Task.FromResult<object>(null);
        }

        private Task StartBackgroundLoopAsync(CancellationToken cancellationToken)
        {
            return Task.Factory.StartNew(
                () => BackgroundLoop(cancellationToken),
                cancellationToken,
                TaskCreationOptions.LongRunning,
                TaskScheduler.Current);
        }

        private async Task BackgroundLoop(CancellationToken cancellationToken)
        {
            var random = new Random();
            while (!cancellationToken.IsCancellationRequested)
            {
                var liveNodes = node.KnownNodeInformation
                    .Where(n => n.State == NodeState.Live)
                    .Where(n => n.EndPoint != selfNodeEndPoint)
                    .ToArray();
                var nonLiveNodes = node.KnownNodeInformation
                    .Where(n => n.State != NodeState.Live)
                    .ToArray();

                if (liveNodes.Any() && nonLiveNodes.Any())
                {
                    // Probably choose non-live nodes.
                    NodeInformation peer;
                    // TODO: Read from configuration
                    if (random.NextDouble() > 0.1)
                    {
                        peer = liveNodes.ChooseRandom(random);
                    }
                    else
                    {
                        peer = nonLiveNodes.ChooseRandom(random);
                    }

                    try
                    {
                        await SyncWithPeerAsync(peer);
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(default, ex, "Failed to connect to peer {0} because {1}", peer.EndPoint, ex);
                        // TODO: Mark suspect & start probing
                        peer.State = NodeState.Dead;
                    }
                }
                else if (nonLiveNodes.Any())
                {
                    logger.LogWarning("There's no live nodes except than self");
                }
                else
                {
                    logger.LogInformation("There's no non-live nodes");
                }

                await Task.Delay(1000);
            }
        }

        private Task SyncWithPeerAsync(NodeInformation peer)
        {
            var client = clientFactory(peer.EndPoint.ToString());
            // Ping/Forward
            throw new NotImplementedException();
        }
    }
}
