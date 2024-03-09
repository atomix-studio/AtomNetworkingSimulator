using Atom.CommunicationSystem;
using Atom.DependencyProvider;
using Atom.Broadcasting;
using Sirenix.OdinInspector;
using System;
using System.Linq;
using System.Net;
using UnityEngine;

namespace Atom.ClusterConnectionService
{
    public class ClusterConnectionService : INodeUpdatableComponent
    {
        public NodeEntity controller { get; set; }
        [Inject] private PeerSamplingService _samplingService;
        [Inject] private PacketRouter _packetRouter;
        [Inject] private BroadcasterComponent _broadcaster;
        [Inject] private NetworkConnectionsComponent _networkHandling;

        /// <summary>
        /// the maximum number of boot nodes a node can reach while joining the network
        /// </summary>
        [SerializeField] private int _maximumBootNodeCalls = 1;
        [SerializeField] private int _reconnectionDelay = 4;

        private float _disconnectedTimer = 0;
        private ClusterInfo _clusterInfo;
        private bool _isConnecting = false;

        public void OnInitialize()
        {
            _packetRouter.RegisterPacketHandler(typeof(ClusterConnectionRequestPacket), (packet) =>
            {
                var respondable = (packet as IRespondable);
                var response = (ClusterConnectionRequestResponsePacket)respondable.GetResponsePacket(respondable).packet;
                response.potentialPeerInfos = new System.Collections.Generic.List<PeerInfo>() { controller.networkHandling.LocalPeerInfo };
                response.potentialPeerInfos.AddRange(controller.networkHandling.Connections.Values.ToList());
                response.potentialPeerInfos.AddRange(controller.networkHandling.KnownPeers.Values.ToList());
                while(response.potentialPeerInfos.Count > 9)
                {
                    response.potentialPeerInfos.RemoveAt(UnityEngine.Random.Range(0, response.potentialPeerInfos.Count));   
                }

                _packetRouter.SendResponse(respondable, response);
            });

            _packetRouter.RegisterPacketHandler(typeof(ClusterConnectionRequestResponsePacket), null);
        }

        public void ConnectToCluster(ClusterInfo clusterInfo)
        {
            _clusterInfo = clusterInfo;
            _isConnecting = true;
            var nodesList = clusterInfo.BootNodes.ToList();

            int _btNodeCalls = 0;
            while(_btNodeCalls < _maximumBootNodeCalls || nodesList.Count == 0)
            {
                int index = UnityEngine.Random.Range(0, nodesList.Count);

                if (nodesList[index] == controller)
                    continue;

                _btNodeCalls++;
                if (_btNodeCalls > _maximumBootNodeCalls)
                    break;

                _broadcaster.SendRequest(nodesList[index].networkHandling.LocalPeerInfo.peerAdress, new ClusterConnectionRequestPacket(), (response) =>
                {
                    // means a timout or an error
                    if(response == null)
                    {
                        Debug.LogError("Connection to cluster request timed out. Quitting isConnecting state.");
                        _isConnecting = false;
                        return;
                    }

                    Debug.Log("Received cluster connection response. Sending new subscription request.");
                    var clusterResponse = (ClusterConnectionRequestResponsePacket)response;
                    // the datas goes to the peer sampling service at this moment.
                    _samplingService.OnReceiveSubscriptionResponse(clusterResponse);
                    _isConnecting = false;
                    controller.IsConnectedAndReady = true;
                   /* _broadcaster.SendRequest(bootNode.networkHandling.LocalPeerInfo.peerAdress, new SubscriptionPacket(), (response) =>
                    {
                        var subscriptionResponse = (SubscriptionResponsePacket)response;

                       
                    });*/
                });
            }
        }

        public void OnUpdate()
        {
            if (controller.IsSleeping)
                return;

            if (_isConnecting)
                return;


            // routine to check disconnctions
            if (_networkHandling.Connections.Count == 0)
            {
                _disconnectedTimer += Time.deltaTime;

                if(_disconnectedTimer > _reconnectionDelay)
                {
                    // mean that the node hasn't receive its first connection order
                    if (_clusterInfo == null)
                        return;

                    ConnectToCluster(_clusterInfo);

                    return;
                }
            }

        }
    }
}
