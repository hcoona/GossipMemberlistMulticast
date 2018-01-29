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
                NodeInformation peer = PickRandomNode(random);

                if (peer != null)
                {
                    try
                    {
                        await SyncWithPeerAsync(peer, random, cancellationToken);
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(default, ex, "Failed to connect to peer {0} because {1}", peer.EndPoint, ex);
                        // TODO: Mark suspect & start probing
                        peer.State = NodeState.Dead;
                    }
                }

                await Task.Delay(1000);
            }
        }

        private NodeInformation PickRandomNode(Random random)
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
                // TODO: Read from configuration
                if (random.NextDouble() > 0.1)
                {
                    return liveNodes.ChooseRandom(random);
                }
                else
                {
                    return nonLiveNodes.ChooseRandom(random);
                }
            }
            else if (nonLiveNodes.Any())
            {
                logger.LogWarning("There's no live nodes except than self");
                return nonLiveNodes.ChooseRandom(random);
            }
            else if (liveNodes.Any())
            {
                logger.LogInformation("There's no non-live nodes");
                return liveNodes.ChooseRandom(random);
            }
            else
            {
                logger.LogWarning("There are no other nodes than self");
                return null;
            }
        }

        private async Task SyncWithPeerAsync(NodeInformation peer, Random random, CancellationToken cancellationToken)
        {
            var client = clientFactory.Invoke(peer.EndPoint.ToString());
            var synRequest = new RequestMessage
            {
                NodeEndpoint = selfNodeEndPoint,
                Ping1Request = new Ping1Request
                {
                    NodePropertyVersions = node.GetNodePropertyVersions()
                }
            };

            bool failed = false;
            try
            {
                var synResponse = await client.Ping1Async(synRequest, cancellationToken: cancellationToken);
                var ack2Request = node.Ack1(synResponse.Ping1Response);

                var ack2Response = await client.Ping2Async(
                    new RequestMessage
                    {
                        NodeEndpoint = selfNodeEndPoint,
                        Ping2Request = ack2Request
                    }, cancellationToken: cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogError(default, ex, "Cannot ping peer {0} because {1}", peer.EndPoint, ex);
                failed = true;
            }

            if (failed)
            {
                // TODO: pick k nodes & forward ping concurrently, currently only implement k = 1
                var forwarder = PickRandomNode(random);
                logger.LogInformation("Pick peer {0} as forwarder to connect peer {1}", forwarder.EndPoint, peer.EndPoint);
                client = clientFactory.Invoke(forwarder.EndPoint);

                var forwardedSynResponse = await client.ForwardAsync(
                    new ForwardRequest
                    {
                        TargetEndpoint = peer.EndPoint,
                        TargetMethod = nameof(client.Ping1),
                        RequestMessage = synRequest
                    }, cancellationToken: cancellationToken);
                ResponseMessage synResponse = null;
                switch (forwardedSynResponse.ResponseCase)
                {
                    case ForwardResponse.ResponseOneofCase.ErrorMessage:
                        // TODO: Exception type
                        throw new Exception(forwardedSynResponse.ErrorMessage);
                    case ForwardResponse.ResponseOneofCase.None:
                        // TODO: Exception type
                        throw new Exception("Forward response not set content from remote");
                    case ForwardResponse.ResponseOneofCase.ResponseMessage:
                        synResponse = forwardedSynResponse.ResponseMessage;
                        break;
                }
                var ack2Request = node.Ack1(synResponse.Ping1Response);

                var forwardedAck2Response = await client.ForwardAsync(
                    new ForwardRequest
                    {
                        TargetEndpoint = peer.EndPoint,
                        TargetMethod = nameof(client.Ping2),
                        RequestMessage = new RequestMessage
                        {
                            NodeEndpoint = selfNodeEndPoint,
                            Ping2Request = ack2Request
                        }
                    }, cancellationToken: cancellationToken);
                ResponseMessage ack2Response = null;
                switch (forwardedAck2Response.ResponseCase)
                {
                    case ForwardResponse.ResponseOneofCase.ErrorMessage:
                        // TODO: Exception type
                        throw new Exception(forwardedSynResponse.ErrorMessage);
                    case ForwardResponse.ResponseOneofCase.None:
                        // TODO: Exception type
                        throw new Exception("Forward response not set content from remote");
                    case ForwardResponse.ResponseOneofCase.ResponseMessage:
                        ack2Response = forwardedAck2Response.ResponseMessage;
                        break;
                }
            }
        }
    }
}
