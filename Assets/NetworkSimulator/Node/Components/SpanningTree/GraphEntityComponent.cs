﻿using Atom.Broadcasting;
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
using UnityEngine;

namespace Atom.Components.GraphNetwork
{
    [Serializable]
    public class GraphFragmentData
    {
        public long FragmentID = -1; // PEER ID OF THE FRAGMENT LEAD
        public int FragmentLevel = 0;

        public List<long> OldFragmentIDs = new List<long>();

        public List<PeerInfo> FragmentMembers = new List<PeerInfo>();

        public MinimumOutgoingEdge MinimumOutgoingEdge = null;

        public GraphFragmentData(short fragmentLevel, long fragmentLeaderId)
        {
            FragmentLevel = fragmentLevel;
            FragmentID = fragmentLeaderId;
        }

    }

    /// <summary>
    /// Representation of a connection with an outer node in the graph
    /// When a connection is accepted by a node, both sides creates an instance of edge with the adress of the other node
    /// All graph edges concatened at the network level represents the MST at the end of the procedure
    /// </summary>
    [Serializable, HideLabel]
    public class GraphEdge
    {
        [HorizontalGroup("GraphEdge")] public long EdgeId;
        [HorizontalGroup("GraphEdge")] public string EdgeAdress;

        public GraphEdge(long edgeId, string edgeAdress)
        {
            EdgeId = edgeId;
            EdgeAdress = edgeAdress;
        }
    }

    [Serializable]
    public class MinimumOutgoingEdge
    {
        public PeerInfo InnerFragmentNode;
        public PeerInfo OuterFragmentNode;
        public long OuterFragmentId;

        public MinimumOutgoingEdge(PeerInfo innerFragmentNode, PeerInfo outerFragmentNode, long outerFragmentId)
        {
            InnerFragmentNode = innerFragmentNode;
            OuterFragmentNode = outerFragmentNode;
            OuterFragmentId = outerFragmentId;
        }
    }

    public class GraphEntityComponent : MonoBehaviour, INodeComponent
    {
        public NodeEntity controller { get; set; }

        [Inject] private BroadcasterComponent _broadcaster;
        [Inject] private PacketRouter _packetRouter;
        [Inject] private NetworkConnectionsComponent _networkHandling;
        [Inject] private GraphcasterComponent _graphcaster;

        [Header("GraphEntityComponent")]
        [SerializeField] private GraphFragmentData _fragmentData;
        public bool IsPendingOutgoingEdge = false;

        public long LocalFragmentId => _fragmentData.FragmentID;
        public bool IsMinimumOutgoinEdge
        {
            get
            {
                return _fragmentData != null && _fragmentData.MinimumOutgoingEdge != null && _fragmentData.MinimumOutgoingEdge.InnerFragmentNode.peerID == _networkHandling.LocalPeerInfo.peerID;
            }
        }

        [SerializeField] private List<GraphEdge> _graphEdges = new List<GraphEdge>();
        public List<GraphEdge> graphEdges => _graphEdges;

        [ShowInInspector, ReadOnly] private long _outgoingJoiningRequestPeerId = -1;
        [ShowInInspector, ReadOnly] private List<FragmentJoiningRequestPacket> _incomingJoiningRequestBuffer = new List<FragmentJoiningRequestPacket>();

        public void OnInitialize()
        {
            _broadcaster.RegisterPacketHandlerWithMiddleware(typeof(SpanningTreeCreationBroadcastPacket), (packet) => { OnSpanningTreeCreationBroadcastPacketReceived((SpanningTreeCreationBroadcastPacket)packet); }, true);

            _packetRouter.RegisterPacketHandler(typeof(FragmentJoiningRequestPacket), (packet) => { OnJoiningFragmentRequestReceived((FragmentJoiningRequestPacket)packet); });
            _packetRouter.RegisterPacketHandler(typeof(FragmentJoiningRequestValidated), (packet) => { OnFragmentJoiningRequestResponseReceived((FragmentJoiningRequestValidated)packet); });

            _graphcaster.RegisterGraphcast(typeof(FragmentUpdatingBroadcastPacket), (packet) => { OnFragmentUpdatingReceived((FragmentUpdatingBroadcastPacket)packet); }, true);
            _graphcaster.RegisterGraphcast(typeof(FragmentLeaderSelectingPacket), (packet) => { OnFragmentLeaderSelectingPacket((FragmentLeaderSelectingPacket)packet); }, false);
            _graphcaster.RegisterGraphcast(typeof(FragmentJoiningRequestRelayedGraphcastPacket), (packet) =>
            {
                var rel = (FragmentJoiningRequestRelayedGraphcastPacket)packet;
                if (_fragmentData.FragmentID == controller.LocalNodeId)
                {
                    if(_fragmentData.MinimumOutgoingEdge == null)
                    {
                        Debug.LogError($"{rel.originAdress} requested a join over the fragment {LocalFragmentId} that has been relayed by {rel.broadcasterID}. No minimum outgoing edge here so recomputing.");

                        FindMoeAndSendJoinRequest();
                        return;
                    }

                    if(rel.joinerfragmentLevel < _fragmentData.FragmentLevel || rel.joinerFragmentId == _fragmentData.MinimumOutgoingEdge.OuterFragmentId)
                    {
                        Debug.LogError($"{rel.originAdress} requested a join over the fragment {LocalFragmentId}. {rel.broadcasterID} should become MOE/Leader for this connection to happen.");
                        if(rel.broadcasterID > rel.originId)
                        {
                            // the broadcaster (the inner fragment node that received the request) has a higher id so he is able to handle a leader switch
                            Debug.LogError($"Leader switching from {LocalFragmentId} to {rel.broadcasterID}");
                            _graphcaster.SendGraphcast(new FragmentLeaderSelectingPacket(LocalFragmentId, rel.broadcasterID, rel.originId));
                        }

                        /*var relayed = new FragmentJoiningRequestPacket(rel.joinerfragmentLevel, rel.joinerFragmentId);
                        relayed.senderAdress = rel.originAdress;
                        relayed.senderID = rel.originId;

                        OnJoiningFragmentRequestReceived(relayed);*/
                    }
                    return;
                }

                _graphcaster.RelayGraphcast(rel);
            }, false);
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

            StartCoroutine(_waitAndSendFragmentJoiningRequestPacket());
        }

        private IEnumerator _waitAndSendFragmentJoiningRequestPacket()
        {
            yield return new WaitForSeconds(NodeRandom.Range(1.75f, 2.25f));
            // every node create a local fragment where she is the leader and the single member

            // at the first level, finding the Minimum outgoing edge is pretty straght-forward as we only need to iterate over connections and find the highest score (which in AtomNetworking context represented the minimum outgoing edge)
            StartCoroutine(MoeSearching());

            //FindMoeAndSendJoinRequest();

        }

        private void OnJoiningFragmentRequestReceived(FragmentJoiningRequestPacket joiningRequestPacket)
        {
            // node cannot handle a received request while they have sent one ?
            /*if (_hasOutgoingJoiningRequestPending)
            {
                _incomingJoiningRequestBuffer.Add(joiningRequestPacket);
                return;
            }
*/
            if (joiningRequestPacket.joinerFragmentId == LocalFragmentId)
                return;

            /*if (joiningRequestPacket.senderID == _outgoingJoiningRequestPeerId)
            {
                Debug.LogError($"Received an incoming request from the current outgoing request to {_outgoingJoiningRequestPeerId}. Ignoring");
                return;
            }*/

            if (_graphEdges.Exists(t => t.EdgeId == joiningRequestPacket.senderID))
            {
                Debug.LogError($"A connection already exists with the JOINER edge {joiningRequestPacket.senderID}. Refreshing connection to the requester ");

                var response = new FragmentJoiningRequestValidated(controller.LocalNodeId, controller.LocalNodeAdress, _fragmentData.FragmentID, _fragmentData.FragmentLevel, GraphOperations.RefreshExistingEdge);               
                _packetRouter.Send(joiningRequestPacket.senderAdress, response);

                return;
            }

            if (_fragmentData.FragmentID != _networkHandling.LocalPeerInfo.peerID && !IsPendingOutgoingEdge && !IsMinimumOutgoinEdge)
            {
                Debug.LogError($"Request received from {joiningRequestPacket.senderAdress} but local node is not a leader {_networkHandling.LocalPeerInfo.peerAdress}");

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
                    return;
                }

                if (joiningRequestPacket.joinerFragmentId == _fragmentData.MinimumOutgoingEdge.OuterFragmentId)
                {
                    MergeWithFragment(joiningRequestPacket);
                }
            }
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

            IsPendingOutgoingEdge = false;
            _fragmentData.MinimumOutgoingEdge = null;

            StartCoroutine(_waitAndSendFragmentJoiningRequestPacket());
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
            IsPendingOutgoingEdge = false;

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

            // the node then reply to the requester that the merge happens
            // the requester wil update itself and graphcast to its local network before creating the connection
            _packetRouter.Send(joiningRequestPacket.senderAdress, response);


            StartCoroutine(_waitAndSendFragmentJoiningRequestPacket());
        }

        [Button]
        // finding the MOE of the updated fragment
        // connect to it
        private void FindMoeAndSendJoinRequest()
        {
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

                var oldLeaderId = LocalFragmentId;
                // bestInfo is moe
                _fragmentData.MinimumOutgoingEdge = new MinimumOutgoingEdge(outerConnections[bestIndex].Item1, outerConnections[bestIndex].Item2, outerFragmentId[bestIndex]);

                // if leader is not MOE, it send a graphcast for leader swap to the actual inner MOE
                if (_fragmentData.MinimumOutgoingEdge.InnerFragmentNode.peerID != controller.LocalNodeId)
                {
                    _fragmentData.FragmentID = _fragmentData.MinimumOutgoingEdge.InnerFragmentNode.peerID;
                    _graphcaster.SendGraphcast(new FragmentLeaderSelectingPacket(oldLeaderId, _fragmentData.MinimumOutgoingEdge.InnerFragmentNode.peerID, _fragmentData.MinimumOutgoingEdge.OuterFragmentNode.peerID));
                }
                // if local node / leader is already the MOE, it will just send the request
                else
                {
                    SendFragmentJoiningRequest(_fragmentData.MinimumOutgoingEdge.OuterFragmentNode);
                }
            }
            else
            {
                _fragmentData.MinimumOutgoingEdge = null;
                Debug.Log("No outer connection found from fragment " + _fragmentData.FragmentID);
            }
        }

        private void OnFragmentLeaderSelectingPacket(FragmentLeaderSelectingPacket packet)
        {
            _fragmentData.FragmentID = packet.NewLeaderId;
            //_fragmentData.MinimumOutgoingEdge.

            if (controller.LocalNodeId == packet.NewLeaderId)
            {
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

            _graphcaster.RelayGraphcast(packet);
        }

        // the join is sent by the inner node of the minimum outgoing edge which connects the two fragments
        // if accepted, the sending node will become a graph edge
        public async void SendFragmentJoiningRequest(PeerInfo outgoingEdgeNodeInfo)
        {
            _outgoingJoiningRequestPeerId = outgoingEdgeNodeInfo.peerID;
            _packetRouter.Send(outgoingEdgeNodeInfo.peerAdress, new FragmentJoiningRequestPacket(_fragmentData.FragmentLevel, _fragmentData.FragmentID));

            await Task.Delay(3000);

            Debug.LogError($"{controller.LocalNodeAdress} : joining request timed out . Recomputing moe and resending new.");
            if(_outgoingJoiningRequestPeerId != -1)
            {
                _outgoingJoiningRequestPeerId = -1;
                FindMoeAndSendJoinRequest();
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

            IsPendingOutgoingEdge = false;

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


        private void OnFragmentUpdatingReceived(FragmentUpdatingBroadcastPacket fragmentUpdatingBroadcastPacket)
        {
            if (fragmentUpdatingBroadcastPacket.broadcasterID == _outgoingJoiningRequestPeerId)
            {
                Debug.LogError("Received an update from outgoind joining request.");
            }
            /*for (int i = 0; i < fragmentUpdatingBroadcastPacket.outdatedFragmentIds.Count; i++)
            {
                if (_fragmentData.OldFragmentIDs.Contains(fragmentUpdatingBroadcastPacket.outdatedFragmentIds[i]))
                {
                    Debug.LogError("HERE ");
                }

                if (fragmentUpdatingBroadcastPacket.outdatedFragmentIds[i] == LocalFragmentId)
                {
                    Debug.Log($"Node {controller.name} with fragment id {LocalFragmentId} set to new fragment {fragmentUpdatingBroadcastPacket.newFragmentId}");
                    _fragmentData.FragmentLevel = fragmentUpdatingBroadcastPacket.newFragmentLevel;
                    _fragmentData.FragmentID = fragmentUpdatingBroadcastPacket.newFragmentId;
                    _fragmentData.OldFragmentIDs = fragmentUpdatingBroadcastPacket.outdatedFragmentIds;
                    break;
                }
            }*/

            _fragmentData.FragmentLevel = fragmentUpdatingBroadcastPacket.newFragmentLevel;
            _fragmentData.FragmentID = fragmentUpdatingBroadcastPacket.newFragmentId;
            _fragmentData.OldFragmentIDs = fragmentUpdatingBroadcastPacket.outdatedFragmentIds;
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

        public void StopSearching()
        {
            StopAllCoroutines();
        }

        public void ResetGraphEdges()
        {
            _graphEdges.Clear();

            // at creation every node is its own fragment leader
            _fragmentData = new GraphFragmentData(0, _networkHandling.LocalPeerInfo.peerID);
            _fragmentData.FragmentMembers.Add(_networkHandling.LocalPeerInfo);
            IsPendingOutgoingEdge = true;
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
        }

#endif
    }
}
