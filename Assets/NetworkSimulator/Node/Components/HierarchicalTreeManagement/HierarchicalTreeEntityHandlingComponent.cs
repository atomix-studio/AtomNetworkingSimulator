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

        /// Send "NO" response to node that wont become local node parent / or leave to timeout on parent
        [SerializeField] private bool _sendRefuseResponse = true;

        [SerializeField, MinMaxSlider(.05f, 2f)] private Vector2 _roundDelayTimerRange = new Vector2();

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

        private float _delayTimer = 0f;
        private Vector3 _initialPosition;
        private HashSet<long> _relayedTreecastsBuffer;

        public int currentRank => _rank;
        public List<PeerInfo> children => _children;

        #region Packet Initialization 

        public async void OnInitialize()
        {
            LocalReset();

            _packetRouter.RegisterPacketHandler(typeof(ParentConnectionResponsePacket), null);
            _packetRouter.RegisterPacketHandler(typeof(ParentConnectionRequestPacket), async (packet) =>
            {
                Debug.Log("Received parent connection request");

                var respondable = (packet as ParentConnectionRequestPacket);


                if (IsChildren(respondable.senderID))
                    return;

                if (_children.Count > 0 || _parent != null)
                {
                    // Impossible to be parented with already children = CYCLE   // if parent already found
                    return;
                }
                else
                {
                    var response = (ParentConnectionResponsePacket)respondable.GetResponsePacket(respondable);
                    _pendingParentRequests.Add(new PendingParentRequest() { ParentConnectionRequestPacket = respondable, ParentConnectionResponsePacket = response });

                    if (!_awaitIncomingParentRequests)
                    {
                        _awaitIncomingParentRequests = true;

                        await WaitBeforeFilterParentRequests();
                    }
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

            Debug.Log($"{controller.LocalNodeId} sorting {_pendingParentRequests.Count} parenting requests");

            _pendingParentRequests.Sort((a, b) =>
            {
                var p_a = WorldSimulationManager.nodeAddresses[a.ParentConnectionRequestPacket.senderAdress].transform.position;
                p_a.y = 0;
                var p_b = WorldSimulationManager.nodeAddresses[b.ParentConnectionRequestPacket.senderAdress].transform.position;
                p_b.y = 0;

                return Vector3.Distance(p_a, local).CompareTo(Vector3.Distance(p_b, local));
            });

            Debug.Log($"{controller.LocalNodeId} accepting parent request from {_pendingParentRequests[0].ParentConnectionRequestPacket.senderID}");

            _parent = new PeerInfo(_pendingParentRequests[0].ParentConnectionRequestPacket.senderID, _pendingParentRequests[0].ParentConnectionRequestPacket.senderAdress);

            _pendingParentRequests[0].ParentConnectionResponsePacket.response = true;
            _packetRouter.SendResponse(_pendingParentRequests[0].ParentConnectionRequestPacket, _pendingParentRequests[0].ParentConnectionResponsePacket);

            if (_sendRefuseResponse)
            {
                for (int i = 1; i < _pendingParentRequests.Count; i++)
                {
                    _pendingParentRequests[i].ParentConnectionResponsePacket.response = false;
                    _packetRouter.SendResponse(_pendingParentRequests[i].ParentConnectionRequestPacket, _pendingParentRequests[i].ParentConnectionResponsePacket);
                }
            }

            _awaitIncomingParentRequests = false;
        }

        private async Task SearchPotentialChildren()
        {
            _delayTimer = _roundDelayTimerRange.x;
            int round_index = 0;
            while (true)
            {
                Debug.Log($"Node {controller.LocalNodeId} > start children round search {round_index}");

                await Task.Delay((int)(_delayTimer * 1000));

                var local = transform.position;
                local.y = 0;

                var list = _connectingComponent.Connections.Values.ToList();

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

                Debug.Log($"Node {controller.LocalNodeId} > send {list.Count} request for children");

                Task<ParentConnectionResponsePacket>[] tasks = new Task<ParentConnectionResponsePacket>[list.Count];

                for (int i = 0; i < list.Count; ++i)
                {
                    tasks[i] = _packetRouter.SendRequestAsync<ParentConnectionResponsePacket>(list[i].peerAdress, new ParentConnectionRequestPacket(transform.position, _rank, _children.Count, 0), _childrenRequestTimeout);
                }

                var results = await Task.WhenAll(tasks);

                Debug.Log($"Node {controller.LocalNodeId} > children request round end");

                bool has_response = false;
                for (int i = 0; i < results.Length; ++i)
                {
                    if (results[i] == null)
                    {
                        // timeout                       
                      
                    }
                    else
                    {
                        if (_sendRefuseResponse && results[i].response == false)
                        {                           
                            continue;
                        }

                        has_response = true;

                        if (IsChildren(results[i].senderID))
                        {
                            Debug.LogError("Already children");
                            return;
                        }

                        // children has accepted this node as parent so we just add to connection
                        _children.Add(new PeerInfo(list[i].peerID, list[i].peerAdress));

                        // updating children rank/depth
                        _packetRouter.Send(list[i].peerAdress, new UpdateRankPacket() { newRank = _rank - 1, parentPosition = transform.position, broadcasterID = controller.LocalNodeId, broadcastID = NodeRandom.UniqueID() });

                        Debug.Log($"{controller.LocalNodeId} connect with {list[i].peerID}");

                        //_currentSubgraphNodesCount = await CountSubgraphAsync();
                    }
                }

                if (!has_response || list.Count == 0) //  || _children.Count >= _childrenCount ||
                {
                    if (_parent == null)
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

                round_index++;
            }

            Debug.Log($"{controller.LocalNodeId} end searching children : {round_index}");
        }

        #endregion

        #region Legacy

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
                var upp = (setColorPacket as SetColorDowncastPacket);
                controller.material.color = upp.newColor;
            });

            RegisterTreecastPacketHandler<SetColorUpcastPacket>((setColorPacket) =>
            {
                var upp = (setColorPacket as SetColorUpcastPacket);
                controller.material.color = upp.newColor;

                // when achieve parent, send down tree cast to all graph
                if (_parent == null)
                {
                    SendTreecast(new SetColorDowncastPacket() { newColor = upp.newColor });
                }
            });

            RegisterTreecastPacketHandler<SetColorFullcastPacket>((setColorPacket) =>
            {
                var upp = (setColorPacket as SetColorFullcastPacket);
                controller.material.color = upp.newColor;
            });

            RegisterTreecastPacketHandler<DisconnectionDowncastPacket>((onReceived) =>
            {
                SendDisconnectionDowncast();
            });

            _broadcaster.RegisterPacketHandlerWithMiddleware(typeof(BroadcastColorPacket), (received) =>
            {
                var upp = (received as BroadcastColorPacket);
                controller.material.color = upp.newColor;
            },
            true);

            _packetRouter.RegisterPacketHandler(typeof(SubgraphCountingResponsePacket), null);
            _packetRouter.RegisterPacketHandler(typeof(SubraphCountingRequestPacket), async (onReceived) =>
            {
                await HandleSubgraphCountingAsync((SubraphCountingRequestPacket)onReceived);
            });

            _packetRouter.RegisterPacketHandler(typeof(DisconnectionNotificationPacket), async (onReceived) =>
            {
                for (int i = 0; i < _children.Count; ++i)
                {
                    if (_children[i].peerID == onReceived.senderID)
                    {
                        _children.RemoveAt(i);
                        return;
                    }
                }

                if (_parent != null && _parent.peerID == onReceived.senderID)
                {
                    _parent = null;

                    SendDisconnectionDowncast();

                    _currentSubgraphNodesCount = 1;
                    _currentSubgraphDepth = 0;

                    return;
                }

                Debug.LogError("Disconnection packet received from an unconnected node.");
            });

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
            _relayedTreecastsBuffer = new HashSet<long>(_relayedTreecastBufferSize);
            _children = new List<PeerInfo>(controller.NetworkViewsTargetCount);
            _currentSubgraphNodesCount = 1;
            _currentRound = 0;
            _parent = null;
            _parentSearchActive = true;
            _inMainGraph = controller.IsBoot;
            _awaitIncomingParentRequests = false;
            _pendingValidations.Clear();
            _pendingParentRequests.Clear();

            if (controller.IsBoot)
                SetRank(1);
            else
                SetRank(0);
        }

        #endregion

        private void SetRank(int rank)
        {
            _rank = rank;
            transform.position = new Vector3(_initialPosition.x, 0, _initialPosition.z) + Vector3.up * 5 * _rank;

            if (_parent != null && !controller.IsBoot)
            {
                Debug.Log("received up rank change");
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
                else if (receivedPacket is IDowncastablePacket downcastablePacket)
                {
                    for (int i = 0; i < _children.Count; i++)
                    {
                        var relayedPacket = downcastablePacket.ClonePacket(downcastablePacket);
                        _packetRouter.Send(_children[i].peerAdress, relayedPacket);
                    }
                }
                else if (receivedPacket is IFullcastablePacket fullcastablePacket)
                {
                    if (_parent != null && fullcastablePacket.allowUpcasting)
                    {
                        var relayedPacket = (IFullcastablePacket)fullcastablePacket.ClonePacket(fullcastablePacket);
                        relayedPacket.allowUpcasting = true;
                        _packetRouter.Send(_parent.peerAdress, relayedPacket);
                    }

                    for (int i = 0; i < _children.Count; i++)
                    {
                        if (_children[i].peerID == fullcastablePacket.senderID)
                            continue;

                        var relayedPacket = fullcastablePacket.ClonePacket(fullcastablePacket);
                        (receivedPacket as IFullcastablePacket).allowUpcasting = false;
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
            else if (treecastablePacket is IFullcastablePacket fullcastablePacket)
            {
                fullcastablePacket.allowUpcasting = true;
                _packetRouter.Send(_parent.peerAdress, treecastablePacket);

                INetworkPacket current = treecastablePacket.ClonePacket(treecastablePacket);

                for (int i = 0; i < _children.Count; ++i)
                {
                    (current as IFullcastablePacket).allowUpcasting = false;
                    _packetRouter.Send(_children[i].peerAdress, current);
                    current = treecastablePacket.ClonePacket(current);
                }
            }
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
        public void SetColorUpcast()
        {
            SendTreecast(new SetColorUpcastPacket() { newColor = new Color(NodeRandom.Range(0f, 1f), NodeRandom.Range(0f, 1f), NodeRandom.Range(0f, 1f), 1) });
        }

        [Button]
        public void SetColorDowncast()
        {
            SendTreecast(new SetColorDowncastPacket() { newColor = new Color(NodeRandom.Range(0f, 1f), NodeRandom.Range(0f, 1f), NodeRandom.Range(0f, 1f), 1) });
        }

        [Button]
        public void SetColorFullcast()
        {
            SendTreecast(new SetColorFullcastPacket() { newColor = new Color(NodeRandom.Range(0f, 1f), NodeRandom.Range(0f, 1f), NodeRandom.Range(0f, 1f), 1) });
        }

        [Button]
        public void SetColorBroadcast()
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
