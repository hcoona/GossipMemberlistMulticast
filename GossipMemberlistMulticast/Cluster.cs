using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace GossipMemberlistMulticast
{
    public class Cluster
    {
        private readonly ILogger<Cluster> logger;
        private readonly Node node;
        private readonly Func<string, Gossiper.GossiperClient> clientFactory;

        public Cluster(
            ILogger<Cluster> logger,
            Node node,
            Func<string, Gossiper.GossiperClient> clientFactory)
        {
            this.logger = logger;
            this.node = node;
            this.clientFactory = clientFactory;
        }

        private CancellationTokenSource backgroundLoopCancellationTokenSource;
        private Task backgroundLoopTask;

        public Task StartAsync(CancellationToken cancellationToken = default)
        {
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
                var liveNodes = node.LiveEndpoints;
                var nonLiveNodes = node.NonLiveEndpoints;

                logger.LogInformation("Live nodes: [{0}]", string.Join(", ", Enumerable.Repeat(node.EndPoint, 1).Concat(liveNodes)));
                logger.LogInformation("Non-live nodes: [{0}]", string.Join(", ", nonLiveNodes));

                var peerEndpoint = RandomPickNode(random, liveNodes, nonLiveNodes);

                if (peerEndpoint != null)
                {
                    try
                    {
                        await SyncWithPeerAsync(peerEndpoint, random, cancellationToken);
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(default, ex, "Failed to connect to peer {0} because {1}", peerEndpoint, ex);
                        // TODO: Mark suspect & start probing
                        node.AssignNodeState(peerEndpoint, NodeState.Dead);
                    }
                }

                // TODO: Read from configuration
                await Task.Delay(5000);
            }
        }

        private string RandomPickNode(Random random, IReadOnlyList<string> liveNodes, IReadOnlyList<string> nonLiveNodes)
        {
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
                logger.LogDebug("There's no live nodes except than self");
                return nonLiveNodes.ChooseRandom(random);
            }
            else if (liveNodes.Any())
            {
                logger.LogDebug("There's no non-live nodes");
                return liveNodes.ChooseRandom(random);
            }
            else
            {
                logger.LogDebug("There are no other nodes than self");
                return null;
            }
        }

        private async Task SyncWithPeerAsync(string peerEndpoint, Random random, CancellationToken cancellationToken)
        {
            var client = clientFactory.Invoke(peerEndpoint);

            var synRequest = new Ping1Request();
            synRequest.NodesSynopsis.AddRange(node.GetNodesSynposis());

            bool failed = false;
            try
            {
                // TODO: Read the deadline setting from configuration
                var synResponse = await client.Ping1Async(
                    synRequest,
                    deadline: DateTime.UtcNow + TimeSpan.FromMilliseconds(200),
                    cancellationToken: cancellationToken);
                var ack2Request = node.Ack1(synResponse);

                var ack2Response = await client.Ping2Async(
                    ack2Request,
                    deadline: DateTime.UtcNow + TimeSpan.FromMilliseconds(200),
                    cancellationToken: cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogError(default, ex, "Cannot ping peer {0} because {1}", peerEndpoint, ex);
                failed = true;
            }

            if (failed)
            {
                // TODO: pick k nodes & forward ping concurrently, currently only implement k = 1
                var forwarderEndpoint = RandomPickNode(random, node.LiveEndpoints, new List<string>());
                if (forwarderEndpoint == null)
                {
                    throw new InvalidOperationException("Cannot pick a node for forwarder");
                }

                logger.LogInformation("Pick peer {0} as forwarder to connect peer {1}", forwarderEndpoint, peerEndpoint);
                client = clientFactory.Invoke(forwarderEndpoint);

                // TODO: Read the deadline setting from configuration
                var forwardedSynResponse = await client.ForwardAsync(
                    new ForwardRequest
                    {
                        TargetEndpoint = peerEndpoint,
                        TargetMethod = nameof(client.Ping1),
                        Ping1Request = synRequest
                    },
                    deadline: DateTime.UtcNow + TimeSpan.FromMilliseconds(200),
                    cancellationToken: cancellationToken);
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

                // TODO: Read the deadline setting from configuration
                var ack2Request = node.Ack1(synResponse);
                var forwardedAck2Response = await client.ForwardAsync(
                    new ForwardRequest
                    {
                        TargetEndpoint = peerEndpoint,
                        TargetMethod = nameof(client.Ping2),
                        Ping2Request = ack2Request
                    },
                    deadline: DateTime.UtcNow + TimeSpan.FromMilliseconds(200),
                    cancellationToken: cancellationToken);
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
