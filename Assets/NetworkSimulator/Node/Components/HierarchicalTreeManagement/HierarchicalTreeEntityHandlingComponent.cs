using Atom.Broadcasting;
using Atom.CommunicationSystem;
using Atom.Components.Connecting;
using Atom.DependencyProvider;
using Atom.Helpers;
using Sirenix.OdinInspector;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Atom.Components.HierarchicalTree
{
    public class HierarchicalTreeEntityHandlingComponent : MonoBehaviour, INodeComponent
    {
        [Inject] private BroadcasterComponent _broadcaster;
        [Inject] private PacketRouter _packetRouter;
        [Inject] private NetworkConnectionsComponent _connectingComponent;

        [SerializeField] private int _wide = 3;
        [SerializeField] private int _groupCount = 3;

        /// <summary>
        /// timeout of a request round, in milliseconds
        /// </summary>
        [SerializeField] private int _roundTimeout = 1000;

        [ShowInInspector, ReadOnly] private int _rank;
        [ShowInInspector, ReadOnly] private int _currentRound = 0;

        public NodeEntity controller { get; set; }

        [ShowInInspector, ReadOnly] private List<RankedClusterData> _rankedClusters = new List<RankedClusterData>();

        private float _delayTimer = 0f;

        [Serializable]
        public class RankedClusterData
        {
            public int Rank;

            [SerializeField] private List<PeerInfo> _connectionsDebug = new List<PeerInfo>();
            private Dictionary<long, PeerInfo> _connections = new Dictionary<long, PeerInfo>();

            public Dictionary<long, PeerInfo> Connections
            {
                get
                {
                    return _connections;
                }                
            }

            public void AddConnection(PeerInfo peerInfo)
            {
                _connections.Add(peerInfo.peerID, peerInfo);
                _connectionsDebug.Add(peerInfo);
            }
        }

        public async void OnInitialize()
        {
            //
            for (int i = 0; i < _wide; i++)
            {
                _rankedClusters.Add(new RankedClusterData() { Rank = i });
            }

            if (NodeRandom.Range(0, 100) > 90)
            {
                transform.position += Vector3.up * 3;
                _rank = 1;
            }

            _broadcaster.RegisterPacketHandlerWithMiddleware(typeof(RankedConnectionSearchBroadcastPacket), (onreceived) =>
            {
                var broadcastable = onreceived as RankedConnectionSearchBroadcastPacket;

                if (broadcastable.senderRank == _rank  && broadcastable.relayCount == _rank)
                {
                    // accepting a connection of same rank if rank is missing
                    if (_rankedClusters[_rank].Connections.Count < _groupCount
                    && !HasAnyConnectionTo(broadcastable.senderID))
                    {
                        // responding to the potential conneciton by a connection request from the receiver
                        // we set the round of the broadcast sender in the request
                        // if the round has changed on the sender, the request will be discarded 
                        _packetRouter.SendRequest(broadcastable.senderAddress, new RankedConnectingRequestPacket(_rank, broadcastable.senderRound), (response) =>
                        {
                            if (response == null)
                                return;

                            _rankedClusters[_rank].AddConnection(new PeerInfo(response.senderID, broadcastable.senderAddress));
                        });
                    }
                }
                else 
                {
                    // check if has any connection with this higher rank and if the higher rank is within the wide (+ 1 or -1)

                    // relaying the broadcast to other known peers 
                    var cloned = (RankedConnectionSearchBroadcastPacket)broadcastable.ClonePacket(broadcastable);
                    cloned.relayCount++;

                    _broadcaster.RelayBroadcast(cloned);
                }
            });

            _packetRouter.RegisterPacketHandler(typeof(RankedConnectingRequestPacket), (packet) =>
            {
                var respondable = (packet as RankedConnectingRequestPacket);
                var response = (RankedConnectionResponsePacket)respondable.GetResponsePacket(respondable).packet;

                // if round has changed since the broadcast, discarding
                // round change on request timeout
                if (respondable.senderRound != _currentRound)
                    return;

                if (_rankedClusters[respondable.senderRank ].Connections.Count < _groupCount 
                && !HasAnyConnectionTo(respondable.senderID))
                {
                    _rankedClusters[respondable.senderRank].AddConnection(new PeerInfo(respondable.senderID, respondable.senderAdress));
                    _packetRouter.SendResponse(respondable, response);
                }
            });

            _packetRouter.RegisterPacketHandler(typeof(RankedConnectionResponsePacket), null);

            await Task.Delay(3000);

            UpdateTask();
        }

        public bool HasAnyConnectionTo(long id)
        {
            foreach (var group in _rankedClusters)
                if (group.Connections.ContainsKey(id))
                    return true;

            return false;
        }

        private async void UpdateTask()
        {

            while (true)
            {
                _delayTimer = NodeRandom.Range(.5f, 1f);

                await Task.Delay((int)(_delayTimer * 1000));

                if (this == null)
                    break;

                await UpdateRankedClusters();

                if (this == null)
                    break;
            }
        }

        private async Task UpdateRankedClusters()
        {          

            bool needs_connections = NeedConnections();

            if (!needs_connections)
                return;
/*
            // if node is higher that rank = 1 => can seek for higher parent if and only if node has any children
            if (_rank > 1 && _rankedClusters[_rank - 1].Connections.Count == 0)
                return;*/

            var packet = new RankedConnectionSearchBroadcastPacket();
            packet.senderAddress = controller.LocalNodeAdress;
            packet.senderRank = _rank;
            packet.senderRound = _currentRound;

            _broadcaster.SendBroadcast(packet);

            // waiting a timeout of the request
            await Task.Delay(_roundTimeout);

            // if not finding any connection before the timeout
            // ranking the node up
            // remind that nodes can only connect with node from the same group, and the rank is equal to the fanout distance of the broadcast (which should indicate a distance from the sender in a well constructed gossip network)
            if (NeedConnections())
            {
                if (_rank > 1 && _rankedClusters[_rank - 1].Connections.Count == 0)
                    return;

                if (NodeRandom.Range(0, 100) > 90)
                {
                    transform.position += Vector3.up * 3;
                    _rank++;
                    Debug.Log($"Ranking {controller.LocalNodeId} up to rank {_rank}");
                }                
            }
        }

        private bool NeedConnections()
        {
            /*var needs_connections = false;

            foreach (var group in _rankedClusters)
            {
                if (group.Value.Connections.Count != _groupCount)
                {
                    needs_connections = true;
                    break;
                }
            }

            return needs_connections;*/

            return _rankedClusters[_rank].Connections.Count < _groupCount;
        }

        public void UpCast()
        {

        }

        public void DownCast()
        {

        }


        private void OnDrawGizmos()
        {
            foreach (var group in _rankedClusters)
            {
                foreach (var connections in group.Connections)
                {
                    var from = WorldSimulationManager.nodeAddresses[connections.Value.peerAdress].transform.position;
                    var to = transform.position;

                    Gizmos.DrawLine(from, to);
                }
            }
        }
    }
}
