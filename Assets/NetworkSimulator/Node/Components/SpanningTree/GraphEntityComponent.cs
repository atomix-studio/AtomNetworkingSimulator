using Atom.Broadcasting;
using Atom.CommunicationSystem;
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
        [SerializeField] private float _minimumOutgoingEdgeExpirationDelay = 2;
        [Space]
        [SerializeField] private GraphFragmentData _fragmentData;


        [ShowInInspector, ReadOnly] private long _outgoingJoiningRequestPeerId = -1;
        [ShowInInspector, ReadOnly] private NodeGraphState _nodeGraphState;
        [ShowInInspector, ReadOnly] private List<FragmentJoiningRequestPacket> _bufferedMergingRequestBuffer = new List<FragmentJoiningRequestPacket>();
        [SerializeField] private List<GraphEdge> _graphEdges = new List<GraphEdge>();

        private float _leaderUpdateTimer = 0;
        private bool _isSearchingMOE = false;
        private DateTime _leaderExpirationTime;
        private bool _isGraphRunning = false;

        public List<GraphEdge> graphEdges => _graphEdges;
        public bool isGraphRunning => _isGraphRunning;

        public long LocalFragmentId =>  _fragmentData.FragmentID;
        public int LocalFragmentLevel => _fragmentData.FragmentLevel;

        public bool IsMinimumOutgoinEdge
        {
            get
            {
                return _fragmentData != null && _fragmentData.MinimumOutgoingEdge != null && _fragmentData.MinimumOutgoingEdge.InnerFragmentNode.peerID == _networkHandling.LocalPeerInfo.peerID;
            }
        }

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
            _graphcaster.RegisterGraphcast(typeof(DiscardTakeLeadPacket), (packet) => OnNewLeadTakeControl((DiscardTakeLeadPacket)packet), false);
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
            FindMoeAndSendJoinRequest();
        }

        #region Fragment JOIN
        private void OnJoiningFragmentRequestReceived(FragmentJoiningRequestPacket joiningRequestPacket)
        {
            if (joiningRequestPacket.joinerFragmentId == LocalFragmentId)
                return;

            /*if (joiningRequestPacket.senderID == _outgoingJoiningRequestPeerId)
            {
                Debug.LogError($"Received an incoming request from the current outgoing request to {_outgoingJoiningRequestPeerId}. Ignoring");
                return;
            }*/

            for (int i = 0; i < _graphEdges.Count; ++i)
            {
                if (_graphEdges[i]?.EdgeId == joiningRequestPacket?.senderID)
                {
                    Debug.Log($"A connection already exists with the JOINER edge {joiningRequestPacket.senderID}/{joiningRequestPacket.joinerFragmentId}. Refreshing connection to the requester {controller.LocalNodeId}/{LocalFragmentId}");

                    var response = new FragmentJoiningRequestValidated(controller.LocalNodeId, controller.LocalNodeAdress, _fragmentData.FragmentID, _fragmentData.FragmentLevel, GraphOperations.RefreshExistingEdge);
                    _packetRouter.Send(joiningRequestPacket.senderAdress, response);

                    return;
                }
            }

            if (_fragmentData.FragmentID != _networkHandling.LocalPeerInfo.peerID)
            {
                Debug.Log($"Request received from {joiningRequestPacket.senderAdress} but local node is not a leader {_networkHandling.LocalPeerInfo.peerAdress}");

                _graphcaster.SendGraphcast(new FragmentJoiningRequestRelayedGraphcastPacket(joiningRequestPacket.senderID, joiningRequestPacket.senderAdress, joiningRequestPacket.joinerfragmentLevel, joiningRequestPacket.joinerFragmentId));

                // if the requester is the moe (outer), he will 
                return;
            }

            // absorbtion if the requester is lower level
            if (joiningRequestPacket.joinerfragmentLevel < _fragmentData.FragmentLevel)
            {
                AbsorbFragment(joiningRequestPacket);
            }
            else
            {
                if (_fragmentData.MinimumOutgoingEdge == null)
                {
                    _bufferedMergingRequestBuffer.Add(joiningRequestPacket);

                    if (!_isSearchingMOE)
                        FindMoeAndSendJoinRequest();
                    // buffering ?
                    return;
                }

                if (joiningRequestPacket.joinerFragmentId == _fragmentData.MinimumOutgoingEdge.OuterFragmentId
                    || joiningRequestPacket.senderID == _fragmentData.MinimumOutgoingEdge.OuterFragmentNode.peerID)
                {
                    MergeWithFragment(joiningRequestPacket);
                }
                else
                {
                    if (_fragmentData.MinimumOutgoingEdge.hasExpired)
                    {
                        TryRefreshOutgoingEdge();
                    }
                }
            }
        }

        // send a request to the local moe to see if the 'situation' as somehow changed.
        // the moe could be outdated and depending on this, the leader will either try to find a new moe OR to send a new join request
        private void TryRefreshOutgoingEdge()
        {
            // avoiding cycling call while refreshing is running
            _fragmentData.MinimumOutgoingEdge.Refresh();

            // if the outerframgent id dosn't correspond, the node checks if the current MOE is still alive and if the current MOE has still the same fragmentId, that could have changed over time
            SendGetNodeFragmentInfoPacket(_fragmentData.MinimumOutgoingEdge.OuterFragmentNode.peerAdress, (response) =>
            {
                if (response == null)
                {
                    // outgoing edge not responding, searching new outgoing edge
                    FindMoeAndSendJoinRequest();
                    return;
                }

                var resp = (GetNodeFragmentInfoResponsePacket)response;
                if (resp.graphFragmentData.FragmentID != _fragmentData.MinimumOutgoingEdge.OuterFragmentId || resp.graphFragmentData.FragmentLevel != _fragmentData.MinimumOutgoingEdge.OuterFragmentLevel)
                {
                    FindMoeAndSendJoinRequest();
                    return;
                }
                else
                {
                    if (_outgoingJoiningRequestPeerId == -1)
                    {
                        Debug.LogError($"The outgoing edge {resp.graphFragmentData.FragmentID} hasn't changed. Trying to call the JOIN from this node.");
                        SendFragmentJoiningRequest(_fragmentData.MinimumOutgoingEdge.OuterFragmentNode);
                    }
                    else
                    {
                        Debug.LogError($"What to do know ? {LocalFragmentId}");

                        SendFragmentJoiningRequest(_fragmentData.MinimumOutgoingEdge.OuterFragmentNode);

                    }
                }
            });
        }

        private void AbsorbFragment(FragmentJoiningRequestPacket joiningRequestPacket)
        {
            Debug.Log(_networkHandling.LocalPeerInfo.peerAdress + "absorbing" + joiningRequestPacket.senderAdress);

            if (_fragmentData.OldFragmentIDs.Contains(joiningRequestPacket.joinerFragmentId))
            {
                Debug.LogError("AbsorbFragment => Fragments have already been merge ? " + joiningRequestPacket.joinerFragmentId + "  " + LocalFragmentId);
                return;
            }

            var response = new FragmentJoiningRequestValidated(controller.LocalNodeId, controller.LocalNodeAdress, _fragmentData.FragmentID, _fragmentData.FragmentLevel, GraphOperations.Absorbed);

            _createGraphEdge(joiningRequestPacket.senderID, joiningRequestPacket.senderAdress);
            _packetRouter.Send(joiningRequestPacket.senderAdress, response);

            // graph cast absorbed + local fragment to set absorbed nodes fragment level adn id
            _graphcaster.SendGraphcast(new FragmentUpdatingBroadcastPacket(joiningRequestPacket.senderID, _fragmentData.FragmentLevel, _fragmentData.OldFragmentIDs));

            _fragmentData.MinimumOutgoingEdge = null;

            StartCoroutine(_waitAndExecuteLeaderRoutine());
        }

        private void MergeWithFragment(FragmentJoiningRequestPacket joiningRequestPacket)
        {
            Debug.LogError(_networkHandling.LocalPeerInfo.peerAdress + " merging" + joiningRequestPacket.senderAdress);
            // in a merge between fragments, the node with the highest id between the two moe speaking becomes the new leader
            // if messages are crossing, the nodes will fall in the same state separately

            if (_fragmentData.OldFragmentIDs.Contains(joiningRequestPacket.joinerFragmentId))
            {
                Debug.LogError("MergeWithFragment => Fragments have already been merge ? " + joiningRequestPacket.joinerFragmentId + "  " + LocalFragmentId);
                return;
            }
            _fragmentData.MinimumOutgoingEdge = null;

            // create a new fragment level + 1 with all members from the two merging fragments
            // respond MERGING
            _fragmentData.FragmentLevel++;
            //_fragmentData.OldFragmentIDs.AddRange(joiningRequestPacket.oldFragmentIds);

            var response = new FragmentJoiningRequestValidated(controller.LocalNodeId, controller.LocalNodeAdress);

            _createGraphEdge(joiningRequestPacket.senderID, joiningRequestPacket.senderAdress);

            // the node then reply to the requester that the merge happens
            // the requester wil update itself and graphcast to its local network before creating the connection
            _packetRouter.Send(joiningRequestPacket.senderAdress, response);


            if (controller.LocalNodeId > joiningRequestPacket.senderID)
            {
                Debug.Log($"Merging leader swap from {_fragmentData.FragmentID} to {controller.LocalNodeId} ");
                _fragmentData.OldFragmentIDs.Add(_fragmentData.FragmentID);
                _fragmentData.FragmentID = controller.LocalNodeId;

                response.FragmentId = controller.LocalNodeId;
                response.FragmentLevel = _fragmentData.FragmentLevel;
                response.graphOperation = GraphOperations.Merged;

                // local leader changing, this is graphCasted in the local fragment before the fragment is actually merged
                _graphcaster.SendGraphcast(new FragmentUpdatingBroadcastPacket(controller.LocalNodeId, _fragmentData.FragmentLevel, _fragmentData.OldFragmentIDs));
            }
            else
            {
                Debug.Log($"Merging leader swap from {_fragmentData.FragmentID} to {joiningRequestPacket.senderID} ");
                _fragmentData.OldFragmentIDs.Add(_fragmentData.FragmentID);
                _fragmentData.FragmentID = joiningRequestPacket.senderID;

                response.FragmentId = joiningRequestPacket.senderID;
                response.FragmentLevel = _fragmentData.FragmentLevel;
                response.graphOperation = GraphOperations.Merged;

                // local leader changing, this is graphCasted in the local fragment before the fragment is actually merged
                _graphcaster.SendGraphcast(new FragmentUpdatingBroadcastPacket(joiningRequestPacket.senderID, _fragmentData.FragmentLevel, _fragmentData.OldFragmentIDs));
            }

            StartCoroutine(_waitAndExecuteLeaderRoutine());
        }

        // if a node receive a JOIN but its node lead/moe, it will follows it to the leader via cast
        private void OnFragmentJoiningRelayedGraphcastPacketReceived(FragmentJoiningRequestRelayedGraphcastPacket rel)
        {
            if (_fragmentData.FragmentID == controller.LocalNodeId)
            {
                if (_fragmentData.MinimumOutgoingEdge == null)
                {
                    Debug.LogError($"{rel.originAdress} requested a join over the fragment {LocalFragmentId} that has been relayed by {rel.broadcasterID}. No minimum outgoing edge here so recomputing.");

                    FindMoeAndSendJoinRequest();
                    return;
                }

                if (rel.joinerfragmentLevel < _fragmentData.FragmentLevel || rel.joinerFragmentId == _fragmentData.MinimumOutgoingEdge.OuterFragmentId)
                {
                    Debug.LogError($"{rel.originAdress} requested a join over the fragment {LocalFragmentId}. {rel.broadcasterID} should become MOE/Leader for this connection to happen.");
                    //if(rel.broadcasterID > rel.originId)
                    {
                        // the broadcaster is the node within the fragment that received the JOIN message
                        // it happened that the JOINER is actually the best outgoing fragment
                        // only the node that is the MOE at a time can be leader of the fragment
                        // so the current leader will pass the relay to the broadcaster, who will handle the joining logic
                        Debug.LogError($"Leader switching from {LocalFragmentId} to {rel.broadcasterID}");
                        _graphcaster.SendGraphcast(new FragmentLeaderSelectingPacket(LocalFragmentId, rel.broadcasterID, rel.originId));
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

            switch (_nodeGraphState)
            {
                case NodeGraphState.Follower:
                    _followerUpdate();
                    break;
                case NodeGraphState.Leader:
                    _leaderUpdate();
                    break;
                case NodeGraphState.LeaderPropesct:
                    if (DateTime.Now > _lastTakeLeadRequestReceived.AddSeconds(4))
                    {
                        Debug.LogError($"{controller.LocalNodeAdress} is the new leader of fragment {_fragmentData.FragmentID}");
                        _leaderUpdateTimer = 0;
                        _fragmentData.FragmentID = controller.LocalNodeId;
                        _nodeGraphState = NodeGraphState.Leader;
                        // replace here by dedicated take lead packet
                        _graphcaster.SendGraphcast(new FragmentUpdatingBroadcastPacket(controller.LocalNodeId, _fragmentData.FragmentLevel, _fragmentData.OldFragmentIDs));
                        FindMoeAndSendJoinRequest();
                    }
                    break;
            }
        }

        private void _leaderUpdate()
        {
            if (_fragmentData.MinimumOutgoingEdge == null && !_isSearchingMOE)
            {
                FindMoeAndSendJoinRequest();
            }
            else if (_fragmentData.MinimumOutgoingEdge != null && _fragmentData.MinimumOutgoingEdge.hasExpired)
            {
                TryRefreshOutgoingEdge();
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
            if (packet.FragmentId != LocalFragmentId)
            {
                Debug.Log($"Node {controller.LocalNodeAdress} received a leader heartbeat from {packet.broadcasterID} but the local node fragment doesn't correspond {LocalFragmentId}");

                // what to do, resending a request as MOE to see what hapens ?
                // return;
            }

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

        [ReadOnly, ShowInInspector] private long _currentPendingLeadId = -1;
        private DateTime _lastTakeLeadRequestReceived;

        private void OnTakeLeadRequestPacketReceived(TakeLeadRequestPacket packet)
        {
            _lastTakeLeadRequestReceived = DateTime.Now;

            if (_nodeGraphState == NodeGraphState.Follower)
            {
                // firstly check if the node is also unleased from the fragment leader 
                // if the node thinks the fragment leader is ok, the message is discarded
                if (DateTime.Now < _leaderExpirationTime)
                    return;

                if (packet.propectId > controller.LocalNodeId)
                {
                    _currentPendingLeadId = packet.propectId;

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
                if (packet.propectId > _currentPendingLeadId)
                {
                    _nodeGraphState = NodeGraphState.Eliminated;
                    _currentPendingLeadId = packet.propectId;
                }

                _graphcaster.RelayGraphcast(packet);

                /*else if(packet.propectId < _currentPendingLeadId)
                {
                    _currentPendingLeadId = controller.LocalNodeId;
                    _nodeGraphState = NodeGraphState.LeaderPropesct;
                    _graphcaster.SendGraphcast(new TakeLeadRequestPacket(controller.LocalNodeId, controller.LocalNodeAdress));
                }*/
            }
        }

        private void OnNewLeadTakeControl(DiscardTakeLeadPacket packet) { }
        #endregion

        #region Minimum outgoing edge finding
        private async Task<bool> FindMinimumOutgoingEdgeTask()
        {
            /*if (_isSearchingMOE)
                return false;
*/
            if (_fragmentData.FragmentID != controller.LocalNodeId)
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
                    if (conmember.graphEntityComponent._fragmentData.FragmentID != _fragmentData.FragmentID)
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
                _fragmentData.MinimumOutgoingEdge = new MinimumOutgoingEdge(outerConnections[bestIndex].Item1, outerConnections[bestIndex].Item2, outerFragmentId[bestIndex], outerFragmentLevel[bestIndex], _minimumOutgoingEdgeExpirationDelay);
                _isSearchingMOE = false;

                return true;
            }
            else
            {
                _fragmentData.MinimumOutgoingEdge = null;
                Debug.Log("No outer connection found from fragment " + _fragmentData.FragmentID);
                _isSearchingMOE = false;

                return false;
            }
        }

        [Button]
        // finding the MOE of the updated fragment
        // connect to it
        private void FindMoeAndSendJoinRequest()
        {
            /*if (_isSearchingMOE)
                return;*/

            _isSearchingMOE = true;

            if (_fragmentData.FragmentID != controller.LocalNodeId)
            {
                Debug.LogError("MOE searching is limited to fragment leader");
                return;
            }

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
                    if (conmember.graphEntityComponent._fragmentData.FragmentID != _fragmentData.FragmentID)
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

                // bestInfo is moe
                _fragmentData.MinimumOutgoingEdge = new MinimumOutgoingEdge(outerConnections[bestIndex].Item1, outerConnections[bestIndex].Item2, outerFragmentId[bestIndex], outerFragmentLevel[bestIndex], _minimumOutgoingEdgeExpirationDelay);

                ExecuteNextFragmentJoiningRequest(LocalFragmentId);
            }
            else
            {
                _fragmentData.MinimumOutgoingEdge = null;
                Debug.Log("No outer connection found from fragment " + _fragmentData.FragmentID);
            }
        }

        private void ExecuteNextFragmentJoiningRequest(long oldLeaderId)
        {
            /// gérer ici le buffering ?

            // if leader is not MOE, it send a graphcast for leader swap to the actual inner MOE
            if (_fragmentData.MinimumOutgoingEdge.InnerFragmentNode.peerID != controller.LocalNodeId)
            {
                RelayLeaderRoleToCurrentMinimumOutgoingEdge(oldLeaderId);
            }
            // if local node / leader is already the MOE, it will just send the request
            else
            {
                /*if(_bufferedMergingRequestBuffer.Count > 0)
                {
                    
                }*/

                SendFragmentJoiningRequest(_fragmentData.MinimumOutgoingEdge.OuterFragmentNode);
            }
        }

        private void RelayLeaderRoleToCurrentMinimumOutgoingEdge(long oldLeaderId)
        {
            // giving up on the lead
            _fragmentData.FragmentID = _fragmentData.MinimumOutgoingEdge.InnerFragmentNode.peerID;
            _nodeGraphState = NodeGraphState.Follower;

            // from this point, the network has no more leader.
            // if the message doesn't achieve the new leader, an election will eventually occur
            _graphcaster.SendGraphcast(new FragmentLeaderSelectingPacket(oldLeaderId, _fragmentData.MinimumOutgoingEdge.InnerFragmentNode.peerID, _fragmentData.MinimumOutgoingEdge.OuterFragmentNode.peerID));
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
                _packetRouter.Send(outgoingEdgeNodeInfo.peerAdress, new FragmentJoiningRequestPacket(_fragmentData.FragmentLevel, _fragmentData.FragmentID));

                /*await Task.Delay(1500);
                _outgoingJoiningRequestPeerId = -1;

                if (_fragmentData.MinimumOutgoingEdge == null || _fragmentData.MinimumOutgoingEdge.hasExpired)
                {
                    var result = await FindMinimumOutgoingEdgeTask();

                    if (result)
                    {
                        ExecuteNextFragmentJoiningRequest(LocalFragmentId);
                    }
                    return;
                }
                else
                {
                    SendFragmentJoiningRequest(outgoingEdgeNodeInfo);
                }*/
            }
            else
            {
                await Task.Delay(1500);
                _outgoingJoiningRequestPeerId = -1;
                SendFragmentJoiningRequest(outgoingEdgeNodeInfo);
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

            _fragmentData.OldFragmentIDs.Add(_fragmentData.FragmentID);

            _fragmentData.FragmentLevel = validation.FragmentLevel;
            _fragmentData.FragmentID = validation.FragmentId;

            // when a refreshing is received, the node will graphcast his known connections in case of they were also desynchronized from the graph
            if (validation.graphOperation == GraphOperations.RefreshExistingEdge)
            {
                Debug.Log($" {controller.name} got existing edge refresh from {validation.senderID}");
                _graphcaster.SendGraphcast(new FragmentUpdatingBroadcastPacket(validation.FragmentId, validation.FragmentLevel, _fragmentData.OldFragmentIDs));
            }

            _fragmentData.MinimumOutgoingEdge = null;
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

            _fragmentData.FragmentLevel = fragmentUpdatingBroadcastPacket.newFragmentLevel;
            _fragmentData.FragmentID = fragmentUpdatingBroadcastPacket.newFragmentId;
            _fragmentData.OldFragmentIDs = fragmentUpdatingBroadcastPacket.outdatedFragmentIds;
            _leaderExpirationTime = DateTime.Now.AddSeconds(_leaderLeaseDuration);

            if (controller.LocalNodeId == fragmentUpdatingBroadcastPacket.newFragmentId)
            {
                if (_nodeGraphState == NodeGraphState.Leader)
                    return;

                _nodeGraphState = NodeGraphState.Leader;

                // no leader selecting packet received but the local node appears to be a leader 
                // might happen with complex network scenario with crossing messages
                Debug.LogError("no leader selecting packet received but the local node appears to be a leader");
                FindMoeAndSendJoinRequest();
            }
            else
            {
                _currentPendingLeadId = -1;
                _nodeGraphState = NodeGraphState.Follower;
            }
        }

        private void OnFragmentLeaderSelectingPacketReceived(FragmentLeaderSelectingPacket packet)
        {
            _fragmentData.FragmentID = packet.NewLeaderId;
            //_fragmentData.MinimumOutgoingEdge.

            if (controller.LocalNodeId == packet.NewLeaderId)
            {
                if (_nodeGraphState == NodeGraphState.Leader)
                    return;

                _nodeGraphState = NodeGraphState.Leader;

                Debug.Log($"Node {controller.LocalNodeAdress} has been selected by old fragment leader to be the new leader.");
                // sending the JOIN to the outgoing edge
                if (_networkHandling.Connections.TryGetValue(packet.OutgoingEdgeId, out var outgoingEdge))
                {
                    SendFragmentJoiningRequest(outgoingEdge);
                }
                else
                {
                    Debug.LogError("New leader not connected to MOE anymore, recomputing MOE..");
                    FindMoeAndSendJoinRequest();
                }
            }
            else
            {
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
            response.graphFragmentData = _fragmentData;
            _packetRouter.SendResponse(nodeFragmentInfoRequestPacket, response);
        }
        #endregion

        private IEnumerator MoeSearching()
        {
            float interval = UnityEngine.Random.Range(.75f, 1.2f);
            var wfs = new WaitForSeconds(interval);
            var wu = new WaitUntil(() => _outgoingJoiningRequestPeerId == -1);
            while (_fragmentData.FragmentID == _networkHandling.LocalPeerInfo.peerID)
            {
                yield return wfs;
                FindMoeAndSendJoinRequest();
                //yield return wu;
            }
        }

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
            _fragmentData = new GraphFragmentData(0, _networkHandling.LocalPeerInfo.peerID);
            _fragmentData.FragmentMembers.Add(_networkHandling.LocalPeerInfo);
            _nodeGraphState = NodeGraphState.Leader;
        }

        public void DisplayDebugConnectionLines()
        {
            if (_fragmentData == null)
                return;

            var pos = controller.transform.position;

            for (int i = 0; i < _graphEdges.Count; ++i)
            {
                var entity = WorldSimulationManager.nodeAddresses[_graphEdges[i].EdgeAdress];
                Debug.DrawLine(entity.transform.position + Vector3.down * .5f, transform.position + Vector3.down * .5f, Color.red);
            }
        }

#if UNITY_EDITOR
        void OnDrawGizmos()
        {
            if (_fragmentData == null)
                return;

            if (_fragmentData.FragmentID == _networkHandling.LocalPeerInfo.peerID)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawCube(controller.transform.position + Vector3.up * 2, Vector3.one);
                Gizmos.color = Color.white;
            }

            if (_currentPendingLeadId != -1)
            {
                if (_currentPendingLeadId == controller.LocalNodeId)
                {
                    Gizmos.color = Color.green;
                    Gizmos.DrawCube(controller.transform.position + Vector3.up * 2, Vector3.one);
                    Gizmos.color = Color.white;
                }
                /*else
                {
                    Gizmos.color = Color.cyan;
                    Gizmos.DrawCube(controller.transform.position + Vector3.up * 2, Vector3.one * .6f);
                    Gizmos.color = Color.white;

                }*/
            }
        }

#endif
        #endregion
    }

}
