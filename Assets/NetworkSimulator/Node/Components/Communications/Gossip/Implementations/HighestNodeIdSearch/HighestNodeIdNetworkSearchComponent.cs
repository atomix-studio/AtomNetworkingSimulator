using Atom.Broadcasting;
using Atom.CommunicationSystem;
using Atom.DependencyProvider;
using Sirenix.OdinInspector;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unity.VisualScripting;
using UnityEngine;

namespace Atom.Components.Gossip
{
    /// <summary>
    /// A gossip showcase about finding the highest id over the network
    /// </summary>
    public class HighestNodeIdNetworkSearchComponent : MonoBehaviour, IGossipDataHandler<NodeIdGossipPacket>, INodeComponent
    {
        [Inject] private GossipComponent _gossipComponent;
        [Inject] private NetworkConnectionsComponent _connectionsComponent;
        [Inject] private PacketRouter _packetRouter;

        // each node keeps a track of the highest known peerID
        // if the node receive a gossip about a higher ID node, it gossips a new packet about it
        private PeerInfo _currentHighestKnownPeerId;

        public NodeEntity controller { get; set; }

        public void OnInitialize()
        {
            _gossipComponent.RegisterGossipHandler<NodeIdGossipPacket>(this);
            _gossipComponent.RegisterGossipPreUpdateCallback(OnBeforeGossipProcess);
        }

        [Button]
        private void StartHighestIdNetworkSearchGossip()
        {
            GetLocalHighestPeerId();

            var new_gossip = new NodeIdGossipPacket() { HighestIdKnownPeer = new PeerInfo(_currentHighestKnownPeerId) };
            new_gossip.gossipStartedTime = DateTime.Now;
            _gossipComponent.BufferAdd(new_gossip);
        }

        private void GetLocalHighestPeerId()
        {
            long maxId = long.MinValue;
            PeerInfo maxIdPeer = null;

            foreach (var v in _connectionsComponent.Connections)
            {
                if (v.Key > maxId)
                {
                    maxId = v.Key;
                    maxIdPeer = v.Value;
                }
            }

            _currentHighestKnownPeerId = maxIdPeer;
        }

        public void OnReceiveGossip(GossipComponent context, NodeIdGossipPacket data)
        {
            if(_currentHighestKnownPeerId == null)
            {
                GetLocalHighestPeerId();
            }

            if(data.HighestIdKnownPeer.peerID > _currentHighestKnownPeerId.peerID)
            {
                var relayed_gossip = (NodeIdGossipPacket)data.ClonePacket(data);
                context.BufferAdd(relayed_gossip);
                _currentHighestKnownPeerId = data.HighestIdKnownPeer;
            }
        }

        private void OnBeforeGossipProcess()
        {
            if (_currentHighestKnownPeerId == null)
                return;

            _gossipComponent.BufferAdd(new NodeIdGossipPacket() { HighestIdKnownPeer = _currentHighestKnownPeerId });
        }

        void OnDrawGizmos()
        {
            if (!Application.isPlaying)
                return;

            if(_currentHighestKnownPeerId != null)
            {
                var pos = WorldSimulationManager.nodeAddresses[_currentHighestKnownPeerId.peerAdress].transform.position;
                Gizmos.color = Color.cyan;
                Gizmos.DrawSphere(pos + Vector3.up, .8f);
                Gizmos.color = Color.white;
            }
        }
    }
}
