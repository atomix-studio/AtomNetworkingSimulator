using Atom.Broadcasting;
using Atom.CommunicationSystem;
using Atom.Components.RpcSystem;
using Atom.DependencyProvider;
using Atom.Helpers;
using Sirenix.OdinInspector;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using UnityEditor.Sprites;
using UnityEngine;

namespace Atom.Components.GraphNetwork
{
    public enum NodeGraphState
    {
        Follower,
        LeaderPropesct,
        Eliminated,
        Leader,
    }

    public class GraphEntityComponent : MonoBehaviour, INodeComponent
    {
        [Inject] private BroadcasterComponent _broadcaster;
        [Inject] private PacketRouter _packetRouter;
        [Inject] private NetworkConnectionsComponent _networkHandling;
        [Inject] private GraphcasterComponent _graphcaster;

        public NodeEntity controller { get; set; }

        [Header("GraphEntityComponent")]
        // local node will consider leader as 'dead' after N seconds
        [SerializeField] private float _leaderLeaseDuration = 12;
        // leader trigger a heartbeat every N second
        [SerializeField] private float _leaderUpdateSpan = 6;
        // time before the node contacts the moe to refresh the data, or recomputes the moe
        [SerializeField] private float _minimumOutgoingEdgeExpirationDelay = 1;

        [SerializeField] private float _takeLeadDelay = 1.5f;
        [Space]

        [Header("Generation variables debub")]
        [ShowInInspector, ReadOnly] private NodeGraphState _nodeGraphState;
        [ReadOnly, ShowInInspector] private long _currentPendingLeadId = -1;
        [ShowInInspector, ReadOnly] private long _outgoingJoiningRequestPeerId = -1;
        [ShowInInspector, ReadOnly] private List<FragmentJoiningRequestPacket> _bufferedMergingRequestBuffer = new List<FragmentJoiningRequestPacket>();
        [SerializeField] private List<GraphEdge> _graphEdges = new List<GraphEdge>();

        private float _leaderUpdateTimer = 0;
        private bool _isSearchingMOE = false;
        private DateTime _leaderExpirationTime;
        private bool _isGraphRunning = false;
        private bool _isGraphCreationCompleted = false;
        private DateTime _lastTakeLeadRequestReceived;

        public List<GraphEdge> graphEdges => _graphEdges;
        public bool isGraphRunning => _isGraphRunning;
        public bool isGraphCreationCompleted => _isGraphCreationCompleted;

        public long LocalFragmentId => FragmentID;
        public int LocalFragmentLevel => FragmentLevel;

        [Header("Fragment Generation Data")]
        public long FragmentID = -1; // PEER ID OF THE FRAGMENT LEAD
        public int FragmentLevel = 0;

        public List<PeerInfo> FragmentMembers = new List<PeerInfo>();

        public MinimumOutgoingEdge MinimumOutgoingEdge = null;

        [ShowInInspector, ReadOnly] private long _moeId = -1;
        [ShowInInspector, ReadOnly] private long _moeFragmentId = -1;


        public void OnInitialize()
        {
            _broadcaster.RegisterPacketHandlerWithMiddleware(typeof(SpanningTreeCreationBroadcastPacket), (packet) => { OnSpanningTreeCreationBroadcastPacketReceived((SpanningTreeCreationBroadcastPacket)packet); }, true);

            _packetRouter.RegisterPacketHandler(typeof(FragmentJoiningRequestPacket), (packet) => { OnJoiningFragmentRequestReceived((FragmentJoiningRequestPacket)packet); });
            _packetRouter.RegisterPacketHandler(typeof(FragmentJoiningRequestValidated), (packet) => { OnFragmentJoiningRequestResponseReceived((FragmentJoiningRequestValidated)packet); });
            _packetRouter.RegisterPacketHandler(typeof(GetNodeFragmentInfoRequestPacket), (packet) => { OnGetNodeFragmentInfoPacketReceived((GetNodeFragmentInfoRequestPacket)packet); });
            _packetRouter.RegisterPacketHandler(typeof(GetNodeFragmentInfoResponsePacket), null);

            _graphcaster.RegisterGraphcast(typeof(FragmentUpdatingBroadcastPacket), (packet) => { OnFragmentUpdatingPacketReceived((FragmentUpdatingBroadcastPacket)packet); }, true);
            _graphcaster.RegisterGraphcast(typeof(FragmentLeaderSelectingPacket), (packet) => { OnFragmentLeaderSelectingPacketReceived((FragmentLeaderSelectingPacket)packet); }, false);
            _graphcaster.RegisterGraphcast(typeof(FragmentJoiningRequestRelayedGraphcastPacket), (packet) => OnFragmentJoiningRelayedGraphcastPacketReceived((FragmentJoiningRequestRelayedGraphcastPacket)packet), false);
            _graphcaster.RegisterGraphcast(typeof(LeaderHeartbeatPacket), (packet) => OnLeaderHeartbeatPacketReceived((LeaderHeartbeatPacket)packet), true);
            _graphcaster.RegisterGraphcast(typeof(TakeLeadRequestPacket), (packet) => OnTakeLeadRequestPacketReceived((TakeLeadRequestPacket)packet), false);
            _graphcaster.RegisterGraphcast(typeof(MinimumOutgoingEdgePacket), (packet) => OnMinimumOutgoingEdgePacketReceived((MinimumOutgoingEdgePacket)packet), true);

        }

        [Button]
        public void StartSpanningTreeCreationWithOneCast()
        {
            _broadcaster.SendMulticast(new SpanningTreeCreationBroadcastPacket(), 1);
        }

        [Button]
        public void StartSpanningTreeCreationWithBroadcast()
        {
            _broadcaster.SendBroadcast(new SpanningTreeCreationBroadcastPacket());
            OnSpanningTreeCreationBroadcastPacketReceived(new SpanningTreeCreationBroadcastPacket());
        }

        private void OnSpanningTreeCreationBroadcastPacketReceived(SpanningTreeCreationBroadcastPacket packet)
        {
            ResetGraphEdges();
            _nodeGraphState = NodeGraphState.Leader;
            _leaderExpirationTime = DateTime.Now.AddSeconds(_leaderLeaseDuration);

            StartCoroutine(_waitAndExecuteLeaderRoutine());
        }

        private IEnumerator _waitAndExecuteLeaderRoutine()
        {
            yield return new WaitForSeconds(NodeRandom.Range(1.75f, 2.25f));
            // every node create a local fragment where she is the leader and the single member

            // at the first level, finding the Minimum outgoing edge is pretty straght-forward as we only need to iterate over connections and find the highest score (which in AtomNetworking context represented the minimum outgoing edge)
            //StartCoroutine(MoeSearching());
            _isGraphRunning = true;
            SearchMinimumOutgoingEdgeAndRelayLead();
        }

        private IEnumerator _waitAndExecuteLeaderFastRoutine()
        {
            yield return new WaitForSeconds(.15f);
            // every node create a local fragment where she is the leader and the single member

            // at the first level, finding the Minimum outgoing edge is pretty straght-forward as we only need to iterate over connections and find the highest score (which in AtomNetworking context represented the minimum outgoing edge)
            //StartCoroutine(MoeSearching());
            _isGraphRunning = true;
            SearchMinimumOutgoingEdgeAndRelayLead();
        }

        #region Fragment JOIN
        private void OnJoiningFragmentRequestReceived(FragmentJoiningRequestPacket joiningRequestPacket)
        {

            if (joiningRequestPacket.joinerFragmentId == LocalFragmentId)
                return;

            for (int i = 0; i < _graphEdges.Count; ++i)
            {
                if (_graphEdges[i]?.EdgeId == joiningRequestPacket?.senderID)
                {
                    Debug.Log($"A connection already exists with the JOINER edge {joiningRequestPacket.senderID}/{joiningRequestPacket.joinerFragmentId}. Refreshing connection to the requester {controller.LocalNodeId}/{LocalFragmentId}");

                    var response = new FragmentJoiningRequestValidated(controller.LocalNodeId, controller.LocalNodeAdress, FragmentID, FragmentLevel, GraphOperations.RefreshExistingEdge);
                    _packetRouter.Send(joiningRequestPacket.senderAdress, response);

                    return;
                }
            }

            if (FragmentID != _networkHandling.LocalPeerInfo.peerID)
            {
                if (joiningRequestPacket.joinerfragmentLevel < FragmentLevel)
                {
                    RemoteAbsorbFragment(joiningRequestPacket.senderID, joiningRequestPacket.senderAdress);
                }
                else /*if (joiningRequestPacket.joinerFragmentId == MinimumOutgoingEdge.OuterFragmentId
                    || joiningRequestPacket.senderID == MinimumOutgoingEdge.OuterFragmentNode.peerID)*/
                {
                    Debug.Log($"Request received from {joiningRequestPacket.senderAdress} but local node is not a leader {_networkHandling.LocalPeerInfo.peerAdress}");

                    if (FragmentMembers.Count > 0)
                        _graphcaster.SendGraphcast(new FragmentJoiningRequestRelayedGraphcastPacket(controller.LocalNodeAdress, joiningRequestPacket.senderID, joiningRequestPacket.senderAdress, joiningRequestPacket.joinerfragmentLevel, joiningRequestPacket.joinerFragmentId));
                    else
                        Debug.Log("No fragment to relay the message. This node should be leader.");
                }
                // if the requester is the moe (outer), he will 
                return;
            }

            // absorbtion if the requester is lower level
            if (joiningRequestPacket.joinerfragmentLevel < FragmentLevel)
            {
                AbsorbFragment(joiningRequestPacket.senderID, joiningRequestPacket.senderAdress);
            }
            else
            {
                if (MinimumOutgoingEdge == null)
                {
                    if (!_isSearchingMOE)
                        SearchMinimumOutgoingEdgeAndRelayLead();

                    return;
                }

                /*if (joiningRequestPacket.senderID > controller.LocalNodeId)
                    MergeWithFragment(joiningRequestPacket);
                else
                    SendFragmentJoiningRequest(new PeerInfo(joiningRequestPacket.senderID, joiningRequestPacket.senderAdress));*/

                if (joiningRequestPacket.joinerFragmentId == MinimumOutgoingEdge.OuterFragmentId
                    || joiningRequestPacket.senderID == MinimumOutgoingEdge.OuterFragmentNode.peerID)
                {
                    MergeWithFragment(joiningRequestPacket);
                }
                else
                {
                    if (MinimumOutgoingEdge.hasExpired)
                    {
                        //TryRefreshOutgoingEdge();
                    }
                }
            }
        }

        [Button]
        // send a request to the local moe to see if the 'situation' as somehow changed.
        // the moe could be outdated and depending on this, the leader will either try to find a new moe OR to send a new join request
        private async Task<bool> TryRefreshOutgoingEdge()
        {
            // avoiding cycling call while refreshing is running
            MinimumOutgoingEdge.Refresh();

            TaskCompletionSource<bool> result = new TaskCompletionSource<bool>(TaskContinuationOptions.RunContinuationsAsynchronously);

            // if the outerframgent id dosn't correspond, the node checks if the current MOE is still alive and if the current MOE has still the same fragmentId, that could have changed over time
            SendGetNodeFragmentInfoPacket(MinimumOutgoingEdge.OuterFragmentNode.peerAdress, (response) =>
            {
                if (response == null)
                {
                    // outgoing edge not responding, searching new outgoing edge
                    SearchMinimumOutgoingEdgeAndRelayLead();
                    result.TrySetResult(false);
                    return;
                }

                var resp = (GetNodeFragmentInfoResponsePacket)response;
                if (MinimumOutgoingEdge == null || resp.FragmentID != MinimumOutgoingEdge.OuterFragmentId || resp.FragmentLevel != MinimumOutgoingEdge.OuterFragmentLevel)
                {
                    SearchMinimumOutgoingEdgeAndRelayLead();
                    result.TrySetResult(false);
                    return;
                }

                // can keep requesting 
                result.TrySetResult(true);
            });

            return await result.Task;
        }

        /*private void AbsorbFragment(FragmentJoiningRequestPacket joiningRequestPacket)
        {
            Debug.Log(_networkHandling.LocalPeerInfo.peerAdress + "absorbing" + joiningRequestPacket.senderAdress);

            var response = new FragmentJoiningRequestValidated(controller.LocalNodeId, controller.LocalNodeAdress, FragmentID, FragmentLevel, GraphOperations.Absorbed);

            _createGraphEdge(joiningRequestPacket.senderID, joiningRequestPacket.senderAdress);
            _packetRouter.Send(joiningRequestPacket.senderAdress, response);

            // graph cast absorbed + local fragment to set absorbed nodes fragment level adn id
            _graphcaster.SendGraphcast(new FragmentUpdatingBroadcastPacket(joiningRequestPacket.senderID, FragmentLevel));

            MinimumOutgoingEdge = null;

            StartCoroutine(_waitAndExecuteLeaderRoutine());
        }*/

        private void AbsorbFragment(long joinerId, string joinerAdress)
        {
            Debug.Log(_networkHandling.LocalPeerInfo.peerAdress + "absorbing" + joinerAdress);

            var response = new FragmentJoiningRequestValidated(controller.LocalNodeId, controller.LocalNodeAdress, FragmentID, FragmentLevel, GraphOperations.Absorbed);

            _createGraphEdge(joinerId, joinerAdress);
            _packetRouter.Send(joinerAdress, response);

            // graph cast absorbed + local fragment to set absorbed nodes fragment level adn id
            //_graphcaster.SendGraphcast(new FragmentUpdatingBroadcastPacket(joinerId, FragmentLevel));
            _graphcaster.SendGraphcast(new FragmentUpdatingBroadcastPacket(FragmentID, FragmentLevel));

            MinimumOutgoingEdge = null;

            StartCoroutine(_waitAndExecuteLeaderFastRoutine());
        }

        public void RemoteAbsorbFragment(long joinerId, string joinerAdress)
        {
            Debug.Log(_networkHandling.LocalPeerInfo.peerAdress + "absorbing" + joinerAdress);

            var response = new FragmentJoiningRequestValidated(controller.LocalNodeId, controller.LocalNodeAdress, FragmentID, FragmentLevel, GraphOperations.Absorbed);

            _createGraphEdge(joinerId, joinerAdress);
            _packetRouter.Send(joinerAdress, response);

            // graph cast absorbed + local fragment to set absorbed nodes fragment level adn id
            //_graphcaster.SendGraphcast(new FragmentUpdatingBroadcastPacket(joinerId, FragmentLevel));
            _graphcaster.SendGraphcast(new FragmentUpdatingBroadcastPacket(FragmentID, FragmentLevel));
            MinimumOutgoingEdge = null;
        }


        private void MergeWithFragment(FragmentJoiningRequestPacket joiningRequestPacket)
        {
            Debug.LogError(_networkHandling.LocalPeerInfo.peerAdress + " merging" + joiningRequestPacket.senderAdress);
            MinimumOutgoingEdge = null;

            // create a new fragment level + 1 with all members from the two merging fragments
            FragmentLevel++;

            var response = new FragmentJoiningRequestValidated(controller.LocalNodeId, controller.LocalNodeAdress);
            _createGraphEdge(joiningRequestPacket.senderID, joiningRequestPacket.senderAdress);

            // the node then reply to the requester that the merge happens
            // the requester wil update itself and graphcast to its local network before creating the connection
            _packetRouter.Send(joiningRequestPacket.senderAdress, response);

            // in a merge between fragments, the node with the highest id between the two moe speaking becomes the new leader
            // if messages are crossing, the nodes will fall in the same state separately
            if (controller.LocalNodeId > joiningRequestPacket.senderID)
            {
                Debug.Log($"Merging leader swap from {FragmentID} to {controller.LocalNodeId} ");
                //_fragmentData.OldFragmentIDs.Add(_fragmentData.FragmentID);
                // local node stay leader
                FragmentID = controller.LocalNodeId;

                response.FragmentId = controller.LocalNodeId;
                response.FragmentLevel = FragmentLevel;
                response.graphOperation = GraphOperations.Merged;

                // local leader changing, this is graphCasted in the local fragment before the fragment is actually merged
                _graphcaster.SendGraphcast(new FragmentUpdatingBroadcastPacket(controller.LocalNodeId, FragmentLevel));
            }
            else
            {
                Debug.Log($"Merging leader swap from {FragmentID} to {joiningRequestPacket.senderID} ");
                //_fragmentData.OldFragmentIDs.Add(_fragmentData.FragmentID);

                // lead given to the joiner (highest ID)
                // the new lead will have to recompute the MOE that will with a high probability give the lead to another node 
                FragmentID = joiningRequestPacket.senderID;
                _nodeGraphState = NodeGraphState.Follower;

                response.FragmentId = joiningRequestPacket.senderID;
                response.FragmentLevel = FragmentLevel;
                response.graphOperation = GraphOperations.Merged;

                // local leader changing, this is graphCasted in the local fragment before the fragment is actually merged
                _graphcaster.SendGraphcast(new FragmentUpdatingBroadcastPacket(joiningRequestPacket.senderID, FragmentLevel));
            }

            StartCoroutine(_waitAndExecuteLeaderFastRoutine());
        }

        // if a node receive a JOIN but its node lead/moe, it will follows it to the leader via cast
        private void OnFragmentJoiningRelayedGraphcastPacketReceived(FragmentJoiningRequestRelayedGraphcastPacket rel)
        {
            if (FragmentID == controller.LocalNodeId)
            {
                if (MinimumOutgoingEdge == null)
                {
                    Debug.LogError($"{rel.originAdress} requested a join over the fragment {LocalFragmentId} that has been relayed by {rel.broadcasterID}. No minimum outgoing edge here so recomputing.");

                    SearchMinimumOutgoingEdgeAndRelayLead();
                    return;
                }

                if (_outgoingJoiningRequestPeerId != -1)
                    return;

                if (rel.joinerfragmentLevel < FragmentLevel)
                {
                    WorldSimulationManager.nodeAddresses[rel.broadcasterAdress].graphEntityComponent.RemoteAbsorbFragment(rel.originId, rel.originAdress);
                    return;
                }

                if (rel.originId == MinimumOutgoingEdge.OuterFragmentNode.peerID
                   || rel.joinerFragmentId == MinimumOutgoingEdge.OuterFragmentId)
                /* if (rel.joinerfragmentLevel < FragmentLevel
                 || rel.originId == MinimumOutgoingEdge.OuterFragmentNode.peerID
                 || rel.joinerFragmentId == MinimumOutgoingEdge.OuterFragmentId)*/
                {
                    //Debug.LogError($"{rel.originAdress} requested a join over the fragment {LocalFragmentId}. {rel.broadcasterID} should become MOE/Leader for this connection to happen.");
                    //if(rel.broadcasterID > rel.originId)
                    {
                        // the broadcaster is the node within the fragment that received the JOIN message
                        // it happened that the JOINER is actually the best outgoing fragment
                        // only the node that is the MOE at a time can be leader of the fragment
                        // so the current leader will pass the relay to the broadcaster, who will handle the joining logic
                        Debug.LogError($"Leader switching from {LocalFragmentId} to {rel.broadcasterID}");

                        _nodeGraphState = NodeGraphState.Follower;
                        var oldfragmentId = FragmentID;
                        FragmentID = rel.broadcasterID;
                        _graphcaster.SendGraphcast(new FragmentLeaderSelectingPacket(oldfragmentId, rel.broadcasterID, rel.originId));
                    }
                }
                return;
            }

            _graphcaster.RelayGraphcast(rel);
        }

        #endregion

        #region Leader / follower routine

        void Update()
        {
            if (!_isGraphRunning)
                return;

            // debug only
            if (MinimumOutgoingEdge == null)
            {
                _moeId = -1;
                _moeFragmentId = -1;
            }
            else
            {
                _moeId = MinimumOutgoingEdge.OuterFragmentNode.peerID;
                _moeFragmentId = MinimumOutgoingEdge.OuterFragmentId;
            }

            switch (_nodeGraphState)
            {
                case NodeGraphState.Follower:
                    _followerUpdate();
                    break;
                case NodeGraphState.Leader:
                    _leaderUpdate();
                    break;
                case NodeGraphState.LeaderPropesct:
                    if (DateTime.Now > _lastTakeLeadRequestReceived.AddSeconds(_takeLeadDelay))
                    {
                        Debug.LogError($"{controller.LocalNodeAdress} is the new leader of fragment {FragmentID}");
                        _leaderUpdateTimer = 0;
                        FragmentID = controller.LocalNodeId;
                        _nodeGraphState = NodeGraphState.Leader;
                        // replace here by dedicated take lead packet
                        _graphcaster.SendGraphcast(new FragmentUpdatingBroadcastPacket(controller.LocalNodeId, FragmentLevel));
                        SearchMinimumOutgoingEdgeAndRelayLead();
                    }
                    break;
            }
        }

        private void _leaderUpdate()
        {
            if (!_isGraphCreationCompleted)
            {
                if (MinimumOutgoingEdge == null)
                {
                    if (!_isSearchingMOE)
                        SearchMinimumOutgoingEdgeAndRelayLead();
                }
                else if (MinimumOutgoingEdge.hasExpired)
                {
                    if (!_isSearchingMOE)
                        SearchMinimumOutgoingEdgeAndRelayLead();

                    //TryRefreshOutgoingEdge();
                }
            }

            _leaderUpdateTimer += Time.deltaTime;
            if (_leaderUpdateTimer > _leaderUpdateSpan)
            {
                // heartbeat graphcast
                _leaderExpirationTime = DateTime.Now.AddSeconds(_leaderLeaseDuration);
                _graphcaster.SendGraphcast(new LeaderHeartbeatPacket(LocalFragmentLevel));
                _leaderUpdateTimer = 0;
            }
        }

        // follower checks for last heartbeat from any leader. if its coming upon the lease time, a new leader election will start from one or many nodes in the network
        private void _followerUpdate()
        {
            // if leader has expired
            if (DateTime.Now > _leaderExpirationTime)
            {
                Debug.LogError($"Node {controller.LocalNodeId} / fragment {LocalFragmentId}, didn't receive any update from leader for more than {_leaderLeaseDuration} secondes");

                // checking if highest node from edges
                for (int i = 0; i < _graphEdges.Count; ++i)
                {
                    if (_graphEdges[i].EdgeId > controller.LocalNodeId)
                    {
                        _nodeGraphState = NodeGraphState.Eliminated;
                        return;

                    }
                }

                // a  node will launch a take lead if its the highest ID in its local graph network (direct connections)
                _lastTakeLeadRequestReceived = DateTime.Now;
                _nodeGraphState = NodeGraphState.LeaderPropesct;
                _currentPendingLeadId = controller.LocalNodeId;

                SendTakeLeadRequestPacket();
                //_graphcaster.SendGraphcast(new TakeLeadRequestPacket(controller.LocalNodeId, controller.LocalNodeAdress));
            }
        }

        [Button]
        private void SendTakeLeadRequestPacket()
        {
            _graphcaster.SendGraphcast(new TakeLeadRequestPacket(controller.LocalNodeId, controller.LocalNodeAdress));
        }

        private void OnLeaderHeartbeatPacketReceived(LeaderHeartbeatPacket packet)
        {
            if (packet.broadcasterID != controller.LocalNodeId)
            {
                // do I really wanne do that here ? probly not
                _currentPendingLeadId = -1;
                _nodeGraphState = NodeGraphState.Follower;

                _leaderExpirationTime = DateTime.Now.AddSeconds(_leaderLeaseDuration);
            }
        }


        #endregion

        #region New leader election

        private void OnTakeLeadRequestPacketReceived(TakeLeadRequestPacket packet)
        {
            _lastTakeLeadRequestReceived = DateTime.Now;

            if (packet.prospectId > _currentPendingLeadId)
            {
                _nodeGraphState = NodeGraphState.Eliminated;
                _currentPendingLeadId = packet.prospectId;

                _graphcaster.RelayGraphcast(packet);
            }
            else
            {
                if(controller.LocalNodeId >  _currentPendingLeadId)
                {
                    _nodeGraphState = NodeGraphState.LeaderPropesct;
                    _graphcaster.SendGraphcast(new TakeLeadRequestPacket(controller.LocalNodeId, controller.LocalNodeAdress));
                }
                //_graphcaster.SendGraphcast(new TakeLeadRequestPacket(controller.LocalNodeId, controller.LocalNodeAdress));

            }
            /*if (_nodeGraphState == NodeGraphState.Follower)
            {
                // firstly check if the node is also unleased from the fragment leader 
                // if the node thinks the fragment leader is ok, the message is discarded
                if (DateTime.Now < _leaderExpirationTime)
                    return;

                if (packet.prospectId > controller.LocalNodeId)
                {
                    _currentPendingLeadId = packet.prospectId;

                    _nodeGraphState = NodeGraphState.Eliminated;
                    _graphcaster.RelayGraphcast(packet);

                    Debug.Log($"New current pending lead is {_currentPendingLeadId}");
                }
                else
                {
                    _currentPendingLeadId = controller.LocalNodeId;
                    _nodeGraphState = NodeGraphState.LeaderPropesct;

                    _graphcaster.SendGraphcast(new TakeLeadRequestPacket(controller.LocalNodeId, controller.LocalNodeAdress));

                    Debug.Log($"Local node has highest ID !  New current pending lead is {_currentPendingLeadId}");
                }
            }
            else
            {
                if (packet.prospectId > _currentPendingLeadId)
                {
                    _nodeGraphState = NodeGraphState.Eliminated;
                    _currentPendingLeadId = packet.prospectId;
                }

                _graphcaster.RelayGraphcast(packet);
            }*/
        }

        #endregion

        #region Minimum outgoing edge finding
        /*        private async Task<bool> FindMinimumOutgoingEdgeTask()
                {
                    *//*if (_isSearchingMOE)
                        return false;
        *//*
                    if (FragmentID != controller.LocalNodeId)
                    {
                        Debug.LogError("MOE searching is limited to fragment leader");
                        return false;
                    }

                    _isSearchingMOE = true;

                    // finding the moe in a fragment is handled by a special broadcast that is issued to the fragment only
                    // every fragment node will check its connections and see if there is a connection that is from another fragment 

                    // simplified version for now, without messaging but direct calculus
                    var outerConnections = new List<(PeerInfo, PeerInfo)>();
                    var outerFragmentId = new List<long>();
                    var outerFragmentLevel = new List<int>();

                    foreach (var n in WorldSimulationManager.nodeAddresses)
                    {
                        if (n.Value.graphEntityComponent.LocalFragmentId != LocalFragmentId)
                            continue;

                        for (int j = 0; j < n.Value.networkHandling.Connections.Count; ++j)
                        {
                            var connpinfo = n.Value.networkHandling.Connections.ElementAt(j);
                            var conmember = WorldSimulationManager.nodeAddresses[connpinfo.Value.peerAdress];
                            if (conmember.graphEntityComponent.FragmentID != FragmentID)
                            {
                                outerConnections.Add(new(n.Value.networkHandling.LocalPeerInfo, connpinfo.Value));
                                outerFragmentId.Add(conmember.graphEntityComponent.LocalFragmentId);
                                outerFragmentLevel.Add(conmember.graphEntityComponent.LocalFragmentLevel);
                            }
                        }
                    }

                    if (outerConnections.Count > 0)
                    {
                        var best = float.MinValue;
                        var bestIndex = 0;

                        for (int i = 0; i < outerConnections.Count; ++i)
                        {
                            var score = outerConnections[i].Item2.score;
                            if (score > best)
                            {
                                best = score;
                                bestIndex = i;
                            }
                        }

                        // bestInfo is moe
                        MinimumOutgoingEdge = new MinimumOutgoingEdge(outerConnections[bestIndex].Item1, outerConnections[bestIndex].Item2, outerFragmentId[bestIndex], outerFragmentLevel[bestIndex], _minimumOutgoingEdgeExpirationDelay);
                        _graphcaster.SendGraphcast(new MinimumOutgoingEdgePacket(MinimumOutgoingEdge));

                        _isSearchingMOE = false;

                        return true;
                    }
                    else
                    {
                        MinimumOutgoingEdge = null;
                        Debug.Log("No outer connection found from fragment " + FragmentID);
                        _isSearchingMOE = false;
                        _isGraphCreationCompleted = true;

                        return false;
                    }
                }
        */
        [Button]
        // finding the MOE of the updated fragment
        // connect to it
        private void SearchMinimumOutgoingEdgeAndRelayLead()
        {
            if (FragmentID != controller.LocalNodeId)
            {
                Debug.LogError("MOE searching is limited to fragment leader");
                return;
            }

            _isSearchingMOE = true;
            // finding the moe in a fragment is handled by a special broadcast that is issued to the fragment only
            // every fragment node will check its connections and see if there is a connection that is from another fragment 

            // simplified version for now, without messaging but direct calculus
            var outerConnections = new List<(PeerInfo, PeerInfo)>();
            var outerFragmentId = new List<long>();
            var outerFragmentLevel = new List<int>();

            foreach (var n in WorldSimulationManager.nodeAddresses)
            {
                if (n.Value.graphEntityComponent.LocalFragmentId != LocalFragmentId)
                    continue;

                for (int j = 0; j < n.Value.networkHandling.Connections.Count; ++j)
                {
                    var connpinfo = n.Value.networkHandling.Connections.ElementAt(j);
                    var conmember = WorldSimulationManager.nodeAddresses[connpinfo.Value.peerAdress];
                    if (conmember.graphEntityComponent.FragmentID != FragmentID)
                    {
                        outerConnections.Add(new(n.Value.networkHandling.LocalPeerInfo, connpinfo.Value));
                        outerFragmentId.Add(conmember.graphEntityComponent.LocalFragmentId);
                        outerFragmentLevel.Add(conmember.graphEntityComponent.LocalFragmentLevel);
                    }
                }
            }

            _isSearchingMOE = false;

            if (outerConnections.Count > 0)
            {
                var best = float.MinValue;
                var bestIndex = 0;

                for (int i = 0; i < outerConnections.Count; ++i)
                {
                    var score = outerConnections[i].Item2.score;
                    if (score > best)
                    {
                        best = score;
                        bestIndex = i;
                    }
                }

                MinimumOutgoingEdge = new MinimumOutgoingEdge(outerConnections[bestIndex].Item1, outerConnections[bestIndex].Item2, outerFragmentId[bestIndex], outerFragmentLevel[bestIndex], _minimumOutgoingEdgeExpirationDelay);

                // bestInfo is moe
                _graphcaster.SendGraphcast(new MinimumOutgoingEdgePacket(MinimumOutgoingEdge));

                ExecuteNextFragmentJoiningRequest(LocalFragmentId);
            }
            else
            {
                MinimumOutgoingEdge = null;
                _isGraphCreationCompleted = true;
                Debug.Log("No outer connection found from fragment " + FragmentID);
            }
        }

        private void ExecuteNextFragmentJoiningRequest(long oldLeaderId)
        {
            /// gérer ici le buffering ?

            // if leader is not MOE, it send a graphcast for leader swap to the actual inner MOE
            if (MinimumOutgoingEdge.InnerFragmentNode.peerID != controller.LocalNodeId)
            {
                RelayLeaderRoleToCurrentMinimumOutgoingEdge(oldLeaderId);
            }
            // if local node / leader is already the MOE, it will just send the request
            else
            {

                SendFragmentJoiningRequest(MinimumOutgoingEdge.OuterFragmentNode);
            }
        }

        private void RelayLeaderRoleToCurrentMinimumOutgoingEdge(long oldLeaderId)
        {
            Debug.LogError($"Relay LeaderRole To CurrentMinimumOutgoingEdge => from {oldLeaderId} to {LocalFragmentId}");

            // giving up on the lead
            FragmentID = MinimumOutgoingEdge.InnerFragmentNode.peerID;
            _nodeGraphState = NodeGraphState.Follower;

            // from this point, the network has no more leader.
            // if the message doesn't achieve the new leader, an election will eventually occur
            _graphcaster.SendGraphcast(new FragmentLeaderSelectingPacket(oldLeaderId, MinimumOutgoingEdge.InnerFragmentNode.peerID, MinimumOutgoingEdge.OuterFragmentNode.peerID));
        }

        private void OnMinimumOutgoingEdgePacketReceived(MinimumOutgoingEdgePacket minimumOutgoingEdgePacket)
        {
            if (MinimumOutgoingEdge == null || minimumOutgoingEdgePacket.MinimumOutgoingEdge.expirationTime > MinimumOutgoingEdge.expirationTime)
                MinimumOutgoingEdge = minimumOutgoingEdgePacket.MinimumOutgoingEdge;

        }
        #endregion

        #region Joining request

        // the join is sent by the inner node of the minimum outgoing edge which connects the two fragments
        // if accepted, the sending node will become a graph edge
        public async void SendFragmentJoiningRequest(PeerInfo outgoingEdgeNodeInfo)
        {
            if (_outgoingJoiningRequestPeerId == -1)
            {
                _outgoingJoiningRequestPeerId = outgoingEdgeNodeInfo.peerID;
                _packetRouter.Send(outgoingEdgeNodeInfo.peerAdress, new FragmentJoiningRequestPacket(FragmentLevel, FragmentID));

                // awaik request timeout before sending one more
                await Task.Delay(1000);

                _outgoingJoiningRequestPeerId = -1;
            }
            else
            {
                Debug.LogError($"Node {controller.LocalNodeId} can't send joining request to {outgoingEdgeNodeInfo.peerID} because it has a pending request to {_outgoingJoiningRequestPeerId}");
            }
        }

        private void OnFragmentJoiningRequestResponseReceived(FragmentJoiningRequestValidated response)
        {
            // when the join request is accepted, the requested node has decided wether its a merge or an absorb and has
            // updated the fragment level if needed.
            // we simulate here without message the updating of all the fragment ids of the nodes of the fragment that has request a join

            var validation = (FragmentJoiningRequestValidated)response;
            // create the connection with the peer that asnwered psitively to our join request
            _createGraphEdge(response.senderID, response.senderAdress);

            //_fragmentData.OldFragmentIDs.Add(_fragmentData.FragmentID);

            FragmentLevel = validation.FragmentLevel;
            FragmentID = validation.FragmentId;

            // when a refreshing is received, the node will graphcast his known connections in case of they were also desynchronized from the graph
            if (validation.graphOperation == GraphOperations.RefreshExistingEdge)
            {
                Debug.Log($" {controller.name} got existing edge refresh from {validation.senderID}");
                _graphcaster.SendGraphcast(new FragmentUpdatingBroadcastPacket(validation.FragmentId, validation.FragmentLevel));
            }

            MinimumOutgoingEdge = null;
            _outgoingJoiningRequestPeerId = -1;
        }

        private void _createGraphEdge(long otherNodeId, string otherNodeAdress)
        {
            if (_graphEdges.Exists(t => t.EdgeId == otherNodeId))
            {
                Debug.LogError($"A connection already exists with the OUTGOING edge {otherNodeId}. ");
                return;
            }

            _graphEdges.Add(new GraphEdge(otherNodeId, otherNodeAdress));
        }
        #endregion

        #region Fragment updating / leader selecting
        private void OnFragmentUpdatingPacketReceived(FragmentUpdatingBroadcastPacket fragmentUpdatingBroadcastPacket)
        {
            if (fragmentUpdatingBroadcastPacket.broadcasterID == _outgoingJoiningRequestPeerId)
            {
                Debug.LogError("Received an update from outgoind joining request.");
            }

            FragmentLevel = fragmentUpdatingBroadcastPacket.newFragmentLevel;
            FragmentID = fragmentUpdatingBroadcastPacket.newFragmentId;
            //_fragmentData.OldFragmentIDs = fragmentUpdatingBroadcastPacket.outdatedFragmentIds;
            _leaderExpirationTime = DateTime.Now.AddSeconds(_leaderLeaseDuration);

            if (controller.LocalNodeId == fragmentUpdatingBroadcastPacket.newFragmentId)
            {
                if (_nodeGraphState == NodeGraphState.Leader)
                    return;

                // just a reset to be sure its done good (if the searching fails it will at list try to recompute one 
                MinimumOutgoingEdge = null;
                _nodeGraphState = NodeGraphState.Leader;

                // no leader selecting packet received but the local node appears to be a leader 
                // might happen with complex network scenario with crossing messages
                Debug.LogError("no leader selecting packet received but the local node appears to be a leader");
                SearchMinimumOutgoingEdgeAndRelayLead();
            }
            else
            {
                _currentPendingLeadId = -1;
                _nodeGraphState = NodeGraphState.Follower;
            }
        }

        private void OnFragmentLeaderSelectingPacketReceived(FragmentLeaderSelectingPacket packet)
        {
            FragmentID = packet.NewLeaderId;
            //_fragmentData.MinimumOutgoingEdge.
            _leaderExpirationTime = DateTime.Now.AddSeconds(_leaderLeaseDuration);

            if (controller.LocalNodeId == packet.NewLeaderId)
            {
                if (_nodeGraphState == NodeGraphState.Leader)
                    return;

                _nodeGraphState = NodeGraphState.Leader;

                // sending the JOIN to the outgoing edge
                if (_networkHandling.Connections.TryGetValue(packet.OutgoingEdgeId, out var outgoingEdge))
                {
                    Debug.Log($"Node {controller.LocalNodeAdress} has been selected by old fragment leader to be the new leader.");

                    SendFragmentJoiningRequest(outgoingEdge);
                }
                else
                {
                    Debug.LogError("New leader not connected to MOE anymore, recomputing MOE..");
                    SearchMinimumOutgoingEdgeAndRelayLead();
                }
            }
            else
            {
                _currentPendingLeadId = -1;
                _nodeGraphState = NodeGraphState.Follower;
            }

            _graphcaster.RelayGraphcast(packet);
        }

        #endregion

        #region Get Fragment Info
        private void SendGetNodeFragmentInfoPacket(string adress, Action<INetworkPacket> responseCallback)
        {
            _packetRouter.SendRequest(adress, new GetNodeFragmentInfoRequestPacket(), responseCallback);
        }

        private void OnGetNodeFragmentInfoPacketReceived(GetNodeFragmentInfoRequestPacket nodeFragmentInfoRequestPacket)
        {
            var response = (GetNodeFragmentInfoResponsePacket)nodeFragmentInfoRequestPacket.GetResponsePacket(nodeFragmentInfoRequestPacket);
            response.FragmentLevel = FragmentLevel;
            response.FragmentID = FragmentID;
            response.MinimumOutgoingEdge = MinimumOutgoingEdge;
            _packetRouter.SendResponse(nodeFragmentInfoRequestPacket, response);
        }
        #endregion

        #region Debug and visualization
        public void StopSearching()
        {
            StopAllCoroutines();
        }

        public void ResetGraphEdges()
        {
            _graphEdges.Clear();

            _isSearchingMOE = false;
            // at creation every node is its own fragment leader
            FragmentID = _networkHandling.LocalPeerInfo.peerID;
            FragmentLevel = 0;
            _isGraphCreationCompleted = false;
            _nodeGraphState = NodeGraphState.Leader;
        }

        public void DisplayDebugConnectionLines()
        {
            if (!isGraphRunning)
                return;

            var pos = transform.position;

            for (int i = 0; i < _graphEdges.Count; ++i)
            {
                var entity = WorldSimulationManager.nodeAddresses[_graphEdges[i].EdgeAdress];
                Debug.DrawLine(entity.transform.position + Vector3.down * .5f, pos + Vector3.down * .5f, Color.red);
            }
        }

#if UNITY_EDITOR
        void OnDrawGizmos()
        {
            if (!isGraphRunning)
                return;

            if (FragmentID == _networkHandling.LocalPeerInfo.peerID)
            {
                Gizmos.color = Color.cyan;
                Gizmos.DrawCube(controller.transform.position + Vector3.up * 2, Vector3.one);
                Gizmos.color = Color.white;
            }

            if (_currentPendingLeadId != -1)
            {
                if (_currentPendingLeadId == controller.LocalNodeId)
                {
                    if (_nodeGraphState == NodeGraphState.LeaderPropesct)
                        Gizmos.color = Color.green;
                    else
                        Gizmos.color = Color.red;

                    Gizmos.DrawCube(controller.transform.position + Vector3.up * 3, Vector3.one);
                    Gizmos.color = Color.white;
                }
            }
        }

#endif
        #endregion
    }

}
