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
        [SerializeField] private bool _alternativeMode;

        [SerializeField] private bool _singleGraphParenting;
        // A node will seek further every X rounds without parent
        [SerializeField] private int _roundsBeforeAugmentRange = 10;
        [SerializeField, MinMaxSlider(.2f, 2f)] private Vector2 _roundDelayTimerRange = new Vector2();
        /// <summary>
        /// Sorting rule looks for the cycles of a gossip message to evaluate the graph distance of a potential children
        /// </summary>
        [SerializeField] private GraphSortingRules _sortingRule = GraphSortingRules.ClosestCyclesDistance;
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
        [SerializeField] private int _childrenRequestTimeout = 333;

        /// <summary>
        /// Timeout of a parent search request round (time before sending a broadcast and looking for the next step when no parent), in milliseconds
        /// </summary>
        [SerializeField] private int _roundTimeout = 1000;
        [SerializeField] private int _relayedTreecastBufferSize = 50;

        [Header("Runtime")]
        [ShowInInspector, ReadOnly] private int _currentSubgraphNodesCount = 1;
        [ShowInInspector, ReadOnly] private int _currentSubgraphDepth = 0;
        [ShowInInspector, ReadOnly] private int _rank;
        [ShowInInspector, ReadOnly] private int _currentRound = 0;
        [ShowInInspector] private bool _parentSearchActive;
        [ShowInInspector] private bool _inMainGraph;

        [ShowInInspector, ReadOnly] private PeerInfo _parent;
        [ShowInInspector, ReadOnly] private List<PeerInfo> _children = new List<PeerInfo>();

        private int _closestUpperBroadcasterRank = int.MaxValue;
        private PeerInfo _closestUpperBroadcasterPeerInfo = null;

        private float _delayTimer = 0f;
        private Vector3 _initialPosition;
        private HashSet<long> _pendingRequestToChildren;
        private HashSet<long> _relayedTreecastsBuffer;

        private Func<int, int, bool> _checkSortingRuleDelegate;
        private bool _waitParentInitialization = false;

        public int currentRank => _rank;
        public List<PeerInfo> children => _children;

        public GraphSortingRules SortingRule
        {
            get
            {
                return _sortingRule;
            }
            set
            {
                _sortingRule = value;
            }
        }

        public enum GraphSortingRules
        {
            ClosestCyclesDistance,
            UnderOrEqualsRank,
            AboveOrEqualsRank,
        }

        /*
         TODO comptage du subgraph pour éviter aux gros graph de rank up / down trop souvent par rapport aux petits (le petit bouge en prio, le gros attend plus longtemps)

        Fixer comptage depuis parent

        Optimiser en connectants directements aux pairs du networking connections

         placement des nodes en mode tree (via RPC) / penser au rank negatifs (foutre un offset ou normaliser)
           > parentPositionXZ + right * childIndex * parentRank + up * rank

         observer la rapidité VS le broadcast dans cette config
         */

        #region Packet Initialization 
        public async void OnInitialize()
        {
            LocalReset();
            InitializeSortingRuleDelegate();

            _parent = null;

            if (_alternativeMode)
            {
                _packetRouter.RegisterPacketHandler(typeof(ParentConnectionResponsePacket), null);
                _packetRouter.RegisterPacketHandler(typeof(ParentConnectionRequestPacket), async (packet) =>
                {
                    Debug.Log("Received parent connection request");

                    var respondable = (packet as ParentConnectionRequestPacket);

                    if(_children.Count > 0)
                    {
                        // Impossible to be parented with already children = CYCLE
                        return;
                    }

                    // if parent already found
                    if (_parent != null)
                        return;

                    if (IsChildren(respondable.senderID))
                        return;

                    var response = (ParentConnectionResponsePacket)respondable.GetResponsePacket(respondable);
                    _pendingParentRequests.Add(new PendingParentRequest() { ParentConnectionRequestPacket = respondable, ParentConnectionResponsePacket = response });

                    if (!_awaitIncomingParentRequests)
                    {
                        _awaitIncomingParentRequests = true;

                        await WaitBeforeFilterParentRequests();
                    }
                });

                _packetRouter.RegisterPacketHandler(typeof(ChildrenValidationPacket), (onReceived) =>
                {
                    var packet = (ChildrenValidationPacket)onReceived;

                    _pendingValidations.Add(new PendingValidationCallback()
                    {
                        ValidationPacket = packet,
                        ValidationResponsePacket = (ChildrenValidationResponsePacket)packet.GetResponsePacket(packet)
                    });

                    if (_pendingValidations.Count == _children.Count)
                    {
                        for (int i = 0; i < _pendingValidations.Count; ++i)
                            _packetRouter.SendResponse(_pendingValidations[i].ValidationPacket, _pendingValidations[i].ValidationResponsePacket);
                    }
                });

                _packetRouter.RegisterPacketHandler(typeof(ChildrenSearchActivationPacket), async (onReceived) =>
                {
                    var packet = (ChildrenSearchActivationPacket)onReceived;
                    await SearchPotentialChildren();
                });

                RegisterTreecastPacketHandler<UpdateRankPacket>((updateRankPacket) =>
                {
                    if (_parent == null)
                    {
                        Debug.LogError("PROBLEM");
                        return;
                    }

                    var upp = (updateRankPacket as UpdateRankPacket);                                      
                    SetRank(upp.newRank);
                    // rank decrease as we descend through children nodes
                    upp.newRank--;
                    upp.parentPosition = transform.position;
                });

                InitializeNetworkMaintainingPackets();
                InitializeTreecastPackets();

                await Task.Delay(200);
                _initialPosition = transform.position;

                await Task.Delay(4000);

                if (controller.IsBoot)
                {
                    StartTreeGeneration();
                }
            }
            else
            {
                _parentSearchActive = true;

                if (_singleGraphParenting)
                {
                    InitializeSingleGraphParentResearchPackets();
                }
                else
                    InitializeDefaultParentResearchPackets();

                InitializeDefaultConnectivityPackets();
                InitializeNetworkMaintainingPackets();
                InitializeTreecastPackets();

                await Task.Delay(4000);

                _initialPosition = transform.position;

                SearchingUpdateTask();
                MaintainingUpdateTask();
            }
        }

        #region Alternative Mode
        
        public async void StartTreeGeneration()
        {
            await SearchPotentialChildren();
        }


        private List<PendingValidationCallback> _pendingValidations = new List<PendingValidationCallback>();
        private bool _awaitIncomingParentRequests;

        private List<PendingParentRequest> _pendingParentRequests = new List<PendingParentRequest>();

        private class PendingParentRequest
        {
            public ParentConnectionRequestPacket ParentConnectionRequestPacket { get; set; }
            public ParentConnectionResponsePacket ParentConnectionResponsePacket { get; set; }
        }

        private class PendingValidationCallback
        {
            public ChildrenValidationPacket ValidationPacket { get; set; }
            public ChildrenValidationResponsePacket ValidationResponsePacket { get; set; }
        }

        private async Task WaitBeforeFilterParentRequests()
        {
            await Task.Delay(_roundTimeout);

            if (_parent != null)
            {
                Debug.LogError("Parent already found");
                return;
            }

            var local = transform.position;
            local.y = 0;

            Debug.LogError($"{controller.LocalNodeId} sorting {_pendingParentRequests.Count} parenting requests");

            _pendingParentRequests.Sort((a, b) =>
            {
                var p_a = WorldSimulationManager.nodeAddresses[a.ParentConnectionRequestPacket.senderAdress].transform.position;
                p_a.y = 0;
                var p_b = WorldSimulationManager.nodeAddresses[b.ParentConnectionRequestPacket.senderAdress].transform.position;
                p_b.y = 0;

                return Vector3.Distance(p_a, local).CompareTo(Vector3.Distance(p_b, local));
            });

            Debug.LogError($"{controller.LocalNodeId} accepting parent request from {_pendingParentRequests[0].ParentConnectionRequestPacket.senderID}");

            _parent = new PeerInfo(_pendingParentRequests[0].ParentConnectionRequestPacket.senderID, _pendingParentRequests[0].ParentConnectionRequestPacket.senderAdress);
            _packetRouter.SendResponse(_pendingParentRequests[0].ParentConnectionRequestPacket, _pendingParentRequests[0].ParentConnectionResponsePacket);

            _awaitIncomingParentRequests = false;
        }

        private async Task SearchPotentialChildren()
        {
            _delayTimer = _roundDelayTimerRange.x;

            while (true)
            {
                await Task.Delay((int)(_delayTimer * 1000));

                var local = transform.position;
                local.y = 0;

                var list = _connectingComponent.Connections.Values.ToList();
                list.Sort((a, b) =>
                {
                    var p_a = WorldSimulationManager.nodeAddresses[a.peerAdress].transform.position;
                    p_a.y = 0;
                    var p_b = WorldSimulationManager.nodeAddresses[b.peerAdress].transform.position;
                    p_b.y = 0;

                    return Vector3.Distance(p_a, local).CompareTo(Vector3.Distance(p_b, local));
                });

                for (int i = 0; i < _children.Count; ++i)
                {
                    for (int j = 0; j < list.Count; ++j)
                    {
                        if (list[j].peerID == _children[i].peerID)
                        {
                            list.RemoveAt(j);
                            j--;
                        }
                    }
                }

                var toFind = Math.Min(_childrenCount - _children.Count, list.Count);
                Task<ParentConnectionResponsePacket>[] tasks = new Task<ParentConnectionResponsePacket>[toFind];

                for (int i = 0; i < toFind; ++i)
                {
                    tasks[i] = _packetRouter.SendRequestAsync<ParentConnectionResponsePacket>(list[i].peerAdress, new ParentConnectionRequestPacket(transform.position, _rank, _children.Count, 0), _childrenRequestTimeout);

                }

                var results = await Task.WhenAll(tasks);

                Debug.Log("Round end");


                bool has_response = false;
                for (int i = 0; i < results.Length; ++i)
                {
                    if (results[i] == null)
                    {
                        // timeout
                    }
                    else
                    {
                        if (IsChildren(results[i].senderID))
                        {
                            Debug.LogError("Already children");
                            return;
                        }

                        has_response = true;

                        _children.Add(new PeerInfo(list[i].peerID, list[i].peerAdress));
                        _packetRouter.Send(list[i].peerAdress, new UpdateRankPacket() { newRank = _rank - 1, parentPosition = transform.position, broadcasterID = controller.LocalNodeId, broadcastID = NodeRandom.UniqueID() });

                        Debug.LogError($"{controller.LocalNodeId} connect with {list[i].peerID}");

                        //_currentSubgraphNodesCount = await CountSubgraphAsync();
                    }
                }

                if (!has_response || _children.Count >= _childrenCount || list.Count == 0)
                {
                    if(_parent == null)
                    {
                        // activation directe de la recherche d'enfant sur les enfants
                        for (int i = 0; i < _children.Count; ++i)
                        {
                            _packetRouter.Send(_children[i].peerAdress, new ChildrenSearchActivationPacket());
                        }
                    }
                    else
                    {
                        // callback au parent, quand tous les enfants du parent sont valide, le parent leur permet d'activer la recherche sur leurs propres enfants
                        _packetRouter.SendRequest(_parent.peerAdress, new ChildrenValidationPacket(), (onResponse) =>
                        {
                            for (int i = 0; i < _children.Count; ++i)
                            {
                                _packetRouter.Send(_children[i].peerAdress, new ChildrenSearchActivationPacket());
                            }
                        }, -1);
                    }
                    

                    break;
                }
            }

            Debug.LogError($"{controller.LocalNodeId} end searching children");
        }

        #endregion

        #region Legacy
        private void InitializeDefaultConnectivityPackets()
        {
            // connecting requests are send by potential parent when they receive a broadcast from a node with lower rank
            _packetRouter.RegisterPacketHandler(typeof(ParentConnectionRequestPacket), (packet) =>
            {
                var respondable = (packet as ParentConnectionRequestPacket);

                // if round has changed since the broadcast, discarding
                // round change on request timeout
                if (respondable.childrenRoundAtRequest != _currentRound)
                    return;

                // CHECK HERE
                _currentRound++;

                // if parent already found
                if (_parent != null)
                    return;

                if (IsChildren(respondable.senderID))
                    return;

                // parent found, stop searching
                _parentSearchActive = false;
                _waitParentInitialization = true;

                var response = (ParentConnectionResponsePacket)respondable.GetResponsePacket(respondable).packet;

                _parent = new PeerInfo(respondable.senderID, respondable.senderAdress);

                var diff = Math.Abs(respondable.parentRank - _rank);

                if (respondable.parentRank <= _rank)
                {
                    Debug.LogError("Rank error");
                }

                _rank = respondable.parentRank - 1;

                // accept
                _packetRouter.SendResponse(respondable, response);
            });

            _packetRouter.RegisterPacketHandler(typeof(ParentConnectionResponsePacket), null);

            if (_singleGraphParenting)
            {
                _packetRouter.RegisterPacketHandler(typeof(DisconnectionNotificationPacket), async (onReceived) =>
                {
                    for (int i = 0; i < _children.Count; ++i)
                    {
                        if (_children[i].peerID == onReceived.senderID)
                        {
                            _children.RemoveAt(i);
                            _currentSubgraphNodesCount = await CountSubgraphAsync();
                            return;
                        }
                    }

                    if (_parent != null && _parent.peerID == onReceived.senderID)
                    {
                        _parent = null;

                        SendDisconnectionDowncast();

                        _currentSubgraphNodesCount = 1;
                        _currentSubgraphDepth = 0;
                        _waitParentInitialization = false;

                        return;
                    }

                    Debug.LogError("Disconnection packet received from an unconnected node.");
                });

                _packetRouter.RegisterPacketHandler<DisconnectionDowncastPacket>((onReceived) =>
                {
                    SendDisconnectionDowncast();
                }, true);

            }
            else
            {
                _packetRouter.RegisterPacketHandler(typeof(DisconnectionNotificationPacket), async (onReceived) =>
                {
                    for (int i = 0; i < _children.Count; ++i)
                    {
                        if (_children[i].peerID == onReceived.senderID)
                        {
                            _children.RemoveAt(i);
                            _currentSubgraphNodesCount = await CountSubgraphAsync();
                            return;
                        }
                    }

                    if (_parent != null && _parent.peerID == onReceived.senderID)
                    {
                        _parent = null;
                        _waitParentInitialization = false;
                        _currentSubgraphNodesCount = await CountSubgraphAsync();
                        return;
                    }

                    Debug.LogError("Disconnection packet received from an unconnected node.");
                });
            }

        }

        private void SendDisconnectionDowncast()
        {
            for (int i = 0; i < _children.Count; ++i)
            {
                _packetRouter.Send(_children[i].peerAdress, new DisconnectionDowncastPacket());
                _children.RemoveAt(i);
                i--;
            }

            _inMainGraph = false;
        }

        private void InitializeDefaultParentResearchPackets()
        {
            _broadcaster.RegisterPacketHandlerWithMiddleware(typeof(ParentResearchBroadcastPacket), (onreceived) =>
            {
                var broadcastable = onreceived as ParentResearchBroadcastPacket;

                // if we receive message from nodes that seeks a parent, then we set every parent graph in search mode
                // because it means that other subgraphs aren't connected with this one
                if (_parent == null)
                    _parentSearchActive = true;

                if (!_waitParentInitialization
                    && _checkSortingRuleDelegate(broadcastable.cyclesDistance, broadcastable.maxCycleDistance) // the rule : we search only for close nodes / the rule can be changed with rank comparison (seeking node at/until/above "cycles" range from this)
                                                                                                               //&& _children.Count < Math.Min(_rank * 2, _childrenCount)
                    && _children.Count < _childrenCount
                    && !IsChildren(broadcastable.senderID)
                    && !_pendingRequestToChildren.Contains(broadcastable.broadcasterID)
                        && (_parent == null || broadcastable.broadcasterID != _parent.peerID)) // avoid direct cycles
                {
                    // Case 1 : avalaible children, we send a request
                    if (broadcastable.senderRank < _rank)
                    {
                        var round = _currentRound;

                        _pendingRequestToChildren.Add(broadcastable.broadcasterID);

                        // responding to the potential conneciton by a connection request from the receiver
                        // we set the round of the broadcast sender in the request
                        // if the round has changed on the sender, the request will be discarded 
                        _packetRouter.SendRequest(broadcastable.senderAddress, new ParentConnectionRequestPacket(transform.position, _rank, _children.Count, broadcastable.senderRound), async (response) =>
                        {
                            _pendingRequestToChildren.Remove(broadcastable.broadcasterID);

                            if (response == null)
                                return;

                            if (IsChildren(broadcastable.broadcasterID))
                            {
                                Debug.LogError("Already children");
                                return;
                            }

                            if (_children.Count >= _childrenCount)
                            {
                                Debug.LogError("Too much children");
                                _packetRouter.Send(broadcastable.senderAddress, new DisconnectionNotificationPacket());
                                return;
                            }

                            if (_parent != null && broadcastable.broadcasterID == _parent.peerID)
                            {
                                Debug.LogError("Cycling");
                                _packetRouter.Send(broadcastable.senderAddress, new DisconnectionNotificationPacket());
                                return;
                            }

                            if (round != _currentRound)
                            {
                                Debug.LogError("Round has changed");
                                _packetRouter.Send(broadcastable.senderAddress, new DisconnectionNotificationPacket());
                                return;
                            }

                            _children.Add(new PeerInfo(response.senderID, broadcastable.senderAddress));

                            _packetRouter.Send(broadcastable.senderAddress, new UpdateRankPacket() { newRank = _rank - 1, parentPosition = transform.position, broadcasterID = controller.LocalNodeId, broadcastID = NodeRandom.UniqueID() });

                            _currentSubgraphNodesCount = await CountSubgraphAsync();
                        },
                        _childrenRequestTimeout);
                    }
                    // Case 2 : evaluating a potential children if the subgraph of this parent node would be moved
                    // we seek for the smallest possible move in rank by keeping the closest possible rank on a round
                    // this will be used in the SearchParent function on each round
                    else if (_parent == null
                    && broadcastable.senderRank < _closestUpperBroadcasterRank
                    && _children.Count < _childrenCount) // possible only if child slot avalaible
                    {
                        _closestUpperBroadcasterPeerInfo = new PeerInfo(broadcastable.senderID, broadcastable.senderAddress);
                        _closestUpperBroadcasterRank = broadcastable.senderRank;
                    }
                }

                // in other cases we just relay the message
                var cloned = (ParentResearchBroadcastPacket)broadcastable.ClonePacket(broadcastable);
                cloned.cyclesDistance++;

                _broadcaster.RelayBroadcast(cloned);
            });

            RegisterTreecastPacketHandler<UpdateRankPacket>((updateRankPacket) =>
            {
                var upp = (updateRankPacket as UpdateRankPacket);
                SetRank(upp.newRank);

                if (upp.broadcasterID == _parent.peerID)
                {
                    _waitParentInitialization = false;
                }

                // rank decrease as we descend through children nodes
                upp.newRank--;
                upp.parentPosition = transform.position;
            });
        }

        private void InitializeSingleGraphParentResearchPackets()
        {
            _broadcaster.RegisterPacketHandlerWithMiddleware(typeof(ParentResearchBroadcastPacket), (onreceived) =>
            {
                var broadcastable = onreceived as ParentResearchBroadcastPacket;

                if (!_inMainGraph)
                    return;

                // if we receive message from nodes that seeks a parent, then we set every parent graph in search mode
                // because it means that other subgraphs aren't connected with this one
                if (_parent == null)
                    _parentSearchActive = true;

                if (!_waitParentInitialization
                    && _checkSortingRuleDelegate(broadcastable.cyclesDistance, broadcastable.maxCycleDistance) // the rule : we search only for close nodes / the rule can be changed with rank comparison (seeking node at/until/above "cycles" range from this)
                                                                                                               //&& _children.Count < Math.Min(_rank * 2, _childrenCount)
                    && _children.Count < _childrenCount
                    && !IsChildren(broadcastable.senderID)
                    && !_pendingRequestToChildren.Contains(broadcastable.broadcasterID)
                        && (_parent == null || broadcastable.broadcasterID != _parent.peerID)) // avoid direct cycles
                {
                    // Case 1 : avalaible children, we send a request
                    if (broadcastable.senderRank < _rank)
                    {
                        var round = _currentRound;

                        if (broadcastable.cyclesDistance == 0)
                        {
                            /*for(int i = 0; i < _connectingComponent.Connections.Count; ++i)
                            {

                            }*/
                            var local = transform.position;
                            local.y = 0;

                            var list = _connectingComponent.Connections.Values.ToList();
                            list.Sort((a, b) =>
                            {
                                var p_a = WorldSimulationManager.nodeAddresses[a.peerAdress].transform.position;
                                p_a.y = 0;
                                var p_b = WorldSimulationManager.nodeAddresses[b.peerAdress].transform.position;
                                p_b.y = 0;

                                return Vector3.Distance(p_a, local).CompareTo(Vector3.Distance(p_b, local));
                            });

                            for (int i = 0; i < _children.Count; ++i)
                            {
                                for (int j = 0; j < list.Count; ++j)
                                {
                                    if (list[j].peerID == _children[i].peerID)
                                    {
                                        list.RemoveAt(j);
                                        j--;
                                    }
                                }
                            }

                            if (list.Count > 0)
                                if (broadcastable.broadcasterID != list[0].peerID)
                                {
                                    Debug.LogError("There is better connectin;");
                                    return;
                                }
                                else
                                {
                                    Debug.LogError("Best connection");
                                }
                        }

                        _pendingRequestToChildren.Add(broadcastable.broadcasterID);

                        // responding to the potential conneciton by a connection request from the receiver
                        // we set the round of the broadcast sender in the request
                        // if the round has changed on the sender, the request will be discarded 
                        _packetRouter.SendRequest(broadcastable.senderAddress, new ParentConnectionRequestPacket(transform.position, _rank, _children.Count, broadcastable.senderRound), async (response) =>
                        {
                            _pendingRequestToChildren.Remove(broadcastable.broadcasterID);

                            if (response == null)
                                return;

                            if (IsChildren(broadcastable.broadcasterID))
                            {
                                Debug.LogError("Already children");
                                return;
                            }

                            if (_children.Count >= _childrenCount)
                            {
                                Debug.LogError("Too much children");
                                _packetRouter.Send(broadcastable.senderAddress, new DisconnectionNotificationPacket());
                                return;
                            }

                            if (_parent != null && broadcastable.broadcasterID == _parent.peerID)
                            {
                                Debug.LogError("Cycling");
                                _packetRouter.Send(broadcastable.senderAddress, new DisconnectionNotificationPacket());
                                return;
                            }

                            if (round != _currentRound)
                            {
                                Debug.LogError("Round has changed");
                                _packetRouter.Send(broadcastable.senderAddress, new DisconnectionNotificationPacket());
                                return;
                            }

                            _children.Add(new PeerInfo(response.senderID, broadcastable.senderAddress));

                            Debug.LogError($"{controller.LocalNodeId} Send rank update ({_rank - 1}) to {broadcastable.senderAddress}");
                            _packetRouter.Send(broadcastable.senderAddress, new UpdateRankPacket() { newRank = _rank - 1, parentPosition = transform.position, broadcasterID = controller.LocalNodeId, broadcastID = NodeRandom.UniqueID() });

                            _currentSubgraphNodesCount = await CountSubgraphAsync();
                        },
                        _childrenRequestTimeout);
                    }
                    /*// Case 2 : evaluating a potential children if the subgraph of this parent node would be moved
                    // we seek for the smallest possible move in rank by keeping the closest possible rank on a round
                    // this will be used in the SearchParent function on each round
                    else if (_parent == null
                    && broadcastable.senderRank < _closestUpperBroadcasterRank
                    && _children.Count < _childrenCount) // possible only if child slot avalaible
                    {
                        _closestUpperBroadcasterPeerInfo = new PeerInfo(broadcastable.senderID, broadcastable.senderAddress);
                        _closestUpperBroadcasterRank = broadcastable.senderRank;
                    }*/
                }

                // in other cases we just relay the message
                var cloned = (ParentResearchBroadcastPacket)broadcastable.ClonePacket(broadcastable);
                cloned.cyclesDistance++;

                _broadcaster.RelayBroadcast(cloned);
            });

            RegisterTreecastPacketHandler<UpdateRankPacket>((updateRankPacket) =>
            {
                if (_parent == null)
                {
                    Debug.LogError("PROBLEM");
                    return;
                }

                var upp = (updateRankPacket as UpdateRankPacket);

                if (upp.broadcasterID == _parent.peerID)
                {
                    _waitParentInitialization = false;
                }

                SetRank(upp.newRank);

                _inMainGraph = true;

                // rank decrease as we descend through children nodes
                upp.newRank--;
                upp.parentPosition = transform.position;
            });
        }

        private void InitializeNetworkMaintainingPackets()
        {
            _packetRouter.RegisterPacketHandler(typeof(ParentHeartbeatResponsePacket), null);
            _packetRouter.RegisterPacketHandler(typeof(ParentHeartbeatPacket), (packet) =>
            {
                if (packet == null)
                    return;

                bool found = false;

                for (int i = 0; i < _children.Count; ++i)
                {
                    if (packet.senderID == _children[i].peerID)
                    {
                        _children[i].last_updated = DateTime.UtcNow;
                        found = true;
                        break;
                    }
                }
                var respondable = (packet as IRespondable);

                if (found)
                {
                    var response = (ParentHeartbeatResponsePacket)respondable.GetResponsePacket(respondable);
                    _packetRouter.SendResponse(respondable, response);
                }
                else
                {
                    Debug.LogError("Wrong children");
                    _packetRouter.Send(respondable.senderAdress, new DisconnectionNotificationPacket());
                }

            });
        }

        private void InitializeTreecastPackets()
        {
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
        }

        private void InitializeSortingRuleDelegate()
        {
            switch (_sortingRule)
            {
                case GraphSortingRules.ClosestCyclesDistance:
                    _checkSortingRuleDelegate = (dist, maxDist) =>
                    {
                        return dist <= maxDist;
                    };
                    return;
                case GraphSortingRules.UnderOrEqualsRank: _checkSortingRuleDelegate = (dist, maxDist) => dist <= _rank; return;
                case GraphSortingRules.AboveOrEqualsRank: _checkSortingRuleDelegate = (dist, maxDist) => dist >= _rank; return;
            }

            throw new NotImplementedException();
        }

        #endregion

        private void FullReset()
        {
            DisconnectFromParent();
            DisconnectChildren();
            LocalReset();
        }

        public void LocalReset()
        {
            _pendingRequestToChildren = new HashSet<long>(250);
            _relayedTreecastsBuffer = new HashSet<long>(_relayedTreecastBufferSize);
            _children = new List<PeerInfo>(_childrenCount);
            _currentSubgraphNodesCount = 1;
            _currentRound = 0;
            _parent = null;
            _parentSearchActive = true;
            _inMainGraph = controller.IsBoot;
            _awaitIncomingParentRequests = false;
            _pendingValidations.Clear();
            _pendingParentRequests.Clear();

            if (_alternativeMode)
            {
                if (_singleGraphParenting && controller.IsBoot)
                    SetRank(10);
                else
                    SetRank(0);
            }
            else
            {
                if (_singleGraphParenting && controller.IsBoot)
                    SetRank(10);
                else
                    SetRank(NodeRandom.Range(0, 10) >= 8 ? 1 : 0);
            }           
        }

        #endregion

        private void SetRank(int rank)
        {
            _rank = rank;
            transform.position = new Vector3(_initialPosition.x, 0, _initialPosition.z) + Vector3.up * 5 * _rank;

            if (_parent != null && !controller.IsBoot)
            {
                Debug.LogError("received up rank change");
            }
        }

        #region Treecasting / communication in tree

        public void RegisterTreecastPacketHandler<T>(Action<INetworkPacket> packetReceiveHandler) where T : ITreecastablePacket
        {
            _packetRouter.RegisterPacketHandler(typeof(T), (receivedPacket) =>
            {
                if (receivedPacket is ITreecastablePacket treecastablePacket == false)
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

                // the automatic disconnection is done after the message has been relayed
                // it allows to propagate the information about the cycle to every member of the cycled graph one time before every
                // of them resets
                if (!CheckTreecastRelayCycles(treecastablePacket))
                {
                    FullReset();
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

        /// <summary>
        /// Checking for treecast that already has been received. It will mean that there is a cycle in the graph because the graph are unidirectionnal and oriented top-bottom
        /// </summary>
        /// <param name="packet"></param>
        /// <returns></returns>
        private bool CheckTreecastRelayCycles(IBroadcastablePacket packet)
        {
            if (_relayedTreecastsBuffer.Contains(packet.broadcastID))
            {
                Debug.LogError("Detected cycle in graph, disconnect node" + packet.GetType());
                return false;
            }
            else
            {
                if (_relayedTreecastsBuffer.Count >= _relayedTreecastBufferSize)
                {
                    _relayedTreecastsBuffer.Remove(_relayedTreecastsBuffer.ElementAt(0));
                }
                _relayedTreecastsBuffer.Add(packet.broadcastID);
                return true;
            }
        }
        #endregion

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

        private async void SearchingUpdateTask()
        {
            while (true)
            {
                _delayTimer = NodeRandom.Range(_roundDelayTimerRange.x, _roundDelayTimerRange.y)
                    // we multiply the timer by the log on base 2 of the children count. 
                    // it allows bigger graph to evolute slower that smallest one, for fastest convergence
                    * ((float)Math.Log(_currentSubgraphNodesCount, _dynamicDelayFunctionLogarithmicBase));

                // insuring a minimal value (log2(1) = 0)
                _delayTimer = Math.Max(_delayTimer, .5f);

                await Task.Delay((int)(_delayTimer * 1000));

                if (this == null)
                    break;

                if (_parent == null)
                    await SearchParent();

                if (this == null)
                    break;
            }
        }

        private async void MaintainingUpdateTask()
        {
            var delay = _connectionsTimeout / 2;

            while (true)
            {
                await Task.Delay(delay);

                if (this == null)
                    break;

                if (_parent != null)
                    await PingParent();

                CheckChildrenTimedOut();
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

            if (_singleGraphParenting && controller.LocalNodeId == 0)
            {

            }
            else
            {
                var packet = new ParentResearchBroadcastPacket();
                packet.senderAddress = controller.LocalNodeAdress;
                packet.senderRank = _rank;
                packet.senderRound = _currentRound;
                packet.senderPosition = transform.position;
                packet.maxCycleDistance = _currentRound / _roundsBeforeAugmentRange;
                Debug.Log(packet.maxCycleDistance);

                _broadcaster.SendBroadcast(packet);

            }

            // waiting a timeout of the request
            await Task.Delay(_roundTimeout);

            // prevent after timeout handling of messages
            _currentRound++;

            // if not finding any connection before the timeout
            // ranking the node up
            // remind that nodes can only connect with node from the same group, and the rank is equal to the fanout distance of the broadcast (which should indicate a distance from the sender in a well constructed gossip network)
            if (_parent == null)
            {
                HandleDefaultParentSubgraphRankUpdate();
            }
        }

        private void HandleDefaultParentSubgraphRankUpdate()
        {
            // every broadcast received by a parent that is over its rank will be comparated to keep the best possible current parent peer
            if (_closestUpperBroadcasterPeerInfo != null && _children.Count < _childrenCount)
            {
                // case of a parent that is under another potential children node (aka a node that seeks parent)
                // the local parent goes up the potential children 
                // it might create a connection on next round
                SendUpdateRankTreecast(new UpdateRankPacket() { newRank = _closestUpperBroadcasterRank + 1, parentPosition = transform.position });

                _closestUpperBroadcasterPeerInfo = null;
                _closestUpperBroadcasterRank = int.MaxValue;
            }
            else
            {
                // move random up or down all te subgraph to be avalaible to other children
                if (NodeRandom.Range(0, 100) > 50)
                {
                    SetRank(_rank + NodeRandom.Range(1, _wide));

                    //Debug.Log($"Ranking {controller.LocalNodeId} up to rank {_rank}");
                }
                else
                {
                    SetRank(_rank - NodeRandom.Range(1, _wide));

                    //Debug.Log($"Ranking {controller.LocalNodeId} down to rank {_rank}");
                }

                // tree cast to subgraph to modify the ranks

                SendUpdateRankTreecast(new UpdateRankPacket() { newRank = _rank - 1, parentPosition = transform.position });
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
            _inMainGraph = false;
        }

        private void DisconnectChildren()
        {
            for (int i = 0; i < _children.Count; ++i)
            {
                _packetRouter.Send(_children[i].peerAdress, new DisconnectionNotificationPacket());
            }

            _children.Clear();
        }

        private void CheckChildrenTimedOut()
        {
            // updates children connections
            // parent should receive heartbeat from children on a regular basis
            for (int i = 0; i < _children.Count; ++i)
            {
                if ((DateTime.Now - _children[i].last_updated).Seconds > _connectionsTimeout / 1000)
                {
                    Debug.Log("Children timed out");

                    _packetRouter.Send(_children[i].peerAdress, new DisconnectionNotificationPacket());
                    _children.RemoveAt(i);
                    i--;
                }
            }
        }

        #region Subgraph sampling

        private async Task HandleSubgraphCountingAsync(SubraphCountingRequestPacket subraphCountingRequestPacket)
        {
            if (_children.Count == 0)
            {
                _packetRouter.SendResponse(subraphCountingRequestPacket, new SubgraphCountingResponsePacket() { childrenCount = 1, maxDepth = subraphCountingRequestPacket.depth + 1 });
            }
            else
            {
                var crtDepth = subraphCountingRequestPacket.depth + 1;

                var tasks = new Task<SubgraphCountingResponsePacket>[_children.Count];
                for (int i = 0; i < tasks.Length; ++i)
                {
                    tasks[i] = _packetRouter.SendRequestAsync<SubgraphCountingResponsePacket>(_children[i].peerAdress, new SubraphCountingRequestPacket() { depth = crtDepth });
                }
                int overallMaxDepth = int.MinValue;

                var results = await Task.WhenAll(tasks);
                int count = 0;
                for (int i = 0; i < results.Length; ++i)
                {
                    if (results[i] != null)
                    {
                        count += results[i].childrenCount;
                        overallMaxDepth = Math.Max(overallMaxDepth, results[i].maxDepth);
                    }
                }

                _packetRouter.SendResponse(subraphCountingRequestPacket, new SubgraphCountingResponsePacket() { childrenCount = count + 1, maxDepth = overallMaxDepth });
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
            int overallMaxDepth = int.MinValue;
            for (int i = 0; i < results.Length; ++i)
            {

                if (results[i] != null)
                {
                    overallMaxDepth = Math.Max(overallMaxDepth, results[i].maxDepth);
                    count += results[i].childrenCount;
                }
            }

            // counting self as well
            count += 1;

            Debug.Log($"Depth = {overallMaxDepth}, Total counted nodes = {count}");

            _currentSubgraphNodesCount = count;
            _currentSubgraphDepth = overallMaxDepth;

            return count;
        }

        [Button]
        private async void CountSubgraph()
        {
            await CountSubgraphAsync();
        }

        #endregion

        #region Debug functions
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

        #endregion
    }
}
