﻿using Atom.Components.Connecting;
using Atom.Components.Handshaking;
using Atom.ComponentSystem;
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
        [InjectNodeComponentDependency] private HandshakingComponent _handshaking;
        [InjectNodeComponentDependency] private ConnectingComponent _connecting;
        [InjectNodeComponentDependency] private PeerSamplingService _peerSampling;

        protected int knownPeersMaximumCount = 100;
        protected int peerConnectionLeaseTime = 60;
        protected int peerConnectionLeaseRefreshTime = 55;

        // average score value from last update 
        // the component uses that data to detect changes (decrease) and will react to a network health going down by rebroadcasting discovery requets
        private List<float> _networkScoreBuffer = new List<float>();

        #region Peer Infos
        /// <summary>
        /// INFO of the local peer
        /// </summary>
        public PeerInfo LocalPeerInfo { get => _localPeerInfo; private set => _localPeerInfo = value; }
        [SerializeField] private PeerInfo _localPeerInfo;

        /// <summary>
        /// Peers that are currently sending datas to the local node
        /// </summary>
        public Dictionary<string, PeerInfo> Callers { get; set; }

        /// <summary>
        /// Peers that the local node can sends data to
        /// </summary>
        public Dictionary<string, PeerInfo> Listenners { get; set; }

        /// <summary>
        /// A collection of known peers which are note in the listenners or the callers.
        /// </summary>
        public List<PeerInfo> KnownPeers { get; set; }
        #endregion

        public void OnInitialize()
        {
            Callers = new Dictionary<string, PeerInfo>();
            Listenners = new Dictionary<string, PeerInfo>();
            KnownPeers = new List<PeerInfo>();
        }

        // Initialization could eventually handle the retrieving of previous known connections ?
        public void InitializeLocalInfo(PeerInfo localPeerInfo)
        {
            LocalPeerInfo = localPeerInfo;
        }

        public void AddCaller(PeerInfo peerInfo)
        {
            TryRemoveKnownPeer(peerInfo);

            Debug.Log($"{this} adding new caller => {peerInfo.peerAdress}");
            Callers.Add(peerInfo.peerAdress, peerInfo);
            peerInfo.last_updated = DateTime.Now;
        }

        public void RemoveCaller(PeerInfo peerInfo)
        {
            TryAddKnownPeer(peerInfo);
            Callers.Remove(peerInfo.peerAdress);
        }

        public void AddListenner(PeerInfo peerInfo)
        {
            TryRemoveKnownPeer(peerInfo);
            Listenners.Add(peerInfo.peerAdress, peerInfo);
            peerInfo.last_updated = DateTime.Now;
        }

        public void RemoveListenner(PeerInfo peerInfo)
        {
            TryAddKnownPeer(peerInfo);
            Listenners.Remove(peerInfo.peerAdress);
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
                
        public void OnUpdate()
        {
            for(int i = 0; i < Callers.Count; ++i)
            {
                if ((DateTime.Now - Callers.ElementAt(i).Value.last_updated).Seconds > peerConnectionLeaseTime)
                {
                    // the caller hasnt pinged the local node for more than the lease time
                    // it is the expiration of the connection
                    // we send a disconnection message to caller
                    _connecting.DisconnectFrom(Callers.ElementAt(i).Value);
                }
            }

            for (int i = 0; i < Listenners.Count; ++i)
            {                
                if ((DateTime.Now - Listenners.ElementAt(i).Value.last_updated).Seconds > peerConnectionLeaseRefreshTime)
                {
                    // refresh message to listenner
                    // refreshing the score at the same time
                    HeartbeatAsync(Listenners.ElementAt(i).Value);

                    UpdateNetworkScore();
                }
            }
        }

        private void UpdateNetworkScore()
        {
            var current_peers_score = 0f;
            // the heartbeat should be able to maintain a score routine and see if a peer becomes too low in PeerScore (like latency is increasing)
            for (int i = 0; i < Callers.Count; ++i)
            {
                current_peers_score += Callers.ElementAt(i).Value.score;
            }
            current_peers_score /= Callers.Count;

            // abritrary value for now
            // the idea is to check if the score is decreasing from a previous high value
            // if so, the node will trigger a discovery request to recreate its network with better peers

            ///TODO analyzing the _networkScoreBuffer curve more in depth
            if (current_peers_score * 1.33f < _networkScoreBuffer.Last())
            {
                _peerSampling.BroadcastDiscoveryRequest();
                _networkScoreBuffer.Add(current_peers_score);
            }
            else if (current_peers_score > _networkScoreBuffer.Last())
            {
                _networkScoreBuffer.Add(current_peers_score);
            }
        }
        #region RPC

        protected async Task<bool> HeartbeatAsync(PeerInfo peerInfo)
        {
            var handshakeResponse = await _handshaking.GetHandshakeAsync(peerInfo);

            if (handshakeResponse == null)
            {
                peerInfo.SetScore(0);
                return false;
            }

            peerInfo.ComputeScore(handshakeResponse.ping, handshakeResponse.networkInfoCallersCount, handshakeResponse.networkInfoListennersCount);
            return true;
        }

        #endregion
    }
}
