using System;
using System.Collections.Generic;
using System.Diagnostics;
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
            var selfNodeInformation = new NodeInformation
            {
                Endpoint = selfNodeEndPoint,
                NodeVersion = Process.GetCurrentProcess().StartTime.ToFileTimeUtc(),
                NodeStateProperty = new VersionedProperty
                {
                    Version = 1,
                    StateProperty = NodeState.Live
                }
            };
            var seedsNodeInformation = seedsProvider()
                .Select(endpoint => new NodeInformation
                {
                    Endpoint = endpoint,
                    NodeVersion = 0,
                    NodeStateProperty = new VersionedProperty
                    {
                        Version = 0,
                        StateProperty = NodeState.Unknown
                    }
                }).ToArray();

            var nodeInformationDictionary = new Dictionary<string, NodeInformation>(StringComparer.InvariantCultureIgnoreCase)
            {
                { selfNodeInformation.Endpoint, selfNodeInformation }
            };
            foreach (var n in seedsNodeInformation)
            {
                nodeInformationDictionary.Add(n.Endpoint, n);
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
                        logger.LogError(default, ex, "Failed to connect to peer {0} because {1}", peer.Endpoint, ex);
                        // TODO: Mark suspect & start probing
                        peer.NodeStateProperty.StateProperty = NodeState.Dead;
                    }
                }

                await Task.Delay(1000);
            }
        }

        private NodeInformation PickRandomNode(Random random)
        {
            var liveNodes = node.KnownNodeInformation
                                .Where(n => n.NodeStateProperty.StateProperty == NodeState.Live)
                                .Where(n => n.Endpoint != selfNodeEndPoint)
                                .ToArray();
            var nonLiveNodes = node.KnownNodeInformation
                .Where(n => n.NodeStateProperty.StateProperty != NodeState.Live)
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
            var client = clientFactory.Invoke(peer.Endpoint);

            var synRequest = new Ping1Request();
            synRequest.NodesSynopsis.AddRange(node.GetNodesSynposis());

            bool failed = false;
            try
            {
                var synResponse = await client.Ping1Async(synRequest, cancellationToken: cancellationToken);
                var ack2Request = node.Ack1(synResponse);

                var ack2Response = await client.Ping2Async(ack2Request, cancellationToken: cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogError(default, ex, "Cannot ping peer {0} because {1}", peer.Endpoint, ex);
                failed = true;
            }

            if (failed)
            {
                // TODO: pick k nodes & forward ping concurrently, currently only implement k = 1
                var forwarder = PickRandomNode(random);
                logger.LogInformation("Pick peer {0} as forwarder to connect peer {1}", forwarder.Endpoint, peer.Endpoint);
                client = clientFactory.Invoke(forwarder.Endpoint);

                var forwardedSynResponse = await client.ForwardAsync(
                    new ForwardRequest
                    {
                        TargetEndpoint = peer.Endpoint,
                        TargetMethod = nameof(client.Ping1),
                        Ping1Request = synRequest
                    }, cancellationToken: cancellationToken);
                Ping1Response synResponse;
                switch (forwardedSynResponse.ResponseCase)
                {
                    case ForwardResponse.ResponseOneofCase.ErrorMessage:
                        // TODO: Exception type
                        throw new Exception(forwardedSynResponse.ErrorMessage);
                    case ForwardResponse.ResponseOneofCase.None:
                        // TODO: Exception type
                        throw new Exception("Forward response not set content from remote");
                    case ForwardResponse.ResponseOneofCase.Ping1Response:
                        synResponse = forwardedSynResponse.Ping1Response;
                        break;
                    default:
                        // TODO: Exception type
                        throw new Exception("Unknown response type");
                }

                var ack2Request = node.Ack1(synResponse);
                var forwardedAck2Response = await client.ForwardAsync(
                    new ForwardRequest
                    {
                        TargetEndpoint = peer.Endpoint,
                        TargetMethod = nameof(client.Ping2),
                        Ping2Request = ack2Request
                    }, cancellationToken: cancellationToken);
                Ping2Response ack2Response;
                switch (forwardedAck2Response.ResponseCase)
                {
                    case ForwardResponse.ResponseOneofCase.ErrorMessage:
                        // TODO: Exception type
                        throw new Exception(forwardedSynResponse.ErrorMessage);
                    case ForwardResponse.ResponseOneofCase.None:
                        // TODO: Exception type
                        throw new Exception("Forward response not set content from remote");
                    case ForwardResponse.ResponseOneofCase.Ping2Response:
                        ack2Response = forwardedAck2Response.Ping2Response;
                        break;
                    default:
                        // TODO: Exception type
                        throw new Exception("Unknown response type");
                }
            }
        }
    }
}
