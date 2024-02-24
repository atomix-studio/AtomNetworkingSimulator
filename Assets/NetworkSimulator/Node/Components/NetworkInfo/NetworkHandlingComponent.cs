using Atom.Components.Connecting;
using Atom.Components.Handshaking;
using Atom.ComponentProvider;
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
        public NodeEntity context { get; set; }
        [InjectComponent] private HandshakingComponent _handshaking;
        [InjectComponent] private ConnectingComponent _connecting;
        [InjectComponent] private PeerSamplingService _peerSampling;

        [SerializeField] protected int knownPeersMaximumCount = 25;
        [SerializeField] protected int peerConnectionLeaseTime = 5;
        [SerializeField] protected int peerConnectionLeaseRefreshTime = 3;

        [SerializeField, ReadOnly] private float _averageScore = 0;
        // average score value from last update 
        // the component uses that data to detect changes (decrease) and will react to a network health going down by rebroadcasting discovery requets
        [SerializeField, ReadOnly] private List<float> _networkScoreBuffer = new List<float>();

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
        public List<PeerInfo> KnownPeers { get => _knownPeers; set => _knownPeers = value; }

        //[SerializeField] private List<PeerInfo> _callersDebug;
        [SerializeField] private List<PeerInfo> _connectionsDebug;
        [SerializeField] private List<PeerInfo> _knownPeers;

        #endregion

        public void OnInitialize()
        {
            //Callers = new Dictionary<string, PeerInfo>();
            Connections = new Dictionary<string, PeerInfo>();
            KnownPeers = new List<PeerInfo>();
            //_callersDebug = new List<PeerInfo>();
            _connectionsDebug = new List<PeerInfo>();
            _networkScoreBuffer.Add(0);

            var router = context.GetNodeComponent<PacketRouter>();
            router.RegisterPacketReceiveMiddleware((packet) =>
            {
                if (Connections.TryGetValue(packet.senderID, out var callerInfo))
                {
                    callerInfo.last_updated = DateTime.UtcNow;
                }

                return true;
            });

            /*router.RegisterPacketReceiveMiddleware((packet) =>
            {
                if (Listenners.ContainsKey(packet.senderID))
                {

                }

                return true;
            });*/
        }

        // Initialization could eventually handle the retrieving of previous known connections ?
        public void InitializeLocalInfo(PeerInfo localPeerInfo)
        {
            LocalPeerInfo = localPeerInfo;
            // ugly, will see later to optimize inits
            context.GetNodeComponent<PacketRouter>().InitPeerAdress(localPeerInfo.peerAdress);
        }
/*
        public void AddCaller(PeerInfo peerInfo)
        {
            TryRemoveKnownPeer(peerInfo);

            if (Callers.ContainsKey(peerInfo.peerID))
                return;

            Debug.Log($"{this} adding new caller => {peerInfo.peerAdress}");
            Callers.Add(peerInfo.peerID, peerInfo);
            _callersDebug.Add(peerInfo);
            peerInfo.last_updated = DateTime.Now;
        }

        public void RemoveCaller(PeerInfo peerInfo)
        {
            Debug.Log($"{this} removing caller => {peerInfo.peerAdress}");

            TryAddKnownPeer(peerInfo);
            Callers.Remove(peerInfo.peerID);
            _callersDebug.Remove(peerInfo);
        }
*/
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
            if (!KnownPeers.Contains(peerInfo))
                KnownPeers.Add(peerInfo);

            if (KnownPeers.Count > knownPeersMaximumCount)
                KnownPeers.RemoveAt(0);
        }

        private void TryRemoveKnownPeer(PeerInfo peerInfo)
        {
            if (KnownPeers.Contains(peerInfo))
                KnownPeers.Remove(peerInfo);
        }
/*
        public PeerInfo FindCallerByAdress(string adress)
        {
            foreach (var peer in Callers)
            {
                if (peer.Value.peerAdress == adress)
                {
                    return peer.Value;
                }
            }

            return null;
        }*/

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
            if (context.IsSleeping)
                return;

          /*  for (int i = 0; i < Callers.Count; ++i)
            {
                if ((DateTime.Now - Callers.ElementAt(i).Value.last_updated).Seconds > peerConnectionLeaseTime)
                {
                    // the caller hasnt pinged the local node for more than the lease time
                    // it is the expiration of the connection
                    // we send a disconnection message to caller
                    _connecting.DisconnectFromCaller(Callers.ElementAt(i).Value);
                }
            }*/

            for (int i = 0; i < Connections.Count; ++i)
            {
                if ((DateTime.Now - Connections.ElementAt(i).Value.last_updated).Seconds > peerConnectionLeaseRefreshTime)
                {
                    // refresh message to listenner
                    // refreshing the score at the same time
                    await UpdatePeerInfoAsync(Connections.ElementAt(i).Value);

                    UpdateNetworkScore();
                }
            }

            if (Connections.Count > context.NetworkViewsTargetCount)
            {
                // sorting listenners by score and disconnect from the one that are not kept in selection
            }
        }

        private void UpdateNetworkScore()
        {
            var oldScroe = _averageScore;

            var current_peers_score = 0f;
            // the heartbeat should be able to maintain a score routine and see if a peer becomes too low in PeerScore (like latency is increasing)
            for (int i = 0; i < Connections.Count; ++i)
            {
                current_peers_score += Connections.ElementAt(i).Value.score;
            }
            current_peers_score /= Connections.Count;
            _averageScore = current_peers_score;

            if (oldScroe > _averageScore)
            {
                Debug.LogError("Network score going down");
            }

            // abritrary value for now
            // the idea is to check if the score is decreasing from a previous high value
            // if so, the node will trigger a discovery request to recreate its network with better peers

            ///TODO analyzing the _networkScoreBuffer curve more in depth

            if (current_peers_score * 1.33f < oldScroe)
            {
                _peerSampling.BroadcastDiscoveryRequest();
                _networkScoreBuffer.Add(current_peers_score);
            }
            else 
            {
                _networkScoreBuffer.Add(current_peers_score);
            }
        }

        public async Task<bool> UpdatePeerInfoAsync(PeerInfo peerInfo)
        {
            var handshakeResponse = await _handshaking.GetHandshakeAsync(peerInfo);

            if (handshakeResponse == null)
            {
                peerInfo.SetScore(0);
                return false;
            }

            peerInfo.ComputeScore(peerInfo.ping, handshakeResponse.networkInfoListennersCount);
            peerInfo.SetScoreByDistance(context.transform.position);

            return true;
        }

    }
}
