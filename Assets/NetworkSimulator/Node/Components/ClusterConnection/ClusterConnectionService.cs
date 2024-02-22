using Atom.CommunicationSystem;
using Atom.ComponentSystem;
using Sirenix.OdinInspector;
using System;
using System.Linq;
using UnityEngine;

namespace Atom.ClusterConnectionService
{
    public class ClusterConnectionService : INodeComponent
    {
        public NodeEntity context { get; set; }
        [InjectNodeComponentDependency] private PeerSamplingService _samplingService;
        [InjectNodeComponentDependency] private PacketRouter _packetRouter;

        /// <summary>
        /// the maximum number of boot nodes a node can reach while joining the network
        /// </summary>
        private int _maximumBootNodeCalls = 25;

        public void OnInitialize()
        {
            _packetRouter.RegisterPacketHandler(typeof(ClusterConnectionRequestPacket), (packet) =>
            {
                var respondable = (packet as IRespondable);
                _packetRouter.SendResponse(respondable, respondable.GetResponsePacket(respondable));
            });

            _packetRouter.RegisterPacketHandler(typeof(ClusterConnectionRequestResponsePacket), null);

            // the response packet doesn't have to be registered if its called with a send response.
            // it will be routed to the caller id
            _packetRouter.RegisterPacketHandler(typeof(SubscriptionPacket), (subscriptionReceivedPacket) =>
            {
                var respondable = (subscriptionReceivedPacket as IRespondable);
                var subResponse = (SubscriptionResponsePacket)respondable.GetResponsePacket(respondable);

                // the boot sends all the node he knows to allow the newcomer to make a first selection of the best peers from this list
                subResponse.potentialPeerInfos = context.networkHandling.Callers.Values.ToList();
                subResponse.potentialPeerInfos.Add(context.networkHandling.LocalPeerInfo);
                subResponse.potentialPeerInfos.AddRange(context.networkHandling.Listenners.Values.ToList());
                // the new peer will eventually propagate a discovery request to these new nodes to really deeply connect to the network

                _packetRouter.SendResponse(respondable, subResponse);
            });

            _packetRouter.RegisterPacketHandler(typeof(SubscriptionResponsePacket), null);
        }

        public void ConnectToCluster(ClusterInfo clusterInfo)
        {
            var broadcaster = context.GetNodeComponent<BroadcasterComponent>();
            int _btNodeCalls = 0;
            foreach (var bootNode in clusterInfo.BootNodes)
            {
                if (bootNode == context)
                    continue;

                _btNodeCalls++;
                if (_btNodeCalls > _maximumBootNodeCalls)
                    break;

                broadcaster.SendRequest(bootNode.networkHandling.LocalPeerInfo.peerAdress, new ClusterConnectionRequestPacket(), (response) =>
                {
                    Debug.Log("Received cluster connection response. Sending new subscription request.");

                    broadcaster.SendRequest(bootNode.networkHandling.LocalPeerInfo.peerAdress, new SubscriptionPacket(), (response) =>
                    {
                        var subscriptionResponse = (SubscriptionResponsePacket)response;

                        // the datas goes to the peer sampling service at this moment.
                        _samplingService.OnReceiveSubscriptionResponse(subscriptionResponse);
                    });

                });
            }
        }
    }
}
