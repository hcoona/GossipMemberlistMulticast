using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace GossipMemberlistMulticast
{
    public class Cluster
    {
        private readonly ILogger<Cluster> logger;
        private readonly string selfNodeEndPoint;
        private readonly ClusterBootstrapper clusterBootstrapper;
        private readonly Func<string, Gossiper.GossiperClient> clientFactory;

        public Cluster(
            ILogger<Cluster> logger,
            string selfNodeEndPoint,
            ClusterBootstrapper clusterBootstrapper,
            Func<string, Gossiper.GossiperClient> clientFactory)
        {
            this.logger = logger;
            this.selfNodeEndPoint = selfNodeEndPoint;
            this.clusterBootstrapper = clusterBootstrapper;
            this.clientFactory = clientFactory;
        }

        private Node node;
        private CancellationTokenSource backgroundLoopCancellationTokenSource;
        private Task backgroundLoopTask;

        public async Task StartAsync(CancellationToken cancellationToken = default)
        {
            node = await clusterBootstrapper.Bootstrap(clientFactory, cancellationToken);

            backgroundLoopCancellationTokenSource = new CancellationTokenSource();
            backgroundLoopTask = StartBackgroundLoopAsync(backgroundLoopCancellationTokenSource.Token);

            // TODO: remove dead nodes
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
                // Probably choose non-live nodes.
                var liveNodes = node.KnownNodeInformation
                    .Where(n => n.State == NodeState.Live)
                    .Where(n => n.EndPoint != selfNodeEndPoint)
                    .ToArray();

                if (liveNodes.Any())
                {
                    var peer = liveNodes.ChooseRandom(random);
                    var client = clientFactory(peer.EndPoint.ToString());
                    // TODO: Ping etc.
                }
                else
                {
                    logger.LogWarning("There's no live nodes except than self");
                }

                await Task.Delay(1000);
            }
        }
    }
}
