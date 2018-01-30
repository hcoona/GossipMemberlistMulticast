using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;

namespace GossipMemberlistMulticast
{
    public class Node
    {
        private readonly object lockObject = new object();
        private readonly ILogger<Node> logger;
        private readonly NodeInformation selfNodeInformation;
        private readonly IDictionary<string, NodeInformation> nodeInformationDictionary;

        public Node(
            ILogger<Node> logger,
            NodeInformation selfNodeInformation,
            IDictionary<string, NodeInformation> nodeInformationDictionary)
        {
            this.logger = logger;
            this.selfNodeInformation = selfNodeInformation;
            this.nodeInformationDictionary = nodeInformationDictionary;
        }

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
                p.StateProperty = nodeState;
                p.Version = n.LastKnownPropertyVersion + 1;
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

                selfNodeInformation.BumpVersion();

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
                selfNodeInformation.BumpVersion();

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
                    if (myNode.NodeVersion < n.NodeVersion)
                    {
                        nodeInformationDictionary[n.Endpoint] = n;
                    }
                    else if (myNode.NodeVersion > n.NodeVersion)
                    {
                        // Ignored
                        // TODO: log it
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
                                    // Ignored
                                    // TODO: log it
                                }
                            }
                            else
                            {
                                myNode.Properties.Add(p.Key, p.Value);
                            }
                        }
                    }
                }
                else
                {
                    nodeInformationDictionary.Add(n.Endpoint, n);
                }
            }
        }
    }
}
