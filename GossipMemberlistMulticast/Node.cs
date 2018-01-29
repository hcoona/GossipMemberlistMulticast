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

        public string EndPoint => selfNodeInformation.EndPoint;

        public IReadOnlyCollection<NodeInformation> KnownNodeInformation
        {
            get
            {
                lock (lockObject)
                {
                    return nodeInformationDictionary.Values.ToList().AsReadOnly();
                }
            }
        }

        public Ping1Response Syn(Ping1Request syn)
        {
            lock (lockObject)
            {
                var response = new Ping1Response();

                foreach (var p in syn.NodePropertyVersions.NodePropertyVersions_)
                {
                    var nodeId = p.Key;
                    var incomingNodeVersion = p.Value.Version_;

                    var n = nodeInformationDictionary[nodeId];
                    if (n.Version < incomingNodeVersion)
                    {
                        response.RequiredNodePropertyVersions.NodePropertyVersions_.Add(
                            nodeId,
                            new Version { Version_ = n.Version });
                    }
                    else
                    {
                        var updatedProperties = n.GetPropertiesAfterVersion(incomingNodeVersion);
                        if (updatedProperties.NodeProperties_.Any())
                        {
                            response.UpdatedNodeProperties.Add(nodeId, updatedProperties);
                        }
                    }
                }

                return response;
            }
        }

        public Ping2Request Ack1(Ping1Response ack1)
        {
            lock (lockObject)
            {
                var response = new Ping2Request();

                foreach (var p in ack1.UpdatedNodeProperties)
                {
                    var nodeId = p.Key;

                    var n = nodeInformationDictionary[nodeId];
                    var delta = n.UpdateProperties(p.Value);
                    if (delta.NodeProperties_.Any())
                    {
                        response.UpdatedNodeProperties.Add(nodeId, delta);
                    }
                }

                selfNodeInformation.Version++;

                foreach (var p in ack1.RequiredNodePropertyVersions.NodePropertyVersions_)
                {
                    var nodeId = p.Key;

                    var n = nodeInformationDictionary[nodeId];
                    var updatedProperties = n.GetPropertiesAfterVersion(p.Value.Version_);
                    if (updatedProperties.NodeProperties_.Any())
                    {
                        response.UpdatedNodeProperties.Add(nodeId, updatedProperties);
                    }
                }

                return response;
            }
        }

        public Ping2Response Ack2(Ping2Request ack2)
        {
            lock (lockObject)
            {
                var response = new Ping2Response();

                foreach (var p in ack2.UpdatedNodeProperties)
                {
                    var nodeId = p.Key;

                    var n = nodeInformationDictionary[nodeId];
                    n.UpdateProperties(p.Value);
                }

                selfNodeInformation.Version++;

                return response;
            }
        }
    }
}
