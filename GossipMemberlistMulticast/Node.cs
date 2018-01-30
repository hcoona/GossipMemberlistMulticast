using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;

namespace GossipMemberlistMulticast
{
    public class Node
    {
        public static Node Create(
            string selfNodeEndpoint,
            Func<IEnumerable<string>> seedsEndpointProvider,
            Func<ILogger<Node>> loggerFactory)
        {
            var selfNodeInformation = NodeInformation.CreateSelfNode(selfNodeEndpoint);
            var seedsNodeInformation = seedsEndpointProvider.Invoke()
                .Where(n => n != selfNodeEndpoint)
                .Select(NodeInformation.CreateSeedNode)
                .ToArray();

            var nodeInformationDictionary = new Dictionary<string, NodeInformation>(StringComparer.InvariantCultureIgnoreCase)
            {
                { selfNodeInformation.Endpoint, selfNodeInformation }
            };
            foreach (var n in seedsNodeInformation)
            {
                nodeInformationDictionary.Add(n.Endpoint, n);
            }

            return new Node(
                loggerFactory.Invoke(),
                selfNodeInformation,
                nodeInformationDictionary);
        }

        private readonly object lockObject = new object();
        private readonly ILogger<Node> logger;
        private readonly NodeInformation selfNodeInformation;
        internal readonly IDictionary<string, NodeInformation> nodeInformationDictionary;

        public Node(
            ILogger<Node> logger,
            NodeInformation selfNodeInformation,
            IDictionary<string, NodeInformation> nodeInformationDictionary)
        {
            this.logger = logger;
            this.selfNodeInformation = selfNodeInformation;
            this.nodeInformationDictionary = nodeInformationDictionary;
        }

        public event EventHandler<NodeStateChangedEventArgs> NodeStateChanged;

        public string EndPoint => selfNodeInformation.Endpoint;

        public IReadOnlyList<string> LiveEndpoints
        {
            get
            {
                lock (lockObject)
                {
                    return nodeInformationDictionary.Values
                        .Where(n => n.NodeState == NodeState.Live)
                        .Where(n => n.Endpoint != selfNodeInformation.Endpoint)
                        .Select(n => n.Endpoint)
                        .ToList()
                        .AsReadOnly();
                }
            }
        }

        public IReadOnlyList<string> NonLiveEndpoints
        {
            get
            {
                lock (lockObject)
                {
                    return nodeInformationDictionary.Values
                        .Where(n => n.NodeState != NodeState.Live)
                        .Select(n => n.Endpoint)
                        .ToList()
                        .AsReadOnly();
                }
            }
        }

        public void AssignNodeState(string endpoint, NodeState nodeState)
        {
            lock (lockObject)
            {
                var n = nodeInformationDictionary[endpoint];
                var p = n.NodeStateProperty;

                var originState = p.StateProperty;
                p.StateProperty = nodeState;
                p.Version = n.LastKnownPropertyVersion + 1;

                if (p.StateProperty != originState)
                {
                    NodeStateChanged?.Invoke(this, new NodeStateChangedEventArgs
                    {
                        EndPoint = endpoint,
                        PreviousNodeState = originState,
                        CurrentNodeState = p.StateProperty
                    });
                }
            }
        }

        public IList<NodeInformationSynopsis> GetNodesSynposis()
        {
            lock (lockObject)
            {
                return nodeInformationDictionary.Values.Select(n => n.GetSynopsis()).ToArray();
            }
        }

        public Ping1Response Syn(Ping1Request syn)
        {
            lock (lockObject)
            {
                var response = new Ping1Response();
                FillRequiredOrUpdatedNodes(
                    syn.NodesSynopsis,
                    response.RequiredNodesSynopsis,
                    response.UpdatedNodes);

                return response;
            }
        }

        public Ping2Request Ack1(Ping1Response ack1)
        {
            lock (lockObject)
            {
                var response = new Ping2Request();

                MergeUpdateNodes(ack1.UpdatedNodes);

                AssignNodeState(selfNodeInformation.Endpoint, NodeState.Live);

                var ignored = new List<NodeInformationSynopsis>();
                FillRequiredOrUpdatedNodes(
                    ack1.RequiredNodesSynopsis,
                    ignored,
                    response.UpdatedNodes);

                return response;
            }
        }

        public Ping2Response Ack2(Ping2Request ack2)
        {
            lock (lockObject)
            {
                var response = new Ping2Response();

                MergeUpdateNodes(ack2.UpdatedNodes);
                AssignNodeState(selfNodeInformation.Endpoint, NodeState.Live);

                return response;
            }
        }

        internal void FillRequiredOrUpdatedNodes(
            IEnumerable<NodeInformationSynopsis> nodesSynopsis,
            IList<NodeInformationSynopsis> requiredNodesSynopsis,
            IList<NodeInformation> updatedNodesSynopsis)
        {
            foreach (var n in nodesSynopsis)
            {
                if (nodeInformationDictionary.ContainsKey(n.Endpoint))
                {
                    var myNode = nodeInformationDictionary[n.Endpoint];
                    if (myNode.NodeVersion < n.NodeVersion)
                    {
                        requiredNodesSynopsis.Add(new NodeInformationSynopsis
                        {
                            Endpoint = n.Endpoint,
                            NodeVersion = myNode.NodeVersion,
                            LastKnownPropertyVersion = 0
                        });
                    }
                    else if (myNode.NodeVersion > n.NodeVersion)
                    {
                        updatedNodesSynopsis.Add(myNode);
                    }
                    else
                    {
                        if (myNode.LastKnownPropertyVersion < n.LastKnownPropertyVersion)
                        {
                            requiredNodesSynopsis.Add(myNode.GetSynopsis());
                        }
                        else if (myNode.LastKnownPropertyVersion > n.LastKnownPropertyVersion)
                        {
                            updatedNodesSynopsis.Add(myNode.GetDelta(n, logger));
                        }
                        else
                        {
                            logger.LogDebug("Node {0} has exactly same version to incoming synopsis.", n.Endpoint);
                        }
                    }
                }
                else
                {
                    requiredNodesSynopsis.Add(new NodeInformationSynopsis
                    {
                        Endpoint = n.Endpoint,
                        NodeVersion = 0,
                        LastKnownPropertyVersion = 0
                    });
                }
            }

            var peerUnknownNodes = nodeInformationDictionary.Keys
                .Except(nodesSynopsis.Select(n => n.Endpoint))
                .Select(key => nodeInformationDictionary[key]);
            foreach (var n in peerUnknownNodes)
            {
                updatedNodesSynopsis.Add(n);
            }
        }

        internal void MergeUpdateNodes(IEnumerable<NodeInformation> updateNodes)
        {
            foreach (var n in updateNodes)
            {
                if (nodeInformationDictionary.ContainsKey(n.Endpoint))
                {
                    var myNode = nodeInformationDictionary[n.Endpoint];
                    var originNodeState = myNode.NodeState;
                    if (myNode.NodeVersion < n.NodeVersion)
                    {
                        myNode = n;
                        nodeInformationDictionary[n.Endpoint] = n;
                    }
                    else if (myNode.NodeVersion > n.NodeVersion)
                    {
                        logger.LogDebug(
                            "Node {0} received node version ({1}) is lower than current known node version ({2})",
                            n.Endpoint,
                            n.NodeVersion,
                            myNode.NodeVersion);
                    }
                    else
                    {
                        foreach (var p in n.Properties)
                        {
                            if (myNode.Properties.ContainsKey(p.Key))
                            {
                                if (myNode.Properties[p.Key].Version < p.Value.Version)
                                {
                                    myNode.Properties[p.Key] = p.Value;
                                }
                                else
                                {
                                    logger.LogDebug(
                                        "Node {0} property {1} received version ({2}) is lower or equal to current known version ({3})",
                                        myNode.Endpoint,
                                        p.Key,
                                        p.Value.Version,
                                        myNode.Properties[p.Key].Version);
                                }
                            }
                            else
                            {
                                myNode.Properties.Add(p.Key, p.Value);
                            }
                        }
                    }
                    if (myNode.NodeState != originNodeState)
                    {
                        NodeStateChanged?.Invoke(this, new NodeStateChangedEventArgs
                        {
                            EndPoint = myNode.Endpoint,
                            PreviousNodeState = originNodeState,
                            CurrentNodeState = myNode.NodeState
                        });
                    }
                }
                else
                {
                    nodeInformationDictionary.Add(n.Endpoint, n);
                    NodeStateChanged?.Invoke(this, new NodeStateChangedEventArgs
                    {
                        EndPoint = n.Endpoint,
                        PreviousNodeState = NodeState.Unknown,
                        CurrentNodeState = n.NodeState
                    });
                }
            }
        }
    }
}
