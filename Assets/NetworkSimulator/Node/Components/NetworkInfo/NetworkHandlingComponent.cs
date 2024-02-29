using Atom.Components.Connecting;
using Atom.Components.Handshaking;
using Atom.DependencyProvider;
using Sirenix.OdinInspector;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

namespace Atom.CommunicationSystem
{
    /// <summary>
    /// This component is the heart of the maintaining of the connections for a node.
    /// It keeps the datas about what nodes he can send message to, which node he will receive from.
    /// It handles the heartbeat mechanic (pinging listenners to let them know the local node is still alive)
    /// It keeps datas about old connections in case he needs to try to reconnect to them.
    /// </summary>
    [Serializable]
    public class NetworkHandlingComponent : MonoBehaviour, INodeUpdatableComponent
    {
        public NodeEntity controller { get; set; }
        [Inject] private HandshakingComponent _handshaking;
        [Inject] private ConnectingComponent _connecting;
        [Inject] private PeerSamplingService _peerSampling;
        [Inject] private PacketRouter _packetRouter;

        [SerializeField] protected int knownPeersMaximumCount = 25;
        [SerializeField] protected int peerConnectionLeaseTime = 5;
        [SerializeField] protected int peerConnectionLeaseRefreshTime = 3;

        [SerializeField, ReadOnly] private float _averageScore = 0;
        // average score value from last update 
        // the component uses that data to detect changes (decrease) and will react to a network health going down by rebroadcasting discovery requets
        [SerializeField, ReadOnly] private List<float> _networkScoreBuffer = new List<float>();
        [SerializeField, ReadOnly] private float _averageNetworkScore;


        public int KnownPeersMaximumCount => knownPeersMaximumCount;
        #region Peer Infos
        /// <summary>
        /// INFO of the local peer
        /// </summary>
        public PeerInfo LocalPeerInfo { get => _localPeerInfo; private set => _localPeerInfo = value; }
        [SerializeField] private PeerInfo _localPeerInfo;

        /// <summary>
        /// Peers that are currently sending datas to the local node
        /// </summary>
        //public Dictionary<string, PeerInfo> Callers { get; set; }

        /// <summary>
        /// Peers that the local node can sends data to
        /// </summary>
        public Dictionary<string, PeerInfo> Connections { get; set; }

        /// <summary>
        /// A collection of known peers which are note in the listenners or the callers.
        /// </summary>
        public Dictionary<string, PeerInfo> KnownPeers { get; set; }

        //[SerializeField] private List<PeerInfo> _callersDebug;
        [SerializeField] private List<PeerInfo> _connectionsDebug;
        [SerializeField] private List<PeerInfo> _knownPeers;

        #endregion

        public void OnInitialize()
        {
            //Callers = new Dictionary<string, PeerInfo>();
            Connections = new Dictionary<string, PeerInfo>();
            KnownPeers = new Dictionary<string, PeerInfo>();
            //_callersDebug = new List<PeerInfo>();
            _connectionsDebug = new List<PeerInfo>();
            _networkScoreBuffer.Add(0);

            _packetRouter.RegisterPacketReceiveMiddleware((packet) =>
            {
                if (Connections.TryGetValue(packet.senderID, out var callerInfo))
                {
                    callerInfo.last_received = DateTime.UtcNow;
                }

                return true;
            });

            _packetRouter.RegisterPacketHandler(typeof(HeartbeatResponsePacket), null);
            _packetRouter.RegisterPacketHandler(typeof(HeartbeatPacket), (packet) =>
            {
                if (packet == null)
                    return;

                var respondable = (packet as IRespondable);
                var response = (HeartbeatResponsePacket)respondable.GetResponsePacket(respondable);
                _packetRouter.SendResponse(respondable, response);
            });

            // we intercept any request response received by the node to keep updating our ping average value as its finest over connections
            _packetRouter.RegisterOnResponseReceivedCallback((response) =>
            {
                if(Connections.TryGetValue(response.packet.senderID, out var peerInfo))
                {
                    peerInfo.UpdateAveragePing(response.requestPing);
                }
            });
        }

        // Initialization could eventually handle the retrieving of previous known connections ?
        public void InitializeLocalInfo(PeerInfo localPeerInfo)
        {
            LocalPeerInfo = localPeerInfo;
            // ugly, will see later to optimize inits
            controller.GetNodeComponent<PacketRouter>().InitPeerAdress(localPeerInfo.peerID);
        }

        public void AddConnection(PeerInfo peerInfo)
        {
            Debug.Log($"{this} adding new listenner => {peerInfo.peerAdress}");

            TryRemoveKnownPeer(peerInfo);

            if (Connections.ContainsKey(peerInfo.peerID))
                return;

            Connections.Add(peerInfo.peerID, peerInfo);
            _connectionsDebug.Add(peerInfo);
            peerInfo.last_updated = DateTime.Now;


            UpdateNetworkScore();

        }

        public void RemoveConnection(PeerInfo peerInfo)
        {
            Debug.Log($"{this} removing listenner => {peerInfo.peerAdress}");

            TryAddKnownPeer(peerInfo);
            _connectionsDebug.Remove(peerInfo);
            Connections.Remove(peerInfo.peerID);
        }

        private void TryAddKnownPeer(PeerInfo peerInfo)
        {
            if (!KnownPeers.ContainsKey(peerInfo.peerID))
                KnownPeers.Add(peerInfo.peerID, peerInfo);

            if (KnownPeers.Count > knownPeersMaximumCount)
                KnownPeers.Remove(KnownPeers.ElementAt(0).Key);
        }

        private void TryRemoveKnownPeer(PeerInfo peerInfo)
        {
            if (KnownPeers.ContainsKey(peerInfo.peerID))
                KnownPeers.Remove(peerInfo.peerID);
        }

        public PeerInfo FindConnectionByAdress(string adress)
        {
            foreach (var peer in Connections)
            {
                if (peer.Value.peerAdress == adress)
                {
                    return peer.Value;
                }
            }

            return null;
        }

        public async void OnUpdate()
        {
            if (controller.IsSleeping)
                return;

            foreach (var peer in Connections)
            {
                if (!peer.Value.requestedByLocal)
                    continue;

                if ((DateTime.Now - peer.Value.last_updated).Seconds > peerConnectionLeaseRefreshTime)
                {
                    // refresh message to listenner
                    // refreshing the score at the same time
                    HeartbeatConnectionWith(peer.Value);
                }
            }
            for (int i = 0; i < Connections.Count; ++i)
            {


            }

            /*if (Connections.Count > context.NetworkViewsTargetCount)
            {
                // sorting listenners by score and disconnect from the one that are not kept in selection
            }*/
        }

        private void UpdateNetworkScore()
        {
            var current_peers_score = 0f;
            // the heartbeat should be able to maintain a score routine and see if a peer becomes too low in PeerScore (like latency is increasing)
            for (int i = 0; i < Connections.Count; ++i)
            {
                current_peers_score += Connections.ElementAt(i).Value.score;
            }
            current_peers_score /= Connections.Count;

            // abritrary value for now
            // the idea is to check if the score is decreasing from a previous high value
            // if so, the node will trigger a discovery request to recreate its network with better peers

            ///TODO analyzing the _networkScoreBuffer curve more in depth

            // if the score has decreasing for more than 10% we might want to update it with a discovery broadcast
            if (current_peers_score * 1.1f < _averageNetworkScore)
            {
                if (_peerSampling.TryBroadcastDiscoveryRequest())
                {
                    //_networkScoreBuffer.Add(current_peers_score);
                    _averageNetworkScore = current_peers_score;
                }
            }
            // we keep the best value increasing
            else if (current_peers_score > _averageNetworkScore)
            {
                //_networkScoreBuffer.Add(current_peers_score);
                _averageNetworkScore = current_peers_score;
            }
        }

        public async Task<bool> HeartbeatConnectionWith(PeerInfo peerInfo)
        {
            var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            _packetRouter.SendRequest(peerInfo.peerAdress, new HeartbeatPacket(), (response) =>
            {
                if (response == null)
                {
                    // on non responding simply set the score to 0 the first time, it will be in the first place to be replaced by a better connection
                    // if score was already 0 at this time, then disconnect
                    if (peerInfo.score <= 0)
                    {
                        _connecting.DisconnectFromPeer(peerInfo);
                    }
                    else
                    {
                        peerInfo.SetScore(0);
                    }

                    tcs.TrySetResult(false);
                    return;
                }
                //var heartbeatResp = (HeartbeatResponsePacket)response;

                // getting this here and not from out of the callback avoids lots compiler crap > allocations

                if (Connections.TryGetValue(response.senderID, out peerInfo))
                {
                    peerInfo.UpdateAveragePing((response as IResponse).requestPing);

                    // updating overall score of the network at instant T
                    UpdateNetworkScore();
                }

                tcs.TrySetResult(true);
            });

            return await tcs.Task;
        }

        public async Task<bool> UpdatePeerInfoAsync(PeerInfo peerInfo)
        {
            var handshakeResponse = await _handshaking.GetHandshakeAsync(peerInfo);

            if (handshakeResponse == null)
            {
                peerInfo.SetScore(0);
                return false;
            }

            peerInfo.SetScoreByDistance(controller.transform.position);

            return true;
        }

    }
}
