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
        [SerializeField] private int _childrenCount = 3;

        /// <summary>
        /// timeout of a request round, in milliseconds
        /// </summary>
        [SerializeField] private int _roundTimeout = 1000;

        [ShowInInspector, ReadOnly] private int _rank;
        [ShowInInspector, ReadOnly] private int _currentRound = 0;

        public NodeEntity controller { get; set; }

        [ShowInInspector, ReadOnly] private PeerInfo _parent;
        [ShowInInspector, ReadOnly] private List<PeerInfo> _children = new List<PeerInfo>();

        private float _delayTimer = 0f;
        private Vector3 _initialPosition;

        /* [Serializable]
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
         }*/


        public async void OnInitialize()
        {
            if (NodeRandom.Range(0, 100) > 90)
            {
                SetRank(1);
            }

            _broadcaster.RegisterPacketHandlerWithMiddleware(typeof(ParentResearchBroadcastPacket), (onreceived) =>
            {
                var broadcastable = onreceived as ParentResearchBroadcastPacket;

                if (broadcastable.senderRank < _rank  /* && broadcastable.relayCount == _rank*/)
                {
                    // accepting a connection of same rank if rank is missing
                    if (_children.Count < _childrenCount
                    && !IsChildren(broadcastable.senderID))
                    {
                        // responding to the potential conneciton by a connection request from the receiver
                        // we set the round of the broadcast sender in the request
                        // if the round has changed on the sender, the request will be discarded 
                        _packetRouter.SendRequest(broadcastable.senderAddress, new RankedConnectingRequestPacket(_rank, broadcastable.senderRound), (response) =>
                        {
                            if (response == null)
                                return;

                            _children.Add(new PeerInfo(response.senderID, broadcastable.senderAddress));
                        });
                    }
                }
                else
                {
                    // check if has any connection with this higher rank and if the higher rank is within the wide (+ 1 or -1)

                    // relaying the broadcast to other known peers 
                    var cloned = (ParentResearchBroadcastPacket)broadcastable.ClonePacket(broadcastable);
                    cloned.relayCount++;

                    _broadcaster.RelayBroadcast(cloned);
                }
            });

            // connecting requests are send by potential parent when they receive a broadcast from a node with lower rank
            _packetRouter.RegisterPacketHandler(typeof(RankedConnectingRequestPacket), (packet) =>
            {
                var respondable = (packet as RankedConnectingRequestPacket);
                var response = (RankedConnectionResponsePacket)respondable.GetResponsePacket(respondable).packet;

                // if round has changed since the broadcast, discarding
                // round change on request timeout
                if (respondable.senderRound != _currentRound)
                    return;

                // if parent already found
                if (_parent != null)
                    return;

                if (IsChildren(respondable.senderID))
                    return;

                _parent = new PeerInfo(respondable.senderID, respondable.senderAdress);

                // accept
                _packetRouter.SendResponse(respondable, response);

                var diff = respondable.senderRank - _rank;
                Debug.Log("Rank difference > " + diff);

                // if parent-children rank diff is more than 1, the parent will downgrad his subgraph OR the children will upgrad the subgraph depending on 
                // 
                if (diff > 1)
                {
                    _rank = respondable.senderRank - 1;
                    SendTreecast(new UpdateRankPacket() { newRank = respondable.senderRank - 2 });
                }
            });

            _packetRouter.RegisterPacketHandler(typeof(RankedConnectionResponsePacket), null);

            RegisterTreecastPacketHandler<UpdateRankPacket>((updateRankPacket) =>
            {
                var upp = (updateRankPacket as UpdateRankPacket);
                SetRank(upp.newRank);

                // rank decrease as we descend through children nodes
                upp.newRank--;
            });

            await Task.Delay(3000);

            _initialPosition = transform.position;

            UpdateTask();
        }

        private void SetRank(int rank)
        {
            _rank = rank;
            transform.position = _initialPosition +  Vector3.up * 3 * _rank;
        }

        public void RegisterTreecastPacketHandler<T>(Action<INetworkPacket> packetReceiveHandler) where T : ITreecastablePacket
        {
            _packetRouter.RegisterPacketHandler(typeof(T), (receivedPacket) =>
            {
                if (receivedPacket is ITreecastablePacket == false)
                    return;

                packetReceiveHandler?.Invoke(receivedPacket);

                // the router checks for packet that are IBroadcastable and ensure they haven't been too much relayed
                // if the packet as reached is maximum broadcast cycles on the node and the router receives it one more time
                if (receivedPacket is IUpcastablePacket upcastablePacket)
                {
                    // packet stop here
                    if (_parent == null)
                        return;

                    var relayedPacket = upcastablePacket.ClonePacket(upcastablePacket);
                    _packetRouter.Send(_parent.peerAdress, relayedPacket);
                }
                else if (receivedPacket is IDowncastablePacket downcastablePacket)
                {
                    for (int i = 0; i < _children.Count; i++)
                    {
                        var relayedPacket = downcastablePacket.ClonePacket(downcastablePacket);
                        _packetRouter.Send(_children[i].peerAdress, relayedPacket);
                    }
                }
            },
            true);
        }

        public void SendTreecast(ITreecastablePacket treecastablePacket)
        {
            // the work is now done by the packet router as it is the solo entry point for transport layer 
            // it is more secure to handle ids down that system cause if they aare unset for some reason,
            // it will screw up their ability to be received as they will be blocked by the broadcaster middleware of the receiver
            // (if two or 3 broadcastable packet arrives with a string.Empty id, all other broadcasts with string.Empty id will be ignored as well

            treecastablePacket.broadcasterID = controller.LocalNodeId;
            treecastablePacket.broadcastID = NodeRandom.UniqueID();// Guid.NewGuid().ToString();

            if (treecastablePacket is IUpcastablePacket)
            {
                _packetRouter.Send(_parent.peerAdress, treecastablePacket);
            }
            else if (treecastablePacket is IDowncastablePacket)
            {
                INetworkPacket current = treecastablePacket;

                for (int i = 0; i < _children.Count; ++i)
                {
                    _packetRouter.Send(_children[i].peerAdress, current);
                    current = treecastablePacket.ClonePacket(current);

                }
            }
            else throw new Exception("Packe ttype not handled");
        }

        public bool IsChildren(long id)
        {
            foreach (var group in _children)
                if (group.peerID == id)
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
            if (_parent == null)
            {
                var packet = new ParentResearchBroadcastPacket();
                packet.senderAddress = controller.LocalNodeAdress;
                packet.senderRank = _rank;
                packet.senderRound = _currentRound;

                _broadcaster.SendBroadcast(packet);

                // waiting a timeout of the request
                await Task.Delay(_roundTimeout);

                // prevent after timeout handling of messages
                _currentRound++;

                // if not finding any connection before the timeout
                // ranking the node up
                // remind that nodes can only connect with node from the same group, and the rank is equal to the fanout distance of the broadcast (which should indicate a distance from the sender in a well constructed gossip network)
                if (_parent == null)
                {
                    // move random up or down all te subgraph to be avalaible to other children
                    if (NodeRandom.Range(0, 100) > 49)
                    {
                        SetRank(_rank + 1);

                        Debug.Log($"Ranking {controller.LocalNodeId} up to rank {_rank}");
                    }
                    else
                    {
                        SetRank(_rank - 1);

                        Debug.Log($"Ranking {controller.LocalNodeId} down to rank {_rank}");
                    }

                    // tree cast to subgraph to modify the ranks

                    SendTreecast(new UpdateRankPacket() { newRank = _rank - 1 });
                }
            }
            else
            {

            }
        }

        public void UpCast()
        {

        }

        public void DownCast()
        {

        }

        private void OnDrawGizmos()
        {
            foreach (var child in _children)
            {
                var from = WorldSimulationManager.nodeAddresses[child.peerAdress].transform.position;
                var to = transform.position;

                Gizmos.DrawLine(from, to);
            }
        }
    }
}
