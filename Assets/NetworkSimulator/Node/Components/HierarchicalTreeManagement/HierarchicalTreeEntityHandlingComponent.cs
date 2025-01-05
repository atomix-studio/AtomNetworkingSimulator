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
        public NodeEntity controller { get; set; }

        [Inject] private BroadcasterComponent _broadcaster;
        [Inject] private PacketRouter _packetRouter;
        [Inject] private NetworkConnectionsComponent _connectingComponent;

        [Header("Paramètres")]
        /// <summary>
        /// Sorting rule looks for the cycles of a gossip message to evaluate the graph distance of a potential children
        /// </summary>
        [SerializeField] private SortingRules _sortingRule = SortingRules.ClosestNode;
        /// <summary>
        /// Base of the logarithmic function used to compute the delay timer of a graph updating, depending on its size (the bigger graph the slower update)
        /// </summary>
        [SerializeField, Range(2, 10)] private int _dynamicDelayFunctionLogarithmicBase = 3;
        /// <summary>
        /// Max tree index distance moved when ranking up or down
        /// </summary>
        [SerializeField] private int _wide = 3;
        /// <summary>
        /// Max number of children per node
        /// </summary>
        [SerializeField] private int _childrenCount = 3;

        /// <summary>
        /// Timeout of the heartbeat with a parent. If time out is reached, the node disconnects and seeks a new parent
        /// </summary>
        [SerializeField] private int _connectionsTimeout = 5000;

        /// <summary>
        /// Timeout of a parent search request round (time before sending a broadcast and looking for the next step when no parent), in milliseconds
        /// </summary>
        [SerializeField] private int _roundTimeout = 1000;

        [Header("Runtime")]
        [ShowInInspector, ReadOnly] private int _currentSubgraphNodesCount = 1;
        [ShowInInspector, ReadOnly] private int _rank;
        [ShowInInspector, ReadOnly] private int _currentRound = 0;
        [ShowInInspector] private bool _parentSearchActive;

        [ShowInInspector, ReadOnly] private PeerInfo _parent;
        [ShowInInspector, ReadOnly] private List<PeerInfo> _children = new List<PeerInfo>();


        private int _closestRank = int.MaxValue;
        private PeerInfo _closestRankPeerInfo = null;

        private float _delayTimer = 0f;
        private Vector3 _initialPosition;

        private Func<int, bool> _checkSortingRuleDelegate;

        public enum SortingRules
        {
            ClosestNode,
            UnderOrEqualsRank,
            AboveOrEqualsRank,
        }

        /*
         TODO comptage du subgraph pour éviter aux gros graph de rank up / down trop souvent par rapport aux petits (le petit bouge en prio, le gros attend plus longtemps)

         placement des nodes en mode tree (via RPC) / penser au rank negatifs (foutre un offset ou normaliser)
           > parentPositionXZ + right * childIndex * parentRank + up * rank

         observer la rapidité VS le broadcast dans cette config

        tester relayCount >= rank dans le research parent pour voir ce que ça donne
         */


        public async void OnInitialize()
        {
            InitializeSortingRuleDelegate();

            _parentSearchActive = true;

            _broadcaster.RegisterPacketHandlerWithMiddleware(typeof(ParentResearchBroadcastPacket), (onreceived) =>
            {
                var broadcastable = onreceived as ParentResearchBroadcastPacket;

                if (_parent == null)
                {
                    if (broadcastable.broadcasterID == this.controller.LocalNodeId)
                        Debug.LogError("Received broadcast from self");

                    _parentSearchActive = true;
                }

                if (_checkSortingRuleDelegate(broadcastable.cyclesDistance) // the rule : we search only for close nodes / the rule can be changed with rank comparison (seeking node at/until/above "cycles" range from this)
                    && _children.Count < _childrenCount
                    && !IsChildren(broadcastable.senderID))
                {
                    // Case 1 : already avalaible children, we send a request
                    if (broadcastable.senderRank < _rank)
                    {
                        // responding to the potential conneciton by a connection request from the receiver
                        // we set the round of the broadcast sender in the request
                        // if the round has changed on the sender, the request will be discarded 
                        _packetRouter.SendRequest(broadcastable.senderAddress, new RankedConnectingRequestPacket(_rank, broadcastable.senderRound), async (response) =>
                        {
                            if (response == null)
                                return;

                            _children.Add(new PeerInfo(response.senderID, broadcastable.senderAddress));

                            _currentSubgraphNodesCount = await CountSubgraphAsync();
                        });
                    }
                    // Case 2 : evaluating a potential children if the subgraph of this parent node would be moved
                    // we seek for the smallest possible move in rank by keeping the closest possible rank on a round
                    // this will be used in the SearchParent function on each round
                    else if (_parent == null && broadcastable.senderRank < _closestRank)
                    {
                        _closestRankPeerInfo = new PeerInfo(broadcastable.senderID, broadcastable.senderAddress);
                        _closestRank = broadcastable.senderRank;
                    }
                }
                // in other cases we just relay the message
                var cloned = (ParentResearchBroadcastPacket)broadcastable.ClonePacket(broadcastable);
                cloned.cyclesDistance++;

                _broadcaster.RelayBroadcast(cloned);

                /*if (broadcastable.senderRank < _rank && broadcastable.relayCount <= 1)
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
                    if (_parent == null && _children.Count < _childrenCount && broadcastable.relayCount <= 1 && broadcastable.senderRank < _closestRank)
                    {
                        _closestRankPeerInfo = new PeerInfo(broadcastable.senderID, broadcastable.senderAddress);
                        _closestRank = broadcastable.senderRank;
                    }

                    // check if has any connection with this higher rank and if the higher rank is within the wide (+ 1 or -1)

                    // relaying the broadcast to other known peers 
                    var cloned = (ParentResearchBroadcastPacket)broadcastable.ClonePacket(broadcastable);
                    cloned.relayCount++;

                    _broadcaster.RelayBroadcast(cloned);
                }*/
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

                // if parent-children rank diff is more than 1, the children that is connection will move his subgraph up so the graph distance parent-children is 1
                if (diff > 1)
                {
                    _rank = respondable.senderRank - 1;
                    SendUpdateRankTreecast(new UpdateRankPacket() { newRank = respondable.senderRank - 2, parentPosition = transform.position });
                }
            });

            _packetRouter.RegisterPacketHandler(typeof(DisconnectionNotificationPacket), (onReceived) =>
            {
                for (int i = 0; i < _children.Count; ++i)
                {
                    if (_children[i].peerID == onReceived.senderID)
                    {
                        _children.RemoveAt(i);
                        break;
                    }
                }

                if (_parent != null && _parent.peerID == onReceived.senderID)
                {
                    _parent = null;
                }
            });

            _packetRouter.RegisterPacketHandler(typeof(RankedConnectionResponsePacket), null);

            _packetRouter.RegisterPacketHandler(typeof(ParentHeartbeatResponsePacket), null);
            _packetRouter.RegisterPacketHandler(typeof(ParentHeartbeatPacket), (packet) =>
            {
                if (packet == null)
                    return;

                for(int i = 0; i < _children.Count; ++i)
                {
                    if(packet.senderID == _children[i].peerID)
                    {
                        _children[i].last_updated = DateTime.UtcNow;
                    }
                }

                var respondable = (packet as IRespondable);
                var response = (ParentHeartbeatResponsePacket)respondable.GetResponsePacket(respondable);
                _packetRouter.SendResponse(respondable, response);
            });

            RegisterTreecastPacketHandler<UpdateRankPacket>((updateRankPacket) =>
            {
                var upp = (updateRankPacket as UpdateRankPacket);
                SetRank(upp.newRank, upp.childIndex, upp.parentPosition);

                // rank decrease as we descend through children nodes
                upp.newRank--;
                upp.parentPosition = transform.position;
            });

            RegisterTreecastPacketHandler<SetColorDowncastPacket>((setColorPacket) =>
            {
                Debug.Log("downcast color");

                var upp = (setColorPacket as SetColorDowncastPacket);
                controller.material.color = upp.newColor;
            });

            RegisterTreecastPacketHandler<SetColorUpcastPacket>((setColorPacket) =>
            {
                Debug.Log("upcast color");

                var upp = (setColorPacket as SetColorUpcastPacket);
                controller.material.color = upp.newColor;

                // when achieve parent, send down tree cast to all graph
                if (_parent == null)
                {
                    SendTreecast(new SetColorDowncastPacket() { newColor = upp.newColor });
                }
            });

            _broadcaster.RegisterPacketHandlerWithMiddleware(typeof(BroadcastColorPacket), (received) =>
            {
                Debug.Log("broadcast color");

                var upp = (received as BroadcastColorPacket);
                controller.material.color = upp.newColor;
            },
            true);

            _packetRouter.RegisterPacketHandler(typeof(SubgraphCountingResponsePacket), null);
            _packetRouter.RegisterPacketHandler(typeof(SubraphCountingRequestPacket), async (onReceived) =>
            {
                await HandleSubgraphCountingAsync((SubraphCountingRequestPacket)onReceived);
            });

            await Task.Delay(3000);

            _initialPosition = transform.position;

            UpdateTask();
        }

        private void InitializeSortingRuleDelegate()
        {
            switch (_sortingRule)
            {
                case SortingRules.ClosestNode: _checkSortingRuleDelegate = (dist) => dist <= 1; return;
                case SortingRules.UnderOrEqualsRank: _checkSortingRuleDelegate = (dist) => dist <= _rank; return;
                case SortingRules.AboveOrEqualsRank: _checkSortingRuleDelegate = (dist) => dist >= _rank; return;
            }

            throw new NotImplementedException();
        }

        private void SetRank(int rank, int childIndex, Vector3 parentPosition)
        {
            _rank = rank;

            /* if(_parent == null)
             {*/
            transform.position = new Vector3(_initialPosition.x, 0, _initialPosition.z) + Vector3.up * 5 * _rank;
            /*}
            else
            {
                int offset = childIndex - _childrenCount / 2;
                if (_childrenCount % 2 == 0)
                    offset++;

                parentPosition.y = 0;
                transform.position = parentPosition + Vector3.right *  offset * _rank * 2 + Vector3.up * 3 * _rank;
            }*/
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
                
                if (receivedPacket is IDowncastablePacket downcastablePacket)
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

        /// <summary>
        /// Sends a cast in the tree (up or down depending on the interface of the message)
        /// Note that messages can be both down and upcast. In this case the message will be sent up by each parent to its parent node, but also to evert of its children
        /// If upcasting, the mesage goest to the higher parent in graph
        /// If down casting, the message goes in every node of the subgraph from the caller
        /// </summary>
        /// <param name="treecastablePacket"></param>
        /// <exception cref="Exception"></exception>
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

        private void SendUpdateRankTreecast(UpdateRankPacket updateRankPacket)
        {
            // the work is now done by the packet router as it is the solo entry point for transport layer 
            // it is more secure to handle ids down that system cause if they aare unset for some reason,
            // it will screw up their ability to be received as they will be blocked by the broadcaster middleware of the receiver
            // (if two or 3 broadcastable packet arrives with a string.Empty id, all other broadcasts with string.Empty id will be ignored as well

            updateRankPacket.broadcasterID = controller.LocalNodeId;
            updateRankPacket.broadcastID = NodeRandom.UniqueID();// Guid.NewGuid().ToString();

            if (updateRankPacket is IUpcastablePacket)
            {
                _packetRouter.Send(_parent.peerAdress, updateRankPacket);
            }
            else if (updateRankPacket is IDowncastablePacket)
            {
                UpdateRankPacket current = updateRankPacket;

                for (int i = 0; i < _children.Count; ++i)
                {
                    current.childIndex = i;
                    _packetRouter.Send(_children[i].peerAdress, current);
                    current = (UpdateRankPacket)updateRankPacket.ClonePacket(current);
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
                _delayTimer = NodeRandom.Range(.5f, 1f)
                    // we multiply the timer by the log on base 2 of the children count. 
                    // it allows bigger graph to evolute slower that smallest one, for fastest convergence
                    * ((float)Math.Log(_currentSubgraphNodesCount, _dynamicDelayFunctionLogarithmicBase));

                // insuring a minimal value (log2(1) = 0)
                _delayTimer = Math.Max(_delayTimer, .5f);

                await Task.Delay((int)(_delayTimer * 1000));

                if (this == null)
                    break;

                if (_parent != null)
                    await PingParent();
                else
                    await SearchParent();
                UpdateChildrenConnections();

                if (this == null)
                    break;
            }
        }

        private async Task SearchParent()
        {
            if (_parent != null)
                return;

            if (!_parentSearchActive)
                return;

            // a node can stop searching if it has children and if it doesn't received any incoming parent search
            if (_children.Count > 0)
                _parentSearchActive = false;

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
                if (_closestRankPeerInfo != null)
                {
                    //Debug.Log(1);

                    // case of a parent that is under another potential parent node
                    // the local parent will set the subgraph above this potential parent to allow the other node to connect itself to it on the next round
                    SendUpdateRankTreecast(new UpdateRankPacket() { newRank = _closestRank + 1, parentPosition = transform.position });

                    _closestRankPeerInfo = null;
                    _closestRank = int.MaxValue;
                }
                else
                {
                    //Debug.Log(2);

                    // move random up or down all te subgraph to be avalaible to other children
                    if (NodeRandom.Range(0, 100) > 49)
                    {
                        SetRank(_rank + NodeRandom.Range(1, _wide), 0, Vector3.zero);

                        Debug.Log($"Ranking {controller.LocalNodeId} up to rank {_rank}");
                    }
                    else
                    {
                        SetRank(_rank - NodeRandom.Range(1, _wide), 0, Vector3.zero);

                        Debug.Log($"Ranking {controller.LocalNodeId} down to rank {_rank}");
                    }

                    // tree cast to subgraph to modify the ranks

                    SendUpdateRankTreecast(new UpdateRankPacket() { newRank = _rank - 1, parentPosition = transform.position });
                }
            }
        }

        private async Task PingParent()
        {
            _packetRouter.SendRequest(_parent.peerAdress, new ParentHeartbeatPacket(), (heartbeatResponse) =>
            {
                if (heartbeatResponse == null)
                {
                    DisconnectFromParent();
                }

            }, _connectionsTimeout);
        }

        private void DisconnectFromParent()
        {
            if (_parent == null)
                return;

            _packetRouter.Send(_parent.peerAdress, new DisconnectionNotificationPacket());
            _parent = null;
        }

        private void UpdateChildrenConnections()
        {
            // updates children connections
            // parent should receive heartbeat from children on a regular basis
            for (int i = 0; i < _children.Count; ++i)
            {
                if ((DateTime.Now - _children[i].last_updated).Milliseconds > _connectionsTimeout)
                {
                    Debug.Log("Children timed out");

                    _packetRouter.Send(_children[i].peerAdress, new DisconnectionNotificationPacket());
                    _children.RemoveAt(i);
                    i--;
                }
            }
        }

        private async Task HandleSubgraphCountingAsync(SubraphCountingRequestPacket subraphCountingRequestPacket)
        {
            if (_children.Count == 0)
            {
                _packetRouter.SendResponse(subraphCountingRequestPacket, new SubgraphCountingResponsePacket() { childrenCount = 1 });
            }
            else
            {
                var tasks = new Task<SubgraphCountingResponsePacket>[_children.Count];
                for (int i = 0; i < tasks.Length; ++i)
                {
                    tasks[i] = _packetRouter.SendRequestAsync<SubgraphCountingResponsePacket>(_children[i].peerAdress, new SubraphCountingRequestPacket());
                }

                var results = await Task.WhenAll(tasks);
                int count = 0;
                for (int i = 0; i < results.Length; ++i)
                {
                    if (results[i] != null)
                        count += results[i].childrenCount;
                }

                _packetRouter.SendResponse(subraphCountingRequestPacket, new SubgraphCountingResponsePacket() { childrenCount = count + 1 });
            }
        }

        /// <summary>
        /// This function allows a node to request all its subgraph to count the nodes
        /// The mesage will recursively goes throught parent to children until the leaf nodes and then go up by aggregating the values
        /// The caller will receive only one message for each direct children 
        /// </summary>
        private async Task<int> CountSubgraphAsync()
        {
            var tasks = new Task<SubgraphCountingResponsePacket>[_children.Count];
            for (int i = 0; i < tasks.Length; ++i)
            {
                tasks[i] = _packetRouter.SendRequestAsync<SubgraphCountingResponsePacket>(_children[i].peerAdress, new SubraphCountingRequestPacket());
            }

            var results = await Task.WhenAll(tasks);
            int count = 0;
            for (int i = 0; i < results.Length; ++i)
            {
                if (results[i] != null)
                    count += results[i].childrenCount;
            }

            // counting self as well
            count += 1;

            Debug.Log("Total counted nodes : " + count);

            return count;
        }

        [Button]
        private async void CountSubgraph()
        {
            await CountSubgraphAsync();
        }


        [Button]
        private void SetColorUpcast()
        {
            SendTreecast(new SetColorUpcastPacket() { newColor = new Color(NodeRandom.Range(0f, 1f), NodeRandom.Range(0f, 1f), NodeRandom.Range(0f, 1f), 1) });
        }

        [Button]
        private void SetColorDowncast()
        {
            SendTreecast(new SetColorDowncastPacket() { newColor = new Color(NodeRandom.Range(0f, 1f), NodeRandom.Range(0f, 1f), NodeRandom.Range(0f, 1f), 1) });
        }

        [Button]
        private void SetColorBroadcast()
        {
            _broadcaster.SendBroadcast(new BroadcastColorPacket() { newColor = new Color(NodeRandom.Range(0f, 1f), NodeRandom.Range(0f, 1f), NodeRandom.Range(0f, 1f), 1) });
        }

        private void OnDrawGizmos()
        {
            if (!this.enabled)
                return;

            foreach (var child in _children)
            {
                var from = WorldSimulationManager.nodeAddresses[child.peerAdress].transform.position;
                var to = transform.position;

                Gizmos.DrawLine(from, to);
            }

            if (_parent == null)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawSphere(transform.position + Vector3.up, .5f);
                Gizmos.color = Color.white;
            }
        }
    }
}
